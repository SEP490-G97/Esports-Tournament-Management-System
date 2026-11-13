namespace ETOMS_API.Services.Interface
{
    public interface IEmailSender
    {
        Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
    }
}
