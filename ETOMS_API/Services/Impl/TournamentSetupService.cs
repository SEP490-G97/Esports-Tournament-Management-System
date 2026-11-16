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


        private static readonly string[] Formats = { "SINGLE_ELIMINATION", "DOUBLE_ELIMINATION", "ROUND_ROBIN", "HYBRID" };

        private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

        private static int NextPowerOfTwo(int n)
        {
            var p = 1;
            while (p < n) p <<= 1;
            return p;
        }

        // TOUR-01, TOUR-03, TOUR-04: Tạo bước 1 kèm Thể thức & Cấu hình Hybrid Stage.
        // Bảng Tournament bắt buộc có số đội, số tuyển thủ và các mốc thời gian (CHECK trong DB), nhưng
        // wizard chỉ hỏi những thứ này ở bước 2 — nên bước 1 ghi giá trị tạm hợp lệ, bước 2 ghi đè.
        public async Task<int> CreateStep1GeneralInfoAsync(int organizerAccountId, Step1GeneralInfoDto dto)
        {
            var name = (dto.Name ?? "").Trim();
            if (name.Length < 3 || name.Length > 200)
                throw new ArgumentException("Tên giải đấu phải từ 3 đến 200 ký tự.");

            var format = (dto.Format ?? "").Trim().ToUpperInvariant();
            if (!Formats.Contains(format))
                throw new ArgumentException("Thể thức giải đấu không hợp lệ.");
            if (format == "DOUBLE_ELIMINATION")
                throw new ArgumentException("Thể thức Loại kép (Double Elimination) chưa hỗ trợ sinh nhánh đấu — vui lòng chọn thể thức khác.");

            var game = await _context.Games.FindAsync(dto.GameId);
            if (game == null || game.Status != "ACTIVE")
                throw new ArgumentException("Tựa game được chọn không tồn tại hoặc đã ngừng hoạt động.");

            int? groupCount = null, advancePerGroup = null;
            if (format == "HYBRID")
            {
                if (dto.GroupCount is not (>= 2 and <= 16))
                    throw new ArgumentException("Số bảng đấu phải từ 2 đến 16.");
                if (dto.AdvancingPerGroup is not (>= 1 and <= 8))
                    throw new ArgumentException("Số đội đi tiếp mỗi bảng phải từ 1 đến 8.");
                groupCount = dto.GroupCount;
                advancePerGroup = dto.AdvancingPerGroup;
            }
            else if (format == "ROUND_ROBIN")
            {
                groupCount = 1;                       // vòng tròn một bảng duy nhất
            }

            var description = (dto.Description ?? "").Trim();
            var organizingUnit = (dto.OrganizerName ?? "").Trim();
            if (organizingUnit.Length > 200) throw new ArgumentException("Tên đơn vị tổ chức tối đa 200 ký tự.");

            // Mốc tạm: mở đăng ký từ ngày mai 1 tuần, khai mạc sau đó 2 ngày (bước 2 sẽ đặt lại).
            var today = DateTime.UtcNow.Date;
            var tournament = new Tournament
            {
                Name = name,
                Description = description.Length == 0 ? null : description,
                BannerUrl = string.IsNullOrWhiteSpace(dto.BannerUrl) ? null : dto.BannerUrl.Trim(),
                OrganizingUnit = organizingUnit.Length == 0 ? null : organizingUnit,
                Region = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim(),
                ContactInfo = string.IsNullOrWhiteSpace(dto.ContactInfo) ? null : dto.ContactInfo.Trim(),
                GameId = game.GameId,
                OrganizerAccountId = organizerAccountId,

                Format = format,
                HasThirdPlaceMatch = format != "ROUND_ROBIN" && dto.HasThirdPlaceMatch,
                GroupCount = groupCount,
                AdvancePerGroup = advancePerGroup,

                MaxTeams = format == "HYBRID" ? NextPowerOfTwo(Math.Max(16, groupCount!.Value * 2)) : 16,
                StarterCount = Math.Max(1, game.MinStarters),
                MaxSubstitutes = Math.Max(0, game.MaxSubstitutes),
                RegOpenAt = today.AddDays(1),
                RegCloseAt = today.AddDays(8),
                StartAt = today.AddDays(10),

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
                throw new ArgumentException("Giải đấu không tồn tại hoặc không ở trạng thái nháp.");

            var open = DateTime.SpecifyKind(dto.RegistrationOpenDate.ToUniversalTime(), DateTimeKind.Utc);
            var close = DateTime.SpecifyKind(dto.RegistrationCloseDate.ToUniversalTime(), DateTimeKind.Utc);
            var start = DateTime.SpecifyKind(dto.TournamentStartDate.ToUniversalTime(), DateTimeKind.Utc);
            var end = DateTime.SpecifyKind(dto.TournamentEndDate.ToUniversalTime(), DateTimeKind.Utc);

            if (close <= DateTime.UtcNow)
                throw new ArgumentException("Thời gian đóng đăng ký phải ở tương lai.");
            if (open >= close)
                throw new ArgumentException("Thời gian mở đăng ký phải trước thời gian đóng đăng ký.");
            if (close > start)
                throw new ArgumentException("Thời gian đóng đăng ký phải trước hoặc bằng ngày bắt đầu thi đấu.");
            if (start >= end)
                throw new ArgumentException("Ngày bắt đầu thi đấu phải trước ngày kết thúc.");

            if (dto.MaxTeams < 4 || dto.MaxTeams > 128 || !IsPowerOfTwo(dto.MaxTeams))
                throw new ArgumentException("Số đội tối đa phải là lũy thừa của 2, từ 4 đến 128 (4, 8, 16, 32, 64, 128).");

            if (tournament.Format == "HYBRID" && tournament.GroupCount is int groups && tournament.AdvancePerGroup is int advance)
            {
                if (dto.MaxTeams < groups * 2)
                    throw new ArgumentException($"Với {groups} bảng, mỗi bảng cần ít nhất 2 đội — số đội tối đa phải từ {groups * 2} trở lên.");
                if (groups * advance > dto.MaxTeams / 2)
                    throw new ArgumentException($"{groups} bảng × {advance} đội đi tiếp = {groups * advance} đội vào knockout, quá nhiều so với {dto.MaxTeams} đội tham dự.");
            }

            tournament.RegOpenAt = open;
            tournament.RegCloseAt = close;
            tournament.StartAt = start;
            tournament.EndAt = end;
            tournament.MaxTeams = dto.MaxTeams;

            await _context.SaveChangesAsync();
            return true;
        }

        // TOUR-05: Step 3 Cấu hình Best-of theo vòng — lưu theo dạng "R1:1,R2:3,F:5" mà phần sinh
        // nhánh/chuyển vòng đọc (StageTransitionService.BestOfFor): F = chung kết, Rn = vòng n của knockout.
        public async Task<bool> UpdateStep3BoConfigAsync(int tournamentId, Step3BoConfigDto dto)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            if (tournament == null || tournament.Status != "DRAFT")
                throw new ArgumentException("Giải đấu không ở trạng thái hợp lệ để chỉnh sửa.");

            if (dto.RoundConfigs.Any(r => r.BestOf is not (1 or 3 or 5 or 7)))
                throw new ArgumentException("Số ván mỗi trận (Best of) chỉ được là 1, 3, 5 hoặc 7.");

            if (tournament.Format == "ROUND_ROBIN")
            {
                tournament.BestOfPerRound = null;      // vòng tròn không có cây knockout
                await _context.SaveChangesAsync();
                return true;
            }

            var knockoutTeams = tournament.Format == "HYBRID" && tournament.GroupCount is int g && tournament.AdvancePerGroup is int a
                ? g * a : tournament.MaxTeams;
            var rounds = (int)Math.Ceiling(Math.Log2(Math.Max(2, knockoutTeams)));

            var parts = new List<string>();
            foreach (var r in dto.RoundConfigs)
            {
                var label = (r.RoundName ?? "").Trim().ToLowerInvariant();
                string? key = label switch
                {
                    "chung kết" => "F",
                    "bán kết" when rounds >= 2 => "R" + (rounds - 1),
                    "tứ kết" when rounds >= 3 => "R" + (rounds - 2),
                    _ => null
                };
                if (key != null) parts.Add($"{key}:{r.BestOf}");
            }
            tournament.BestOfPerRound = parts.Count == 0 ? null : string.Join(",", parts);

            await _context.SaveChangesAsync();
            return true;
        }

        // TOUR-06: Step 4 Lệ phí & Giải thưởng
        public async Task<bool> UpdateStep4FinanceAsync(int tournamentId, Step4FinanceDto dto)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            if (tournament == null || tournament.Status != "DRAFT")
                throw new ArgumentException("Giải đấu không ở trạng thái hợp lệ.");

            if (dto.EntryFee < 0 || dto.EntryFee > 100_000_000)
                throw new ArgumentException("Lệ phí tham gia phải từ 0 đến 100.000.000 VNĐ.");

            var bank = (dto.BankName ?? "").Trim();
            var number = (dto.AccountNumber ?? "").Trim();
            var holder = (dto.AccountName ?? "").Trim();
            if (dto.EntryFee > 0 && (bank.Length == 0 || number.Length == 0 || holder.Length == 0))
                throw new ArgumentException("Giải có lệ phí phải nhập đủ ngân hàng, số tài khoản và chủ tài khoản nhận phí.");
            if (number.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(number, "^[0-9]{6,20}$"))
                throw new ArgumentException("Số tài khoản chỉ gồm chữ số, dài 6–20 số.");
            if (bank.Length > 100 || holder.Length > 100)
                throw new ArgumentException("Tên ngân hàng và chủ tài khoản tối đa 100 ký tự.");

            tournament.EntryFee = dto.EntryFee;
            tournament.BankAccountInfo = number.Length == 0 ? null : $"{bank} | STK: {number} | ChuTK: {holder}";
            tournament.PrizeStructure = string.IsNullOrWhiteSpace(dto.PrizePoolDescription) ? null : dto.PrizePoolDescription.Trim();

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

        public async Task<bool> IsOrganizerOrAdminAsync(int accountId)
        {
            var account = await _context.Accounts.FindAsync(accountId);
            return account is not null && (account.Role == "ORGANIZER" || account.Role == "ADMIN");
        }

        public async Task<bool?> CanManageAsync(int tournamentId, int accountId)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            if (tournament is null) return null;
            if (tournament.OrganizerAccountId == accountId) return true;
            var account = await _context.Accounts.FindAsync(accountId);
            return account is not null && account.Role == "ADMIN";
        }
    }
}