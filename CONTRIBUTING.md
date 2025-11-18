# Contributing to Voyager Libraries

## Pull Request Requirements

Before submitting a PR, ensure ALL of the following are met:

### ✅ Mandatory Checks (CI will fail if not met)

1. **Build Success**
   - Code compiles without errors for both `net8.0` and `net48`
   - Zero build warnings (warnings treated as errors)
   - Run: `dotnet build -c Release`

2. **All Tests Pass**
   - 100% of tests must pass
   - No skipped or ignored tests without justification
   - Run: `dotnet test -c Release`

3. **Code Coverage** (if applicable)
   - Coverage reports are generated
   - No significant decrease in coverage percentage
   - Run: `dotnet test --collect:"XPlat Code Coverage"`
   
   **Viewing Coverage Reports:**
   
   *Online (public repos only):*
   - Coverage is automatically uploaded to Codecov after each push
   - View at: `https://codecov.io/gh/Voyager-Poland/{repo-name}`
   - Check PR comments for coverage changes
   
   *Locally:*
   ```bash
   # 1. Install ReportGenerator (one-time setup)
   dotnet tool install -g dotnet-reportgenerator-globaltool
   
   # 2. Run tests with coverage
   dotnet test --collect:"XPlat Code Coverage"
   
   # 3. Generate HTML report
   reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html
   
   # 4. Open report in browser
   Start-Process coverage-report/index.html
   ```
   
   The HTML report shows line-by-line coverage with color coding:
   - 🟢 Green = covered by tests
   - 🔴 Red = not covered by tests
   - 🟡 Yellow = partially covered

4. **Code Style**
   - `.editorconfig` rules enforced
   - No style violations
   - Format code: Right-click solution → "Format Document"

5. **XML Documentation**
   - All public classes have `<summary>` tags
   - All public methods have `<summary>` and `<returns>` tags
   - All parameters have `<param>` tags

6. **Result Pattern Compliance**
   - ✅ All methods returning data use `Result<T>` or `Result`
   - ❌ No `throw` for business logic
   - ❌ No `return null`
   - See: `docs/AI-INSTRUCTIONS.md`

### 📋 Code Review Checklist

Before requesting review:

- [ ] I have read and followed `docs/AI-INSTRUCTIONS.md`
- [ ] All public APIs have XML documentation
- [ ] I am using `Result<T>` or `Result` instead of exceptions
- [ ] I am not returning `null` anywhere
- [ ] I have written tests for new functionality
- [ ] I have not created duplicate tests
- [ ] SOLID principles are followed
- [ ] Code compiles without warnings
- [ ] All tests pass locally
- [ ] I have updated CHANGELOG.md (if applicable)
- [ ] I have updated README.md (if applicable)

## Workflow

### 1. Create a Feature Branch

```bash
git checkout -b feature/JIRA-123-your-feature-name
# or
git checkout -b fix/JIRA-456-bug-description
```

### 2. Make Your Changes

Follow the coding standards in `docs/AI-INSTRUCTIONS.md`.

**Example of correct code:**

```csharp
/// <summary>
/// Retrieves a user by their unique identifier.
/// </summary>
/// <param name="userId">The unique identifier of the user.</param>
/// <returns>
/// A <see cref="Result{User}"/> containing the user if found,
/// or an error if the user doesn't exist or validation fails.
/// </returns>
public Result<User> GetUser(int userId)
{
    if (userId <= 0)
        return Error.ValidationError("User ID must be positive");
    
    var user = _repository.Find(userId);
    
    if (user is null)
        return Error.NotFoundError($"User {userId} not found");
    
    return user;
}
```

### 3. Write Tests

```csharp
[TestFixture]
public class UserServiceTests
{
    [Test]
    public void GetUser_ValidId_ReturnsUser()
    {
        // Arrange
        var service = CreateService();
        
        // Act
        var result = service.GetUser(123);
        
        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Id, Is.EqualTo(123));
    }
    
    [Test]
    public void GetUser_InvalidId_ReturnsValidationError()
    {
        var service = CreateService();
        var result = service.GetUser(-1);
        
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error.Type, Is.EqualTo(ErrorType.Validation));
    }
}
```

### 4. Run Local Checks

```bash
# Build for both frameworks
dotnet build -c Release

# Run all tests
dotnet test -c Release

# Check code coverage (optional but recommended)
dotnet test --collect:"XPlat Code Coverage"

# Format code
# In Visual Studio: Ctrl+K, Ctrl+D
# In VS Code: Shift+Alt+F
```

### 5. Commit Your Changes

Write clear, descriptive commit messages that explain WHAT and WHY:

```bash
git add .
git commit -m "Add GetUser method with Result pattern to handle user retrieval"
git commit -m "Fix null reference in UserRepository when user not found"
git commit -m "Update README with new API examples and usage instructions"
git commit -m "Add edge case tests for GetUser method"
```

**Good commit messages:**
- ✅ "Add validation for user email format"
- ✅ "Fix memory leak in cache service"
- ✅ "Update documentation for Result pattern usage"
- ✅ "Refactor UserService to follow single responsibility principle"

**Bad commit messages:**
- ❌ "fix"
- ❌ "updates"
- ❌ "WIP"
- ❌ "changes"
- ❌ "asdf"

**Tips:**
- Start with a verb (Add, Fix, Update, Remove, Refactor)
- Be specific about what changed
- Explain WHY if the change isn't obvious
- Keep it under 72 characters when possible
- Reference JIRA ticket if applicable: "Fix login timeout (JIRA-123)"
```

### 6. Push and Create PR

```bash
git push origin feature/your-feature-name
```

Then create a Pull Request on GitHub.

## Pull Request Template

When creating a PR, please fill out:

```markdown
## Description
Brief description of what this PR does.

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Checklist
- [ ] Code compiles without warnings
- [ ] All tests pass
- [ ] XML documentation added for public APIs
- [ ] Using Result<T> pattern (no exceptions for business logic)
- [ ] No null returns
- [ ] SOLID principles followed
- [ ] CHANGELOG.md updated (if applicable)

## Testing
Describe how you tested your changes.
```

## Code Review Process

1. **Automated Checks** - GitHub Actions will run:
   - Build for net8.0 and net48
   - All tests
   - Code coverage (if configured)
   - Code style verification

2. **Manual Review**
   - At least one team member must review
   - Reviewer checks for:
     - Code quality and readability
     - SOLID principles
     - Proper use of Result pattern
     - XML documentation completeness
     - Test coverage

3. **Required Approvals**
   - 1 approval required for merge
   - All GitHub Actions checks must pass

4. **Merge**
   - Use "Squash and merge" for clean history
   - Delete branch after merge

## Common PR Rejection Reasons

### ❌ Will NOT be merged:

1. **Missing XML Documentation**
   ```csharp
   public User GetUser(int id) { } // ❌ No XML docs
   ```

2. **Using Exceptions for Business Logic**
   ```csharp
   if (user is null)
       throw new NotFoundException(); // ❌ Use Result pattern
   ```

3. **Returning Null**
   ```csharp
   public User GetUser(int id)
   {
       return _repository.Find(id); // ❌ Can return null
   }
   ```

4. **Build Warnings**
   ```
   warning CS1591: Missing XML comment for publicly visible type or member
   ```

5. **Failing Tests**
   ```
   Test UserServiceTests.GetUser_ValidId_ReturnsUser failed
   ```

6. **Duplicate Tests**
   ```csharp
   [Test]
   public void GetUser_Works() { } // ❌
   
   [Test]
   public void GetUser_ReturnsUser() { } // ❌ Duplicate
   ```

## Getting Help

- **Questions about standards?** → Read `docs/AI-INSTRUCTIONS.md`
- **Questions about Result pattern?** → Check `Voyager.Common.Results` documentation
- **Technical issues?** → Ask in team chat
- **Need code review?** → Tag a senior developer in PR

## Version Management

This project uses **Semantic Versioning**:
- **MAJOR** (1.x.x) - Breaking changes (manual)
- **MINOR** (x.1.x) - New features, backward compatible (manual)
- **PATCH** (x.x.1) - Bug fixes (automated by CI)

GitHub Actions automatically increments the patch version on every merge to `main`.

To bump MAJOR or MINOR:
1. Edit `.csproj` file: `<Version>2.0.0</Version>`
2. Update `CHANGELOG.md`
3. Create PR with version bump
4. After merge, GitHub Actions will auto-increment to `2.0.1`, `2.0.2`, etc.

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
