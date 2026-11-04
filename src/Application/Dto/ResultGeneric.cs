namespace Application.Dto;

public class ResultGeneric<T>(T value, bool isSuccess, List<string> errors)
{
    public T Value { get; } = value;
    public bool IsSuccess { get; } = isSuccess;
    public List<string> Errors { get; } = errors ?? [];

    public static ResultGeneric<T> Success(T value) => new(value, true, []);
    public static ResultGeneric<T> Failure(List<string> errors) => new(default!, false, errors ?? []);
    public static ResultGeneric<T> Failure(string error) => new(default!, false, [error]);
}

