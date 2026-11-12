namespace ETOMS_API.Utils;

// Bọc kết quả trả về của Service: thành công/thất bại + thông báo (tiếng Việt) + mã HTTP + lỗi theo field.
// Gộp từ các nhánh: hỗ trợ cả Result.Success(data) và Result<T>.Success(data).
public class Result
{
    protected Result(bool isSuccess, string message, int statusCode, IReadOnlyDictionary<string, string[]>? fieldErrors)
    {
        IsSuccess = isSuccess;
        Message = message;
        StatusCode = statusCode;
        FieldErrors = fieldErrors ?? new Dictionary<string, string[]>();
    }

    public bool IsSuccess { get; }
    public string Message { get; }
    public int StatusCode { get; }
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; }

    public static Result Success(string message = "Thao tác thành công.")
        => new(true, message, StatusCodes.Status200OK, null);

    public static Result Failure(string message, int statusCode = StatusCodes.Status400BadRequest)
        => new(false, message, statusCode, null);

    public static Result FieldFailure(string field, string message)
        => new(false, "Thông tin chưa hợp lệ.", StatusCodes.Status400BadRequest,
            new Dictionary<string, string[]> { [field] = [message] });

    public static Result<T> Success<T>(T data, string message = "Thao tác thành công.")
        => new(data, true, message, StatusCodes.Status200OK, null);

    public static Result<T> Failure<T>(string message, int statusCode = StatusCodes.Status400BadRequest)
        => new(default, false, message, statusCode, null);
}

public sealed class Result<T> : Result
{
    internal Result(T? data, bool isSuccess, string message, int statusCode, IReadOnlyDictionary<string, string[]>? fieldErrors)
        : base(isSuccess, message, statusCode, fieldErrors)
    {
        Data = data;
    }

    public T? Data { get; }

    // Cách gọi Result<T>.Success(data) (nhánh Khanh/Dũng). Mặc định message rỗng như bản gốc của nhánh đó;
    // cách gọi Result.Success(data) ở lớp cha vẫn giữ mặc định "Thao tác thành công." như cũ.
    public static Result<T> Success(T data, string message = "")
        => new(data, true, message, StatusCodes.Status200OK, null);

    public new static Result<T> Failure(
        string message,
        int statusCode = StatusCodes.Status400BadRequest)
        => new(default, false, message, statusCode, null);

    public new static Result<T> FieldFailure(string field, string message)
        => new(default, false, "Thông tin chưa hợp lệ.", StatusCodes.Status400BadRequest,
            new Dictionary<string, string[]> { [field] = [message] });
}
