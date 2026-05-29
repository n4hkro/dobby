namespace Dobby.Core;

public sealed class Result
{
    private Result(bool isSuccess, ErrorCode? errorCode)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
    }

    public bool IsSuccess { get; }

    public ErrorCode? ErrorCode { get; }

    public static Result Success() => new(true, null);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result Failure(ErrorCode errorCode) => new(false, errorCode);

    public static Result<T> Failure<T>(ErrorCode errorCode) => Result<T>.Failure(errorCode);

    public static Result<T> Cancelled<T>() => Failure<T>(Dobby.Core.ErrorCode.Cancelled);
}

public sealed class Result<T>
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, ErrorCode? errorCode)
    {
        IsSuccess = isSuccess;
        _value = value;
        ErrorCode = errorCode;
    }

    public bool IsSuccess { get; }

    public ErrorCode? ErrorCode { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result does not expose a value.");

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(ErrorCode errorCode) => new(false, default, errorCode);
}

public readonly record struct ErrorCode(string Value)
{
    public static readonly ErrorCode Cancelled = new("Cancelled");
    public static readonly ErrorCode PluginInternalError = new("PluginInternalError");
    public static readonly ErrorCode InvalidInput = new("InvalidInput");
    public static readonly ErrorCode UnsupportedOperation = new("UnsupportedOperation");
    public static readonly ErrorCode DatabaseUnavailable = new("DatabaseUnavailable");
    public static readonly ErrorCode NetworkUnavailable = new("NetworkUnavailable");
    public static readonly ErrorCode SerializationFailed = new("SerializationFailed");
    public static readonly ErrorCode IoFailed = new("IoFailed");
    public static readonly ErrorCode NotFound = new("NotFound");
    public static readonly ErrorCode AlreadyExists = new("AlreadyExists");
    public static readonly ErrorCode ValidationFailed = new("ValidationFailed");
    public static readonly ErrorCode Unauthorized = new("Unauthorized");
    public static readonly ErrorCode Forbidden = new("Forbidden");
    public static readonly ErrorCode LicenseDenied = new("LicenseDenied");
    public static readonly ErrorCode LicenseReadOnly = new("LicenseReadOnly");
    public static readonly ErrorCode LicensePending = new("LicensePending");
    public static readonly ErrorCode LicenseBindingMismatch = new("LicenseBindingMismatch");

    public override string ToString() => Value;
}
