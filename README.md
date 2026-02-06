# ZMapper

<div align="center">

**High-Performance Object Mapping for .NET**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0+-purple.svg)](https://dotnet.microsoft.com/)
[![Build](https://img.shields.io/badge/build-passing-brightgreen.svg)]()

*AutoMapper's fluent API + Mapperly's compile-time source generation = ZMapper*

[Features](#features) | [Quick Start](#quick-start) | [Profiles & DI](#profiles--dependency-injection) | [Performance](#performance) | [API Reference](#api-reference)

</div>

---

## Features

- **Near-Zero Overhead** - Source-generated code runs as fast as hand-written mapping (~17 ns per object)
- **Familiar API** - AutoMapper-style fluent configuration (`CreateMap`, `ForMember`, `MapFrom`)
- **Compile-Time Safety** - All mapping code generated at build time, no runtime reflection
- **Profiles** - Organize mappings into reusable `IMapperProfile` classes (like AutoMapper profiles)
- **Dependency Injection** - Auto-generated `AddZMapper()` extension for `IServiceCollection`
- **Hooks** - `BeforeMap` and `AfterMap` callbacks for custom pre/post-processing logic
- **Conditional Mapping** - Map properties only when conditions are met (`When()`)
- **Reverse Mapping** - Bidirectional mappings with a single `.ReverseMap()` call
- **Nested Objects** - Full support for deep object graphs (any nesting depth)
- **Collections** - High-performance batch mapping with `ReadOnlySpan<T>`, arrays, lists, and `IEnumerable<T>`
- **Extension Methods** - Auto-generated `.ToXxx()` extension methods for zero-ceremony mapping
- **Modern C#** - `init`, `required`, nullable reference types, records, enums, `DateOnly`, `Guid`, etc.

## Quick Start

### Installation

```bash
dotnet add package ZMapper
```

> **Single NuGet package** - includes both the runtime library and the compile-time source generator. No separate analyzer package needed.

### Define Your Types

```csharp
public class PersonDto
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}

public class Person
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public string ContactEmail { get; set; }
}
```

### Option A: Profile-Based Configuration (Recommended)

```csharp
using ZMapper.Abstractions.Configuration;

public class PersonProfile : IMapperProfile
{
    public void Configure(MapperConfiguration config)
    {
        config.CreateMap<PersonDto, Person>()
            .ForMember(dest => dest.FullName,
                       opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"))
            .ForMember(dest => dest.ContactEmail,
                       opt => opt.MapFrom(src => src.Email));
    }
}
```

### Option B: Inline Configuration (Original Pattern)

```csharp
using ZMapper;
using ZMapper.Abstractions;

public partial class MyMapperConfig
{
    public static IMapper Configure()
    {
        var config = new MapperConfiguration();

        config.CreateMap<PersonDto, Person>()
            .ForMember(dest => dest.FullName,
                       opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"))
            .ForMember(dest => dest.ContactEmail,
                       opt => opt.MapFrom(src => src.Email));

        // The source generator creates this method at compile time
        return CreateGeneratedMapper();
    }
}
```

### Map Objects

```csharp
// With profiles: create the unified mapper
IMapper mapper = Mapper.Create();

// Or with DI (see Profiles & Dependency Injection section below)
// builder.Services.AddZMapper();

// Single object
var person = mapper.Map<PersonDto, Person>(dto);

// Or use the auto-generated extension method (even faster!)
var person = dto.ToPerson();

// Batch mapping with Span<T>
ReadOnlySpan<PersonDto> dtos = GetDtos();
Person[] people = mapper.MapArray<PersonDto, Person>(dtos);

// List mapping
List<Person> people = mapper.MapList<PersonDto, Person>(dtoList);

// IEnumerable mapping (LINQ queries, EF results, HashSets, etc.)
IEnumerable<PersonDto> filtered = dtos.Where(d => d.IsActive);
List<Person> activePeople = mapper.MapList<PersonDto, Person>(filtered);
```

## Profiles & Dependency Injection

### Organizing Mappings with Profiles

Profiles let you group related mappings into separate classes, just like AutoMapper:

```csharp
using ZMapper.Abstractions.Configuration;

// One profile per domain area
public class UserProfile : IMapperProfile
{
    public void Configure(MapperConfiguration config)
    {
        config.CreateMap<UserDto, User>()
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Username))
            .ForMember(dest => dest.EmailAddress, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.FullName, opt => opt.Ignore());
    }
}

public class AddressProfile : IMapperProfile
{
    public void Configure(MapperConfiguration config)
    {
        config.CreateMap<AddressDto, Address>()
            .ForMember(dest => dest.StreetAddress, opt => opt.MapFrom(src => src.Street))
            .ForMember(dest => dest.Zip, opt => opt.MapFrom(src => src.PostalCode))
            .ForMember(dest => dest.CountryName, opt => opt.MapFrom(src => src.Country));
    }
}
```

The source generator discovers all `IMapperProfile` implementations at compile time and generates a **unified mapper** (`Mapper`) that combines all profile mappings into a single `IMapper` instance.

### Dependency Injection

When your project references `Microsoft.Extensions.DependencyInjection.Abstractions`, ZMapper automatically generates an `AddZMapper()` extension method:

```csharp
// In Program.cs or Startup.cs
builder.Services.AddZMapper();

// Then inject IMapper anywhere
public class UserService
{
    private readonly IMapper _mapper;

    public UserService(IMapper mapper)
    {
        _mapper = mapper;
    }

    public User GetUser(UserDto dto) => _mapper.Map<UserDto, User>(dto);
}
```

### Manual Creation (Without DI)

If you don't use dependency injection, create the mapper directly:

```csharp
// Without hooks
IMapper mapper = Mapper.Create();

// With hooks (from a MapperConfiguration)
var config = new MapperConfiguration();
new UserProfile().Configure(config);
IMapper mapper = Mapper.Create(config);
```

## Performance

All benchmarks compare ZMapper against manual mapping (baseline), Mapperly (source generation), and AutoMapper (reflection-based).

### Simple Object Mapping (Single)

| Method | Mean | Ratio | Allocated |
|---|---|---|---|
| ZMapper | 16.56 ns | 0.93x | 88 B |
| Manual Mapping | 17.79 ns | 1.00x | 88 B |
| Mapperly | 17.83 ns | 1.00x | 88 B |
| AutoMapper | 52.64 ns | 2.96x | 88 B |

> ZMapper matches manual mapping speed and is **3x faster than AutoMapper**.

### Simple Batch Mapping (1,000 objects)

| Method | Mean | Ratio |
|---|---|---|
| Manual Loop | 18.79 us | 1.00x |
| **ZMapper (Span)** | **19.38 us** | **1.03x** |
| Mapperly Loop | 23.50 us | 1.25x |
| AutoMapper Loop | 54.38 us | 2.89x |

> Span-based batch mapping is nearly identical to hand-written loops.

### Complex Object Mapping (Nested objects, collections, enums)

| Method | Mean | Ratio |
|---|---|---|
| Manual (Order) | 168.35 ns | 1.00x |
| **ZMapper (Order)** | **172.59 ns** | **1.03x** |
| Mapperly (Order) | 214.79 ns | 1.28x |
| AutoMapper (Order) | 351.79 ns | 2.09x |

> Even with deep object graphs (Order -> OrderItems[] -> OrderStatusInfo), ZMapper stays within 3% of manual mapping.

### Complex Batch Mapping (1,000 orders)

| Method | Mean | Ratio |
|---|---|---|
| **ZMapper (Span)** | **122.75 us** | **0.86x** |
| Manual Loop | 142.93 us | 1.00x |
| Mapperly Loop | 167.91 us | 1.18x |
| AutoMapper Loop | 237.94 us | 1.67x |

> ZMapper's Span-based batch mapping is **faster than manual mapping** for complex objects.

### Why Is ZMapper Fast?

1. **Compile-time code generation** - No runtime reflection or dictionary lookups
2. **Direct property access** - Generated code reads/writes properties directly
3. **`ReadOnlySpan<T>` batch operations** - Zero-copy, stack-friendly iteration
4. **`AggressiveInlining`** - Generated extension methods are JIT-inlined
5. **No boxing** - Value types stay on the stack

Run benchmarks yourself:

```bash
cd tests/ZMapper.Benchmarks
dotnet run -c Release
# Or filter specific benchmarks:
dotnet run -c Release -- --filter *Complex*
```

## API Reference

### CreateMap - Convention-Based Mapping

Properties with matching names are mapped automatically:

```csharp
config.CreateMap<Source, Destination>();
```

### ForMember - Explicit Property Mapping

Map properties with different names or custom expressions:

```csharp
config.CreateMap<OrderDto, Order>()
    .ForMember(dest => dest.OrderTotal,
               opt => opt.MapFrom(src => src.Items.Sum(i => i.Price)));
```

### Ignore - Skip Properties

Prevent specific properties from being mapped:

```csharp
config.CreateMap<UserDto, User>()
    .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
    .ForMember(dest => dest.InternalId, opt => opt.Ignore());
// Ignored properties retain their default values
```

### Default Behavior - Unmapped Property Detection

ZMapper maps properties **by name convention**. If a destination property has no matching source property and is not explicitly configured, ZMapper emits a **compile-time warning** (ZMAP001):

```
warning ZMAP001: Destination property 'Address' on type 'Destination' has no matching source
property on 'Source'. Use .ForMember(d => d.Address, opt => opt.Ignore()) to explicitly ignore,
or .IgnoreNonExisting() to skip all non-matching properties.
```

You have two options to resolve:

```csharp
// Option 1: Explicitly ignore individual properties
config.CreateMap<Source, Destination>()
    .ForMember(dest => dest.Address, opt => opt.Ignore());

// Option 2: Opt out of unmapped property checks entirely
config.CreateMap<Source, Destination>()
    .IgnoreNonExisting(); // All non-matching properties silently keep defaults
```

This is especially useful with hooks, where destination-only properties are set by `BeforeMap`/`AfterMap`:

```csharp
config.CreateMap<InvoiceDto, Invoice>()
    .IgnoreNonExisting() // CreatedAt, ProcessedBy are set by hooks below
    .BeforeMap((src, dest) => dest.CreatedAt = DateTime.UtcNow)
    .AfterMap((src, dest) => dest.ProcessedBy = "ZMapper");
```

### When - Conditional Mapping

Map a property only when a condition is true:

```csharp
config.CreateMap<ProductDto, Product>()
    .ForMember(dest => dest.Price, opt =>
    {
        opt.MapFrom(src => src.Price);
        opt.When(src => src.Price > 0); // Only map positive prices
    })
    .ForMember(dest => dest.Description, opt =>
    {
        opt.MapFrom(src => src.Description!);
        opt.When(src => src.Description != null); // Only map non-null
    });
```

### ReverseMap - Bidirectional Mapping

Create mappings in both directions with a single call:

```csharp
config.CreateMap<OrderDto, Order>()
    .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.OrderId))
    .ForMember(dest => dest.Customer, opt => opt.MapFrom(src => src.CustomerName))
    .ReverseMap();
// Now both OrderDto -> Order and Order -> OrderDto work
```

### BeforeMap / AfterMap - Hooks

Execute custom logic before or after the mapping:

```csharp
config.CreateMap<InvoiceDto, Invoice>()
    .ForMember(dest => dest.InvoiceId, opt => opt.MapFrom(src => src.Id))
    .BeforeMap((src, dest) =>
    {
        // Runs BEFORE property mapping
        dest.CreatedAt = DateTime.UtcNow;
    })
    .AfterMap((src, dest) =>
    {
        // Runs AFTER property mapping
        dest.ProcessedBy = "ZMapper";
        dest.TotalWithTax = dest.Total * 1.21m;
    });
```

Hooks are useful for:
- Setting audit fields (timestamps, user info)
- Computing derived values after mapping
- Logging or validation
- Normalizing data before mapping

### Collection Mapping

ZMapper supports multiple collection mapping strategies:

```csharp
// Span-based array mapping (fastest, zero-copy iteration)
Person[] people = mapper.MapArray<PersonDto, Person>(dtos.AsSpan());

// List mapping from ReadOnlySpan<T>
List<Person> people = mapper.MapList<PersonDto, Person>(dtos.AsSpan());

// IEnumerable<T> mapping (works with LINQ, EF, HashSet, etc.)
IEnumerable<PersonDto> query = dtos.Where(d => d.IsActive);
List<Person> activePeople = mapper.MapList<PersonDto, Person>(query);
```

### Nested Object Mapping

Register mappings for each type in the object graph. ZMapper handles the nesting automatically:

```csharp
// Register from leaf types up
config.CreateMap<AddressDto, Address>();
config.CreateMap<CustomerDto, Customer>();  // Customer has Address property
config.CreateMap<OrderItemDto, OrderItem>();
config.CreateMap<OrderDto, Order>();        // Order has Customer + List<OrderItem>

var order = mapper.Map<OrderDto, Order>(orderDto);
// Entire object graph is mapped, including nested collections
```

Null nested objects are handled safely (result is `null`, no exceptions).

### Map to Existing Instance

Map into an already-constructed object instead of creating a new one:

```csharp
var existingUser = GetFromDatabase();
mapper.Map<UserDto, User>(dto, existingUser);
// existingUser's properties are updated in-place
```

### Extension Methods

For every registered mapping, ZMapper generates a `.ToXxx()` extension method:

```csharp
// Given: config.CreateMap<UserDto, User>()
// Generated: public static User ToUser(this UserDto source)

var user = dto.ToUser();  // Clean, discoverable, inlined by JIT
```

These methods are marked with `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for maximum performance.

## Supported Types

ZMapper handles all common .NET types out of the box:

| Category | Types |
|---|---|
| Primitives | `int`, `long`, `double`, `decimal`, `bool`, `char`, `byte`, etc. |
| Strings | `string`, including null, empty, unicode, and long strings |
| Date/Time | `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan` |
| Identifiers | `Guid`, `Uri` |
| Enums | All enum types (mapped by value) |
| Nullable | `int?`, `DateTime?`, `MyEnum?`, etc. |
| Collections | `List<T>`, `T[]`, `IReadOnlyList<T>`, `IEnumerable<T>` |
| Modern C# | `init` setters, `required` properties, records |

## Architecture

```
Your Code (Profiles or Fluent Config)
        |
        v
  Source Generator  <-- Compile Time (Roslyn)
        |
        v
  Generated C# Code
   - Per-class mapper (CreateMap pattern)
   - Unified mapper (Profile pattern)
   - Generic Map<TSource, TDest>() dispatcher
   - ToB() extension methods
   - MapArray / MapList batch methods
   - AddZMapper() DI extension
        |
        v
  Runtime Execution  <-- Zero Overhead
```

### NuGet Packages

| Package | Purpose |
|---|---|
| **ZMapper** | Core library + interfaces + Roslyn source generator (all-in-one NuGet) |

## What Gets Generated?

Given this profile configuration:

```csharp
public class UserProfile : IMapperProfile
{
    public void Configure(MapperConfiguration config)
    {
        config.CreateMap<UserDto, User>()
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Username));
    }
}
```

The source generator emits:

```csharp
// 1. Unified mapper with all profile mappings combined
public sealed class Mapper : IMapper
{
    public User Map_UserDto_To_User(UserDto source)
    {
        var destination = new User();
        destination.UserId = source.Id;
        destination.UserName = source.Username;
        return destination;
    }

    // + MapArray, MapList, MapList (IEnumerable), Map<T,T> dispatcher
    // + static Create() factory method

    public static Mapper Create() => new();
}

// 2. Extension method (inlined)
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static User ToUser(this UserDto source)
{
    var destination = new User();
    destination.UserId = source.Id;
    destination.UserName = source.Username;
    return destination;
}

// 3. DI extension (only when M.E.DI.Abstractions is referenced)
public static IServiceCollection AddZMapper(this IServiceCollection services)
{
    services.AddSingleton<IMapper>(new Mapper());
    return services;
}
```

Inspect generated code by adding to your `.csproj`:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>

<ItemGroup>
    <Compile Remove="$(CompilerGeneratedFilesOutputPath)/**" />
</ItemGroup>
```

## Project Structure

```
ZMapper/
  src/
    ZMapper/                  # Core library + interfaces (IMapper, IMapperProfile, etc.)
    ZMapper.SourceGenerator/  # Roslyn source generator
  tests/
    ZMapper.Tests/            # Unit tests (126 tests)
    ZMapper.Benchmarks/       # BenchmarkDotNet suite
  examples/
    ZMapper.Example/          # Working example project (Profile pattern)
```

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Acknowledgments

- **[AutoMapper](https://automapper.org/)** - Inspiration for the fluent API and profile pattern
- **[Mapperly](https://github.com/riok/mapperly)** - Pioneering source generation for object mapping
- **Roslyn** - Source generator infrastructure
