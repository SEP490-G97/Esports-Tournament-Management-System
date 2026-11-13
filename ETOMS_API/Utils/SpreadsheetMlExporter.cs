using System.Globalization;
using System.Text;
using System.Xml;
using ETOMS_API.DTOs.Ranking;

namespace ETOMS_API.Utils;

public static class SpreadsheetMlExporter
{
    private const string SpreadsheetNamespace = "urn:schemas-microsoft-com:office:spreadsheet";

    public static byte[] Build(TournamentReportResponse report)
    {
        var sheets = new[]
        {
            new Sheet("Đăng ký",
                ["Mã đơn", "Tên đội", "Thẻ đội", "Trạng thái", "Trạng thái lệ phí", "Gửi lúc", "Thành tích", "Hạng"],
                report.Registrations.Select(item => new object?[]
                {
                    item.RegistrationId, item.TeamName, item.TeamTag, item.Status, item.PaymentStatus,
                    item.SubmittedAt, item.FinalPlacement, item.FinalRank
                })),
            new Sheet("Lệ phí",
                ["Mã đơn", "Tên đội", "Loại bản ghi", "Số tiền khai báo", "Ghi chú", "Hiện hành", "Thời điểm ghi nhận"],
                report.Payments.Select(item => new object?[]
                {
                    item.RegistrationId, item.TeamName, item.RecordKind, item.DeclaredAmount,
                    item.Note, item.IsCurrent ? "Có" : "Không", item.RecordedAt
                })),
            new Sheet("Trận đấu",
                ["Mã trận", "Đội A", "Đội B", "Đội thắng", "Tỷ số A", "Tỷ số B", "Trạng thái", "Lịch thi đấu", "Chốt lúc"],
                report.Matches.Select(item => new object?[]
                {
                    item.MatchId, item.TeamA, item.TeamB, item.Winner, item.ScoreA, item.ScoreB,
                    item.Status, item.ScheduledAt, item.VerifiedAt
                })),
            new Sheet("Tranh chấp",
                ["Mã trận", "Lý do", "Trạng thái", "Phán quyết", "Khiếu nại", "Mở lúc", "Xử lý lúc"],
                report.Disputes.Select(item => new object?[]
                {
                    item.MatchId, item.OpenReason, item.Status, item.RulingReason,
                    item.AppealStatus, item.OpenedAt, item.ResolvedAt
                })),
            new Sheet("Vòng bảng",
                ["Bảng", "Mã đội", "Tên đội", "Đã đấu", "Thắng", "Thua", "Hiệu số", "Điểm"],
                report.Standings.Select(item => new object?[]
                {
                    item.GroupLabel, item.TeamId, item.TeamName, item.Played,
                    item.Won, item.Lost, item.ScoreDiff, item.Points
                }))
        };

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            CloseOutput = false
        }))
        {
            writer.WriteStartDocument();
            writer.WriteProcessingInstruction("mso-application", "progid=\"Excel.Sheet\"");
            writer.WriteStartElement("Workbook", SpreadsheetNamespace);
            writer.WriteAttributeString("xmlns", "ss", null, SpreadsheetNamespace);

            foreach (var sheet in sheets)
            {
                writer.WriteStartElement("Worksheet", SpreadsheetNamespace);
                writer.WriteAttributeString("ss", "Name", SpreadsheetNamespace, sheet.Name);
                writer.WriteStartElement("Table", SpreadsheetNamespace);
                WriteRow(writer, sheet.Headers.Cast<object?>());
                foreach (var row in sheet.Rows)
                {
                    WriteRow(writer, row);
                }
                writer.WriteEndElement();
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return stream.ToArray();
    }

    private static void WriteRow(XmlWriter writer, IEnumerable<object?> cells)
    {
        writer.WriteStartElement("Row", SpreadsheetNamespace);
        foreach (var value in cells)
        {
            writer.WriteStartElement("Cell", SpreadsheetNamespace);
            var (type, content) = Format(value);
            writer.WriteStartElement("Data", SpreadsheetNamespace);
            writer.WriteAttributeString("ss", "Type", SpreadsheetNamespace, type);
            writer.WriteString(content);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static (string Type, string Value) Format(object? value)
    {
        if (value is null) return ("String", string.Empty);
        if (value is DateTimeOffset dateTimeOffset)
            return ("DateTime", dateTimeOffset.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
        if (value is DateTime dateTime)
            return ("DateTime", DateTime.SpecifyKind(dateTime, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
        if (value is bool boolean) return ("String", boolean ? "Có" : "Không");
        if (value is IFormattable formattable && value is not string)
            return ("Number", formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty);
        return ("String", value.ToString() ?? string.Empty);
    }

    private sealed record Sheet(string Name, IReadOnlyList<string> Headers, IEnumerable<object?[]> Rows);
}
