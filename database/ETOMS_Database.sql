/*===============================================================================
  ETOMS - Hệ thống quản lý giải đấu Thể thao Điện tử
  SEP490_G97 - Script tạo Database  (v2, 25 bảng)

  Chạy được ở CẢ HAI nơi: SQL Server 2019+ (SSMS) và Azure SQL Database.
  Collation: Vietnamese_CI_AS - mọi cột chữ dùng NVARCHAR.

  CHẠY BẰNG sqlcmd: phải thêm -f 65001 để đọc đúng UTF-8, nếu không dấu tiếng Việt
  trong câu lệnh sẽ bị hỏng. SSMS đọc đúng sẵn, không cần làm gì.

  Nguồn: UC Update.docx bản 27/10/2026 - 46 use case (UC-01 … UC-46).
  Bản v1 có 62 bảng, giữ ở _backup_sql_v1_62bang/ để đối chiếu với Report 3.

  BA QUY TẮC THIẾT KẾ CỦA BẢN NÀY
  -------------------------------
    1. Mỗi bảng có ĐÚNG MỘT khóa chính độc lập dạng <Tên>ID.
       KHÔNG có khóa chính ghép, KHÔNG có khóa ngoại ghép.
    2. Không có bảng danh mục nhỏ lẻ. Mọi tập giá trị cố định là
       VARCHAR + CHECK (vai trò của ENUM; SQL Server không có kiểu ENUM).
    3. Các ràng buộc liên-dòng dùng CHỈ MỤC DUY NHẤT CÓ ĐIỀU KIỆN
       (CREATE UNIQUE INDEX ... WHERE ...) chứ không thêm bảng.

  HAI RÀNG BUỘC NỀN TẢNG CỦA DỰ ÁN - đã phản ánh vào thiết kế
  -----------------------------------------------------------
    1. KHÔNG tích hợp API nhà phát hành game (LI-01).
       => Tỉ số CHỈ vào hệ thống qua MatchReport (hai báo cáo mù độc lập của
          hai đội) hoặc qua phán quyết trong Dispute. Không bảng nào lưu bản
          sao dữ liệu từ nhà phát hành.
    2. KHÔNG tích hợp cổng thanh toán (LI-02).
       => Không có bảng giao dịch, không có cột số dư. PaymentRecord là bảng
          MINH CHỨNG, không phải bảng giao dịch.
    Riêng Livestream (UC-46) chỉ lưu ĐƯỜNG DẪN tới dịch vụ phát bên ngoài;
    ETOMS không lưu và không truyền video.

  CÁCH CHẠY
  ---------
  SQL Server / SSMS:
    Lần đầu, mở file và bấm F5 khi đang ở database bất kỳ. Script tạo database
    ETOMS rồi DỪNG và nhắc bạn chọn nó. Chọn ETOMS trên thanh database rồi chạy
    lại cả file - lần này nó tạo toàn bộ bảng.

  Azure SQL Database:
    Tạo database sẵn trên cổng Azure rồi chạy file này khi đã kết nối vào đó.

  sqlcmd (hai lượt):
    sqlcmd -S .\SQLEXPRESS -E -d master -f 65001 -i ETOMS_Database.sql
    sqlcmd -S .\SQLEXPRESS -E -d ETOMS  -f 65001 -i ETOMS_Database.sql
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
  Chặn chạy nhầm vào database hệ thống. Nếu đang ở master/tempdb/model/msdb thì
  SET NOEXEC ON làm mọi câu lệnh phía sau chỉ được phân tích, không thực thi.
-------------------------------------------------------------------------------*/
IF DB_NAME() IN (N'master', N'tempdb', N'model', N'msdb')
BEGIN
    PRINT N'DỪNG: đang ở database ' + DB_NAME() + N'. Hãy chọn database ETOMS rồi chạy lại.';
    SET NOEXEC ON;
END
GO

/* BẮT BUỘC: các chỉ mục có điều kiện (CREATE UNIQUE INDEX ... WHERE ...) chỉ tạo
   được khi QUOTED_IDENTIFIER và ANSI_NULLS đang ON. SSMS mặc định ON, nhưng
   sqlcmd mặc định OFF - đặt rõ ở đây để script chạy đúng ở mọi nơi.
   Thiếu hai dòng này thì mọi chỉ mục có điều kiện báo lỗi Msg 1934. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

/*-------------------------------------------------------------------------------
  BƯỚC 1 - DỌN SẠCH. Xóa toàn bộ view và bảng cũ CÙNG DỮ LIỆU trong chúng.
  Nhờ bước này mà file chạy lại được bao nhiêu lần cũng được: mở trong SSMS,
  chọn database ETOMS rồi bấm F5, mỗi lần đều ra một database mới tinh.

  Xóa theo THỨ TỰ NGƯỢC với lúc tạo, vì bảng con phải chết trước bảng cha.
  KHÔNG dùng DROP DATABASE: làm vậy thì phải ngắt kết nối và đổi sang master,
  bất tiện trong SSMS và không chạy được trên Azure SQL.
-------------------------------------------------------------------------------*/
DROP VIEW IF EXISTS dbo.vw_GroupStanding;
DROP VIEW IF EXISTS dbo.vw_TeamPenaltyPoint;
DROP VIEW IF EXISTS dbo.vw_Leaderboard;
GO

DROP TABLE IF EXISTS dbo.AuditLog;
DROP TABLE IF EXISTS dbo.Livestream;
DROP TABLE IF EXISTS dbo.TournamentFollow;
DROP TABLE IF EXISTS dbo.Notification;
DROP TABLE IF EXISTS dbo.Attachment;
DROP TABLE IF EXISTS dbo.Penalty;
DROP TABLE IF EXISTS dbo.Dispute;
DROP TABLE IF EXISTS dbo.MatchReport;
DROP TABLE IF EXISTS dbo.MatchChatMessage;
DROP TABLE IF EXISTS dbo.MatchRequest;
DROP TABLE IF EXISTS dbo.MatchGame;
DROP TABLE IF EXISTS dbo.[Match];
DROP TABLE IF EXISTS dbo.BracketNode;
DROP TABLE IF EXISTS dbo.PaymentRecord;
DROP TABLE IF EXISTS dbo.RegistrationLineup;
DROP TABLE IF EXISTS dbo.Registration;
DROP TABLE IF EXISTS dbo.TournamentSponsor;
DROP TABLE IF EXISTS dbo.Tournament;
DROP TABLE IF EXISTS dbo.TeamMember;
DROP TABLE IF EXISTS dbo.RecruitmentPost;
DROP TABLE IF EXISTS dbo.Team;
DROP TABLE IF EXISTS dbo.PlayerCredential;
DROP TABLE IF EXISTS dbo.Game;
DROP TABLE IF EXISTS dbo.AuthToken;
DROP TABLE IF EXISTS dbo.Account;
GO

/* Nếu còn bảng nào sót lại thì DỪNG, vì chạy tiếp sẽ ra lỗi "đã tồn tại" khó hiểu.
   Thường là do có bảng người khác tạo thêm mà danh sách xóa ở trên chưa biết.

   NGOẠI LỆ dbo.sysdiagrams: đó là bảng do CHÍNH SSMS tạo khi bạn dùng
   Database Diagrams để xem sơ đồ khóa ngoại. KHÔNG xóa nó (xóa là mất hết sơ đồ
   đã vẽ) và KHÔNG tính nó là bảng sót, nếu không thì cứ vẽ một sơ đồ là lần sau
   script này bị chặn không chạy được. */
IF EXISTS (SELECT 1 FROM sys.tables WHERE name <> 'sysdiagrams')
BEGIN
    DECLARE @sot NVARCHAR(2000) = N'';
    SELECT @sot = @sot + name + N', ' FROM sys.tables WHERE name <> 'sysdiagrams';
    PRINT N'DỪNG: còn bảng chưa xóa được: ' + @sot;
    PRINT N'Hãy xóa tay những bảng đó rồi chạy lại file này.';
    SET NOEXEC ON;
END
ELSE
    PRINT N'Đã dọn sạch database. Bắt đầu tạo lại từ đầu.';
GO

/*===============================================================================
  MODULE A - TÀI KHOẢN & HỒ SƠ        UC-01, UC-02, UC-03, UC-40, UC-43
===============================================================================*/

/* Account - UC-01 đăng ký, UC-02 đăng nhập, UC-03 hồ sơ, UC-43 khóa/mở tài khoản.
   Role là cột ENUM nên KHÔNG cần hai bảng Role và UserRole.
     PLAYER    - mặc định sau khi đăng ký
     ORGANIZER - được quản trị viên cấp quyền tổ chức giải
     ADMIN     - quản trị hệ thống
   Vai trò Đội trưởng suy ra từ Team.CaptainAccountID; vai trò Trọng tài suy ra
   từ Match.RefereeAccountID, nên cả hai không nằm trong ENUM này.
   Hai cột NotifyInApp / NotifyEmail thay cho bảng NotificationPreference. */
CREATE TABLE dbo.Account
(
    AccountID        INT IDENTITY(1,1) NOT NULL,
    Email            NVARCHAR(256)     NOT NULL,
    PasswordHash     VARCHAR(255)      NULL,      -- NULL khi chỉ đăng nhập bằng Google
    GoogleSubjectId  VARCHAR(100)      NULL,
    FullName         NVARCHAR(150)     NOT NULL,
    AvatarUrl        NVARCHAR(500)     NULL,
    DateOfBirth      DATE              NULL,
    Phone            VARCHAR(20)       NULL,
    School           NVARCHAR(200)     NULL,
    SocialLinks      NVARCHAR(500)     NULL,
    Role             VARCHAR(20)       NOT NULL CONSTRAINT DF_Account_Role   DEFAULT 'PLAYER',
    Status           VARCHAR(20)       NOT NULL CONSTRAINT DF_Account_Status DEFAULT 'PENDING_VERIFY',
    BanReason        NVARCHAR(500)     NULL,
    NotifyInApp      BIT               NOT NULL CONSTRAINT DF_Account_NotiApp   DEFAULT 1,
    NotifyEmail      BIT               NOT NULL CONSTRAINT DF_Account_NotiMail  DEFAULT 1,
    EmailVerifiedAt  DATETIME2(0)      NULL,
    CreatedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_Account_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Account         PRIMARY KEY (AccountID),
    CONSTRAINT UQ_Account_Email   UNIQUE (Email),
    CONSTRAINT CK_Account_Role    CHECK (Role   IN ('PLAYER','ORGANIZER','ADMIN')),
    CONSTRAINT CK_Account_Status  CHECK (Status IN ('PENDING_VERIFY','ACTIVE','BANNED')),
    -- Khóa tài khoản thì BẮT BUỘC có lý do, để còn trả lời người dùng được.
    CONSTRAINT CK_Account_Ban     CHECK (Status <> 'BANNED' OR BanReason IS NOT NULL),
    -- Phải có ít nhất một cách đăng nhập.
    CONSTRAINT CK_Account_Login   CHECK (PasswordHash IS NOT NULL OR GoogleSubjectId IS NOT NULL)
);
GO

/* Một Google ID chỉ gắn đúng một tài khoản, nhưng nhiều tài khoản được để NULL. */
CREATE UNIQUE INDEX UX_Account_Google
    ON dbo.Account(GoogleSubjectId) WHERE GoogleSubjectId IS NOT NULL;
GO

/* AuthToken - UC-02. Gộp RefreshToken và VerificationToken của bản v1.
   REFRESH         - phiên đăng nhập, UC-02 đăng xuất thì đánh dấu UsedAt
   EMAIL_VERIFY    - xác minh email sau khi đăng ký (UC-01)
   PASSWORD_RESET  - liên kết đặt lại mật khẩu, có thời hạn */
CREATE TABLE dbo.AuthToken
(
    AuthTokenID INT IDENTITY(1,1) NOT NULL,
    AccountID   INT               NOT NULL,
    TokenHash   VARCHAR(255)      NOT NULL,
    TokenType   VARCHAR(20)       NOT NULL,
    ExpiresAt   DATETIME2(0)      NOT NULL,
    UsedAt      DATETIME2(0)      NULL,
    CreatedAt   DATETIME2(0)      NOT NULL CONSTRAINT DF_AuthToken_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AuthToken          PRIMARY KEY (AuthTokenID),
    CONSTRAINT UQ_AuthToken_Hash     UNIQUE (TokenHash),
    CONSTRAINT CK_AuthToken_Type     CHECK (TokenType IN ('REFRESH','EMAIL_VERIFY','PASSWORD_RESET')),
    CONSTRAINT FK_AuthToken_Account  FOREIGN KEY (AccountID) REFERENCES dbo.Account(AccountID)
);
GO

/*===============================================================================
  MODULE B - DANH MỤC TỰA GAME        UC-43
===============================================================================*/

/* Game - UC-43 quản lý danh mục game.
   Quy tắc gốc của tựa game nằm luôn ở đây (số người, dự bị, thể thức mặc định,
   loại file minh chứng) nên KHÔNG cần bảng GameRule riêng. Đây là NGUỒN KIỂM TRA
   cho Tạo giải (UC-12) và Đăng ký đội (UC-06).
   MinStarters = 1 là hợp lệ, để chạy giải game đối kháng đơn. */
CREATE TABLE dbo.Game
(
    GameID                INT IDENTITY(1,1) NOT NULL,
    Title                 NVARCHAR(150)     NOT NULL,
    ShortCode             VARCHAR(10)       NOT NULL,
    LogoUrl               NVARCHAR(500)     NULL,
    BannerUrl             NVARCHAR(500)     NULL,
    Status                VARCHAR(20)       NOT NULL CONSTRAINT DF_Game_Status DEFAULT 'ACTIVE',
    MinStarters           INT               NOT NULL,
    MaxStarters           INT               NOT NULL,
    MaxSubstitutes        INT               NOT NULL,
    DefaultFormat         VARCHAR(30)       NOT NULL,
    AllowedEvidenceTypes  NVARCHAR(200)     NOT NULL,   -- ví dụ: 'png,jpg,jpeg,mp4,rofl'
    MaxEvidenceFileSizeMB INT               NOT NULL CONSTRAINT DF_Game_MaxSize DEFAULT 10,
    CONSTRAINT PK_Game           PRIMARY KEY (GameID),
    CONSTRAINT UQ_Game_Code      UNIQUE (ShortCode),
    CONSTRAINT UQ_Game_Title     UNIQUE (Title),
    CONSTRAINT CK_Game_Status    CHECK (Status IN ('ACTIVE','INACTIVE')),
    CONSTRAINT CK_Game_Roster    CHECK (MinStarters >= 1 AND MaxStarters >= MinStarters AND MaxSubstitutes >= 0),
    CONSTRAINT CK_Game_Format    CHECK (DefaultFormat IN ('SINGLE_ELIMINATION','DOUBLE_ELIMINATION','ROUND_ROBIN','HYBRID'))
);
GO

/* PlayerCredential - UC-03. Gộp GameAccount và IdentityDocument của bản v1.
   IN_GAME_NAME  - tên trong game, gắn với một tựa game, lưu ở Value
   STUDENT_CARD  - ảnh thẻ sinh viên, lưu ở FileUrl
   CITIZEN_ID    - ảnh giấy tờ tùy thân, lưu ở FileUrl
   Ban tổ chức đối chiếu các bản ghi này khi duyệt đơn (UC-08). */
CREATE TABLE dbo.PlayerCredential
(
    PlayerCredentialID INT IDENTITY(1,1) NOT NULL,
    AccountID          INT               NOT NULL,
    GameID             INT               NULL,       -- NULL khi là giấy tờ định danh
    CredentialType     VARCHAR(20)       NOT NULL,
    Value              NVARCHAR(150)     NULL,       -- tên trong game
    FileUrl            NVARCHAR(500)     NULL,       -- ảnh giấy tờ
    VerifiedAt         DATETIME2(0)      NULL,
    CreatedAt          DATETIME2(0)      NOT NULL CONSTRAINT DF_PlayerCred_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_PlayerCredential         PRIMARY KEY (PlayerCredentialID),
    CONSTRAINT CK_PlayerCredential_Type    CHECK (CredentialType IN ('IN_GAME_NAME','STUDENT_CARD','CITIZEN_ID')),
    -- Tên trong game phải có tựa game và có giá trị; giấy tờ phải có file.
    CONSTRAINT CK_PlayerCredential_Shape   CHECK (
        (CredentialType =  'IN_GAME_NAME' AND GameID IS NOT NULL AND Value   IS NOT NULL) OR
        (CredentialType <> 'IN_GAME_NAME' AND FileUrl IS NOT NULL)),
    CONSTRAINT FK_PlayerCredential_Account FOREIGN KEY (AccountID) REFERENCES dbo.Account(AccountID),
    CONSTRAINT FK_PlayerCredential_Game    FOREIGN KEY (GameID)    REFERENCES dbo.Game(GameID)
);
GO

/* Một người khai đúng MỘT tên trong game cho mỗi tựa game. */
CREATE UNIQUE INDEX UX_PlayerCredential_Ign
    ON dbo.PlayerCredential(AccountID, GameID) WHERE CredentialType = 'IN_GAME_NAME';
GO

/*===============================================================================
  MODULE C - ĐỘI TUYỂN & TUYỂN QUÂN   UC-04, UC-05, UC-37, UC-44
===============================================================================*/

/* Team - UC-04 tạo và quản lý đội, UC-05 gia nhập bằng mã mời.
   Mã mời đang hiệu lực nằm luôn ở đây nên KHÔNG cần bảng InviteCode riêng.
   Một đội thuộc đúng một tựa game. */
CREATE TABLE dbo.Team
(
    TeamID           INT IDENTITY(1,1) NOT NULL,
    GameID           INT               NOT NULL,
    CaptainAccountID INT               NOT NULL,
    Name             NVARCHAR(150)     NOT NULL,
    Tag              NVARCHAR(10)      NOT NULL,
    LogoUrl          NVARCHAR(500)     NULL,
    InviteCode       VARCHAR(20)       NULL,
    InviteExpiresAt  DATETIME2(0)      NULL,
    Status           VARCHAR(20)       NOT NULL CONSTRAINT DF_Team_Status DEFAULT 'ACTIVE',
    CreatedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_Team_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Team          PRIMARY KEY (TeamID),
    CONSTRAINT UQ_Team_Tag      UNIQUE (GameID, Tag),
    CONSTRAINT UQ_Team_Name     UNIQUE (GameID, Name),
    CONSTRAINT CK_Team_Status   CHECK (Status IN ('ACTIVE','DISBANDED')),
    CONSTRAINT FK_Team_Game     FOREIGN KEY (GameID)           REFERENCES dbo.Game(GameID),
    CONSTRAINT FK_Team_Captain  FOREIGN KEY (CaptainAccountID) REFERENCES dbo.Account(AccountID)
);
GO

CREATE UNIQUE INDEX UX_Team_InviteCode
    ON dbo.Team(InviteCode) WHERE InviteCode IS NOT NULL;
GO

/* RecruitmentPost - UC-44 bảng tin tuyển quân.
   LOOKING_FOR_PLAYER - đội tìm người, TeamID bắt buộc
   LOOKING_FOR_TEAM   - người tìm đội, TeamID để NULL */
CREATE TABLE dbo.RecruitmentPost
(
    RecruitmentPostID INT IDENTITY(1,1) NOT NULL,
    GameID            INT               NOT NULL,
    TeamID            INT               NULL,
    AuthorAccountID   INT               NOT NULL,
    PostType          VARCHAR(20)       NOT NULL,
    Title             NVARCHAR(200)     NOT NULL,
    Content           NVARCHAR(MAX)     NULL,
    HighlightUrl      NVARCHAR(500)     NULL,
    Status            VARCHAR(20)       NOT NULL CONSTRAINT DF_RecPost_Status DEFAULT 'OPEN',
    CreatedAt         DATETIME2(0)      NOT NULL CONSTRAINT DF_RecPost_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_RecruitmentPost        PRIMARY KEY (RecruitmentPostID),
    CONSTRAINT CK_RecruitmentPost_Type   CHECK (PostType IN ('LOOKING_FOR_PLAYER','LOOKING_FOR_TEAM')),
    CONSTRAINT CK_RecruitmentPost_Status CHECK (Status   IN ('OPEN','CLOSED')),
    CONSTRAINT CK_RecruitmentPost_Team   CHECK (
        (PostType = 'LOOKING_FOR_PLAYER' AND TeamID IS NOT NULL) OR
        (PostType = 'LOOKING_FOR_TEAM')),
    CONSTRAINT FK_RecruitmentPost_Game   FOREIGN KEY (GameID)          REFERENCES dbo.Game(GameID),
    CONSTRAINT FK_RecruitmentPost_Team   FOREIGN KEY (TeamID)          REFERENCES dbo.Team(TeamID),
    CONSTRAINT FK_RecruitmentPost_Author FOREIGN KEY (AuthorAccountID) REFERENCES dbo.Account(AccountID)
);
GO

/* TeamMember - UC-04 roster, UC-05 gia nhập và rời đội, UC-44 đơn và lời mời.
   Một bảng làm cả ba việc nên KHÔNG cần bảng JoinApplication riêng:
     Direction = JOIN_BY_CODE  người tự vào bằng mã mời  -> Status ACTIVE ngay
     Direction = APPLIED       người nộp đơn theo tin     -> chờ đội trưởng duyệt
     Direction = INVITED       đội trưởng mời trực tiếp   -> chờ người đó nhận
   GameID lặp lại từ Team để cưỡng chế được luật "một người chỉ ở một đội cho
   mỗi tựa game" bằng chỉ mục có điều kiện ở dưới. */
CREATE TABLE dbo.TeamMember
(
    TeamMemberID      INT IDENTITY(1,1) NOT NULL,
    TeamID            INT               NOT NULL,
    GameID            INT               NOT NULL,
    AccountID         INT               NOT NULL,
    RecruitmentPostID INT               NULL,
    Slot              VARCHAR(20)       NOT NULL CONSTRAINT DF_TeamMember_Slot DEFAULT 'STARTER',
    Direction         VARCHAR(20)       NOT NULL,
    Status            VARCHAR(20)       NOT NULL CONSTRAINT DF_TeamMember_Status DEFAULT 'PENDING',
    Message           NVARCHAR(500)     NULL,
    JoinedAt          DATETIME2(0)      NULL,
    LeftAt            DATETIME2(0)      NULL,
    CreatedAt         DATETIME2(0)      NOT NULL CONSTRAINT DF_TeamMember_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TeamMember          PRIMARY KEY (TeamMemberID),
    CONSTRAINT CK_TeamMember_Slot     CHECK (Slot      IN ('STARTER','SUBSTITUTE')),
    CONSTRAINT CK_TeamMember_Dir      CHECK (Direction IN ('JOIN_BY_CODE','APPLIED','INVITED')),
    CONSTRAINT CK_TeamMember_Status   CHECK (Status    IN ('PENDING','ACTIVE','REJECTED','LEFT','REMOVED')),
    CONSTRAINT FK_TeamMember_Team     FOREIGN KEY (TeamID)            REFERENCES dbo.Team(TeamID),
    CONSTRAINT FK_TeamMember_Game     FOREIGN KEY (GameID)            REFERENCES dbo.Game(GameID),
    CONSTRAINT FK_TeamMember_Account   FOREIGN KEY (AccountID)        REFERENCES dbo.Account(AccountID),
    CONSTRAINT FK_TeamMember_Post     FOREIGN KEY (RecruitmentPostID) REFERENCES dbo.RecruitmentPost(RecruitmentPostID)
);
GO

/* Hai luật của bản v1 được giữ nguyên, không cần thêm bảng nào. */
CREATE UNIQUE INDEX UX_TeamMember_ActiveInTeam
    ON dbo.TeamMember(TeamID, AccountID) WHERE Status = 'ACTIVE';
CREATE UNIQUE INDEX UX_TeamMember_OneTeamPerGame
    ON dbo.TeamMember(AccountID, GameID)  WHERE Status = 'ACTIVE';
GO

/*===============================================================================
  MODULE D - GIẢI ĐẤU & TÀI TRỢ       UC-12, UC-13, UC-14, UC-36, UC-42
===============================================================================*/

/* Tournament - UC-12 tạo và cấu hình, UC-13 thể lệ và thông tin, UC-14 công bố/hủy.
   Bản v1 tách thành Rulebook, Prize, TieBreakerRule, RoundConfig; ở đây chúng là
   cột của chính giải:
     RulebookContent / RulebookVersion - thể lệ bản hiện hành. Sửa thể lệ thì tăng
       RulebookVersion và ghi bản cũ vào AuditLog, nên bản đội đã chấp nhận
       (Registration.RulebookVersionAccepted) không bao giờ bị thay đổi.
     PrizeStructure    - cơ cấu giải thưởng dạng văn bản công bố
     TieBreakerOrder   - thứ tự tiêu chí phân định khi bằng điểm
     BestOfPerRound    - số ván mỗi vòng, ví dụ 'R1:1,R2:3,F:5'
     PenaltyThreshold  - NULL = KHÔNG tự tước quyền (UC-35)
   StreamUrl là link phát của cả giải; link theo từng trận nằm ở bảng Livestream. */
CREATE TABLE dbo.Tournament
(
    TournamentID         INT IDENTITY(1,1) NOT NULL,
    GameID               INT               NOT NULL,
    OrganizerAccountID   INT               NOT NULL,
    Name                 NVARCHAR(200)     NOT NULL,
    Description          NVARCHAR(MAX)     NULL,
    BannerUrl            NVARCHAR(500)     NULL,
    OrganizingUnit       NVARCHAR(200)     NULL,
    Region               NVARCHAR(100)     NULL,
    ContactInfo          NVARCHAR(300)     NULL,
    IsOnline             BIT               NOT NULL CONSTRAINT DF_Tour_Online DEFAULT 1,
    Venue                NVARCHAR(300)     NULL,
    Format               VARCHAR(30)       NOT NULL,
    HasThirdPlaceMatch   BIT               NOT NULL CONSTRAINT DF_Tour_Third DEFAULT 0,
    MaxTeams             INT               NOT NULL,
    StarterCount         INT               NOT NULL,
    MaxSubstitutes       INT               NOT NULL,
    RegOpenAt            DATETIME2(0)      NOT NULL,
    RegCloseAt           DATETIME2(0)      NOT NULL,
    StartAt              DATETIME2(0)      NOT NULL,
    EndAt                DATETIME2(0)      NULL,
    EntryFee             DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Tour_Fee DEFAULT 0,
    BankAccountInfo      NVARCHAR(300)     NULL,
    PrizeStructure       NVARCHAR(MAX)     NULL,
    RequireStudentCard   BIT               NOT NULL CONSTRAINT DF_Tour_ReqCard DEFAULT 1,
    RequirePaymentProof  BIT               NOT NULL CONSTRAINT DF_Tour_ReqPay  DEFAULT 1,
    RequireInGameName    BIT               NOT NULL CONSTRAINT DF_Tour_ReqIgn  DEFAULT 1,
    TieBreakerOrder      NVARCHAR(200)     NULL,
    BestOfPerRound       NVARCHAR(200)     NULL,
    PenaltyThreshold     INT               NULL,
    RulebookContent      NVARCHAR(MAX)     NULL,
    RulebookVersion      INT               NOT NULL CONSTRAINT DF_Tour_RbVer DEFAULT 1,
    StreamUrl            NVARCHAR(500)     NULL,
    Status               VARCHAR(20)       NOT NULL CONSTRAINT DF_Tour_Status DEFAULT 'DRAFT',
    CancelReason         NVARCHAR(500)     NULL,
    CreatedAt            DATETIME2(0)      NOT NULL CONSTRAINT DF_Tour_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Tournament            PRIMARY KEY (TournamentID),
    CONSTRAINT CK_Tournament_Format     CHECK (Format IN ('SINGLE_ELIMINATION','DOUBLE_ELIMINATION','ROUND_ROBIN','HYBRID')),
    CONSTRAINT CK_Tournament_Status     CHECK (Status IN ('DRAFT','PUBLISHED','ONGOING','COMPLETED','CANCELLED')),
    CONSTRAINT CK_Tournament_Scale      CHECK (MaxTeams >= 2 AND StarterCount >= 1 AND MaxSubstitutes >= 0),
    -- Mốc thời gian phải theo đúng thứ tự (MSG-TOUR-001 của bản v1).
    CONSTRAINT CK_Tournament_Timeline   CHECK (RegOpenAt < RegCloseAt AND RegCloseAt <= StartAt),
    CONSTRAINT CK_Tournament_End        CHECK (EndAt IS NULL OR EndAt >= StartAt),
    CONSTRAINT CK_Tournament_Fee        CHECK (EntryFee >= 0),
    CONSTRAINT CK_Tournament_Threshold  CHECK (PenaltyThreshold IS NULL OR PenaltyThreshold >= 1),
    -- Hủy giải thì BẮT BUỘC có lý do, vì phải thông báo cho mọi đội đã đăng ký.
    CONSTRAINT CK_Tournament_Cancel     CHECK (Status <> 'CANCELLED' OR CancelReason IS NOT NULL),
    CONSTRAINT FK_Tournament_Game       FOREIGN KEY (GameID)             REFERENCES dbo.Game(GameID),
    CONSTRAINT FK_Tournament_Organizer  FOREIGN KEY (OrganizerAccountID) REFERENCES dbo.Account(AccountID)
);
GO

/* TournamentSponsor - UC-42. Nhà tài trợ và mẩu quảng cáo GẮN VỚI ĐÚNG MỘT GIẢI.
   Thêm / sửa / xóa theo từng giải. Kích thước logo trên trang giải suy ra từ Tier. */
CREATE TABLE dbo.TournamentSponsor
(
    TournamentSponsorID INT IDENTITY(1,1) NOT NULL,
    TournamentID        INT               NOT NULL,
    Name                NVARCHAR(200)     NOT NULL,
    LogoUrl             NVARCHAR(500)     NULL,
    WebsiteUrl          NVARCHAR(500)     NULL,
    ContactInfo         NVARCHAR(300)     NULL,
    Tier                VARCHAR(20)       NOT NULL,
    DisplayOrder        INT               NOT NULL CONSTRAINT DF_TourSponsor_Order DEFAULT 1,
    CreatedByAccountID  INT               NOT NULL,
    CreatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_TourSponsor_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TournamentSponsor       PRIMARY KEY (TournamentSponsorID),
    CONSTRAINT UQ_TournamentSponsor_Name  UNIQUE (TournamentID, Name),
    CONSTRAINT CK_TournamentSponsor_Tier  CHECK (Tier IN ('DIAMOND','GOLD','SILVER','BRONZE','PARTNER')),
    CONSTRAINT CK_TournamentSponsor_Order CHECK (DisplayOrder >= 1),
    CONSTRAINT FK_TournamentSponsor_Tour  FOREIGN KEY (TournamentID)       REFERENCES dbo.Tournament(TournamentID),
    CONSTRAINT FK_TournamentSponsor_By    FOREIGN KEY (CreatedByAccountID) REFERENCES dbo.Account(AccountID)
);
GO

/*===============================================================================
  MODULE E - ĐĂNG KÝ & LỆ PHÍ         UC-06 … UC-11
===============================================================================*/

/* Registration - UC-06 nộp đơn, UC-07 theo dõi, UC-08 duyệt, UC-09 lệ phí,
   UC-10 hàng chờ, UC-11 rút đơn.
   Bản v1 tách WaitlistEntry và TeamTitle; ở đây là cột:
     WaitlistPosition - vị trí trong hàng chờ khi Status = WAITLISTED
     PaymentStatus    - trạng thái lệ phí, KHÔNG phải giao dịch (LI-02)
     FinalPlacement   - thứ hạng chung cuộc, nguồn cho bảng xếp hạng (UC-19, UC-38)
   RulebookVersionAccepted đóng băng bản thể lệ đội đã cam kết (UC-06, UC-13). */
CREATE TABLE dbo.Registration
(
    RegistrationID          INT IDENTITY(1,1) NOT NULL,
    TournamentID            INT               NOT NULL,
    TeamID                  INT               NOT NULL,
    SubmittedByAccountID    INT               NOT NULL,
    RulebookVersionAccepted INT               NULL,
    ConsentAt               DATETIME2(0)      NULL,
    Status                  VARCHAR(20)       NOT NULL CONSTRAINT DF_Reg_Status DEFAULT 'DRAFT',
    WaitlistPosition        INT               NULL,
    PaymentStatus           VARCHAR(20)       NOT NULL CONSTRAINT DF_Reg_Pay DEFAULT 'UNPAID',
    ReviewNote              NVARCHAR(500)     NULL,
    FinalPlacement          VARCHAR(20)       NOT NULL CONSTRAINT DF_Reg_Place DEFAULT 'NONE',
    FinalRank               INT               NULL,
    SubmittedAt             DATETIME2(0)      NULL,
    ReviewedAt              DATETIME2(0)      NULL,
    CONSTRAINT PK_Registration          PRIMARY KEY (RegistrationID),
    CONSTRAINT CK_Registration_Status   CHECK (Status IN ('DRAFT','PENDING','APPROVED','REJECTED','WAITLISTED','WITHDRAWN','DISQUALIFIED')),
    CONSTRAINT CK_Registration_Pay      CHECK (PaymentStatus IN ('UNPAID','PROOF_SUBMITTED','CONFIRMED','REFUNDED')),
    CONSTRAINT CK_Registration_Place    CHECK (FinalPlacement IN ('NONE','CHAMPION','RUNNER_UP','THIRD','PARTICIPANT')),
    -- Từ chối đơn thì BẮT BUỘC kèm lý do (UC-08).
    CONSTRAINT CK_Registration_Reject   CHECK (Status <> 'REJECTED' OR ReviewNote IS NOT NULL),
    -- Chỉ đơn trong hàng chờ mới có vị trí hàng chờ (UC-10).
    CONSTRAINT CK_Registration_Wait     CHECK ((Status = 'WAITLISTED' AND WaitlistPosition IS NOT NULL)
                                            OR (Status <> 'WAITLISTED' AND WaitlistPosition IS NULL)),
    CONSTRAINT FK_Registration_Tour     FOREIGN KEY (TournamentID)         REFERENCES dbo.Tournament(TournamentID),
    CONSTRAINT FK_Registration_Team     FOREIGN KEY (TeamID)               REFERENCES dbo.Team(TeamID),
    CONSTRAINT FK_Registration_By       FOREIGN KEY (SubmittedByAccountID) REFERENCES dbo.Account(AccountID)
);
GO

/* Một đội chỉ có MỘT đơn còn sống trong một giải; rút hoặc bị từ chối thì được nộp lại. */
CREATE UNIQUE INDEX UX_Registration_OneLivePerTeam
    ON dbo.Registration(TournamentID, TeamID)
    WHERE Status <> 'WITHDRAWN' AND Status <> 'REJECTED';
/* Vị trí hàng chờ không được trùng trong cùng một giải. */
CREATE UNIQUE INDEX UX_Registration_WaitPosition
    ON dbo.Registration(TournamentID, WaitlistPosition) WHERE Status = 'WAITLISTED';
GO

/* RegistrationLineup - UC-06 khai đội hình, UC-08 đối chiếu từng người.
   InGameNameSnapshot chụp lại tên trong game LÚC NỘP ĐƠN, để sau này người chơi
   đổi tên thì hồ sơ đã duyệt vẫn tra được. */
CREATE TABLE dbo.RegistrationLineup
(
    RegistrationLineupID INT IDENTITY(1,1) NOT NULL,
    RegistrationID       INT               NOT NULL,
    AccountID            INT               NOT NULL,
    Slot                 VARCHAR(20)       NOT NULL,
    InGameNameSnapshot   NVARCHAR(150)     NULL,
    PlayerCredentialID   INT               NULL,
    CONSTRAINT PK_RegistrationLineup       PRIMARY KEY (RegistrationLineupID),
    CONSTRAINT UQ_RegistrationLineup_User  UNIQUE (RegistrationID, AccountID),
    CONSTRAINT CK_RegistrationLineup_Slot  CHECK (Slot IN ('STARTER','SUBSTITUTE')),
    CONSTRAINT FK_RegistrationLineup_Reg   FOREIGN KEY (RegistrationID)     REFERENCES dbo.Registration(RegistrationID),
    CONSTRAINT FK_RegistrationLineup_Acc   FOREIGN KEY (AccountID)          REFERENCES dbo.Account(AccountID),
    CONSTRAINT FK_RegistrationLineup_Cred  FOREIGN KEY (PlayerCredentialID) REFERENCES dbo.PlayerCredential(PlayerCredentialID)
);
GO

/* PaymentRecord - UC-09 minh chứng lệ phí, UC-11 ghi nhận hoàn tiền.
   KHÔNG PHẢI BẢNG GIAO DỊCH (LI-02). Chỉ lưu ảnh biên lai, số tiền ĐỘI KHAI BÁO,
   ai ghi nhận và lúc nào. Hệ thống không bao giờ giữ số dư, không chuyển tiền.
   Thay biên lai mới thì biên lai cũ vẫn giữ với IsCurrent = 0. */
CREATE TABLE dbo.PaymentRecord
(
    PaymentRecordID      INT IDENTITY(1,1) NOT NULL,
    RegistrationID       INT               NOT NULL,
    RecordKind           VARCHAR(20)       NOT NULL,
    FileUrl              NVARCHAR(500)     NULL,
    DeclaredAmount       DECIMAL(18,2)     NULL,
    Note                 NVARCHAR(500)     NULL,
    IsCurrent            BIT               NOT NULL CONSTRAINT DF_PayRec_Current DEFAULT 1,
    RecordedByAccountID  INT               NOT NULL,
    RecordedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_PayRec_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_PaymentRecord        PRIMARY KEY (PaymentRecordID),
    CONSTRAINT CK_PaymentRecord_Kind   CHECK (RecordKind IN ('PROOF','REFUND')),
    CONSTRAINT CK_PaymentRecord_Amount CHECK (DeclaredAmount IS NULL OR DeclaredAmount >= 0),
    -- Minh chứng lệ phí phải có file; bản ghi hoàn tiền thì cần lý do.
    CONSTRAINT CK_PaymentRecord_Shape  CHECK (
        (RecordKind = 'PROOF'  AND FileUrl IS NOT NULL) OR
        (RecordKind = 'REFUND' AND Note    IS NOT NULL)),
    CONSTRAINT FK_PaymentRecord_Reg     FOREIGN KEY (RegistrationID)      REFERENCES dbo.Registration(RegistrationID),
    CONSTRAINT FK_PaymentRecord_By      FOREIGN KEY (RecordedByAccountID) REFERENCES dbo.Account(AccountID)
);
GO

/* Mỗi đơn chỉ có MỘT biên lai đang hiệu lực. */
CREATE UNIQUE INDEX UX_PaymentRecord_CurrentProof
    ON dbo.PaymentRecord(RegistrationID) WHERE RecordKind = 'PROOF' AND IsCurrent = 1;
GO

/*===============================================================================
  MODULE F - NHÁNH ĐẤU, LỊCH & TRẬN   UC-15 … UC-26, UC-45
===============================================================================*/

/* BracketNode - UC-15 sinh nhánh và suất đặc cách, UC-16 xếp hạt giống và khóa,
   UC-18 chuyển giai đoạn, UC-20 xem nhánh đấu.
   MỘT bảng thay cho bốn bảng TournamentStage, Bracket, BracketGroup, BracketSeed
   của bản v1: cây tự trỏ qua ParentNodeID.
     StageType = GROUP    -> nút là một suất trong bảng đấu vòng tròn
     StageType = KNOCKOUT -> nút là một ô trận trên cây loại trực tiếp
   IsBye = 1 là suất đặc cách khi số đội không phải lũy thừa của 2 (UC-15).
   Status = LOCKED thì không được sinh lại nhánh (UC-16). */
CREATE TABLE dbo.BracketNode
(
    BracketNodeID     INT IDENTITY(1,1) NOT NULL,
    TournamentID      INT               NOT NULL,
    ParentNodeID      INT               NULL,
    StageNo           INT               NOT NULL CONSTRAINT DF_BrNode_Stage DEFAULT 1,
    StageType         VARCHAR(20)       NOT NULL,
    GroupLabel        NVARCHAR(10)      NULL,
    RoundNo           INT               NULL,
    PositionInRound   INT               NULL,
    NodeType          VARCHAR(20)       NOT NULL,
    SeedTeamID        INT               NULL,
    SeedNo            INT               NULL,
    IsBye             BIT               NOT NULL CONSTRAINT DF_BrNode_Bye DEFAULT 0,
    SeedingMethod     VARCHAR(20)       NOT NULL CONSTRAINT DF_BrNode_Seeding DEFAULT 'MANUAL',
    SeedingRandomSeed INT               NULL,
    Status            VARCHAR(20)       NOT NULL CONSTRAINT DF_BrNode_Status DEFAULT 'DRAFT',
    GeneratedAt       DATETIME2(0)      NULL,
    CONSTRAINT PK_BracketNode          PRIMARY KEY (BracketNodeID),
    CONSTRAINT CK_BracketNode_Stage    CHECK (StageType     IN ('GROUP','KNOCKOUT')),
    CONSTRAINT CK_BracketNode_Node     CHECK (NodeType      IN ('GROUP_SLOT','MATCH_SLOT')),
    CONSTRAINT CK_BracketNode_Seeding  CHECK (SeedingMethod IN ('MANUAL','RANDOM_DRAW','BY_RANKING')),
    CONSTRAINT CK_BracketNode_Status   CHECK (Status        IN ('DRAFT','LOCKED')),
    CONSTRAINT CK_BracketNode_StageNo  CHECK (StageNo >= 1),
    -- Chỉ bốc thăm ngẫu nhiên mới được lưu hạt giống, để tái lập được kết quả bốc.
    CONSTRAINT CK_BracketNode_Random   CHECK (SeedingRandomSeed IS NULL OR SeedingMethod = 'RANDOM_DRAW'),
    CONSTRAINT FK_BracketNode_Tour     FOREIGN KEY (TournamentID) REFERENCES dbo.Tournament(TournamentID),
    CONSTRAINT FK_BracketNode_Parent   FOREIGN KEY (ParentNodeID) REFERENCES dbo.BracketNode(BracketNodeID),
    CONSTRAINT FK_BracketNode_Seed     FOREIGN KEY (SeedTeamID)   REFERENCES dbo.Team(TeamID)
);
GO

CREATE INDEX IX_BracketNode_Tour   ON dbo.BracketNode(TournamentID, StageNo, RoundNo);
CREATE INDEX IX_BracketNode_Parent ON dbo.BracketNode(ParentNodeID);
GO

/* Match - UC-17 xếp lịch và gán trọng tài, UC-21 check-in, UC-23 thắng trắng,
   UC-24 tước quyền, UC-25 mã phòng, UC-28 chốt tự động, UC-32 phán quyết.
   Bản v1 có bảng check-in riêng và bảng TournamentReferee; ở đây là cột.
   Trận KHÔNG có trọng tài vẫn hợp lệ - khi đó quyền phân xử thuộc ban tổ chức.
   ResultSource cho biết tỉ số cuối đến từ đâu, phục vụ ràng buộc LI-01. */
CREATE TABLE dbo.Match
(
    MatchID           INT IDENTITY(1,1) NOT NULL,
    TournamentID      INT               NOT NULL,
    BracketNodeID     INT               NOT NULL,
    TeamAID           INT               NULL,
    TeamBID           INT               NULL,
    RefereeAccountID  INT               NULL,
    BestOf            INT               NOT NULL CONSTRAINT DF_Match_BestOf DEFAULT 1,
    ScheduledAt       DATETIME2(0)      NULL,
    RoomCode          NVARCHAR(100)     NULL,
    RoomPassword      NVARCHAR(100)     NULL,
    CheckInOpenAt     DATETIME2(0)      NULL,
    TeamACheckedInAt  DATETIME2(0)      NULL,
    TeamBCheckedInAt  DATETIME2(0)      NULL,
    CheckInMode       VARCHAR(20)       NOT NULL CONSTRAINT DF_Match_CiMode DEFAULT 'SELF',
    OverrideReason    NVARCHAR(500)     NULL,
    ScoreA            INT               NULL,
    ScoreB            INT               NULL,
    WinnerTeamID      INT               NULL,
    Status            VARCHAR(20)       NOT NULL CONSTRAINT DF_Match_Status DEFAULT 'PENDING',
    ResultSource      VARCHAR(20)       NULL,
    ReportDeadlineAt  DATETIME2(0)      NULL,
    VerifiedAt        DATETIME2(0)      NULL,
    CONSTRAINT PK_Match           PRIMARY KEY (MatchID),
    CONSTRAINT CK_Match_BestOf    CHECK (BestOf IN (1,3,5,7)),
    CONSTRAINT CK_Match_CiMode    CHECK (CheckInMode IN ('SELF','MANUAL_OVERRIDE')),
    CONSTRAINT CK_Match_Status    CHECK (Status IN ('PENDING','READY','LIVE','WAITING_REPORT','DISPUTED','VERIFIED','WALKOVER','CANCELLED')),
    CONSTRAINT CK_Match_Source    CHECK (ResultSource IS NULL OR ResultSource IN ('AUTO_MATCHED','VAR_RULING','WALKOVER')),
    CONSTRAINT CK_Match_Score     CHECK ((ScoreA IS NULL AND ScoreB IS NULL) OR (ScoreA >= 0 AND ScoreB >= 0)),
    -- Hai đội của một trận phải khác nhau.
    CONSTRAINT CK_Match_TwoTeams  CHECK (TeamAID IS NULL OR TeamBID IS NULL OR TeamAID <> TeamBID),
    -- Check-in thủ công thì BẮT BUỘC có lý do (UC-21).
    CONSTRAINT CK_Match_Override  CHECK (CheckInMode <> 'MANUAL_OVERRIDE' OR OverrideReason IS NOT NULL),
    -- Trận đã chốt thì phải có tỉ số và phải nói rõ tỉ số đến từ đâu.
    CONSTRAINT CK_Match_Verified  CHECK (Status <> 'VERIFIED'
                                      OR (ScoreA IS NOT NULL AND ScoreB IS NOT NULL AND ResultSource IS NOT NULL)),
    CONSTRAINT FK_Match_Tour      FOREIGN KEY (TournamentID)     REFERENCES dbo.Tournament(TournamentID),
    CONSTRAINT FK_Match_Node      FOREIGN KEY (BracketNodeID)    REFERENCES dbo.BracketNode(BracketNodeID),
    CONSTRAINT FK_Match_TeamA     FOREIGN KEY (TeamAID)          REFERENCES dbo.Team(TeamID),
    CONSTRAINT FK_Match_TeamB     FOREIGN KEY (TeamBID)          REFERENCES dbo.Team(TeamID),
    CONSTRAINT FK_Match_Referee   FOREIGN KEY (RefereeAccountID) REFERENCES dbo.Account(AccountID),
    CONSTRAINT FK_Match_Winner    FOREIGN KEY (WinnerTeamID)     REFERENCES dbo.Team(TeamID)
);
GO

CREATE INDEX IX_Match_Tour     ON dbo.Match(TournamentID, Status);
CREATE INDEX IX_Match_Schedule ON dbo.Match(ScheduledAt);
GO

/* MatchGame - UC-45 tỉ số trực tiếp từng ván, và tỉ số từng ván của BO3/BO5.
   Trọng tài HOẶC ban tổ chức nhập ngay khi mỗi ván kết thúc; đội trưởng KHÔNG
   nhập ở đây - họ nộp báo cáo mù cuối trận qua MatchReport (UC-27).
   UpdatedAt là mốc để đẩy cập nhật thời gian thực lên trang trận và nhánh đấu. */
CREATE TABLE dbo.MatchGame
(
    MatchGameID       INT IDENTITY(1,1) NOT NULL,
    MatchID           INT               NOT NULL,
    GameNo            INT               NOT NULL,
    ScoreA            INT               NULL,
    ScoreB            INT               NULL,
    WinnerTeamID      INT               NULL,
    UpdatedByAccountID INT              NULL,
    UpdatedAt         DATETIME2(0)      NULL,
    CONSTRAINT PK_MatchGame         PRIMARY KEY (MatchGameID),
    CONSTRAINT UQ_MatchGame_Number  UNIQUE (MatchID, GameNo),
    CONSTRAINT CK_MatchGame_Number  CHECK (GameNo >= 1),
    CONSTRAINT CK_MatchGame_Score   CHECK ((ScoreA IS NULL AND ScoreB IS NULL) OR (ScoreA >= 0 AND ScoreB >= 0)),
    CONSTRAINT FK_MatchGame_Match   FOREIGN KEY (MatchID)            REFERENCES dbo.Match(MatchID),
    CONSTRAINT FK_MatchGame_Winner  FOREIGN KEY (WinnerTeamID)       REFERENCES dbo.Team(TeamID),
    CONSTRAINT FK_MatchGame_By      FOREIGN KEY (UpdatedByAccountID) REFERENCES dbo.Account(AccountID)
);
GO

/* MatchRequest - UC-22 xin thay người, UC-26 xin đấu lại sau sự cố.
   Hai việc cùng một hình: một yêu cầu trên một trận, có người duyệt. */
CREATE TABLE dbo.MatchRequest
(
    MatchRequestID      INT IDENTITY(1,1) NOT NULL,
    MatchID             INT               NOT NULL,
    TeamID              INT               NULL,       -- NULL khi ban tổ chức tự ra lệnh đấu lại
    RequestKind         VARCHAR(20)       NOT NULL,
    OutAccountID        INT               NULL,
    InAccountID         INT               NULL,
    Reason              NVARCHAR(500)     NOT NULL,
    Status              VARCHAR(20)       NOT NULL CONSTRAINT DF_MatchReq_Status DEFAULT 'PENDING',
    RequestedByAccountID INT              NOT NULL,
    DecidedByAccountID  INT               NULL,
    DecidedAt           DATETIME2(0)      NULL,
    CreatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_MatchReq_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_MatchRequest        PRIMARY KEY (MatchRequestID),
    CONSTRAINT CK_MatchRequest_Kind   CHECK (RequestKind IN ('SUBSTITUTION','REMATCH')),
    CONSTRAINT CK_MatchRequest_Status CHECK (Status IN ('PENDING','APPROVED','REJECTED')),
    -- Xin thay người thì phải nói rõ ai ra ai vào.
    CONSTRAINT CK_MatchRequest_Sub    CHECK (RequestKind <> 'SUBSTITUTION'
                                          OR (OutAccountID IS NOT NULL AND InAccountID IS NOT NULL AND OutAccountID <> InAccountID)),
    CONSTRAINT FK_MatchRequest_Match  FOREIGN KEY (MatchID)              REFERENCES dbo.Match(MatchID),
    CONSTRAINT FK_MatchRequest_Team   FOREIGN KEY (TeamID)               REFERENCES dbo.Team(TeamID),
    CONSTRAINT FK_MatchRequest_Out    FOREIGN KEY (OutAccountID)         REFERENCES dbo.Account(AccountID),
    CONSTRAINT FK_MatchRequest_In     FOREIGN KEY (InAccountID)          REFERENCES dbo.Account(AccountID),
    CONSTRAINT FK_MatchRequest_By     FOREIGN KEY (RequestedByAccountID) REFERENCES dbo.Account(AccountID),
    CONSTRAINT FK_MatchRequest_Dec    FOREIGN KEY (DecidedByAccountID)   REFERENCES dbo.Account(AccountID)
);
GO

/* MatchChatMessage - UC-25 phòng chờ trận để hai đội và trọng tài trao mã phòng.
   Cờ tố cáo nằm ngay trên tin nhắn nên KHÔNG cần bảng báo cáo hành vi riêng.
   Log chat này cũng là bằng chứng khi xét tranh chấp (UC-31). */
CREATE TABLE dbo.MatchChatMessage
(
    MatchChatMessageID  INT IDENTITY(1,1) NOT NULL,
    MatchID             INT               NOT NULL,
    SenderAccountID     INT               NOT NULL,
    Content             NVARCHAR(2000)    NOT NULL,
    IsPinned            BIT               NOT NULL CONSTRAINT DF_MatchChat_Pin DEFAULT 0,
    ReportStatus        VARCHAR(20)       NOT NULL CONSTRAINT DF_MatchChat_Rep DEFAULT 'NONE',
    ReportedByAccountID INT               NULL,
    ReportNote          NVARCHAR(500)     NULL,
    SentAt              DATETIME2(0)      NOT NULL CONSTRAINT DF_MatchChat_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_MatchChatMessage        PRIMARY KEY (MatchChatMessageID),
    CONSTRAINT CK_MatchChatMessage_Report CHECK (ReportStatus IN ('NONE','REPORTED','ACTIONED')),
    CONSTRAINT CK_MatchChatMessage_By     CHECK (ReportStatus = 'NONE' OR ReportedByAccountID IS NOT NULL),
    CONSTRAINT FK_MatchChatMessage_Match  FOREIGN KEY (MatchID)             REFERENCES dbo.Match(MatchID),
    CONSTRAINT FK_MatchChatMessage_Sender FOREIGN KEY (SenderAccountID)     REFERENCES dbo.Account(AccountID),
    CONSTRAINT FK_MatchChatMessage_Rep    FOREIGN KEY (ReportedByAccountID) REFERENCES dbo.Account(AccountID)
);
GO

CREATE INDEX IX_MatchChatMessage_Match ON dbo.MatchChatMessage(MatchID, SentAt);
GO

/*===============================================================================
  MODULE G - KẾT QUẢ, TRANH CHẤP & KỶ LUẬT   UC-27 … UC-35, UC-39
===============================================================================*/

/* MatchReport - UC-27 báo cáo mù, UC-28 đối chiếu tự động.
   Mỗi đội MỘT dòng, nộp độc lập và KHÔNG thấy số bên kia trước khi nộp.
   Đã nộp thì không sửa, không rút - đó là lý do bảng này không có cột UpdatedAt.
   PerGameScores giữ tỉ số từng ván do chính đội đó khai, ví dụ '1-0,0-1,1-0'.
   Đây là MỘT TRONG HAI đường duy nhất để tỉ số vào hệ thống (LI-01). */
CREATE TABLE dbo.MatchReport
(
    MatchReportID       INT IDENTITY(1,1) NOT NULL,
    MatchID             INT               NOT NULL,
    TeamID              INT               NOT NULL,
    ReportedByAccountID INT               NOT NULL,
    ReportedScoreA      INT               NOT NULL,
    ReportedScoreB      INT               NOT NULL,
    PerGameScores       NVARCHAR(200)     NULL,
    Status              VARCHAR(20)       NOT NULL CONSTRAINT DF_MatchReport_Status DEFAULT 'SUBMITTED',
    SubmittedAt         DATETIME2(0)      NOT NULL CONSTRAINT DF_MatchReport_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_MatchReport           PRIMARY KEY (MatchReportID),
    -- Một đội chỉ nộp được một bản cho một trận; đây là UNIQUE, không phải khóa chính ghép.
    CONSTRAINT UQ_MatchReport_TeamMatch UNIQUE (MatchID, TeamID),
    CONSTRAINT CK_MatchReport_Status    CHECK (Status IN ('SUBMITTED','MATCHED','CONFLICTING','MISSING')),
    CONSTRAINT CK_MatchReport_Score     CHECK (ReportedScoreA >= 0 AND ReportedScoreB >= 0),
    CONSTRAINT FK_MatchReport_Match     FOREIGN KEY (MatchID)             REFERENCES dbo.Match(MatchID),
    CONSTRAINT FK_MatchReport_Team      FOREIGN KEY (TeamID)              REFERENCES dbo.Team(TeamID),
    CONSTRAINT FK_MatchReport_By        FOREIGN KEY (ReportedByAccountID) REFERENCES dbo.Account(AccountID)
);
GO

/* Dispute - UC-29 quá hạn báo cáo, UC-30 cắm cờ, UC-31 xét tranh chấp,
   UC-32 phán quyết, UC-33 khiếu nại.
   Bản v1 tách bảng Appeal và bảng cắm cờ; ở đây là cột của cùng một vòng đời.
   AppealCount cưỡng chế luật "chỉ được khiếu nại MỘT lần" (UC-33). */
CREATE TABLE dbo.Dispute
(
    DisputeID            INT IDENTITY(1,1) NOT NULL,
    MatchID              INT               NOT NULL,
    OpenReason           VARCHAR(30)       NOT NULL,
    FlaggedByAccountID   INT               NULL,
    FlagCategory         VARCHAR(30)       NULL,
    FlagNote             NVARCHAR(1000)    NULL,
    Status               VARCHAR(20)       NOT NULL CONSTRAINT DF_Dispute_Status DEFAULT 'OPEN',
    FinalScoreA          INT               NULL,
    FinalScoreB          INT               NULL,
    RulingReason         NVARCHAR(1000)    NULL,
    ResolvedByAccountID  INT               NULL,
    ResolvedAt           DATETIME2(0)      NULL,
    AppealCount          INT               NOT NULL CONSTRAINT DF_Dispute_AppealN DEFAULT 0,
    AppealReason         NVARCHAR(1000)    NULL,
    AppealStatus         VARCHAR(20)       NOT NULL CONSTRAINT DF_Dispute_AppealS DEFAULT 'NONE',
    OpenedAt             DATETIME2(0)      NOT NULL CONSTRAINT DF_Dispute_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Dispute            PRIMARY KEY (DisputeID),
    CONSTRAINT CK_Dispute_Open       CHECK (OpenReason IN ('CONFLICTING_REPORT','REPORT_TIMEOUT','FLAGGED')),
    CONSTRAINT CK_Dispute_Status     CHECK (Status IN ('OPEN','UNDER_REVIEW','RESOLVED','CLOSED')),
    CONSTRAINT CK_Dispute_FlagCat    CHECK (FlagCategory IS NULL OR FlagCategory IN ('WRONG_SCORE','CHEATING','INELIGIBLE_PLAYER','OTHER')),
    CONSTRAINT CK_Dispute_AppealS    CHECK (AppealStatus IN ('NONE','PENDING','ACCEPTED','REJECTED')),
    -- Cắm cờ thì BẮT BUỘC có người cắm, phân loại và giải thích (UC-30).
    CONSTRAINT CK_Dispute_Flag       CHECK (OpenReason <> 'FLAGGED'
                                         OR (FlaggedByAccountID IS NOT NULL AND FlagCategory IS NOT NULL AND FlagNote IS NOT NULL)),
    -- Phán quyết thì BẮT BUỘC có tỉ số cuối, lý do viết ra, và ai đã quyết (UC-32).
    CONSTRAINT CK_Dispute_Ruling     CHECK (Status <> 'RESOLVED'
                                         OR (FinalScoreA IS NOT NULL AND FinalScoreB IS NOT NULL
                                             AND RulingReason IS NOT NULL AND ResolvedByAccountID IS NOT NULL)),
    -- Chỉ được khiếu nại một lần (UC-33).
    CONSTRAINT CK_Dispute_AppealN    CHECK (AppealCount BETWEEN 0 AND 1),
    CONSTRAINT FK_Dispute_Match      FOREIGN KEY (MatchID)             REFERENCES dbo.Match(MatchID),
    CONSTRAINT FK_Dispute_Flagger    FOREIGN KEY (FlaggedByAccountID)  REFERENCES dbo.Account(AccountID),
    CONSTRAINT FK_Dispute_Resolver   FOREIGN KEY (ResolvedByAccountID) REFERENCES dbo.Account(AccountID)
);
GO

/* Một trận chỉ có MỘT tranh chấp đang mở. */
CREATE UNIQUE INDEX UX_Dispute_LivePerMatch
    ON dbo.Dispute(MatchID) WHERE Status <> 'CLOSED' AND Status <> 'RESOLVED';
GO

/* Penalty - UC-34 cấp và hủy thẻ phạt, UC-35 tước quyền khi đủ điểm.
   Tính theo ĐIỂM: mỗi án có số Points theo mức độ, tổng điểm đang hiệu lực so
   với Tournament.PenaltyThreshold. Ngưỡng để NULL thì KHÔNG tự tước quyền.
   Án cấp nhầm KHÔNG bị xóa: hủy bằng một dòng CANCELLATION trỏ vào án cũ, cả
   hai vẫn hiện trong hồ sơ kỷ luật (UC-34, UC-37). */
CREATE TABLE dbo.Penalty
(
    PenaltyID          INT IDENTITY(1,1) NOT NULL,
    TournamentID       INT               NOT NULL,
    TeamID             INT               NOT NULL,
    MatchID            INT               NULL,
    Severity           VARCHAR(20)       NOT NULL,
    Points             INT               NOT NULL CONSTRAINT DF_Penalty_Points DEFAULT 1,
    Reason             NVARCHAR(1000)    NOT NULL,
    IssuedByAccountID  INT               NOT NULL,
    Status             VARCHAR(20)       NOT NULL CONSTRAINT DF_Penalty_Status DEFAULT 'ACTIVE',
    CancelsPenaltyID   INT               NULL,
    CancelReason       NVARCHAR(500)     NULL,
    IssuedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_Penalty_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Penalty           PRIMARY KEY (PenaltyID),
    CONSTRAINT CK_Penalty_Severity  CHECK (Severity IN ('WARNING','MINOR','MAJOR','DISQUALIFICATION','CANCELLATION')),
    CONSTRAINT CK_Penalty_Status    CHECK (Status   IN ('ACTIVE','EXPIRED','CANCELLED')),
    CONSTRAINT CK_Penalty_Points    CHECK (Points >= 0),
    CONSTRAINT CK_Penalty_Cancel    CHECK ((Severity =  'CANCELLATION' AND CancelsPenaltyID IS NOT NULL AND CancelReason IS NOT NULL)
                                        OR (Severity <> 'CANCELLATION' AND CancelsPenaltyID IS NULL)),
    CONSTRAINT FK_Penalty_Tour      FOREIGN KEY (TournamentID)      REFERENCES dbo.Tournament(TournamentID),
    CONSTRAINT FK_Penalty_Team      FOREIGN KEY (TeamID)            REFERENCES dbo.Team(TeamID),
    CONSTRAINT FK_Penalty_Match     FOREIGN KEY (MatchID)           REFERENCES dbo.Match(MatchID),
    CONSTRAINT FK_Penalty_By        FOREIGN KEY (IssuedByAccountID) REFERENCES dbo.Account(AccountID),
    CONSTRAINT FK_Penalty_Cancels   FOREIGN KEY (CancelsPenaltyID)  REFERENCES dbo.Penalty(PenaltyID)
);
GO

/* Một án chỉ bị hủy đúng một lần. */
CREATE UNIQUE INDEX UX_Penalty_CancelsOnce
    ON dbo.Penalty(CancelsPenaltyID) WHERE CancelsPenaltyID IS NOT NULL;
CREATE INDEX IX_Penalty_TeamTour ON dbo.Penalty(TournamentID, TeamID, Status);
GO

/* Attachment - UC-27 minh chứng kèm báo cáo, UC-31 và UC-39 xem bằng chứng.
   Kho file DÙNG CHUNG thay cho bảng EvidenceFile riêng.
   ĐÁNH ĐỔI ĐÃ BIẾT: OwnerType + OwnerID là tham chiếu đa hình nên cơ sở dữ liệu
   KHÔNG kiểm được file có trỏ đúng dòng tồn tại hay không. Tầng ứng dụng phải
   kiểm, và khi xóa dòng chủ thì phải tự xóa file theo. */
CREATE TABLE dbo.Attachment
(
    AttachmentID       INT IDENTITY(1,1) NOT NULL,
    OwnerType          VARCHAR(20)       NOT NULL,
    OwnerID            INT               NOT NULL,
    FileUrl            NVARCHAR(500)     NOT NULL,
    FileType           VARCHAR(20)       NOT NULL,
    SizeBytes          BIGINT            NULL,
    UploadedByAccountID INT              NOT NULL,
    UploadedAt         DATETIME2(0)      NOT NULL CONSTRAINT DF_Attachment_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Attachment          PRIMARY KEY (AttachmentID),
    CONSTRAINT CK_Attachment_Owner    CHECK (OwnerType IN ('MATCH_REPORT','DISPUTE','TOURNAMENT','ACCOUNT')),
    CONSTRAINT CK_Attachment_Type     CHECK (FileType  IN ('IMAGE','VIDEO','REPLAY','DOCUMENT')),
    CONSTRAINT CK_Attachment_Size     CHECK (SizeBytes IS NULL OR SizeBytes > 0),
    CONSTRAINT FK_Attachment_By       FOREIGN KEY (UploadedByAccountID) REFERENCES dbo.Account(AccountID)
);
GO

CREATE INDEX IX_Attachment_Owner ON dbo.Attachment(OwnerType, OwnerID);
GO

/*===============================================================================
  MODULE H - NỀN TẢNG                 UC-36, UC-38, UC-40, UC-41, UC-43, UC-46
===============================================================================*/

/* Notification - UC-40 thông báo in-app, UC-41 email theo sự kiện và nhắc check-in.
   Nội dung mẫu nằm ở tầng ứng dụng nên KHÔNG cần bảng NotificationTemplate.
   Bật/tắt theo kênh đọc từ Account.NotifyInApp và Account.NotifyEmail. */
CREATE TABLE dbo.Notification
(
    NotificationID   INT IDENTITY(1,1) NOT NULL,
    AccountID        INT               NOT NULL,
    NotificationType VARCHAR(30)       NOT NULL,
    Title            NVARCHAR(200)     NOT NULL,
    Body             NVARCHAR(1000)    NOT NULL,
    LinkUrl          NVARCHAR(500)     NULL,
    Channel          VARCHAR(20)       NOT NULL CONSTRAINT DF_Notification_Ch DEFAULT 'IN_APP',
    IsRead           BIT               NOT NULL CONSTRAINT DF_Notification_Read DEFAULT 0,
    SentAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_Notification_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Notification          PRIMARY KEY (NotificationID),
    CONSTRAINT CK_Notification_Type     CHECK (NotificationType IN ('REGISTRATION','CHECKIN_REMINDER','MATCH_RESULT','DISPUTE','PENALTY','NEWS','SYSTEM')),
    CONSTRAINT CK_Notification_Channel  CHECK (Channel IN ('IN_APP','EMAIL','BOTH')),
    CONSTRAINT FK_Notification_Account  FOREIGN KEY (AccountID) REFERENCES dbo.Account(AccountID)
);
GO

CREATE INDEX IX_Notification_Inbox ON dbo.Notification(AccountID, IsRead, SentAt);
GO

/* TournamentFollow - UC-40. Ai đang theo dõi giải nào, để biết gửi cập nhật cho ai. */
CREATE TABLE dbo.TournamentFollow
(
    TournamentFollowID INT IDENTITY(1,1) NOT NULL,
    AccountID          INT               NOT NULL,
    TournamentID       INT               NOT NULL,
    FollowedAt         DATETIME2(0)      NOT NULL CONSTRAINT DF_TourFollow_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TournamentFollow       PRIMARY KEY (TournamentFollowID),
    CONSTRAINT UQ_TournamentFollow       UNIQUE (AccountID, TournamentID),
    CONSTRAINT FK_TournamentFollow_Acc   FOREIGN KEY (AccountID)    REFERENCES dbo.Account(AccountID),
    CONSTRAINT FK_TournamentFollow_Tour  FOREIGN KEY (TournamentID) REFERENCES dbo.Tournament(TournamentID)
);
GO

/* Livestream - UC-46. Ban tổ chức thêm / sửa / xóa link phát và gán vào từng trận.
   ETOMS CHỈ lưu đường dẫn tới dịch vụ phát bên ngoài, KHÔNG lưu và KHÔNG truyền
   video. MatchID để NULL nghĩa là link của cả giải. */
CREATE TABLE dbo.Livestream
(
    LivestreamID       INT IDENTITY(1,1) NOT NULL,
    TournamentID       INT               NOT NULL,
    MatchID            INT               NULL,
    Platform           VARCHAR(20)       NOT NULL,
    StreamUrl          NVARCHAR(500)     NOT NULL,
    Title              NVARCHAR(200)     NULL,
    Status             VARCHAR(20)       NOT NULL CONSTRAINT DF_Livestream_Status DEFAULT 'SCHEDULED',
    CreatedByAccountID INT               NOT NULL,
    CreatedAt          DATETIME2(0)      NOT NULL CONSTRAINT DF_Livestream_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Livestream           PRIMARY KEY (LivestreamID),
    CONSTRAINT CK_Livestream_Platform  CHECK (Platform IN ('YOUTUBE','FACEBOOK','TWITCH','OTHER')),
    CONSTRAINT CK_Livestream_Status    CHECK (Status   IN ('SCHEDULED','LIVE','ENDED')),
    CONSTRAINT FK_Livestream_Tour      FOREIGN KEY (TournamentID)       REFERENCES dbo.Tournament(TournamentID),
    CONSTRAINT FK_Livestream_Match     FOREIGN KEY (MatchID)            REFERENCES dbo.Match(MatchID),
    CONSTRAINT FK_Livestream_By        FOREIGN KEY (CreatedByAccountID) REFERENCES dbo.Account(AccountID)
);
GO

CREATE INDEX IX_Livestream_Tour ON dbo.Livestream(TournamentID, Status);
GO

/* AuditLog - UC-43 nhật ký thao tác, UC-32 ghi lại phán quyết, UC-13 giữ bản
   thể lệ cũ sau khi sửa.
   Ghi mọi thao tác quyền lực kèm giá trị trước - sau. Là bằng chứng chống thiên
   vị, và vì giữ BeforeValue nên bản thể lệ cũ vẫn tra được dù Tournament chỉ
   lưu bản hiện hành. */
CREATE TABLE dbo.AuditLog
(
    AuditLogID     BIGINT IDENTITY(1,1) NOT NULL,
    ActorAccountID INT                  NULL,   -- NULL = tác vụ nền
    TournamentID   INT                  NULL,
    ActionType     VARCHAR(30)          NOT NULL,
    ObjectType     VARCHAR(30)          NOT NULL,
    ObjectID       INT                  NULL,
    BeforeValue    NVARCHAR(MAX)        NULL,
    AfterValue     NVARCHAR(MAX)        NULL,
    Reason         NVARCHAR(1000)       NULL,
    CreatedAt      DATETIME2(0)         NOT NULL CONSTRAINT DF_AuditLog_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AuditLog         PRIMARY KEY (AuditLogID),
    CONSTRAINT CK_AuditLog_Action  CHECK (ActionType IN ('FORCE_UPDATE','REMATCH','MANUAL_CHECKIN','DISQUALIFY','BAN','UNBAN',
                                                         'APPROVE','REJECT','RULEBOOK_EDIT','CANCEL_TOURNAMENT','GRANT_ORGANIZER')),
    CONSTRAINT FK_AuditLog_Actor   FOREIGN KEY (ActorAccountID) REFERENCES dbo.Account(AccountID),
    CONSTRAINT FK_AuditLog_Tour    FOREIGN KEY (TournamentID)   REFERENCES dbo.Tournament(TournamentID)
);
GO

CREATE INDEX IX_AuditLog_Timeline ON dbo.AuditLog(TournamentID, CreatedAt);
GO

/*===============================================================================
  VIEW - thay cho bảng lưu trùng
===============================================================================*/

/* vw_Leaderboard - UC-19 trao danh hiệu, UC-38 bảng xếp hạng toàn hệ thống.
   Tính trực tiếp từ kết quả đã chốt nên KHÔNG cần bảng LeaderboardEntry.
   Điểm thành tích theo quy mô giải: vô địch 100, á quân 70, hạng ba 50, dự 10;
   nhân hệ số theo số đội của giải, rồi trừ điểm phạt đang hiệu lực. */
CREATE VIEW dbo.vw_Leaderboard
AS
SELECT
    t.GameID,
    r.TeamID,
    YEAR(t.StartAt)                                    AS Season,
    COUNT(*)                                           AS TournamentPlayed,
    SUM(CASE r.FinalPlacement WHEN 'CHAMPION' THEN 1 ELSE 0 END) AS TitleCount,
    SUM(
        CASE r.FinalPlacement
            WHEN 'CHAMPION'    THEN 100
            WHEN 'RUNNER_UP'   THEN  70
            WHEN 'THIRD'       THEN  50
            WHEN 'PARTICIPANT' THEN  10
            ELSE 0
        END
        * CASE WHEN t.MaxTeams >= 32 THEN 2 WHEN t.MaxTeams >= 16 THEN 1 ELSE 1 END
    )                                                  AS AchievementPoint
FROM dbo.Registration r
JOIN dbo.Tournament   t ON t.TournamentID = r.TournamentID
WHERE r.Status = 'APPROVED' AND t.Status = 'COMPLETED'
GROUP BY t.GameID, r.TeamID, YEAR(t.StartAt);
GO

/* vw_TeamPenaltyPoint - UC-35. Tổng điểm phạt đang hiệu lực so với ngưỡng của giải. */
CREATE VIEW dbo.vw_TeamPenaltyPoint
AS
SELECT
    p.TournamentID,
    p.TeamID,
    SUM(p.Points)           AS ActivePoints,
    MAX(t.PenaltyThreshold) AS PenaltyThreshold
FROM dbo.Penalty p
JOIN dbo.Tournament t ON t.TournamentID = p.TournamentID
WHERE p.Status = 'ACTIVE'
GROUP BY p.TournamentID, p.TeamID;
GO

/* vw_GroupStanding - UC-18 bảng xếp hạng vòng bảng.
   Tính từ các trận đã chốt của những nút thuộc giai đoạn vòng bảng. */
CREATE VIEW dbo.vw_GroupStanding
AS
SELECT
    m.TournamentID,
    n.GroupLabel,
    x.TeamID,
    COUNT(*)                                                AS Played,
    SUM(CASE WHEN m.WinnerTeamID = x.TeamID THEN 1 ELSE 0 END) AS Won,
    SUM(CASE WHEN m.WinnerTeamID IS NOT NULL
              AND m.WinnerTeamID <> x.TeamID THEN 1 ELSE 0 END) AS Lost,
    SUM(x.ScoreFor - x.ScoreAgainst)                        AS ScoreDiff,
    SUM(CASE WHEN m.WinnerTeamID = x.TeamID THEN 3 ELSE 0 END) AS Point
FROM dbo.Match m
JOIN dbo.BracketNode n ON n.BracketNodeID = m.BracketNodeID
CROSS APPLY (VALUES
      (m.TeamAID, m.ScoreA, m.ScoreB),
      (m.TeamBID, m.ScoreB, m.ScoreA)) AS x(TeamID, ScoreFor, ScoreAgainst)
WHERE n.StageType = 'GROUP' AND m.Status = 'VERIFIED' AND x.TeamID IS NOT NULL
GROUP BY m.TournamentID, n.GroupLabel, x.TeamID;
GO

/*===============================================================================
  DỮ LIỆU MẪU
===============================================================================*/

INSERT INTO dbo.Account (Email, PasswordHash, FullName, Role, Status, EmailVerifiedAt) VALUES
 (N'admin@etoms.local', '$2a$11$PLACEHOLDER_SEED_ONLY_CHANGE_ME', N'Quản trị hệ thống', 'ADMIN', 'ACTIVE', SYSUTCDATETIME());
GO

INSERT INTO dbo.Game (Title, ShortCode, Status, MinStarters, MaxStarters, MaxSubstitutes, DefaultFormat, AllowedEvidenceTypes) VALUES
 (N'League of Legends', 'LOL', 'ACTIVE', 5, 5, 3, 'SINGLE_ELIMINATION', N'png,jpg,jpeg,mp4,rofl'),
 (N'Valorant',          'VAL', 'ACTIVE', 5, 5, 2, 'SINGLE_ELIMINATION', N'png,jpg,jpeg,mp4'),
 (N'Liên Quân Mobile',  'LQM', 'ACTIVE', 5, 5, 2, 'SINGLE_ELIMINATION', N'png,jpg,jpeg,mp4'),
 (N'PUBG Mobile',       'PUM', 'ACTIVE', 4, 4, 1, 'ROUND_ROBIN',        N'png,jpg,jpeg,mp4'),
 (N'Liên Minh Tốc Chiến','LMTC','ACTIVE', 5, 5, 2, 'SINGLE_ELIMINATION', N'png,jpg,jpeg,mp4'),
 (N'FC Online',         'FCO', 'ACTIVE', 1, 1, 0, 'SINGLE_ELIMINATION', N'png,jpg,jpeg,mp4');
GO

/*===============================================================================
  KIỂM TRA SAU KHI CHẠY
===============================================================================*/

SELECT
    (SELECT COUNT(*) FROM sys.tables)                                  AS SoBang,
    (SELECT COUNT(*) FROM sys.views  WHERE is_ms_shipped = 0)          AS SoView,
    (SELECT COUNT(*) FROM sys.foreign_keys)                            AS SoKhoaNgoai,
    (SELECT COUNT(*) FROM sys.check_constraints)                        AS SoCheckConstraint,
    (SELECT COUNT(*) FROM sys.indexes i JOIN sys.tables tb ON tb.object_id = i.object_id
      WHERE i.is_unique = 1 AND i.is_primary_key = 0)                  AS SoRangBuocDuyNhat,
    (SELECT COUNT(*) FROM sys.indexes WHERE has_filter = 1)            AS SoChiMucCoDieuKien;
GO

/* Mọi bảng PHẢI có khóa chính một cột. Câu này liệt kê bảng vi phạm - phải RỖNG. */
SELECT t.name AS BangViPham, COUNT(*) AS SoCotTrongPK
FROM sys.tables t
JOIN sys.indexes i        ON i.object_id = t.object_id AND i.is_primary_key = 1
JOIN sys.index_columns ic ON ic.object_id = t.object_id AND ic.index_id = i.index_id
GROUP BY t.name
HAVING COUNT(*) > 1;
GO

SELECT 'Account' AS Bang, COUNT(*) AS SoDong FROM dbo.Account
UNION ALL SELECT 'Game', COUNT(*) FROM dbo.Game;
GO

PRINT N'Đã tạo xong database ETOMS (v2, 25 bảng).';
GO

SET NOEXEC OFF;
GO
