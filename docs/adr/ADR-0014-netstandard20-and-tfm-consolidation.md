# ADR-0014: Konsolidacja matrycy TFM — `netstandard2.0` + `net10.0` (drop `net48`, `net6.0`, `net8.0`)

**Status:** Zaakceptowane (wdrożone — branch `master`, planowany tag `v2.0.0`)
**Data:** 2026-05-04
**Kontekst:** Voyager.Common.Results — strategia wielo-targetowania i dystrybucja paczki NuGet
**JIRA:** TBD
**Supersedes:** ADR-0002 (matryca TFM); rozszerza ADR-0013 (drop net6 zapowiedziany w punkcie 6 planu wdrożenia)

## Problem

ADR-0013 (kwiecień 2026) doprowadził matrycę do `net48;net6.0;net8.0;net10.0` (4 TFM-y) i wprost zapowiedział *"Osobny ticket JIRA: Drop net6.0 → ADR-0014, v2.0.0"*. Pytanie z PR review: skoro dropujemy net6, to czy faktycznie potrzebujemy 3 osobnych TFM-ów dla bibliotek tej klasy, czy istnieje prostsza ścieżka?

Kontekst techniczny biblioteki:

1. **Powierzchnia API biblioteki to wyłącznie BCL netstandard2.0**. `grep` po `src/Voyager.Common.Results` potwierdza zerowe użycie modernych API (`ValueTask`, `IAsyncEnumerable`, `Span<T>`, `Memory<T>`, `Index`/`Range`). Cała biblioteka opiera się o `System`, `System.Collections.Generic`, `System.Linq`, `System.Threading`, `System.Threading.Tasks`, `System.Text` — wszystkie obecne na netstandard2.0.
2. **Jedyna funkcja językowa wymagająca polyfilla to `record` z `init`** (potrzebuje `IsExternalInit`). Polyfill jest już dostarczony jako `PackageReference` warunkowo dla `net48` — `LangVersion=10.0` dla netstandard2.0 wspiera `record` z polyfillem identycznie jak dla net48.
3. **netstandard2.0 to TFM zgodny binarnie z `net48`** (.NET Framework 4.6.1+) **oraz wszystkimi wersjami .NET Core/5/6/7/8/9/10**. Jedna paczka `lib/netstandard2.0/` obsługuje wszystkie te runtime'y, dodatkowo Mono, Xamarin i Unity.
4. **Optymalizacje JIT z .NET 10 (physical promotion, escape analysis dla closures, stack alloc, devirtualization)** opisane w ADR-0013 są stosowane przez JIT na poziomie IL **w czasie wykonywania**, niezależnie od TFM, dla którego skompilowano assembly. Assembly netstandard2.0 wykonywane na runtime net10 dostaje te same korzyści perf — ADR-0013 przypisał je TFM-owi, ale są one runtime-side. Co naprawdę zyskuje się z TFM-specific assembly to (a) dostęp do nowszych BCL API w czasie kompilacji, (b) `[IsAotCompatible]` i annotacje trim, (c) precyzyjniejsze nullable annotations w referencyjnych BCL.
5. **Bieżąca matryca 4-TFM-owa kosztuje** — ~33% dłuższy CI niż 3-TFM (osobna `test-net48` na windows-latest), 4× pliki `lib/<tfm>/` w paczce, 4× przebieg analizatorów.

## Rozważone opcje

### Opcja A — Zostaw net48 i net8.0, dropuj tylko net6.0

Matryca: `net48;net8.0;net10.0`. Minimalne odchylenie od ADR-0013.

**Odrzucona** — nie wykorzystuje faktu, że biblioteka jest perfect-fit dla netstandard2.0. Nadal trzymamy `#if NET48` w 8 plikach źródłowych (Result.cs, ResultT.cs, Error.cs, 5× extensions, GlobalUsings.cs) tylko po to, żeby zaadresować różnicę między `ImplicitUsings` SDK net48 (brak) a SDK net8/net10 (są). To duplikacja konfiguracji bez funkcjonalnego zysku.

### Opcja B — `netstandard2.0;net10.0` (rekomendowana) ✅

Jedna paczka netstandard2.0 obsługuje **wszystko** od net48 wzwyż. net10.0 dodatkowo dla:
- przyszłej gotowości AOT (`<IsAotCompatible>true</IsAotCompatible>` jest atrybutem TFM-specific dla net8+),
- przyszłego użycia modernych API (jeśli kiedyś dodamy `ValueTask`-owe overloady, `IAsyncEnumerable<T>` w `TraverseAsync` itp.) bez konieczności wprowadzania nowego TFM-u w przyszłości,
- spójności z planem strategii Voyagera (net10 jest LTS do 2028-11-14, ekosystem Voyagera już tam idzie — patrz ADR-0013 punkt 2).

**Wybrana** — patrz "Decyzja" niżej.

### Opcja C — `netstandard2.0` only

Najprostsza możliwa konfiguracja. Jedna paczka, jeden binary, koniec dyskusji.

**Odrzucona, ale słaby drugi wybór** — traci ścieżkę do AOT/trimming, traci możliwość TFM-specific overloadów (np. `ValueTask` dla net6+), traci wnioski z ADR-0013 że ekosystem Voyagera idzie na net10. Jeśli kiedyś dodamy ValueTask-owe overloady (BindAsync może zwracać ValueTask na net6+), będziemy musieli ponownie rozszerzać matrycę. Dla biblioteki z roadmapą perf/AOT nie warto się tym ograniczać.

### Opcja D — `netstandard2.0;net8.0;net10.0`

Hybryda zachowująca net8 jako "fallback LTS" (LTS do 2026-11-10).

**Odrzucona** — net8 LTS kończy się za ~6 miesięcy od daty tego ADR. Konsumenci na net8 dostaną `lib/netstandard2.0/` (działa identycznie). Po listopadzie 2026 net8.0 TFM stanie się tym, czym dziś jest net6.0 — kandydatem do drop-u w kolejnym ADR. Lepiej nie wprowadzać go po to, by go zaraz usuwać.

## Decyzja

1. **`TargetFrameworks = netstandard2.0;net10.0`** w `Directory.Build.props`.
2. **Pełny drop `net48`** jako odrębny TFM. Konsumenci na .NET Framework 4.8 dostają `lib/netstandard2.0/` (zgodne binarnie, weryfikowane przez Microsoft od 2017 roku).
3. **Pełny drop `net6.0`** zgodnie z zapowiedzią ADR-0013. Runtime EOL od 2024-11-12 (~18 miesięcy temu). Konsumenci na net6 dostają `lib/netstandard2.0/`.
4. **Pełny drop `net8.0`** jako odrębny TFM. Konsumenci na net8 dostają `lib/netstandard2.0/`.
5. **`IsExternalInit` PackageReference rozszerzony na netstandard2.0** (`Condition="'$(TargetFramework)' == 'netstandard2.0'"`).
6. **Usunięcie `#if NET48` z 8 plików** — konsolidacja `using` deklaracji jako bezwarunkowe (LangVersion=10.0 dla netstandard2.0 nie ma ImplicitUsings, więc explicit `using` są wymagane uniwersalnie). Spójne z duchem ADR-0002 (eksplicytne usings).
7. **`GlobalUsings.cs` — usunąć warunek `#if NET6_0_OR_GREATER`** lub zlikwidować plik na rzecz jednolitych `using` per-plik. Decyzja ergonomiczna do follow-up commit; ten ADR ich nie wymusza.
8. **Test projects: `net48;net8.0;net10.0`** — testujemy assembly netstandard2.0 na każdym runtime, który deklarujemy jako wspierany (net48 weryfikuje zgodność z .NET Framework, net8.0 weryfikuje współczesny LTS, net10.0 weryfikuje TFM-specific assembly). net6.0 z testów wypada — zgodnie z drop-em.
9. **CI matrix**: `net8.0;net10.0` na ubuntu-latest + osobny job `test-net48` na windows-latest. Drop joba dla net6. Job pack/publish nadal na net10 SDK (per ADR-0013).
10. **Bump MAJOR → v2.0.0** zgodnie z zapowiedzią ADR-0013. Zmiana matrycy TFM jest breaking-by-convention nawet jeśli powierzchnia API jest niezmieniona.

## Uzasadnienie

### Dlaczego netstandard2.0 wystarcza dla net48

netstandard2.0 została zaprojektowana właśnie po to, żeby zniknęła potrzeba osobnego TFM-u dla .NET Framework 4.6.1+. .NET Framework 4.8 ma pełne wsparcie dla netstandard2.0 wbudowane w runtime (od 4.7.2 brak runtime shim-ów). Polyfill `IsExternalInit` (`PackageReference` z `PrivateAssets=all`, znacznik `internal`) rozwiązuje jedyną brakującą funkcję językową. Ta sama strategia jest stosowana przez `Microsoft.Bcl.AsyncInterfaces`, `System.Text.Json` (do czasu .NET 5), `Polly` (przez wiele wersji) i większość bibliotek BCL targetujących wszystkie runtime'y.

### Dlaczego nie tracimy nic mierzalnego

- **Zero modernych API w bibliotece** (zweryfikowane grep-em). `Map`, `Bind`, `Tap`, `Match`, `Combine`, `Partition`, `BindWithRetryAsync` — wszystko to korzysta z `Task<T>`, `Func<>`, `IEnumerable<>`. Dostępne na netstandard2.0.
- **JIT optymalizacje z ADR-0013 (physical promotion, escape analysis, stack alloc, devirtualization) działają na IL, w czasie wykonywania**. Konsument biblioteki działający na net10 runtime dostanie te optymalizacje dla kodu z `Map`/`Bind`/`Tap` niezależnie od tego, czy assembly jest skompilowany do netstandard2.0 czy do net10.0. To oznacza, że perf rationale ADR-0013 nie wymaga TFM-specific assembly — wymaga TFM-specific *runtime* po stronie konsumenta. Konsument net10 wykonujący `lib/netstandard2.0/Voyager.Common.Results.dll` dostaje wszystkie korzyści JIT net10.
- **AOT/trimming readiness** — to *jedyny* mierzalny argument za TFM-specific assembly net10 (atrybut `[IsAotCompatible]`, `RequiresUnreferencedCode` annotations, trim warnings). Dlatego zostawiamy `net10.0` jako drugi TFM.

### Dlaczego mimo wszystko zostawiamy net10.0

Trzy powody:
1. **AOT/trim annotations** wymagają TFM net8+. ADR-0013 wprost zostawił AOT readiness jako follow-up — utrzymując net10.0 zostawiamy tę ścieżkę otwartą bez kolejnej zmiany matrycy w przyszłości.
2. **Nullable annotations w referencyjnym BCL** — `string?` zwracane przez np. `Environment.GetEnvironmentVariable` jest oznaczone nullowo na net6+, a nieoznaczone na netstandard2.0. Dla biblioteki z `Nullable=enable` to różnica w jakości warning-ów. Konsument net10 importujący z `lib/net10.0/` dostaje ostrzejsze nullable-checking po naszej stronie kodu.
3. **Forward compatibility z ekosystemem Voyagera** — ADR-0013 ustalił net10 jako kierunek strategii. Utrzymanie tego TFM zachowuje spójność i pozwala w przyszłości dodać `ValueTask`-owe overloady czy `IAsyncEnumerable<T>` warunkowo dla net10 bez kolejnej zmiany matrycy.

### Dlaczego konsolidacja `#if NET48`

Po dropie net48 jako TFM, dyrektywa `#if NET48` traci sens (netstandard2.0 nie definiuje `NET48`). Dyrektywy `#if NET48` w 8 plikach istniały tylko dlatego, że SDK net48 nie ma `ImplicitUsings` (a SDK net6+/net8+/net10+ tak — w ramach ADR-0002 wyłączony, ale `ImplicitUsings=disable` to jednak osobna zmienna konfiguracji). Po unifikacji na netstandard2.0 wszędzie jest tak samo: brak ImplicitUsings (netstandard2.0 SDK ich nie wspiera), więc `using` deklaracje są wymagane uniwersalnie. Po prostu robimy je bezwarunkowo. To **wzmacnia ADR-0002** — dosłownie "explicit usings everywhere", bez warunków.

## Konsekwencje

### Pozytywne

- **Matryca: 4 → 2 TFM** (-50% wymiarów paczki). Paczka zawiera `lib/netstandard2.0/` + `lib/net10.0/` zamiast czterech katalogów. CI build/test ~50% szybszy.
- **Eliminacja jobu `test-net48`** na osobnym Windows runner-ze nie jest możliwa (testy net48 nadal weryfikują binary compatibility netstandard2.0 → net48), ale CI matrix dla ubuntu-latest spada z 3 do 2 TFM.
- **Eliminacja `#if NET48` w 8 plikach** — czystszy kod, mniej do utrzymania.
- **Wydłużone wsparcie dla legacy** — netstandard2.0 obejmuje również runtime'y, których obecnie nie wspieramy: net461-net47, .NET Core 2.x, Mono, Xamarin, Unity. Biblioteka staje się dostępna dla szerszej puli konsumentów bez dodatkowej pracy.
- **Spójność z ADR-0002** — explicit usings everywhere, bez warunków per-TFM.
- **Spójność z ADR-0013** — strategia "net10 jako modern target" zachowana, plan drop-net6 wykonany, AOT readiness pozostaje otwarty.

### Negatywne

- **Breaking change w sensie semver-by-convention** — wymaga MAJOR bump `v2.0.0`. Konsumenci muszą świadomie zaktualizować. Zgodnie z planem ADR-0013 punkt 6.
- **Konsumenci net8.0** od momentu wydania v2.0.0 dostają assembly netstandard2.0 zamiast net8-specific. Funkcjonalnie identyczne (zerowe użycie net8-specific API), ale formalnie regresja precyzji TFM. Konsumenci net10 nie są dotknięci (nadal mają `lib/net10.0/`).
- **Polyfill `IsExternalInit` jest teraz wpisany w PackageReference dla netstandard2.0** — drobna zwiększona powierzchnia transitive dependency dla wszystkich konsumentów. Pakiet `IsExternalInit` jest jednak markowany internal, więc nie konfliktuje z runtime-owym `IsExternalInit` na .NET 5+.
- **LangVersion zostaje na `latest` dla net10.0 i `10.0` dla netstandard2.0** — kod współdzielony między TFM-ami nadal nie może używać C# 11/12/13/14 features. Bez zmian względem dziś (gdzie kod współdzielony był limitowany przez net6=10.0).

### Wpływ na konsumentów

- **net48** — bez zmian funkcjonalnych. Dotychczas wybierali `lib/net48/`, po v2.0.0 wybierają `lib/netstandard2.0/`. Powierzchnia API identyczna, polyfill nadal obecny.
- **net6.0** — runtime EOL, formalny komunikat: zaktualizuj do net8/net10. Jeśli zostają na net6, biorą `lib/netstandard2.0/` (działa, ale runtime poza wsparciem MSFT).
- **net8.0** — bez zmian funkcjonalnych. Wybierają `lib/netstandard2.0/` zamiast `lib/net8.0/`. Identyczne API.
- **net10.0** — bez zmian. Nadal `lib/net10.0/`.

## Relacja z poprzednimi ADR

- **ADR-0002** (matryca + ImplicitUsings) — częściowo superseded. Reguła "Disable ImplicitUsings globally" pozostaje (wręcz wzmocniona — netstandard2.0 SDK nie ma ImplicitUsings, więc explicit usings są wymuszone strukturalnie). Lista TFM jest zastąpiona.
- **ADR-0013** (dodanie net10) — *nie* unieważniony. Ten ADR wykonuje punkt 6 jego planu wdrożenia ("Drop net6.0 → ADR-0014, v2.0.0") oraz idzie dalej (drop net48, net8.0 na rzecz netstandard2.0). Argumentacja perf z ADR-0013 zostaje przeformułowana: korzyści JIT są runtime-side (każdy konsument net10 je dostaje, niezależnie od TFM assembly), a wartość TFM-specific net10.0 to AOT-readiness i nullable annotations BCL.

## Plan wdrożenia

1. ✅ Ten ADR (review).
2. ✅ `Directory.Build.props` — `<TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks>` + LangVersion per-TFM (`10.0` dla netstandard2.0, `latest` dla net10.0).
3. ✅ `Voyager.Common.Results.csproj` — rozszerzono condition `IsExternalInit` PackageReference na `netstandard2.0`. Description i PackageTags zaktualizowane.
4. ✅ Usunięto `#if NET48` z 8 plików (`Error.cs`, `Result.cs`, `ResultT.cs`, 4× `Extensions/*.cs`, `Resilience/CircuitBreakerPolicy.cs`, `Resilience/ResultCircuitBreakerExtensions.cs`); `using` skonsolidowane jako bezwarunkowe.
5. ✅ `GlobalUsings.cs` — usunięto warunek `#if NET6_0_OR_GREATER` w bibliotece i w obu test-projektach.
6. ✅ Test projects — TFM zmieniony na `net48;net8.0;net10.0` (override globalnego `netstandard2.0;net10.0`). Testują assembly netstandard2.0 na każdym wspieranym runtime + assembly net10.0 na net10.
7. ✅ CI workflow — usunięto matrix entry dla net6.0; `test-net48` na windows-latest pozostawiony; **publish/deploy zgated na tag push** (`startsWith(github.ref, 'refs/tags/v')`); pack+upload artefaktów też gated na tag.
8. ✅ README / CHANGELOG / copilot-instructions / PR template zaktualizowane.
9. ⏭ Tag `v2.0.0`, release notes z migracją.
10. ⏭ Follow-up: AOT-readiness dla net10.0 (`<IsAotCompatible>true</IsAotCompatible>` warunkowo) — osobny ADR.

## Odpowiedź na pytanie z review

> "Czy potrzebujemy net48, jeśli dodajemy netstandard2.0?"

**Nie.** netstandard2.0 jest binary-compatible z .NET Framework 4.6.1+, włączając net48. Polyfill `IsExternalInit` (jedyna potrzeba językowa) działa identycznie dla obu TFM. Test project nadal trzyma `net48` jako test target, żeby zweryfikować, że netstandard2.0 binary działa na .NET Framework runtime — to jest realne testowanie kompatybilności, nie jest TFM bibliotek.

---

**Powiązane:**

- [ADR-0002: Add net6 Target and Disable Implicit Usings](./ADR-0002-net6-and-implicit-usings.md) — ten ADR zastępuje matrycę TFM, utrzymuje regułę explicit usings.
- [ADR-0013: Dodanie net10.0 jako LTS TFM](./ADR-0013-net10-lts-target-framework.md) — ten ADR realizuje punkt 6 planu wdrożenia (drop net6) i idzie dalej (konsolidacja na netstandard2.0).
- [ADR-001: MinVer Git-Based Versioning](./ADR-001-MinVer-Git-Based-Versioning.md) — zmiana matrycy TFM wymaga `dotnet clean` przed rebuild. v2.0.0 to dedykowany tag.

**Źródła zewnętrzne:**

- [.NET Standard 2.0 — supported platforms](https://learn.microsoft.com/dotnet/standard/net-standard?tabs=net-standard-2-0)
- [.NET Standard 2.0 vs .NET Framework 4.6.1+ binary compatibility](https://learn.microsoft.com/dotnet/standard/net-standard?tabs=net-standard-2-0#net-standard-versions)
- [.NET 6 End of Support (Microsoft Lifecycle)](https://learn.microsoft.com/lifecycle/end-of-support/end-of-support-2024#end-of-servicing)
- [.NET 8 LTS support timeline (ends 2026-11-10)](https://learn.microsoft.com/dotnet/core/releases-and-support)
- [IsExternalInit polyfill — manatools/IsExternalInit](https://www.nuget.org/packages/IsExternalInit/)
- [Performance Improvements in .NET 10 — Stephen Toub](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/) (kontekst dla rationale "JIT optymalizacje są runtime-side")
- [AOT compatibility analyzers — IsAotCompatible](https://learn.microsoft.com/dotnet/core/deploying/native-aot/) (uzasadnienie utrzymania net10.0 jako TFM)
