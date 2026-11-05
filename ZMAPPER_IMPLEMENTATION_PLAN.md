# ZMapper — Implementation Analysis for AutoMapper Feature Parity

> Based on `ZMAPPER_MISSING.md` gap analysis against GIRS project (ASP.NET Core 8, ~150+ mappings, 15 profiles, 14 custom converters).
> Architecture: Roslyn incremental source generator (`ZMapperGenerator.cs`, ~26k lines).

---

## Implementation Roadmap

### Phase 1 — P0: Blocking for Migration (Est. complexity: Medium)

#### Feature 1: Map-level ConvertUsing (lambda)

**Priority:** P0 | **Complexity:** Low | **Risk:** Low

**What:** `CreateMap<S, D>().ConvertUsing(src => expression)` — replace entire property-by-property mapping with a single lambda expression.

**Files to modify:**

| File | Change |
|------|--------|
| `IMappingExpression.cs` | Add `IMappingExpression<S,D> ConvertUsing(Expression<Func<TSource, TDestination>> converter)` |
| `MappingExpression<S,D>` in `MapperConfiguration.cs` | Implement method, store expression on `MappingDefinition` |
| `MappingDefinition.cs` | Add `object? ConvertUsingExpression { get; set; }` |
| `ZMapperGenerator.cs` — extraction | Detect `.ConvertUsing(...)` call after `CreateMap`, extract lambda body |
| `ZMapperGenerator.cs` — models | Add `string? ConvertUsingExpression` to `MappingModel` |
| `ZMapperGenerator.cs` — generation | When `ConvertUsingExpression` is set, emit `return {expression};` instead of property-by-property code |

**Generator logic (extraction):**
```
In ExtractMappingFromCall():
  1. After detecting CreateMap, walk the fluent chain
  2. If InvocationExpression named "ConvertUsing" is found:
     a. Extract lambda argument (LambdaExpressionSyntax)
     b. Store lambda body as string in MappingModel.ConvertUsingExpression
     c. Replace parameter name with "source"
```

**Generator logic (code generation):**
```
In GenerateMapperCode() / GenerateUnifiedMapperCode():
  if (mapping.ConvertUsingExpression != null)
  {
      // Skip all property assignment logic
      // Emit: return {ConvertUsingExpression};
      // Skip MapArray/MapList generation OR adapt them to use the expression
  }
```

**Generated code example:**
```csharp
// CreateMap<ListDto, int>().ConvertUsing(s => s.Id);
public int Map_ListDto_To_Int32(ListDto source)
{
    return source.Id;
}
```

**Edge cases:**
- ConvertUsing with complex expressions (LINQ, method calls)
- Interaction with ForMember — should be mutually exclusive (emit diagnostic if both used)
- Extension method generation: `.ToInt32()` may conflict with system methods — use full type name in method name

**Tests needed:**
- Simple property extraction: `s => s.Id`
- Expression with method call: `s => s.Text.ToUpper()`
- Verify ForMember + ConvertUsing combination produces diagnostic error
- Collection mapping with ConvertUsing

---

#### Feature 2: ITypeConverter — fluent registration + global application

**Priority:** P0 | **Complexity:** Medium | **Risk:** Medium

**What:** `CreateMap<S, D>().ConvertUsing<TConverter>()` at the mapping level + automatic application when property types match across all mappings.

**This is a two-part feature:**

##### Part A: Map-level ConvertUsing<TConverter>()

**Files to modify:**

| File | Change |
|------|--------|
| `IMappingExpression.cs` | Add `IMappingExpression<S,D> ConvertUsing<TConverter>() where TConverter : ITypeConverter<S,D>, new()` |
| `MappingExpression<S,D>` in `MapperConfiguration.cs` | Store converter type on `MappingDefinition` |
| `MappingDefinition.cs` | Add `Type? ConverterType { get; set; }` (map-level) |
| `ZMapperGenerator.cs` — extraction | Detect `.ConvertUsing<T>()` (generic, no lambda), resolve `T`'s full type name |
| `ZMapperGenerator.cs` — models | Add `string? ConverterTypeName` to `MappingModel` |
| `ZMapperGenerator.cs` — generation | Emit `new TConverter().Convert(source)` instead of property mapping |

**Generated code example:**
```csharp
// CreateMap<DateTimeOffset, DateTime>().ConvertUsing<DateTimeOffsetToDateTime>();
public DateTime Map_DateTimeOffset_To_DateTime(DateTimeOffset source)
{
    return new DateTimeOffsetToDateTime().Convert(source);
}
```

##### Part B: Automatic property-level type converter application

**This is the key differentiator.** When a type converter mapping `CreateMap<DateTimeOffset, DateTime>().ConvertUsing<T>()` is registered, the generator must automatically apply it to any property of type `DateTimeOffset` being mapped to a property of type `DateTime` in **any** mapping.

**Files to modify:**

| File | Change |
|------|--------|
| `ZMapperGenerator.cs` — Execute | Build a `Dictionary<(string srcType, string destType), string converterTypeName>` from all mappings that have `ConverterTypeName` set |
| `ZMapperGenerator.cs` — GeneratePropertyAssignment | Before emitting standard assignment, check if `(srcProp.Type, destProp.Type)` has a registered type converter. If yes, emit converter call instead. |

**Generator logic (in Execute):**
```
// Collect global type converters from all configurations
var typeConverters = allMappings
    .Where(m => m.ConverterTypeName != null)
    .ToDictionary(
        m => (m.SourceType, m.DestinationType),
        m => m.ConverterTypeName);

// Pass to GenerateMapperCode / GenerateUnifiedMapperCode
```

**Generator logic (in GeneratePropertyAssignment):**
```
// Before standard property assignment:
if (typeConverters.TryGetValue((srcProp.Type, destProp.Type), out var converter))
{
    sb.AppendLine($"    destination.{destProp.Name} = new {converter}().Convert(source.{srcProp.Name});");
    return;
}
```

**Generated code example:**
```csharp
// UserDto.CreatedAt (DateTimeOffset) -> User.CreatedAt (DateTime)
// Automatically uses registered DateTimeOffsetToDateTime converter
public User Map_UserDto_To_User(UserDto source)
{
    var destination = new User();
    destination.Id = source.Id;
    destination.Name = source.Name;
    destination.CreatedAt = new DateTimeOffsetToDateTime().Convert(source.CreatedAt); // auto-applied
    return destination;
}
```

**Edge cases:**
- Nullable variants: `DateTimeOffset? -> DateTime?` needs separate registration or auto-wrapping
- Multiple converters for same type pair — last wins or diagnostic error
- Type converter mappings should NOT generate extension methods (`.ToDateTime()` would be confusing)
- Performance: `new TConverter()` on every call — consider caching as `static readonly` field or just trust JIT to optimize parameterless ctor + inline

**Tests needed:**
- Map-level ConvertUsing<T>() generates converter call
- Auto-application across unrelated mappings
- Nullable type handling
- Multiple type converters don't conflict
- Type converter mapping doesn't generate extension methods

---

### Phase 2 — P1: Runtime Logic in Mappings (Est. complexity: High)

#### Feature 5 (implement first): ResolutionContext / Runtime Parameters

**Priority:** P1 | **Complexity:** HIGH | **Risk:** High

> This is architecturally the most complex feature because runtime parameters are inherently unknown at compile-time, yet ZMapper generates all mapping code at compile-time.

**What:** Pass runtime key-value parameters to mapping operations, accessible in conditions and converters.

**Design Decision — Two approaches:**

##### Approach A: Generated context parameter (Recommended)

Add an optional `MappingContext` parameter to all generated methods. The source generator emits overloads.

**New runtime class:**
```csharp
// src/ZMapper/MappingContext.cs
namespace ZMapper;

/// <summary>
/// Carries runtime parameters into mapping operations.
/// Thread-safe for read-only access after construction.
/// </summary>
public sealed class MappingContext
{
    private readonly Dictionary<string, object> _items = new();

    public MappingContext() { }

    public object this[string key]
    {
        get => _items[key];
        set => _items[key] = value;
    }

    public bool TryGetValue(string key, out object value) => _items.TryGetValue(key, out value);

    public bool ContainsKey(string key) => _items.ContainsKey(key);

    public T Get<T>(string key) => (T)_items[key];

    public T GetOrDefault<T>(string key, T defaultValue = default)
        => _items.TryGetValue(key, out var value) ? (T)value : defaultValue;
}
```

**Files to modify:**

| File | Change |
|------|--------|
| `MappingContext.cs` (NEW) | Runtime context class (above) |
| `IMapper.cs` | Add overloads: `Map<S,D>(S source, MappingContext context)` etc. |
| `ZMapperGenerator.cs` — generation | Emit additional method overloads accepting `MappingContext context` |

**Generated code:**
```csharp
// Without context (existing — unchanged, zero-overhead)
public User Map_UserDto_To_User(UserDto source)
{
    var destination = new User();
    destination.Id = source.Id;
    destination.Region = source.Region?.ToRegion();
    return destination;
}

// With context (new overload)
public User Map_UserDto_To_User(UserDto source, MappingContext context)
{
    var destination = new User();
    destination.Id = source.Id;
    if (!context.ContainsKey("IgnoreNested") || !(bool)context["IgnoreNested"])
    {
        destination.Region = source.Region?.ToRegion();
    }
    return destination;
}

// IMapper dispatch
public TDest Map<TSrc, TDest>(TSrc source, MappingContext context) { ... }
```

**Caller usage:**
```csharp
var context = new MappingContext();
context["IgnoreNested"] = true;
var company = mapper.Map<CompanyDto, Company>(companyDto, context);
```

**Why Approach A over runtime dictionary on IMapper:**
- No breaking changes to existing zero-overhead path
- Context is explicitly passed — no hidden thread-local state
- Source generator can optimize: only emit context-aware overloads for mappings that actually USE context (via PreCondition)
- JIT can inline the no-context version as before

##### Approach B: Lambda options (AutoMapper-compatible API)

```csharp
var company = mapper.Map<Company>(companyDto, opt => opt.Items["IgnoreNested"] = true);
```

This is syntactic sugar over Approach A. Can be added as extension method:
```csharp
public static TDest Map<TSrc, TDest>(this IMapper mapper, TSrc source, Action<MappingContext> configure)
{
    var context = new MappingContext();
    configure(context);
    return mapper.Map<TSrc, TDest>(source, context);
}
```

**Implementation order:** Approach A first (core), Approach B as sugar on top.

---

#### Feature 4: PreCondition

**Priority:** P1 | **Complexity:** Medium | **Risk:** Medium | **Depends on:** Feature 5 (ResolutionContext)

**What:** `ForMember(d => d.X, opt => opt.PreCondition((src, dest, ctx) => ...))` — condition evaluated before resolving source value, with access to context.

**Files to modify:**

| File | Change |
|------|--------|
| `IMemberConfigurationExpression.cs` | Add `void PreCondition(Expression<Func<TSource, TDestination, MappingContext, bool>> condition)` |
| `MemberConfigurationExpression` in `MapperConfiguration.cs` | Store precondition expression |
| `MemberConfiguration.cs` in `MappingDefinition.cs` | Add `object? PreCondition` |
| `ZMapperGenerator.cs` — extraction | Detect `.PreCondition(...)` in ForMember chain, extract 3-param lambda |
| `ZMapperGenerator.cs` — models | Add `string? PreCondition` to `MemberConfigModel` with param names |
| `ZMapperGenerator.cs` — generation | Wrap property assignment in `if (precondition)` block, only in context-aware overload |

**Key difference from `When()`:**
- `When(src => ...)` is evaluated inline, uses only source
- `PreCondition((src, dest, ctx) => ...)` is evaluated before source resolution, has access to all three

**Generator logic:**
```
// In GeneratePropertyAssignment, when PreCondition exists:
if (memberConfig.PreCondition != null)
{
    sb.AppendLine($"    if ({memberConfig.PreCondition})");
    sb.AppendLine($"    {{");
    // ... emit normal property assignment indented ...
    sb.AppendLine($"    }}");
}
```

**Generated code:**
```csharp
public Company Map_CompanyDto_To_Company(CompanyDto source, MappingContext context)
{
    var destination = new Company();
    destination.Id = source.Id;

    // PreCondition: only map Region if context doesn't have IgnoreNested=true
    if (!context.ContainsKey("IgnoreNested") || !(bool)context["IgnoreNested"])
    {
        destination.Region = source.Region?.ToRegion();
    }

    if (!context.ContainsKey("IgnoreNested") || !(bool)context["IgnoreNested"])
    {
        destination.Stream = source.Stream?.ToStream();
    }

    return destination;
}
```

**Simplification opportunity:** Since the user code will use extension methods like `ctx.ShouldMapNested()`, the generator just needs to emit the lambda body verbatim — the extension method is regular C# that compiles normally.

**Problem:** The generator extracts the lambda as a string at compile-time. The lambda references `ctx` which is a `MappingContext` parameter. The generator must:
1. Know the parameter names in the lambda
2. Replace them with the actual variable names in generated code (`source`, `destination`, `context`)

**Tests needed:**
- PreCondition with context parameter
- PreCondition without context (just src, dest)
- PreCondition combined with When()
- PreCondition on ForMember with MapFrom

---

#### Feature 3: ConstructUsing

**Priority:** P1 | **Complexity:** Medium | **Risk:** Low

**What:** `CreateMap<S, D>().ConstructUsing(src => new D(...))` — custom factory for destination object creation.

**Files to modify:**

| File | Change |
|------|--------|
| `IMappingExpression.cs` | Add `IMappingExpression<S,D> ConstructUsing(Expression<Func<TSource, TDestination>> factory)` |
| `MappingExpression<S,D>` in `MapperConfiguration.cs` | Store factory expression |
| `MappingDefinition.cs` | Add `object? ConstructUsingExpression` |
| `ZMapperGenerator.cs` — extraction | Detect `.ConstructUsing(...)`, extract lambda body |
| `ZMapperGenerator.cs` — models | Add `string? ConstructUsingExpression` to `MappingModel` |
| `ZMapperGenerator.cs` — generation | Replace `new TDest()` with factory expression |

**Two behaviors based on lambda return:**

1. **Factory + property mapping:** Lambda creates instance, then normal property-by-property mapping applies:
   ```csharp
   CreateMap<S, D>().ConstructUsing(src => new D(src.Id));
   // Generated: var destination = new D(source.Id);
   //            destination.Name = source.Name; // property mapping continues
   ```

2. **Full construction (when combined with ConvertUsing semantics):** Lambda does everything, skip property mapping. This is actually `ConvertUsing` — so `ConstructUsing` should always just replace the `new TDest()` part.

**Generated code:**
```csharp
// CreateMap<OrderDto, Order>().ConstructUsing(src => new Order(src.Id, src.Date));
public Order Map_OrderDto_To_Order(OrderDto source)
{
    var destination = new Order(source.Id, source.Date); // ConstructUsing
    destination.Status = source.Status;                  // normal property mapping continues
    return destination;
}
```

**For the complex GIRS case** (EmailMapperProfile with GroupBy/Select/ToList), this is actually `ConvertUsing` — the entire mapping is replaced. The distinction:
- `ConstructUsing` = custom constructor, then property mapping
- `ConvertUsing` = replace entire mapping

**Tests needed:**
- Parameterized constructor
- Factory with complex expression
- ConstructUsing + ForMember combination
- ConstructUsing + IgnoreNonExisting

---

### Phase 3 — P2: Quality of Life (Est. complexity: Low)

#### Feature 8: ConvertUsing + Source Property on ForMember

**Priority:** P2 | **Complexity:** Low | **Risk:** Low

**What:** `ForMember(d => d.X, opt => opt.ConvertUsing<TConverter>(s => s.Y))` — specify which source property feeds the converter.

**Files to modify:**

| File | Change |
|------|--------|
| `IMemberConfigurationExpression.cs` | Add overload: `void ConvertUsing<TConverter>(Expression<Func<TSource, TSourceMember>> sourceMember)` |
| `MemberConfigurationExpression` | Store source member expression with converter |
| `MemberConfiguration.cs` | Already has `SourceMemberName` + `ConverterType` — just need both to be set together |
| `ZMapperGenerator.cs` — extraction | In `ExtractMemberConfigurations`, detect `ConvertUsing` with lambda arg, extract both converter type and source member |
| `ZMapperGenerator.cs` — models | `MemberConfigModel` already has `SourceMember` + needs `ConverterTypeName` |
| `ZMapperGenerator.cs` — generation | Emit `destination.X = new TConverter().Convert(source, source.Y);` |

**Generated code:**
```csharp
// .ForMember(d => d.IssuanceDate,
//     opt => opt.ConvertUsing(new ToDDMMYYYYConverter(), s => s.IssuanceDateOffset))
destination.IssuanceDate = new ToDDMMYYYYConverter().Convert(source, source.IssuanceDateOffset);
```

**Tests needed:**
- ConvertUsing with explicit source property
- ConvertUsing with same-name source property
- ConvertUsing with nested source property (`s => s.Client.Date`)

---

#### Feature 7: MemberList Validation (Source / Destination)

**Priority:** P2 | **Complexity:** Low | **Risk:** Low

**What:** `CreateMap<S, D>(MemberList.Destination)` vs `CreateMap<S, D>(MemberList.Source)` — choose which side's properties are validated for coverage.

**Files to modify:**

| File | Change |
|------|--------|
| `MemberList.cs` (NEW) | `public enum MemberList { Destination, Source, None }` |
| `IMappingExpression.cs` / `MapperConfiguration.cs` | `CreateMap<S,D>(MemberList memberList = MemberList.Destination)` |
| `MappingDefinition.cs` | Add `MemberList MemberList { get; set; }` |
| `ZMapperGenerator.cs` — extraction | Detect `MemberList` argument in `CreateMap` call |
| `ZMapperGenerator.cs` — models | Add `MemberList` to `MappingModel` |
| `ZMapperGenerator.cs` — ZMAP001 logic | Switch diagnostic based on MemberList value: <br> `Destination` = current behavior (warn on unmapped dest props) <br> `Source` = warn on unused source props <br> `None` = no warnings |

**Implementation is straightforward** — the ZMAP001 diagnostic emission already iterates destination properties. Just add a parallel check for source properties when `MemberList.Source` is selected.

**New diagnostic:**
- `ZMAP003`: Unmapped source property (when `MemberList.Source` is active)

**Tests needed:**
- MemberList.Destination (existing behavior)
- MemberList.Source generates ZMAP003 for unused source props
- MemberList.None suppresses all warnings
- MemberList with ForMember/Ignore

---

#### Feature 6: Standalone Mapper (without DI)

**Priority:** P2 | **Complexity:** Low | **Risk:** Low

**What:** Create mapper instance without `IServiceProvider`.

**Analysis:** ZMapper already substantially supports this:

1. **Extension methods** (`.ToXxx()`) work without any DI — they're static, zero-dependency
2. **CreateMap pattern** generates `CreateGeneratedMapper()` factory — also works without DI
3. **Profile pattern** generates `Mapper.Create()` / `Mapper.Create(config)` — works without DI

**What's actually missing:** A convenient top-level API similar to AutoMapper's:
```csharp
var mapper = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<User, UserDto>();
}).CreateMapper();
```

**Files to modify:**

| File | Change |
|------|--------|
| `MapperConfiguration.cs` | Add `IMapper CreateMapper()` method |
| `ZMapperGenerator.cs` — generation | `CreateMapper()` returns `Mapper.Create(this)` in generated code |

**Implementation:**
```csharp
// In MapperConfiguration.cs
public IMapper CreateMapper()
{
    return Mapper.Create(this);
}
```

This is trivial since `Mapper.Create(config)` already exists. The only addition is the convenience method on `MapperConfiguration`.

**Alternative:** Document that `.ToXxx()` extension methods are the preferred zero-DI approach (they're faster than going through `IMapper`).

**Tests needed:**
- Standalone mapper creation
- Standalone mapper with profiles
- Standalone mapper with hooks

---

## Cross-Cutting Concerns

### Source Generator Size Management

The generator is already ~26k lines. Each new feature adds extraction + model + generation code. Consider:

1. **Extract into partial classes:** Split `ZMapperGenerator.cs` into:
   - `ZMapperGenerator.Detection.cs` — syntax predicates
   - `ZMapperGenerator.Extraction.cs` — semantic extraction
   - `ZMapperGenerator.Generation.cs` — code emission
   - `ZMapperGenerator.Models.cs` — internal data models

2. **Helper method pattern:** Each new feature follows the same pattern:
   - Add field to model class
   - Add extraction logic in `ExtractMappingFromCall`
   - Add generation logic in `GeneratePropertyAssignment` or `GenerateMapperCode`

### Testing Strategy

For each feature:
1. **Unit test** — verify mapping produces correct result
2. **Compilation test** — verify generated code compiles without warnings
3. **Diagnostic test** — verify ZMAP warnings/errors fire correctly
4. **Integration test** — verify feature works with DI, profiles, and CreateMap patterns

### Performance Considerations

| Feature | Runtime Impact |
|---------|---------------|
| ConvertUsing (lambda) | None — lambda inlined at compile-time |
| ConvertUsing<T> | Minimal — `new T()` is cheap for parameterless ctors, JIT may inline |
| ConstructUsing | None — factory inlined |
| PreCondition | Minimal — single `if` check |
| ResolutionContext | Low — dictionary lookup only when context provided; zero-cost when not used (separate overload) |
| MemberList | Zero — compile-time only |
| Standalone mapper | Zero — same generated code |

### Backwards Compatibility

All features are **additive**:
- No changes to existing generated code for mappings that don't use new features
- New `IMapper` overloads (with `MappingContext`) don't break existing code
- `MemberList` defaults to `Destination` (current behavior)
- Extension methods remain unchanged

---

## Implementation Order (Recommended)

```
Phase 1 (P0):
  1. Map-level ConvertUsing lambda        [Low complexity, unblocks simple migrations]
  2. ITypeConverter fluent registration   [Medium complexity, unblocks DateTimeOffset migrations]

Phase 2 (P1):
  3. ResolutionContext / MappingContext    [High complexity, foundational for PreCondition]
  4. PreCondition                         [Medium complexity, depends on #3]
  5. ConstructUsing                       [Medium complexity, independent]

Phase 3 (P2):
  6. ConvertUsing + source property       [Low complexity]
  7. MemberList validation                [Low complexity]
  8. Standalone mapper convenience API    [Low complexity, mostly documentation]
```

**Estimated scope:** ~8 features, ~15 new/modified files, ~200+ new tests.

---

## Summary Table

| # | Feature | Complexity | Files Modified | New Tests | Depends On |
|---|---------|-----------|----------------|-----------|------------|
| 1 | ConvertUsing lambda | Low | 6 | ~15 | — |
| 2 | ITypeConverter registration | Medium | 7 | ~20 | — |
| 3 | ResolutionContext | High | 5+ | ~25 | — |
| 4 | PreCondition | Medium | 6 | ~15 | #3 |
| 5 | ConstructUsing | Medium | 6 | ~15 | — |
| 6 | ConvertUsing + source | Low | 5 | ~10 | — |
| 7 | MemberList validation | Low | 6 | ~15 | — |
| 8 | Standalone mapper | Low | 2 | ~5 | — |
