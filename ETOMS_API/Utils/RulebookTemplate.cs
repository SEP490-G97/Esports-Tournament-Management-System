using System.Globalization;
using System.Text;
using ETOMS_API.Models;

namespace ETOMS_API.Utils;

// Góp ý 23/11 mục 8: nhiều giải chỉ có một dòng thể lệ hoặc để trống. Bộ dựng này sinh bản thể lệ ĐẦY ĐỦ cho giải
// từ dữ liệu thật của hệ thống: cấu hình giải (thể thức, số ván từng vòng, đội hình, lệ phí...), luật của tựa game
// (bản đồ, vị trí, loại tệp minh chứng — bảng Game / GameRuleset) và quy trình trận của ETOMS (điểm danh 30 phút,
// báo cáo mù hai bên, hạn báo cáo, tranh chấp). Phần luật riêng từng tựa game viết theo cách các giải chính thức của
// nhà phát hành tổ chức (Riot Games, Garena, Tencent/Krafton, EA/Nexon) — ETOMS không nối API nào của họ (LI-01).
// Ban tổ chức dùng làm bản nháp rồi sửa lại cho giải của mình ở trang Soạn thể lệ. Chỉ dùng thẻ RichTextSanitizer cho phép.
public static class RulebookTemplate
{
    public static string Build(Tournament t, Game g, IReadOnlyList<string> maps, IReadOnlyList<string> positions)
    {
        var html = new StringBuilder();
        var code = (g.ShortCode ?? string.Empty).ToUpperInvariant();

        H2(html, "1. Thông tin chung");
        Ul(html,
            $"Giải: <strong>{E(t.Name)}</strong> — tựa game <strong>{E(g.Title)}</strong>.",
            $"Thể thức: <strong>{FormatLabel(t.Format)}</strong>{FormatDetail(t)}; tối đa {t.MaxTeams} đội.",
            t.IsOnline ? "Hình thức: thi đấu trực tuyến." : $"Hình thức: thi đấu tại địa điểm{(string.IsNullOrWhiteSpace(t.Venue) ? "" : " — " + E(t.Venue))}.",
            $"Đăng ký: {Vn(t.RegOpenAt)} đến {Vn(t.RegCloseAt)}. Khai mạc: {Vn(t.StartAt)}.",
            "Ban tổ chức có quyền điều chỉnh lịch thi đấu và sẽ thông báo cho các đội trên ETOMS. Khi thể lệ đổi, ETOMS tạo phiên bản mới; đội vẫn giữ bản đã chấp nhận lúc đăng ký.");

        H2(html, "2. Điều kiện tham gia và đội hình");
        Ul(html,
            $"Mỗi đội có <strong>{t.StarterCount} tuyển thủ chính thức</strong>{(t.MaxSubstitutes > 0 ? $" và tối đa <strong>{t.MaxSubstitutes} dự bị</strong>" : ", không có dự bị")}; đội trưởng là người đăng ký và chịu trách nhiệm liên lạc với Ban tổ chức.",
            t.RequireInGameName ? "Mỗi tuyển thủ phải khai báo đúng tên trong game (IGN); tên trong game phải trùng tài khoản thi đấu thực tế." : "Tên trong game khai báo trên hồ sơ phải trùng tài khoản thi đấu thực tế.",
            t.RequireIdentityDocument ? "Tuyển thủ phải nộp giấy tờ định danh khi đăng ký; Ban tổ chức đối chiếu trước khi duyệt đơn." : null,
            t.EntryFee > 0
                ? $"Lệ phí: {t.EntryFee.ToString("N0", Vi)} đồng/đội. ETOMS không thu tiền — đội chuyển khoản theo thông tin của Ban tổ chức và tải minh chứng lên đơn đăng ký."
                : "Giải miễn lệ phí.",
            "Một tuyển thủ chỉ được thi đấu cho một đội trong giải. Đội hình khóa khi Ban tổ chức khóa nhánh đấu.",
            "Thay người trong trận: đội trưởng gửi yêu cầu thay người ở trang trận (người ra, người vào là thành viên đã đăng ký, có lý do); trọng tài hoặc Ban tổ chức duyệt.");

        H2(html, "3. Số ván và cách xếp hạng");
        Ul(html, BestOfLines(t).ToArray());
        if (t.Format is "ROUND_ROBIN" or "HYBRID")
            P(html, "Xếp hạng vòng bảng: thắng 1 trận được 3 điểm, thua 0 điểm. Bằng điểm thì xét lần lượt: " + TieBreakers(t.TieBreakerOrder) + ".");
        if (t.HasThirdPlaceMatch) P(html, "Có trận tranh hạng Ba giữa hai đội thua ở bán kết.");

        H2(html, "4. Quy trình một trận trên ETOMS");
        Ul(html,
            "Điểm danh mở trước giờ thi đấu 30 phút và đóng đúng giờ thi đấu. Đội trưởng bấm <strong>Điểm danh</strong> ở trang trận.",
            "Hết giờ điểm danh mà chỉ một đội có mặt: hệ thống tự xử thắng không thi đấu cho đội có mặt. Hai đội cùng vắng thì Ban tổ chức xếp lại giờ.",
            "Khi cả hai đội đã điểm danh, phòng chờ mở: trọng tài hoặc Ban tổ chức gửi mã phòng, hai đội trao đổi trong khung chat của trận.",
            "Kết thúc trận, <strong>mỗi đội trưởng tự báo cáo tỉ số</strong> kèm minh chứng. Hai bản báo cáo độc lập, đội này không thấy bản của đội kia.",
            "Hạn báo cáo = giờ thi đấu + 60 phút cho mỗi ván + 60 phút. Quá hạn mà thiếu báo cáo, Ban tổ chức xử lý.",
            "Hai bản trùng nhau thì kết quả được xác nhận tự động và đội thắng đi tiếp. Hai bản lệch nhau thì trận chuyển sang <strong>tranh chấp</strong>; trọng tài xem minh chứng và ra phán quyết.");

        H2(html, "5. Luật riêng của " + E(g.Title));
        GameRules(html, code, t, maps, positions);

        H2(html, "6. Minh chứng kết quả");
        Ul(html,
            $"Tệp được nhận: {E(EvidenceTypes(g.AllowedEvidenceTypes))}; tối đa {g.MaxEvidenceFileSizeMb} MB mỗi tệp.",
            EvidenceHint(code),
            "Minh chứng phải chụp nguyên màn hình, không cắt ghép, thấy rõ tên đội hoặc tên tuyển thủ và tỉ số.");

        H2(html, "7. Kỷ luật");
        Ul(html,
            "Mức xử lý: Cảnh cáo, Lỗi nhẹ, Lỗi nặng, Tước quyền thi đấu, Hủy kết quả. Mỗi án phạt có điểm phạt và lý do ghi trên ETOMS.",
            t.PenaltyThreshold is > 0 ? $"Đội cộng dồn từ {t.PenaltyThreshold} điểm phạt trở lên có thể bị tước quyền thi đấu." : null,
            "Nghiêm cấm: phần mềm thứ ba, hack, lợi dụng lỗi game, chơi hộ / dùng tài khoản người khác, thỏa thuận dàn xếp kết quả.",
            "Nghiêm cấm xúc phạm, phân biệt đối xử trong khung chat trận và trên kênh của giải. Tin nhắn xúc phạm có thể bị báo cáo và là căn cứ xử phạt.",
            "Báo cáo tỉ số sai sự thật là lỗi nặng.");

        H2(html, "8. Khiếu nại");
        P(html, "Khiếu nại về kết quả được gửi qua tranh chấp của trận trên ETOMS, kèm minh chứng. Phán quyết của trọng tài và Ban tổ chức là quyết định cuối cùng của giải.");
        return html.ToString();
    }

    private static void GameRules(StringBuilder html, string code, Tournament t, IReadOnlyList<string> maps, IReadOnlyList<string> positions)
    {
        var mapList = maps.Count > 0 ? string.Join(", ", maps.Select(E)) : null;
        var posList = positions.Count > 0 ? string.Join(", ", positions.Select(E)) : null;
        switch (code)
        {
            case "LOL":
                Ul(html,
                    $"Bản đồ {mapList ?? "Summoner's Rift"}, phòng tùy chỉnh 5v5, chế độ <strong>Cấm chọn giải đấu</strong> (Tournament Draft): mỗi đội cấm 5 tướng theo lượt luân phiên.",
                    posList is null ? null : $"Vị trí: {posList}.",
                    "Chọn phe: ván 1 do đội có hạt giống cao hơn chọn (cùng hạt giống thì trọng tài tung đồng xu); từ ván 2 đội thua ván trước được chọn phe.",
                    "Ván đấu được ghi nhận khi hai đội đã giao tranh, gây sát thương lên lính/quái/công trình, hoặc đồng hồ qua 2:00. Lỗi kỹ thuật trước thời điểm đó thì làm lại ván với cùng lượt cấm chọn.",
                    "Tạm dừng: mỗi đội tối đa 10 phút tạm dừng kỹ thuật mỗi ván, phải báo lý do trong khung chat trận. Tuyển thủ mất kết nối được chờ kết nối lại trong thời gian tạm dừng còn lại.",
                    "Bỏ ván giữa chừng tính là thua ván.");
                break;
            case "LMTC":
                Ul(html,
                    $"Bản đồ {mapList ?? "Summoner's Rift"} (Tốc Chiến), phòng tùy chỉnh 5v5, chế độ cấm chọn giải đấu: hai đội luân phiên cấm và chọn tướng.",
                    posList is null ? null : $"Vị trí: {posList}.",
                    "Chọn phe: ván 1 do đội có hạt giống cao hơn chọn (cùng hạt giống thì trọng tài tung đồng xu); từ ván 2 đội thua ván trước được chọn phe.",
                    "Thi đấu trên điện thoại hoặc máy tính bảng cá nhân; không dùng giả lập trên máy tính.",
                    "Mất kết nối: đội được tạm dừng tối đa 5 phút mỗi ván để kết nối lại. Lỗi xảy ra trước mạng hạ gục đầu tiên thì trọng tài có thể cho làm lại ván.",
                    "Bỏ ván giữa chừng tính là thua ván.");
                break;
            case "LQM":
                Ul(html,
                    $"Bản đồ {mapList ?? "Đấu trường 5v5"}, phòng tùy chỉnh 5v5 chế độ <strong>Đấu giải</strong> (cấm chọn luân phiên).",
                    posList is null ? null : $"Vị trí: {posList}.",
                    "Chọn phe: ván 1 do đội có hạt giống cao hơn chọn (cùng hạt giống thì trọng tài tung đồng xu); từ ván 2 đội thua ván trước được chọn phe.",
                    "Thi đấu trên điện thoại hoặc máy tính bảng cá nhân; không dùng giả lập trên máy tính, không dùng tay cầm hay macro.",
                    "Mất kết nối: đội được tạm dừng tối đa 5 phút mỗi ván để kết nối lại. Lỗi xảy ra trước mạng hạ gục đầu tiên thì trọng tài có thể cho làm lại ván.",
                    "Bỏ ván giữa chừng tính là thua ván.");
                break;
            case "VAL":
                Ul(html,
                    $"Phòng tùy chỉnh 5v5, bật Chế độ giải đấu (Tournament Mode), hiệp phụ <strong>thắng cách biệt 2 hiệp</strong>. Đội thắng 13 hiệp trước thắng bản đồ.",
                    mapList is null ? null : $"Kho bản đồ: {mapList}.",
                    "Chọn bản đồ — BO1: hai đội luân phiên cấm đến khi còn một bản đồ. BO3: cấm, cấm, chọn, chọn, cấm, cấm; bản đồ còn lại là bản đồ quyết định. Đội không chọn bản đồ được chọn phe Tấn công / Phòng thủ.",
                    posList is null ? null : $"Vai trò đặc vụ: {posList}.",
                    "Tạm dừng chiến thuật: mỗi đội 2 lần mỗi bản đồ, mỗi lần 60 giây, gọi trong thời gian mua đồ. Tạm dừng kỹ thuật phải báo trọng tài.",
                    "Tuyển thủ mất kết nối giữa hiệp thì hiệp đó vẫn tính; trận tạm dừng ở đầu hiệp sau để chờ kết nối lại.");
                break;
            case "PUM":
                Ul(html,
                    $"Chế độ Squad 4 người, góc nhìn thứ ba (TPP).{(mapList is null ? "" : " Bản đồ: " + mapList + ".")}",
                    posList is null ? null : $"Vai trò trong đội: {posList}.",
                    "Điểm mỗi ván = điểm hạng + điểm hạ gục (1 điểm/mạng). Điểm hạng: hạng 1: 10, hạng 2: 6, hạng 3: 5, hạng 4: 4, hạng 5: 3, hạng 6: 2, hạng 7–8: 1, từ hạng 9: 0.",
                    "Trên ETOMS mỗi trận là hai đội đối đầu trong cùng phòng tùy chỉnh: đội có tổng điểm cao hơn ở một ván thắng ván đó; bằng điểm thì đội có hạng sinh tồn cao hơn thắng.",
                    "Thi đấu trên điện thoại hoặc máy tính bảng cá nhân; không dùng giả lập trên máy tính, không dùng tay cầm hay macro.",
                    "Mất kết nối sau khi máy bay cất cánh: ván vẫn tiếp tục, không đấu lại.");
                break;
            case "FCO":
                Ul(html,
                    $"Thi đấu đồng đội: mỗi đội {t.StarterCount} tuyển thủ chính thức, mỗi ván là một lượt đấu <strong>1 đấu 1</strong> giữa hai tuyển thủ của hai đội. Đội thắng nhiều ván hơn thắng trận.",
                    "Thứ tự ra sân do đội trưởng báo cho trọng tài trong khung chat trận trước giờ thi đấu, không đổi sau khi trận bắt đầu.",
                    "Thiết lập phòng: chế độ đấu giao hữu, độ khó và thời lượng hiệp theo quy định của Ban tổ chức, mỗi tuyển thủ dùng đội hình trên tài khoản của chính mình.",
                    "Ván hòa ở vòng loại trực tiếp được phân định bằng hiệp phụ và luân lưu 11 mét theo cài đặt của game.",
                    "Mất kết nối: nếu chưa có bàn thắng thì đá lại ván; nếu đã có bàn thắng, trọng tài quyết định giữ kết quả hoặc đá lại phần còn lại.");
                break;
            default:
                Ul(html,
                    mapList is null ? "Bản đồ và chế độ chơi theo thiết lập phòng giải đấu của game." : $"Bản đồ: {mapList}.",
                    posList is null ? null : $"Vị trí: {posList}.",
                    "Mất kết nối và lỗi kỹ thuật do trọng tài quyết định đấu lại hay giữ kết quả.");
                break;
        }
    }

    private static string EvidenceHint(string code) => code switch
    {
        "LOL" => "Chụp màn hình bảng tỉ số cuối ván; nên kèm tệp phát lại .rofl khi có tranh chấp.",
        "VAL" => "Chụp màn hình bảng điểm cuối bản đồ (thấy tỉ số hiệp của hai đội).",
        "PUM" => "Chụp màn hình bảng xếp hạng cuối ván (hạng và số mạng hạ gục của cả đội).",
        "FCO" => "Chụp màn hình kết quả cuối mỗi ván 1 đấu 1 (thấy tên hai tuyển thủ và tỉ số).",
        _ => "Chụp màn hình kết quả cuối mỗi ván (thấy tên hai đội và tỉ số)."
    };

    private static IEnumerable<string> BestOfLines(Tournament t)
    {
        var parts = (t.BestOfPerRound ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Split(':', StringSplitOptions.TrimEntries))
            .Where(kv => kv.Length == 2 && int.TryParse(kv[1], out var bo) && bo is 1 or 3 or 5 or 7)
            .Select(kv => (Key: kv[0].ToUpperInvariant(), Bo: int.Parse(kv[1])))
            .ToList();
        if (parts.Count == 0)
        {
            yield return "Mỗi trận thi đấu một ván (BO1), trừ khi Ban tổ chức thông báo khác.";
            yield break;
        }
        foreach (var (key, bo) in parts.OrderBy(p => p.Key == "F" ? int.MaxValue : int.TryParse(p.Key.TrimStart('R'), out var r) ? r : 0))
            yield return $"{(key == "F" ? "Chung kết" : "Vòng " + key.TrimStart('R'))}: BO{bo}{(bo > 1 ? $" (thắng {bo / 2 + 1} ván trước thắng trận)" : "")}.";
        yield return "Vòng không ghi ở trên thi đấu một ván (BO1).";
    }

    private static string FormatLabel(string format) => format switch
    {
        "SINGLE_ELIMINATION" => "Loại trực tiếp",
        "DOUBLE_ELIMINATION" => "Loại kép",
        "ROUND_ROBIN" => "Vòng tròn",
        "HYBRID" => "Vòng bảng rồi loại trực tiếp",
        _ => format
    };

    private static string FormatDetail(Tournament t) => t.Format switch
    {
        "SINGLE_ELIMINATION" => " — thua một trận là bị loại",
        "DOUBLE_ELIMINATION" => " — đội thua ở nhánh thắng rơi xuống nhánh thua, thua hai trận mới bị loại; chung kết tổng thi đấu một trận",
        "ROUND_ROBIN" => t.GroupCount is > 1 ? $" — {t.GroupCount} bảng, các đội trong bảng gặp nhau một lượt" : " — các đội gặp nhau một lượt",
        "HYBRID" => $" — {t.GroupCount ?? 1} bảng đấu vòng tròn, mỗi bảng chọn {t.AdvancePerGroup ?? 1} đội vào vòng loại trực tiếp",
        _ => string.Empty
    };

    private static string TieBreakers(string? order)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HEAD_TO_HEAD"] = "kết quả đối đầu",
            ["GAME_DIFFERENCE"] = "hiệu số ván thắng – thua",
            ["TOTAL_GAMES_WON"] = "tổng số ván thắng",
            ["RANDOM_DRAW"] = "bốc thăm"
        };
        var items = (order ?? "HEAD_TO_HEAD,GAME_DIFFERENCE,TOTAL_GAMES_WON,RANDOM_DRAW")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => labels.TryGetValue(k, out var v) ? v : null).Where(v => v is not null).ToList();
        return items.Count > 0 ? string.Join(", ", items) : "kết quả đối đầu, hiệu số ván, bốc thăm";
    }

    private static string EvidenceTypes(string? csv)
        => string.Join(", ", (csv ?? "png,jpg").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => "." + x.ToLowerInvariant()));

    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    private static string Vn(DateTime utc) => utc.AddHours(7).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);
    // Chỉ thoát ký tự đặc biệt của HTML, giữ nguyên chữ tiếng Việt (HtmlEncode biến "ê" thành &#234;).
    private static string E(string? s) => (s ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    public static string Escape(string? s) => E(s);
    private static void H2(StringBuilder b, string text) => b.Append("<h2>").Append(text).Append("</h2>");
    private static void P(StringBuilder b, string text) => b.Append("<p>").Append(text).Append("</p>");
    private static void Ul(StringBuilder b, params string?[] items)
    {
        b.Append("<ul>");
        foreach (var item in items.Where(i => !string.IsNullOrWhiteSpace(i))) b.Append("<li>").Append(item).Append("</li>");
        b.Append("</ul>");
    }
}
