namespace InkFlow.BuildingBlocks.Application;

public readonly record struct Result<T>
{
    private Result(T? value, Error error, bool isSuccess)
    {
        Value = value;
        Error = error;
        IsSuccess = isSuccess;
    }

    public T? Value { get; }
    public Error Error { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public static Result<T> Success(T value) => new(value, Error.None, true);
    public static Result<T> Failure(Error error) => new(default, error, false);
}
