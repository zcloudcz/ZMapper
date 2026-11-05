# ZMapper — analýza (FABLE)

## Co to je
Produkční NuGet knihovna pro mapování objektů v .NET (verze **1.3.1**): AutoMapper-style fluent API + Mapperly-style compile-time generování kódu (Roslyn incremental source generator, žádná runtime reflexe, ~17 ns/objekt). Single-package řešení — `src/ZMapper/ZMapper.csproj` balí generátor (`src/ZMapper.SourceGenerator/ZMapperGenerator.cs`) do `analyzers/dotnet/cs/`. Multi-target `net10.0; net8.0; netstandard2.0`. Konzumují ji projekty Fakvio (InvoiceApi) a GIRS. Remote: `github.com/zcloudcz/ZMapper`.

## Stav
- **Aktivní, kanonický repozitář.** Poslední commit `1984e20 "bugs fix v131"` (2026-04-16), vše pushnuté na origin.
- **Pozor na branch model:** aktuálně je checkoutnutý branch `v122`, který ale nese commity v1.3.0/1.3.1 (`.net8 support - v130`, `bugs fix v131`). `master` je daleko pozadu (končí na `08ef68c readme fix`). Branch-per-version konvence se rozjela s realitou.
- **Necommitnuté soubory** (git status `??`): `CLAUDE.md`, `MISMATCH.md`, `ZMAPPER_MISSING.md`, `ZMAPPER_IMPLEMENTATION_PLAN.md` — analytické dokumenty existují jen lokálně.
- Testy: 24 souborů v `tests/ZMapper.Tests/` (xUnit + FluentAssertions), benchmarky v `tests/ZMapper.Benchmarks/`, příklad v `examples/ZMapper.Example/`.
- **Vztah k `C:\GIT\ZCLOUD\ZMapper_DEV`:** ZMapper_DEV je zastaralý vývojový snapshot v1.0.0 (cca leden 2026, není to git repo). Tento repozitář (ZMapper) je novější a jediný živý — viz Návrhy.

## Problémy

### Známá selhání mapperu (dokumentovaná)
1. **`MISMATCH.md`** — 3 blockery source generátoru nalezené na GIRS s v1.3.0 (neplatný kód `destination.this[]` pro `CreateMap<List<A>, List<B>>`, leak lambda parametru z `ConstructUsing`, duplicitní názvy `Map_List_To_List`). **Podle statické analýzy jsou všechny tři opravené v commitu `1984e20` (v1.3.1)** — regresní testy `tests/ZMapper.Tests/CollectionToCollectionTests.cs` explicitně referencují "Bug #1 and #3", generátor má `ReplaceLambdaParameter` (`ZMapperGenerator.cs:631-653`) a element-inclusive názvy metod (`ZMapperGenerator.cs:1622-1632`, `Map_ListOfCluster_To_ListOfListDto`). **Dokument ale neobsahuje žádnou poznámku o stavu** — vypadá to jako 3 otevřené blockery. Chybí ověření na reálném GIRS.
2. **`ZMapperFails.md`** — pitfally z v1.0.0 integrace do InvoiceApi (ForMember ignorován, BaseEntity properties nemapovány, nullable mismatch…). Většina byla opravena commity `66f8b1f` a `5719668` (viz `tests/ZMapper.Tests/LimitationFixTests.cs`), ale dokument opět **nemá status markery** a mate nové čtenáře/agenty.
3. **`ConstructUsing`/`ConvertUsing` extrakce zvládá jen `SimpleLambdaExpressionSyntax`** (`src/ZMapper.SourceGenerator/ZMapperGenerator.cs:647` a okolí) — lambda se závorkami `(s) => …` nebo dvouparametrová `(src, ctx) => …` se potichu ignoruje (mapping se vygeneruje bez factory). Tichá degradace, žádná diagnostika.

### Zastaralá dokumentace
4. **`CLAUDE.md` je v rozporu s kódem:** tvrdí "generator ~26k lines" (reálně **3 075 řádků**), "Requires .NET 9+" (reálně multi-target net8/net10/netstandard2.0) a tabulka "Missing Features" uvádí ConvertUsing, ConstructUsing, PreCondition, MemberList jako **"Not implemented"** — přitom commit `5719668` je implementoval a README je inzeruje. Agent řídící se CLAUDE.md bude implementovat už hotové věci.
5. **`ZMAPPER_MISSING.md`** — gap analýza vůči AutoMapperu je částečně přežitá ze stejného důvodu (P0/P1 položky 1, 2, 4, 5, 6, 7 už mají implementaci: `MappingContext.cs`, `MemberList.cs`, `MemberConverterSourceTests.cs`, `PreConditionTests.cs`, `MemberListTests.cs`).
6. README badge "build passing" je statický obrázek — žádná CI pipeline v repu není (`.github/workflows` neexistuje).

### Struktura
7. **Generátor je monolit** — celých 3 075 řádků v jediném `ZMapperGenerator.cs` (detekce, extrakce, modely, generování, diagnostika). Funkční, ale těžko udržovatelné.
8. Necommitnuté dokumenty (bod výše) + nejasný vztah `v122` ↔ `master` = riziko ztráty práce / zmatku při klonování (kdo naklonuje master, dostane starou verzi bez oprav).

## Návrhy

**P1 — kritické**
1. **Re-verifikovat MISMATCH.md bugy na GIRS** s v1.3.1 a do `MISMATCH.md` doplnit stav (fixed in 1.3.1 / still open). Dnes dokument tvrdí 3 compilation-error blockery, které už pravděpodobně neplatí.
2. **Commitnout** `CLAUDE.md`, `MISMATCH.md`, `ZMAPPER_MISSING.md`, `ZMAPPER_IMPLEMENTATION_PLAN.md` (po aktualizaci obsahu — viz P2/1).
3. **Srovnat branche:** merge `v122` → `master` (nebo master přejmenovat/oddeprecatovat). Default branch na GitHubu musí obsahovat v1.3.1.

**P2 — důležité**
1. **Aktualizovat `CLAUDE.md`** — opravit tabulku Missing Features (implementováno od `5719668`), počet řádků generátoru, target frameworky. Totéž pro `ZMAPPER_MISSING.md` (označit hotové položky).
2. **Označit `ZMapperFails.md` jako historický** (v1.0.0) a u každého bodu doplnit "fixed in vX.Y" / "still valid".
3. **Podpora parenthesized lambd v `ConstructUsing`/`ConvertUsing` extrakci** (`ZMapperGenerator.cs:647`) nebo aspoň ZMAP diagnostika "unsupported lambda form" místo tichého ignorování.
4. **Konsolidace se ZMapper_DEV:** ZMapper_DEV (v1.0.0, bez gitu) smazat nebo archivovat; před tím přenést unikátní dokumenty, které dávají smysl (`GETTING_STARTED.md`, `NUGET_PUBLISH.md`, případně `ENTERPRISE_ROADMAP.md`) do tohoto repa. Dvě paralelní codebase = riziko editace té špatné.

**P3 — nice-to-have**
1. Rozdělit `ZMapperGenerator.cs` na partial classes (Detection / Extraction / Models / Emit) — bez změny chování.
2. Přidat GitHub Actions CI (build + testy na net8/net10) a nahradit statický badge reálným.
3. Doplnit testy na `ConstructUsing` edge-cases (dnes jen okrajově v `ConvertUsingTests.cs` a `IntegrationTests.cs`).
