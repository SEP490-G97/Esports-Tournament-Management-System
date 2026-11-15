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
        // Số đội vào cây knockout và số vòng của cây (vòng N = chung kết). Vòng tròn: 0 vòng.
        private static (int Teams, int Rounds) KnockoutShape(Tournament t)
        {
            if (t.Format == "ROUND_ROBIN") return (0, 0);
            var teams = t.Format == "HYBRID" && t.GroupCount is int g && t.AdvancePerGroup is int a ? g * a : t.MaxTeams;
            return (teams, (int)Math.Ceiling(Math.Log2(Math.Max(2, teams))));
        }

        // Chưa cấu hình thì gợi ý chuẩn phổ biến: các vòng đầu BO1, bán kết BO3, chung kết BO5.
        private static int SuggestedBestOf(int round, int rounds) =>
            round == rounds ? 5 : round == rounds - 1 ? 3 : 1;

        public async Task<Step3BoBoardDto> GetStep3BoConfigAsync(int tournamentId)
        {
            var t = await _context.Tournaments.AsNoTracking().FirstOrDefaultAsync(x => x.TournamentId == tournamentId)
                ?? throw new KeyNotFoundException("Không tìm thấy giải đấu.");
            var (teams, rounds) = KnockoutShape(t);
            var board = new Step3BoBoardDto { Format = t.Format, KnockoutTeams = teams };
            if (rounds == 0)
            {
                board.Note = "Thể thức vòng tròn không có cây loại trực tiếp nên không cấu hình BO theo vòng.";
                return board;
            }
            if (t.Format == "DOUBLE_ELIMINATION")
                board.Note = "Loại kép: cấu hình áp dụng theo vòng của nhánh thắng; trận chung kết tổng dùng BO của chung kết.";
            var configured = !string.IsNullOrWhiteSpace(t.BestOfPerRound);
            for (var r = 1; r <= rounds; r++)
                board.Rounds.Add(new Step3BoRoundDto
                {
                    RoundNo = r,
                    Label = BracketLabels.KnockoutRound(rounds, r) + (r < rounds - 2 ? $" ({1 << (rounds - r + 1)} đội)" : ""),
                    BestOf = configured ? StageTransitionService.BestOfFor(t.BestOfPerRound, r, rounds) : SuggestedBestOf(r, rounds)
                });
            return board;
        }

        public async Task<bool> UpdateStep3BoConfigAsync(int tournamentId, Step3BoConfigDto dto)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            if (tournament == null || tournament.Status != "DRAFT")
                throw new ArgumentException("Giải đấu không ở trạng thái hợp lệ để chỉnh sửa.");

            if (dto?.RoundConfigs == null)
                throw new ArgumentException("Thiếu cấu hình Best of.");
            if (dto.RoundConfigs.Any(r => r.BestOf is not (1 or 3 or 5 or 7)))
                throw new ArgumentException("Số ván mỗi trận (Best of) chỉ được là 1, 3, 5 hoặc 7.");

            var (_, rounds) = KnockoutShape(tournament);
            if (rounds == 0)
            {
                tournament.BestOfPerRound = null;      // vòng tròn không có cây knockout
                await _context.SaveChangesAsync();
                return true;
            }

            var byRound = new Dictionary<int, int>();
            foreach (var r in dto.RoundConfigs)
            {
                int? round = r.RoundNo;
                if (round is null)
                {
                    // Tương thích bản cũ gửi theo tên vòng.
                    var label = (r.RoundName ?? "").Trim().ToLowerInvariant();
                    round = label switch
                    {
                        "chung kết" => rounds,
                        "bán kết" when rounds >= 2 => rounds - 1,
                        "tứ kết" when rounds >= 3 => rounds - 2,
                        _ => null
                    };
                    if (round is null) continue;
                }
                if (round < 1 || round > rounds)
                    throw new ArgumentException($"Vòng {round} không có trong nhánh của giải (giải có {rounds} vòng knockout).");
                if (!byRound.TryAdd(round.Value, r.BestOf))
                    throw new ArgumentException($"Vòng {BracketLabels.KnockoutRound(rounds, round)} bị khai báo hai lần.");
            }
            if (dto.RoundConfigs.Any(r => r.RoundNo.HasValue) && byRound.Count != rounds)
                throw new ArgumentException("Hãy chọn Best of cho đủ tất cả các vòng.");

            var parts = byRound.OrderBy(kv => kv.Key)
                .Select(kv => (kv.Key == rounds ? "F" : "R" + kv.Key) + ":" + kv.Value).ToList();
            var value = parts.Count == 0 ? null : string.Join(",", parts);
            if (value is { Length: > 100 })
                throw new ArgumentException("Cấu hình Best of quá dài.");
            tournament.BestOfPerRound = value;

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