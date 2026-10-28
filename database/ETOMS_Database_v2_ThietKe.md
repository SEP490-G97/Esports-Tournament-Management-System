# ETOMS — Cấu trúc Database rút gọn (v2)

**Phạm vi:** bám theo `UC Update.docx` bản 27/10/2026 — **46 use case (UC-01 … UC-46)**.
**Tổng: 25 bảng.** Mọi bảng có khóa chính độc lập. Không có khóa chính ghép, không có khóa ngoại ghép.

> **Lưu ý kỹ thuật:** SQL Server **không có kiểu `ENUM`**. Mọi cột ghi `(ENUM)` dưới đây hiện thực bằng
> `VARCHAR(n) NOT NULL` + `CHECK (Cot IN ('A','B',...))`. Cách này giữ đúng tinh thần ENUM (không cần bảng
> danh mục) mà vẫn được cơ sở dữ liệu cưỡng chế.

---

## Module A — Tài khoản & Hồ sơ (UC-01, 02, 03, 40, 43)

### Account
- **Các trường chính:** `AccountID (PK)`, `Email`, `PasswordHash`, `GoogleSubjectId`, `FullName`, `AvatarUrl`, `DateOfBirth`, `Phone`, `School`, `SocialLinks`, `Role (ENUM: PLAYER | ORGANIZER | ADMIN)`, `Status (ENUM: PENDING_VERIFY | ACTIVE | BANNED)`, `BanReason`, `NotifyInApp`, `NotifyEmail`, `EmailVerifiedAt`, `CreatedAt`
- **Mục đích:** Một dòng cho một người dùng, kèm vai trò và tuỳ chọn nhận thông báo — thay cho ba bảng `Role`, `UserRole`, `NotificationPreference`.

### AuthToken
- **Các trường chính:** `AuthTokenID (PK)`, `AccountID (FK)`, `TokenHash`, `TokenType (ENUM: REFRESH | EMAIL_VERIFY | PASSWORD_RESET)`, `ExpiresAt`, `UsedAt`, `CreatedAt`
- **Mục đích:** Mọi loại mã có thời hạn của một tài khoản — thay cho hai bảng `RefreshToken` và `VerificationToken`.

### PlayerCredential
- **Các trường chính:** `PlayerCredentialID (PK)`, `AccountID (FK)`, `GameID (FK, null khi là giấy tờ)`, `CredentialType (ENUM: IN_GAME_NAME | STUDENT_CARD | CITIZEN_ID)`, `Value`, `FileUrl`, `VerifiedAt`
- **Mục đích:** Tên trong game theo từng tựa game và ảnh giấy tờ định danh mà ban tổ chức sẽ đối chiếu lúc duyệt đơn.

---

## Module B — Danh mục tựa game (UC-43)

### Game
- **Các trường chính:** `GameID (PK)`, `Title`, `ShortCode`, `LogoUrl`, `BannerUrl`, `Status (ENUM: ACTIVE | INACTIVE)`, `MinStarters`, `MaxStarters`, `MaxSubstitutes`, `DefaultFormat (ENUM: SINGLE_ELIMINATION | DOUBLE_ELIMINATION | ROUND_ROBIN | HYBRID)`, `AllowedEvidenceTypes`, `MaxEvidenceFileSizeMB`
- **Mục đích:** Tựa game kèm luôn quy tắc gốc của nó (số người, dự bị, loại file minh chứng) — thay cho bảng `GameRule` tách riêng.

---

## Module C — Đội tuyển & Tuyển quân (UC-04, 05, 37, 44)

### Team
- **Các trường chính:** `TeamID (PK)`, `GameID (FK)`, `CaptainAccountID (FK)`, `Name`, `Tag`, `LogoUrl`, `InviteCode`, `InviteExpiresAt`, `Status (ENUM: ACTIVE | DISBANDED)`, `CreatedAt`
- **Mục đích:** Một đội thuộc đúng một tựa game, kèm mã mời đang hiệu lực — thay cho bảng `InviteCode` riêng.

### TeamMember
- **Các trường chính:** `TeamMemberID (PK)`, `TeamID (FK)`, `AccountID (FK)`, `RecruitmentPostID (FK, null)`, `Slot (ENUM: STARTER | SUBSTITUTE)`, `Direction (ENUM: JOIN_BY_CODE | APPLIED | INVITED)`, `Status (ENUM: PENDING | ACTIVE | REJECTED | LEFT | REMOVED)`, `Message`, `JoinedAt`, `LeftAt`
- **Mục đích:** Vừa là danh sách thành viên, vừa là đơn ứng tuyển và lời mời đang chờ — thay cho bảng `JoinApplication` riêng.

### RecruitmentPost
- **Các trường chính:** `RecruitmentPostID (PK)`, `GameID (FK)`, `TeamID (FK, null khi là tin tìm đội)`, `AuthorAccountID (FK)`, `PostType (ENUM: LOOKING_FOR_PLAYER | LOOKING_FOR_TEAM)`, `Title`, `Content`, `HighlightUrl`, `Status (ENUM: OPEN | CLOSED)`, `CreatedAt`
- **Mục đích:** Bảng tin tuyển quân dùng chung cho cả tin tuyển người và tin tìm đội.

---

## Module D — Giải đấu & Tài trợ (UC-12, 13, 14, 36, 42)

### Tournament
- **Các trường chính:** `TournamentID (PK)`, `GameID (FK)`, `OrganizerAccountID (FK)`, `Name`, `Description`, `BannerUrl`, `OrganizingUnit`, `Region`, `ContactInfo`, `Venue`, `Format (ENUM)`, `HasThirdPlaceMatch`, `MaxTeams`, `StarterCount`, `MaxSubstitutes`, `RegOpenAt`, `RegCloseAt`, `StartAt`, `EndAt`, `EntryFee`, `BankAccountInfo`, `PrizeStructure`, `RequireStudentCard`, `RequirePaymentProof`, `RequireInGameName`, `TieBreakerOrder`, `BestOfPerRound`, `PenaltyThreshold`, `RulebookContent`, `RulebookVersion`, `StreamUrl`, `Status (ENUM: DRAFT | PUBLISHED | ONGOING | COMPLETED | CANCELLED)`, `CancelReason`, `CreatedAt`
- **Mục đích:** Toàn bộ cấu hình của một giải, kể cả thể lệ bản hiện hành và cơ cấu giải thưởng — thay cho bốn bảng `Rulebook`, `Prize`, `TieBreakerRule`, `RoundConfig`.

### TournamentSponsor
- **Các trường chính:** `TournamentSponsorID (PK)`, `TournamentID (FK)`, `Name`, `LogoUrl`, `WebsiteUrl`, `ContactInfo`, `Tier (ENUM: DIAMOND | GOLD | SILVER | BRONZE | PARTNER)`, `DisplayOrder`, `CreatedByAccountID (FK)`, `CreatedAt`
- **Mục đích:** Nhà tài trợ và mẩu quảng cáo **gắn vào đúng một giải đấu**, thêm/sửa/xoá theo từng giải (UC-42).

---

## Module E — Đăng ký & Lệ phí (UC-06 … UC-11)

### Registration
- **Các trường chính:** `RegistrationID (PK)`, `TournamentID (FK)`, `TeamID (FK)`, `SubmittedByAccountID (FK)`, `RulebookVersionAccepted`, `ConsentAt`, `Status (ENUM: DRAFT | PENDING | APPROVED | REJECTED | WAITLISTED | WITHDRAWN | DISQUALIFIED)`, `WaitlistPosition`, `PaymentStatus (ENUM: UNPAID | PROOF_SUBMITTED | CONFIRMED | REFUNDED)`, `ReviewNote`, `FinalPlacement (ENUM: NONE | CHAMPION | RUNNER_UP | THIRD | PARTICIPANT)`, `FinalRank`, `SubmittedAt`, `ReviewedAt`
- **Mục đích:** Đơn dự giải của một đội, mang luôn vị trí hàng chờ, trạng thái lệ phí và thứ hạng chung cuộc — thay cho ba bảng `WaitlistEntry`, `TeamTitle` và bảng trạng thái thanh toán riêng.

### RegistrationLineup
- **Các trường chính:** `RegistrationLineupID (PK)`, `RegistrationID (FK)`, `AccountID (FK)`, `Slot (ENUM: STARTER | SUBSTITUTE)`, `InGameNameSnapshot`, `PlayerCredentialID (FK)`
- **Mục đích:** Danh sách thi đấu đội khai cho đúng giải đó, có chụp lại tên trong game tại thời điểm nộp.

### PaymentRecord
- **Các trường chính:** `PaymentRecordID (PK)`, `RegistrationID (FK)`, `RecordKind (ENUM: PROOF | REFUND)`, `FileUrl`, `DeclaredAmount`, `Note`, `IsCurrent`, `RecordedByAccountID (FK)`, `RecordedAt`
- **Mục đích:** **Chỉ lưu minh chứng**, không phải giao dịch và không có số dư — gộp `PaymentProof` với `RefundRecord`, giữ đúng ràng buộc LI-02.

---

## Module F — Nhánh đấu, Lịch & Trận (UC-15 … UC-24, UC-26, UC-45)

### BracketNode
- **Các trường chính:** `BracketNodeID (PK)`, `TournamentID (FK)`, `ParentNodeID (FK, tự trỏ)`, `StageNo`, `StageType (ENUM: GROUP | KNOCKOUT)`, `GroupLabel`, `RoundNo`, `PositionInRound`, `NodeType (ENUM: GROUP_SLOT | MATCH_SLOT)`, `SeedTeamID (FK, null)`, `SeedNo`, `IsBye`, `SeedingMethod (ENUM: MANUAL | RANDOM_DRAW | BY_RANKING)`, `SeedingRandomSeed`, `Status (ENUM: DRAFT | LOCKED)`
- **Mục đích:** Cây nhánh đấu một bảng duy nhất: nút cha–con dựng cả vòng bảng lẫn nhánh loại trực tiếp, kèm suất đặc cách — thay cho bốn bảng `TournamentStage`, `Bracket`, `BracketGroup`, `BracketSeed`.

### Match
- **Các trường chính:** `MatchID (PK)`, `TournamentID (FK)`, `BracketNodeID (FK)`, `TeamAID (FK)`, `TeamBID (FK)`, `RefereeAccountID (FK, null)`, `BestOf`, `ScheduledAt`, `RoomCode`, `RoomPassword`, `CheckInOpenAt`, `TeamACheckedInAt`, `TeamBCheckedInAt`, `CheckInMode (ENUM: SELF | MANUAL_OVERRIDE)`, `OverrideReason`, `ScoreA`, `ScoreB`, `WinnerTeamID (FK, null)`, `Status (ENUM: PENDING | READY | LIVE | WAITING_REPORT | DISPUTED | VERIFIED | WALKOVER | CANCELLED)`, `ResultSource (ENUM: AUTO_MATCHED | VAR_RULING | WALKOVER)`, `VerifiedAt`
- **Mục đích:** Một trận kèm luôn mốc check-in của hai đội và trọng tài phụ trách — thay cho bảng check-in riêng và bảng `TournamentReferee`.

### MatchGame
- **Các trường chính:** `MatchGameID (PK)`, `MatchID (FK)`, `GameNo`, `ScoreA`, `ScoreB`, `WinnerTeamID (FK, null)`, `UpdatedByAccountID (FK)`, `UpdatedAt`
- **Mục đích:** Tỉ số từng ván của BO3/BO5, cũng chính là nguồn cho tỉ số trực tiếp (UC-45).

### MatchRequest
- **Các trường chính:** `MatchRequestID (PK)`, `MatchID (FK)`, `TeamID (FK)`, `RequestKind (ENUM: SUBSTITUTION | REMATCH)`, `OutAccountID (FK, null)`, `InAccountID (FK, null)`, `Reason`, `Status (ENUM: PENDING | APPROVED | REJECTED)`, `RequestedByAccountID (FK)`, `DecidedByAccountID (FK, null)`, `DecidedAt`
- **Mục đích:** Mọi yêu cầu cần người duyệt trên một trận: thay người (UC-22) và đấu lại sau sự cố (UC-26).

### MatchChatMessage
- **Các trường chính:** `MatchChatMessageID (PK)`, `MatchID (FK)`, `SenderAccountID (FK)`, `Content`, `IsPinned`, `ReportStatus (ENUM: NONE | REPORTED | ACTIONED)`, `ReportedByAccountID (FK, null)`, `SentAt`
- **Mục đích:** Phòng chờ trao mã phòng, đồng thời là bằng chứng khi tranh chấp; cờ tố cáo nằm ngay trên tin nhắn nên không cần bảng báo cáo riêng.

---

## Module G — Kết quả, Tranh chấp & Kỷ luật (UC-27 … UC-35, UC-39)

### MatchReport
- **Các trường chính:** `MatchReportID (PK)`, `MatchID (FK)`, `TeamID (FK)`, `ReportedByAccountID (FK)`, `ReportedScoreA`, `ReportedScoreB`, `PerGameScores`, `Status (ENUM: SUBMITTED | MATCHED | CONFLICTING | MISSING)`, `SubmittedAt`
- **Mục đích:** Báo cáo mù của **một** đội; hai đội là hai dòng, nộp rồi không sửa và không rút.

### Dispute
- **Các trường chính:** `DisputeID (PK)`, `MatchID (FK)`, `OpenReason (ENUM: CONFLICTING_REPORT | REPORT_TIMEOUT | FLAGGED)`, `FlaggedByAccountID (FK, null)`, `FlagCategory (ENUM: WRONG_SCORE | CHEATING | INELIGIBLE_PLAYER | OTHER)`, `FlagNote`, `Status (ENUM: OPEN | UNDER_REVIEW | RESOLVED | CLOSED)`, `FinalScoreA`, `FinalScoreB`, `RulingReason`, `ResolvedByAccountID (FK, null)`, `ResolvedAt`, `AppealCount`, `AppealReason`, `AppealStatus (ENUM: NONE | PENDING | ACCEPTED | REJECTED)`
- **Mục đích:** Vòng đời tranh chấp từ lúc mở (lệch báo cáo, quá hạn, hay bị cắm cờ) tới phán quyết và một lần khiếu nại — gộp bảng `Appeal` và bảng cắm cờ.

### Penalty
- **Các trường chính:** `PenaltyID (PK)`, `TournamentID (FK)`, `TeamID (FK)`, `MatchID (FK, null)`, `Severity (ENUM: WARNING | MINOR | MAJOR | DISQUALIFICATION | CANCELLATION)`, `Points`, `Reason`, `IssuedByAccountID (FK)`, `Status (ENUM: ACTIVE | EXPIRED | CANCELLED)`, `CancelsPenaltyID (FK, tự trỏ)`, `CancelReason`, `IssuedAt`
- **Mục đích:** Án phạt tính theo điểm, so với `Tournament.PenaltyThreshold` để tước quyền (UC-35); án cấp nhầm bị huỷ bằng một dòng đối ứng chứ không xoá.

### Attachment
- **Các trường chính:** `AttachmentID (PK)`, `OwnerType (ENUM: MATCH_REPORT | DISPUTE | TOURNAMENT | ACCOUNT)`, `OwnerID`, `FileUrl`, `FileType (ENUM: IMAGE | VIDEO | REPLAY | DOCUMENT)`, `SizeBytes`, `UploadedByAccountID (FK)`, `UploadedAt`
- **Mục đích:** Kho file dùng chung cho ảnh chụp màn hình, video và file replay của mọi nơi cần đính kèm — thay cho bảng `EvidenceFile` riêng.

---

## Module H — Nền tảng (UC-36, 38, 40, 41, 43, 46)

### Notification
- **Các trường chính:** `NotificationID (PK)`, `AccountID (FK)`, `NotificationType (ENUM: REGISTRATION | CHECKIN_REMINDER | MATCH_RESULT | DISPUTE | PENALTY | NEWS | SYSTEM)`, `Title`, `Body`, `LinkUrl`, `Channel (ENUM: IN_APP | EMAIL | BOTH)`, `IsRead`, `SentAt`
- **Mục đích:** Thông báo in-app và email đã gửi cho một người; nội dung mẫu nằm ở tầng ứng dụng nên bỏ bảng `NotificationTemplate`.

### TournamentFollow
- **Các trường chính:** `TournamentFollowID (PK)`, `AccountID (FK)`, `TournamentID (FK)`, `FollowedAt`
- **Mục đích:** Ai đang theo dõi giải nào, để biết gửi thông báo cập nhật cho những ai.

### Livestream
- **Các trường chính:** `LivestreamID (PK)`, `TournamentID (FK)`, `MatchID (FK, null)`, `Platform (ENUM: YOUTUBE | FACEBOOK | TWITCH | OTHER)`, `StreamUrl`, `Title`, `Status (ENUM: SCHEDULED | LIVE | ENDED)`, `CreatedByAccountID (FK)`, `CreatedAt`
- **Mục đích:** Link phát trực tiếp do ban tổ chức thêm/sửa/xoá và gán vào từng trận (UC-46); ETOMS chỉ nhúng link của dịch vụ ngoài, không lưu video.

### AuditLog
- **Các trường chính:** `AuditLogID (PK)`, `ActorAccountID (FK, null khi là tác vụ nền)`, `TournamentID (FK, null)`, `ActionType (ENUM: FORCE_UPDATE | REMATCH | MANUAL_CHECKIN | DISQUALIFY | BAN | APPROVE | REJECT | RULEBOOK_EDIT)`, `ObjectType (ENUM)`, `ObjectID`, `BeforeValue`, `AfterValue`, `Reason`, `CreatedAt`
- **Mục đích:** Nhật ký mọi thao tác quyền lực kèm giá trị trước–sau, vừa là bằng chứng chống thiên vị vừa là nơi giữ bản thể lệ cũ sau khi sửa.

---

## Bảng xếp hạng — làm bằng VIEW, không phải bảng

### vw_Leaderboard
- **Nguồn:** `Registration.FinalPlacement` + `Registration.FinalRank` + `Tournament.MaxTeams` + `Penalty.Points`
- **Mục đích:** Điểm thành tích toàn hệ thống theo tựa game và theo mùa (UC-19, UC-38) tính trực tiếp từ kết quả đã chốt, nên không cần bảng `LeaderboardEntry` lưu trùng.
