namespace ETOMS_API.Services.Interface
{
    public interface IPasswordService
    {
        string Hash(string password);
        bool Verify(string password, string hash);
    }
}
