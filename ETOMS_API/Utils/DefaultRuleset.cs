using ETOMS_API.Models;

namespace ETOMS_API.Utils;

// Góp ý nhóm 18/11 mục 11: "vị trí chưa thấy được áp dụng trong giải đấu" — phần lớn giải không có bộ luật (BTC bỏ qua bước chọn)
// nên trang xếp vị trí đội hình không có gì để chọn. Khi công bố giải mà chưa có bộ luật, hệ thống tự gắn bộ luật mặc định:
// bộ luật CÒN DÙNG đầu tiên của tựa game khớp số người chính thức / dự bị của giải. BTC vẫn đổi được khi chưa có đơn đăng ký.
public static class DefaultRuleset
{
    public static GameRuleset? Pick(IEnumerable<GameRuleset> rulesetsOfGame, Tournament t)
        => rulesetsOfGame
            .Where(r => r.GameId == t.GameId && r.Status == "ACTIVE"
                && t.StarterCount >= r.MinStarters && t.StarterCount <= r.MaxStarters && t.MaxSubstitutes <= r.MaxSubstitutes)
            .OrderBy(r => r.GameRulesetId)
            .FirstOrDefault();
}
