# 📦 Budowanie i Publikacja Pakietu NuGet

## Wymagania

- .NET SDK 8.0 lub nowszy
- .NET Framework 4.8 Developer Pack
- Dostęp do GitHub Packages (dla publikacji)
- Opcjonalnie: Konto NuGet.org (dla publikacji publicznej)

## ✨ Deterministic Builds

Projekt używa **deterministic compilation** aby zapewnić, że identyczny kod źródłowy zawsze produkuje identyczne binaria. To eliminuje ostrzeżenia o niezdeterministycznych bibliotekach DLL w pakietach NuGet.

**Konfiguracja:** Automatycznie włączone w `build/Build.CodeQuality.props`

```xml
<Deterministic>true</Deterministic>
<ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
```

**Więcej informacji:** Zobacz [docs/DETERMINISTIC-BUILDS.md](docs/DETERMINISTIC-BUILDS.md)

## 🤖 Automatyczna Publikacja (Zalecana)

Projekt używa **GitHub Actions** z **MinVer** do automatycznego wersjonowania. MinVer oblicza wersję na podstawie Git tagów.

### Jak działa MinVer?

MinVer automatycznie:
- 📌 **Odczytuje Git tagi** w formacie `v1.2.3`
- 🔢 **Oblicza wersję** na podstawie najnowszego taga
- 🏷️ **Dodaje sufiks `-preview.X`** dla commitów między tagami
- 🎯 **Używa `MinVerMinimumMajorMinor`** (0.1) jeśli brak tagów

### Jak opublikować nową wersję?

**Opcja 1: Wersja preview (automatyczna)**

Po prostu push do `main`:

```bash
git add .
git commit -m "Add Result.Combine method"
git push origin main
```

MinVer utworzy wersję preview, np. `0.1.0-preview.5+abc1234`

**Opcja 2: Wersja release (z tagiem)**

1. **Utwórz i push tag:**

```bash
git tag v1.2.3
git push origin v1.2.3
```

2. **GitHub Actions automatycznie:**
   - ✅ Buduje projekt dla .NET 8.0 i .NET Framework 4.8
   - ✅ Uruchamia testy z pokryciem kodu
   - ✅ Tworzy pakiet NuGet z wersją `1.2.3`
   - ✅ Publikuje na GitHub Packages
   - ✅ Publikuje na NuGet.org
   - ✅ Tworzy GitHub Release z pakietem

### Konwencje tagowania

```bash
# Patch version (bug fixes)
git tag v1.0.1

# Minor version (new features, backward compatible)
git tag v1.1.0

# Major version (breaking changes)
git tag v2.0.0

# Preview/beta releases
git tag v1.2.0-preview.1
git tag v1.2.0-beta.2
```

## 🔨 Budowanie Pakietu Lokalnie

### Podstawowe budowanie

```bash
# W katalogu głównym projektu
dotnet pack src/Voyager.Common.Results/Voyager.Common.Results.csproj -c Release
```

Pakiet zostanie utworzony w:
```
src/Voyager.Common.Results/bin/Release/Voyager.Common.Results.1.0.0.nupkg
src/Voyager.Common.Results/bin/Release/Voyager.Common.Results.1.0.0.snupkg (symbole)
```

### Budowanie z konkretną wersją

```bash
dotnet pack src/Voyager.Common.Results/Voyager.Common.Results.csproj -c Release /p:Version=1.2.3
```

### Budowanie multi-target (weryfikacja)

```bash
# Sprawdź czy obie wersje budują się poprawnie
dotnet build src/Voyager.Common.Results/Voyager.Common.Results.csproj -c Release -f net8.0
dotnet build src/Voyager.Common.Results/Voyager.Common.Results.csproj -c Release -f net48
```

## 🧪 Testowanie Pakietu Lokalnie

### 1. Utwórz lokalny folder dla pakietów

```bash
mkdir C:\LocalNuGet
```

### 2. Skopiuj pakiet do lokalnego folderu

```bash
copy src\Voyager.Common.Results\bin\Release\*.nupkg C:\LocalNuGet\
```

### 3. Dodaj lokalne źródło NuGet

```bash
dotnet nuget add source C:\LocalNuGet --name LocalPackages
```

### 4. Testuj w projekcie .NET 8

```bash
dotnet new console -n TestNet8 -f net8.0
cd TestNet8
dotnet add package Voyager.Common.Results --source LocalPackages
```

Dodaj kod testowy:

```csharp
using Voyager.Common.Results;

var result = Result<int>.Success(42);
Console.WriteLine(result.Match(
    onSuccess: x => $"Success: {x}",
    onFailure: e => $"Error: {e.Message}"
));
```

```bash
dotnet run
```

### 5. Testuj w projekcie .NET Framework 4.8

```bash
dotnet new console -n TestNet48 -f net48
cd TestNet48
dotnet add package Voyager.Common.Results --source LocalPackages
```

Dodaj ten sam kod testowy i uruchom.

### 6. Usuń lokalne źródło (po testach)

```bash
dotnet nuget remove source LocalPackages
```

## 🚀 Ręczna Publikacja (Advanced)

### GitHub Packages

```bash
# Dodaj źródło GitHub Packages (jeśli nie masz)
dotnet nuget add source "https://nuget.pkg.github.com/Voyager-Poland/index.json" \
  -n Voyager-Poland \
  -u YOUR_GITHUB_USERNAME \
  -p YOUR_GITHUB_PAT \
  --store-password-in-clear-text

# Publikuj pakiet
dotnet nuget push src/Voyager.Common.Results/bin/Release/*.nupkg \
  -s Voyager-Poland \
  --skip-duplicate
```

### NuGet.org

1. **Uzyskaj API Key z NuGet.org:**
   - Zaloguj się na https://www.nuget.org
   - **Account Settings** → **API Keys** → **Create**
   - Scope: `Push`, Package: `Voyager.Common.Results`

2. **Publikuj:**

```bash
dotnet nuget push src/Voyager.Common.Results/bin/Release/*.nupkg \
  --api-key YOUR_NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

Symbole (`.snupkg`) są automatycznie publikowane.

## 📊 Weryfikacja Pakietu

### Sprawdź zawartość pakietu

**Opcja 1: NuGet Package Explorer (GUI)**
1. Pobierz [NuGet Package Explorer](https://github.com/NuGetPackageExplorer/NuGetPackageExplorer)
2. Otwórz plik `.nupkg`
3. Sprawdź:
   - ✅ Metadata (Version, Authors, Description)
   - ✅ `lib/net8.0/Voyager.Common.Results.dll`
   - ✅ `lib/net48/Voyager.Common.Results.dll`
   - ✅ README.md w pakiecie
   - ✅ XML dokumentacja

**Opcja 2: Linia komend**

```bash
# Rozpakuj pakiet
mkdir temp
cd temp
unzip ../Voyager.Common.Results.1.0.0.nupkg

# Sprawdź strukturę
ls -R
```

### Sprawdź po publikacji

#### GitHub Packages
https://github.com/orgs/Voyager-Poland/packages

#### NuGet.org
https://www.nuget.org/packages/Voyager.Common.Results

**Instalacja testowa:**

```bash
dotnet new console -n FinalTest
cd FinalTest
dotnet add package Voyager.Common.Results
dotnet run
```

## 🔄 Wersjonowanie (Semantic Versioning + MinVer)

Projekt używa [MinVer](https://github.com/adamralph/minver) do automatycznego wersjonowania na podstawie Git tagów zgodnie z [Semantic Versioning](https://semver.org/):

### ⚠️ WAŻNE: MinVer wymaga tagów Git!

**Jeśli nie masz tagów Git, wersja będzie `0.0.0.0` zamiast oczekiwanej!**

📖 **Szybki start:** [docs/QUICK-START-VERSIONING.md](docs/QUICK-START-VERSIONING.md) - Jak utworzyć pierwszy tag w 3 krokach  
📖 **Szczegóły:** [docs/QUICK-START-VERSIONING.md](docs/QUICK-START-VERSIONING.md) - Przewodnik wersjonowania

### Jak MinVer oblicza wersję?

1. **Z tagiem:** `v1.2.3` → pakiet `1.2.3`
2. **Bez tagu (commits po tagu):** `v1.2.3` + 5 commitów → `1.2.4-preview.5+sha`
3. **Brak tagów:** używa `MinVerMinimumMajorMinor` (0.1) → `0.1.0-preview.X`

### Semantic Versioning

- **MAJOR.MINOR.PATCH** (np. `1.2.3`)
- **MAJOR** (1.x.x) - Breaking changes (niezgodne wstecz)
- **MINOR** (x.1.x) - Nowe funkcjonalności (backward compatible)
- **PATCH** (x.x.1) - Bug fixes

### Przykłady wersjonowania

| Sytuacja | Git Tag | Wersja Pakietu | Typ |
|----------|---------|----------------|-----|
| Release | `v1.0.0` | `1.0.0` | Release |
| Patch fix | `v1.0.1` | `1.0.1` | Release |
| New feature | `v1.1.0` | `1.1.0` | Release |
| Breaking change | `v2.0.0` | `2.0.0` | Release |
| Preview | `v1.2.0-preview.1` | `1.2.0-preview.1` | Preview |
| Bez tagu (5 commits) | - | `0.1.0-preview.5+abc1234` | Auto Preview |
| Po tagu (3 commits) | `v1.0.0` | `1.0.1-preview.3+def5678` | Auto Preview |

### Konfiguracja MinVer

W `build/Build.Versioning.props`:

```xml
<MinVerTagPrefix>v</MinVerTagPrefix>              <!-- Tagi: v1.0.0 -->
<MinVerMinimumMajorMinor>0.1</MinVerMinimumMajorMinor>  <!-- Default bez tagów -->
<MinVerDefaultPreReleaseIdentifiers>preview</MinVerDefaultPreReleaseIdentifiers>
```

### Workflow tagowania

```bash
# 1. Zaktualizuj CHANGELOG.md
# 2. Commit zmian
git add .
git commit -m "Prepare release v1.2.0"

# 3. Utwórz tag
git tag v1.2.0

# 4. Push (tag triggers release workflow)
git push origin main
git push origin v1.2.0

# 5. GitHub Actions automatycznie:
#    - Buduje z wersją 1.2.0
#    - Publikuje pakiet
#    - Tworzy GitHub Release
```

## 📋 Checklist przed publikacją release

Jeśli publikujesz wersję release (z tagiem):

- [ ] Zaktualizuj `CHANGELOG.md` z listą zmian
- [ ] Zaktualizuj `PackageReleaseNotes` w `.csproj` (opcjonalnie)
- [ ] Uruchom: `dotnet test` (wszystkie testy przechodzą)
- [ ] Uruchom: `dotnet build -c Release` (bez błędów)
- [ ] Commituj zmiany: `git commit -am "Prepare release v1.x.x"`
- [ ] Utwórz tag: `git tag v1.x.x`
- [ ] Push: `git push origin main && git push origin v1.x.x`
- [ ] GitHub Actions automatycznie zbuduje i opublikuje pakiet

## 🔍 Sprawdzenie pokrycia kodu

```bash
# Uruchom testy z pokryciem
dotnet test --collect:"XPlat Code Coverage"

# Wygeneruj raport HTML (wymaga reportgenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool

reportgenerator \
  -reports:**/coverage.cobertura.xml \
  -targetdir:coverage-report \
  -reporttypes:Html

# Otwórz raport
start coverage-report/index.html
```

## ❓ Rozwiązywanie problemów

### Workflow nie uruchamia się

**Przyczyny:**
- Push do niewłaściwej gałęzi (musi być `main` lub `master`)
- Workflow wyłączony w Settings → Actions

**Rozwiązanie:**
```bash
git branch  # Sprawdź aktualną gałąź
git checkout main
git push origin main
```

### Błąd: "Package already exists with version X.Y.Z"

GitHub Packages i NuGet.org nie pozwalają nadpisać wersji.

**Rozwiązanie:** 
- Utwórz nowy tag z wyższą wersją: `git tag v1.0.1`
- MinVer automatycznie użyje nowej wersji
- Usuń błędny tag jeśli trzeba: `git tag -d v1.0.0 && git push origin :refs/tags/v1.0.0`

### Błąd: "401 Unauthorized" przy publikacji

**GitHub Packages:**
```bash
Error: Response status code does not indicate success: 401 (Unauthorized)
```

**Rozwiązanie:** Sprawdź GitHub Secrets:
- Settings → Secrets and variables → Actions
- Zweryfikuj `VOY_ACTIONLOGIN` i `VOY_ACTIONLOGINPASS`

**NuGet.org:**
```bash
Error: Response status code does not indicate success: 401 (Unauthorized)
```

**Rozwiązanie:** Sprawdź `VOY_AND_API_KEY` lub wyłącz publikację na NuGet.org w workflow.

### Testy nie przechodzą lokalnie na .NET Framework 4.8

**Przyczyna:** Brak .NET Framework 4.8 Developer Pack

**Rozwiązanie:**
```bash
# Pobierz i zainstaluj:
https://dotnet.microsoft.com/download/dotnet-framework/net48
```

### Build działa lokalnie, ale failuje na GitHub Actions

**Przyczyna:** Różnice między środowiskami (Windows vs Linux)

**Rozwiązanie:** 
- Workflow używa `ubuntu-latest` (Linux)
- Sprawdź logi Actions dla szczegółów
- Może wymagać zmiany na `windows-latest` jeśli są problemy z .NET Framework 4.8

## 📚 GitHub Actions - Szczegóły

### Struktura Workflow

```yaml
jobs:
  build:         # Buduje, testuje, pakuje (MinVer oblicza wersję z Git)
  deploy:        # Publikuje pakiety (wymaga build, tylko na push do main/master)
  release:       # Tworzy GitHub Release (wymaga build, tylko dla tagów v*)
```

### MinVer w GitHub Actions

Workflow **musi** mieć `fetch-depth: 0` aby MinVer miał dostęp do pełnej historii Git:

```yaml
- uses: actions/checkout@v4
  with:
    fetch-depth: 0  # CRITICAL: MinVer needs full Git history
```

### Używane Secrets

- `VOY_ACTIONLOGIN` - GitHub username dla packages
- `VOY_ACTIONLOGINPASS` - GitHub PAT dla packages
- `VOY_AND_API_KEY` - NuGet.org API key (opcjonalny)

### Artifacts

Build job tworzy artifact `CommonResults` z plikami `.nupkg`:
- Dostępny przez 1 dzień
- Używany przez deploy job
- Można pobrać z Actions UI dla weryfikacji

## 🔐 Bezpieczeństwo

### ✅ Dobre praktyki

- ✅ Secrets są zaszyfrowane przez GitHub
- ✅ API Keys mają minimalne uprawnienia (tylko Push)
- ✅ PAT mają ustawiony expiration date
- ✅ Secrets nie są logowane (automatycznie maskowane)

### ❌ Czego unikać

- ❌ Nie commituj API keys do repozytorium
- ❌ Nie używaj API key z pełnymi uprawnieniami
- ❌ Nie udostępniaj PAT publicznie
- ❌ Nie wyłączaj `--skip-duplicate` (może powodować błędy)

## 📚 Dodatkowe zasoby

- [GitHub Actions Documentation](https://docs.github.com/actions)
- [Dokumentacja NuGet](https://docs.microsoft.com/nuget/)
- [Semantic Versioning](https://semver.org/)
- [NuGet Package Explorer](https://github.com/NuGetPackageExplorer/NuGetPackageExplorer)
- [GitHub Packages dla NuGet](https://docs.github.com/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry)
