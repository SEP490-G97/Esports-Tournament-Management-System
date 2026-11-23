using System.Text.Json;
using ETOMS_API.DTOs.TournamentSetup;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
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
            var v = await ValidateStep1Async(organizerAccountId, dto);
            // Mốc tạm: mở đăng ký từ ngày mai 1 tuần, khai mạc sau đó 2 ngày (bước 2 sẽ đặt lại).
            var today = DateTime.UtcNow.Date;
            var tournament = new Tournament
            {
                OrganizerAccountId = organizerAccountId,
                BannerUrl = string.IsNullOrWhiteSpace(dto.BannerUrl) ? null : dto.BannerUrl.Trim(),
                Region = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim(),
                ContactInfo = string.IsNullOrWhiteSpace(dto.ContactInfo) ? null : dto.ContactInfo.Trim(),
                MaxTeams = v.Format == "HYBRID" ? NextPowerOfTwo(Math.Max(16, v.GroupCount!.Value * 2)) : 16,
                RegOpenAt = today.AddDays(1),
                RegCloseAt = today.AddDays(8),
                StartAt = today.AddDays(10),
                Status = "DRAFT",
                RulebookVersion = 1,
                CreatedAt = DateTime.UtcNow,
                // Lược đồ DEFAULT 1 cho 4 cột này, nhưng EF ghi giá trị thật (ValueGeneratedNever, PendingTables) — không gán
                // thì giải tạo từ 18/11 thành "thi đấu trực tiếp" không có địa điểm (lỗi phát hiện 22/11). Bước 5 đặt lại 3 cột Require*.
                IsOnline = true,
                RequireInGameName = true,
                RequirePaymentProof = true,
                RequireIdentityDocument = true
            };
            ApplyStep1(tournament, v, gameChanged: true);
            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();
            return tournament.TournamentId;
        }

        // Quay lại bước 1 của trình tạo giải: SỬA bản nháp đã tạo (không sinh thêm giải nháp mới).
        public async Task<bool> UpdateStep1GeneralInfoAsync(int tournamentId, int organizerAccountId, Step1GeneralInfoDto dto)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            if (tournament == null || tournament.Status != "DRAFT")
                throw new ArgumentException("Giải đấu không tồn tại hoặc đã công bố — sửa ở trang Sửa thông tin.");
            var v = await ValidateStep1Async(tournament.OrganizerAccountId, dto);
            var gameChanged = tournament.GameId != v.Game.GameId;
            ApplyStep1(tournament, v, gameChanged);
            if (v.Format == "HYBRID" && tournament.MaxTeams < v.GroupCount!.Value * 2)
                tournament.MaxTeams = NextPowerOfTwo(v.GroupCount.Value * 2);
            await _context.SaveChangesAsync();
            return true;
        }

        private sealed record Step1Values(string Name, string Format, Game Game, int? GroupCount, int? AdvancePerGroup,
            string? Description, bool HasThirdPlace, string OrganizingUnit, int? OrganizationId);

        private static void ApplyStep1(Tournament t, Step1Values v, bool gameChanged)
        {
            t.Name = v.Name;
            t.Description = v.Description;
            t.OrganizingUnit = v.OrganizingUnit;
            t.OrganizationId = v.OrganizationId;
            t.GameId = v.Game.GameId;
            t.Format = v.Format;
            t.HasThirdPlaceMatch = v.HasThirdPlace;
            t.GroupCount = v.GroupCount;
            t.AdvancePerGroup = v.AdvancePerGroup;
            if (gameChanged)
            {
                // Đội hình theo luật của tựa game mới.
                t.StarterCount = Math.Max(1, v.Game.MinStarters);
                t.MaxSubstitutes = Math.Max(0, v.Game.MaxSubstitutes);
            }
        }

        private async Task<Step1Values> ValidateStep1Async(int organizerAccountId, Step1GeneralInfoDto dto)
        {
            var name = (dto.Name ?? "").Trim();
            if (name.Length < 3 || name.Length > 200)
                throw new ArgumentException("Tên giải đấu phải từ 3 đến 200 ký tự.");

            var format = (dto.Format ?? "").Trim().ToUpperInvariant();
            if (!Formats.Contains(format))
                throw new ArgumentException("Thể thức giải đấu không hợp lệ.");

            var game = await _context.Games.FindAsync(dto.GameId);
            if (game == null || game.Status != "ACTIVE")
                throw new ArgumentException("Tựa game được chọn không tồn tại hoặc đã ngừng hoạt động.");

            int? groupCount = null, advancePerGroup = null;
            if (format == "HYBRID")
            {
                // Góp ý nhóm 18/11: số bảng là lũy thừa của 2 (2, 4, 8, 16) để cây knockout sau vòng bảng cân.
                if (dto.GroupCount is not (2 or 4 or 8 or 16))
                    throw new ArgumentException("Số bảng đấu phải là lũy thừa của 2: 2, 4, 8 hoặc 16.");
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
            // Hướng 1 (nhóm chốt 17/11): Ban tổ chức là một TỔ CHỨC — giải luôn đứng tên một đơn vị tổ chức đã duyệt mà
            // tài khoản là Chủ nhiệm hoặc Điều hành giải (Tournament.OrganizationID). Tài khoản chưa thuộc đơn vị nào
            // thì phải lập đơn vị (chờ Admin duyệt) hoặc được một đơn vị mời vào trước. Không gõ tay tên đơn vị.
            var identity = await GetOrganizerIdentityAsync(organizerAccountId);
            if (identity.Organizations.Count == 0)
                throw new ArgumentException("Bạn chưa thuộc đơn vị tổ chức nào đã được duyệt (với vai trò Chủ nhiệm hoặc Điều hành giải). Hãy lập đơn vị ở trang Tổ chức hoặc nhờ chủ nhiệm một đơn vị mời bạn.");
            var org = dto.OrganizationId is int orgId
                ? identity.Organizations.FirstOrDefault(o => o.OrganizationId == orgId)
                  ?? throw new ArgumentException("Bạn chỉ chọn được đơn vị tổ chức đã duyệt mà bạn là chủ nhiệm hoặc người điều hành giải.")
                : identity.Organizations.Count == 1
                    ? identity.Organizations[0]
                    : throw new ArgumentException("Hãy chọn đơn vị tổ chức đứng tên giải.");
            string organizingUnit = org.Name;
            int? organizationId = org.OrganizationId;
            if (organizingUnit.Length > 200) organizingUnit = organizingUnit[..200];
            if (description.Length > 4000) throw new ArgumentException("Mô tả giải tối đa 4000 ký tự.");

            return new Step1Values(name, format, game, groupCount, advancePerGroup,
                description.Length == 0 ? null : description,
                // Tranh hạng Ba chỉ có ở loại trực tiếp / vòng bảng rồi loại trực tiếp (loại kép: hạng Ba là đội thua chung kết nhánh thua).
                format is "SINGLE_ELIMINATION" or "HYBRID" && dto.HasThirdPlaceMatch,
                organizingUnit, organizationId);
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

            // Góp ý nhóm 18/11: cả 4 mốc phải từ hiện tại trở đi (cho lệch 5 phút vì người dùng chọn giờ rồi mới bấm lưu).
            var earliest = DateTime.UtcNow.AddMinutes(-5);
            if (open < earliest)
                throw new ArgumentException("Thời gian mở đăng ký phải từ hiện tại trở đi, không chọn ngày đã qua.");
            if (close <= DateTime.UtcNow || start <= DateTime.UtcNow || end <= DateTime.UtcNow)
                throw new ArgumentException("Thời gian đóng đăng ký, khai mạc và kết thúc phải ở tương lai.");
            if (ETOMS_API.Utils.TournamentTimeline.Check(open, close, start, end) is { } bad)
                throw new ArgumentException(bad.Message);

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

            // Ngân hàng phải nằm trong danh sách, số tài khoản 6–20 chữ số, chủ tài khoản IN HOA không dấu.
            var error = ETOMS_API.Utils.BankAccount.TryCompose(dto.EntryFee, dto.BankName, dto.AccountNumber, dto.AccountName, out var bankInfo);
            if (error is not null) throw new ArgumentException(error);

            tournament.EntryFee = dto.EntryFee;
            tournament.BankAccountInfo = bankInfo;
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
            tournament.RequirePaymentProof = dto.RequirePaymentBill && tournament.EntryFee > 0;   // giải miễn phí không có biên lai để nộp
            tournament.RequireInGameName = dto.RequireIgnVerify;

            tournament.Status = "PUBLISHED";
            // Chưa chọn bộ luật thì gắn bộ luật mặc định của game (để đội xếp được vị trí trong đội hình).
            if (!await _context.TournamentRulesets.AnyAsync(tr => tr.TournamentId == tournamentId))
            {
                var pick = DefaultRuleset.Pick(await _context.GameRulesets.Where(r => r.GameId == tournament.GameId).ToListAsync(), tournament);
                if (pick is not null)
                    _context.TournamentRulesets.Add(new TournamentRuleset { TournamentId = tournamentId, GameRulesetId = pick.GameRulesetId, CreatedAt = DateTime.UtcNow });
            }

            await _context.SaveChangesAsync();
            return true;
        }

        // Đơn vị tổ chức của tài khoản tạo giải: tên tài khoản + các đơn vị tổ chức đã duyệt (ACTIVE) mà tài khoản
        // đang là chủ nhiệm (CHAIR) hoặc người điều hành giải (TOURNAMENT_OPERATOR).
        public async Task<OrganizerIdentityDto> GetOrganizerIdentityAsync(int accountId)
        {
            var name = await _context.Accounts.Where(a => a.AccountId == accountId).Select(a => a.FullName).FirstOrDefaultAsync() ?? string.Empty;
            var orgs = await _context.OrganizationMembers
                .Where(m => m.AccountId == accountId && m.Status == "ACTIVE"
                    && (m.Role == "CHAIR" || m.Role == "TOURNAMENT_OPERATOR")
                    && m.Organization.Status == "ACTIVE")
                .OrderBy(m => m.Organization.Name)
                .Select(m => new OrganizerOrganizationOption { OrganizationId = m.OrganizationId, Name = m.Organization.Name })
                .ToListAsync();
            return new OrganizerIdentityDto { AccountName = name.Trim(), Organizations = orgs };
        }

        public async Task<bool> SetBannerAsync(int tournamentId, string bannerUrl)
        {
            var t = await _context.Tournaments.FindAsync(tournamentId);
            if (t is null || t.Status is "COMPLETED" or "CANCELLED") return false;
            t.BannerUrl = bannerUrl;
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