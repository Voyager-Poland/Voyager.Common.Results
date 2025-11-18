## Description
<!-- Brief description of what this PR does -->

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update
- [ ] Code refactoring

## Mandatory Checklist (CI will verify)
- [ ] Code compiles without warnings for both net8.0 and net48
- [ ] All tests pass
- [ ] Code coverage maintained or improved
- [ ] `.editorconfig` style rules followed

## Code Quality Checklist
- [ ] All public classes have XML documentation (`<summary>`)
- [ ] All public methods have XML documentation (`<summary>`, `<returns>`, `<param>`)
- [ ] Using `Result<T>` or `Result` pattern (no exceptions for business logic)
- [ ] No `return null` statements
- [ ] No duplicate tests
- [ ] SOLID principles followed
- [ ] Railway operators used for chaining (Map, Bind, Tap, Ensure)

## Documentation
- [ ] CHANGELOG.md updated (if applicable)
- [ ] README.md updated (if applicable)
- [ ] New public APIs documented in docs/ (if applicable)

## Testing
<!-- Describe how you tested your changes -->

**Test scenarios covered:**
- [ ] Success path
- [ ] Validation errors
- [ ] Edge cases
- [ ] Error handling

## Breaking Changes
<!-- If this is a breaking change, describe the impact and migration path -->

## Additional Notes
<!-- Any additional information that reviewers should know -->

---

## For Reviewers

**Review Focus:**
- [ ] Code follows `docs/AI-INSTRUCTIONS.md`
- [ ] Result Pattern used correctly
- [ ] XML documentation complete and accurate
- [ ] No business logic exceptions
- [ ] Test coverage adequate
- [ ] SOLID principles respected
