namespace ETOMS_API.Utils
{
    public class JwtOptions
    {
        public const string Section = "Jwt";
        public string Key { get; set; } = "";
        public string Issuer { get; set; } = "";
        public string Audience { get; set; } = "";
        public int AccessMinutes { get; set; } = 15;
        public int SessionRefreshHours { get; set; } = 12;
        public int RefreshDays { get; set; } = 7;
    }
}
