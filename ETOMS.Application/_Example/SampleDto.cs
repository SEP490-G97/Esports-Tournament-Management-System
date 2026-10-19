namespace ETOMS.Application._Example;

// VÍ DỤ MẪU — copy cấu trúc này sang module của bạn (DTOs/<MÃ_MODULE>/).
// Quy tắc: API chỉ trả đúng field cần cho màn hình, KHÔNG trả nguyên entity.
// Ví dụ entity Team có cả cột nội bộ (CreatedBy, RowVersion, IsDeleted...) thì
// DTO này chỉ lấy những gì client cần hiển thị.
public class SampleDto
{
    public int Id { get; set; }
    public string Ten { get; set; } = string.Empty;
}
