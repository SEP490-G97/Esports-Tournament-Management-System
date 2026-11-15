namespace ETOMS_API.Services.Interface
{
    public interface IOneTimeTokenService
    {
        /// <summary>Tạo token mới, đồng thời vô hiệu hóa token cùng loại chưa dùng của tài khoản. Trả về token thô.</summary>
        Task<string> IssueAsync(int accountId, string tokenType, TimeSpan lifetime, CancellationToken ct = default);

        /// <summary>Đánh dấu đã dùng (atomic). Trả về AccountID nếu token hợp lệ, null nếu sai/hết hạn/đã dùng.</summary>
        Task<int?> ConsumeAsync(string rawToken, string tokenType, CancellationToken ct = default);
    }
}
