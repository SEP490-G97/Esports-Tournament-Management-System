namespace ETOMS.Application.Common;

// Bọc kết quả trả về của Service: thành công/thất bại + thông báo (tiếng Việt) + lỗi theo field.
// Controller nhìn IsSuccess để quyết định; FieldErrors để gắn lỗi vào đúng ô trên form.
public class Result
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;

    // Khóa = tên field (vd "Name", "Tag"), giá trị = thông báo lỗi tiếng Việt.
    public Dictionary<string, string> FieldErrors { get; init; } = new();

    public static Result Success(string message = "") => new() { IsSuccess = true, Message = message };
    public static Result Failure(string message) => new() { IsSuccess = false, Message = message };

    public static Result FieldFailure(string field, string message)
        => new() { IsSuccess = false, Message = message, FieldErrors = new() { [field] = message } };
}

public class Result<T> : Result
{
    public T? Data { get; init; }

    public static Result<T> Success(T data, string message = "")
        => new() { IsSuccess = true, Data = data, Message = message };
    public static new Result<T> Failure(string message)
        => new() { IsSuccess = false, Message = message };
    public static new Result<T> FieldFailure(string field, string message)
        => new() { IsSuccess = false, Message = message, FieldErrors = new() { [field] = message } };
}
