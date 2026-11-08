// Services/Impl/TournamentSetupService.cs
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

        // TOUR-01: Tạo giải - Step 1 Thông tin chung
        public async Task<int> CreateStep1GeneralInfoAsync(int organizerAccountId, Step1GeneralInfoDto dto)
        {
            var game = await _context.Games.FindAsync(dto.GameId);
            if (game == null)
                throw new KeyNotFoundException("Tựa Game được chọn không tồn tại.");

            var tournament = new Tournament
            {
                Name = dto.Name,
                Description = dto.Description,
                BannerUrl = dto.BannerUrl,
                OrganizingUnit = dto.OrganizerName,
                Region = dto.Location,
                ContactInfo = dto.ContactInfo,
                GameId = dto.GameId,
                OrganizerAccountId = organizerAccountId,
                Format = "SINGLE_ELIMINATION", // Thể thức mặc định
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

            // Validate mốc thời gian: Open < Close < Start < End
            if (dto.RegistrationOpenDate >= dto.RegistrationCloseDate)
                throw new ArgumentException("Thời gian mở đăng ký phải trước thời gian đóng đăng ký.");

            if (dto.RegistrationCloseDate >= dto.TournamentStartDate)
                throw new ArgumentException("Thời gian đóng đăng ký phải trước ngày bắt đầu thi đấu.");

            if (dto.TournamentStartDate >= dto.TournamentEndDate)
                throw new ArgumentException("Ngày bắt đầu thi đấu phải trước ngày kết thúc.");

            // Validate MaxTeams (Lũy thừa của 2: 4, 8, 16, 32, 64...)
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

            // Lưu danh sách cấu hình ván đấu dạng JSON vào trường BestOfPerRound
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

        // TOUR-07: Step 5 Yêu cầu hồ sơ & Công bố giải đấu (Publish)
        public async Task<bool> FinalizeStep5PublishAsync(int tournamentId, Step5RequirementDto dto)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            if (tournament == null || tournament.Status != "DRAFT")
                throw new InvalidOperationException("Giải đấu không tồn tại hoặc đã được công bố.");

            tournament.RequireStudentCard = dto.RequireStudentCard;
            tournament.RequirePaymentProof = dto.RequirePaymentBill;
            tournament.RequireInGameName = dto.RequireIgnVerify;

            // Chuyển trạng thái giải đấu sang PUBLISHED
            tournament.Status = "PUBLISHED";

            await _context.SaveChangesAsync();
            return true;
        }
    }
}