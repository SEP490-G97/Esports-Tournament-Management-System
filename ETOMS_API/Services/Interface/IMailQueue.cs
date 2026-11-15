using System.Threading.Channels;

namespace ETOMS_API.Services.Interface;

public record QueuedMail(string To, string Subject, string HtmlBody);

/// <summary>Hàng đợi email trong bộ nhớ, dùng cho gửi hàng loạt (thông báo hệ thống) để không chặn request.</summary>
public interface IMailQueue
{
    bool TryEnqueue(QueuedMail mail);
    ChannelReader<QueuedMail> Reader { get; }
}