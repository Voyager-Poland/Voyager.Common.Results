# ADR-0013: Dodanie `net10.0` jako LTS TFM i powrót do spójności z ADR-0002

**Status:** Zaakceptowane
**Data:** 2026-04-22
**Kontekst:** Voyager.Common.Results — strategia wielo-targetowania i dystrybucja paczki NuGet
**JIRA:** BZPB-430
**Supersedes:** ADR-0002 (rozszerza, nie unieważnia — patrz sekcja "Relacja z ADR-0002")

## Problem

ADR-0002 (grudzień 2025) ustalił matrycę `net48;net6.0;net8.0` oraz zasadę **"Disable `ImplicitUsings` globally"** z eksplicytnymi global usings per projekt. Przewidywał też: *"future drop of net6 will require a follow-up ADR"*.

Cztery miesiące później trzy okoliczności wymuszają decyzję:

1. **.NET 6 jest poza wsparciem od 2024-11-12** ([Microsoft Lifecycle](https://learn.microsoft.com/lifecycle/end-of-support/end-of-support-2024#end-of-servicing)) — nie dostaje patchy bezpieczeństwa od ~17 miesięcy. Azure App Service/Functions już zdjął obsługę. Skanery SCA (Snyk, Dependabot) flagują biblioteki targetujące EOL runtime; dla nas to ryzyko audytowe w sprzedaży klientom.
2. **Kolejne NuGety w ekosystemie Voyagera migrują na `net10.0`** — m.in. EF Core 10 wymaga runtime'u net10. Konsumenci biblioteki Voyager.Common.Results coraz częściej budują na `net10.0` i dostawanie assembly downlevelowanego z `net8.0` zmniejsza korzyści z JIT/AOT.
3. **W obecnym `Voyager.Common.Results.csproj` istnieje niezudokumentowany wyjątek** — `ImplicitUsings=enable` dla `net8.0`, sprzeczny z literą ADR-0002. Jego rozszerzenie na `net10.0` pogłębiłoby niespójność. Reviewer na PR #14 wprost poprosił o rozstrzygnięcie.

BZPB-430 jest pierwszym krokiem strategii. Drop net6 to osobny ticket (follow-up, MAJOR bump) — niniejszy ADR **nie usuwa net6**, tylko dodaje net10 i porządkuje implicit usings.

## Rozważone opcje

### Opcja A — Tylko dodać net10, zostawić wyjątek ImplicitUsings na net8 i rozszerzyć na net10

Minimalna zmiana. Zachowuje status quo niespójności.

**Odrzucona** — utrwala niezudokumentowany wyjątek od ADR-0002. Reviewer explicite wskazał że "warto rozstrzygnąć przy okazji".

### Opcja B — Supersede ADR-0002: włączyć `ImplicitUsings=enable` dla wszystkich modernych TFM i usunąć `GlobalUsings.cs`

Spójność przez włączenie, nie wyłączenie.

**Odrzucona** — rationale ADR-0002 pozostaje aktualne: *"Predictability: Explicit global usings avoid TFM-specific defaults"* oraz *"Build Reliability: Prevents missing System, System.Linq etc. when implicit usings differ between SDK versions"*. Implicit usings SDK dla classlib w różnych wersjach .NET mogą różnić się zawartością; explicit usings dają deterministyczny surface. Dodatkowo `net48` i tak musi mieć explicit (SDK nie wspiera ImplicitUsings dla .NET Framework), więc pełna spójność Opcji B jest nieosiągalna.

### Opcja C — Usunąć `ImplicitUsings=enable` z csproj, pełna zgodność z ADR-0002 ✅

Polegamy wyłącznie na `GlobalUsings.cs`, który już eksponuje `System`, `System.Collections.Generic`, `System.Linq`, `System.Threading`, `System.Threading.Tasks` pod `#if NET6_0_OR_GREATER`.

**Wybrana** — najtańsza ścieżka do spójności. Biblioteka nie używa `System.IO` ani `System.Net.Http` bezpośrednio (jedyne wystąpienie `HttpRequestException` w `Error.cs:335` to porównanie `GetType().Name`, nie wymaga `using`), więc różnica między implicit a explicit SDK usings jest dla tej biblioteki pusta.

## Decyzja

1. **`TargetFrameworks = net48;net6.0;net8.0;net10.0`** w `Directory.Build.props`.
   `LangVersion=latest` dla net10.0 (C# 14), `latest` dla net8.0 (C# 13 effectively), `10.0` dla net6.0/net48 (jak było).

2. **NuGet pack/publish przeniesione z net8.0 SDK na net10.0 SDK** w `.github/workflows/ci.yml`.
   Paczka i tak zawiera wszystkie 4 TFM-y (`lib/net48`, `lib/net6.0`, `lib/net8.0`, `lib/net10.0`) — zmienia się tylko SDK, którym jest produkowana. .NET 10 SDK jest LTS do 2028-11-14 i potrafi budować wszystkie poprzednie TFM-y (downlevel build jest oficjalnie wspierany). Konsumenci paczki nie zauważą różnicy.

3. **Usunąć `<PropertyGroup>` z `<ImplicitUsings>enable</ImplicitUsings>`** z `src/Voyager.Common.Results/Voyager.Common.Results.csproj`.
   `ImplicitUsings=disable` (globalnie w `Directory.Build.props`) obowiązuje dla wszystkich TFM. Global usings dostarcza `GlobalUsings.cs` per projekt (istnieje już dla net6+). Sprzeczność z ADR-0002 zostaje naprawiona.

## Uzasadnienie

### Dlaczego właśnie net10 (a nie net9)

- **net9 jest STS (Standard Term Support)** — kończy się 2026-05-12, czyli za ~miesiąc od tej decyzji. Dodawanie go jako TFM nie ma sensu.
- **net10 jest LTS do 2028-11-14** ([Releases and support](https://learn.microsoft.com/dotnet/core/releases-and-support)) — daje 2.5 roku stabilnej podstawy, zbieżne z cyklem LTS Voyagera.

### Co daje net10 dla tej biblioteki (Result type)

Zmiany istotne dla hot-path railway-oriented programming, z [Performance Improvements in .NET 10 (Stephen Toub)](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/):

- **Physical promotion dla struct arguments** — JIT trzyma `Result<T>` i `Error` w rejestrach zamiast w pamięci. Redukuje koszt `Map`/`Bind`/`Tap`/`Match`, które intensywnie przekazują te struktury.
- **Escape analysis dla delegatów i closures** — delegaty do `Map`/`Bind`/`Match` mogą lądować na stosie zamiast sterty. To był dotychczas największy koszt railway programming; net10 to niweluje.
- **Stack allocation małych tablic ref/value + devirtualizacja interface methods** — korzyść dla `Combine`, `Partition`, `TraverseAsync` w `ResultCollectionExtensions`.
- **JIT inlining bloków try-finally** — `Finally`/`Tap` wreszcie inline'owalne.

### Dlaczego pack/publish na net10 SDK (a nie net8)

- net8 SDK kończy LTS 2026-11-10. Za 7 miesięcy i tak musielibyśmy zmieniać.
- net10 SDK jest LTS do 2028-11-14 — długi horyzont stabilności.
- Nowszy SDK = nowsze analizatory (`AnalysisLevel=latest` w `Build.CodeQuality.props`), nowsza kompilacja Roslyn, nowsze optymalizacje MSBuild.
- Zero wpływu na konsumentów — TFM-y w paczce zostają te same.

### Dlaczego powrót do `ImplicitUsings=disable` (a nie rozszerzenie na net10)

- Rationale ADR-0002 wciąż aktualne: deterministyczny namespace surface, niezależny od wersji SDK.
- Biblioteka ma tylko jeden projekt z istniejącym wyjątkiem (`Voyager.Common.Results.csproj`). Usunięcie go redukuje kod konfiguracji, nie dodaje.
- `GlobalUsings.cs` już robi robotę dla NET6_0_OR_GREATER. Analiza kodu potwierdza, że biblioteka nie używa żadnego namespace-u obecnego w implicit SDK a nieobecnego w `GlobalUsings.cs` (konkretnie brakowałoby `System.IO` i `System.Net.Http` — żaden nie jest używany).

## Konsekwencje

### Pozytywne

- **Spójność z ADR-0002** — jeden mechanizm global usings (explicit `GlobalUsings.cs`) dla wszystkich TFM.
- **Długoterminowy horyzont LTS** (net10 do 2028-11-14; net8 jako fallback do 2026-11-10).
- **Odblokowuje następny krok** — drop net6.0 w osobnym ADR/ticketcie (MAJOR bump v2.0.0). Matryca z net10 w miejscu dowodzi, że konsumenci mają ścieżkę migracji.
- **Gotowość na AOT / trimming** — biblioteka jest kandydatem na `<IsAotCompatible>true</IsAotCompatible>` warunkowo dla net10 (zero reflection, zero dynamic code). Osobna decyzja, ale ta zmiana ją umożliwia.

### Negatywne

- **Matryca CI rośnie z 3 do 4 TFM** — ~33% dłuższy build+test (akceptowalne; drop net6 w następnym ticketcie wróci do 3 TFM).
- **LangVersion wciąż zablokowany na `10.0`** dla net6.0 — nie możemy używać C# 13/14 features w kodzie współdzielonym między TFM. Zostaje do rozwiązania w follow-up ADR (drop net6).

### Wpływ na konsumentów

- Żaden breaking change. Dodanie TFM jest MINOR bump (`v1.12.0`).
- Konsumenci na net10 dostają assembly kompilowane exact-match zamiast downlevel z net8.
- Paczka zwiększa się o jeden folder `lib/net10.0/` (~rozmiar porównywalny do net8).

## Relacja z ADR-0002

ADR-0002 **nie jest unieważniony**. Ten ADR:

- **Rozszerza** listę TFM o `net10.0` (ADR-0002 przewidział taki follow-up w sekcji Consequences).
- **Utrzymuje** zasadę "Disable ImplicitUsings globally" — wręcz **wzmacnia ją**, usuwając niezudokumentowany wyjątek dla net8.0.
- **Nie dotyka** decyzji o net6 (osobny ADR).

## Plan wdrożenia

1. ✅ Ten ADR.
2. ✅ `Directory.Build.props` — dodanie `net10.0` + `LangVersion` per-TFM (już w PR #14).
3. ✅ CI: matryca rozszerzona, pack/publish przeniesione na 10.0.x (już w PR #14).
4. ✅ README, CHANGELOG, metadata paczki (już w PR #14).
5. ✅ Usunięcie `<ImplicitUsings>enable</ImplicitUsings>` z `Voyager.Common.Results.csproj` (follow-up commit w PR #14 na podstawie tego ADR).
6. ⏭ Osobny ticket JIRA: **Drop net6.0** → ADR-0014, `v2.0.0`.
7. ⏭ Osobny ticket JIRA: **AOT readiness** → warunkowy `<IsAotCompatible>true</IsAotCompatible>` dla net10.0.

---

**Powiązane:**

- [ADR-0002: Add net6 Target and Disable Implicit Usings](./ADR-0002-net6-and-implicit-usings.md) — ramy dla tej decyzji; niniejszy ADR rozszerza matrycę TFM i wzmacnia zasadę disable ImplicitUsings.
- [ADR-001: MinVer Git-Based Versioning](./ADR-001-MinVer-Git-Based-Versioning.md) — zmiana TFM wymaga `dotnet clean` przed rebuild.

**Źródła zewnętrzne:**

- [.NET Releases and support (Microsoft Learn)](https://learn.microsoft.com/dotnet/core/releases-and-support)
- [End of Support in 2024 — .NET 6 LTS retirement](https://learn.microsoft.com/lifecycle/end-of-support/end-of-support-2024#end-of-servicing)
- [What's new in .NET 10 runtime](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/runtime)
- [Performance Improvements in .NET 10 — Stephen Toub, devblogs](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/)
- [What's new in C# 14](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14)
- [EF Core 10 — What's new](https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew)
