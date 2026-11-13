using ETOMS_API.DTOs.Auth;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System.Security.Principal;
using static ETOMS_API.DTOs.Auth.AuthDtos;

namespace ETOMS_API.Repositories.Impl
{
    public class AccountRepository(EtomsDbContext db) : IAccountRepository
    {
        public async Task AddAsync(Account account, CancellationToken ct = default) =>
                await db.Accounts.AddAsync(account, ct);

        public Task<Account?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        db.Accounts.FirstOrDefaultAsync(a => a.Email == email, ct);

        public Task<Account?> FindByGoogleSubjectAsync(string subject, CancellationToken ct = default) =>
        db.Accounts.FirstOrDefaultAsync(a => a.GoogleSubjectId == subject, ct);

        public Task<Account?> FindByIdAsync(int id, CancellationToken ct = default) =>
         db.Accounts.FirstOrDefaultAsync(a => a.AccountId == id, ct);

        public Task<AccountStatusInfo?> GetStatusAsync(int id, CancellationToken ct = default) =>
       db.Accounts.AsNoTracking()
           .Where(a => a.AccountId == id)
           .Select(a => new AccountStatusInfo(a.Status, a.BanReason))
           .FirstOrDefaultAsync(ct);

        public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
    }
}
