using ETOMS_API.Services.Interface;

namespace ETOMS_API.Services.Impl
{
    public class PasswordService : IPasswordService
    {
        public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

        public bool Verify(string password, string hash)
        {
            try { return BCrypt.Net.BCrypt.Verify(password, hash); }
            catch { return false; }  
        }
    }
}
