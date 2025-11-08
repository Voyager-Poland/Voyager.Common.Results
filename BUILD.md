# 📦 Budowanie i Publikacja Pakietu NuGet

## Wymagania

- .NET SDK 8.0 lub nowszy
- .NET Framework 4.8 Developer Pack
- Dostęp do GitHub Packages (dla publikacji)
- Opcjonalnie: Konto NuGet.org (dla publikacji publicznej)

## 🤖 Automatyczna Publikacja (Zalecana)

Projekt używa **GitHub Actions** do automatycznego budowania i publikacji. Każdy push do gałęzi `main` lub `master`:

1. ✅ **Automatycznie zwiększa wersję build** (np. 1.0.0 → 1.0.1)
2. ✅ **Buduje projekt** dla .NET 8.0 i .NET Framework 4.8
3. ✅ **Uruchamia testy** z pokryciem kodu
4. ✅ **Tworzy pakiet NuGet**
5. ✅ **Publikuje na GitHub Packages**
6. ✅ **Publikuje na NuGet.org** (jeśli skonfigurowane)

### Jak opublikować nową wersję?

Po prostu push do `main`:

```bash
git add .
git commit -m "Add Result.Combine method"
git push origin main
```

GitHub Actions zrobi resztę! 🚀

### Ręczne zwiększanie Major/Minor

Jeśli chcesz zmienić wersję Major lub Minor (nie tylko build):

1. **Edytuj `src/Voyager.Common.Results/Voyager.Common.Results.csproj`:**

```xml
<!-- Zmiana Minor version -->
<Version>1.1.0</Version>

<!-- Lub Major version (breaking changes) -->
<Version>2.0.0</Version>
```

2. **Zaktualizuj `PackageReleaseNotes`:**

```xml
<PackageReleaseNotes>
  v1.1.0: Added async support for Result operations
</PackageReleaseNotes>
```

3. **Zaktualizuj `CHANGELOG.md`:**

```markdown
## [1.1.0] - 2025-01-15

### Added
- Async extension methods for Result<T>
- MapAsync, BindAsync, TapAsync operations

### Changed
- Improved error messages
```

4. **Commit i push:**

```bash
git add .
git commit -m "Release v1.1.0: Add async support"
git push origin main
```

**Uwaga**: Build number będzie nadal automatycznie zwiększony przez workflow (np. 1.1.0 → 1.1.1).

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

## 🔄 Wersjonowanie (Semantic Versioning)

Projekt używa [Semantic Versioning](https://semver.org/):

- **MAJOR.MINOR.BUILD** (np. `1.2.3`)
- **MAJOR** (1.x.x) - Breaking changes (niezgodne wstecz)
- **MINOR** (x.1.x) - Nowe funkcjonalności (backward compatible)
- **BUILD** (x.x.1) - Bug fixes i małe zmiany (automatycznie zwiększane)

### Przykłady zmian wersji

| Zmiana | Poprzednia | Nowa | Typ |
|--------|-----------|------|-----|
| Fix błędu | 1.0.0 | 1.0.1 | AUTO (GitHub Actions) |
| Nowa metoda (compatible) | 1.0.5 | 1.1.0 | MANUAL (edytuj .csproj) |
| Zmiana API (breaking) | 1.5.3 | 2.0.0 | MANUAL (edytuj .csproj) |

## 📋 Checklist przed ręczną publikacją

Jeśli publikujesz ręcznie (bez GitHub Actions):

- [ ] Zwiększ wersję w `.csproj`
- [ ] Zaktualizuj `PackageReleaseNotes`
- [ ] Zaktualizuj `CHANGELOG.md`
- [ ] Uruchom: `dotnet test` (wszystkie testy przechodzą)
- [ ] Uruchom: `dotnet build -c Release` (bez błędów)
- [ ] Uruchom: `dotnet pack -c Release` (pakiet utworzony)
- [ ] Przetestuj pakiet lokalnie (oba frameworki)
- [ ] Commituj zmiany: `git commit -am "Release v1.x.x"`
- [ ] Publikuj pakiet
- [ ] Push do Git: `git push origin main`

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
- Workflow automatycznie zwiększa build number, więc to nie powinno się zdarzyć
- Jeśli publikujesz ręcznie, zwiększ wersję ręcznie

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
  newversion:    # Automatycznie zwiększa wersję build
  build:         # Buduje, testuje, pakuje (wymaga newversion)
  deploy:        # Publikuje pakiety (wymaga build)
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
