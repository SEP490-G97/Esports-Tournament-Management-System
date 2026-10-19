namespace ETOMS.Application.Common;

// Bọc kết quả trả về của Service: thành công/thất bại + thông báo (tiếng Việt) + dữ liệu.
// Controller nhìn vào IsSuccess để quyết định trả Ok / BadRequest.
public class Result
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;

    public static Result Success(string message = "") => new() { IsSuccess = true, Message = message };
    public static Result Failure(string message) => new() { IsSuccess = false, Message = message };
}

public class Result<T> : Result
{
    public T? Data { get; init; }

    public static Result<T> Success(T data, string message = "")
        => new() { IsSuccess = true, Data = data, Message = message };
    public static new Result<T> Failure(string message)
        => new() { IsSuccess = false, Message = message };
}
