using ETOMS.Application._Example;

namespace ETOMS.Tests._Example;

// VÍ DỤ MẪU test một Service. Khi Service thật inject IGenericRepository<T>,
// dùng Moq để giả lập repository (không cần database thật).
public class SampleServiceTests
{
    [Fact]
    public async Task GetAllAsync_TraVe_DanhSach_KhongRong()
    {
        var service = new SampleService();

        var result = await service.GetAllAsync();

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetByIdAsync_IdKhongTonTai_TraVe_Null()
    {
        var service = new SampleService();

        var result = await service.GetByIdAsync(999);

        Assert.Null(result);
    }
}
