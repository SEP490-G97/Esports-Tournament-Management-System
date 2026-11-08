// DTOs/RecruitmentDTOs.cs
namespace ETOMS_API.DTOs.Recruitment
{
    public class CreateLFPPostDto
    {
        public int TeamId { get; set; }
        public string RequiredRole { get; set; } = string.Empty;
        public string RequiredRank { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class CreateLFTPostDto
    {
        public int GameId { get; set; }
        public string PreferredRole { get; set; } = string.Empty;
        public string CurrentRank { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class RecruitmentBoardQueryDto
    {
        public string PostType { get; set; } = "LFP"; // LFP hoặc LFT
        public int? GameId { get; set; }
        public string? Role { get; set; }
        public string? Rank { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class ApplyLFPPostDto
    {
        public int PostId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? HighlightUrl { get; set; }
    }

    public class ReviewApplicationDto
    {
        public bool IsAccepted { get; set; }
        public string? RejectReason { get; set; }
    }

    public class InviteFreeAgentDto
    {
        public int TeamId { get; set; }
        public int PlayerAccountId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}