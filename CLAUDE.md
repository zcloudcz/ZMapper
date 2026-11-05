# ZMapper — Project Guide

## What is ZMapper?

ZMapper is a **compile-time source-generated object mapper for .NET** (like AutoMapper API but with Mapperly-style codegen). Instead of runtime reflection, a Roslyn incremental source generator analyzes `CreateMap<S,D>()` calls at compile-time and emits optimized C# code. Benchmark: ~17ns/object (3x faster than AutoMapper). Requires .NET 9+.

## Solution Structure

```
ZMapper.sln
├── src/
│   ├── ZMapper/                          # Core library (NuGet package)
│   │   ├── Abstractions/
│   │   │   ├── IMapper.cs                # Main mapper interface (Map, MapArray, MapList)
│   │   │   ├── ITypeConverter.cs         # ITypeConverter<S,D> + IMemberConverter<S,SM,DM>
│   │   │   ├── Attributes/              # [Ignore], [MapFrom("Name")], [MapperConfiguration]
│   │   │   └── Configuration/
│   │   │       ├── IMappingExpression.cs  # Fluent API: ForMember, ReverseMap, BeforeMap, etc.
│   │   │       ├── IMapperProfile.cs      # Profile pattern interface
│   │   │       └── IMapperConfiguration.cs
│   │   ├── MapperConfiguration.cs         # Runtime config holder + MappingExpression<S,D>
│   │   ├── MappingDefinition.cs           # Runtime mapping metadata (MappingDefinition, MemberConfiguration)
│   │   └── ZMapper.csproj                 # Multi-target: net10.0;netstandard2.0
│   └── ZMapper.SourceGenerator/
│       ├── ZMapperGenerator.cs            # THE source generator (~26k lines, everything in one file)
│       └── ZMapper.SourceGenerator.csproj
├── tests/
│   ├── ZMapper.Tests/                     # 144+ xUnit tests with FluentAssertions
│   └── ZMapper.Benchmarks/               # BenchmarkDotNet: Manual vs ZMapper vs Mapperly vs AutoMapper
└── examples/
    └── ZMapper.Example/                   # Example using Profile pattern
```

## How It Works (Data Flow)

```
User writes:  config.CreateMap<UserDto, User>()
                    .ForMember(d => d.UserId, o => o.MapFrom(s => s.Id))
                    .BeforeMap((s, d) => { ... })
                    
     ↓ Compile-time (Roslyn)

ZMapperGenerator detects CreateMap calls via syntax predicates
     ↓
Extracts: source/dest types, properties, ForMember configs, hooks
     ↓ Stores in internal models (MappingModel, MemberConfigModel, PropertyModel)

Generates 3-4 files:
  1. {ClassName}_Generated.g.cs      — IMapper implementation with typed Map methods
  2. ZMapper_GlobalExtensions.g.cs   — .ToUser() extension methods (AggressiveInlining)
  3. ZMapper_Mapper.g.cs             — Unified Mapper class (for Profile pattern)
  4. ZMapper_ServiceCollectionExtensions.g.cs — AddZMapper() (only if M.E.DI referenced)
```

## Two Configuration Patterns

### 1. CreateMap Pattern (original)
```csharp
public partial class MyMapperConfig
{
    public void Configure(MapperConfiguration config)
    {
        config.CreateMap<UserDto, User>()
              .ForMember(d => d.UserId, o => o.MapFrom(s => s.Id));
    }
}
// Generates: MyMapperConfig_Generated.g.cs with nested GeneratedMapper : IMapper
```

### 2. Profile Pattern (preferred)
```csharp
public partial class UserProfile : IMapperProfile  // MUST be partial
{
    public void Configure(MapperConfiguration config)
    {
        config.CreateMap<UserDto, User>();
    }
}
// Generates: ZMapper_Mapper.g.cs with public sealed class Mapper : IMapper
```

## Source Generator Architecture (ZMapperGenerator.cs)

The entire generator is in one file. Key sections:

1. **Detection** — Two `IncrementalValuesProvider`s:
   - Provider 1: Finds methods containing `CreateMap` calls (CreateMap pattern)
   - Provider 2: Finds classes implementing `IMapperProfile` (Profile pattern)

2. **Extraction** — Semantic analysis:
   - `ExtractMappingFromCall()` — resolves types, properties, member configs
   - `ExtractMemberConfigurations()` — parses ForMember/MapFrom/Ignore/When chains
   - `ExtractHooks()` — finds BeforeMap/AfterMap
   - `ExtractReverseMappingIfPresent()` — creates inverted mapping for ReverseMap()
   - `GetProperties()` — walks inheritance chain, filters by accessibility/setters

3. **Internal Models** (inside the generator file):
   - `MapperConfigurationModel` — namespace, class name, list of mappings
   - `ProfileConfigurationModel` — profile class info + mappings + error state
   - `MappingModel` — source/dest types, properties, member configs, hooks, flags
   - `PropertyModel` — name, type, IsRequired, IsNullable, IsInitOnly, IsComplexType, IsCollection
   - `MemberConfigModel` — DestinationMember, SourceMember, SourceExpression, IsIgnored, Condition

4. **Code Generation** — StringBuilder-based:
   - `GenerateMapperCode()` — per-class mapper (5 methods per mapping + generic dispatch)
   - `GenerateExtensionMethodsCode()` — .ToXxx() extension methods
   - `GenerateUnifiedMapperCode()` — unified Mapper class for profiles
   - `GenerateServiceCollectionExtension()` — AddZMapper() DI extension
   - `GeneratePropertyAssignment()` — handles 4 cases: complex expression, collection of complex, nested complex, simple

5. **Diagnostics:**
   - ZMAP001: Unmapped destination property (warning, suppressed by IgnoreNonExisting)
   - ZMAP002: Profile class not partial (error)

## Runtime Classes (src/ZMapper/)

- **MapperConfiguration** — holds `List<MappingDefinition>`, provides `CreateMap<S,D>()`
- **MappingExpression<S,D>** — implements fluent API, populates MappingDefinition
- **MemberConfigurationExpression<S,D,M>** — implements ForMember options (MapFrom, Ignore, When, ConvertUsing)
- **MappingDefinition** — runtime metadata: types, member configs, hooks, IgnoreNonExisting flag
- **MemberConfiguration** — per-member: destination name, source name, converter type, condition, ignored flag

**Important dual role:** These runtime classes serve two purposes:
1. At compile-time: the source generator reads the syntax of calls to these classes
2. At runtime: the generated `AddZMapper()` / `Mapper.Create(config)` actually instantiates them to wire up hooks

## Key Design Decisions

- **Everything in `namespace ZMapper`** — no sub-namespaces
- **Single NuGet package** — ZMapper.csproj packs the source generator DLL into `analyzers/dotnet/cs/`
- **Zero-overhead extensions** — `.ToUser()` methods with AggressiveInlining, no boxing
- **Static hook fields** — BeforeMap/AfterMap stored as static fields, populated from config at runtime
- **Property assignment handles nullable** — emits `?? default` for `T? -> T` conversions
- **Init-only / required props** — detected and emitted in object initializer syntax

## Naming Constraints

- Generated class is `Mapper` (not `ZMapper` — would conflict with namespace)
- Generated files: `{ClassName}_Generated.g.cs`, `ZMapper_GlobalExtensions.g.cs`, `ZMapper_Mapper.g.cs`

## Development Notes

- **Language:** Czech developer, English code and comments
- **Testing:** xUnit + FluentAssertions 8.8.0, currently 144+ tests
- **Build target:** .NET 10 with `<RollForward>LatestMajor</RollForward>`
- **EmitCompilerGeneratedFiles trap:** If enabled then disabled, delete `Generated/` folder to avoid CS0101
- **CultureInfo:** Use `InvariantCulture` for locale-independent formatting in tests
- Code should be clean, well-structured, and commented so a junior .NET developer can understand it
- Solutions should be covered with unit/integration tests

## Currently Supported Features

| Feature | API |
|---------|-----|
| Convention-based mapping | `CreateMap<S,D>()` |
| Custom member mapping | `ForMember(d => d.X, o => o.MapFrom(s => s.Y))` |
| Complex expressions | `MapFrom(s => s.Client.Name)`, ternary, interpolation |
| Ignore member | `ForMember(d => d.X, o => o.Ignore())` |
| Conditional mapping | `ForMember(d => d.X, o => o.When(s => s.X != null))` |
| Member converter | `ForMember(d => d.X, o => o.ConvertUsing<T>())` |
| Reverse mapping | `.ReverseMap()` |
| Suppress warnings | `.IgnoreNonExisting()` |
| Hooks | `.BeforeMap(...)` / `.AfterMap(...)` |
| Profiles | `IMapperProfile` (partial class) |
| DI | `AddZMapper()` auto-generated |
| Collections | `MapArray`, `MapList` (Span-based) |
| Attributes | `[Ignore]`, `[MapFrom("Name")]` |
| Extension methods | `.ToXxx()` (AggressiveInlining) |
| Map to existing | `Map<S,D>(src, dest)` |
| Inheritance | Properties from base types included |
| Nullable handling | `T? -> T` emits `?? default` |

## Missing Features (vs AutoMapper)

See `ZMAPPER_MISSING.md` for detailed gap analysis and `ZMAPPER_IMPLEMENTATION_PLAN.md` for implementation plan. Summary:

| # | Feature | Priority | Status |
|---|---------|----------|--------|
| 1 | Map-level ConvertUsing lambda | P0 | Not implemented |
| 2 | ITypeConverter fluent registration + auto-apply | P0 | Not implemented |
| 3 | ResolutionContext / runtime parameters | P1 | Not implemented |
| 4 | PreCondition (with context access) | P1 | Not implemented |
| 5 | ConstructUsing (factory) | P1 | Not implemented |
| 6 | ConvertUsing + source property spec | P2 | Not implemented |
| 7 | MemberList validation (Source/Destination) | P2 | Not implemented |
| 8 | Standalone mapper convenience API | P2 | Not implemented |
