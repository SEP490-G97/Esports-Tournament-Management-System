namespace ETOMS_API.DTOs.Registration;

// REG-03: Tải minh chứng lệ phí. File upload xử lý ở Controller (IFormFile),
// đây chỉ giữ Note tùy chọn kèm minh chứng.
public class UploadProofRequest
{
    public string? Note { get; set; }
}
