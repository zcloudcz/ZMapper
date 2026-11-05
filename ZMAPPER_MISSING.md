# ZMapper — chybějící features oproti AutoMapper

Soupis funkcionality AutoMapper, kterou ZMapper (https://github.com/zcloudcz/ZMapper) aktuálně **nepodporuje**, ale je potřebná pro migraci reálných projektů.

Analýza vychází z projektu GIRS (ASP.NET Core 8 MVC, ~150+ mapování, 15 profilů, 14 custom converterů, AutoMapper 9.0.0).

> **ZMapper je compile-time source generator (Roslyn)** — žádná runtime reflection. Benchmark: ~17ns/objekt (3x rychlejší než AutoMapper). Vyžaduje .NET 9.0+.

---

## Co ZMapper UŽ UMÍ (srovnání s AutoMapper)

| AutoMapper feature | ZMapper ekvivalent | Poznámka |
|---|---|---|
| `CreateMap<S,D>()` | `CreateMap<S,D>()` | Convention-based, shodné názvy properties |
| `ForMember` + `MapFrom` | `ForMember` + `MapFrom` | Včetně nested props, LINQ, ternary, string interpolation |
| `ForMember` + `Ignore()` | `ForMember` + `Ignore()` | Identické API |
| `ReverseMap()` | `ReverseMap()` | Automatická inverze member konfigurací |
| `Condition` | `When()` | Jiný název, stejná funkce: `opt.When(src => src.Price > 0)` |
| `IValueConverter` (member) | `IMemberConverter<TSource, TSourceMember, TDest>` | Jiné rozhraní, 3 generic parametry místo 2 |
| `ITypeConverter<S,D>` | `ITypeConverter<S,D>` | Rozhraní existuje (viz omezení níže) |
| `BeforeMap` / `AfterMap` | `BeforeMap` / `AfterMap` | Identické API |
| `Profile` | `IMapperProfile` (partial class) | Třídy musí být `partial` |
| DI: `AddAutoMapper()` | `AddZMapper()` | Auto-generated extension |
| `IgnoreAllNonExisting` | `IgnoreNonExisting()` | Potlačí ZMAP001 warning |
| Map to existing instance | `Map<S,D>(src, dest)` | Identické |
| Collection mapping | `MapArray`, `MapList` | Span-based, vysoký výkon |
| Atributová konfigurace | `[Ignore]`, `[MapFrom("Name")]` | Alternativa k fluent API |
| Auto-generated extensions | `.ToXxx()` metody | Bonus — inlined, AggressiveInlining |

---

## Co ZMapper CHYBÍ

### 1. ITypeConverter — fluent registrace na úrovni mapování

#### Popis
Rozhraní `ITypeConverter<S,D>` v ZMapper existuje, ale **chybí fluent API pro jeho registraci** na úrovni `CreateMap`. V AutoMapper se globální type konvertery registrují takto a automaticky se aplikují kdekoli se mapuje daný typový pár.

#### Příklad (AutoMapper)
```csharp
// Registrace v profilu — platí globálně pro všechna mapování obsahující DateTimeOffset -> DateTime
CreateMap<DateTimeOffset, DateTime>().ConvertUsing<DateTimeOffsetToDateTime>();
CreateMap<DateTime, DateTimeOffset>().ConvertUsing<DateTimeToDateTimeOffset>();
```

#### Reálné použití v GIRS (4 globální konvertery)
| Konverter | Vstup | Výstup |
|---|---|---|
| `DateTimeOffsetToDateTime` | `DateTimeOffset` | `DateTime` |
| `DateTimeToDateTimeOffset` | `DateTime` | `DateTimeOffset` |
| `NullableDateTimeOffsetToNullableDateTime` | `DateTimeOffset?` | `DateTime?` |
| `NullableDateTimeToNullableDateTimeOffset` | `DateTime?` | `DateTimeOffset?` |

#### Co ZMapper potřebuje
- `CreateMap<S,D>().ConvertUsing<TConverter>()` na `IMappingExpression`
- Source generator musí při generování kódu pro jakýkoli mapping rozpoznat, že property typu `DateTimeOffset` mapovaná na `DateTime` má použít registrovaný `ITypeConverter`
- Alternativně: globální registrace konverterů mimo `CreateMap` (např. `config.AddTypeConverter<T>()`)

---

### 2. Map-level ConvertUsing (inline lambda)

#### Popis
Konverze celého objektu inline lambdou bez vytváření konvertorové třídy. Používá se pro jednoduché typové transformace.

#### Příklad (AutoMapper)
```csharp
CreateMap<ListDto, int>().ConvertUsing(s => s.Id);
CreateMap<ListDto, string>().ConvertUsing(s => s.Text);
```

#### Co ZMapper potřebuje
- `CreateMap<S, D>().ConvertUsing(src => ...)` — lambda na úrovni celého mapování
- Source generator emituje lambda výraz místo property-by-property mapování

---

### 3. ConstructUsing (factory)

#### Popis
Vlastní factory pro vytvoření cílového objektu. Používá se když cílový typ nemá bezparametrický konstruktor nebo vyžaduje komplexní inicializaci (grouping, agregace).

#### Příklad (AutoMapper)
```csharp
CreateMap<List<EmailRecipient>, List<EmailRecipientDto>>()
    .ConstructUsing((src, ctx) =>
        src.GroupBy(r => r.Email)
           .Select(g => new EmailRecipientDto
           {
               Email = g.Key,
               FullName = g.First().FullName,
               Types = g.Select(r => r.Type).Distinct().ToList()
           })
           .ToList()
    );
```

#### Reálné použití v GIRS
- `EmailMapperProfile` — 3 mapování s komplexní List aggregací a deduplikací

#### Co ZMapper potřebuje
- `.ConstructUsing((source) => ...)` — factory lambda pro vytvoření cílové instance
- Source generator použije factory místo `new TDest()` a přeskočí property-by-property mapování

---

### 4. PreCondition (podmínka s přístupem ke kontextu)

#### Popis
ZMapper má `When()` (ekvivalent AutoMapper `Condition`), ale **nemá `PreCondition`** — podmínku vyhodnocenou před resolvováním zdrojové hodnoty, s přístupem k runtime kontextu/parametrům.

#### Příklad (AutoMapper)
```csharp
CreateMap<CompanyDto, Company>()
    .ForMember(d => d.Region,
        opt => opt.PreCondition((src, dest, ctx) => ctx.ShouldMapNested()))
    .ForMember(d => d.Stream,
        opt => opt.PreCondition((src, dest, ctx) => ctx.ShouldMapNested()));
```

#### Reálné použití v GIRS
- `OrganizationStructureMapperProfile` — 6 member mapování podmíněných runtime parametrem `IgnoreNested`

#### Co ZMapper potřebuje
- `ForMember(d => d.X, opt => opt.PreCondition((src, dest, ctx) => ...))` — podmínka s přístupem ke kontextu
- Vyhodnocuje se před resolvováním zdrojové hodnoty
- Vyžaduje existenci `ResolutionContext` (viz bod 5)

---

### 5. ResolutionContext / runtime parametry

#### Popis
Předání runtime parametrů do mapování přes slovník. Konvertery a podmínky mohou číst tyto parametry. Umožňuje měnit chování mapperu za běhu bez vytváření nové konfigurace.

#### Příklad (AutoMapper)
```csharp
// Extension metoda pro čtení z kontextu
public static class MapperContextExtensions
{
    public const string IgnoreNested = "IgnoreNested";

    public static bool ShouldMapNested(this ResolutionContext context)
    {
        return !context.Options.Items.ContainsKey(IgnoreNested)
            || !(bool)context.Options.Items[IgnoreNested];
    }
}

// Volání mapperu s runtime parametrem
var company = _mapper.Map<Company>(companyDto, opt =>
    opt.Items[MapperContextExtensions.IgnoreNested] = true);
```

#### Reálné použití v GIRS
- `OrganizationStructureService` — předává `IgnoreNested = true` pro zabránění mapování nested navigation properties (Region, Stream) při částečném updatu

#### Co ZMapper potřebuje
- `Map<TDest>(source, opt => opt.Items["key"] = value)` — overload pro předání runtime parametrů
- `ResolutionContext` (nebo ekvivalent) dostupný v `PreCondition`, `When`, `ConvertUsing`
- **Pozn.:** Toto je architektonicky nejsložitější feature pro source generator — runtime parametry jsou z definice neznámé v compile-time. Možné řešení: generovaný kód přijímá volitelný context parametr.

---

### 6. Standalone / statický mapper (bez DI)

#### Popis
Možnost vytvořit mapper instanci manuálně bez DI kontejneru. Používá se v utilitách, migračních nástrojích, testech a console aplikacích.

#### Příklad (AutoMapper)
```csharp
public static class IdentityMappers
{
    private static readonly IMapper _mapper = new MapperConfiguration(cfg =>
    {
        cfg.CreateMap<User, UserClone>(MemberList.Destination);
    }).CreateMapper();

    public static UserClone CloneUser(User user) => _mapper.Map<UserClone>(user);
}
```

#### Reálné použití v GIRS
- `GIRS.EntityFramework/Mappers/IdentityMappers.cs` — statická instance pro klonování entit
- `GIRS.ConsoleTestApp` — testovací console app
- `GIRS.DataMigrations.MigrationTool` — migrační nástroj

#### Co ZMapper potřebuje
- Možnost instanciovat mapper bez `IServiceProvider`
- Alternativně: auto-generated statické metody (ZMapper už generuje `.ToXxx()` extension metody, ty mohou být dostatečné pro tento use case)

---

### 7. MemberList validace (Source / Destination)

#### Popis
AutoMapper umožňuje specifikovat, zda se validují zdrojové nebo cílové properties. `MemberList.Destination` (default) vyžaduje, aby každá cílová property měla zdroj. `MemberList.Source` naopak vyžaduje, aby každá zdrojová property byla využita.

#### Příklad (AutoMapper)
```csharp
CreateMap<User, UserDto>(MemberList.Destination);  // default — všechny dest properties musí mít zdroj
CreateMap<UserDto, User>(MemberList.Source);        // všechny source properties musí být využity
```

#### Reálné použití v GIRS
- ~95% mapování používá `MemberList.Destination` (default)
- 2 mapování v `IdentityMapperProfile` používají `MemberList.Source`

#### Co ZMapper potřebuje
- `CreateMap<S,D>(MemberList.Destination)` — compile-time validace pokrytí cílových properties
- `CreateMap<S,D>(MemberList.Source)` — compile-time validace využití zdrojových properties
- ZMapper už má `IgnoreNonExisting()` a ZMAP001 warning, což částečně pokrývá `MemberList.Destination` chování — ale chybí explicitní přepínání režimu a `MemberList.Source`

---

### 8. IMemberConverter — specifikace zdrojové property

#### Popis
ZMapper má `IMemberConverter<TSource, TSourceMember, TDest>` a `ConvertUsing<TConverter>()`, ale chybí možnost specifikovat **ze které zdrojové property** se má hodnota vzít (pokud se liší od cílové).

#### Příklad (AutoMapper)
```csharp
// ConvertUsing s explicitní zdrojovou property
.ForMember(d => d.IssuanceDate,
    opt => opt.ConvertUsing(new ToDDMMYYYYDateTimeValueConverter(), s => s.IssuanceDateOffset))
//                                                                  ^^^^^^^^^^^^^^^^^^^^^^^^
//                                    zdrojová property se liší od cílové (IssuanceDate vs IssuanceDateOffset)
```

#### Co ZMapper potřebuje
- `ForMember(d => d.X, opt => opt.ConvertUsing<TConverter>(s => s.Y))` — specifikace zdrojové property pro konverter
- Alternativně: kombinace `MapFrom` + `ConvertUsing` v jednom fluent řetězci

---

## Srovnávací přehled

| # | Feature | AutoMapper | ZMapper | Gap |
|---|---|---|---|---|
| 1 | ITypeConverter fluent registrace | `ConvertUsing<T>()` na mapping | Interface existuje, **chybí registrace** | Střední |
| 2 | Map-level ConvertUsing lambda | `ConvertUsing(s => ...)` | Chybí | Nízký |
| 3 | ConstructUsing | `ConstructUsing((s, ctx) => ...)` | Chybí | Střední |
| 4 | PreCondition | `PreCondition((s, d, ctx) => ...)` | Chybí (jen `When`) | Střední |
| 5 | ResolutionContext / runtime params | `opt.Items["key"] = value` | Chybí | **Vysoký** |
| 6 | Standalone mapper | `new MapperConfiguration().CreateMapper()` | Chybí (ale `.ToXxx()` existuje) | Nízký |
| 7 | MemberList validace | `MemberList.Source/Destination` | Částečně (`IgnoreNonExisting`) | Nízký |
| 8 | ConvertUsing + zdrojová property | `ConvertUsing(conv, s => s.Y)` | Chybí | Nízký |

---

## Prioritizace pro implementaci

### P0 — Blokující pro migraci projektů s komplexními mapováními
1. **ITypeConverter fluent registrace** — bez tohoto nelze nahradit globální konvertery (DateTimeOffset <-> DateTime apod.)
2. **Map-level ConvertUsing lambda** — jednoduché celotypové konverze (ListDto -> int)

### P1 — Potřebné pro projekty s runtime logikou v mapování
3. **ResolutionContext / runtime parametry** — architektonicky nejsložitější, ale potřebné pro dynamické chování
4. **PreCondition** — závisí na ResolutionContext
5. **ConstructUsing** — factory pro komplexní inicializaci

### P2 — Quality of life
6. **ConvertUsing + zdrojová property** — specifikace zdroje pro member konverter
7. **MemberList validace** — explicitní přepínání Source/Destination režimu
8. **Standalone mapper** — pro utility/migrace/testy bez DI (workaround: `.ToXxx()` extension metody)
