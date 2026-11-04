namespace Application.Dto;

public class Result(bool isSuccess, List<string> errors)
{
    public bool IsSuccess { get; } = isSuccess;
    public List<string> Errors { get; } = errors;

    public static Result Success() => new(true, []);
    public static Result Failure(List<string> errors) => new(false, errors);
    public static Result Failure(string error) => new(false, [error]);
}
