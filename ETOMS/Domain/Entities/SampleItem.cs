namespace ETOMS.Domain.Entities;

// VÍ DỤ MẪU một entity (thực tế entity sẽ được scaffold từ database — CLAUDE.md mục 6).
// Để minh họa: entity có cả cột nội bộ (CreatedBy) mà API KHÔNG nên trả ra.
public class SampleItem
{
    public int Id { get; set; }
    public string Ten { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty; // cột nội bộ, không xổ ra API
}
