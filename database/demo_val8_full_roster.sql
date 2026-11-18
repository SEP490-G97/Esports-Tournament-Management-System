-- 18/11/2026 — Giải test tạo từ đầu để test SINH NHÁNH: 8 đội Valorant MỚI, mỗi đội 7 người (5 chính thức + 2 dự bị).
-- CHỈ THÊM MỚI, chạy lại không nhân đôi (kiểm theo email / tên đội / tên giải). Không sửa dữ liệu cũ.
--   56 tài khoản tuyển thủ val8.d<đội>p<số>@demo.etoms (mật khẩu giống tài khoản demo player20), người số 1 là đội trưởng.
--   Giải "Giải test · Valorant 8 đội đủ dự bị": Loại trực tiếp, 8 suất, đã đóng đăng ký, 8 đơn đã duyệt, CHƯA sinh nhánh.
--   Đứng tên CLB Esports Sinh viên Hòa Lạc, quản lý bằng btc.tuan@demo.etoms.
SET NAMES utf8mb4 COLLATE utf8mb4_0900_as_ci;
SET @game := (SELECT GameID FROM Game WHERE Title = 'Valorant'), @now := UTC_TIMESTAMP(),
    @pwd := (SELECT PasswordHash FROM Account WHERE Email = 'player20@demo.etoms');

-- ===== 56 tuyển thủ =====
INSERT INTO Account (Email, PasswordHash, FullName, Role, Status, NotifyInApp, NotifyEmail, OrganizerRequestStatus, EmailVerifiedAt, CreatedAt)
SELECT CONCAT('val8.d', d.n, 'p', p.n, '@demo.etoms'), @pwd,
       CONCAT(ELT(p.n, 'Nguyễn', 'Trần', 'Lê', 'Phạm', 'Hoàng', 'Vũ', 'Đặng'), ' ',
              ELT(d.n, 'Minh', 'Quốc', 'Gia', 'Thành', 'Đức', 'Hữu', 'Tuấn', 'Bảo'), ' ',
              ELT(p.n, 'An', 'Bình', 'Cường', 'Duy', 'Khôi', 'Long', 'Phong')),
       'PLAYER', 'ACTIVE', 1, 0, 'NONE', @now, @now
FROM (SELECT 1 n UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8) d
CROSS JOIN (SELECT 1 n UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7) p
WHERE @pwd IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM Account a WHERE a.Email = CONCAT('val8.d', d.n, 'p', p.n, '@demo.etoms'));

-- ===== 8 đội (đội trưởng = tuyển thủ số 1 của đội) =====
INSERT INTO Team (GameID, CaptainAccountID, Name, Tag, Description, Status, CreatedAt)
SELECT @game, a.AccountID, t.Name, t.Tag, 'Đội test sinh nhánh — 5 chính thức + 2 dự bị.', 'ACTIVE', @now
FROM (SELECT 1 n, 'Phượng Hoàng Lửa' Name, 'PHL' Tag UNION ALL SELECT 2, 'Thần Long Esports', 'TLE' UNION ALL SELECT 3, 'Hắc Kỳ Lân', 'HKL'
      UNION ALL SELECT 4, 'Bão Tố Gaming', 'BTO' UNION ALL SELECT 5, 'Sấm Sét Esports', 'SSE' UNION ALL SELECT 6, 'Băng Giá Gaming', 'BGG'
      UNION ALL SELECT 7, 'Thiên Ưng Esports', 'TUE' UNION ALL SELECT 8, 'Hổ Trắng Gaming', 'HTG') t
JOIN Account a ON a.Email = CONCAT('val8.d', t.n, 'p1@demo.etoms')
WHERE NOT EXISTS (SELECT 1 FROM Team x WHERE x.GameID = @game AND (x.Name = t.Name OR x.Tag = t.Tag));

-- ===== Thành viên: số 1–5 chính thức, 6–7 dự bị =====
INSERT INTO TeamMember (TeamID, GameID, AccountID, Slot, Direction, Status, JoinedAt, CreatedAt)
SELECT tm.TeamID, @game, a.AccountID, IF(p.n <= 5, 'STARTER', 'SUBSTITUTE'), 'JOIN_BY_CODE', 'ACTIVE', @now, @now
FROM (SELECT 1 n, 'Phượng Hoàng Lửa' Name UNION ALL SELECT 2, 'Thần Long Esports' UNION ALL SELECT 3, 'Hắc Kỳ Lân' UNION ALL SELECT 4, 'Bão Tố Gaming'
      UNION ALL SELECT 5, 'Sấm Sét Esports' UNION ALL SELECT 6, 'Băng Giá Gaming' UNION ALL SELECT 7, 'Thiên Ưng Esports' UNION ALL SELECT 8, 'Hổ Trắng Gaming') t
JOIN Team tm ON tm.GameID = @game AND tm.Name = t.Name
CROSS JOIN (SELECT 1 n UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7) p
JOIN Account a ON a.Email = CONCAT('val8.d', t.n, 'p', p.n, '@demo.etoms')
WHERE NOT EXISTS (SELECT 1 FROM TeamMember m WHERE m.AccountID = a.AccountID AND m.GameID = @game AND m.Status = 'ACTIVE');

-- ===== Giải: đã đóng đăng ký, khai mạc ngày mai, chưa sinh nhánh =====
INSERT INTO Tournament (GameID, OrganizerAccountID, OrganizationID, Name, Description, OrganizingUnit, IsOnline, Format, HasThirdPlaceMatch,
    MaxTeams, StarterCount, MaxSubstitutes, RegOpenAt, RegCloseAt, StartAt, EndAt, EntryFee,
    RequireIdentityDocument, RequirePaymentProof, RequireInGameName, BestOfPerRound, RulebookContent, RulebookVersion, Status, CreatedAt)
SELECT @game, 6, 2, 'Giải test · Valorant 8 đội đủ dự bị', 'Giải test sinh nhánh: 8 đội, mỗi đội 5 chính thức + 2 dự bị.',
       'CLB Esports Sinh viên Hòa Lạc', 1, 'SINGLE_ELIMINATION', 1,
       8, 5, 2, @now - INTERVAL 3 DAY, @now - INTERVAL 2 HOUR, @now + INTERVAL 1 DAY, @now + INTERVAL 7 DAY, 0,
       0, 0, 0, 'R1:1,R2:3,F:5', '<p>Thể lệ giải test.</p>', 1, 'PUBLISHED', @now
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Tournament WHERE Name = 'Giải test · Valorant 8 đội đủ dự bị');
SET @t := (SELECT TournamentID FROM Tournament WHERE Name = 'Giải test · Valorant 8 đội đủ dự bị');

-- ===== 8 đơn đã duyệt (thứ tự duyệt = thứ tự hạt giống) + đội hình 7 người =====
INSERT INTO Registration (TournamentID, TeamID, SubmittedByAccountID, RulebookVersionAccepted, ConsentAt, Status, PaymentStatus, FinalPlacement, SubmittedAt, ReviewedAt)
SELECT @t, tm.TeamID, tm.CaptainAccountID, 1, @now - INTERVAL 2 DAY, 'APPROVED', 'UNPAID', 'NONE', @now - INTERVAL 2 DAY, @now - INTERVAL 1 DAY + INTERVAL t.n MINUTE
FROM (SELECT 1 n, 'Phượng Hoàng Lửa' Name UNION ALL SELECT 2, 'Thần Long Esports' UNION ALL SELECT 3, 'Hắc Kỳ Lân' UNION ALL SELECT 4, 'Bão Tố Gaming'
      UNION ALL SELECT 5, 'Sấm Sét Esports' UNION ALL SELECT 6, 'Băng Giá Gaming' UNION ALL SELECT 7, 'Thiên Ưng Esports' UNION ALL SELECT 8, 'Hổ Trắng Gaming') t
JOIN Team tm ON tm.GameID = @game AND tm.Name = t.Name
WHERE @t IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Registration r WHERE r.TournamentID = @t AND r.TeamID = tm.TeamID);

INSERT INTO RegistrationLineup (RegistrationID, AccountID, Slot)
SELECT r.RegistrationID, m.AccountID, m.Slot
FROM Registration r
JOIN TeamMember m ON m.TeamID = r.TeamID AND m.Status = 'ACTIVE'
WHERE r.TournamentID = @t
  AND NOT EXISTS (SELECT 1 FROM RegistrationLineup l WHERE l.RegistrationID = r.RegistrationID AND l.AccountID = m.AccountID);

-- ===== Kiểm tra =====
SELECT t.TournamentID, t.Name, t.Status, COUNT(DISTINCT r.RegistrationID) AS DonDaDuyet, COUNT(l.RegistrationLineupID) AS NguoiTrongDoiHinh,
       SUM(l.Slot = 'STARTER') AS ChinhThuc, SUM(l.Slot = 'SUBSTITUTE') AS DuBi
FROM Tournament t
JOIN Registration r ON r.TournamentID = t.TournamentID AND r.Status = 'APPROVED'
LEFT JOIN RegistrationLineup l ON l.RegistrationID = r.RegistrationID
WHERE t.TournamentID = @t
GROUP BY t.TournamentID, t.Name, t.Status;
