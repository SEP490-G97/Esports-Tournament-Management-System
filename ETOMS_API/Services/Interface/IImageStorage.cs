namespace ETOMS_API.Services.Interface
{
    public enum ImageError { None, Missing, TooLarge, BadType }

    public record ImageSaveResult(ImageError Error, string? Url = null);

    public interface IImageStorage
    {
        /// <summary>
        /// Kiểm tra (kích thước, đuôi file, chữ ký byte PNG/JPEG/WebP) rồi lưu vào wwwroot/uploads/{folder}.
        /// Trả về URL tương đối dạng /uploads/{folder}/{guid}.{ext}.
        /// </summary>
        Task<ImageSaveResult> SaveAsync(IFormFile? file, string folder, long maxBytes, CancellationToken ct = default);

        /// <summary>Xóa file đã lưu; bỏ qua nếu URL không nằm dưới /uploads/.</summary>
        void TryDelete(string? url);
    }
}
