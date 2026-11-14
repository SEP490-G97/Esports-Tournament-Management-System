-- GRL-01, GRL-02, GRL-03, GRL-04, GRL-06, GRL-07: kho bộ luật/bản đồ/vị trí theo tựa game.
-- TẠM THỜI: module GRL KHÔNG nằm trong 46 use case chính thức chốt ngày 27/10/2026 (xem CLAUDE.md
-- mục 9 — "đừng code theo chúng cho tới khi nhóm quyết định đưa lại vào phạm vi"). Script này được
-- thêm theo yêu cầu cụ thể để tính năng (đã có sẵn code C#, chỉ thiếu bảng) chạy được ngay, chờ
-- nhóm/GVHD chính thức cập nhật UC và catalogue 144 chức năng cho khớp. Nếu quyết định KHÔNG đưa
-- GRL lại phạm vi, xoá 5 bảng này, Repositories/EtomsDbContext.PendingTables.cs và Models/GameRuleset.cs,
-- GameMap.cs, GamePosition.cs, TournamentRuleset.cs, MatchPickBan.cs, RegistrationLineup.Position.cs.
--
-- Cấu trúc khớp đúng theo cấu hình EF đã viết tay ở Repositories/EtomsDbContext.PendingTables.cs —
-- đổi cột ở đâu thì sửa cả hai nơi. Yêu cầu MySQL 8.0.16+ (CHECK được cưỡng chế).
-- Áp dụng: mysql -h <server> -u <user> -p --default-character-set=utf8mb4 <tên_db> < grl-ruleset-map-position.sql

SET NAMES utf8mb4;

-- GRL-01: bộ luật của một tựa game (roster, định dạng mặc định, minh chứng chấp nhận).
CREATE TABLE IF NOT EXISTS GameRuleset
(
    GameRulesetID         INT NOT NULL AUTO_INCREMENT,
    GameID                INT               NOT NULL,
    Name                  VARCHAR(100)     NOT NULL,
    MinStarters           INT               NOT NULL,
    MaxStarters           INT               NOT NULL,
    MaxSubstitutes        INT               NOT NULL,
    DefaultFormat         VARCHAR(30)      NOT NULL,
    AllowedEvidenceTypes  VARCHAR(200)     NOT NULL,
    MaxEvidenceFileSizeMB INT               NOT NULL DEFAULT 10,
    Status                VARCHAR(20)      NOT NULL DEFAULT 'ACTIVE',
    CreatedAt             DATETIME         NOT NULL DEFAULT (UTC_TIMESTAMP()),
    CONSTRAINT PK_GameRuleset        PRIMARY KEY (GameRulesetID),
    CONSTRAINT UQ_GameRuleset_Name   UNIQUE (GameID, Name),
    CONSTRAINT CK_GameRuleset_Status CHECK (Status IN ('ACTIVE', 'RETIRED')),
    CONSTRAINT CK_GameRuleset_Roster CHECK (MinStarters >= 1 AND MaxStarters >= MinStarters AND MaxSubstitutes >= 0),
    CONSTRAINT CK_GameRuleset_Format CHECK (DefaultFormat IN ('SINGLE_ELIMINATION', 'DOUBLE_ELIMINATION', 'ROUND_ROBIN', 'HYBRID')),
    CONSTRAINT FK_GameRuleset_Game   FOREIGN KEY (GameID) REFERENCES Game (GameID)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_0900_as_ci;

-- GRL-02: bản đồ thuộc một bộ luật.
CREATE TABLE IF NOT EXISTS GameMap
(
    GameMapID     INT NOT NULL AUTO_INCREMENT,
    GameRulesetID INT               NOT NULL,
    Name          VARCHAR(100)     NOT NULL,
    ImageUrl      VARCHAR(500)     NULL,
    Status        VARCHAR(20)      NOT NULL DEFAULT 'ACTIVE',
    CreatedAt     DATETIME         NOT NULL DEFAULT (UTC_TIMESTAMP()),
    CONSTRAINT PK_GameMap        PRIMARY KEY (GameMapID),
    CONSTRAINT UQ_GameMap_Name   UNIQUE (GameRulesetID, Name),
    CONSTRAINT CK_GameMap_Status CHECK (Status IN ('ACTIVE', 'RETIRED')),
    CONSTRAINT FK_GameMap_Ruleset FOREIGN KEY (GameRulesetID) REFERENCES GameRuleset (GameRulesetID)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_0900_as_ci;

-- GRL-03: vị trí trong đội hình thuộc một bộ luật (vd Đường trên, Đi rừng...).
CREATE TABLE IF NOT EXISTS GamePosition
(
    GamePositionID INT NOT NULL AUTO_INCREMENT,
    GameRulesetID  INT               NOT NULL,
    Name           VARCHAR(50)      NOT NULL,
    SortOrder      INT               NOT NULL DEFAULT 0,
    Status         VARCHAR(20)      NOT NULL DEFAULT 'ACTIVE',
    CreatedAt      DATETIME         NOT NULL DEFAULT (UTC_TIMESTAMP()),
    CONSTRAINT PK_GamePosition        PRIMARY KEY (GamePositionID),
    CONSTRAINT UQ_GamePosition_Name   UNIQUE (GameRulesetID, Name),
    CONSTRAINT CK_GamePosition_Status CHECK (Status IN ('ACTIVE', 'RETIRED')),
    CONSTRAINT FK_GamePosition_Ruleset FOREIGN KEY (GameRulesetID) REFERENCES GameRuleset (GameRulesetID)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_0900_as_ci;

-- GRL-06: bộ luật mà một giải đã chọn (tách bảng riêng để không thêm cột vào Tournament). 1 giải tối đa 1 bộ luật.
CREATE TABLE IF NOT EXISTS TournamentRuleset
(
    TournamentRulesetID INT NOT NULL AUTO_INCREMENT,
    TournamentID        INT NOT NULL,
    GameRulesetID       INT NOT NULL,
    CreatedAt           DATETIME NOT NULL DEFAULT (UTC_TIMESTAMP()),
    CONSTRAINT PK_TournamentRuleset           PRIMARY KEY (TournamentRulesetID),
    CONSTRAINT UQ_TournamentRuleset_Tournament UNIQUE (TournamentID),
    CONSTRAINT FK_TournamentRuleset_Tournament FOREIGN KEY (TournamentID) REFERENCES Tournament (TournamentID),
    CONSTRAINT FK_TournamentRuleset_Ruleset    FOREIGN KEY (GameRulesetID) REFERENCES GameRuleset (GameRulesetID)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_0900_as_ci;

-- GRL-07: từng lượt cấm/chọn bản đồ của một trận, theo thứ tự StepNo.
CREATE TABLE IF NOT EXISTS MatchPickBan
(
    MatchPickBanID      INT NOT NULL AUTO_INCREMENT,
    MatchID              INT NOT NULL,
    StepNo               INT NOT NULL,
    Action                VARCHAR(10) NOT NULL,
    TeamID                INT NULL,
    GameMapID             INT NOT NULL,
    RecordedByAccountID   INT NOT NULL,
    RecordedAt            DATETIME NOT NULL DEFAULT (UTC_TIMESTAMP()),
    CONSTRAINT PK_MatchPickBan        PRIMARY KEY (MatchPickBanID),
    CONSTRAINT UQ_MatchPickBan_Step   UNIQUE (MatchID, StepNo),
    CONSTRAINT CK_MatchPickBan_Action CHECK (Action IN ('BAN', 'PICK', 'DECIDER')),
    CONSTRAINT FK_MatchPickBan_Match  FOREIGN KEY (MatchID) REFERENCES `Match` (MatchID),
    CONSTRAINT FK_MatchPickBan_Team   FOREIGN KEY (TeamID) REFERENCES Team (TeamID),
    CONSTRAINT FK_MatchPickBan_Map    FOREIGN KEY (GameMapID) REFERENCES GameMap (GameMapID),
    CONSTRAINT FK_MatchPickBan_Account FOREIGN KEY (RecordedByAccountID) REFERENCES Account (AccountID)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_0900_as_ci;

-- GRL-04: vị trí trong đội hình đăng ký, lấy theo bộ luật giải đã chọn (GRL-06).
-- PendingSchema.Detect() ở Program.cs tự dò cột này lúc khởi động; có cột là EF ánh xạ ngay,
-- không cần sửa code.
ALTER TABLE RegistrationLineup
    ADD COLUMN GamePositionID INT NULL AFTER PlayerCredentialID,
    ADD CONSTRAINT FK_RegistrationLineup_Position FOREIGN KEY (GamePositionID) REFERENCES GamePosition (GamePositionID);
