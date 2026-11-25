namespace ETOMS_API.Utils;

// Nhớ tạm "tài khoản đang hoạt động" cho BanCheckMiddleware: database ở xa (~200 ms/lượt), trước đây MỌI
// request của người đã đăng nhập tốn thêm một lượt chỉ để hỏi trạng thái tài khoản.
// Chỉ nhớ trạng thái HOẠT ĐỘNG, tối đa 30 giây. Khóa/mở khóa (ADM) xóa mục nhớ ngay trên máy chủ này;
// máy chủ khác cùng dùng database sẽ thấy trạng thái mới sau tối đa 30 giây.
public static class AccountStatusCache
{
    public static readonly TimeSpan ActiveTtl = TimeSpan.FromSeconds(30);

    public static string Key(int accountId) => "account-active:" + accountId;
}
