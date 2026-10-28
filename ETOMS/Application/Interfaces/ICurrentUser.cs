namespace ETOMS.Application.Interfaces;

// Đại diện cho người dùng đang đăng nhập. Hiện tạm lấy từ session (stub).
// Khi Vũ Đức Hải làm xong AUTH thì thay phần cài đặt bằng đọc từ JWT/Claims,
// các Service/Controller dùng interface này KHÔNG phải sửa gì.
public interface ICurrentUser
{
    int? UserId { get; }
    string? DisplayName { get; }
    bool IsAuthenticated { get; }
}
