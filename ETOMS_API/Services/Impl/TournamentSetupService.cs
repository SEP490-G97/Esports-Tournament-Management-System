using System.Text.Json;
using ETOMS_API.DTOs.TournamentSetup;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl
{
    public class TournamentSetupService : ITournamentSetupService
    {
        private readonly EtomsDbContext _context;

        public TournamentSetupService(EtomsDbContext context)
        {
            _context = context;
        }

        // TOUR-01, TOUR-03, TOUR-04: Tạo bước 1 kèm Thể thức & Cấu hình Hybrid Stage
        public async Task<int> CreateStep1GeneralInfoAsync(int organizerAccountId, Step1GeneralInfoDto dto)
        {
            var game = await _context.Games.FindAsync(dto.GameId);
            if (game == null)
                throw new KeyNotFoundException("Tựa Game được chọn không tồn tại.");

            // Xử lý logic cấu hình Hybrid Stage (TOUR-04)
            string? hybridStageConfig = null;
            if (dto.Format == "HYBRID")
            {
                if (!dto.GroupCount.HasValue || dto.GroupCount.Value <= 0 || !dto.AdvancingPerGroup.HasValue || dto.AdvancingPerGroup.Value <= 0)
                {
                    throw new ArgumentException("Giải đấu Hybrid bắt buộc nhập số bảng đấu và số đội đi tiếp mỗi bảng.");
                }

                hybridStageConfig = JsonSerializer.Serialize(new
                {
                    GroupStage = new
                    {
                        GroupCount = dto.GroupCount.Value,
                        AdvancingPerGroup = dto.AdvancingPerGroup.Value
                    },
                    KnockoutStage = new
                    {
                        Format = "SINGLE_ELIMINATION"
                    }
                });
            }

            var tournament = new Tournament
            {
                Name = dto.Name,
                // Nếu là Hybrid, ghép chuỗi Json cấu hình vào cuối Description hoặc thuộc tính tùy chọn có sẵn
                Description = dto.Format == "HYBRID"
                    ? $"{dto.Description}\n[HybridConfig]: {hybridStageConfig}"
                    : dto.Description,
                BannerUrl = dto.BannerUrl,
                OrganizingUnit = dto.OrganizerName,
                Region = dto.Location,
                ContactInfo = dto.ContactInfo,
                GameId = dto.GameId,
                OrganizerAccountId = organizerAccountId,

                // TOUR-03: Thể thức & Tranh hạng ba
                Format = dto.Format,
                HasThirdPlaceMatch = dto.HasThirdPlaceMatch,

                // Tạm thời lưu JSON Hybrid vào BestOfPerRound nếu chưa có cột HybridStageConfig
                BestOfPerRound = hybridStageConfig,

                Status = "DRAFT",
                RulebookVersion = 1,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();
            return tournament.TournamentId;
        }

        // TOUR-02: Step 2 Timeline & Quy mô
        public async Task<bool> UpdateStep2TimelineAsync(int tournamentId, Step2TimelineDto dto)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            if (tournament == null || tournament.Status != "DRAFT")
                throw new InvalidOperationException("Giải đấu không tồn tại hoặc không ở trạng thái nháp.");

            if (dto.RegistrationOpenDate >= dto.RegistrationCloseDate)
                throw new ArgumentException("Thời gian mở đăng ký phải trước thời gian đóng đăng ký.");

            if (dto.RegistrationCloseDate >= dto.TournamentStartDate)
                throw new ArgumentException("Thời gian đóng đăng ký phải trước ngày bắt đầu thi đấu.");

            if (dto.TournamentStartDate >= dto.TournamentEndDate)
                throw new ArgumentException("Ngày bắt đầu thi đấu phải trước ngày kết thúc.");

            if (dto.MaxTeams <= 0 || (dto.MaxTeams & (dto.MaxTeams - 1)) != 0)
                throw new ArgumentException("Số lượng đội tối đa phải là số mũ của 2 (4, 8, 16, 32, 64...).");

            tournament.RegOpenAt = dto.RegistrationOpenDate;
            tournament.RegCloseAt = dto.RegistrationCloseDate;
            tournament.StartAt = dto.TournamentStartDate;
            tournament.EndAt = dto.TournamentEndDate;
            tournament.MaxTeams = dto.MaxTeams;

            await _context.SaveChangesAsync();
            return true;
        }

        // TOUR-05: Step 3 Cấu hình Best-of theo vòng
        public async Task<bool> UpdateStep3BoConfigAsync(int tournamentId, Step3BoConfigDto dto)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            if (tournament == null || tournament.Status != "DRAFT")
                throw new InvalidOperationException("Giải đấu không ở trạng thái hợp lệ để chỉnh sửa.");

            tournament.BestOfPerRound = JsonSerializer.Serialize(dto.RoundConfigs);
            await _context.SaveChangesAsync();
            return true;
        }

        // TOUR-06: Step 4 Lệ phí & Giải thưởng
        public async Task<bool> UpdateStep4FinanceAsync(int tournamentId, Step4FinanceDto dto)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            if (tournament == null || tournament.Status != "DRAFT")
                throw new InvalidOperationException("Giải đấu không ở trạng thái hợp lệ.");

            tournament.EntryFee = dto.EntryFee;
            tournament.BankAccountInfo = $"{dto.BankName} | STK: {dto.AccountNumber} | ChuTK: {dto.AccountName}";
            tournament.PrizeStructure = dto.PrizePoolDescription;

            await _context.SaveChangesAsync();
            return true;
        }

        // TOUR-07: Step 5 Yêu cầu hồ sơ & Công bố giải đấu
        public async Task<bool> FinalizeStep5PublishAsync(int tournamentId, Step5RequirementDto dto)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            if (tournament == null || tournament.Status != "DRAFT")
                throw new InvalidOperationException("Giải đấu không tồn tại hoặc đã được công bố.");

            tournament.RequireIdentityDocument = dto.RequireStudentCard;
            tournament.RequirePaymentProof = dto.RequirePaymentBill;
            tournament.RequireInGameName = dto.RequireIgnVerify;

            tournament.Status = "PUBLISHED";

            await _context.SaveChangesAsync();
            return true;
        }
    }
}