// DTOs/RecruitmentDTOs.cs
using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Recruitment
{
    public class CreateLFPPostDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn đội tuyển.")]
        public int TeamId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập vị trí cần tuyển.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Vị trí cần tuyển phải từ 2 đến 50 ký tự.")]
        public string RequiredRole { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập rank yêu cầu.")]
        [StringLength(50, ErrorMessage = "Rank yêu cầu tối đa 50 ký tự.")]
        public string RequiredRank { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
        public string Description { get; set; } = string.Empty;
    }

    public class CreateLFTPostDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tựa game.")]
        public int GameId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập vị trí sở trường.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Vị trí sở trường phải từ 2 đến 50 ký tự.")]
        public string PreferredRole { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập rank hiện tại.")]
        [StringLength(50, ErrorMessage = "Rank hiện tại tối đa 50 ký tự.")]
        public string CurrentRank { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
        public string Description { get; set; } = string.Empty;
    }

    public class RecruitmentBoardQueryDto
    {
        public string PostType { get; set; } = "LFP"; // mã ngắn LFP hoặc LFT, service tự đổi sang giá trị trong DB
        public int? GameId { get; set; }
        public string? Role { get; set; }
        public string? Rank { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class ApplyLFPPostDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Không xác định được tin tuyển.")]
        public int PostId { get; set; }

        [StringLength(500, ErrorMessage = "Lời nhắn tối đa 500 ký tự.")]
        public string Message { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Liên kết highlight tối đa 500 ký tự.")]
        [Url(ErrorMessage = "Liên kết highlight không hợp lệ.")]
        public string? HighlightUrl { get; set; }
    }

    public class ReviewApplicationDto
    {
        public bool IsAccepted { get; set; }

        [StringLength(500, ErrorMessage = "Lý do từ chối tối đa 500 ký tự.")]
        public string? RejectReason { get; set; }
    }

    public class InviteFreeAgentDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn đội tuyển.")]
        public int TeamId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tuyển thủ được mời.")]
        public int PlayerAccountId { get; set; }

        [StringLength(500, ErrorMessage = "Lời nhắn tối đa 500 ký tự.")]
        public string Message { get; set; } = string.Empty;
    }
}
