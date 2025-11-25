# ADR-001: Przejście na MinVer i wersjonowanie oparte na Git tagach

**Status:** Zaakceptowane  
**Data:** 2025-11-18  
**Autor:** Andrzej Świstowski  
**Zadanie JIRA:** [BZPB-386](https://jira.voyager-poland.com/browse/BZPB-386)

## Problem

Dotychczasowy proces wersjonowania biblioteki `Voyager.Common.Results` wymagał:

1. **Ręcznej edycji wersji** w pliku `.csproj` dla zmian major/minor
2. **Automatycznego inkrementowania build number** przez GitHub Actions job `newversion`, który:
   - Modyfikował plik `.csproj` w CI/CD
   - Commitował zmianę wersji z powrotem do repozytorium
   - Tworzył dodatkowy commit po każdym merge do `main`

**Problemy tego podejścia:**

- ❌ **Brak single source of truth** - wersja w `.csproj` mogła nie odpowiadać rzeczywistemu stanowi kodu
- ❌ **Zanieczyszczenie historii Git** - automatyczne commity typu "Bump project version to 1.0.X" nie niosły wartości biznesowej
- ❌ **Ryzyko konfliktów** - jednoczesny push i automatyczny commit mogły powodować konflikty
- ❌ **Brak semantycznej kontroli wersji** - developerzy nie musieli świadomie decydować o numerze wersji
- ❌ **Trudność w odtworzeniu stanu** - dla danego pakietu NuGet trudno było jednoznacznie wskazać commit w Git
- ❌ **Brak wymuszenia tagowania** - tagi Git były opcjonalne, nie stanowiły integralnej części procesu release

**Cele biznesowe:**

> "Wprowadzić CI i instrukcje jak prawidłowo aktualizować wersję. Wiedza jak prawidłowo tagować i wprowadzenie wymagania tagowania aplikacji podniesie poziom techniczny we wszystkich projektach" - z opisu BZPB-386

## Decyzja

Przechodzimy na **MinVer** - narzędzie do automatycznego wersjonowania opartego na Git tagach z następującymi zasadami:

### 1. Git tagi jako single source of truth

- **Oficjalne release** = Git tag w formacie `v<MAJOR>.<MINOR>.<PATCH>` (np. `v1.2.6`)
- **Preview builds** = automatycznie generowane między tagami z sufixem `-preview.<height>`
- **Wersja obliczana** z historii Git, nie przechowywana w plikach projektu

### 2. Semantic Versioning wymuszony procesem

Developerzy muszą **świadomie** tworzyć tagi zgodnie z SemVer:
- `v1.0.0` → `v1.0.1` - **PATCH** (bug fix)
- `v1.0.1` → `v1.1.0` - **MINOR** (nowa funkcjonalność, backward compatible)
- `v1.1.0` → `v2.0.0` - **MAJOR** (breaking changes)

### 3. Modularyzacja konfiguracji build

Struktura `build/*.props`:
```
Directory.Build.props (orchestrator)
├── build/Build.Versioning.props    → MinVer configuration
├── build/Build.CodeQuality.props   → Analyzers, deterministic builds
├── build/Build.SourceLink.props    → SourceLink dla debugowania
└── build/Build.NuGet.props         → Package metadata
```

Każdy plik ma **jedną odpowiedzialność** (Single Responsibility Principle).

### 4. AssemblyVersion strategia - KRYTYCZNE UZASADNIENIE

```xml
<!-- build/Build.Versioning.props -->
<AssemblyVersion>$(MinVerMajor).0.0.0</AssemblyVersion>
<FileVersion>$(MinVerMajor).$(MinVerMinor).$(MinVerPatch).0</FileVersion>
```

#### Dlaczego AssemblyVersion używa tylko MAJOR?

**WSZYSTKIE wersje 1.x.x mają AssemblyVersion = 1.0.0.0**

To jest **świadoma decyzja architektoniczna**, nie bug. Przykłady:

| Git Tag | PackageVersion | AssemblyVersion | FileVersion |
|---------|---------------|-----------------|-------------|
| v1.0.0  | 1.0.0         | **1.0.0.0**     | 1.0.0.0     |
| v1.2.6  | 1.2.6         | **1.0.0.0**     | 1.2.6.0     |
| v1.5.8  | 1.5.8         | **1.0.0.0**     | 1.5.8.0     |
| v2.0.0  | 2.0.0         | **2.0.0.0**     | 2.0.0.0     |

**Powody:**

1. **Binding Redirect w .NET Framework**
   - .NET Framework używa `AssemblyVersion` do resolvowania zależności
   - Zmiana `AssemblyVersion` wymusza recompilację wszystkich aplikacji zależnych
   - `AssemblyVersion = 1.0.0.0` dla wszystkich `1.x.x` = wszystkie są binary compatible

2. **Semantic Versioning zgodnie z specyfikacją**
   - SemVer gwarantuje: MINOR i PATCH są backward compatible
   - Tylko MAJOR może łamać kompatybilność binarną
   - Zmiana `AssemblyVersion` tylko dla MAJOR = zgodne z SemVer

3. **Unikanie Binding Redirect Hell**
   ```xml
   <!-- PROBLEM gdy AssemblyVersion = FileVersion -->
   <dependentAssembly>
     <assemblyIdentity name="Voyager.Common.Results" />
     <bindingRedirect oldVersion="1.0.0.0-1.2.5.0" newVersion="1.2.6.0" />
     <bindingRedirect oldVersion="1.2.6.0-1.3.0.0" newVersion="1.3.1.0" />
     <!-- ... dziesiątki redirectów dla każdej MINOR/PATCH wersji ... -->
   </dependentAssembly>
   ```
   
   ```xml
   <!-- ROZWIĄZANIE gdy AssemblyVersion = Major only -->
   <!-- Brak binding redirectów! Wszystkie 1.x.x używają 1.0.0.0 -->
   ```

4. **Best practice dla bibliotek .NET**
   - Microsoft używa tego podejścia w swoich bibliotekach
   - Newtonsoft.Json, Serilog, AutoMapper - wszyscy stosują Major-only
   - Industry standard dla nugetowych pakietów

**Gdzie znajdziesz pełną wersję?**

- ✅ **PackageVersion** (NuGet) - 1.2.6
- ✅ **FileVersion** - 1.2.6.0 (widoczne w właściwościach DLL)
- ✅ **InformationalVersion** - 1.2.6 (Assembly attribute)
- ⚠️ **AssemblyVersion** - 1.0.0.0 (dla binary compatibility)

**Konsekwencje:**

```csharp
// Kod użytkownika biblioteki
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

// Referencja do Voyager.Common.Results 1.2.6
// CLR widzi: AssemblyVersion = 1.0.0.0

// Aktualizacja do Voyager.Common.Results 1.5.8
// CLR nadal widzi: AssemblyVersion = 1.0.0.0
// ✅ Brak potrzeby recompilacji!
// ✅ Brak binding redirectów!

// Aktualizacja do Voyager.Common.Results 2.0.0
// CLR widzi: AssemblyVersion = 2.0.0.0
// ⚠️ Breaking change - wymaga recompilacji
// ⚠️ Może wymagać binding redirect jeśli różne dependency używają 1.x i 2.x
```

**Alternatywa którą odrzuciliśmy:**

```xml
<!-- ❌ ZŁE: AssemblyVersion = FileVersion -->
<AssemblyVersion>$(MinVerMajor).$(MinVerMinor).$(MinVerPatch).0</AssemblyVersion>
```

**Dlaczego to złe?**
- Każda MINOR/PATCH wymaga binding redirectów
- Aplikacje muszą być rekompilowane przy każdej aktualizacji biblioteki
- Większość developerów nie rozumie różnicy i trafia na runtime errors
- Łamie zasadę "backward compatible updates should be seamless"

### 5. Refaktoryzacja CI/CD workflow

**Przed:**
```yaml
jobs:
  newversion:  # ❌ Automatyczny bump, commit do repo
  build:
  deploy:
```

**Po:**
```yaml
jobs:
  build:   # ✅ MinVer oblicza z Git
  deploy:  # ✅ Tylko dla main/master
  release: # ✅ Tworzy GitHub Release dla tagów
```

**Kluczowe zmiany:**
- Usunięto job `newversion` - niepotrzebny
- `fetch-depth: 0` dla pełnej historii Git (required by MinVer)
- Deploy tylko dla push do `main`/`master`, nie dla PR
- Nowy job `release` tworzy GitHub Release z pakietami dla tagów `v*`

## Dlaczego ta opcja

### Korzyści techniczne

1. **Single source of truth**
   - Git tag = jedyne źródło prawdy o wersji
   - Pakiet NuGet 1.2.6 = commit z tagiem v1.2.6
   - Deterministyczne buildy - ten sam kod = ta sama wersja

2. **Czystsza historia Git**
   - Brak automatycznych commitów wersji
   - Każdy commit ma wartość biznesową
   - Łatwiejsze code review i git blame

3. **Wymuszenie świadomego release**
   - Developer musi zdecydować: MAJOR, MINOR czy PATCH?
   - Tag = akt świadomej decyzji o publikacji
   - Systemowe tworzenie tagów w repo (cel BZPB-386)

4. **Binding Redirect Hell - rozwiązany**
   - AssemblyVersion = Major only
   - Wszystkie 1.x.x są binary compatible
   - Zero binding redirectów dla MINOR/PATCH updates
   - Aplikacje nie muszą być rekompilowane przy aktualizacji biblioteki

5. **Preview builds automatyczne**
   - Każdy commit między tagami = preview
   - Format: `1.2.7-preview.5+abc1234`
   - Łatwe testowanie przed oficjalnym release

6. **GitHub Release integration**
   - Tag automatycznie tworzy GitHub Release
   - Release zawiera pakiety NuGet
   - Auto-generated release notes z commitów

### Korzyści procesowe

1. **Podniesienie poziomu technicznego** (cel BZPB-386)
   - Zespół uczy się Git tagging best practices
   - Zrozumienie Semantic Versioning
   - Świadomość binary compatibility i AssemblyVersion

2. **Reużywalność rozwiązania**
   - `build/*.props` może być kopiowany do innych projektów
   - Standardowy proces dla całej organizacji
   - Dokumentacja i wytyczne dla nowych projektów

3. **Eliminacja błędów**
   - Nie można zapomnieć o bumping wersji (MinVer to robi)
   - Nie można pomylić się w numerze wersji (SemVer wymuszony tagiem)
   - Nie można deployować bez tagu (proces wymusza)

## Alternatywy które odrzuciliśmy

### Alternatywa 1: Pozostać przy automatycznym bumping w CI

**Dlaczego odrzucona:**
- Nie rozwiązuje problemu zanieczyszczonej historii Git
- Nie wymusza świadomego tagowania
- Nie spełnia celu biznesowego BZPB-386
- Nadal problemy z śledzeniem release → commit

### Alternatywa 2: Nerdbank.GitVersioning

**Dlaczego odrzucona:**
- Bardziej skomplikowana konfiguracja niż MinVer
- Wymaga pliku `version.json` w repo (kolejny plik do zarządzania)
- MinVer jest prostszy i "convention over configuration"
- MinVer lepiej pasuje do naszej filozofii "Git as source of truth"

### Alternatywa 3: Ręczne zarządzanie wersją w .csproj (status quo)

**Dlaczego odrzucona:**
- Podatne na błędy ludzkie (zapomnienie o bump)
- Wersja może nie odpowiadać stanowi kodu
- Nie wymusza tagowania
- Nie spełnia celu biznesowego

### Alternatywa 4: AssemblyVersion = FileVersion (pełna wersja)

**Dlaczego odrzucona - KRYTYCZNE:**
```xml
<!-- ❌ Ta konfiguracja jest często spotykana, ale BŁĘDNA dla bibliotek -->
<AssemblyVersion>$(MinVerMajor).$(MinVerMinor).$(MinVerPatch).0</AssemblyVersion>
```

**Konsekwencje tej konfiguracji:**

1. **Binding Redirect Nightmare**
   ```xml
   <!-- Aplikacja musi mieć dziesiątki redirectów -->
   <dependentAssembly>
     <assemblyIdentity name="Voyager.Common.Results" />
     <bindingRedirect oldVersion="1.0.0.0-1.5.8.0" newVersion="1.5.8.0" />
   </dependentAssembly>
   ```

2. **Wymuszona recompilacja**
   - Update z 1.2.6 → 1.2.7 (PATCH, bug fix)
   - AssemblyVersion zmienia się: 1.2.6.0 → 1.2.7.0
   - **Wszystkie aplikacje MUSZĄ być rekompilowane**
   - Łamie obietnicę "backward compatible"

3. **Diamond dependency problem**
   ```
   YourApp
   ├── LibraryA → Voyager.Common.Results 1.2.6
   └── LibraryB → Voyager.Common.Results 1.3.0
   
   Z AssemblyVersion = Full:
   ❌ Runtime error! CLR nie wie, którą wersję załadować
   
   Z AssemblyVersion = Major only:
   ✅ Obie biblioteki używają 1.0.0.0 - działa!
   ```

4. **Łamie SemVer contract**
   - SemVer mówi: MINOR i PATCH są backward compatible
   - Zmiana AssemblyVersion = breaking change w .NET
   - **Sprzeczność między obietnicą SemVer a rzeczywistością .NET**

**Dlaczego niektórzy to robią?**
- Brak zrozumienia różnicy między PackageVersion a AssemblyVersion
- Copy-paste z przykładów dla aplikacji (nie bibliotek)
- "Widzę pełną wersję w Properties → lepsza diagnostyka" (mamy FileVersion do tego!)

**Nasz wybór:**
```xml
<!-- ✅ POPRAWNIE: Major-only dla bibliotek .NET -->
<AssemblyVersion>$(MinVerMajor).0.0.0</AssemblyVersion>
<FileVersion>$(MinVerMajor).$(MinVerMinor).$(MinVerPatch).0</FileVersion>
```

## Co to oznacza dla zespołu

### Dla developerów

**Workflow release:**
```bash
# 1. Merge feature do main
git checkout main
git merge feature/xyz
git push

# 2. Zdecyduj o typie wersji
# Bug fix? → PATCH (1.2.6 → 1.2.7)
# Nowa feature? → MINOR (1.2.7 → 1.3.0)
# Breaking change? → MAJOR (1.3.0 → 2.0.0)

# 3. Utwórz tag
git tag v1.2.7
git push origin v1.2.7

# 4. CI automatycznie:
#    - Buduje z wersją 1.2.7
#    - Publikuje na NuGet
#    - Tworzy GitHub Release
```

**Preview builds:**
```bash
# Każdy push do main bez tagu = preview
git push origin main
# CI utworzy: 1.2.8-preview.3+sha
```

**Diagnostyka wersji:**
```powershell
# Sprawdź wersję przed tagowaniem
dotnet build -c Release
# MinVer wypisze obliczoną wersję

# Sprawdź wersję w DLL
[Reflection.AssemblyName]::GetAssemblyName("Voyager.Common.Results.dll")
# AssemblyVersion: 1.0.0.0 (dla wszystkich 1.x.x)
# FileVersion: 1.2.6.0 (pełna wersja)
```

### Dla QA/Release Managerów

- ✅ Każdy release ma jednoznaczny tag w Git
- ✅ GitHub Release zawiera artefakty
- ✅ Możliwość testowania preview builds przed release
- ✅ Rollback = deploy starszego tagu

### Dla użytkowników biblioteki

- ✅ Aktualizacje MINOR/PATCH nie wymagają rekompilacji
- ✅ Brak binding redirect problems
- ✅ Przejrzystość: wersja pakietu = tag w Git
- ⚠️ Update MAJOR (1.x → 2.x) może wymagać binding redirects

### Czego się nauczyć

1. **Git tagging** - kiedy i jak tagować
2. **Semantic Versioning** - różnica między MAJOR/MINOR/PATCH
3. **AssemblyVersion vs FileVersion** - po co dwa numery wersji?
4. **Binary compatibility** - dlaczego AssemblyVersion = Major only
5. **MinVer** - jak działa, jak debugować

### Dokumentacja

Zespół musi przeczytać:
- `docs/QUICK-START-VERSIONING.md` - szybki start
- `requirements/VERSIONING-GUIDE.md` - szczegółowy przewodnik
- `BUILD.md` (zaktualizowany) - proces budowania
- `GITHUB_ACTIONS_SETUP.md` (zaktualizowany) - CI/CD

## Kiedy sprawdzimy czy to działa

**Milestone 1 (M+1 miesiąc): 2025-12-18**
- ✅ Czy zespół rozumie proces tagowania?
- ✅ Czy tagi są tworzone systematycznie?
- ✅ Czy problemy z binding redirects zniknęły?

**Milestone 2 (M+3 miesiące): 2026-02-18**
- ✅ Czy proces został przyjęty w innych projektach?
- ✅ Czy dokumentacja jest wystarczająca?
- ✅ Czy redukcja błędów wersjonowania jest mierzalna?

**Metryki sukcesu:**
- Zero automatycznych commitów wersji w historii Git
- 100% releases ma odpowiadający Git tag
- Zero zgłoszeń "DLL hell" / binding redirect issues od użytkowników
- Minimum 3 inne projekty Voyager używają MinVer

**Review:**
Po 3 miesiącach zespół oceni, czy proces wymaga adjustacji. Potencjalne problemy do monitorowania:
- Czy developerzy pamiętają o tagowaniu?
- Czy SemVer jest prawidłowo stosowany?
- Czy AssemblyVersion = Major only powoduje problemy?

---

**Powiązane dokumenty:**
- [Szablon ADR](../../Szablon_ADR__Architecture_Decision_Record_.md)
- [MinVer GitHub](https://github.com/adamralph/minver)
- [.NET Assembly Versioning Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning)
- [Semantic Versioning 2.0.0](https://semver.org/)

**Wskazówka dla AI:** Ten ADR dokumentuje przejście na Git-based versioning z MinVer, z kluczowym założeniem że AssemblyVersion używa tylko MAJOR version (1.0.0.0 dla wszystkich 1.x.x) aby uniknąć binding redirect hell w .NET Framework. To jest świadoma decyzja architektoniczna zgodna z best practices Microsoft dla bibliotek NuGet.