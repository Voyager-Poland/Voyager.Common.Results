namespace Voyager.Common.Results;

/// <summary>
/// Error types in the system
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// No error
    /// </summary>
    None,

    /// <summary>
    /// Input validation error
    /// </summary>
    Validation,

    /// <summary>
    /// Permission/authorization error
    /// </summary>
    Permission,

    /// <summary>
    /// Database error
    /// </summary>
    Database,

    /// <summary>
    /// Business logic error
    /// </summary>
    Business,

    /// <summary>
    /// Resource not found
    /// </summary>
    NotFound,

    /// <summary>
    /// Conflict (e.g. duplicate)
    /// </summary>
    Conflict,

    /// <summary>
    /// Unexpected system error
    /// </summary>
    Unexpected
}
