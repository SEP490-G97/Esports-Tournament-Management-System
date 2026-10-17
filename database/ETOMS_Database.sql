/*===============================================================================
  ETOMS - Hệ thống quản lý giải đấu Thể thao Điện tử
  SEP490_G97 - Script tạo Database hoàn chỉnh
  Chạy được ở CẢ HAI nơi: SQL Server 2019+ (SSMS) và Azure SQL Database.
  Collation: Vietnamese_CI_AS - mọi cột chữ dùng NVARCHAR

  CHẠY BẰNG sqlcmd: phải thêm -f 65001 để đọc đúng UTF-8, nếu không dấu tiếng Việt
  trong câu lệnh sẽ bị hỏng. SSMS đọc đúng sẵn, không cần làm gì.

  Nguồn: danh mục 144 chức năng (19 module) + Report 3 (SRS).
  Mã quy tắc BR-<MODULE>-nn và mã thông báo MSG-<MODULE>-nnn trong file này
  trùng khớp từng mã với Report 3 mục 5.1 và 5.2.

  HAI RÀNG BUỘC NỀN TẢNG CỦA DỰ ÁN - đã phản ánh vào thiết kế:
    1. KHÔNG tích hợp API nhà phát hành game.
       => Tỉ số CHỈ vào hệ thống qua MatchReport (2 bản báo cáo độc lập của
          2 đội) hoặc qua phán quyết VAR trong Dispute. Không có bảng nào
          lưu bản sao dữ liệu từ nhà phát hành.
    2. KHÔNG tích hợp cổng thanh toán.
       => Không có bảng giao dịch, không có cột số dư. PaymentProof và
          RefundRecord là bảng MINH CHỨNG, không phải bảng giao dịch.

  CÁCH CHẠY
  ---------
  SQL Server / SSMS:
    Lần đầu, mở file và bấm F5 khi đang ở database bất kỳ. Script tạo
    database ETOMS rồi DỪNG và nhắc bạn chọn nó. Chọn ETOMS trên thanh
    database rồi chạy lại cả file - lần này nó tạo toàn bộ bảng.

  Azure SQL Database:
    Tạo database trên cổng Azure với Collation = Vietnamese_CI_AS (đặt lúc
    tạo, sau không đổi được). Kết nối thẳng vào database đó rồi chạy cả
    file. Khối CREATE DATABASE bên dưới tự bỏ qua vì Azure không cho chạy.

  XOÁ VÀ TẠO LẠI TỪ ĐẦU (chỉ SQL Server, mất toàn bộ dữ liệu):
    Chạy riêng ba dòng này ở database master trước, rồi chạy lại file:
      -- ALTER DATABASE ETOMS SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
      -- DROP DATABASE ETOMS;
      -- GO
===============================================================================*/

/*-------------------------------------------------------------------------------
  BƯỚC 0 - Tạo database. Chỉ chạy trên SQL Server.
  EngineEdition = 5 nghĩa là Azure SQL Database: ở đó database phải được tạo
  sẵn trên cổng Azure, và câu lệnh CREATE DATABASE trong script sẽ lỗi.
  Bọc trong EXEC vì CREATE DATABASE phải đứng một mình trong một batch.
-------------------------------------------------------------------------------*/
IF SERVERPROPERTY('EngineEdition') <> 5 AND DB_ID(N'ETOMS') IS NULL
BEGIN
    EXEC (N'CREATE DATABASE ETOMS COLLATE Vietnamese_CI_AS');
    PRINT N'Đã tạo database ETOMS. Hãy CHỌN nó trên thanh database rồi chạy lại file này.';
END
GO

/*-------------------------------------------------------------------------------
  CHỐT CHẶN - không cho tạo 50 bảng nhầm vào database hệ thống.
  SET NOEXEC ON làm mọi câu lệnh phía sau chỉ được phân tích chứ không chạy,
  nên script dừng an toàn thay vì đổ bảng vào master.
-------------------------------------------------------------------------------*/
IF DB_NAME() IN (N'master', N'tempdb', N'model', N'msdb')
BEGIN
    PRINT N'DỪNG: đang ở database ' + DB_NAME() + N'. Hãy chọn database ETOMS rồi chạy lại.';
    PRINT N'(Các thông báo lỗi ngay sau dòng này là bình thường: script đã dừng, không tạo gì.)';
    SET NOEXEC ON;
END
GO

/* BẮT BUỘC: các chỉ mục có điều kiện (CREATE UNIQUE INDEX ... WHERE ...) chỉ tạo
   được khi QUOTED_IDENTIFIER và ANSI_NULLS đang ON. SSMS mặc định ON, nhưng
   sqlcmd mặc định OFF - đặt rõ ở đây để script chạy đúng ở mọi nơi. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

/*===============================================================================
  NHÓM 1 - ĐỊNH DANH & TÀI KHOẢN        (module 1 Xác thực, 2 Hồ sơ)
===============================================================================*/

/* Role - danh mục quyền hệ thống.
   Lưu ý: vai trò "Đội trưởng" KHÔNG nằm ở đây. Đó là vai trò trong đội,
   nằm ở TeamMembership.RoleInTeam. */
CREATE TABLE dbo.Role
(
    RoleId      INT IDENTITY(1,1) NOT NULL,
    Code        VARCHAR(20)       NOT NULL,
    Name        NVARCHAR(50)      NOT NULL,
    Description NVARCHAR(255)     NULL,
    CONSTRAINT PK_Role      PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Role_Code UNIQUE (Code)
);
GO

/* AppUser - thực thể "Người dùng" của SRS.
   Đặt tên AppUser vì USER là từ khoá của T-SQL.
   Quy tắc: Email duy nhất toàn hệ thống. Tài khoản KHÔNG BAO GIỜ bị xoá,
            chỉ chuyển sang BANNED (xem trigger TR_AppUser_NoDelete). */
CREATE TABLE dbo.AppUser
(
    UserId           INT IDENTITY(1,1) NOT NULL,
    Email            NVARCHAR(256)     NOT NULL,
    PasswordHash     VARCHAR(255)      NULL,
    GoogleSubjectId  VARCHAR(255)      NULL,   -- API-01 đăng nhập bằng Google OAuth2
    DisplayName      NVARCHAR(100)     NOT NULL,
    AvatarUrl        NVARCHAR(500)     NULL,
    DateOfBirth      DATE              NULL,
    PhoneNumber      NVARCHAR(20)      NULL,
    Organization     NVARCHAR(150)     NULL,   -- trường / đơn vị
    FacebookUrl      NVARCHAR(500)     NULL,
    Status           VARCHAR(20)       NOT NULL CONSTRAINT DF_AppUser_Status DEFAULT 'UNVERIFIED',
    EmailVerifiedAt  DATETIME2(0)      NULL,
    BanReason        NVARCHAR(500)     NULL,   -- ADM-02: bắt buộc khi khoá tài khoản
    BannedAt         DATETIME2(0)      NULL,
    BannedByUserId   INT               NULL,
    FailedLoginCount INT               NOT NULL CONSTRAINT DF_AppUser_Failed DEFAULT 0,
    LockoutUntil     DATETIME2(0)      NULL,   -- khoá 30 phút sau nhiều lần đăng nhập sai (SRS chưa đặt mã cho thông báo này)
    CreatedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_AppUser_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt        DATETIME2(0)      NULL,
    CONSTRAINT PK_AppUser            PRIMARY KEY (UserId),
    CONSTRAINT UQ_AppUser_Email      UNIQUE (Email),
    CONSTRAINT CK_AppUser_Status     CHECK (Status IN ('UNVERIFIED','ACTIVE','BANNED')),
    CONSTRAINT CK_AppUser_Credential CHECK (PasswordHash IS NOT NULL OR GoogleSubjectId IS NOT NULL),
    CONSTRAINT CK_AppUser_BanReason  CHECK (Status <> 'BANNED' OR BanReason IS NOT NULL),
    CONSTRAINT FK_AppUser_BannedBy   FOREIGN KEY (BannedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

CREATE UNIQUE INDEX UX_AppUser_GoogleSubjectId
    ON dbo.AppUser(GoogleSubjectId) WHERE GoogleSubjectId IS NOT NULL;
GO

/* UserRole - 1 người dùng giữ NHIỀU vai trò cùng lúc.
   Ví dụ: là Người chơi ở giải này và Ban tổ chức ở giải khác. */
CREATE TABLE dbo.UserRole
(
    UserId          INT          NOT NULL,
    RoleId          INT          NOT NULL,
    GrantedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_UserRole_Granted DEFAULT SYSUTCDATETIME(),
    GrantedByUserId INT          NULL,
    CONSTRAINT PK_UserRole          PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_UserRole_User     FOREIGN KEY (UserId)          REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_UserRole_Role     FOREIGN KEY (RoleId)          REFERENCES dbo.Role(RoleId),
    CONSTRAINT FK_UserRole_GrantedBy FOREIGN KEY (GrantedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* RefreshToken - AUTH-02 sinh JWT + làm mới, AUTH-05/06 thu hồi phiên đăng nhập */
CREATE TABLE dbo.RefreshToken
(
    RefreshTokenId INT IDENTITY(1,1) NOT NULL,
    UserId         INT               NOT NULL,
    TokenHash      VARCHAR(255)      NOT NULL,
    ExpiresAt      DATETIME2(0)      NOT NULL,
    RevokedAt      DATETIME2(0)      NULL,
    RevokeReason   NVARCHAR(100)     NULL,
    UserAgent      NVARCHAR(300)     NULL,
    IpAddress      VARCHAR(45)       NULL,
    CreatedAt      DATETIME2(0)      NOT NULL CONSTRAINT DF_RefreshToken_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_RefreshToken          PRIMARY KEY (RefreshTokenId),
    CONSTRAINT UQ_RefreshToken_Hash     UNIQUE (TokenHash),
    CONSTRAINT FK_RefreshToken_User     FOREIGN KEY (UserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* VerificationToken - AUTH-03 đặt lại mật khẩu, AUTH-04 xác minh email */
CREATE TABLE dbo.VerificationToken
(
    VerificationTokenId INT IDENTITY(1,1) NOT NULL,
    UserId              INT               NOT NULL,
    TokenHash           VARCHAR(255)      NOT NULL,
    TokenType           VARCHAR(30)       NOT NULL,
    ExpiresAt           DATETIME2(0)      NOT NULL,
    UsedAt              DATETIME2(0)      NULL,
    CreatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_VerificationToken_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_VerificationToken         PRIMARY KEY (VerificationTokenId),
    CONSTRAINT UQ_VerificationToken_Hash    UNIQUE (TokenHash),
    CONSTRAINT CK_VerificationToken_Type    CHECK (TokenType IN ('EMAIL_VERIFICATION','PASSWORD_RESET')),
    CONSTRAINT FK_VerificationToken_User    FOREIGN KEY (UserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* NotificationPreference - PRF-06 bật/tắt từng loại thông báo */
CREATE TABLE dbo.NotificationPreference
(
    UserId           INT         NOT NULL,
    NotificationType VARCHAR(50) NOT NULL,
    InAppEnabled     BIT         NOT NULL CONSTRAINT DF_NotificationPreference_InApp DEFAULT 1,
    EmailEnabled     BIT         NOT NULL CONSTRAINT DF_NotificationPreference_Email DEFAULT 1,
    CONSTRAINT PK_NotificationPreference       PRIMARY KEY (UserId, NotificationType),
    CONSTRAINT FK_NotificationPreference_User  FOREIGN KEY (UserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* OrganizerRequest - ORG-02 xét duyệt hồ sơ đơn vị tổ chức, chống lập giải ảo.
   Mã ADM-05 đã gộp vào ORG-02 ngày 17/10/2026. */
CREATE TABLE dbo.OrganizerRequest
(
    OrganizerRequestId INT IDENTITY(1,1) NOT NULL,
    UserId             INT               NOT NULL,
    OrganizationName   NVARCHAR(150)     NOT NULL,
    ProofFileUrl       NVARCHAR(500)     NULL,
    Note               NVARCHAR(1000)    NULL,
    Status             VARCHAR(20)       NOT NULL CONSTRAINT DF_OrganizerRequest_Status DEFAULT 'PENDING',
    ReviewedByUserId   INT               NULL,
    ReviewedAt         DATETIME2(0)      NULL,
    DecisionReason     NVARCHAR(500)     NULL,
    CreatedAt          DATETIME2(0)      NOT NULL CONSTRAINT DF_OrganizerRequest_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_OrganizerRequest            PRIMARY KEY (OrganizerRequestId),
    CONSTRAINT CK_OrganizerRequest_Status     CHECK (Status IN ('PENDING','APPROVED','REJECTED','REVOKED')),
    CONSTRAINT FK_OrganizerRequest_User       FOREIGN KEY (UserId)           REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_OrganizerRequest_ReviewedBy FOREIGN KEY (ReviewedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/*===============================================================================
  NHÓM 2 - DANH MỤC GAME                 (module 3 Quản trị hệ thống, 2 Hồ sơ)
===============================================================================*/

/* Game - ADM-03. Không xoá cứng game đã có giải => chỉ đổi Status. */
CREATE TABLE dbo.Game
(
    GameId    INT IDENTITY(1,1) NOT NULL,
    Title     NVARCHAR(100)     NOT NULL,
    ShortCode VARCHAR(20)       NOT NULL,
    LogoUrl   NVARCHAR(500)     NULL,
    BannerUrl NVARCHAR(500)     NULL,
    Status    VARCHAR(20)       NOT NULL CONSTRAINT DF_Game_Status DEFAULT 'ACTIVE',
    CreatedAt DATETIME2(0)      NOT NULL CONSTRAINT DF_Game_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Game             PRIMARY KEY (GameId),
    CONSTRAINT UQ_Game_Title       UNIQUE (Title),
    CONSTRAINT UQ_Game_ShortCode   UNIQUE (ShortCode),
    CONSTRAINT CK_Game_Status      CHECK (Status IN ('ACTIVE','INACTIVE'))
);
GO

/* Ruleset - GRL-01 bộ luật thi đấu của một tựa game.
   THAY CHO bảng GameRule của bản 13/10/2026. Lý do thay: GameRule có ràng buộc
   UNIQUE (GameId) nên MỖI TỰA GAME CHỈ ĐƯỢC MỘT bộ luật, còn GRL-01 đòi một
   tựa game có NHIỀU bộ luật - ví dụ Liên Minh Huyền Thoại có Draft Pick và
   Blind Pick. Mã ADM-04 "Cấu hình Rule theo Game" và GRL-05 "Hỗ trợ tựa game
   thi đấu đơn" đều đã gộp vào GRL-01 ngày 17/10/2026.
   MinStarters được phép bằng 1 để chạy giải game đối kháng đơn; đội một người
   vẫn đi qua đúng luồng đăng ký và thi đấu.
   Đây là NGUỒN KIỂM TRA cho Tạo giải (TOUR-01) và Đăng ký đội (REG-01):
   giải chọn một bộ luật ở GRL-06 (Tournament.RulesetId). */
CREATE TABLE dbo.Ruleset
(
    RulesetId               INT IDENTITY(1,1) NOT NULL,
    GameId                  INT               NOT NULL,
    Name                    NVARCHAR(100)     NOT NULL,
    MinStarters             INT               NOT NULL,
    MaxStarters             INT               NOT NULL,
    MaxSubstitutes          INT               NOT NULL,
    DefaultFormat           VARCHAR(30)       NOT NULL,
    DefaultBestOf           INT               NOT NULL CONSTRAINT DF_Ruleset_BestOf DEFAULT 1,
    AllowedEvidenceTypes    NVARCHAR(200)     NOT NULL,  -- ví dụ: 'png,jpg,jpeg,mp4,rofl'
    MaxEvidenceFileSizeMB   INT               NOT NULL CONSTRAINT DF_Ruleset_MaxSize DEFAULT 10,
    IsActive                BIT               NOT NULL CONSTRAINT DF_Ruleset_Active DEFAULT 1,
    CreatedByUserId         INT               NULL,
    CreatedAt               DATETIME2(0)      NOT NULL CONSTRAINT DF_Ruleset_At DEFAULT SYSUTCDATETIME(),
    UpdatedByUserId         INT               NULL,
    UpdatedAt               DATETIME2(0)      NULL,
    CONSTRAINT PK_Ruleset            PRIMARY KEY (RulesetId),
    CONSTRAINT UQ_Ruleset_Name       UNIQUE (GameId, Name),
    CONSTRAINT CK_Ruleset_Roster     CHECK (MinStarters >= 1 AND MaxStarters >= MinStarters AND MaxSubstitutes >= 0),
    CONSTRAINT CK_Ruleset_Format     CHECK (DefaultFormat IN ('SINGLE_ELIMINATION','DOUBLE_ELIMINATION','ROUND_ROBIN','HYBRID')),
    CONSTRAINT CK_Ruleset_BestOf     CHECK (DefaultBestOf IN (1,3,5,7)),
    CONSTRAINT FK_Ruleset_Game       FOREIGN KEY (GameId)          REFERENCES dbo.Game(GameId),
    CONSTRAINT FK_Ruleset_CreatedBy  FOREIGN KEY (CreatedByUserId) REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_Ruleset_UpdatedBy  FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* GameAccount - PRF-02. Quy tắc: 1 người dùng khai đúng 1 tên trong game cho 1 tựa game (SRS chưa đặt mã cho thông báo này). */
CREATE TABLE dbo.GameAccount
(
    GameAccountId INT IDENTITY(1,1) NOT NULL,
    UserId        INT               NOT NULL,
    GameId        INT               NOT NULL,
    InGameName    NVARCHAR(100)     NOT NULL,
    ServerRegion  NVARCHAR(50)      NULL,
    Status        VARCHAR(20)       NOT NULL CONSTRAINT DF_GameAccount_Status DEFAULT 'DECLARED',
    DeclaredAt    DATETIME2(0)      NOT NULL CONSTRAINT DF_GameAccount_Declared DEFAULT SYSUTCDATETIME(),
    UpdatedAt     DATETIME2(0)      NULL,
    CONSTRAINT PK_GameAccount           PRIMARY KEY (GameAccountId),
    CONSTRAINT UQ_GameAccount_UserGame  UNIQUE (UserId, GameId),
    CONSTRAINT CK_GameAccount_Status    CHECK (Status IN ('DECLARED','CHANGED')),
    CONSTRAINT FK_GameAccount_User      FOREIGN KEY (UserId) REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_GameAccount_Game      FOREIGN KEY (GameId) REFERENCES dbo.Game(GameId)
);
GO

/* IdentityDocument - PRF-03 ảnh thẻ sinh viên / giấy xác nhận.
   DỮ LIỆU CÁ NHÂN: chỉ Ban tổ chức của giải mà người dùng đăng ký được đọc,
   và chỉ trong thời gian giải chạy. Kiểm soát ở tầng ứng dụng. */
CREATE TABLE dbo.IdentityDocument
(
    IdentityDocumentId INT IDENTITY(1,1) NOT NULL,
    UserId             INT               NOT NULL,
    DocumentType       VARCHAR(30)       NOT NULL,
    FileUrl            NVARCHAR(500)     NOT NULL,
    ContentType        VARCHAR(100)      NULL,
    FileSizeBytes      BIGINT            NULL,
    Status             VARCHAR(20)       NOT NULL CONSTRAINT DF_IdentityDocument_Status DEFAULT 'UPLOADED',
    UploadedAt         DATETIME2(0)      NOT NULL CONSTRAINT DF_IdentityDocument_Uploaded DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_IdentityDocument          PRIMARY KEY (IdentityDocumentId),
    CONSTRAINT CK_IdentityDocument_Type     CHECK (DocumentType IN ('STUDENT_CARD','CONFIRMATION_LETTER','CITIZEN_ID','OTHER')),
    CONSTRAINT CK_IdentityDocument_Status   CHECK (Status IN ('UPLOADED','VERIFIED','REJECTED')),
    CONSTRAINT FK_IdentityDocument_User     FOREIGN KEY (UserId) REFERENCES dbo.AppUser(UserId)
);
GO

/*===============================================================================
  NHÓM 3 - ĐỘI TUYỂN                     (module 4 Đội tuyển, 5 Tìm đội & tìm người)
===============================================================================*/

/* Team - TEAM-01. Quy tắc: tên đội duy nhất trong cùng 1 tựa game.
   Giải thể = xoá mềm, giữ lại lịch sử thi đấu. */
CREATE TABLE dbo.Team
(
    TeamId        INT IDENTITY(1,1) NOT NULL,
    GameId        INT               NOT NULL,
    Name          NVARCHAR(100)     NOT NULL,
    Tag           NVARCHAR(10)      NOT NULL,
    LogoUrl       NVARCHAR(500)     NULL,
    Slogan        NVARCHAR(255)     NULL,
    CaptainUserId INT               NOT NULL,
    WinCount      INT               NOT NULL CONSTRAINT DF_Team_Win DEFAULT 0,
    LossCount     INT               NOT NULL CONSTRAINT DF_Team_Loss DEFAULT 0,
    Status        VARCHAR(20)       NOT NULL CONSTRAINT DF_Team_Status DEFAULT 'ACTIVE',
    CreatedAt     DATETIME2(0)      NOT NULL CONSTRAINT DF_Team_CreatedAt DEFAULT SYSUTCDATETIME(),
    DisbandedAt   DATETIME2(0)      NULL,
    CONSTRAINT PK_Team              PRIMARY KEY (TeamId),
    CONSTRAINT UQ_Team_GameName     UNIQUE (GameId, Name),
    CONSTRAINT UQ_Team_GameTag      UNIQUE (GameId, Tag),
    CONSTRAINT UQ_Team_IdGame       UNIQUE (TeamId, GameId),  -- để TeamMembership tham chiếu ghép
    CONSTRAINT CK_Team_Status       CHECK (Status IN ('ACTIVE','DISBANDED')),
    CONSTRAINT FK_Team_Game         FOREIGN KEY (GameId)        REFERENCES dbo.Game(GameId),
    CONSTRAINT FK_Team_Captain      FOREIGN KEY (CaptainUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* TeamMembership - TEAM-03/05/06.
   GameId được lưu lặp lại có chủ đích và ràng buộc bằng KHOÁ NGOẠI GHÉP tới
   Team(TeamId, GameId) => không thể lệch với game của đội.
   Nhờ vậy mới đặt được chỉ mục duy nhất "1 người 1 đội cho mỗi game". */
CREATE TABLE dbo.TeamMembership
(
    TeamMembershipId INT IDENTITY(1,1) NOT NULL,
    TeamId           INT               NOT NULL,
    GameId           INT               NOT NULL,
    UserId           INT               NOT NULL,
    RoleInTeam       VARCHAR(20)       NOT NULL CONSTRAINT DF_TeamMembership_Role DEFAULT 'STARTER',
    Status           VARCHAR(20)       NOT NULL CONSTRAINT DF_TeamMembership_Status DEFAULT 'ACTIVE',
    JoinedAt         DATETIME2(0)      NOT NULL CONSTRAINT DF_TeamMembership_Joined DEFAULT SYSUTCDATETIME(),
    LeftAt           DATETIME2(0)      NULL,
    CONSTRAINT PK_TeamMembership        PRIMARY KEY (TeamMembershipId),
    CONSTRAINT CK_TeamMembership_Role   CHECK (RoleInTeam IN ('CAPTAIN','STARTER','SUBSTITUTE')),
    CONSTRAINT CK_TeamMembership_Status CHECK (Status IN ('ACTIVE','LEFT')),
    CONSTRAINT FK_TeamMembership_Team   FOREIGN KEY (TeamId, GameId) REFERENCES dbo.Team(TeamId, GameId),
    CONSTRAINT FK_TeamMembership_User   FOREIGN KEY (UserId)         REFERENCES dbo.AppUser(UserId)
);
GO

-- 1 người dùng chỉ có 1 tư cách thành viên đang hoạt động trong 1 đội
CREATE UNIQUE INDEX UX_TeamMembership_ActiveInTeam
    ON dbo.TeamMembership(TeamId, UserId) WHERE Status = 'ACTIVE';
-- QUY TẮC DỮ LIỆU: 1 người chơi chỉ thuộc 1 đội cho mỗi tựa game
CREATE UNIQUE INDEX UX_TeamMembership_OneTeamPerGame
    ON dbo.TeamMembership(UserId, GameId) WHERE Status = 'ACTIVE';
GO

/* InviteCode - TEAM-04 mã mời có hạn dùng, giới hạn lượt dùng, thu hồi được */
CREATE TABLE dbo.InviteCode
(
    InviteCodeId    INT IDENTITY(1,1) NOT NULL,
    TeamId          INT               NOT NULL,
    Code            VARCHAR(40)       NOT NULL,
    ExpiresAt       DATETIME2(0)      NULL,
    MaxUses         INT               NULL,
    UsedCount       INT               NOT NULL CONSTRAINT DF_InviteCode_Used DEFAULT 0,
    Status          VARCHAR(20)       NOT NULL CONSTRAINT DF_InviteCode_Status DEFAULT 'ACTIVE',
    CreatedByUserId INT               NOT NULL,
    CreatedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_InviteCode_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_InviteCode           PRIMARY KEY (InviteCodeId),
    CONSTRAINT UQ_InviteCode_Code      UNIQUE (Code),
    CONSTRAINT CK_InviteCode_Status    CHECK (Status IN ('ACTIVE','REVOKED','EXPIRED')),
    CONSTRAINT CK_InviteCode_Uses      CHECK (MaxUses IS NULL OR UsedCount <= MaxUses),
    CONSTRAINT FK_InviteCode_Team      FOREIGN KEY (TeamId)          REFERENCES dbo.Team(TeamId),
    CONSTRAINT FK_InviteCode_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* RecruitmentPost - FA-01 (LFP: đội tìm người) và FA-02 (LFT: người tìm đội).
   Một bảng cho cả hai chiều, phân biệt bằng PostType. */
CREATE TABLE dbo.RecruitmentPost
(
    RecruitmentPostId INT IDENTITY(1,1) NOT NULL,
    PostType          VARCHAR(10)       NOT NULL,
    AuthorUserId      INT               NOT NULL,
    TeamId            INT               NULL,
    GameId            INT               NOT NULL,
    PositionsWanted   NVARCHAR(200)     NULL,
    RankRequirement   NVARCHAR(100)     NULL,
    AvailableTime     NVARCHAR(200)     NULL,
    Description       NVARCHAR(2000)    NULL,
    ContactInfo       NVARCHAR(200)     NULL,
    Status            VARCHAR(20)       NOT NULL CONSTRAINT DF_RecruitmentPost_Status DEFAULT 'OPEN',
    ExpiresAt         DATETIME2(0)      NULL,
    CreatedAt         DATETIME2(0)      NOT NULL CONSTRAINT DF_RecruitmentPost_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_RecruitmentPost           PRIMARY KEY (RecruitmentPostId),
    CONSTRAINT CK_RecruitmentPost_Type      CHECK (PostType IN ('LFP','LFT')),
    CONSTRAINT CK_RecruitmentPost_Status    CHECK (Status IN ('OPEN','CLOSED','EXPIRED')),
    -- LFP là đội đăng tin => bắt buộc có TeamId. LFT là cá nhân => không có TeamId.
    CONSTRAINT CK_RecruitmentPost_TeamLink  CHECK ((PostType = 'LFP' AND TeamId IS NOT NULL)
                                               OR (PostType = 'LFT' AND TeamId IS NULL)),
    CONSTRAINT FK_RecruitmentPost_Author    FOREIGN KEY (AuthorUserId) REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_RecruitmentPost_Team      FOREIGN KEY (TeamId)       REFERENCES dbo.Team(TeamId),
    CONSTRAINT FK_RecruitmentPost_Game      FOREIGN KEY (GameId)       REFERENCES dbo.Game(GameId)
);
GO

/* JoinApplication - FA-04/05. Quy tắc: 1 người nộp tối đa 1 đơn cho 1 bài đăng. */
CREATE TABLE dbo.JoinApplication
(
    JoinApplicationId INT IDENTITY(1,1) NOT NULL,
    RecruitmentPostId INT               NOT NULL,
    ApplicantUserId   INT               NOT NULL,
    Message           NVARCHAR(1000)    NULL,
    HighlightUrl      NVARCHAR(500)     NULL,
    Status            VARCHAR(20)       NOT NULL CONSTRAINT DF_JoinApplication_Status DEFAULT 'SUBMITTED',
    SubmittedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_JoinApplication_Submitted DEFAULT SYSUTCDATETIME(),
    DecidedByUserId   INT               NULL,
    DecidedAt         DATETIME2(0)      NULL,
    CONSTRAINT PK_JoinApplication            PRIMARY KEY (JoinApplicationId),
    CONSTRAINT UQ_JoinApplication_PostUser   UNIQUE (RecruitmentPostId, ApplicantUserId),
    CONSTRAINT CK_JoinApplication_Status     CHECK (Status IN ('SUBMITTED','ACCEPTED','DECLINED')),
    CONSTRAINT FK_JoinApplication_Post       FOREIGN KEY (RecruitmentPostId) REFERENCES dbo.RecruitmentPost(RecruitmentPostId),
    CONSTRAINT FK_JoinApplication_Applicant  FOREIGN KEY (ApplicantUserId)   REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_JoinApplication_DecidedBy  FOREIGN KEY (DecidedByUserId)   REFERENCES dbo.AppUser(UserId)
);
GO

/*===============================================================================
  NHÓM 4 - GIẢI ĐẤU                      (module 6 Thiết lập giải, 13 Cổng thông tin)
===============================================================================*/

/* Tournament - TOUR-01..TOUR-12.
   Toàn bộ cấu hình mà hệ thống sẽ tự áp dụng về sau nằm ở đây. */
CREATE TABLE dbo.Tournament
(
    TournamentId              INT IDENTITY(1,1) NOT NULL,
    Name                      NVARCHAR(200)     NOT NULL,
    GameId                    INT               NOT NULL,
    OrganizerUserId           INT               NOT NULL,
    Description               NVARCHAR(MAX)     NULL,
    BannerUrl                 NVARCHAR(500)     NULL,
    OrganizingUnit            NVARCHAR(200)     NULL,
    Region                    NVARCHAR(100)     NULL,
    ContactInfo               NVARCHAR(300)     NULL,
    IsOnline                  BIT               NOT NULL CONSTRAINT DF_Tournament_IsOnline DEFAULT 1,
    Venue                     NVARCHAR(300)     NULL,
    -- Thể thức (TOUR-03/04/05)
    Format                    VARCHAR(30)       NOT NULL,
    HasThirdPlaceMatch        BIT               NOT NULL CONSTRAINT DF_Tournament_ThirdPlace DEFAULT 0,
    -- Quy mô & danh sách thi đấu (TOUR-02, nguồn kiểm tra REG-01)
    MaxTeams                  INT               NOT NULL,
    StarterCount              INT               NOT NULL,
    MaxSubstitutes            INT               NOT NULL,
    -- Mốc thời gian (TOUR-02)
    RegistrationOpenAt        DATETIME2(0)      NOT NULL,
    RegistrationCloseAt       DATETIME2(0)      NOT NULL,
    StartAt                   DATETIME2(0)      NOT NULL,
    EndAt                     DATETIME2(0)      NULL,
    -- Tài chính - CHỈ LÀ THÔNG TIN CÔNG BỐ, không có giao dịch (TOUR-06)
    EntryFee                  DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Tournament_Fee DEFAULT 0,
    BankAccountInfo           NVARCHAR(300)     NULL,
    -- Yêu cầu hồ sơ (TOUR-07)
    RequireStudentCard        BIT               NOT NULL CONSTRAINT DF_Tournament_ReqCard DEFAULT 1,
    RequirePaymentProof       BIT               NOT NULL CONSTRAINT DF_Tournament_ReqPay DEFAULT 1,
    RequireInGameName         BIT               NOT NULL CONSTRAINT DF_Tournament_ReqIgn DEFAULT 1,
    -- Tham số vận hành cho các tác vụ nền
    CheckInOpenMinutesBefore  INT               NOT NULL CONSTRAINT DF_Tournament_CiOpen DEFAULT 30,  -- JOB-01 / NOT-03
    CheckInCloseMinutesBefore INT               NOT NULL CONSTRAINT DF_Tournament_CiClose DEFAULT 5,  -- JOB-02
    ResultWindowMinutes       INT               NOT NULL CONSTRAINT DF_Tournament_ResWindow DEFAULT 60,  -- JOB-03
    PenaltyThreshold          INT               NULL,      -- JOB-12, NULL = không tự tước quyền
    Status                    VARCHAR(25)       NOT NULL CONSTRAINT DF_Tournament_Status DEFAULT 'DRAFT',
    CancelReason              NVARCHAR(500)     NULL,
    CancelledAt               DATETIME2(0)      NULL,
    CreatedAt                 DATETIME2(0)      NOT NULL CONSTRAINT DF_Tournament_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt                 DATETIME2(0)      NULL,
    CONSTRAINT PK_Tournament              PRIMARY KEY (TournamentId),
    CONSTRAINT UQ_Tournament_IdGame       UNIQUE (TournamentId, GameId),
    CONSTRAINT CK_Tournament_Format       CHECK (Format IN ('SINGLE_ELIMINATION','DOUBLE_ELIMINATION','ROUND_ROBIN','HYBRID')),
    CONSTRAINT CK_Tournament_Status       CHECK (Status IN ('DRAFT','PUBLISHED','REGISTRATION_OPEN','REGISTRATION_CLOSED','ONGOING','COMPLETED','CANCELLED')),
    CONSTRAINT CK_Tournament_MaxTeams     CHECK (MaxTeams >= 2),
    CONSTRAINT CK_Tournament_Roster       CHECK (StarterCount >= 1 AND MaxSubstitutes >= 0),
    CONSTRAINT CK_Tournament_Fee          CHECK (EntryFee >= 0),
    -- BR-TOUR-01 / MSG-TOUR-001: đóng đăng ký phải trước ngày thi đấu
    CONSTRAINT CK_Tournament_Timeline     CHECK (RegistrationOpenAt < RegistrationCloseAt
                                             AND RegistrationCloseAt <= StartAt
                                             AND (EndAt IS NULL OR EndAt >= StartAt)),
    CONSTRAINT CK_Tournament_CancelReason CHECK (Status <> 'CANCELLED' OR CancelReason IS NOT NULL),
    CONSTRAINT CK_Tournament_Venue        CHECK (IsOnline = 1 OR Venue IS NOT NULL),
    CONSTRAINT FK_Tournament_Game         FOREIGN KEY (GameId)          REFERENCES dbo.Game(GameId),
    CONSTRAINT FK_Tournament_Organizer    FOREIGN KEY (OrganizerUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* Rulebook - TOUR-08.
   Bản đã PUBLISHED không bao giờ sửa tại chỗ. Sửa = tạo phiên bản mới,
   bản cũ chuyển SUPERSEDED và vẫn được giữ vì các đội đã cam kết theo nó. */
CREATE TABLE dbo.Rulebook
(
    RulebookId      INT IDENTITY(1,1) NOT NULL,
    TournamentId    INT               NOT NULL,
    VersionNumber   INT               NOT NULL,
    Content         NVARCHAR(MAX)     NOT NULL,
    Status          VARCHAR(20)       NOT NULL CONSTRAINT DF_Rulebook_Status DEFAULT 'DRAFT',
    PublishedAt     DATETIME2(0)      NULL,
    CreatedByUserId INT               NOT NULL,
    CreatedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_Rulebook_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Rulebook             PRIMARY KEY (RulebookId),
    CONSTRAINT UQ_Rulebook_Version     UNIQUE (TournamentId, VersionNumber),
    CONSTRAINT CK_Rulebook_Status      CHECK (Status IN ('DRAFT','PUBLISHED','SUPERSEDED')),
    CONSTRAINT CK_Rulebook_Version     CHECK (VersionNumber >= 1),
    CONSTRAINT FK_Rulebook_Tournament  FOREIGN KEY (TournamentId)    REFERENCES dbo.Tournament(TournamentId),
    CONSTRAINT FK_Rulebook_CreatedBy   FOREIGN KEY (CreatedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

-- Mỗi giải chỉ có ĐÚNG 1 bản thể lệ đang hiệu lực
CREATE UNIQUE INDEX UX_Rulebook_CurrentPublished
    ON dbo.Rulebook(TournamentId) WHERE Status = 'PUBLISHED';
GO

/* Prize - TOUR-06 cơ cấu giải thưởng Top 1/2/3... */
CREATE TABLE dbo.Prize
(
    PrizeId      INT IDENTITY(1,1) NOT NULL,
    TournamentId INT               NOT NULL,
    Rank         INT               NOT NULL,
    Title        NVARCHAR(100)     NOT NULL,
    Amount       DECIMAL(18,2)     NULL,
    Description  NVARCHAR(500)     NULL,
    CONSTRAINT PK_Prize            PRIMARY KEY (PrizeId),
    CONSTRAINT UQ_Prize_Rank       UNIQUE (TournamentId, Rank),
    CONSTRAINT CK_Prize_Rank       CHECK (Rank >= 1),
    CONSTRAINT CK_Prize_Amount     CHECK (Amount IS NULL OR Amount >= 0),
    CONSTRAINT FK_Prize_Tournament FOREIGN KEY (TournamentId) REFERENCES dbo.Tournament(TournamentId)
);
GO

/* TournamentStage - TOUR-04 giải nhiều vòng kết hợp.
   MỌI giải đều có ít nhất 1 vòng. Giải loại trực tiếp đơn giản = 1 vòng KNOCKOUT.
   Giải kết hợp = vòng 1 GROUP + vòng 2 KNOCKOUT. */
CREATE TABLE dbo.TournamentStage
(
    TournamentStageId      INT IDENTITY(1,1) NOT NULL,
    TournamentId           INT               NOT NULL,
    StageNumber            INT               NOT NULL,
    Name                   NVARCHAR(100)     NOT NULL,
    StageType              VARCHAR(20)       NOT NULL,
    GroupCount             INT               NULL,   -- chỉ dùng khi StageType = GROUP
    TeamsAdvancingPerGroup INT               NULL,   -- chỉ dùng khi StageType = GROUP
    Status                 VARCHAR(20)       NOT NULL CONSTRAINT DF_TournamentStage_Status DEFAULT 'PENDING',
    CONSTRAINT PK_TournamentStage            PRIMARY KEY (TournamentStageId),
    CONSTRAINT UQ_TournamentStage_Number     UNIQUE (TournamentId, StageNumber),
    -- Để Bracket tham chiếu ghép được (TournamentStageId, TournamentId)
    CONSTRAINT UQ_TournamentStage_IdTour     UNIQUE (TournamentStageId, TournamentId),
    CONSTRAINT CK_TournamentStage_Type       CHECK (StageType IN ('GROUP','KNOCKOUT')),
    CONSTRAINT CK_TournamentStage_Status     CHECK (Status IN ('PENDING','ONGOING','COMPLETED')),
    CONSTRAINT CK_TournamentStage_GroupCfg   CHECK ((StageType = 'GROUP' AND GroupCount >= 1 AND TeamsAdvancingPerGroup >= 1)
                                                 OR (StageType = 'KNOCKOUT' AND GroupCount IS NULL AND TeamsAdvancingPerGroup IS NULL)),
    CONSTRAINT FK_TournamentStage_Tournament FOREIGN KEY (TournamentId) REFERENCES dbo.Tournament(TournamentId)
);
GO

/* TieBreakerRule - BRK-07. Có THỨ TỰ ƯU TIÊN, nên Priority nằm trong khoá. */
CREATE TABLE dbo.TieBreakerRule
(
    TieBreakerRuleId  INT IDENTITY(1,1) NOT NULL,
    TournamentStageId INT               NOT NULL,
    Priority          INT               NOT NULL,
    Criterion         VARCHAR(30)       NOT NULL,
    CONSTRAINT PK_TieBreakerRule          PRIMARY KEY (TieBreakerRuleId),
    CONSTRAINT UQ_TieBreakerRule_Priority UNIQUE (TournamentStageId, Priority),
    CONSTRAINT CK_TieBreakerRule_Priority CHECK (Priority >= 1),
    CONSTRAINT CK_TieBreakerRule_Criterion CHECK (Criterion IN ('HEAD_TO_HEAD','WINS','GAME_DIFFERENCE','ROUND_DIFFERENCE','POINTS','DRAW_LOT')),
    CONSTRAINT FK_TieBreakerRule_Stage    FOREIGN KEY (TournamentStageId) REFERENCES dbo.TournamentStage(TournamentStageId)
);
GO

/* RoundConfig - TOUR-05 cấu hình BO1/BO3/BO5 cho TỪNG vòng đấu */
CREATE TABLE dbo.RoundConfig
(
    RoundConfigId     INT IDENTITY(1,1) NOT NULL,
    TournamentStageId INT               NOT NULL,
    RoundNumber       INT               NOT NULL,
    BestOf            INT               NOT NULL,
    CONSTRAINT PK_RoundConfig        PRIMARY KEY (RoundConfigId),
    CONSTRAINT UQ_RoundConfig_Round  UNIQUE (TournamentStageId, RoundNumber),
    CONSTRAINT CK_RoundConfig_Round  CHECK (RoundNumber >= 1),
    CONSTRAINT CK_RoundConfig_BestOf CHECK (BestOf IN (1,3,5,7)),
    CONSTRAINT FK_RoundConfig_Stage  FOREIGN KEY (TournamentStageId) REFERENCES dbo.TournamentStage(TournamentStageId)
);
GO

/* TournamentReferee - TOUR-13 mời người dùng làm Trọng tài CỦA MỘT GIẢI.
   Trọng tài là TUỲ CHỌN. Trận không có trọng tài thì quyền phân xử
   chuyển về Ban tổ chức (kiểm tra ở tầng ứng dụng). */
CREATE TABLE dbo.TournamentReferee
(
    TournamentId INT          NOT NULL,
    UserId       INT          NOT NULL,
    Status       VARCHAR(20)  NOT NULL CONSTRAINT DF_TournamentReferee_Status DEFAULT 'INVITED',
    InvitedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_TournamentReferee_Invited DEFAULT SYSUTCDATETIME(),
    RespondedAt  DATETIME2(0) NULL,
    CONSTRAINT PK_TournamentReferee         PRIMARY KEY (TournamentId, UserId),
    CONSTRAINT CK_TournamentReferee_Status  CHECK (Status IN ('INVITED','ACCEPTED','DECLINED','REMOVED')),
    CONSTRAINT FK_TournamentReferee_Tour    FOREIGN KEY (TournamentId) REFERENCES dbo.Tournament(TournamentId),
    CONSTRAINT FK_TournamentReferee_User    FOREIGN KEY (UserId)       REFERENCES dbo.AppUser(UserId)
);
GO

/* TournamentFollow - POR-05 theo dõi giải đấu */
CREATE TABLE dbo.TournamentFollow
(
    UserId       INT          NOT NULL,
    TournamentId INT          NOT NULL,
    FollowedAt   DATETIME2(0) NOT NULL CONSTRAINT DF_TournamentFollow_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TournamentFollow       PRIMARY KEY (UserId, TournamentId),
    CONSTRAINT FK_TournamentFollow_User  FOREIGN KEY (UserId)       REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_TournamentFollow_Tour  FOREIGN KEY (TournamentId) REFERENCES dbo.Tournament(TournamentId)
);
GO

/* TournamentMediaAsset - MKT-04 bộ tài nguyên truyền thông của giải */
CREATE TABLE dbo.TournamentMediaAsset
(
    TournamentMediaAssetId INT IDENTITY(1,1) NOT NULL,
    TournamentId           INT               NOT NULL,
    AssetType              VARCHAR(30)       NOT NULL,
    FileUrl                NVARCHAR(500)     NOT NULL,
    Caption                NVARCHAR(255)     NULL,
    UploadedByUserId       INT               NOT NULL,
    UploadedAt             DATETIME2(0)      NOT NULL CONSTRAINT DF_TournamentMediaAsset_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TournamentMediaAsset          PRIMARY KEY (TournamentMediaAssetId),
    CONSTRAINT CK_TournamentMediaAsset_Type     CHECK (AssetType IN ('POSTER','BANNER','LOGO','SCHEDULE_IMAGE','RESULT_IMAGE','OTHER')),
    CONSTRAINT FK_TournamentMediaAsset_Tour     FOREIGN KEY (TournamentId)     REFERENCES dbo.Tournament(TournamentId),
    CONSTRAINT FK_TournamentMediaAsset_User     FOREIGN KEY (UploadedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* TrackingLink - MKT-03 liên kết đo hiệu quả cho cộng tác viên */
CREATE TABLE dbo.TrackingLink
(
    TrackingLinkId    INT IDENTITY(1,1) NOT NULL,
    TournamentId      INT               NOT NULL,
    Code              VARCHAR(40)       NOT NULL,
    CollaboratorName  NVARCHAR(150)     NOT NULL,
    ClickCount        INT               NOT NULL CONSTRAINT DF_TrackingLink_Click DEFAULT 0,
    RegistrationCount INT               NOT NULL CONSTRAINT DF_TrackingLink_Reg DEFAULT 0,
    CreatedAt         DATETIME2(0)      NOT NULL CONSTRAINT DF_TrackingLink_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TrackingLink        PRIMARY KEY (TrackingLinkId),
    CONSTRAINT UQ_TrackingLink_Code   UNIQUE (Code),
    CONSTRAINT FK_TrackingLink_Tour   FOREIGN KEY (TournamentId) REFERENCES dbo.Tournament(TournamentId)
);
GO

/* NewsArticle - POR-06/07/08 và ADM-08 (Scope = SYSTEM) */
CREATE TABLE dbo.NewsArticle
(
    NewsArticleId INT IDENTITY(1,1) NOT NULL,
    Title         NVARCHAR(250)     NOT NULL,
    Body          NVARCHAR(MAX)     NOT NULL,
    CoverImageUrl NVARCHAR(500)     NULL,
    AuthorUserId  INT               NOT NULL,
    Scope         VARCHAR(20)       NOT NULL,
    TournamentId  INT               NULL,
    Status        VARCHAR(20)       NOT NULL CONSTRAINT DF_NewsArticle_Status DEFAULT 'DRAFT',
    IsPinned      BIT               NOT NULL CONSTRAINT DF_NewsArticle_Pinned DEFAULT 0,
    PublishedAt   DATETIME2(0)      NULL,
    CreatedAt     DATETIME2(0)      NOT NULL CONSTRAINT DF_NewsArticle_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_NewsArticle          PRIMARY KEY (NewsArticleId),
    CONSTRAINT CK_NewsArticle_Scope    CHECK (Scope IN ('SYSTEM','TOURNAMENT')),
    CONSTRAINT CK_NewsArticle_Status   CHECK (Status IN ('DRAFT','PUBLISHED','UNPUBLISHED')),
    CONSTRAINT CK_NewsArticle_Link     CHECK ((Scope = 'TOURNAMENT' AND TournamentId IS NOT NULL)
                                           OR (Scope = 'SYSTEM'     AND TournamentId IS NULL)),
    CONSTRAINT FK_NewsArticle_Author   FOREIGN KEY (AuthorUserId) REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_NewsArticle_Tour     FOREIGN KEY (TournamentId) REFERENCES dbo.Tournament(TournamentId)
);
GO

/*===============================================================================
  NHÓM 5 - ĐĂNG KÝ & KIỂM DUYỆT          (module 7 Đăng ký)
===============================================================================*/

/* Registration - REG-01..REG-08.
   TeamId + GameId ghép khoá ngoại tới Team(TeamId, GameId) và
   TournamentId + GameId ghép tới Tournament(TournamentId, GameId)
   => KHÔNG THỂ đăng ký đội LMHT vào giải Valorant. Chặn ở mức database. */
CREATE TABLE dbo.Registration
(
    RegistrationId         INT IDENTITY(1,1) NOT NULL,
    TournamentId           INT               NOT NULL,
    TeamId                 INT               NOT NULL,
    GameId                 INT               NOT NULL,
    Status                 VARCHAR(20)       NOT NULL CONSTRAINT DF_Registration_Status DEFAULT 'DRAFT',
    -- QUY TẮC DỮ LIỆU: lưu SỐ PHIÊN BẢN thể lệ đã cam kết, không trỏ bản hiện hành.
    -- Sửa thể lệ về sau không làm thay đổi điều đội đã đồng ý (REG-02).
    AcceptedRulebookVersion INT              NULL,
    AcceptedAt             DATETIME2(0)      NULL,
    SubmittedAt            DATETIME2(0)      NULL,
    ReviewedByUserId       INT               NULL,
    ReviewedAt             DATETIME2(0)      NULL,
    DecisionReason         NVARCHAR(500)     NULL,
    -- REG-08: trạng thái lệ phí TÁCH RIÊNG khỏi trạng thái duyệt hồ sơ
    PaymentStatus          VARCHAR(25)       NOT NULL CONSTRAINT DF_Registration_Payment DEFAULT 'NOT_PAID',
    TrackingLinkId         INT               NULL,   -- MKT-03
    CreatedAt              DATETIME2(0)      NOT NULL CONSTRAINT DF_Registration_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Registration             PRIMARY KEY (RegistrationId),
    CONSTRAINT UQ_Registration_IdTour      UNIQUE (RegistrationId, TournamentId),
    CONSTRAINT CK_Registration_Status      CHECK (Status IN ('DRAFT','PENDING','APPROVED','REJECTED','WAITLISTED','WITHDRAWN')),
    CONSTRAINT CK_Registration_Payment     CHECK (PaymentStatus IN ('NOT_PAID','PENDING_VERIFICATION','VERIFIED','REFUNDED')),
    CONSTRAINT CK_Registration_RejectReason CHECK (Status <> 'REJECTED' OR DecisionReason IS NOT NULL),
    CONSTRAINT FK_Registration_Tournament  FOREIGN KEY (TournamentId, GameId) REFERENCES dbo.Tournament(TournamentId, GameId),
    CONSTRAINT FK_Registration_Team        FOREIGN KEY (TeamId, GameId)       REFERENCES dbo.Team(TeamId, GameId),
    CONSTRAINT FK_Registration_ReviewedBy  FOREIGN KEY (ReviewedByUserId)     REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_Registration_Tracking    FOREIGN KEY (TrackingLinkId)       REFERENCES dbo.TrackingLink(TrackingLinkId)
);
GO

/* QUY TẮC DỮ LIỆU: 1 đội 1 đơn đăng ký cho 1 giải, kể cả khi đơn trước bị từ chối.
   Chỉ đơn đã RÚT (WITHDRAWN) mới giải phóng chỗ.
   => Bị từ chối thì SỬA LẠI CHÍNH ĐƠN ĐÓ rồi nộp lại (REJECTED -> PENDING),
      không tạo đơn mới. Đây là cách hiểu đúng của REG-04 "nộp bổ sung". */
CREATE UNIQUE INDEX UX_Registration_OneLivePerTeam
    ON dbo.Registration(TournamentId, TeamId) WHERE Status <> 'WITHDRAWN';
GO

/* RegistrationLineup - REG-02 chốt danh sách thi đấu RIÊNG CHO GIẢI NÀY.
   Khác TeamMembership (thành viên của đội). Một đội có thể có 8 người
   nhưng chỉ đăng ký 5 chính + 2 dự bị cho giải này. */
CREATE TABLE dbo.RegistrationLineup
(
    RegistrationLineupId INT IDENTITY(1,1) NOT NULL,
    RegistrationId       INT               NOT NULL,
    TournamentId         INT               NOT NULL,
    UserId               INT               NOT NULL,
    Slot                 VARCHAR(20)       NOT NULL,
    InGameNameSnapshot   NVARCHAR(100)     NULL,   -- chụp lại tên trong game lúc nộp đơn
    IdentityDocumentId   INT               NULL,   -- ảnh thẻ sinh viên nộp cho giải này
    CONSTRAINT PK_RegistrationLineup        PRIMARY KEY (RegistrationLineupId),
    CONSTRAINT UQ_RegistrationLineup_User   UNIQUE (RegistrationId, UserId),
    CONSTRAINT CK_RegistrationLineup_Slot   CHECK (Slot IN ('STARTER','SUBSTITUTE')),
    CONSTRAINT FK_RegistrationLineup_Reg    FOREIGN KEY (RegistrationId, TournamentId) REFERENCES dbo.Registration(RegistrationId, TournamentId),
    CONSTRAINT FK_RegistrationLineup_User   FOREIGN KEY (UserId)             REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_RegistrationLineup_Doc    FOREIGN KEY (IdentityDocumentId) REFERENCES dbo.IdentityDocument(IdentityDocumentId)
);
GO

/* PaymentProof - REG-03.
   KHÔNG PHẢI BẢNG GIAO DỊCH. Chỉ lưu ảnh biên lai + số tiền ĐỘI KHAI BÁO +
   ai xác nhận, lúc nào. Hệ thống không bao giờ giữ số dư, không chuyển tiền.
   Thay biên lai mới thì biên lai cũ vẫn được giữ (IsCurrent = 0). */
CREATE TABLE dbo.PaymentProof
(
    PaymentProofId    INT IDENTITY(1,1) NOT NULL,
    RegistrationId    INT               NOT NULL,
    FileUrl           NVARCHAR(500)     NOT NULL,
    StatedAmount      DECIMAL(18,2)     NULL,
    TransferReference NVARCHAR(150)     NULL,
    UploadedByUserId  INT               NOT NULL,
    UploadedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_PaymentProof_At DEFAULT SYSUTCDATETIME(),
    Status            VARCHAR(20)       NOT NULL CONSTRAINT DF_PaymentProof_Status DEFAULT 'UPLOADED',
    VerifiedByUserId  INT               NULL,
    VerifiedAt        DATETIME2(0)      NULL,
    RejectReason      NVARCHAR(500)     NULL,
    IsCurrent         BIT               NOT NULL CONSTRAINT DF_PaymentProof_Current DEFAULT 1,
    CONSTRAINT PK_PaymentProof            PRIMARY KEY (PaymentProofId),
    CONSTRAINT CK_PaymentProof_Status     CHECK (Status IN ('UPLOADED','VERIFIED','REJECTED')),
    CONSTRAINT CK_PaymentProof_Amount     CHECK (StatedAmount IS NULL OR StatedAmount >= 0),
    CONSTRAINT FK_PaymentProof_Reg        FOREIGN KEY (RegistrationId)   REFERENCES dbo.Registration(RegistrationId),
    CONSTRAINT FK_PaymentProof_UploadedBy FOREIGN KEY (UploadedByUserId) REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_PaymentProof_VerifiedBy FOREIGN KEY (VerifiedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

CREATE UNIQUE INDEX UX_PaymentProof_CurrentPerReg
    ON dbo.PaymentProof(RegistrationId) WHERE IsCurrent = 1;
GO

/* RefundRecord - REG-10. Cũng là bảng MINH CHỨNG, không phải giao dịch. */
CREATE TABLE dbo.RefundRecord
(
    RefundRecordId   INT IDENTITY(1,1) NOT NULL,
    RegistrationId   INT               NOT NULL,
    Amount           DECIMAL(18,2)     NOT NULL,
    Reason           VARCHAR(30)       NOT NULL,
    ProofFileUrl     NVARCHAR(500)     NULL,
    RecordedByUserId INT               NOT NULL,
    RecordedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_RefundRecord_At DEFAULT SYSUTCDATETIME(),
    Note             NVARCHAR(500)     NULL,
    CONSTRAINT PK_RefundRecord           PRIMARY KEY (RefundRecordId),
    CONSTRAINT CK_RefundRecord_Amount    CHECK (Amount >= 0),
    CONSTRAINT CK_RefundRecord_Reason    CHECK (Reason IN ('REJECTED','WITHDRAWN','TOURNAMENT_CANCELLED','OTHER')),
    CONSTRAINT FK_RefundRecord_Reg       FOREIGN KEY (RegistrationId)   REFERENCES dbo.Registration(RegistrationId),
    CONSTRAINT FK_RefundRecord_RecordedBy FOREIGN KEY (RecordedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* WaitlistEntry - REG-09 hàng đợi vào trước ra trước theo Position. */
CREATE TABLE dbo.WaitlistEntry
(
    WaitlistEntryId INT IDENTITY(1,1) NOT NULL,
    TournamentId    INT               NOT NULL,
    RegistrationId  INT               NOT NULL,
    Position        INT               NOT NULL,
    Status          VARCHAR(20)       NOT NULL CONSTRAINT DF_WaitlistEntry_Status DEFAULT 'WAITING',
    AddedAt         DATETIME2(0)      NOT NULL CONSTRAINT DF_WaitlistEntry_At DEFAULT SYSUTCDATETIME(),
    PromotedAt      DATETIME2(0)      NULL,
    ConfirmDeadline DATETIME2(0)      NULL,   -- JOB-06: quá hạn thì dồn đội kế tiếp
    CONSTRAINT PK_WaitlistEntry           PRIMARY KEY (WaitlistEntryId),
    CONSTRAINT UQ_WaitlistEntry_Reg       UNIQUE (RegistrationId),
    CONSTRAINT CK_WaitlistEntry_Status    CHECK (Status IN ('WAITING','PROMOTED','EXPIRED')),
    CONSTRAINT CK_WaitlistEntry_Position  CHECK (Position >= 1),
    CONSTRAINT FK_WaitlistEntry_Tour      FOREIGN KEY (TournamentId)   REFERENCES dbo.Tournament(TournamentId),
    CONSTRAINT FK_WaitlistEntry_Reg       FOREIGN KEY (RegistrationId) REFERENCES dbo.Registration(RegistrationId)
);
GO

CREATE UNIQUE INDEX UX_WaitlistEntry_PositionWaiting
    ON dbo.WaitlistEntry(TournamentId, Position) WHERE Status = 'WAITING';
GO

/*===============================================================================
  NHÓM 6 - NHÁNH ĐẤU & TRẬN ĐẤU          (module 8 Nhánh đấu, 9 Vận hành trận)
===============================================================================*/

/* Bracket - BRK-01/02/03. Mỗi vòng có đúng 1 nhánh đấu.
   Khoá rồi là KHÔNG SINH LẠI (BR-BRK-02 / MSG-BRK-001). */
CREATE TABLE dbo.Bracket
(
    BracketId         INT IDENTITY(1,1) NOT NULL,
    TournamentStageId INT               NOT NULL,
    TournamentId      INT               NOT NULL,   -- lưu lặp có chủ đích, xem khoá ngoại ghép bên dưới
    Format            VARCHAR(30)       NOT NULL,
    RoundCount        INT               NULL,
    ByeCount          INT               NOT NULL CONSTRAINT DF_Bracket_Bye DEFAULT 0,
    SeedingMethod     VARCHAR(20)       NOT NULL CONSTRAINT DF_Bracket_Seeding DEFAULT 'REGISTRATION_ORDER',
    SeedingRandomSeed INT               NULL,   -- seed của lần bốc thăm, lưu để tái lập và đối chứng khi đội khiếu nại
    Status            VARCHAR(20)       NOT NULL CONSTRAINT DF_Bracket_Status DEFAULT 'GENERATED',
    GeneratedAt       DATETIME2(0)      NULL,
    LockedAt          DATETIME2(0)      NULL,
    LockedByUserId    INT               NULL,
    CONSTRAINT PK_Bracket          PRIMARY KEY (BracketId),
    CONSTRAINT UQ_Bracket_Stage    UNIQUE (TournamentStageId),
    -- Để Match tham chiếu ghép được (BracketId, TournamentId)
    CONSTRAINT UQ_Bracket_IdTour   UNIQUE (BracketId, TournamentId),
    CONSTRAINT CK_Bracket_Format   CHECK (Format IN ('SINGLE_ELIMINATION','DOUBLE_ELIMINATION','ROUND_ROBIN')),
    CONSTRAINT CK_Bracket_Status   CHECK (Status IN ('GENERATED','PREVIEWED','LOCKED')),
    CONSTRAINT CK_Bracket_Seeding  CHECK (SeedingMethod IN ('REGISTRATION_ORDER','RANDOM_DRAW','MANUAL')),
    CONSTRAINT CK_Bracket_SeedRnd  CHECK (SeedingRandomSeed IS NULL OR SeedingMethod = 'RANDOM_DRAW'),
    CONSTRAINT CK_Bracket_Locked   CHECK (Status <> 'LOCKED' OR (LockedAt IS NOT NULL AND LockedByUserId IS NOT NULL)),
    -- KHOÁ NGOẠI GHÉP: TournamentId của nhánh đấu buộc phải khớp giải của vòng đấu.
    CONSTRAINT FK_Bracket_Stage    FOREIGN KEY (TournamentStageId, TournamentId) REFERENCES dbo.TournamentStage(TournamentStageId, TournamentId),
    CONSTRAINT FK_Bracket_LockedBy FOREIGN KEY (LockedByUserId)    REFERENCES dbo.AppUser(UserId)
);
GO

/* BracketGroup - BRK-06/13 bảng đấu vòng tròn (chỉ dùng ở vòng GROUP) */
CREATE TABLE dbo.BracketGroup
(
    BracketGroupId INT IDENTITY(1,1) NOT NULL,
    BracketId      INT               NOT NULL,
    GroupName      NVARCHAR(20)      NOT NULL,
    CONSTRAINT PK_BracketGroup       PRIMARY KEY (BracketGroupId),
    CONSTRAINT UQ_BracketGroup_Name  UNIQUE (BracketId, GroupName),
    CONSTRAINT UQ_BracketGroup_IdBr  UNIQUE (BracketGroupId, BracketId),
    CONSTRAINT FK_BracketGroup_Br    FOREIGN KEY (BracketId) REFERENCES dbo.Bracket(BracketId)
);
GO

/* BracketSeed - BRK-02 xếp hạt giống. 1 đội 1 số hạt giống trong 1 nhánh đấu. */
CREATE TABLE dbo.BracketSeed
(
    BracketSeedId  INT IDENTITY(1,1) NOT NULL,
    BracketId      INT               NOT NULL,
    TeamId         INT               NOT NULL,
    SeedNumber     INT               NOT NULL,
    BracketGroupId INT               NULL,
    HasBye         BIT               NOT NULL CONSTRAINT DF_BracketSeed_Bye DEFAULT 0,
    CONSTRAINT PK_BracketSeed          PRIMARY KEY (BracketSeedId),
    CONSTRAINT UQ_BracketSeed_Team     UNIQUE (BracketId, TeamId),
    CONSTRAINT UQ_BracketSeed_Number   UNIQUE (BracketId, SeedNumber),
    CONSTRAINT CK_BracketSeed_Number   CHECK (SeedNumber >= 1),
    CONSTRAINT FK_BracketSeed_Bracket  FOREIGN KEY (BracketId)                 REFERENCES dbo.Bracket(BracketId),
    CONSTRAINT FK_BracketSeed_Team     FOREIGN KEY (TeamId)                    REFERENCES dbo.Team(TeamId),
    CONSTRAINT FK_BracketSeed_Group    FOREIGN KEY (BracketGroupId, BracketId) REFERENCES dbo.BracketGroup(BracketGroupId, BracketId)
);
GO

/* Match - MAT-01. Một cuộc gặp giữa 2 đội tại 1 vị trí trong nhánh đấu.
   NextMatchId / NextMatchSlot: JOB-05 đẩy đội thắng lên trận kế tiếp.
   LoserNextMatchId: nhánh thua của thể thức loại trực tiếp kép (BRK-05). */
CREATE TABLE dbo.Match
(
    MatchId          INT IDENTITY(1,1) NOT NULL,
    BracketId        INT               NOT NULL,
    TournamentId     INT               NOT NULL,   -- lưu lặp có chủ đích, xem 2 khoá ngoại ghép bên dưới
    BracketGroupId   INT               NULL,
    RoundNumber      INT               NOT NULL,
    PositionInRound  INT               NOT NULL,
    MatchCode        NVARCHAR(30)      NULL,
    BracketSide      VARCHAR(10)       NOT NULL CONSTRAINT DF_Match_Side DEFAULT 'UPPER',  -- UPPER / LOWER / GROUP / FINAL
    TeamAId          INT               NULL,
    TeamBId          INT               NULL,
    BestOf           INT               NOT NULL CONSTRAINT DF_Match_BestOf DEFAULT 1,
    ScheduledAt      DATETIME2(0)      NULL,
    CheckInOpenAt    DATETIME2(0)      NULL,      -- JOB-01
    CheckInCloseAt   DATETIME2(0)      NULL,      -- JOB-02
    ReportDeadlineAt DATETIME2(0)      NULL,      -- JOB-03
    RoomId           NVARCHAR(100)     NULL,      -- MAT-08
    RoomPassword     NVARCHAR(100)     NULL,
    RefereeUserId    INT               NULL,      -- TUỲ CHỌN, NULL = Ban tổ chức xử lý
    TeamACheckedInAt DATETIME2(0)      NULL,
    TeamBCheckedInAt DATETIME2(0)      NULL,
    TeamAScore       INT               NULL,
    TeamBScore       INT               NULL,
    WinnerTeamId     INT               NULL,
    IsThirdPlace     BIT               NOT NULL CONSTRAINT DF_Match_ThirdPlace DEFAULT 0,  -- BRK-09
    NextMatchId      INT               NULL,
    NextMatchSlot    CHAR(1)           NULL,
    LoserNextMatchId INT               NULL,
    LoserNextSlot    CHAR(1)           NULL,
    Status           VARCHAR(20)       NOT NULL CONSTRAINT DF_Match_Status DEFAULT 'SCHEDULED',
    VerifiedAt       DATETIME2(0)      NULL,
    CreatedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_Match_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Match               PRIMARY KEY (MatchId),
    CONSTRAINT UQ_Match_Position      UNIQUE (BracketId, RoundNumber, PositionInRound),
    -- Để Penalty tham chiếu ghép được (MatchId, TournamentId)
    CONSTRAINT UQ_Match_IdTour        UNIQUE (MatchId, TournamentId),
    CONSTRAINT CK_Match_BestOf        CHECK (BestOf IN (1,3,5,7)),
    CONSTRAINT CK_Match_Side          CHECK (BracketSide IN ('UPPER','LOWER','GROUP','FINAL')),
    CONSTRAINT CK_Match_Status        CHECK (Status IN ('SCHEDULED','CHECKIN_OPEN','READY','ONGOING','REPORTED','VERIFIED','DISPUTED','WALKOVER')),
    CONSTRAINT CK_Match_Slot          CHECK (NextMatchSlot  IS NULL OR NextMatchSlot  IN ('A','B')),
    CONSTRAINT CK_Match_LoserSlot     CHECK (LoserNextSlot  IS NULL OR LoserNextSlot  IN ('A','B')),
    -- Một trận không thể để 1 đội gặp chính nó
    CONSTRAINT CK_Match_TwoTeams      CHECK (TeamAId IS NULL OR TeamBId IS NULL OR TeamAId <> TeamBId),
    -- Đội thắng phải là 1 trong 2 đội của trận
    CONSTRAINT CK_Match_Winner        CHECK (WinnerTeamId IS NULL OR WinnerTeamId = TeamAId OR WinnerTeamId = TeamBId),
    -- Đã VERIFIED thì bắt buộc có tỉ số và đội thắng
    CONSTRAINT CK_Match_VerifiedData  CHECK (Status <> 'VERIFIED' OR (TeamAScore IS NOT NULL AND TeamBScore IS NOT NULL AND WinnerTeamId IS NOT NULL)),
    -- KHOÁ NGOẠI GHÉP 1: TournamentId của trận buộc phải khớp giải của nhánh đấu.
    CONSTRAINT FK_Match_Bracket       FOREIGN KEY (BracketId, TournamentId)   REFERENCES dbo.Bracket(BracketId, TournamentId),
    CONSTRAINT FK_Match_Group         FOREIGN KEY (BracketGroupId, BracketId) REFERENCES dbo.BracketGroup(BracketGroupId, BracketId),
    CONSTRAINT FK_Match_TeamA         FOREIGN KEY (TeamAId)          REFERENCES dbo.Team(TeamId),
    CONSTRAINT FK_Match_TeamB         FOREIGN KEY (TeamBId)          REFERENCES dbo.Team(TeamId),
    CONSTRAINT FK_Match_Winner        FOREIGN KEY (WinnerTeamId)     REFERENCES dbo.Team(TeamId),
    -- KHOÁ NGOẠI GHÉP 2 (TOUR-13): chỉ gán được người ĐÃ ĐƯỢC MỜI LÀM
    -- TRỌNG TÀI CỦA CHÍNH GIẢI NÀY. Không thể lấy một người bất kỳ, cũng không
    -- thể lấy trọng tài của giải khác.
    -- RefereeUserId = NULL thì SQL Server BỎ QUA kiểm tra khoá ngoại ghép, nên
    -- trận không có trọng tài vẫn hợp lệ - quyền phân xử về Ban tổ chức
    -- (xem ma trận quyền: RES-06, RES-07 đều có dấu tích ở cột Organizer).
    CONSTRAINT FK_Match_Referee       FOREIGN KEY (TournamentId, RefereeUserId) REFERENCES dbo.TournamentReferee(TournamentId, UserId),
    CONSTRAINT FK_Match_NextMatch     FOREIGN KEY (NextMatchId)      REFERENCES dbo.Match(MatchId),
    CONSTRAINT FK_Match_LoserNext     FOREIGN KEY (LoserNextMatchId) REFERENCES dbo.Match(MatchId)
);
GO

/* MatchGame - TOUR-05 BO3/BO5: tỉ số CHÍNH THỨC của từng ván, ghi sau khi xác nhận */
CREATE TABLE dbo.MatchGame
(
    MatchGameId  INT IDENTITY(1,1) NOT NULL,
    MatchId      INT               NOT NULL,
    GameNumber   INT               NOT NULL,
    TeamAScore   INT               NULL,
    TeamBScore   INT               NULL,
    WinnerTeamId INT               NULL,
    CONSTRAINT PK_MatchGame        PRIMARY KEY (MatchGameId),
    CONSTRAINT UQ_MatchGame_Number UNIQUE (MatchId, GameNumber),
    CONSTRAINT CK_MatchGame_Number CHECK (GameNumber >= 1),
    CONSTRAINT FK_MatchGame_Match  FOREIGN KEY (MatchId)      REFERENCES dbo.Match(MatchId),
    CONSTRAINT FK_MatchGame_Winner FOREIGN KEY (WinnerTeamId) REFERENCES dbo.Team(TeamId)
);
GO

/* SubstitutionRequest - MAT-05 xin thay người, MAT-06 duyệt */
CREATE TABLE dbo.SubstitutionRequest
(
    SubstitutionRequestId INT IDENTITY(1,1) NOT NULL,
    MatchId               INT               NOT NULL,
    TeamId                INT               NOT NULL,
    OutUserId             INT               NOT NULL,
    InUserId              INT               NOT NULL,
    Reason                NVARCHAR(500)     NULL,
    Status                VARCHAR(20)       NOT NULL CONSTRAINT DF_SubstitutionRequest_Status DEFAULT 'PENDING',
    RequestedByUserId     INT               NOT NULL,
    RequestedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_SubstitutionRequest_At DEFAULT SYSUTCDATETIME(),
    DecidedByUserId       INT               NULL,
    DecidedAt             DATETIME2(0)      NULL,
    DecisionReason        NVARCHAR(500)     NULL,
    CONSTRAINT PK_SubstitutionRequest           PRIMARY KEY (SubstitutionRequestId),
    CONSTRAINT CK_SubstitutionRequest_Status    CHECK (Status IN ('PENDING','APPROVED','REJECTED')),
    CONSTRAINT CK_SubstitutionRequest_Players   CHECK (OutUserId <> InUserId),
    CONSTRAINT FK_SubstitutionRequest_Match     FOREIGN KEY (MatchId)           REFERENCES dbo.Match(MatchId),
    CONSTRAINT FK_SubstitutionRequest_Team      FOREIGN KEY (TeamId)            REFERENCES dbo.Team(TeamId),
    CONSTRAINT FK_SubstitutionRequest_Out       FOREIGN KEY (OutUserId)         REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_SubstitutionRequest_In        FOREIGN KEY (InUserId)          REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_SubstitutionRequest_ReqBy     FOREIGN KEY (RequestedByUserId) REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_SubstitutionRequest_DecBy     FOREIGN KEY (DecidedByUserId)   REFERENCES dbo.AppUser(UserId)
);
GO

/* MatchChatMessage - MAT-07 phòng chờ, PEN-04 báo cáo hành vi trong phòng chat */
CREATE TABLE dbo.MatchChatMessage
(
    MatchChatMessageId BIGINT IDENTITY(1,1) NOT NULL,
    MatchId            INT                  NOT NULL,
    SenderUserId       INT                  NOT NULL,
    Body               NVARCHAR(2000)       NOT NULL,
    SentAt             DATETIME2(0)         NOT NULL CONSTRAINT DF_MatchChatMessage_At DEFAULT SYSUTCDATETIME(),
    IsReported         BIT                  NOT NULL CONSTRAINT DF_MatchChatMessage_Reported DEFAULT 0,
    ReportedByUserId   INT                  NULL,
    ReportedAt         DATETIME2(0)         NULL,
    ReportReason       NVARCHAR(500)        NULL,
    CONSTRAINT PK_MatchChatMessage          PRIMARY KEY (MatchChatMessageId),
    CONSTRAINT FK_MatchChatMessage_Match    FOREIGN KEY (MatchId)          REFERENCES dbo.Match(MatchId),
    CONSTRAINT FK_MatchChatMessage_Sender   FOREIGN KEY (SenderUserId)     REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_MatchChatMessage_ReportBy FOREIGN KEY (ReportedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/*===============================================================================
  NHÓM 7 - KẾT QUẢ & VAR                 (module 10 Kết quả & VAR)
  ĐÂY LÀ LÕI CỦA HỆ THỐNG - thay cho API nhà phát hành.
===============================================================================*/

/* MatchReport - RES-01 báo cáo tỉ số MÙ (hai đội không thấy bản của nhau).
   QUY TẮC DỮ LIỆU: ĐÚNG 2 bản cho 1 trận, mỗi đội 1 bản (UQ_MatchReport_MatchTeam).
   Nộp rồi KHÔNG SỬA, KHÔNG RÚT (trigger TR_MatchReport_Immutable, BR-RES-01 / MSG-RES-001).
   Tỉ số luôn ghi theo góc nhìn của TRẬN (TeamA / TeamB), không phải
   "đội tôi / đội bạn", để đối chiếu trực tiếp không cần đảo chiều. */
CREATE TABLE dbo.MatchReport
(
    MatchReportId      INT IDENTITY(1,1) NOT NULL,
    MatchId            INT               NOT NULL,
    TeamId             INT               NOT NULL,
    ReportedTeamAScore INT               NOT NULL,
    ReportedTeamBScore INT               NOT NULL,
    ReportedByUserId   INT               NOT NULL,
    SubmittedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_MatchReport_At DEFAULT SYSUTCDATETIME(),
    Status             VARCHAR(20)       NOT NULL CONSTRAINT DF_MatchReport_Status DEFAULT 'SUBMITTED',
    CONSTRAINT PK_MatchReport            PRIMARY KEY (MatchReportId),
    CONSTRAINT UQ_MatchReport_MatchTeam  UNIQUE (MatchId, TeamId),
    CONSTRAINT CK_MatchReport_Status     CHECK (Status IN ('SUBMITTED','MATCHED','CONFLICTING','MISSING')),
    CONSTRAINT CK_MatchReport_Score      CHECK (ReportedTeamAScore >= 0 AND ReportedTeamBScore >= 0),
    CONSTRAINT FK_MatchReport_Match      FOREIGN KEY (MatchId)          REFERENCES dbo.Match(MatchId),
    CONSTRAINT FK_MatchReport_Team       FOREIGN KEY (TeamId)           REFERENCES dbo.Team(TeamId),
    CONSTRAINT FK_MatchReport_ReportedBy FOREIGN KEY (ReportedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* MatchReportGame - tỉ số từng ván do chính đội đó khai (BO3/BO5) */
CREATE TABLE dbo.MatchReportGame
(
    MatchReportGameId  INT IDENTITY(1,1) NOT NULL,
    MatchReportId      INT               NOT NULL,
    GameNumber         INT               NOT NULL,
    ReportedTeamAScore INT               NOT NULL,
    ReportedTeamBScore INT               NOT NULL,
    CONSTRAINT PK_MatchReportGame        PRIMARY KEY (MatchReportGameId),
    CONSTRAINT UQ_MatchReportGame_Number UNIQUE (MatchReportId, GameNumber),
    CONSTRAINT CK_MatchReportGame_Number CHECK (GameNumber >= 1),
    CONSTRAINT CK_MatchReportGame_Score  CHECK (ReportedTeamAScore >= 0 AND ReportedTeamBScore >= 0),
    CONSTRAINT FK_MatchReportGame_Report FOREIGN KEY (MatchReportId) REFERENCES dbo.MatchReport(MatchReportId)
);
GO

/* EvidenceFile - RES-01/04 ảnh chụp màn hình hoặc file phát lại.
   QUY TẮC DỮ LIỆU: KHÔNG BAO GIỜ xoá khi giải chưa COMPLETED
   (trigger TR_EvidenceFile_NoDelete). Được công khai trên trang trận đấu. */
CREATE TABLE dbo.EvidenceFile
(
    EvidenceFileId   INT IDENTITY(1,1) NOT NULL,
    MatchReportId    INT               NOT NULL,
    FileUrl          NVARCHAR(500)     NOT NULL,
    FileType         VARCHAR(20)       NOT NULL,
    ContentType      VARCHAR(100)      NULL,
    FileSizeBytes    BIGINT            NULL,
    UploadedByUserId INT               NOT NULL,
    UploadedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_EvidenceFile_At DEFAULT SYSUTCDATETIME(),
    IsPublished      BIT               NOT NULL CONSTRAINT DF_EvidenceFile_Pub DEFAULT 0,
    CONSTRAINT PK_EvidenceFile          PRIMARY KEY (EvidenceFileId),
    CONSTRAINT CK_EvidenceFile_Type     CHECK (FileType IN ('SCREENSHOT','REPLAY','VIDEO','OTHER')),
    CONSTRAINT FK_EvidenceFile_Report   FOREIGN KEY (MatchReportId)    REFERENCES dbo.MatchReport(MatchReportId),
    CONSTRAINT FK_EvidenceFile_Uploader FOREIGN KEY (UploadedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* Dispute - RES-05/06/07. Sinh ra từ 3 nguồn:
     CONFLICTING_REPORTS - 2 bản báo cáo lệch nhau (JOB-04)
     FLAG                - có người cắm cờ nghi vấn gian lận (RES-05)
     TIMEOUT             - hết hạn báo cáo mà vẫn thiếu bản (JOB-03) */
CREATE TABLE dbo.Dispute
(
    DisputeId         INT IDENTITY(1,1) NOT NULL,
    MatchId           INT               NOT NULL,
    Cause             VARCHAR(30)       NOT NULL,
    RaisedByUserId    INT               NULL,   -- NULL = hệ thống tự sinh
    RaisedAt          DATETIME2(0)      NOT NULL CONSTRAINT DF_Dispute_At DEFAULT SYSUTCDATETIME(),
    AssignedToUserId  INT               NULL,
    Status            VARCHAR(20)       NOT NULL CONSTRAINT DF_Dispute_Status DEFAULT 'OPEN',
    ForcedTeamAScore  INT               NULL,
    ForcedTeamBScore  INT               NULL,
    ForcedWinnerTeamId INT              NULL,
    DecisionReason    NVARCHAR(1000)    NULL,
    DecidedByUserId   INT               NULL,
    DecidedAt         DATETIME2(0)      NULL,
    ClosedAt          DATETIME2(0)      NULL,
    CONSTRAINT PK_Dispute             PRIMARY KEY (DisputeId),
    CONSTRAINT CK_Dispute_Cause       CHECK (Cause IN ('CONFLICTING_REPORTS','FLAG','TIMEOUT')),
    CONSTRAINT CK_Dispute_Status      CHECK (Status IN ('OPEN','UNDER_REVIEW','DECIDED','CLOSED')),
    -- Đã phán quyết thì BẮT BUỘC có tỉ số áp đặt + lý do văn bản + người quyết
    CONSTRAINT CK_Dispute_Decision    CHECK (Status NOT IN ('DECIDED','CLOSED')
                                          OR (ForcedTeamAScore IS NOT NULL AND ForcedTeamBScore IS NOT NULL
                                              AND DecisionReason IS NOT NULL AND DecidedByUserId IS NOT NULL)),
    CONSTRAINT FK_Dispute_Match       FOREIGN KEY (MatchId)            REFERENCES dbo.Match(MatchId),
    CONSTRAINT FK_Dispute_RaisedBy    FOREIGN KEY (RaisedByUserId)     REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_Dispute_AssignedTo  FOREIGN KEY (AssignedToUserId)   REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_Dispute_DecidedBy   FOREIGN KEY (DecidedByUserId)    REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_Dispute_Winner      FOREIGN KEY (ForcedWinnerTeamId) REFERENCES dbo.Team(TeamId)
);
GO

-- 1 trận chỉ có 1 vụ tranh chấp đang mở
CREATE UNIQUE INDEX UX_Dispute_LivePerMatch
    ON dbo.Dispute(MatchId) WHERE Status <> 'CLOSED';
GO

/* Appeal - RES-08. 1 phán quyết chỉ được khiếu nại ĐÚNG 1 LẦN. */
CREATE TABLE dbo.Appeal
(
    AppealId          INT IDENTITY(1,1) NOT NULL,
    DisputeId         INT               NOT NULL,
    TeamId            INT               NOT NULL,
    Ground            NVARCHAR(2000)    NOT NULL,
    SubmittedByUserId INT               NOT NULL,
    SubmittedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_Appeal_At DEFAULT SYSUTCDATETIME(),
    Status            VARCHAR(20)       NOT NULL CONSTRAINT DF_Appeal_Status DEFAULT 'SUBMITTED',
    ReviewedByUserId  INT               NULL,
    ReviewedAt        DATETIME2(0)      NULL,
    OutcomeReason     NVARCHAR(1000)    NULL,
    CONSTRAINT PK_Appeal             PRIMARY KEY (AppealId),
    CONSTRAINT UQ_Appeal_Dispute     UNIQUE (DisputeId),
    CONSTRAINT CK_Appeal_Status      CHECK (Status IN ('SUBMITTED','UNDER_REVIEW','UPHELD','DISMISSED')),
    CONSTRAINT CK_Appeal_Outcome     CHECK (Status NOT IN ('UPHELD','DISMISSED') OR OutcomeReason IS NOT NULL),
    CONSTRAINT FK_Appeal_Dispute     FOREIGN KEY (DisputeId)         REFERENCES dbo.Dispute(DisputeId),
    CONSTRAINT FK_Appeal_Team        FOREIGN KEY (TeamId)            REFERENCES dbo.Team(TeamId),
    CONSTRAINT FK_Appeal_SubmittedBy FOREIGN KEY (SubmittedByUserId) REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_Appeal_ReviewedBy  FOREIGN KEY (ReviewedByUserId)  REFERENCES dbo.AppUser(UserId)
);
GO

/*===============================================================================
  NHÓM 8 - KỶ LUẬT                       (module 11 Kỷ luật)
===============================================================================*/

/* Penalty - PEN-01/02/03.
   QUY TẮC DỮ LIỆU: KHÔNG BAO GIỜ xoá. Cấp nhầm thì tạo bản ghi thứ 2 có
   CancelsPenaltyId trỏ về bản ghi cũ. Bản gốc vẫn hiện để giữ nguyên dấu vết. */
CREATE TABLE dbo.Penalty
(
    PenaltyId        INT IDENTITY(1,1) NOT NULL,
    TeamId           INT               NOT NULL,
    TournamentId     INT               NOT NULL,
    MatchId          INT               NULL,
    Severity         VARCHAR(20)       NOT NULL,
    Points           INT               NOT NULL CONSTRAINT DF_Penalty_Points DEFAULT 1,
    Reason           NVARCHAR(1000)    NOT NULL,
    IssuedByUserId   INT               NOT NULL,
    IssuedAt         DATETIME2(0)      NOT NULL CONSTRAINT DF_Penalty_At DEFAULT SYSUTCDATETIME(),
    Status           VARCHAR(20)       NOT NULL CONSTRAINT DF_Penalty_Status DEFAULT 'ACTIVE',
    CancelsPenaltyId INT               NULL,
    CancelReason     NVARCHAR(500)     NULL,
    CONSTRAINT PK_Penalty            PRIMARY KEY (PenaltyId),
    CONSTRAINT CK_Penalty_Severity   CHECK (Severity IN ('WARNING','MINOR','MAJOR','DISQUALIFICATION','CANCELLATION')),
    CONSTRAINT CK_Penalty_Status     CHECK (Status IN ('ISSUED','ACTIVE','EXPIRED','CANCELLED')),
    CONSTRAINT CK_Penalty_Points     CHECK (Points >= 0),
    CONSTRAINT CK_Penalty_Cancel     CHECK ((Severity = 'CANCELLATION' AND CancelsPenaltyId IS NOT NULL AND CancelReason IS NOT NULL)
                                         OR (Severity <> 'CANCELLATION' AND CancelsPenaltyId IS NULL)),
    CONSTRAINT FK_Penalty_Team       FOREIGN KEY (TeamId)           REFERENCES dbo.Team(TeamId),
    CONSTRAINT FK_Penalty_Tournament FOREIGN KEY (TournamentId)     REFERENCES dbo.Tournament(TournamentId),
    -- KHOÁ NGOẠI GHÉP: thẻ phạt của giải này không thể trỏ tới trận của giải khác.
    CONSTRAINT FK_Penalty_Match      FOREIGN KEY (MatchId, TournamentId) REFERENCES dbo.Match(MatchId, TournamentId),
    CONSTRAINT FK_Penalty_IssuedBy   FOREIGN KEY (IssuedByUserId)   REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_Penalty_Cancels    FOREIGN KEY (CancelsPenaltyId) REFERENCES dbo.Penalty(PenaltyId)
);
GO

/* 1 thẻ phạt chỉ được huỷ ĐÚNG 1 LẦN.
   Phải dùng chỉ mục có điều kiện chứ KHÔNG dùng ràng buộc UNIQUE: trong SQL Server,
   UNIQUE coi mọi NULL là bằng nhau nên chỉ cho phép ĐÚNG 1 dòng NULL - mà
   hầu hết thẻ phạt đều có CancelsPenaltyId = NULL. */
CREATE UNIQUE INDEX UX_Penalty_CancelsOnce
    ON dbo.Penalty(CancelsPenaltyId) WHERE CancelsPenaltyId IS NOT NULL;
GO

/*===============================================================================
  NHÓM 9 - THÔNG BÁO                     (module 12 Thông báo)
===============================================================================*/

/* NotificationTemplate - NOT-04. Mã MSG-<MODULE>-nnn của SRS mục 5.2 nằm ở đây,
   để đổi lời văn 1 chỗ là đổi cho toàn hệ thống. */
CREATE TABLE dbo.NotificationTemplate
(
    NotificationTemplateId INT IDENTITY(1,1) NOT NULL,
    Code                   VARCHAR(20)       NOT NULL,
    MessageType            VARCHAR(30)       NOT NULL,
    Context                NVARCHAR(300)     NULL,
    SubjectTemplate        NVARCHAR(300)     NULL,
    BodyTemplate           NVARCHAR(1000)    NOT NULL,  -- lời văn tiếng Việt hiển thị cho người dùng
    IsActive               BIT               NOT NULL CONSTRAINT DF_NotificationTemplate_Active DEFAULT 1,
    UpdatedAt              DATETIME2(0)      NULL,
    CONSTRAINT PK_NotificationTemplate        PRIMARY KEY (NotificationTemplateId),
    CONSTRAINT UQ_NotificationTemplate_Code   UNIQUE (Code)
);
GO

/* Notification - NOT-01/02/03.
   RelatedObjectType / RelatedObjectId có tính đa hình (trỏ tới Match,
   Registration, Dispute...) nên KHÔNG đặt khoá ngoại - kiểm ở tầng ứng dụng. */
CREATE TABLE dbo.Notification
(
    NotificationId    BIGINT IDENTITY(1,1) NOT NULL,
    RecipientUserId   INT                  NOT NULL,
    TemplateCode      VARCHAR(20)          NULL,
    NotificationType  VARCHAR(50)          NOT NULL,
    Subject           NVARCHAR(300)        NULL,
    Body              NVARCHAR(2000)       NOT NULL,
    RelatedObjectType VARCHAR(50)          NULL,
    RelatedObjectId   INT                  NULL,
    Channel           VARCHAR(20)          NOT NULL,
    Status            VARCHAR(20)          NOT NULL CONSTRAINT DF_Notification_Status DEFAULT 'QUEUED',
    QueuedAt          DATETIME2(0)         NOT NULL CONSTRAINT DF_Notification_At DEFAULT SYSUTCDATETIME(),
    SentAt            DATETIME2(0)         NULL,
    ReadAt            DATETIME2(0)         NULL,
    FailureReason     NVARCHAR(500)        NULL,
    CONSTRAINT PK_Notification           PRIMARY KEY (NotificationId),
    CONSTRAINT CK_Notification_Channel   CHECK (Channel IN ('IN_APP','EMAIL')),
    CONSTRAINT CK_Notification_Status    CHECK (Status IN ('QUEUED','SENT','READ','FAILED')),
    CONSTRAINT FK_Notification_Recipient FOREIGN KEY (RecipientUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/*===============================================================================
  NHÓM 10 - XẾP HẠNG                     (module 14 Truyền thông & Xếp hạng)
===============================================================================*/

/* LeaderboardEntry - RNK-01/02. JOB-14 tính lại mỗi khi 1 giải COMPLETED. */
CREATE TABLE dbo.LeaderboardEntry
(
    LeaderboardEntryId INT IDENTITY(1,1) NOT NULL,
    TeamId             INT               NOT NULL,
    GameId             INT               NOT NULL,
    Season             NVARCHAR(50)      NOT NULL,
    Points             INT               NOT NULL CONSTRAINT DF_LeaderboardEntry_Points DEFAULT 0,
    TournamentsPlayed  INT               NOT NULL CONSTRAINT DF_LeaderboardEntry_Played DEFAULT 0,
    TitlesWon          INT               NOT NULL CONSTRAINT DF_LeaderboardEntry_Titles DEFAULT 0,
    LastUpdatedAt      DATETIME2(0)      NOT NULL CONSTRAINT DF_LeaderboardEntry_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_LeaderboardEntry        PRIMARY KEY (LeaderboardEntryId),
    CONSTRAINT UQ_LeaderboardEntry_Key    UNIQUE (TeamId, GameId, Season),
    CONSTRAINT FK_LeaderboardEntry_Team   FOREIGN KEY (TeamId, GameId) REFERENCES dbo.Team(TeamId, GameId)
);
GO

/* TeamTitle - RNK-03 trao danh hiệu khi giải kết thúc */
CREATE TABLE dbo.TeamTitle
(
    TeamTitleId  INT IDENTITY(1,1) NOT NULL,
    TeamId       INT               NOT NULL,
    TournamentId INT               NOT NULL,
    Rank         INT               NOT NULL,
    TitleName    NVARCHAR(150)     NOT NULL,
    AwardedAt    DATETIME2(0)      NOT NULL CONSTRAINT DF_TeamTitle_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TeamTitle        PRIMARY KEY (TeamTitleId),
    CONSTRAINT UQ_TeamTitle_Rank   UNIQUE (TournamentId, Rank),
    CONSTRAINT UQ_TeamTitle_Team   UNIQUE (TournamentId, TeamId),
    CONSTRAINT CK_TeamTitle_Rank   CHECK (Rank >= 1),
    CONSTRAINT FK_TeamTitle_Team   FOREIGN KEY (TeamId)       REFERENCES dbo.Team(TeamId),
    CONSTRAINT FK_TeamTitle_Tour   FOREIGN KEY (TournamentId) REFERENCES dbo.Tournament(TournamentId)
);
GO

/*===============================================================================
  NHÓM 11 - NHẬT KÝ THAO TÁC             (module 3 - ADM-07)
===============================================================================*/

/* AuditLog - ADM-07. QUY TẮC DỮ LIỆU: CHỈ THÊM, KHÔNG SỬA KHÔNG XOÁ.
   Không ai được sửa hay xoá, KỂ CẢ Quản trị hệ thống
   (trigger TR_AuditLog_Immutable chặn ở mức database).
   Đây là bảng làm cho một quyết định của Ban tổ chức có thể bảo vệ được. */
CREATE TABLE dbo.AuditLog
(
    AuditLogId   BIGINT IDENTITY(1,1) NOT NULL,
    ActorUserId  INT                  NULL,   -- NULL = hệ thống / tác vụ nền
    ActionType   VARCHAR(50)          NOT NULL,
    ObjectType   VARCHAR(50)          NOT NULL,
    ObjectId     INT                  NULL,
    TournamentId INT                  NULL,
    ValueBefore  NVARCHAR(MAX)        NULL,
    ValueAfter   NVARCHAR(MAX)        NULL,
    Reason       NVARCHAR(1000)       NULL,
    IpAddress    VARCHAR(45)          NULL,
    OccurredAt   DATETIME2(0)         NOT NULL CONSTRAINT DF_AuditLog_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AuditLog        PRIMARY KEY (AuditLogId),
    CONSTRAINT FK_AuditLog_Actor  FOREIGN KEY (ActorUserId)  REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_AuditLog_Tour   FOREIGN KEY (TournamentId) REFERENCES dbo.Tournament(TournamentId)
);
GO

/*===============================================================================
  NĂM MODULE MỚI - bổ sung 17/10/2026 cho danh mục 144 chức năng
  -------------------------------------------------------------------------------
  Phần này giữ nguyên 50 bảng bên trên không sửa một dòng nào, để lược đồ cũ
  còn đối chiếu được với Report 3 và với các mã BR/MSG đã kiểm. Các cột cần
  thêm vào bảng cũ nằm ở cuối phần này dưới dạng ALTER TABLE, có ghi lý do.

    15. Chủ thể tổ chức         ORG-01..ORG-09
    16. Tài trợ & Quảng cáo     SPN-01..SPN-02
    17. Luật chơi theo tựa game GRL-02..GRL-07  (GRL-01 là bảng Ruleset ở trên)
    18. Tỉ số trực tiếp         LIVE-01..LIVE-04 (dùng bảng MatchGame có sẵn)
    19. Liêm chính thi đấu      ITG-01..ITG-11
===============================================================================*/

/* RulesetMap - GRL-02 kho bản đồ của một bộ luật.
   Trạng thái còn dùng hay đã loại giữ bằng IsActive, không xoá cứng, vì các
   trận đã đấu trên bản đồ đó vẫn phải tra được tên. */
CREATE TABLE dbo.RulesetMap
(
    RulesetMapId INT IDENTITY(1,1) NOT NULL,
    RulesetId    INT               NOT NULL,
    MapName      NVARCHAR(100)     NOT NULL,
    ImageUrl     NVARCHAR(500)     NULL,
    IsActive     BIT               NOT NULL CONSTRAINT DF_RulesetMap_Active DEFAULT 1,
    CONSTRAINT PK_RulesetMap         PRIMARY KEY (RulesetMapId),
    CONSTRAINT UQ_RulesetMap_Name    UNIQUE (RulesetId, MapName),
    CONSTRAINT FK_RulesetMap_Ruleset FOREIGN KEY (RulesetId) REFERENCES dbo.Ruleset(RulesetId)
);
GO

/* RulesetPosition - GRL-03 vị trí trong đội theo bộ luật.
   Trước đây hệ thống chỉ phân biệt chính thức / dự bị (RegistrationLineup.Slot);
   bảng này cho khai đường trên, đi rừng, đường giữa, xạ thủ, hỗ trợ... */
CREATE TABLE dbo.RulesetPosition
(
    RulesetPositionId INT IDENTITY(1,1) NOT NULL,
    RulesetId         INT               NOT NULL,
    PositionName      NVARCHAR(100)     NOT NULL,
    DisplayOrder      INT               NOT NULL CONSTRAINT DF_RulesetPosition_Order DEFAULT 1,
    CONSTRAINT PK_RulesetPosition         PRIMARY KEY (RulesetPositionId),
    CONSTRAINT UQ_RulesetPosition_Name    UNIQUE (RulesetId, PositionName),
    CONSTRAINT CK_RulesetPosition_Order   CHECK (DisplayOrder >= 1),
    CONSTRAINT FK_RulesetPosition_Ruleset FOREIGN KEY (RulesetId) REFERENCES dbo.Ruleset(RulesetId)
);
GO

/* MatchPickBan - GRL-07. Hai đội cấm/chọn bản đồ Ở NGOÀI hệ thống, trọng tài
   nhập lại kết quả vào đây. Lưu công khai trên trang trận và dùng làm bằng
   chứng khi có tranh chấp, nên có người nhập và thời điểm nhập. */
CREATE TABLE dbo.MatchPickBan
(
    MatchPickBanId   INT IDENTITY(1,1) NOT NULL,
    MatchId          INT               NOT NULL,
    Sequence         INT               NOT NULL,
    ActionType       VARCHAR(10)       NOT NULL,
    TeamId           INT               NOT NULL,
    RulesetMapId     INT               NOT NULL,
    RecordedByUserId INT               NOT NULL,
    RecordedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_MatchPickBan_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_MatchPickBan            PRIMARY KEY (MatchPickBanId),
    CONSTRAINT UQ_MatchPickBan_Seq        UNIQUE (MatchId, Sequence),
    CONSTRAINT CK_MatchPickBan_Action     CHECK (ActionType IN ('PICK','BAN')),
    CONSTRAINT CK_MatchPickBan_Seq        CHECK (Sequence >= 1),
    CONSTRAINT FK_MatchPickBan_Match      FOREIGN KEY (MatchId)          REFERENCES dbo.Match(MatchId),
    CONSTRAINT FK_MatchPickBan_Team       FOREIGN KEY (TeamId)           REFERENCES dbo.Team(TeamId),
    CONSTRAINT FK_MatchPickBan_Map        FOREIGN KEY (RulesetMapId)     REFERENCES dbo.RulesetMap(RulesetMapId),
    CONSTRAINT FK_MatchPickBan_RecordedBy FOREIGN KEY (RecordedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* Organization - ORG-01 nộp hồ sơ lập đơn vị, ORG-02 xét duyệt, ORG-09 đình chỉ.
   Ba loại đơn vị theo ORG-01: câu lạc bộ cấp trường, công ty tổ chức sự kiện,
   cá nhân. Mỗi loại yêu cầu bộ giấy tờ riêng (bảng OrganizationDocument).
   ParentUnit là đơn vị chủ quản, hiện trên hồ sơ công khai ORG-03.
   RÀNG BUỘC: từ chối (ORG-02) và đình chỉ (ORG-09) đều BẮT BUỘC kèm lý do. */
CREATE TABLE dbo.Organization
(
    OrganizationId   INT IDENTITY(1,1) NOT NULL,
    Name             NVARCHAR(200)     NOT NULL,
    OrgType          VARCHAR(20)       NOT NULL,
    Description      NVARCHAR(MAX)     NULL,
    LogoUrl          NVARCHAR(500)     NULL,
    ParentUnit       NVARCHAR(200)     NULL,
    ContactEmail     NVARCHAR(256)     NULL,
    ContactPhone     VARCHAR(20)       NULL,
    Status           VARCHAR(20)       NOT NULL CONSTRAINT DF_Organization_Status DEFAULT 'PENDING',
    OwnerUserId      INT               NOT NULL,
    CreatedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_Organization_At DEFAULT SYSUTCDATETIME(),
    ReviewedByUserId INT               NULL,
    ReviewedAt       DATETIME2(0)      NULL,
    ReviewNote       NVARCHAR(500)     NULL,
    CONSTRAINT PK_Organization          PRIMARY KEY (OrganizationId),
    CONSTRAINT UQ_Organization_Name     UNIQUE (Name),
    CONSTRAINT CK_Organization_Type     CHECK (OrgType IN ('SCHOOL_CLUB','EVENT_COMPANY','INDIVIDUAL')),
    CONSTRAINT CK_Organization_Status   CHECK (Status IN ('PENDING','APPROVED','REJECTED','SUSPENDED')),
    CONSTRAINT CK_Organization_Reason   CHECK (Status NOT IN ('REJECTED','SUSPENDED') OR ReviewNote IS NOT NULL),
    CONSTRAINT FK_Organization_Owner    FOREIGN KEY (OwnerUserId)      REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_Organization_Reviewer FOREIGN KEY (ReviewedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* OrganizationDocument - ORG-01/ORG-02 giấy tờ minh chứng theo loại đơn vị.
   Quản trị viên đối chiếu từng giấy tờ ở đây trước khi quyết định. */
CREATE TABLE dbo.OrganizationDocument
(
    OrganizationDocumentId INT IDENTITY(1,1) NOT NULL,
    OrganizationId         INT               NOT NULL,
    DocType                VARCHAR(40)       NOT NULL,
    FileUrl                NVARCHAR(500)     NOT NULL,
    UploadedAt             DATETIME2(0)      NOT NULL CONSTRAINT DF_OrgDoc_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_OrganizationDocument     PRIMARY KEY (OrganizationDocumentId),
    CONSTRAINT FK_OrganizationDocument_Org FOREIGN KEY (OrganizationId) REFERENCES dbo.Organization(OrganizationId)
);
GO

/* OrganizationMember - ORG-04 mời thành viên, ORG-05 gán vai trò, ORG-06 bàn giao.
   Theo ORG-04: MỘT người thuộc nhiều đơn vị được, một đơn vị có nhiều người,
   nên chỉ khoá trùng theo cặp (đơn vị, người) khi đang hoạt động.
   Theo ORG-06: mỗi đơn vị chỉ có ĐÚNG MỘT chủ nhiệm đang hoạt động - cưỡng chế
   bằng chỉ mục duy nhất có điều kiện ở dưới. */
CREATE TABLE dbo.OrganizationMember
(
    OrganizationMemberId INT IDENTITY(1,1) NOT NULL,
    OrganizationId       INT               NOT NULL,
    UserId               INT               NOT NULL,
    OrgRole              VARCHAR(20)       NOT NULL,
    InvitedByUserId      INT               NULL,
    JoinedAt             DATETIME2(0)      NOT NULL CONSTRAINT DF_OrgMember_At DEFAULT SYSUTCDATETIME(),
    IsActive             BIT               NOT NULL CONSTRAINT DF_OrgMember_Active DEFAULT 1,
    CONSTRAINT PK_OrganizationMember         PRIMARY KEY (OrganizationMemberId),
    CONSTRAINT CK_OrganizationMember_Role    CHECK (OrgRole IN ('OWNER','OPERATOR','MEDIA')),
    CONSTRAINT FK_OrganizationMember_Org     FOREIGN KEY (OrganizationId)  REFERENCES dbo.Organization(OrganizationId),
    CONSTRAINT FK_OrganizationMember_User    FOREIGN KEY (UserId)          REFERENCES dbo.AppUser(UserId),
    CONSTRAINT FK_OrganizationMember_Inviter FOREIGN KEY (InvitedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

CREATE UNIQUE INDEX UX_OrganizationMember_Active
    ON dbo.OrganizationMember(OrganizationId, UserId) WHERE IsActive = 1;
CREATE UNIQUE INDEX UX_OrganizationMember_OneOwner
    ON dbo.OrganizationMember(OrganizationId) WHERE OrgRole = 'OWNER' AND IsActive = 1;
GO

/* OrganizationMetric - ORG-07 tính bốn chỉ số rồi hiện lên hồ sơ đơn vị.
   Là bảng KẾT QUẢ TÍNH SẴN, không phải nguồn sự thật: mỗi lần tác vụ nền chạy
   thì ghi một dòng mới, nên còn xem được chỉ số thay đổi theo thời gian. */
CREATE TABLE dbo.OrganizationMetric
(
    OrganizationMetricId INT IDENTITY(1,1) NOT NULL,
    OrganizationId       INT               NOT NULL,
    CompletionRate       DECIMAL(5,2)      NOT NULL,
    DisputeRate          DECIMAL(5,2)      NOT NULL,
    AvgDisputeHours      DECIMAL(9,2)      NULL,
    RefundCount          INT               NOT NULL CONSTRAINT DF_OrgMetric_Refund DEFAULT 0,
    ComputedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_OrgMetric_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_OrganizationMetric     PRIMARY KEY (OrganizationMetricId),
    CONSTRAINT CK_OrgMetric_Rate         CHECK (CompletionRate BETWEEN 0 AND 100 AND DisputeRate BETWEEN 0 AND 100),
    CONSTRAINT CK_OrgMetric_Refund       CHECK (RefundCount >= 0),
    CONSTRAINT FK_OrganizationMetric_Org FOREIGN KEY (OrganizationId) REFERENCES dbo.Organization(OrganizationId)
);
GO

/* Sponsor - SPN-01 kho nhà tài trợ DÙNG CHUNG cho nhiều giải của một đơn vị. */
CREATE TABLE dbo.Sponsor
(
    SponsorId      INT IDENTITY(1,1) NOT NULL,
    OrganizationId INT               NOT NULL,
    Name           NVARCHAR(200)     NOT NULL,
    LogoUrl        NVARCHAR(500)     NULL,
    WebsiteUrl     NVARCHAR(500)     NULL,
    ContactInfo    NVARCHAR(300)     NULL,
    DefaultTier    VARCHAR(20)       NULL,
    IsActive       BIT               NOT NULL CONSTRAINT DF_Sponsor_Active DEFAULT 1,
    CONSTRAINT PK_Sponsor      PRIMARY KEY (SponsorId),
    CONSTRAINT UQ_Sponsor_Name UNIQUE (OrganizationId, Name),
    CONSTRAINT CK_Sponsor_Tier CHECK (DefaultTier IS NULL OR DefaultTier IN ('DIAMOND','GOLD','SILVER','BRONZE','PARTNER')),
    CONSTRAINT FK_Sponsor_Org  FOREIGN KEY (OrganizationId) REFERENCES dbo.Organization(OrganizationId)
);
GO

/* TournamentSponsor - SPN-02. Theo SPN-02: MỘT nhà tài trợ xuất hiện ở nhiều
   giải VỚI HẠNG KHÁC NHAU, nên hạng nằm ở đây chứ không ở bảng Sponsor.
   Kích thước logo trên trang giải suy ra từ Tier. */
CREATE TABLE dbo.TournamentSponsor
(
    TournamentSponsorId INT IDENTITY(1,1) NOT NULL,
    TournamentId        INT               NOT NULL,
    SponsorId           INT               NOT NULL,
    Tier                VARCHAR(20)       NOT NULL,
    DisplayOrder        INT               NOT NULL CONSTRAINT DF_TourSponsor_Order DEFAULT 1,
    CONSTRAINT PK_TournamentSponsor      PRIMARY KEY (TournamentSponsorId),
    CONSTRAINT UQ_TournamentSponsor      UNIQUE (TournamentId, SponsorId),
    CONSTRAINT CK_TournamentSponsor_Tier CHECK (Tier IN ('DIAMOND','GOLD','SILVER','BRONZE','PARTNER')),
    CONSTRAINT FK_TournamentSponsor_Tour FOREIGN KEY (TournamentId) REFERENCES dbo.Tournament(TournamentId),
    CONSTRAINT FK_TournamentSponsor_Spon FOREIGN KEY (SponsorId)    REFERENCES dbo.Sponsor(SponsorId)
);
GO

/* IntegrityThreshold - ITG-06. Ngưỡng là CHÍNH SÁCH CỦA BAN TỔ CHỨC, đặt theo
   từng giải, không phải hằng số chôn trong mã nguồn. */
CREATE TABLE dbo.IntegrityThreshold
(
    IntegrityThresholdId INT IDENTITY(1,1) NOT NULL,
    TournamentId         INT               NOT NULL,
    MinScore             INT               NOT NULL CONSTRAINT DF_IntegThreshold_Score DEFAULT 3,
    MinSignalCount       INT               NOT NULL CONSTRAINT DF_IntegThreshold_Count DEFAULT 2,
    UpdatedByUserId      INT               NULL,
    UpdatedAt            DATETIME2(0)      NULL,
    CONSTRAINT PK_IntegrityThreshold       PRIMARY KEY (IntegrityThresholdId),
    CONSTRAINT UQ_IntegrityThreshold_Tour  UNIQUE (TournamentId),
    CONSTRAINT CK_IntegrityThreshold_Score CHECK (MinScore >= 1 AND MinSignalCount >= 1),
    CONSTRAINT FK_IntegrityThreshold_Tour  FOREIGN KEY (TournamentId)    REFERENCES dbo.Tournament(TournamentId),
    CONSTRAINT FK_IntegrityThreshold_User  FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.AppUser(UserId)
);
GO

/* IntegritySignal - ITG-01 bốn dấu hiệu, CHỈ chạy trên dữ liệu hệ thống đã có.
   KHÔNG phân tích ảnh, KHÔNG đụng vào máy người chơi, và cũng không lấy dữ
   liệu từ nhà phát hành game (ràng buộc nền tảng LI-01).
     REPORT_TIME_GAP        - hiệu thời gian nộp hai báo cáo lệch khỏi phân bố giải
     REPEATED_SCORE_PATTERN - cặp đội gặp nhiều lần mà lần nào cũng đúng một mẫu tỉ số
     NO_ROOM_CODE_IN_CHAT   - đủ hai báo cáo mà log chat không hề trao mã phòng
     REUSED_EVIDENCE        - file bằng chứng trùng xuất hiện ở nhiều trận */
CREATE TABLE dbo.IntegritySignal
(
    IntegritySignalId INT IDENTITY(1,1) NOT NULL,
    MatchId           INT               NOT NULL,
    SignalType        VARCHAR(30)       NOT NULL,
    Weight            INT               NOT NULL CONSTRAINT DF_IntegSignal_Weight DEFAULT 1,
    Detail            NVARCHAR(500)     NULL,
    DetectedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_IntegSignal_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_IntegritySignal        PRIMARY KEY (IntegritySignalId),
    CONSTRAINT UQ_IntegritySignal        UNIQUE (MatchId, SignalType),
    CONSTRAINT CK_IntegritySignal_Type   CHECK (SignalType IN ('REPORT_TIME_GAP','REPEATED_SCORE_PATTERN','NO_ROOM_CODE_IN_CHAT','REUSED_EVIDENCE')),
    CONSTRAINT CK_IntegritySignal_Weight CHECK (Weight >= 1),
    CONSTRAINT FK_IntegritySignal_Match  FOREIGN KEY (MatchId) REFERENCES dbo.Match(MatchId)
);
GO

/* MatchRiskScore - ITG-05 cộng điểm bốn dấu hiệu rồi xếp trận theo mức rủi ro.
   ITG-07: cảnh báo KHÔNG tự khoá trận - bảng này không có cột nào khoá trận,
   ban tổ chức tự quyết có mở tranh chấp hay không. */
CREATE TABLE dbo.MatchRiskScore
(
    MatchRiskScoreId INT IDENTITY(1,1) NOT NULL,
    MatchId          INT               NOT NULL,
    TotalScore       INT               NOT NULL,
    SignalCount      INT               NOT NULL,
    RiskLevel        VARCHAR(10)       NOT NULL,
    ComputedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_MatchRisk_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_MatchRiskScore       PRIMARY KEY (MatchRiskScoreId),
    CONSTRAINT UQ_MatchRiskScore_Match UNIQUE (MatchId),
    CONSTRAINT CK_MatchRiskScore_Level CHECK (RiskLevel IN ('LOW','MEDIUM','HIGH')),
    CONSTRAINT CK_MatchRiskScore_Num   CHECK (TotalScore >= 0 AND SignalCount >= 0),
    CONSTRAINT FK_MatchRiskScore_Match FOREIGN KEY (MatchId) REFERENCES dbo.Match(MatchId)
);
GO

/*-------------------------------------------------------------------------------
  CÁC CỘT THÊM VÀO BẢNG CŨ
  Tách ra đây thay vì sửa trực tiếp 50 bảng bên trên, để lược đồ cũ còn
  đối chiếu nguyên trạng được với Report 3 và các mã BR/MSG đã kiểm.
-------------------------------------------------------------------------------*/

/* Tournament: giải thuộc về ĐƠN VỊ (ORG-06: đơn vị đổi ban chấp hành thì giải
   và lịch sử vẫn thuộc đơn vị, không mất), và chọn MỘT bộ luật (GRL-06).
   Để NULL được vì các giải tạo trước 17/10/2026 chưa gắn đơn vị nào. */
ALTER TABLE dbo.Tournament ADD
    OrganizationId INT NULL,
    RulesetId      INT NULL,
    CONSTRAINT FK_Tournament_Organization FOREIGN KEY (OrganizationId) REFERENCES dbo.Organization(OrganizationId),
    CONSTRAINT FK_Tournament_Ruleset      FOREIGN KEY (RulesetId)      REFERENCES dbo.Ruleset(RulesetId);
GO

/* RegistrationLineup: GRL-04 gán vị trí cho từng người khi khai đội hình.
   Trước đây chỉ có Slot = STARTER / SUBSTITUTE. */
ALTER TABLE dbo.RegistrationLineup ADD
    RulesetPositionId INT NULL,
    CONSTRAINT FK_RegistrationLineup_Position FOREIGN KEY (RulesetPositionId) REFERENCES dbo.RulesetPosition(RulesetPositionId);
GO

/* MatchReport: ITG-08 mã băm móc nối, ITG-09 đã gửi mã băm cho đội trưởng chưa.
   HashPrevValue là mã băm của bản ghi liền trước, nên sửa một báo cáo cũ sẽ
   làm lệch toàn bộ chuỗi phía sau. Email ở ITG-09 là ĐIỂM NEO NGOÀI cơ sở dữ
   liệu - không có nó thì quản trị viên vẫn tính lại được cả chuỗi. */
ALTER TABLE dbo.MatchReport ADD
    HashValue       CHAR(64)     NULL,
    HashPrevValue   CHAR(64)     NULL,
    HashEmailSentAt DATETIME2(0) NULL;
GO

/* MatchGame: LIVE-01 trọng tài nhập tỉ số từng ván NGAY KHI ván kết thúc, nên
   cần biết ai sửa và sửa lúc nào; LIVE-04 dùng mốc thời gian này để đẩy cập
   nhật thời gian thực. Đội trưởng KHÔNG nhập ở đây - họ nộp báo cáo mù RES-01. */
ALTER TABLE dbo.MatchGame ADD
    UpdatedByUserId INT          NULL,
    UpdatedAt       DATETIME2(0) NULL,
    CONSTRAINT FK_MatchGame_UpdatedBy FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.AppUser(UserId);
GO

/*-------------------------------------------------------------------------------
  CHỈ MỤC CHO NĂM MODULE MỚI
-------------------------------------------------------------------------------*/
CREATE INDEX IX_RulesetMap_Ruleset       ON dbo.RulesetMap(RulesetId, IsActive);
CREATE INDEX IX_RulesetPosition_Ruleset  ON dbo.RulesetPosition(RulesetId, DisplayOrder);
CREATE INDEX IX_MatchPickBan_Match       ON dbo.MatchPickBan(MatchId, Sequence);
CREATE INDEX IX_Organization_Review      ON dbo.Organization(Status, CreatedAt);
CREATE INDEX IX_OrganizationDocument_Org ON dbo.OrganizationDocument(OrganizationId);
CREATE INDEX IX_OrganizationMember_User  ON dbo.OrganizationMember(UserId, IsActive);
CREATE INDEX IX_OrganizationMetric_Org   ON dbo.OrganizationMetric(OrganizationId, ComputedAt);
CREATE INDEX IX_Sponsor_Org              ON dbo.Sponsor(OrganizationId, IsActive);
CREATE INDEX IX_TournamentSponsor_Tour   ON dbo.TournamentSponsor(TournamentId, DisplayOrder);
CREATE INDEX IX_IntegritySignal_Match    ON dbo.IntegritySignal(MatchId);
CREATE INDEX IX_MatchRiskScore_Queue     ON dbo.MatchRiskScore(RiskLevel, TotalScore DESC);
CREATE INDEX IX_Tournament_Organization  ON dbo.Tournament(OrganizationId);
GO

/*===============================================================================
  CHỈ MỤC PHỤ TRỢ - cho các truy vấn hay dùng và các tác vụ nền
===============================================================================*/
CREATE INDEX IX_UserRole_Role            ON dbo.UserRole(RoleId);
CREATE INDEX IX_RefreshToken_User        ON dbo.RefreshToken(UserId, ExpiresAt);
CREATE INDEX IX_GameAccount_Game         ON dbo.GameAccount(GameId);
CREATE INDEX IX_IdentityDocument_User    ON dbo.IdentityDocument(UserId);
CREATE INDEX IX_Team_Captain             ON dbo.Team(CaptainUserId);
CREATE INDEX IX_TeamMembership_User      ON dbo.TeamMembership(UserId, Status);
CREATE INDEX IX_InviteCode_Team          ON dbo.InviteCode(TeamId, Status);
CREATE INDEX IX_RecruitmentPost_Board    ON dbo.RecruitmentPost(GameId, PostType, Status);
CREATE INDEX IX_JoinApplication_Applicant ON dbo.JoinApplication(ApplicantUserId);
CREATE INDEX IX_Tournament_Portal        ON dbo.Tournament(Status, GameId, StartAt);
CREATE INDEX IX_Tournament_Organizer     ON dbo.Tournament(OrganizerUserId);
CREATE INDEX IX_Rulebook_Tournament      ON dbo.Rulebook(TournamentId);
CREATE INDEX IX_TournamentStage_Tour     ON dbo.TournamentStage(TournamentId);
CREATE INDEX IX_NewsArticle_Feed         ON dbo.NewsArticle(Status, IsPinned, PublishedAt);
CREATE INDEX IX_Registration_Queue       ON dbo.Registration(TournamentId, Status);
CREATE INDEX IX_Registration_Team        ON dbo.Registration(TeamId);
CREATE INDEX IX_RegistrationLineup_Tour  ON dbo.RegistrationLineup(TournamentId, UserId);
CREATE INDEX IX_PaymentProof_Reg         ON dbo.PaymentProof(RegistrationId);
CREATE INDEX IX_RefundRecord_Reg         ON dbo.RefundRecord(RegistrationId);
CREATE INDEX IX_WaitlistEntry_Queue      ON dbo.WaitlistEntry(TournamentId, Status, Position);
CREATE INDEX IX_BracketSeed_Bracket      ON dbo.BracketSeed(BracketId);
CREATE INDEX IX_Match_Bracket            ON dbo.Match(BracketId, RoundNumber);
CREATE INDEX IX_Match_TeamA              ON dbo.Match(TeamAId);
CREATE INDEX IX_Match_TeamB              ON dbo.Match(TeamBId);
CREATE INDEX IX_Match_Referee            ON dbo.Match(RefereeUserId);
-- JOB-01/02/03: quét theo trạng thái + mốc thời gian
CREATE INDEX IX_Match_JobCheckIn         ON dbo.Match(Status, CheckInOpenAt);
CREATE INDEX IX_Match_JobNoShow          ON dbo.Match(Status, CheckInCloseAt);
CREATE INDEX IX_Match_JobReportDeadline  ON dbo.Match(Status, ReportDeadlineAt);
CREATE INDEX IX_MatchChatMessage_Match   ON dbo.MatchChatMessage(MatchId, SentAt);
CREATE INDEX IX_MatchReport_Match        ON dbo.MatchReport(MatchId);
CREATE INDEX IX_EvidenceFile_Report      ON dbo.EvidenceFile(MatchReportId);
CREATE INDEX IX_Dispute_Queue            ON dbo.Dispute(Status, RaisedAt);
CREATE INDEX IX_Dispute_Assignee         ON dbo.Dispute(AssignedToUserId, Status);
CREATE INDEX IX_Penalty_TeamTour         ON dbo.Penalty(TournamentId, TeamId, Status);
CREATE INDEX IX_Notification_Inbox       ON dbo.Notification(RecipientUserId, Status, QueuedAt);
CREATE INDEX IX_Notification_Outbox      ON dbo.Notification(Status, Channel, QueuedAt);
CREATE INDEX IX_LeaderboardEntry_Rank    ON dbo.LeaderboardEntry(GameId, Season, Points DESC);
CREATE INDEX IX_AuditLog_Object          ON dbo.AuditLog(ObjectType, ObjectId, OccurredAt);
CREATE INDEX IX_AuditLog_Tournament      ON dbo.AuditLog(TournamentId, OccurredAt);

/* Bổ sung sau khi soát lại toàn bộ 108 khoá ngoại.
   Tiêu chí: khoá ngoại nào mà MỘT MÀN HÌNH hoặc MỘT TÁC VỤ NỀN thật sự đi ngược
   từ bảng con lên bảng cha thì phải có chỉ mục dẫn đầu bằng cột đó. Cột chỉ ghi
   nhận "ai đã làm việc này" (BannedByUserId, ReviewedByUserId, IssuedByUserId,
   UploadedByUserId, GrantedByUserId...) CỐ Ý không đánh chỉ mục: chúng chỉ được
   đọc khi mở đúng một bản ghi, thêm chỉ mục chỉ làm chậm thao tác ghi. */
CREATE INDEX IX_Tournament_Game           ON dbo.Tournament(GameId, StartAt);
CREATE INDEX IX_NewsArticle_Tour          ON dbo.NewsArticle(TournamentId, PublishedAt);
CREATE INDEX IX_TournamentFollow_Tour     ON dbo.TournamentFollow(TournamentId);
CREATE INDEX IX_TournamentMediaAsset_Tour ON dbo.TournamentMediaAsset(TournamentId);
CREATE INDEX IX_TournamentReferee_User    ON dbo.TournamentReferee(UserId, Status);
CREATE INDEX IX_OrganizerRequest_User     ON dbo.OrganizerRequest(UserId, Status);
CREATE INDEX IX_VerificationToken_User    ON dbo.VerificationToken(UserId, TokenType);
CREATE INDEX IX_RecruitmentPost_Team      ON dbo.RecruitmentPost(TeamId);
CREATE INDEX IX_RecruitmentPost_Author    ON dbo.RecruitmentPost(AuthorUserId);
CREATE INDEX IX_Registration_Tracking     ON dbo.Registration(TrackingLinkId);
CREATE INDEX IX_RegistrationLineup_User   ON dbo.RegistrationLineup(UserId);
CREATE INDEX IX_BracketSeed_Team          ON dbo.BracketSeed(TeamId);
CREATE INDEX IX_BracketSeed_Group         ON dbo.BracketSeed(BracketGroupId);
CREATE INDEX IX_Match_Group               ON dbo.Match(BracketGroupId, RoundNumber);
-- Cot TournamentId moi tren Bracket / Match: phuc vu khoa ngoai ghep va cac
-- truy van "moi tran cua giai X" ma truoc day phai join qua 3 bang.
CREATE INDEX IX_Bracket_Tournament         ON dbo.Bracket(TournamentId);
CREATE INDEX IX_Match_Tournament           ON dbo.Match(TournamentId, Status);
-- JOB-05: đẩy đội thắng lên trận kế tiếp, và nhánh thua của loại trực tiếp kép
CREATE INDEX IX_Match_NextMatch           ON dbo.Match(NextMatchId);
CREATE INDEX IX_Match_LoserNext           ON dbo.Match(LoserNextMatchId);
CREATE INDEX IX_Match_Winner              ON dbo.Match(WinnerTeamId);
CREATE INDEX IX_MatchGame_Winner          ON dbo.MatchGame(WinnerTeamId);
CREATE INDEX IX_MatchReport_Team          ON dbo.MatchReport(TeamId);
CREATE INDEX IX_MatchChatMessage_Sender   ON dbo.MatchChatMessage(SenderUserId);
CREATE INDEX IX_SubstitutionRequest_Match ON dbo.SubstitutionRequest(MatchId, Status);
CREATE INDEX IX_SubstitutionRequest_Team  ON dbo.SubstitutionRequest(TeamId);
CREATE INDEX IX_Dispute_Match             ON dbo.Dispute(MatchId, RaisedAt);
CREATE INDEX IX_Appeal_Team               ON dbo.Appeal(TeamId);
CREATE INDEX IX_Penalty_Team              ON dbo.Penalty(TeamId, Status);
CREATE INDEX IX_Penalty_Match             ON dbo.Penalty(MatchId);
CREATE INDEX IX_TeamTitle_Team            ON dbo.TeamTitle(TeamId);
GO

/*===============================================================================
  TRIGGER - các QUY TẮC NGHIỆP VỤ DỮ LIỆU không diễn đạt được bằng CHECK / UNIQUE.
  Mục đích: dù ai viết sai code ở tầng ứng dụng thì database vẫn chặn.
===============================================================================*/

/* QUY TẮC: nhật ký thao tác chỉ được thêm, không sửa không xoá. */
-- BR-ADM-01
CREATE TRIGGER dbo.TR_AuditLog_Immutable
ON dbo.AuditLog
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    ROLLBACK TRANSACTION;
    RAISERROR (N'AuditLog la append-only: khong duoc UPDATE hay DELETE, ke ca System Admin.', 16, 1);
END;
GO

/* QUY TẮC: tài khoản không bao giờ bị xoá (chỉ chuyển sang BANNED). */
-- BR-AUTH-02
CREATE TRIGGER dbo.TR_AppUser_NoDelete
ON dbo.AppUser
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;
    ROLLBACK TRANSACTION;
    RAISERROR (N'Tai khoan khong duoc xoa. Hay dat Status = BANNED kem ly do.', 16, 1);
END;
GO

/* QUY TẮC: đúng 2 bản báo cáo cho 1 trận, không hơn / bản đã nộp không được
   sửa hay rút. Cột Status vẫn được phép đổi (SUBMITTED -> MATCHED /
   CONFLICTING) vì đó là kết quả đối chiếu của JOB-04. */
-- BR-RES-01
CREATE TRIGGER dbo.TR_MatchReport_Immutable
ON dbo.MatchReport
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR (N'Ban bao cao ket qua da nop thi khong duoc rut hay xoa.', 16, 1);
        RETURN;
    END;

    IF UPDATE(MatchId) OR UPDATE(TeamId) OR UPDATE(ReportedTeamAScore)
       OR UPDATE(ReportedTeamBScore) OR UPDATE(ReportedByUserId) OR UPDATE(SubmittedAt)
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR (N'Ban bao cao ket qua da nop thi khong duoc sua. Chi cot Status duoc cap nhat.', 16, 1);
        RETURN;
    END;
END;
GO

/* Tỉ số từng ván do đội khai cũng bất biến. */
-- BR-RES-01
CREATE TRIGGER dbo.TR_MatchReportGame_Immutable
ON dbo.MatchReportGame
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    ROLLBACK TRANSACTION;
    RAISERROR (N'Ti so tung van trong ban bao cao da nop thi khong duoc sua hay xoa.', 16, 1);
END;
GO

/* QUY TẮC: minh chứng không bao giờ bị xoá - trong suốt vòng đời của giải. */
-- BR-RES-03
CREATE TRIGGER dbo.TR_EvidenceFile_NoDelete
ON dbo.EvidenceFile
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM deleted d
        JOIN dbo.MatchReport mr ON mr.MatchReportId = d.MatchReportId
        JOIN dbo.Match       m  ON m.MatchId        = mr.MatchId
        JOIN dbo.Tournament  t  ON t.TournamentId   = m.TournamentId
        WHERE t.Status <> 'COMPLETED'
    )
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR (N'Minh chung khong duoc xoa khi giai dau chua ket thuc.', 16, 1);
    END;
END;
GO

/* QUY TẮC: thẻ phạt không bao giờ bị xoá - huỷ bằng 1 bản ghi liên kết. */
-- BR-PEN-01
CREATE TRIGGER dbo.TR_Penalty_NoDelete
ON dbo.Penalty
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;
    ROLLBACK TRANSACTION;
    RAISERROR (N'The phat khong duoc xoa. Hay tao 1 ban ghi Severity = CANCELLATION tro ve the phat cu.', 16, 1);
END;
GO

/* QUY TẮC: nhánh đấu đã khoá là chốt - khoá rồi thì không thêm / bớt trận nào.
   Vẫn cho phép UPDATE trận (đẩy đội thắng lên vòng sau, đặt lại, xử thắng). */
-- BR-BRK-02
CREATE TRIGGER dbo.TR_Match_BracketLocked
ON dbo.Match
AFTER INSERT, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.Bracket b ON b.BracketId = i.BracketId WHERE b.Status = 'LOCKED')
       OR EXISTS (SELECT 1 FROM deleted  d JOIN dbo.Bracket b ON b.BracketId = d.BracketId WHERE b.Status = 'LOCKED')
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR (N'Nhanh dau da khoa: khong duoc them hay xoa tran dau. Chi con duong dau lai (rematch) hoac truat quyen.', 16, 1);
    END;
END;
GO

/* QUY TẮC: kết quả trận đi một chiều - tỉ số đã VERIFIED thì không sửa.
   Muốn sửa phải đổi Status (đặt lại về SCHEDULED, hoặc qua phán quyết VAR). */
-- BR-COM-01
CREATE TRIGGER dbo.TR_Match_VerifiedResultImmutable
ON dbo.Match
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted  d ON d.MatchId = i.MatchId
        WHERE d.Status = 'VERIFIED' AND i.Status = 'VERIFIED'
          AND (ISNULL(d.TeamAScore, -1)   <> ISNULL(i.TeamAScore, -1)
            OR ISNULL(d.TeamBScore, -1)   <> ISNULL(i.TeamBScore, -1)
            OR ISNULL(d.WinnerTeamId, -1) <> ISNULL(i.WinnerTeamId, -1))
    )
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR (N'Ket qua da xac nhan thi khong duoc sua truc tiep. Phai dau lai hoac ap ket qua qua phan quyet VAR, va deu phai ghi ly do.', 16, 1);
    END;
END;
GO


/* BR-REG-02: so nguoi trong danh sach thi dau phai dung luat cua tua game.
   Khong lam duoc bang CHECK vi phai dem nhieu dong, nen dung trigger.
   Chi kiem khi don da roi trang thai DRAFT - luc con nhap do dang thi chua du nguoi. */
CREATE TRIGGER dbo.TR_RegistrationLineup_RosterRule
ON dbo.RegistrationLineup
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @bad INT;
    SELECT TOP 1 @bad = r.RegistrationId
    FROM dbo.Registration r
    JOIN dbo.Tournament   t ON t.TournamentId = r.TournamentId
    WHERE r.RegistrationId IN (SELECT RegistrationId FROM inserted
                               UNION SELECT RegistrationId FROM deleted)
      AND r.Status NOT IN ('DRAFT', 'WITHDRAWN', 'REJECTED')
      AND (
            (SELECT COUNT(*) FROM dbo.RegistrationLineup l
              WHERE l.RegistrationId = r.RegistrationId AND l.Slot = 'STARTER')    <> t.StarterCount
         OR (SELECT COUNT(*) FROM dbo.RegistrationLineup l
              WHERE l.RegistrationId = r.RegistrationId AND l.Slot = 'SUBSTITUTE') > t.MaxSubstitutes
          );

    IF @bad IS NOT NULL
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR (N'So luong thanh vien khong khop voi luat cua tua game (MSG-REG-001).', 16, 1);
    END;
END;
GO
/* BR-REG-03 / MSG-REG-002 / QUY TẮC DỮ LIỆU: 1 người chơi không được nằm trong danh sách thi đấu
   của 2 đội khác nhau trong CÙNG MỘT giải. */
CREATE TRIGGER dbo.TR_RegistrationLineup_OnePlayerPerTournament
ON dbo.RegistrationLineup
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.Registration       r  ON r.RegistrationId  = i.RegistrationId
        JOIN dbo.RegistrationLineup l2 ON l2.UserId         = i.UserId
                                      AND l2.TournamentId   = i.TournamentId
                                      AND l2.RegistrationLineupId <> i.RegistrationLineupId
        JOIN dbo.Registration       r2 ON r2.RegistrationId = l2.RegistrationId
        WHERE r.Status  <> 'WITHDRAWN' AND r.Status  <> 'REJECTED'
          AND r2.Status <> 'WITHDRAWN' AND r2.Status <> 'REJECTED'
    )
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR (N'Nguoi choi nay da co ten trong danh sach thi dau cua mot doi khac trong cung giai (MSG-REG-002).', 16, 1);
    END;
END;
GO

/*===============================================================================
  VIEW - phục vụ trực tiếp các tác vụ nền và các màn hình hay dùng
===============================================================================*/

/* JOB-04 / RES-02: đặt 2 bản báo cáo cạnh nhau và kết luận khớp hay lệch.
   Đây chính là cơ chế THAY CHO API nhà phát hành. */
CREATE VIEW dbo.vw_MatchReportComparison
AS
SELECT
    m.MatchId,
    m.BracketId,
    m.Status                AS MatchStatus,
    m.TeamAId,
    m.TeamBId,
    ra.MatchReportId        AS ReportAId,
    ra.ReportedTeamAScore   AS A_SaysTeamA,
    ra.ReportedTeamBScore   AS A_SaysTeamB,
    ra.SubmittedAt          AS A_SubmittedAt,
    rb.MatchReportId        AS ReportBId,
    rb.ReportedTeamAScore   AS B_SaysTeamA,
    rb.ReportedTeamBScore   AS B_SaysTeamB,
    rb.SubmittedAt          AS B_SubmittedAt,
    CASE
        WHEN ra.MatchReportId IS NULL OR rb.MatchReportId IS NULL THEN 'WAITING'
        WHEN ra.ReportedTeamAScore = rb.ReportedTeamAScore
         AND ra.ReportedTeamBScore = rb.ReportedTeamBScore        THEN 'MATCHED'
        ELSE 'CONFLICTING'
    END AS AgreementState
FROM dbo.Match m
LEFT JOIN dbo.MatchReport ra ON ra.MatchId = m.MatchId AND ra.TeamId = m.TeamAId
LEFT JOIN dbo.MatchReport rb ON rb.MatchId = m.MatchId AND rb.TeamId = m.TeamBId;
GO

/* REG-06 / TOUR-12: còn bao nhiêu suất so với MaxTeams. */
CREATE VIEW dbo.vw_TournamentSlot
AS
SELECT
    t.TournamentId,
    t.Name,
    t.MaxTeams,
    SUM(CASE WHEN r.Status = 'APPROVED'   THEN 1 ELSE 0 END) AS ApprovedCount,
    SUM(CASE WHEN r.Status = 'PENDING'    THEN 1 ELSE 0 END) AS PendingCount,
    SUM(CASE WHEN r.Status = 'WAITLISTED' THEN 1 ELSE 0 END) AS WaitlistedCount,
    t.MaxTeams - SUM(CASE WHEN r.Status = 'APPROVED' THEN 1 ELSE 0 END) AS FreeSlot
FROM dbo.Tournament t
LEFT JOIN dbo.Registration r ON r.TournamentId = t.TournamentId
GROUP BY t.TournamentId, t.Name, t.MaxTeams;
GO

/* JOB-06 / REG-09: đội kế tiếp trong hàng đợi của mỗi giải. */
CREATE VIEW dbo.vw_WaitlistNext
AS
SELECT TournamentId, WaitlistEntryId, RegistrationId, Position, AddedAt
FROM (
    SELECT
        w.TournamentId, w.WaitlistEntryId, w.RegistrationId, w.Position, w.AddedAt,
        ROW_NUMBER() OVER (PARTITION BY w.TournamentId ORDER BY w.Position) AS rn
    FROM dbo.WaitlistEntry w
    WHERE w.Status = 'WAITING'
) q
WHERE rn = 1;
GO

/* JOB-12 / PEN-03: tổng điểm phạt đang hiệu lực so với ngưỡng tước quyền. */
CREATE VIEW dbo.vw_TeamPenaltyPoint
AS
SELECT
    p.TournamentId,
    p.TeamId,
    SUM(p.Points)          AS ActivePoints,
    MAX(t.PenaltyThreshold) AS PenaltyThreshold
FROM dbo.Penalty p
JOIN dbo.Tournament t ON t.TournamentId = p.TournamentId
WHERE p.Status = 'ISSUED' OR p.Status = 'ACTIVE'
GROUP BY p.TournamentId, p.TeamId;
GO

/*===============================================================================
  DỮ LIỆU DANH MỤC BAN ĐẦU
===============================================================================*/

INSERT INTO dbo.Role (Code, Name, Description) VALUES
 ('ADMIN',     N'Quản trị hệ thống', N'Quản lý danh mục game, tài khoản, duyệt quyền Organizer.'),
 ('ORGANIZER', N'Ban tổ chức',       N'Tạo và vận hành giải đấu. Phải được Admin duyệt.'),
 ('PLAYER',    N'Người chơi',        N'Vai trò mặc định của mọi tài khoản đã xác minh.'),
 ('REFEREE',   N'Trọng tài',         N'Được BTC mời cho từng giải. Phân xử tranh chấp.');
GO

INSERT INTO dbo.Game (Title, ShortCode, Status) VALUES
 (N'League of Legends', 'LOL', 'ACTIVE'),
 (N'Valorant',          'VAL', 'ACTIVE'),
 (N'Liên Quân Mobile',  'LQM', 'ACTIVE'),
 (N'PUBG Mobile',       'PUM', 'ACTIVE');
GO

/* Ruleset - GRL-01. Liên Minh Huyền Thoại cố tình có HAI bộ luật để chứng minh
   mô hình nhiều bộ luật trên một tựa game đã chạy được (GameRule cũ không cho). */
INSERT INTO dbo.Ruleset (GameId, Name, MinStarters, MaxStarters, MaxSubstitutes, DefaultFormat, DefaultBestOf, AllowedEvidenceTypes, MaxEvidenceFileSizeMB)
SELECT GameId, N'Draft Pick', 5, 5, 3, 'SINGLE_ELIMINATION', 3, N'png,jpg,jpeg,mp4,rofl', 10 FROM dbo.Game WHERE ShortCode = 'LOL'
UNION ALL SELECT GameId, N'Blind Pick', 5, 5, 3, 'SINGLE_ELIMINATION', 1, N'png,jpg,jpeg,mp4,rofl', 10 FROM dbo.Game WHERE ShortCode = 'LOL'
UNION ALL SELECT GameId, N'Luật mặc định', 5, 5, 2, 'SINGLE_ELIMINATION', 1, N'png,jpg,jpeg,mp4', 10 FROM dbo.Game WHERE ShortCode = 'VAL'
UNION ALL SELECT GameId, N'Luật mặc định', 5, 5, 2, 'SINGLE_ELIMINATION', 1, N'png,jpg,jpeg,mp4', 10 FROM dbo.Game WHERE ShortCode = 'LQM'
UNION ALL SELECT GameId, N'Luật mặc định', 4, 4, 1, 'ROUND_ROBIN',        1, N'png,jpg,jpeg,mp4', 10 FROM dbo.Game WHERE ShortCode = 'PUM';
GO

/* RulesetPosition - GRL-03. Năm vị trí của Liên Minh Huyền Thoại, để
   RegistrationLineup.RulesetPositionId (GRL-04) có dữ liệu mà trỏ tới. */
INSERT INTO dbo.RulesetPosition (RulesetId, PositionName, DisplayOrder)
SELECT r.RulesetId, v.PositionName, v.DisplayOrder
FROM dbo.Ruleset r
JOIN dbo.Game g ON g.GameId = r.GameId
CROSS APPLY (VALUES
      (N'Đường trên', 1),
      (N'Đi rừng',    2),
      (N'Đường giữa', 3),
      (N'Xạ thủ',     4),
      (N'Hỗ trợ',     5)) AS v(PositionName, DisplayOrder)
WHERE g.ShortCode = 'LOL' AND r.Name = N'Draft Pick';
GO

/* RulesetMap - GRL-02. Bản đồ thi đấu của bộ luật Draft Pick. */
INSERT INTO dbo.RulesetMap (RulesetId, MapName)
SELECT r.RulesetId, N'Summoner''s Rift'
FROM dbo.Ruleset r
JOIN dbo.Game g ON g.GameId = r.GameId
WHERE g.ShortCode = 'LOL' AND r.Name = N'Draft Pick';
GO

/* 34 thong bao he thong cua SRS muc 5.2, viet bang tieng Viet vi ETOMS chi
   phuc vu nguoi dung Viet Nam - toan he thong dung mot ngon ngu duy nhat.
   Cac chuoi trong {ngoac nhon} la cho tang ung dung thay gia tri that vao.
   Ma dung so do MSG-<MODULE>-nnn, trung khop tung ma voi bang o Report 3 muc 5.2. */
INSERT INTO dbo.NotificationTemplate (Code, MessageType, Context, BodyTemplate) VALUES
 ('MSG-COM-001', 'INLINE', N'No search result', N'Không tìm thấy kết quả phù hợp.'),
 ('MSG-COM-002', 'INLINE', N'A required field is empty', N'Vui lòng nhập đầy đủ thông tin bắt buộc.'),
 ('MSG-COM-003', 'TOAST_SUCCESS', N'Save succeeded', N'Lưu thông tin thành công.'),
 ('MSG-COM-004', 'TOAST_ERROR', N'Save failed', N'Không thể lưu thông tin. Vui lòng thử lại.'),
 ('MSG-AUTH-001', 'INLINE', N'Sign-in refused', N'Email hoặc mật khẩu không đúng.'),
 ('MSG-AUTH-002', 'INLINE', N'Banned account signs in', N'Tài khoản đang bị khoá. Lý do: {lý do}.'),
 ('MSG-AUTH-003', 'INLINE', N'Email already registered', N'Email này đã được đăng ký.'),
 ('MSG-AUTH-004', 'INLINE', N'Unverified email attempts a competition action', N'Vui lòng xác minh email trước khi thực hiện thao tác này.'),
 ('MSG-AUTH-005', 'INLINE', N'Activation or reset link expired', N'Liên kết đã hết hạn. Vui lòng yêu cầu gửi lại.'),
 ('MSG-TOUR-001', 'INLINE', N'Registration window closes after the start date', N'Thời gian đóng đăng ký phải trước ngày khai mạc.'),
 ('MSG-TOUR-002', 'TOAST_ERROR', N'Cancel attempted after the bracket is locked', N'Không thể huỷ giải sau khi nhánh đấu đã khoá.'),
 ('MSG-TOUR-003', 'DIALOG', N'Publish confirmation', N'Xuất bản giải đấu? Sau khi xuất bản, mọi người đều xem được trang giải.'),
 ('MSG-REG-001', 'INLINE', N'Lineup size does not match the roster rule', N'Số lượng thành viên không khớp với luật của tựa game.'),
 ('MSG-REG-002', 'INLINE', N'A player is on another lineup in this tournament', N'Vận động viên {tên} đã có tên trong đội khác ở giải này.'),
 ('MSG-REG-003', 'INLINE', N'The team already has a live registration', N'Đội của bạn đã có một đơn đăng ký đang hiệu lực ở giải này.'),
 ('MSG-REG-004', 'INLINE', N'Rulebook acceptance not ticked', N'Bạn phải xác nhận đã đọc và chấp nhận thể lệ giải.'),
 ('MSG-REG-005', 'TOAST_SUCCESS', N'Entry placed on the waitlist', N'Giải đã đủ số đội. Đơn của bạn được đưa vào hàng đợi ở vị trí {n}.'),
 ('MSG-REG-006', 'INLINE', N'Rejection with no reason', N'Vui lòng nhập lý do từ chối.'),
 ('MSG-REG-007', 'TOAST_SUCCESS', N'Promoted from the waitlist', N'Đội của bạn đã được chuyển từ hàng đợi vào danh sách chờ duyệt.'),
 ('MSG-BRK-001', 'TOAST_ERROR', N'Regeneration attempted on a locked bracket', N'Nhánh đấu đã khoá, không thể sinh lại.'),
 ('MSG-BRK-002', 'INLINE', N'Fewer than two approved teams', N'Cần ít nhất hai đội đã được duyệt để sinh nhánh đấu.'),
 ('MSG-BRK-003', 'DIALOG', N'Lock bracket confirmation', N'Khoá nhánh đấu? Sau khi khoá sẽ không sinh lại được, danh sách thành viên bị đóng băng và hàng đợi bị đóng.'),
 ('MSG-MAT-001', 'INLINE', N'Check-in before the window opens', N'Cửa sổ điểm danh chưa mở. Mở trước giờ thi đấu 30 phút.'),
 ('MSG-MAT-002', 'INLINE', N'Action after the deadline', N'Đã quá hạn. Vui lòng liên hệ Ban tổ chức.'),
 ('MSG-MAT-003', 'TOAST_SUCCESS', N'Check-in recorded', N'Điểm danh thành công. Chờ đối thủ điểm danh.'),
 ('MSG-RES-001', 'INLINE', N'This team already reported', N'Đội của bạn đã nộp báo cáo cho trận này.'),
 ('MSG-RES-002', 'DIALOG', N'Submit confirmation', N'Nộp kết quả? Báo cáo đã nộp không thể sửa hoặc rút lại.'),
 ('MSG-RES-003', 'INLINE', N'No evidence attached', N'Vui lòng đính kèm ảnh hoặc file minh chứng.'),
 ('MSG-RES-004', 'INLINE', N'Ruling with no reason', N'Vui lòng nhập lý do phán quyết.'),
 ('MSG-RES-005', 'TOAST_ERROR', N'Second appeal on the same ruling', N'Phán quyết này đã được khiếu nại một lần.'),
 ('MSG-RES-006', 'TOAST_ERROR', N'Reports disagree', N'Hai báo cáo lệch nhau. Trận đấu đã bị đóng băng và chuyển cho Trọng tài.'),
 ('MSG-PEN-001', 'INLINE', N'Penalty with no reason', N'Vui lòng nhập lý do cấp thẻ phạt.'),
 ('MSG-PEN-002', 'TOAST_ERROR', N'Penalty threshold reached', N'Đội đã đủ điểm phạt và bị tước quyền thi đấu.'),
 ('MSG-NOT-001', 'TOAST_SUCCESS', N'Announcement sent', N'Đã gửi thông báo tới {n} người nhận.');
GO

/*===============================================================================
  KIỂM TRA SAU KHI CHẠY
===============================================================================*/
PRINT '';
PRINT N'============== ETOMS DATABASE - KẾT QUẢ ==============';
GO

SELECT
    (SELECT COUNT(*) FROM sys.tables)                                  AS SoBang,
    (SELECT COUNT(*) FROM sys.views  WHERE is_ms_shipped = 0)          AS SoView,
    (SELECT COUNT(*) FROM sys.triggers WHERE is_ms_shipped = 0)        AS SoTrigger,
    (SELECT COUNT(*) FROM sys.foreign_keys)                            AS SoKhoaNgoai,
    (SELECT COUNT(*) FROM sys.check_constraints)                       AS SoCheckConstraint,
    (SELECT COUNT(*) FROM sys.indexes i JOIN sys.tables tb ON tb.object_id = i.object_id
      WHERE i.is_unique = 1 AND i.is_primary_key = 0)                  AS SoRangBuocDuyNhat;
GO

SELECT 'Role' AS Bang, COUNT(*) AS SoDong FROM dbo.Role
UNION ALL SELECT 'Game',                 COUNT(*) FROM dbo.Game
UNION ALL SELECT 'Ruleset',              COUNT(*) FROM dbo.Ruleset
UNION ALL SELECT 'RulesetPosition',      COUNT(*) FROM dbo.RulesetPosition
UNION ALL SELECT 'RulesetMap',           COUNT(*) FROM dbo.RulesetMap
UNION ALL SELECT 'NotificationTemplate', COUNT(*) FROM dbo.NotificationTemplate;
GO

PRINT N'Đã tạo xong database ETOMS.';
GO

SET NOEXEC OFF;
GO

/* ============================================================================
   TÌNH TRẠNG LƯỢC ĐỒ SO VỚI DANH MỤC CHỨC NĂNG  -  cập nhật 17/10/2026
   ----------------------------------------------------------------------------
   Danh mục ChucNangcuaGiaDau.xlsx: 144 chức năng / 19 module.
   Lược đồ này: 62 bảng. Cả 19 module đều đã có bảng.

   Bổ sung ngày 17/10/2026 cho 5 module mới (chi tiết xem phần
   "NĂM MODULE MỚI" ở giữa file):
     - Ruleset thay cho GameRule, bỏ ràng buộc một tựa game một bộ luật
     - RulesetMap, RulesetPosition, MatchPickBan
     - Organization, OrganizationDocument, OrganizationMember, OrganizationMetric
     - Sponsor, TournamentSponsor
     - IntegrityThreshold, IntegritySignal, MatchRiskScore
     - thêm cột: Tournament.OrganizationId, Tournament.RulesetId,
                 RegistrationLineup.RulesetPositionId,
                 MatchReport.HashValue / HashPrevValue / HashEmailSentAt,
                 MatchGame.UpdatedByUserId / UpdatedAt

   CÒN LẠI - KHÔNG phải việc của lược đồ:
     1. OrganizerRequest là bảng của luồng CŨ: cấp quyền tổ chức cho một CÁ NHÂN.
        Luồng mới là ORG-01 nộp hồ sơ đơn vị rồi ORG-02 xét duyệt, dùng bảng
        Organization. Bảng cũ được GIỮ LẠI để không làm vỡ dữ liệu và mã nguồn
        đang chạy; khi chuyển xong toàn bộ sang Organization thì mới bỏ nó.
     2. ITG-07 (hàng đợi cảnh báo), ITG-10 (kiểm chứng mã băm) và ITG-11
        (dòng thời gian giải) KHÔNG cần bảng riêng: chúng đọc IntegritySignal,
        MatchRiskScore, MatchReport.HashValue và AuditLog.
     3. LIVE-02 và LIVE-04 không cần bảng riêng: đọc MatchGame.

   HAI RÀNG BUỘC NỀN TẢNG VẪN ĐƯỢC GIỮ Ở PHẦN MỚI:
     - Không có bảng nào lưu dữ liệu lấy từ API nhà phát hành game (LI-01).
       IntegritySignal chỉ đọc dữ liệu hệ thống tự sinh: thời điểm nộp báo cáo,
       mẫu tỉ số, log chat, metadata file bằng chứng.
     - Không có bảng giao dịch và không có cột số dư (LI-02).
       OrganizationMetric.RefundCount chỉ ĐẾM số lần hoàn tiền đã ghi nhận ở
       RefundRecord, không giữ tiền và không chuyển tiền.
   ============================================================================ */
