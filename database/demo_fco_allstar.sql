-- Dữ liệu DEMO: 8 tuyển thủ FC Online (gamer tag công khai, KHÔNG có thông tin cá nhân) + một giải loại trực tiếp
-- để thử tỉ số trực tiếp (UC-45) và livestream (UC-46). Mật khẩu mọi tài khoản = mật khẩu demo của btc.tuan (Demo@1234).
-- An toàn khi chạy lại: tài khoản/đội/đăng ký đều kiểm tra tồn tại; nhánh đấu + trận chỉ tạo khi giải vừa được tạo mới.
SET NAMES utf8mb4 COLLATE utf8mb4_0900_as_ci;
SET @org := 6;
SET @fco := 7;
SET @pw := (SELECT PasswordHash FROM Account WHERE AccountID = 6);
-- Tuyển thủ 1: LeeHariii
INSERT INTO Account (Email, PasswordHash, FullName, AvatarUrl, Role, Status, EmailVerifiedAt, CreatedAt)
SELECT 'leehariii@demo.etoms', @pw, 'LeeHariii', '/images/demo/players/leehariii.svg', 'PLAYER', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Account WHERE Email = 'leehariii@demo.etoms');
SET @a1 := (SELECT AccountID FROM Account WHERE Email = 'leehariii@demo.etoms');
INSERT INTO PlayerCredential (AccountID, GameID, CredentialType, Value, VerifiedAt, CreatedAt)
SELECT @a1, @fco, 'IN_GAME_NAME', 'LeeHariii', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PlayerCredential WHERE AccountID = @a1 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME');
SET @c1 := (SELECT PlayerCredentialID FROM PlayerCredential WHERE AccountID = @a1 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME' LIMIT 1);
INSERT INTO Team (GameID, CaptainAccountID, Name, Tag, LogoUrl, Status, CreatedAt)
SELECT @fco, @a1, 'LeeHariii', 'LHR', '/images/demo/teams/fco-lhr.svg', 'ACTIVE', UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Team WHERE GameID = @fco AND (Name = 'LeeHariii' OR Tag = 'LHR'));
SET @t1 := (SELECT TeamID FROM Team WHERE GameID = @fco AND Tag = 'LHR');
INSERT INTO TeamMember (TeamID, GameID, AccountID, Slot, Direction, Status, JoinedAt, CreatedAt)
SELECT @t1, @fco, @a1, 'STARTER', 'JOIN_BY_CODE', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM TeamMember WHERE TeamID = @t1 AND AccountID = @a1);
-- Tuyển thủ 2: SangJunior
INSERT INTO Account (Email, PasswordHash, FullName, AvatarUrl, Role, Status, EmailVerifiedAt, CreatedAt)
SELECT 'sangjunior@demo.etoms', @pw, 'SangJunior', '/images/demo/players/sangjunior.svg', 'PLAYER', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Account WHERE Email = 'sangjunior@demo.etoms');
SET @a2 := (SELECT AccountID FROM Account WHERE Email = 'sangjunior@demo.etoms');
INSERT INTO PlayerCredential (AccountID, GameID, CredentialType, Value, VerifiedAt, CreatedAt)
SELECT @a2, @fco, 'IN_GAME_NAME', 'SangJunior', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PlayerCredential WHERE AccountID = @a2 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME');
SET @c2 := (SELECT PlayerCredentialID FROM PlayerCredential WHERE AccountID = @a2 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME' LIMIT 1);
INSERT INTO Team (GameID, CaptainAccountID, Name, Tag, LogoUrl, Status, CreatedAt)
SELECT @fco, @a2, 'SangJunior', 'SGJ', '/images/demo/teams/fco-sgj.svg', 'ACTIVE', UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Team WHERE GameID = @fco AND (Name = 'SangJunior' OR Tag = 'SGJ'));
SET @t2 := (SELECT TeamID FROM Team WHERE GameID = @fco AND Tag = 'SGJ');
INSERT INTO TeamMember (TeamID, GameID, AccountID, Slot, Direction, Status, JoinedAt, CreatedAt)
SELECT @t2, @fco, @a2, 'STARTER', 'JOIN_BY_CODE', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM TeamMember WHERE TeamID = @t2 AND AccountID = @a2);
-- Tuyển thủ 3: Lucero
INSERT INTO Account (Email, PasswordHash, FullName, AvatarUrl, Role, Status, EmailVerifiedAt, CreatedAt)
SELECT 'lucero@demo.etoms', @pw, 'Lucero', '/images/demo/players/lucero.svg', 'PLAYER', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Account WHERE Email = 'lucero@demo.etoms');
SET @a3 := (SELECT AccountID FROM Account WHERE Email = 'lucero@demo.etoms');
INSERT INTO PlayerCredential (AccountID, GameID, CredentialType, Value, VerifiedAt, CreatedAt)
SELECT @a3, @fco, 'IN_GAME_NAME', 'Lucero', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PlayerCredential WHERE AccountID = @a3 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME');
SET @c3 := (SELECT PlayerCredentialID FROM PlayerCredential WHERE AccountID = @a3 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME' LIMIT 1);
INSERT INTO Team (GameID, CaptainAccountID, Name, Tag, LogoUrl, Status, CreatedAt)
SELECT @fco, @a3, 'Lucero', 'LCR', '/images/demo/teams/fco-lcr.svg', 'ACTIVE', UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Team WHERE GameID = @fco AND (Name = 'Lucero' OR Tag = 'LCR'));
SET @t3 := (SELECT TeamID FROM Team WHERE GameID = @fco AND Tag = 'LCR');
INSERT INTO TeamMember (TeamID, GameID, AccountID, Slot, Direction, Status, JoinedAt, CreatedAt)
SELECT @t3, @fco, @a3, 'STARTER', 'JOIN_BY_CODE', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM TeamMember WHERE TeamID = @t3 AND AccountID = @a3);
-- Tuyển thủ 4: Hakumen
INSERT INTO Account (Email, PasswordHash, FullName, AvatarUrl, Role, Status, EmailVerifiedAt, CreatedAt)
SELECT 'hakumen@demo.etoms', @pw, 'Hakumen', '/images/demo/players/hakumen.svg', 'PLAYER', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Account WHERE Email = 'hakumen@demo.etoms');
SET @a4 := (SELECT AccountID FROM Account WHERE Email = 'hakumen@demo.etoms');
INSERT INTO PlayerCredential (AccountID, GameID, CredentialType, Value, VerifiedAt, CreatedAt)
SELECT @a4, @fco, 'IN_GAME_NAME', 'Hakumen', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PlayerCredential WHERE AccountID = @a4 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME');
SET @c4 := (SELECT PlayerCredentialID FROM PlayerCredential WHERE AccountID = @a4 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME' LIMIT 1);
INSERT INTO Team (GameID, CaptainAccountID, Name, Tag, LogoUrl, Status, CreatedAt)
SELECT @fco, @a4, 'Hakumen', 'HKM', '/images/demo/teams/fco-hkm.svg', 'ACTIVE', UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Team WHERE GameID = @fco AND (Name = 'Hakumen' OR Tag = 'HKM'));
SET @t4 := (SELECT TeamID FROM Team WHERE GameID = @fco AND Tag = 'HKM');
INSERT INTO TeamMember (TeamID, GameID, AccountID, Slot, Direction, Status, JoinedAt, CreatedAt)
SELECT @t4, @fco, @a4, 'STARTER', 'JOIN_BY_CODE', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM TeamMember WHERE TeamID = @t4 AND AccountID = @a4);
-- Tuyển thủ 5: Tabi
INSERT INTO Account (Email, PasswordHash, FullName, AvatarUrl, Role, Status, EmailVerifiedAt, CreatedAt)
SELECT 'tabi@demo.etoms', @pw, 'Tabi', '/images/demo/players/tabi.svg', 'PLAYER', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Account WHERE Email = 'tabi@demo.etoms');
SET @a5 := (SELECT AccountID FROM Account WHERE Email = 'tabi@demo.etoms');
INSERT INTO PlayerCredential (AccountID, GameID, CredentialType, Value, VerifiedAt, CreatedAt)
SELECT @a5, @fco, 'IN_GAME_NAME', 'Tabi', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PlayerCredential WHERE AccountID = @a5 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME');
SET @c5 := (SELECT PlayerCredentialID FROM PlayerCredential WHERE AccountID = @a5 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME' LIMIT 1);
INSERT INTO Team (GameID, CaptainAccountID, Name, Tag, LogoUrl, Status, CreatedAt)
SELECT @fco, @a5, 'Tabi', 'TAB', '/images/demo/teams/fco-tab.svg', 'ACTIVE', UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Team WHERE GameID = @fco AND (Name = 'Tabi' OR Tag = 'TAB'));
SET @t5 := (SELECT TeamID FROM Team WHERE GameID = @fco AND Tag = 'TAB');
INSERT INTO TeamMember (TeamID, GameID, AccountID, Slot, Direction, Status, JoinedAt, CreatedAt)
SELECT @t5, @fco, @a5, 'STARTER', 'JOIN_BY_CODE', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM TeamMember WHERE TeamID = @t5 AND AccountID = @a5);
-- Tuyển thủ 6: Maestro
INSERT INTO Account (Email, PasswordHash, FullName, AvatarUrl, Role, Status, EmailVerifiedAt, CreatedAt)
SELECT 'maestro@demo.etoms', @pw, 'Maestro', '/images/demo/players/maestro.svg', 'PLAYER', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Account WHERE Email = 'maestro@demo.etoms');
SET @a6 := (SELECT AccountID FROM Account WHERE Email = 'maestro@demo.etoms');
INSERT INTO PlayerCredential (AccountID, GameID, CredentialType, Value, VerifiedAt, CreatedAt)
SELECT @a6, @fco, 'IN_GAME_NAME', 'Maestro', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PlayerCredential WHERE AccountID = @a6 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME');
SET @c6 := (SELECT PlayerCredentialID FROM PlayerCredential WHERE AccountID = @a6 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME' LIMIT 1);
INSERT INTO Team (GameID, CaptainAccountID, Name, Tag, LogoUrl, Status, CreatedAt)
SELECT @fco, @a6, 'Maestro', 'MAE', '/images/demo/teams/fco-mae.svg', 'ACTIVE', UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Team WHERE GameID = @fco AND (Name = 'Maestro' OR Tag = 'MAE'));
SET @t6 := (SELECT TeamID FROM Team WHERE GameID = @fco AND Tag = 'MAE');
INSERT INTO TeamMember (TeamID, GameID, AccountID, Slot, Direction, Status, JoinedAt, CreatedAt)
SELECT @t6, @fco, @a6, 'STARTER', 'JOIN_BY_CODE', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM TeamMember WHERE TeamID = @t6 AND AccountID = @a6);
-- Tuyển thủ 7: Djokovic
INSERT INTO Account (Email, PasswordHash, FullName, AvatarUrl, Role, Status, EmailVerifiedAt, CreatedAt)
SELECT 'djokovic@demo.etoms', @pw, 'Djokovic', '/images/demo/players/djokovic.svg', 'PLAYER', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Account WHERE Email = 'djokovic@demo.etoms');
SET @a7 := (SELECT AccountID FROM Account WHERE Email = 'djokovic@demo.etoms');
INSERT INTO PlayerCredential (AccountID, GameID, CredentialType, Value, VerifiedAt, CreatedAt)
SELECT @a7, @fco, 'IN_GAME_NAME', 'Djokovic', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PlayerCredential WHERE AccountID = @a7 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME');
SET @c7 := (SELECT PlayerCredentialID FROM PlayerCredential WHERE AccountID = @a7 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME' LIMIT 1);
INSERT INTO Team (GameID, CaptainAccountID, Name, Tag, LogoUrl, Status, CreatedAt)
SELECT @fco, @a7, 'Djokovic', 'DJK', '/images/demo/teams/fco-djk.svg', 'ACTIVE', UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Team WHERE GameID = @fco AND (Name = 'Djokovic' OR Tag = 'DJK'));
SET @t7 := (SELECT TeamID FROM Team WHERE GameID = @fco AND Tag = 'DJK');
INSERT INTO TeamMember (TeamID, GameID, AccountID, Slot, Direction, Status, JoinedAt, CreatedAt)
SELECT @t7, @fco, @a7, 'STARTER', 'JOIN_BY_CODE', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM TeamMember WHERE TeamID = @t7 AND AccountID = @a7);
-- Tuyển thủ 8: Bino7
INSERT INTO Account (Email, PasswordHash, FullName, AvatarUrl, Role, Status, EmailVerifiedAt, CreatedAt)
SELECT 'bino7@demo.etoms', @pw, 'Bino7', '/images/demo/players/bino7.svg', 'PLAYER', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Account WHERE Email = 'bino7@demo.etoms');
SET @a8 := (SELECT AccountID FROM Account WHERE Email = 'bino7@demo.etoms');
INSERT INTO PlayerCredential (AccountID, GameID, CredentialType, Value, VerifiedAt, CreatedAt)
SELECT @a8, @fco, 'IN_GAME_NAME', 'Bino7', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PlayerCredential WHERE AccountID = @a8 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME');
SET @c8 := (SELECT PlayerCredentialID FROM PlayerCredential WHERE AccountID = @a8 AND GameID = @fco AND CredentialType = 'IN_GAME_NAME' LIMIT 1);
INSERT INTO Team (GameID, CaptainAccountID, Name, Tag, LogoUrl, Status, CreatedAt)
SELECT @fco, @a8, 'Bino7', 'BN7', '/images/demo/teams/fco-bn7.svg', 'ACTIVE', UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Team WHERE GameID = @fco AND (Name = 'Bino7' OR Tag = 'BN7'));
SET @t8 := (SELECT TeamID FROM Team WHERE GameID = @fco AND Tag = 'BN7');
INSERT INTO TeamMember (TeamID, GameID, AccountID, Slot, Direction, Status, JoinedAt, CreatedAt)
SELECT @t8, @fco, @a8, 'STARTER', 'JOIN_BY_CODE', 'ACTIVE', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM TeamMember WHERE TeamID = @t8 AND AccountID = @a8);
-- Giải demo
SET @tname := 'Giải FC Online All-Star (demo) 14/11/2026';
SET @new := (SELECT COUNT(*) = 0 FROM Tournament WHERE Name = @tname);
INSERT INTO Tournament (GameID, OrganizerAccountID, Name, Description, OrganizingUnit, IsOnline, Format, HasThirdPlaceMatch, MaxTeams, StarterCount, MaxSubstitutes, RegOpenAt, RegCloseAt, StartAt, EntryFee, RequireIdentityDocument, RequirePaymentProof, RequireInGameName, BestOfPerRound, RulebookVersion, Status, BracketLockedByAccountID, BracketLockedAt, CreatedAt)
SELECT @fco, @org, @tname, 'Dữ liệu DEMO để thử tỉ số trực tiếp và livestream: 8 tuyển thủ FC Online đấu 1v1, tứ kết BO3, lịch mô phỏng nhánh 0:1 ngày 14/11. Tỉ số và kết quả trong giải này do người dùng nhập thử, KHÔNG phải số liệu chính thức.', 'CLB Esports Sinh viên Hòa Lạc', 1, 'SINGLE_ELIMINATION', 0, 8, 1, 0, '2026-11-01 00:00:00', '2026-11-10 00:00:00', '2026-11-14 07:00:00', 0, 0, 0, 1, 'R1:3,R2:3,F:5', 1, 'ONGOING', @org, '2026-11-12 00:00:00', UTC_TIMESTAMP() FROM DUAL
WHERE @new;
SET @tid := (SELECT TournamentID FROM Tournament WHERE Name = @tname);
INSERT INTO Registration (TournamentID, TeamID, SubmittedByAccountID, RulebookVersionAccepted, ConsentAt, Status, PaymentStatus, FinalPlacement, SubmittedAt, ReviewedAt)
SELECT @tid, @t1, @a1, 1, '2026-11-05 00:00:00', 'APPROVED', 'UNPAID', 'NONE', '2026-11-05 00:00:00', '2026-11-11 00:00:00' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Registration WHERE TournamentID = @tid AND TeamID = @t1);
INSERT INTO RegistrationLineup (RegistrationID, AccountID, Slot, InGameNameSnapshot, PlayerCredentialID)
SELECT r.RegistrationID, @a1, 'STARTER', 'LeeHariii', @c1 FROM Registration r
WHERE r.TournamentID = @tid AND r.TeamID = @t1 AND NOT EXISTS (SELECT 1 FROM RegistrationLineup x WHERE x.RegistrationID = r.RegistrationID AND x.AccountID = @a1);
INSERT INTO Registration (TournamentID, TeamID, SubmittedByAccountID, RulebookVersionAccepted, ConsentAt, Status, PaymentStatus, FinalPlacement, SubmittedAt, ReviewedAt)
SELECT @tid, @t2, @a2, 1, '2026-11-05 00:00:00', 'APPROVED', 'UNPAID', 'NONE', '2026-11-05 00:00:00', '2026-11-11 00:00:00' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Registration WHERE TournamentID = @tid AND TeamID = @t2);
INSERT INTO RegistrationLineup (RegistrationID, AccountID, Slot, InGameNameSnapshot, PlayerCredentialID)
SELECT r.RegistrationID, @a2, 'STARTER', 'SangJunior', @c2 FROM Registration r
WHERE r.TournamentID = @tid AND r.TeamID = @t2 AND NOT EXISTS (SELECT 1 FROM RegistrationLineup x WHERE x.RegistrationID = r.RegistrationID AND x.AccountID = @a2);
INSERT INTO Registration (TournamentID, TeamID, SubmittedByAccountID, RulebookVersionAccepted, ConsentAt, Status, PaymentStatus, FinalPlacement, SubmittedAt, ReviewedAt)
SELECT @tid, @t3, @a3, 1, '2026-11-05 00:00:00', 'APPROVED', 'UNPAID', 'NONE', '2026-11-05 00:00:00', '2026-11-11 00:00:00' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Registration WHERE TournamentID = @tid AND TeamID = @t3);
INSERT INTO RegistrationLineup (RegistrationID, AccountID, Slot, InGameNameSnapshot, PlayerCredentialID)
SELECT r.RegistrationID, @a3, 'STARTER', 'Lucero', @c3 FROM Registration r
WHERE r.TournamentID = @tid AND r.TeamID = @t3 AND NOT EXISTS (SELECT 1 FROM RegistrationLineup x WHERE x.RegistrationID = r.RegistrationID AND x.AccountID = @a3);
INSERT INTO Registration (TournamentID, TeamID, SubmittedByAccountID, RulebookVersionAccepted, ConsentAt, Status, PaymentStatus, FinalPlacement, SubmittedAt, ReviewedAt)
SELECT @tid, @t4, @a4, 1, '2026-11-05 00:00:00', 'APPROVED', 'UNPAID', 'NONE', '2026-11-05 00:00:00', '2026-11-11 00:00:00' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Registration WHERE TournamentID = @tid AND TeamID = @t4);
INSERT INTO RegistrationLineup (RegistrationID, AccountID, Slot, InGameNameSnapshot, PlayerCredentialID)
SELECT r.RegistrationID, @a4, 'STARTER', 'Hakumen', @c4 FROM Registration r
WHERE r.TournamentID = @tid AND r.TeamID = @t4 AND NOT EXISTS (SELECT 1 FROM RegistrationLineup x WHERE x.RegistrationID = r.RegistrationID AND x.AccountID = @a4);
INSERT INTO Registration (TournamentID, TeamID, SubmittedByAccountID, RulebookVersionAccepted, ConsentAt, Status, PaymentStatus, FinalPlacement, SubmittedAt, ReviewedAt)
SELECT @tid, @t5, @a5, 1, '2026-11-05 00:00:00', 'APPROVED', 'UNPAID', 'NONE', '2026-11-05 00:00:00', '2026-11-11 00:00:00' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Registration WHERE TournamentID = @tid AND TeamID = @t5);
INSERT INTO RegistrationLineup (RegistrationID, AccountID, Slot, InGameNameSnapshot, PlayerCredentialID)
SELECT r.RegistrationID, @a5, 'STARTER', 'Tabi', @c5 FROM Registration r
WHERE r.TournamentID = @tid AND r.TeamID = @t5 AND NOT EXISTS (SELECT 1 FROM RegistrationLineup x WHERE x.RegistrationID = r.RegistrationID AND x.AccountID = @a5);
INSERT INTO Registration (TournamentID, TeamID, SubmittedByAccountID, RulebookVersionAccepted, ConsentAt, Status, PaymentStatus, FinalPlacement, SubmittedAt, ReviewedAt)
SELECT @tid, @t6, @a6, 1, '2026-11-05 00:00:00', 'APPROVED', 'UNPAID', 'NONE', '2026-11-05 00:00:00', '2026-11-11 00:00:00' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Registration WHERE TournamentID = @tid AND TeamID = @t6);
INSERT INTO RegistrationLineup (RegistrationID, AccountID, Slot, InGameNameSnapshot, PlayerCredentialID)
SELECT r.RegistrationID, @a6, 'STARTER', 'Maestro', @c6 FROM Registration r
WHERE r.TournamentID = @tid AND r.TeamID = @t6 AND NOT EXISTS (SELECT 1 FROM RegistrationLineup x WHERE x.RegistrationID = r.RegistrationID AND x.AccountID = @a6);
INSERT INTO Registration (TournamentID, TeamID, SubmittedByAccountID, RulebookVersionAccepted, ConsentAt, Status, PaymentStatus, FinalPlacement, SubmittedAt, ReviewedAt)
SELECT @tid, @t7, @a7, 1, '2026-11-05 00:00:00', 'APPROVED', 'UNPAID', 'NONE', '2026-11-05 00:00:00', '2026-11-11 00:00:00' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Registration WHERE TournamentID = @tid AND TeamID = @t7);
INSERT INTO RegistrationLineup (RegistrationID, AccountID, Slot, InGameNameSnapshot, PlayerCredentialID)
SELECT r.RegistrationID, @a7, 'STARTER', 'Djokovic', @c7 FROM Registration r
WHERE r.TournamentID = @tid AND r.TeamID = @t7 AND NOT EXISTS (SELECT 1 FROM RegistrationLineup x WHERE x.RegistrationID = r.RegistrationID AND x.AccountID = @a7);
INSERT INTO Registration (TournamentID, TeamID, SubmittedByAccountID, RulebookVersionAccepted, ConsentAt, Status, PaymentStatus, FinalPlacement, SubmittedAt, ReviewedAt)
SELECT @tid, @t8, @a8, 1, '2026-11-05 00:00:00', 'APPROVED', 'UNPAID', 'NONE', '2026-11-05 00:00:00', '2026-11-11 00:00:00' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Registration WHERE TournamentID = @tid AND TeamID = @t8);
INSERT INTO RegistrationLineup (RegistrationID, AccountID, Slot, InGameNameSnapshot, PlayerCredentialID)
SELECT r.RegistrationID, @a8, 'STARTER', 'Bino7', @c8 FROM Registration r
WHERE r.TournamentID = @tid AND r.TeamID = @t8 AND NOT EXISTS (SELECT 1 FROM RegistrationLineup x WHERE x.RegistrationID = r.RegistrationID AND x.AccountID = @a8);
-- Nhánh đấu 8 đội: chung kết -> 2 bán kết -> 4 tứ kết (chỉ tạo khi giải mới tạo)
INSERT INTO BracketNode (TournamentID, ParentNodeID, StageNo, StageType, RoundNo, PositionInRound, NodeType, IsBye, SeedingMethod, Status, GeneratedAt)
SELECT @tid, NULL, 1, 'KNOCKOUT', 3, 1, 'MATCH_SLOT', 0, 'MANUAL', 'LOCKED', '2026-11-12 00:00:00' FROM DUAL WHERE @new;
SET @nF := IF(@new, LAST_INSERT_ID(), NULL);
INSERT INTO BracketNode (TournamentID, ParentNodeID, StageNo, StageType, RoundNo, PositionInRound, NodeType, IsBye, SeedingMethod, Status, GeneratedAt)
SELECT @tid, @nF, 1, 'KNOCKOUT', 2, 1, 'MATCH_SLOT', 0, 'MANUAL', 'LOCKED', '2026-11-12 00:00:00' FROM DUAL WHERE @new;
SET @nS1 := IF(@new, LAST_INSERT_ID(), NULL);
INSERT INTO BracketNode (TournamentID, ParentNodeID, StageNo, StageType, RoundNo, PositionInRound, NodeType, IsBye, SeedingMethod, Status, GeneratedAt)
SELECT @tid, @nF, 1, 'KNOCKOUT', 2, 2, 'MATCH_SLOT', 0, 'MANUAL', 'LOCKED', '2026-11-12 00:00:00' FROM DUAL WHERE @new;
SET @nS2 := IF(@new, LAST_INSERT_ID(), NULL);
INSERT INTO BracketNode (TournamentID, ParentNodeID, StageNo, StageType, RoundNo, PositionInRound, NodeType, IsBye, SeedingMethod, Status, GeneratedAt)
SELECT @tid, @nS1, 1, 'KNOCKOUT', 1, 1, 'MATCH_SLOT', 0, 'MANUAL', 'LOCKED', '2026-11-12 00:00:00' FROM DUAL WHERE @new;
SET @nQ1 := IF(@new, LAST_INSERT_ID(), NULL);
INSERT INTO `Match` (TournamentID, BracketNodeID, TeamAID, TeamBID, BestOf, ScheduledAt, CheckInOpenAt, TeamACheckedInAt, TeamBCheckedInAt, CheckInMode, Status)
SELECT @tid, @nQ1, @t1, @t2, 3, '2026-11-14 07:00:00', '2026-11-14 06:30:00', '2026-11-14 06:30:00', '2026-11-14 06:30:00', 'SELF', 'READY' FROM DUAL WHERE @new;
SET @m1 := IF(@new, LAST_INSERT_ID(), NULL);
INSERT INTO BracketNode (TournamentID, ParentNodeID, StageNo, StageType, RoundNo, PositionInRound, NodeType, IsBye, SeedingMethod, Status, GeneratedAt)
SELECT @tid, @nS1, 1, 'KNOCKOUT', 1, 2, 'MATCH_SLOT', 0, 'MANUAL', 'LOCKED', '2026-11-12 00:00:00' FROM DUAL WHERE @new;
SET @nQ2 := IF(@new, LAST_INSERT_ID(), NULL);
INSERT INTO `Match` (TournamentID, BracketNodeID, TeamAID, TeamBID, BestOf, ScheduledAt, CheckInOpenAt, TeamACheckedInAt, TeamBCheckedInAt, CheckInMode, Status)
SELECT @tid, @nQ2, @t3, @t4, 3, '2026-11-14 07:45:00', '2026-11-14 07:15:00', '2026-11-14 07:15:00', '2026-11-14 07:15:00', 'SELF', 'READY' FROM DUAL WHERE @new;
SET @m2 := IF(@new, LAST_INSERT_ID(), NULL);
INSERT INTO BracketNode (TournamentID, ParentNodeID, StageNo, StageType, RoundNo, PositionInRound, NodeType, IsBye, SeedingMethod, Status, GeneratedAt)
SELECT @tid, @nS2, 1, 'KNOCKOUT', 1, 3, 'MATCH_SLOT', 0, 'MANUAL', 'LOCKED', '2026-11-12 00:00:00' FROM DUAL WHERE @new;
SET @nQ3 := IF(@new, LAST_INSERT_ID(), NULL);
INSERT INTO `Match` (TournamentID, BracketNodeID, TeamAID, TeamBID, BestOf, ScheduledAt, CheckInOpenAt, TeamACheckedInAt, TeamBCheckedInAt, CheckInMode, Status)
SELECT @tid, @nQ3, @t5, @t6, 3, '2026-11-14 08:30:00', '2026-11-14 08:00:00', '2026-11-14 08:00:00', '2026-11-14 08:00:00', 'SELF', 'READY' FROM DUAL WHERE @new;
SET @m3 := IF(@new, LAST_INSERT_ID(), NULL);
INSERT INTO BracketNode (TournamentID, ParentNodeID, StageNo, StageType, RoundNo, PositionInRound, NodeType, IsBye, SeedingMethod, Status, GeneratedAt)
SELECT @tid, @nS2, 1, 'KNOCKOUT', 1, 4, 'MATCH_SLOT', 0, 'MANUAL', 'LOCKED', '2026-11-12 00:00:00' FROM DUAL WHERE @new;
SET @nQ4 := IF(@new, LAST_INSERT_ID(), NULL);
INSERT INTO `Match` (TournamentID, BracketNodeID, TeamAID, TeamBID, BestOf, ScheduledAt, CheckInOpenAt, TeamACheckedInAt, TeamBCheckedInAt, CheckInMode, Status)
SELECT @tid, @nQ4, @t7, @t8, 3, '2026-11-14 09:15:00', '2026-11-14 08:45:00', '2026-11-14 08:45:00', '2026-11-14 08:45:00', 'SELF', 'READY' FROM DUAL WHERE @new;
SET @m4 := IF(@new, LAST_INSERT_ID(), NULL);
INSERT INTO `Match` (TournamentID, BracketNodeID, BestOf, CheckInMode, Status) SELECT @tid, @nS1, 3, 'SELF', 'PENDING' FROM DUAL WHERE @new;
INSERT INTO `Match` (TournamentID, BracketNodeID, BestOf, CheckInMode, Status) SELECT @tid, @nS2, 3, 'SELF', 'PENDING' FROM DUAL WHERE @new;
INSERT INTO `Match` (TournamentID, BracketNodeID, BestOf, CheckInMode, Status) SELECT @tid, @nF, 5, 'SELF', 'PENDING' FROM DUAL WHERE @new;
-- Bản ghi phát sóng của buổi đấu (link YouTube công khai) gán vào tứ kết 1
INSERT INTO Livestream (TournamentID, MatchID, Platform, StreamUrl, Title, Status, CreatedByAccountID, CreatedAt)
SELECT @tid, @m1, 'YOUTUBE', 'https://www.youtube.com/watch?v=OmiamPRwZ1I', 'Bản ghi phát sóng nhánh 0:1 (BO3) ngày 14/11', 'ENDED', @org, UTC_TIMESTAMP() FROM DUAL WHERE @new;
