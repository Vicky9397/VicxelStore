namespace Dpm.BuildingBlocks.Application;

public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Locked,
    RateLimited,
    /// <summary>The resource existed but is permanently gone, such as a quarantined file.</summary>
    Gone,
    /// <summary>The payment gateway declined the charge.</summary>
    PaymentFailed,
    Failure,
}

public sealed record Error(string Code, string Message, ErrorKind Kind)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorKind.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorKind.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorKind.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorKind.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorKind.Forbidden);

    public static Error Locked(string code, string message) => new(code, message, ErrorKind.Locked);

    public static Error RateLimited(string code, string message) => new(code, message, ErrorKind.RateLimited);

    public static Error Gone(string code, string message) => new(code, message, ErrorKind.Gone);

    public static Error PaymentFailed(string code, string message) => new(code, message, ErrorKind.PaymentFailed);

    public static Error Failure(string code, string message) => new(code, message, ErrorKind.Failure);
}
