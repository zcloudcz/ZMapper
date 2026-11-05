# ZMapper 1.3.0 Source Generator — nalezené problémy

Testováno na projektu GIRS (ASP.NET Core 8, net8.0), ~14 profilů, ~2000 řádků mapper konfigurace.

---

## 1. `CreateMap<List<A>, List<B>>()` — generuje neplatný kód `destination.this[]`

### Vstup (profil)

```csharp
config.CreateMap<List<Cluster>, List<ListDto>>(MemberList.Destination)
    .ReverseMap();
```

### Vygenerovaný kód (ZMapper_GlobalExtensions.g.cs)

```csharp
public static List<ListDto> ToList(this List<Cluster> source)
{
    var destination = new List<ListDto>();
    destination.Capacity = source.Capacity;
    destination.this[] = source.this[] != null      // ← CHYBA: neplatný C# syntax
        ? source.this[].ToListDto()
        : null;
    return destination;
}
```

### Očekávaný kód

```csharp
public static List<ListDto> ToList(this List<Cluster> source)
{
    var destination = new List<ListDto>(source.Count);
    for (int i = 0; i < source.Count; i++)
    {
        destination.Add(source[i] != null
            ? source[i].ToListDto()
            : null);
    }
    return destination;
}
```

### Proč se to děje

Source generator pravděpodobně zachází s `List<T>` jako s běžným objektem a mapuje jeho properties (Capacity, indexer `this[]`). Ale `List<T>` je kolekce — měl by generovat iteraci (for/foreach), ne přiřazování do `this[]`.

### Workaround

Nemapovat `List<A> → List<B>` přímo — registrovat jen `CreateMap<A, B>()` a nechat runtime `MapList<A, B>()` / kolekční overloady. Jenže v AutoMapperu `CreateMap<List<A>, List<B>>()` funguje transparentně.

### Dotčená místa v GIRS

- `ConstsMapperProfile.cs:30` — `CreateMap<List<Cluster>, List<ListDto>>`
- `EmailMapperProfile.cs:35` — `CreateMap<List<DbViewEmailBlackListExcludedUser>, List<EmailBlackListExcludedUserDto>>`
- `EmailMapperProfile.cs:41` — `CreateMap<List<EmailBlackList>, List<EmailBlackListDto>>`

---

## 2. `ConstructUsing` — lambda parametry leakují do extension method jako neexistující proměnné

### Vstup (profil)

```csharp
config.CreateMap<List<DbViewEmailBlackListExcludedUser>, List<EmailBlackListExcludedUserDto>>(MemberList.Destination)
    .ConstructUsing(dbViewEmailBlackListExcludedUser =>
        CreateEmailBlackListExcludedUsers(dbViewEmailBlackListExcludedUser));

config.CreateMap<EmailBlackListDto, List<EmailBlackList>>(MemberList.Destination)
    .ConstructUsing(emailBlackListCompanyAccountableFunctionDto =>
        emailBlackListCompanyAccountableFunctionDto.AccountableFunctions
            .Select(accountableFunctionDto =>
                CreateEmailBlackListCompanyAccountableFunctionToEntity(
                    emailBlackListCompanyAccountableFunctionDto, accountableFunctionDto))
            .ToList());
```

### Vygenerovaný kód

```csharp
public static List<EmailBlackListExcludedUserDto> ToList(
    this List<DbViewEmailBlackListExcludedUser> source)
{
    // ↓ CHYBA: dbViewEmailBlackListExcludedUser je undefined proměnná (lambda parametr z ConstructUsing)
    var destination = CreateEmailBlackListExcludedUsers(dbViewEmailBlackListExcludedUser);
    destination.Capacity = source.Capacity;
    destination.this[] = source.this[] != null ...
}
```

### Očekávaný kód

```csharp
public static List<EmailBlackListExcludedUserDto> ToList(
    this List<DbViewEmailBlackListExcludedUser> source)
{
    // ConstructUsing lambda by měla dostat `source` jako argument
    var destination = CreateEmailBlackListExcludedUsers(source);
    ...
}
```

### Proč se to děje

Source generator extrahuje tělo lambda výrazu z `ConstructUsing`, ale nenahrazuje lambda parametr za `source`. Použije název parametru z lambda doslova (`dbViewEmailBlackListExcludedUser`), místo aby ho nahradil za `source`.

---

## 3. `CreateMap<List<A>, List<B>>()` — generuje metodu `Map_List_To_List` s duplicitním názvem

### Vstup (profily)

```csharp
// ConstsMapperProfile:
config.CreateMap<List<Cluster>, List<ListDto>>();

// EmailMapperProfile:
config.CreateMap<List<DbViewEmailBlackListExcludedUser>, List<EmailBlackListExcludedUserDto>>();
config.CreateMap<List<EmailBlackList>, List<EmailBlackListDto>>();
```

### Vygenerovaný kód (ZMapper_Mapper.g.cs)

```csharp
// Všechny 3 mappingy generují metodu se stejným názvem:
public List<ListDto> Map_List_To_List(List<Cluster> source) { ... }
public List<EmailBlackListExcludedUserDto> Map_List_To_List(List<DbViewEmailBlackListExcludedUser> source) { ... }
public List<EmailBlackListDto> Map_List_To_List(List<EmailBlackList> source) { ... }
```

### Problém

Název metody `Map_List_To_List` se odvozuje jen z názvu vnějšího typu (`List` → `List`), ignoruje generické argumenty. Všechny `CreateMap<List<X>, List<Y>>()` tak generují stejné jméno metody. Naopak pro `CreateMap<A, B>()` (non-generic) se generuje unikátní `Map_A_To_B`.

### Očekávání

Název metody by měl zahrnout generické argumenty, např. `Map_ListOfCluster_To_ListOfListDto`.

---

## Shrnutí

| # | Problém | Závažnost | Blocker? |
|---|---------|-----------|----------|
| 1 | `CreateMap<List<A>, List<B>>()` generuje `this[]` syntax | VYSOKÁ | ANO — neplatný C#, compilation error |
| 2 | `ConstructUsing` lambda parametry leakují jako neexistující proměnné | VYSOKÁ | ANO — undefined variable, compilation error |
| 3 | `Map_List_To_List` duplicitní název metody pro různé generické argumenty | STŘEDNÍ | ANO — CS0111 duplicate member |

Všechny 3 problémy jsou v **source generátoru** (`ZMapper.SourceGenerator`). Runtime mapper (`IMapper.Map<>`) pravděpodobně funguje správně, ale source gen produkuje nekompilabilný kód.
