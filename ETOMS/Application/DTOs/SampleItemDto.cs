namespace ETOMS.Application.DTOs;

// DTO chỉ có field cần cho client — KHÔNG có CreatedBy. "Cần gì lấy đó, không xổ full entity."
public class SampleItemDto
{
    public int Id { get; set; }
    public string Ten { get; set; } = string.Empty;
}
