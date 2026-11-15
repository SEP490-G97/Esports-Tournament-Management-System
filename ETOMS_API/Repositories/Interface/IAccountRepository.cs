using ETOMS_API.Models;
using static ETOMS_API.DTOs.Auth.AuthDtos;

namespace ETOMS_API.Repositories.Interface
{
    public interface IAccountRepository
    {
        Task<Account?> FindByIdAsync(int id, CancellationToken ct = default);
        Task<Account?> FindByEmailAsync(string email, CancellationToken ct = default);
        Task<Account?> FindByGoogleSubjectAsync(string subject, CancellationToken ct = default);
        Task<AccountStatusInfo?> GetStatusAsync(int id, CancellationToken ct = default);
        Task AddAsync(Account account, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
