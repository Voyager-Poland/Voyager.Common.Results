# MinVer - Wizualizacja Wersjonowania

## 📊 Jak MinVer oblicza wersję?

```
Git Repository                MinVer Process                  Output
═══════════════              ══════════════                 ════════

┌─────────────┐              ┌──────────────┐               ┌─────────────────┐
│ Commit #1   │              │ Szuka tagu   │               │ AssemblyVersion │
│ abc1234     │──────────────▶ z prefixem  │──────────────▶│ 0.0.0.0         │
│             │              │ "v"          │               │                 │
│ (no tag)    │              │              │               │ FileVersion     │
└─────────────┘              │ ❌ Not found │               │ 0.1.0.0         │
                             │              │               │                 │
                             │ Uses default │               │ PackageVersion  │
                             │ 0.0.0-preview│               │ 0.1.0-preview.X │
                             └──────────────┘               └─────────────────┘


┌─────────────┐              ┌──────────────┐               ┌─────────────────┐
│ Commit #10  │              │ Szuka tagu   │               │ AssemblyVersion │
│ def5678     │──────────────▶ z prefixem  │──────────────▶│ 1.0.0.0         │
│             │              │ "v"          │               │                 │
│ tag: v1.2.6 │              │              │               │ FileVersion     │
└─────────────┘              │ ✅ Found!    │               │ 1.2.6.0         │
                             │              │               │                 │
                             │ Uses v1.2.6  │               │ PackageVersion  │
                             │              │               │ 1.2.6           │
                             └──────────────┘               └─────────────────┘


┌─────────────┐              ┌──────────────┐               ┌─────────────────┐
│ Commit #13  │              │ Szuka tagu   │               │ AssemblyVersion │
│ ghi9012     │──────────────▶ z prefixem  │──────────────▶│ 1.0.0.0         │
│             │              │ "v"          │               │                 │
│ (3 commits  │              │              │               │ FileVersion     │
│  after tag) │              │ ✅ Found     │               │ 1.2.7.0         │
└─────────────┘              │ v1.2.6       │               │                 │
      ▲                      │              │               │ PackageVersion  │
      │                      │ + 3 commits  │               │ 1.2.7-preview.3 │
      │                      │ = bump patch │               └─────────────────┘
  3 commits                  └──────────────┘
  after v1.2.6
```

---

## 🎯 Scenariusze

### Scenariusz 1: Brak tagów (Twoja obecna sytuacja)

```
Repository State:
─────────────────
main: ───●───●───●───●───●  (21 commits, no tags)
         1   2   3  ...  21

MinVer Output:
──────────────
Searched: 21 commits
Found:    No tags with "v" prefix
Used:     Default 0.0.0-preview
Bumped:   0.1.0-preview.21 (minimum major minor)

Result:
───────
AssemblyVersion:       0.0.0.0
FileVersion:           0.1.0.0  
PackageVersion:        0.1.0-preview.21
InformationalVersion:  0.1.0-preview.21
```

### Scenariusz 2: Po utworzeniu tagu v1.2.6

```
Repository State:
─────────────────
main: ───●───●───●───●───●  
         1   2   3  ...  21
                           ↑
                         v1.2.6

MinVer Output:
──────────────
Searched: 21 commits
Found:    v1.2.6 at commit #21
Used:     1.2.6
Height:   0 (tag at HEAD)

Result:
───────
AssemblyVersion:       1.0.0.0     ← Major only!
FileVersion:           1.2.6.0     ← Full version
PackageVersion:        1.2.6       ← For NuGet
InformationalVersion:  1.2.6       ← Display version
```

### Scenariusz 3: 3 commity po tagu

```
Repository State:
─────────────────
main: ───●───●───●───●───●───●───●───●
         1   2   3  ...  21  22  23  24
                           ↑
                         v1.2.6

MinVer Output:
──────────────
Searched: 24 commits
Found:    v1.2.6 at commit #21
Height:   3 (3 commits after tag)
Used:     1.2.6 + auto-increment patch

Result:
───────
AssemblyVersion:       1.0.0.0
FileVersion:           1.2.7.0           ← Bumped patch!
PackageVersion:        1.2.7-preview.3   ← Preview + height
InformationalVersion:  1.2.7-preview.3+abc1234
```

---

## 🔢 Mapowanie wersji

### Git Tag → Assembly Versions

| Git Tag | MinVer Parse | AssemblyVersion | FileVersion | PackageVersion | InformationalVersion |
|---------|--------------|-----------------|-------------|----------------|----------------------|
| (none) | `0.0.0-preview` | `0.0.0.0` | `0.1.0.0` | `0.1.0-preview.21` | `0.1.0-preview.21+abc1234` |
| `v1.0.0` | `1.0.0` | `1.0.0.0` | `1.0.0.0` | `1.0.0` | `1.0.0` |
| `v1.2.6` | `1.2.6` | `1.0.0.0` ⚠️ | `1.2.6.0` | `1.2.6` | `1.2.6` |
| `v2.0.0` | `2.0.0` | `2.0.0.0` | `2.0.0.0` | `2.0.0` | `2.0.0` |
| `v1.2.6` + 3 commits | `1.2.6` + height | `1.0.0.0` | `1.2.7.0` | `1.2.7-preview.3` | `1.2.7-preview.3+def5678` |

⚠️ **Uwaga:** `AssemblyVersion` używa tylko MAJOR wersji (1.0.0.0) zgodnie z konfiguracją w `Build.Versioning.props`:

```xml
<AssemblyVersion>$(MinVerMajor).0.0.0</AssemblyVersion>
```

**Dlaczego?** Zapobiega problemom z binding redirect i zapewnia kompatybilność dla wszystkich wersji `1.x.x`.

---

## 📦 Wersjonowanie pakietu NuGet

### Struktura pakietu

```
Voyager.Common.Results.1.2.6.nupkg          ← PackageVersion (z MinVer)
│
├── lib/
│   ├── net8.0/
│   │   └── Voyager.Common.Results.dll      ← Nazwa BEZ wersji!
│   │       ├── AssemblyVersion: 1.0.0.0    ← W metadata
│   │       ├── FileVersion: 1.2.6.0        ← W metadata
│   │       └── InformationalVersion: 1.2.6 ← W metadata
│   │
│   └── net48/
│       └── Voyager.Common.Results.dll      ← Nazwa BEZ wersji!
│           ├── AssemblyVersion: 1.0.0.0
│           ├── FileVersion: 1.2.6.0
│           └── InformationalVersion: 1.2.6
│
└── Voyager.Common.Results.nuspec
    └── <version>1.2.6</version>            ← PackageVersion
```

### Dlaczego DLL nie ma wersji w nazwie?

**To jest standard .NET!**

```
✅ POPRAWNIE:  Voyager.Common.Results.dll
❌ ŹLE:        Voyager.Common.Results.1.2.6.dll
```

**Powody:**
1. 📦 **NuGet zarządza wersjami** - pakiet ma wersję (`1.2.6.nupkg`), nie DLL
2. 🔄 **Kompatybilność** - aplikacja może załadować różne wersje DLL
3. 🎯 **Binding Redirect** - .NET Framework przekierowuje na podstawie `AssemblyVersion`
4. 🏗️ **Build Output** - upraszcza ścieżki (`bin/Release/net8.0/`)

**Wersja jest w metadanych:**
```powershell
[System.Reflection.Assembly]::LoadFrom("path/to/Voyager.Common.Results.dll").GetName()
```

---

## 🔄 Workflow z MinVer

### Rozwój lokalny (bez tagów)

```
Developer Workflow                MinVer                    Version
═════════════════                ══════                    ═══════

git clone repo          ────▶    No tags found    ────▶    0.1.0-preview.X
git checkout -b feature
# ... coding ...
git commit -m "Add feature"      
dotnet build            ────▶    Still no tags    ────▶    0.1.0-preview.Y
```

### Release workflow (z tagiem)

```
Release Workflow                  MinVer                    Version
════════════════                 ══════                    ═══════

git checkout main
git pull
git tag v1.2.6          ────▶    Found v1.2.6     ────▶    1.2.6
dotnet build            ────▶    Uses tag         ────▶    1.2.6
dotnet pack             ────▶    Creates package  ────▶    Voyager.Common.Results.1.2.6.nupkg
git push origin v1.2.6  ────▶    Triggers CI/CD   ────▶    Auto-publish to NuGet
```

### Po release (preview builds)

```
Post-Release Workflow             MinVer                    Version
═════════════════════            ══════                    ═══════

# Tag v1.2.6 exists at commit #21

Commit #22              ────▶    v1.2.6 + 1       ────▶    1.2.7-preview.1
Commit #23              ────▶    v1.2.6 + 2       ────▶    1.2.7-preview.2
Commit #24              ────▶    v1.2.6 + 3       ────▶    1.2.7-preview.3

# MinVer auto-increments PATCH and adds preview suffix
```

---

## 📋 Checklist: Pierwsze wersjonowanie

- [ ] 1. Sprawdź, że nie masz uncommited changes: `git status`
- [ ] 2. Commituj wszystkie zmiany: `git add . && git commit -m "Prepare release"`
- [ ] 3. Utwórz tag: `git tag v1.2.6`
- [ ] 4. Zweryfikuj tag: `git tag` (powinno pokazać `v1.2.6`)
- [ ] 5. Clean build: `dotnet clean -c Release`
- [ ] 6. Rebuild: `dotnet build -c Release`
- [ ] 7. Sprawdź wersję DLL:
  ```powershell
  [System.Reflection.Assembly]::LoadFrom("$PWD\src\Voyager.Common.Results\bin\Release\net8.0\Voyager.Common.Results.dll").GetName()
  ```
- [ ] 8. Powinno pokazać `Version: 1.0.0.0` (AssemblyVersion)
- [ ] 9. (Opcjonalnie) Push tag: `git push origin v1.2.6`
- [ ] 10. (Opcjonalnie) Sprawdź GitHub Actions: projekt automatycznie opublikuje pakiet

---

## 🔗 Dodatkowe zasoby

- [MinVer GitHub](https://github.com/adamralph/minver) - Dokumentacja MinVer
- [Semantic Versioning](https://semver.org/) - Konwencja wersjonowania
- [.NET Assembly Versioning](https://learn.microsoft.com/en-us/dotnet/standard/assembly/versioning) - Microsoft Docs
- [docs/QUICK-START-VERSIONING.md](QUICK-START-VERSIONING.md) - Szybki przewodnik
- [docs/QUICK-START-VERSIONING.md](QUICK-START-VERSIONING.md) - Przewodnik wersjonowania
