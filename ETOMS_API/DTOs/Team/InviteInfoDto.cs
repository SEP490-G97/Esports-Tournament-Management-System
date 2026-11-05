namespace ETOMS_API.DTOs.Team;

// TEAM-04 (v2): mã mời là MỘT mã nằm trên Team (InviteCode + InviteExpiresAt).
public class InviteInfoDto
{
    public int TeamId { get; set; }
    public string? Code { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public bool HasCode => !string.IsNullOrEmpty(Code);
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
    public bool IsUsable => HasCode && !IsExpired;
}
