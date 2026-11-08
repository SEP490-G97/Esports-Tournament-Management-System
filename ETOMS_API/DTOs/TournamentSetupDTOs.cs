// DTOs/TournamentSetupDTOs.cs
namespace ETOMS_API.DTOs.TournamentSetup
{
    public class Step1GeneralInfoDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? BannerUrl { get; set; }
        public string OrganizerName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string ContactInfo { get; set; } = string.Empty;
        public int GameId { get; set; }
    }

    public class Step2TimelineDto
    {
        public DateTime RegistrationOpenDate { get; set; }
        public DateTime RegistrationCloseDate { get; set; }
        public DateTime TournamentStartDate { get; set; }
        public DateTime TournamentEndDate { get; set; }
        public int MaxTeams { get; set; }
    }

    public class RoundBoConfigDto
    {
        public int StageOrder { get; set; }
        public string RoundName { get; set; } = string.Empty;
        public int BestOf { get; set; } // 1, 3, 5
    }

    public class Step3BoConfigDto
    {
        public List<RoundBoConfigDto> RoundConfigs { get; set; } = new();
    }

    public class Step4FinanceDto
    {
        public decimal EntryFee { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string? QrCodeUrl { get; set; }
        public string PrizePoolDescription { get; set; } = string.Empty;
    }

    public class Step5RequirementDto
    {
        public bool RequireStudentCard { get; set; }
        public bool RequirePaymentBill { get; set; }
        public bool RequireIgnVerify { get; set; }
        public List<string> AdditionalChecklistItems { get; set; } = new();
    }
}