namespace Voyager.Common.Results;

/// <summary>
/// Represents an error with a type, code and message
/// </summary>
public sealed record Error(ErrorType Type, string Code, string Message)
{
    /// <summary>
    /// No error - used for representing success
    /// </summary>
    public static readonly Error None = new(ErrorType.None, string.Empty, string.Empty);

    // ========== FACTORY METHODS ==========

    /// <summary>
    /// Creates a validation error
    /// </summary>
    public static Error ValidationError(string code, string message) =>
        new(ErrorType.Validation, code, message);

    /// <summary>
    /// Creates a validation error with a default code
    /// </summary>
    public static Error ValidationError(string message) =>
        new(ErrorType.Validation, "Validation.Failed", message);

    /// <summary>
    /// Creates a permission error
    /// </summary>
    public static Error PermissionError(string code, string message) =>
        new(ErrorType.Permission, code, message);

    /// <summary>
    /// Creates a permission error with a default code
    /// </summary>
    public static Error PermissionError(string message) =>
        new(ErrorType.Permission, "Permission.Denied", message);

    /// <summary>
    /// Creates a database error
    /// </summary>
    public static Error DatabaseError(string code, string message) =>
        new(ErrorType.Database, code, message);

    /// <summary>
    /// Creates a database error with a default code
    /// </summary>
    public static Error DatabaseError(string message) =>
        new(ErrorType.Database, "Database.Error", message);

    /// <summary>
    /// Creates a business logic error
    /// </summary>
    public static Error BusinessError(string code, string message) =>
        new(ErrorType.Business, code, message);

    /// <summary>
    /// Creates a business logic error with a default code
    /// </summary>
    public static Error BusinessError(string message) =>
        new(ErrorType.Business, "Business.RuleViolation", message);

    /// <summary>
    /// Creates a not found error
    /// </summary>
    public static Error NotFoundError(string code, string message) =>
        new(ErrorType.NotFound, code, message);

    /// <summary>
    /// Creates a not found error with a default code
    /// </summary>
    public static Error NotFoundError(string message) =>
        new(ErrorType.NotFound, "NotFound", message);

    /// <summary>
    /// Creates a conflict error
    /// </summary>
    public static Error ConflictError(string code, string message) =>
        new(ErrorType.Conflict, code, message);

    /// <summary>
    /// Creates a conflict error with a default code
    /// </summary>
    public static Error ConflictError(string message) =>
        new(ErrorType.Conflict, "Conflict", message);

    /// <summary>
    /// Creates an unexpected error
    /// </summary>
    public static Error UnexpectedError(string code, string message) =>
        new(ErrorType.Unexpected, code, message);

    /// <summary>
    /// Creates an unexpected error with a default code
    /// </summary>
    public static Error UnexpectedError(string message) =>
        new(ErrorType.Unexpected, "Unexpected.Error", message);

    /// <summary>
    /// Creates an error from an exception
    /// </summary>
    public static Error FromException(Exception exception) =>
        new(ErrorType.Unexpected, "Exception", exception.Message);
}
