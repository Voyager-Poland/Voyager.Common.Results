# Szybki Start - Pierwsze Wersjonowanie

## 🎯 Problem

Budujesz projekt, ale DLL ma wersję `0.0.0.0` zamiast `1.2.6`.

## ✅ Rozwiązanie w 3 krokach

### 1️⃣ Utworzenie tagu Git

```powershell
# Sprawdź aktualny branch (powinno być main lub master)
git branch

# Commituj wszystkie zmiany
git add .
git commit -m "Prepare release v1.2.6"

# Utwórz tag
git tag v1.2.6

# Sprawdź, czy tag został utworzony
git tag
```

### 2️⃣ Rebuild projektu

```powershell
# Wyczyść stare pliki
dotnet clean -c Release

# Zbuduj ponownie
dotnet build -c Release
```

### 3️⃣ Weryfikacja

```powershell
# Sprawdź wersję w DLL
[System.Reflection.Assembly]::LoadFrom("$PWD\src\Voyager.Common.Results\bin\Release\net8.0\Voyager.Common.Results.dll").GetName() | Format-List Name, Version

# Powinno pokazać:
# Name    : Voyager.Common.Results
# Version : 1.0.0.0
```

**✅ Sukces!** Wersja assembly to teraz `1.0.0.0` (Major version z tagu).

---

## 📦 Publikacja pakietu (opcjonalnie)

Jeśli chcesz opublikować na GitHub Packages / NuGet.org:

```powershell
# Push tagu do GitHub
git push origin v1.2.6

# GitHub Actions automatycznie:
# ✅ Zbuduje pakiet z wersją 1.2.6
# ✅ Uruchomi testy
# ✅ Opublikuje Voyager.Common.Results.1.2.6.nupkg
# ✅ Utworzy GitHub Release
```

Ślledź postęp: https://github.com/Voyager-Poland/Voyager.Common.Results/actions

---

## 🔄 Workflow dla następnych wersji

### Patch release (bug fix): 1.2.6 → 1.2.7

```powershell
git tag v1.2.7
git push origin v1.2.7
```

### Minor release (nowa funkcjonalność): 1.2.7 → 1.3.0

```powershell
git tag v1.3.0
git push origin v1.3.0
```

### Major release (breaking changes): 1.3.0 → 2.0.0

```powershell
git tag v2.0.0
git push origin v2.0.0
```

---

## ❓ FAQ

### Dlaczego `AssemblyVersion` to `1.0.0.0` zamiast `1.2.6.0`?

To jest **best practice** dla bibliotek .NET:
- `AssemblyVersion` używa tylko **MAJOR** wersji (1.0.0.0)
- Zapobiega problemom z binding redirects
- Pozwala na kompatybilność dla wszystkich wersji `1.x.x`

Pełna wersja (`1.2.6`) jest w:
- ✅ `FileVersion` - 1.2.6.0
- ✅ `InformationalVersion` - 1.2.6
- ✅ `PackageVersion` - 1.2.6 (nazwa pakietu NuGet)

### Czy muszę pushować tagi?

**Lokalnie:** Nie - tag działa lokalnie bez pusha.

**CI/CD:** Tak - GitHub Actions potrzebuje tagu w remote repo.

### Co jeśli omyłkowo utworzę zły tag?

```powershell
# Usuń tag lokalnie
git tag -d v1.2.6

# Usuń tag z remote (jeśli został już zpushowany)
git push origin :refs/tags/v1.2.6

# Utwórz poprawny tag
git tag v1.2.7
```

### Czy mogę zobaczyć wersję przed buildem?

```powershell
# Sprawdź, jaką wersję MinVer obliczy
dotnet build -c Release /p:MinVerVerbosity=detailed 2>&1 | Select-String "Calculated version"

# Wynik:
# MinVer: Calculated version 1.2.6
```

---

## 📚 Więcej informacji

- [docs/MINVER-VISUALIZATION.md](MINVER-VISUALIZATION.md) - Wizualizacje MinVer
- [BUILD.md](../BUILD.md) - Pełna dokumentacja budowania
- [MinVer GitHub](https://github.com/adamralph/minver) - Dokumentacja MinVer
