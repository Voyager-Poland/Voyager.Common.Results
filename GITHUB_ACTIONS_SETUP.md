# GitHub Actions Setup

## Konfiguracja Secrets - Zmienne Środowiskowe GitHub

Projekt używa zmiennych środowiskowych GitHub do automatycznej publikacji pakietów. Secrets są już skonfigurowane na poziomie organizacji/repozytorium.

### Wymagane Secrets

Workflow wykorzystuje następujące secrets (już skonfigurowane):

- **`VOY_ACTIONLOGIN`** - Login do GitHub Packages
- **`VOY_ACTIONLOGINPASS`** - Token/hasło do GitHub Packages  
- **`VOY_AND_API_KEY`** - API Key do NuGet.org (opcjonalny, jeśli publikujesz publicznie)

### Automatyczny Proces Publikacji

Workflow automatycznie:
1. **Zwiększa wersję build** przy każdym push do `main`
2. **Buduje projekt** (multi-target: .NET 8.0 i .NET Framework 4.8)
3. **Uruchamia testy**
4. **Tworzy pakiet NuGet**
5. **Publikuje** na GitHub Packages i NuGet.org

## Workflow - Automatyczne Wersjonowanie i Publikacja

### Jak to działa?

#### 1. Push do `main` uruchamia cały proces:

```bash
git add .
git commit -m "Add new feature"
git push origin main
```

#### 2. Automatyczne zwiększanie wersji

Workflow używa `vers-one/dotnet-project-version-updater` do automatycznego zwiększania numeru build w pliku `.csproj`:

```xml
<!-- Przed: -->
<Version>1.0.0</Version>

<!-- Po automatycznym bump: -->
<Version>1.0.1</Version>
```

Commit z nową wersją jest automatycznie push'owany przez GitHub Actions.

#### 3. Build, Test, Pack, Deploy

Po zwiększeniu wersji workflow:
- Przywraca zależności (w tym z prywatnego GitHub Packages)
- Buduje projekt w konfiguracji Release
- Uruchamia testy
- Pakuje do `.nupkg`
- Publikuje na GitHub Packages i NuGet.org

### Struktura Workflow

```yaml
jobs:
  newversion:    # Zwiększa wersję build automatycznie
  build:         # Buduje, testuje, pakuje
  deploy:        # Publikuje pakiety
```

## Publikacja Nowej Wersji

### Automatyczna (zalecana)

Po prostu push do `main`:

```bash
git add .
git commit -m "Implement Result.Combine method"
git push origin main
```

GitHub Actions:
- ✅ Automatycznie zwiększy wersję build
- ✅ Zbuduje i przetestuje
- ✅ Opublikuje nową wersję

### Ręczne zwiększanie wersji (Major/Minor)

Jeśli chcesz zmienić wersję Major lub Minor (nie tylko build):

1. Edytuj ręcznie `src/Voyager.Common.Results/Voyager.Common.Results.csproj`:

```xml
<!-- Zmiana Minor version -->
<Version>1.1.0</Version>

<!-- Lub Major version -->
<Version>2.0.0</Version>
```

2. Zaktualizuj `CHANGELOG.md`:

```markdown
## [2.0.0] - 2024-01-15

### Breaking Changes
- Renamed ResultExtensions to ResultCombineExtensions

### Added
- New async methods for Result combination
```

3. Commit i push:

```bash
git add .
git commit -m "Release v2.0.0: Breaking changes and new features"
git push origin main
```

**Uwaga**: Build number zostanie nadal automatycznie zwiększony (np. 2.0.0 → 2.0.1), ale rozpocznie się od nowej bazy.

## Weryfikacja Publikacji

### 1. Sprawdź GitHub Actions

1. Przejdź do: https://github.com/Voyager-Poland/Voyager.Common.Results/actions
2. Znajdź workflow ".NET push"
3. Sprawdź logi każdego job'a:
   - **newversion**: Czy wersja została zwiększona?
   - **build**: Czy testy przeszły?
   - **deploy**: Czy publikacja się powiodła?

### 2. Sprawdź GitHub Packages

https://github.com/orgs/Voyager-Poland/packages

### 3. Sprawdź NuGet.org (jeśli publikujesz publicznie)

https://www.nuget.org/packages/Voyager.Common.Results

### 4. Testowa instalacja

```bash
# Z NuGet.org
dotnet new console -n TestApp
cd TestApp
dotnet add package Voyager.Common.Results

# Z GitHub Packages (wymaga uwierzytelnienia)
dotnet nuget add source "https://nuget.pkg.github.com/Voyager-Poland/index.json" \
  -n Voyager-Poland -u YOUR_USERNAME -p YOUR_PAT --store-password-in-clear-text
dotnet add package Voyager.Common.Results
```

## Multi-Targeting (.NET 8.0 i .NET Framework 4.8)

Projekt jest skonfigurowany do budowania dla obu platform:

```xml
<TargetFrameworks>net8.0;net48</TargetFrameworks>
```

Jeden pakiet NuGet zawiera obie wersje:
- `lib/net8.0/Voyager.Common.Results.dll`
- `lib/net48/Voyager.Common.Results.dll`

NuGet automatycznie wybierze odpowiednią wersję dla projektu konsumenta.

## Troubleshooting

### Błąd: "401 Unauthorized" przy publikacji

**GitHub Packages:**
```
Error: Response status code does not indicate success: 401 (Unauthorized)
```

**Rozwiązanie**: Sprawdź czy secrets są poprawnie skonfigurowane:
- Settings → Secrets and variables → Actions
- Zweryfikuj `VOY_ACTIONLOGIN` i `VOY_ACTIONLOGINPASS`

**NuGet.org:**
```
Error: Response status code does not indicate success: 401 (Unauthorized)
```

**Rozwiązanie**: Sprawdź `VOY_AND_API_KEY` lub usuń krok publikacji na NuGet.org jeśli nie jest potrzebny.

### Błąd: "Package already exists with version X"

GitHub Packages/NuGet.org nie pozwalają nadpisać istniejącej wersji.

**Rozwiązanie**: 
- Workflow automatycznie zwiększa wersję build, więc to nie powinno się zdarzyć
- Jeśli występuje, upewnij się że workflow `newversion` zakończył się sukcesem

### Workflow nie uruchamia się

**Przyczyny**:
- Push był do innej gałęzi niż `main`
- Workflow jest wyłączony w Settings → Actions

**Rozwiązanie**:
```bash
# Sprawdź aktualną gałąź
git branch

# Push do main
git checkout main
git push origin main
```

### Testy nie przechodzą

```bash
# Uruchom testy lokalnie przed push
dotnet test

# Sprawdź logi w GitHub Actions
# Actions → Workflow run → build job → Test step
```

### Konflikt po automatycznym commit wersji

Może się zdarzyć jeśli push'ujesz jednocześnie z automatycznym commit'em wersji.

**Rozwiązanie**:
```bash
git pull --rebase origin main
git push origin main
```

## Konfiguracja dla Nowego Projektu

Jeśli chcesz użyć tego samego workflow dla innego projektu:

### 1. Skopiuj pliki workflow

```bash
.github/workflows/ci.yml           # Opcjonalny CI dla innych gałęzi
.github/workflows/publish-nuget.yml # Workflow publikacji (dostosuj ścieżki)
```

### 2. Dostosuj ścieżki w workflow

W pliku workflow zmień:

```yaml
# Przed:
file: "src/Voyager.Common.Results/Voyager.Common.Results.csproj"

# Po (dla Twojego projektu):
file: "src/YourProject/YourProject.csproj"
```

```yaml
# Przed:
path: ${{ github.workspace }}/src/Voyager.Common.Results/bin/Release/*.nupkg

# Po:
path: ${{ github.workspace }}/src/YourProject/bin/Release/*.nupkg
```

### 3. Upewnij się że secrets są dostępne

Jeśli projekty są w tej samej organizacji, secrets są współdzielone.

Dla nowego repozytorium poza organizacją:
1. Settings → Secrets and variables → Actions
2. Dodaj te same secrets: `VOY_ACTIONLOGIN`, `VOY_ACTIONLOGINPASS`, `VOY_AND_API_KEY`

## Bezpieczeństwo

### ✅ Best Practices

- **Secrets są zaszyfrowane** przez GitHub
- **Secrets nie są widoczne** w logach
- **Minimum uprawnień**: API Key tylko do push pakietów
- **Automatyczne wygasanie**: Ustaw expiration na GitHub PAT

### ❌ Czego unikać

- ❌ Nie commituj secrets do repozytorium
- ❌ Nie loguj wartości secrets (GitHub automatycznie je maskuje)
- ❌ Nie używaj API Key z pełnymi uprawnieniami
- ❌ Nie udostępniaj PAT publicznie

## Dodatkowe Informacje

### Permissions w Workflow

```yaml
permissions:
  contents: write    # Potrzebne do commit'owania wersji
  packages: write    # Potrzebne do publikacji na GitHub Packages
```

### Cache Dependencies (opcjonalnie)

Możesz przyspieszyć workflow dodając cache:

```yaml
- name: Cache NuGet packages
  uses: actions/cache@v3
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
    restore-keys: |
      ${{ runner.os }}-nuget-
```

### Powiadomienia o Publikacji

Możesz dodać powiadomienia (np. Slack, Teams, email) po udanej publikacji:

```yaml
- name: Notify on success
  if: success()
  run: echo "Package published successfully!"
  # Tutaj możesz dodać integrację z Slack/Teams
