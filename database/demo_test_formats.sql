-- 17/11/2026 — Dữ liệu để cả nhóm test đủ 4 thể thức + luồng duyệt đơn (yêu cầu của Hùng/Khánh trong nhóm).
-- CHỈ THÊM MỚI: 5 giải tên bắt đầu "Giải test ·", đứng tên CLB Esports Sinh viên Hòa Lạc (OrganizationID 2,
-- chủ nhiệm btc.tuan@demo.etoms, AccountID 6). Không sửa/xóa dữ liệu cũ. Chạy lại không nhân đôi (kiểm theo tên giải).
--   1) FC Online · Loại trực tiếp — 8 đội đã duyệt, đã đóng đăng ký  -> vào "Chia nhánh" sinh + khóa nhánh
--   2) FC Online · Loại kép        — 7 đội đã duyệt (lẻ, có miễn đấu vòng 1)
--   3) LMHT · Vòng tròn             — 6 đội đã duyệt
--   4) FC Online · Vòng bảng rồi loại trực tiếp — 8 đội, 2 bảng, mỗi bảng 2 đội đi tiếp
--   5) LMHT · Đang mở đăng ký      — 4 đơn CHỜ DUYỆT để test Duyệt / Từ chối đơn
-- Giải miễn phí, không bắt buộc IGN / giấy tờ / biên lai để test nhanh. Mốc giờ tính theo lúc chạy (UTC).
SET NAMES utf8mb4 COLLATE utf8mb4_0900_as_ci;
SET @org := 2, @btc := 6, @unit := 'CLB Esports Sinh viên Hòa Lạc', @now := UTC_TIMESTAMP();

-- ===== Giải =====
INSERT INTO Tournament (GameID, OrganizerAccountID, OrganizationID, Name, Description, OrganizingUnit, IsOnline, Format, HasThirdPlaceMatch,
    GroupCount, AdvancePerGroup, MaxTeams, StarterCount, MaxSubstitutes, RegOpenAt, RegCloseAt, StartAt, EndAt, EntryFee,
    RequireIdentityDocument, RequirePaymentProof, RequireInGameName, BestOfPerRound, RulebookContent, RulebookVersion, Status, CreatedAt)
SELECT * FROM (
    -- Dòng đầu đặt tên cột (bảng tạm không được trùng tên cột).
    SELECT 7 AS c1, @btc AS c2, @org AS c3, 'Giải test · FC Online Loại trực tiếp' AS c4, 'Giải dùng để test sinh nhánh loại trực tiếp 8 đội.' AS c5,
        @unit AS c6, 1 AS c7, 'SINGLE_ELIMINATION' AS c8, 1 AS c9,
        NULL AS c10, NULL AS c11, 8 AS c12, 3 AS c13, 1 AS c14, @now - INTERVAL 3 DAY AS c15, @now - INTERVAL 2 HOUR AS c16,
        @now + INTERVAL 1 DAY AS c17, @now + INTERVAL 7 DAY AS c18, 0 AS c19,
        0 AS c20, 0 AS c21, 0 AS c22, 'R1:1,R2:3,F:5' AS c23, '<p>Thể lệ giải test.</p>' AS c24, 1 AS c25, 'PUBLISHED' AS c26, @now AS c27
    UNION ALL SELECT 7, @btc, @org, 'Giải test · FC Online Loại kép', 'Giải dùng để test thể thức loại kép (7 đội, có miễn đấu).', @unit, 1, 'DOUBLE_ELIMINATION', 0,
        NULL, NULL, 8, 3, 1, @now - INTERVAL 3 DAY, @now - INTERVAL 2 HOUR, @now + INTERVAL 1 DAY, @now + INTERVAL 7 DAY, 0,
        0, 0, 0, 'R1:1,R2:3,F:5', '<p>Thể lệ giải test.</p>', 1, 'PUBLISHED', @now
    UNION ALL SELECT 2, @btc, @org, 'Giải test · LMHT Vòng tròn', 'Giải dùng để test vòng tròn tính điểm 6 đội.', @unit, 1, 'ROUND_ROBIN', 0,
        1, NULL, 8, 5, 3, @now - INTERVAL 3 DAY, @now - INTERVAL 2 HOUR, @now + INTERVAL 1 DAY, @now + INTERVAL 7 DAY, 0,
        0, 0, 0, 'R1:1,F:1', '<p>Thể lệ giải test.</p>', 1, 'PUBLISHED', @now
    UNION ALL SELECT 7, @btc, @org, 'Giải test · FC Online Vòng bảng rồi loại trực tiếp', 'Giải dùng để test vòng bảng (2 bảng) rồi loại trực tiếp.', @unit, 1, 'HYBRID', 1,
        2, 2, 8, 3, 1, @now - INTERVAL 3 DAY, @now - INTERVAL 2 HOUR, @now + INTERVAL 1 DAY, @now + INTERVAL 7 DAY, 0,
        0, 0, 0, 'R1:1,F:3', '<p>Thể lệ giải test.</p>', 1, 'PUBLISHED', @now
    UNION ALL SELECT 2, @btc, @org, 'Giải test · LMHT Đang mở đăng ký', 'Giải dùng để test duyệt / từ chối đơn đăng ký.', @unit, 1, 'SINGLE_ELIMINATION', 0,
        NULL, NULL, 8, 5, 3, @now - INTERVAL 1 DAY, @now + INTERVAL 3 DAY, @now + INTERVAL 5 DAY, @now + INTERVAL 10 DAY, 0,
        0, 0, 0, 'R1:1,R2:3,F:5', '<p>Thể lệ giải test.</p>', 1, 'PUBLISHED', @now
) AS x
WHERE NOT EXISTS (SELECT 1 FROM Tournament WHERE Name LIKE 'Giải test ·%');

SET @t1 := (SELECT TournamentID FROM Tournament WHERE Name = 'Giải test · FC Online Loại trực tiếp');
SET @t2 := (SELECT TournamentID FROM Tournament WHERE Name = 'Giải test · FC Online Loại kép');
SET @t3 := (SELECT TournamentID FROM Tournament WHERE Name = 'Giải test · LMHT Vòng tròn');
SET @t4 := (SELECT TournamentID FROM Tournament WHERE Name = 'Giải test · FC Online Vòng bảng rồi loại trực tiếp');
SET @t5 := (SELECT TournamentID FROM Tournament WHERE Name = 'Giải test · LMHT Đang mở đăng ký');

-- ===== Đơn đăng ký (đội đủ người theo luật game, đội trưởng là người nộp) =====
-- Thứ tự ReviewedAt = thứ tự hạt giống "Theo thứ tự duyệt đơn".
INSERT INTO Registration (TournamentID, TeamID, SubmittedByAccountID, RulebookVersionAccepted, ConsentAt, Status, PaymentStatus, FinalPlacement, SubmittedAt, ReviewedAt)
SELECT p.TournamentID, p.TeamID, t.CaptainAccountID, 1, @now - INTERVAL 2 DAY, p.Status, 'UNPAID', 'NONE',
       @now - INTERVAL 2 DAY, CASE WHEN p.Status = 'APPROVED' THEN @now - INTERVAL 1 DAY + INTERVAL p.Seq MINUTE END
FROM (
              SELECT @t1 AS TournamentID, 31 AS TeamID, 1 AS Seq, 'APPROVED' AS Status
    UNION ALL SELECT @t1, 32, 2, 'APPROVED' UNION ALL SELECT @t1, 33, 3, 'APPROVED' UNION ALL SELECT @t1, 34, 4, 'APPROVED'
    UNION ALL SELECT @t1, 53, 5, 'APPROVED' UNION ALL SELECT @t1, 54, 6, 'APPROVED' UNION ALL SELECT @t1, 55, 7, 'APPROVED' UNION ALL SELECT @t1, 56, 8, 'APPROVED'
    UNION ALL SELECT @t2, 31, 1, 'APPROVED' UNION ALL SELECT @t2, 32, 2, 'APPROVED' UNION ALL SELECT @t2, 33, 3, 'APPROVED' UNION ALL SELECT @t2, 34, 4, 'APPROVED'
    UNION ALL SELECT @t2, 57, 5, 'APPROVED' UNION ALL SELECT @t2, 58, 6, 'APPROVED' UNION ALL SELECT @t2, 59, 7, 'APPROVED'
    UNION ALL SELECT @t3, 3, 1, 'APPROVED' UNION ALL SELECT @t3, 4, 2, 'APPROVED' UNION ALL SELECT @t3, 5, 3, 'APPROVED'
    UNION ALL SELECT @t3, 6, 4, 'APPROVED' UNION ALL SELECT @t3, 11, 5, 'APPROVED' UNION ALL SELECT @t3, 12, 6, 'APPROVED'
    UNION ALL SELECT @t4, 53, 1, 'APPROVED' UNION ALL SELECT @t4, 54, 2, 'APPROVED' UNION ALL SELECT @t4, 55, 3, 'APPROVED' UNION ALL SELECT @t4, 56, 4, 'APPROVED'
    UNION ALL SELECT @t4, 57, 5, 'APPROVED' UNION ALL SELECT @t4, 58, 6, 'APPROVED' UNION ALL SELECT @t4, 59, 7, 'APPROVED' UNION ALL SELECT @t4, 60, 8, 'APPROVED'
    UNION ALL SELECT @t5, 11, 1, 'PENDING' UNION ALL SELECT @t5, 12, 2, 'PENDING' UNION ALL SELECT @t5, 13, 3, 'PENDING' UNION ALL SELECT @t5, 14, 4, 'PENDING'
) AS p
JOIN Team t ON t.TeamID = p.TeamID
WHERE p.TournamentID IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM Registration r WHERE r.TournamentID = p.TournamentID AND r.TeamID = p.TeamID);

-- ===== Đội hình của từng đơn = thành viên đang hoạt động của đội (đúng slot chính thức / dự bị) =====
INSERT INTO RegistrationLineup (RegistrationID, AccountID, Slot)
SELECT r.RegistrationID, m.AccountID, m.Slot
FROM Registration r
JOIN TeamMember m ON m.TeamID = r.TeamID AND m.Status = 'ACTIVE'
WHERE r.TournamentID IN (@t1, @t2, @t3, @t4, @t5)
  AND NOT EXISTS (SELECT 1 FROM RegistrationLineup l WHERE l.RegistrationID = r.RegistrationID AND l.AccountID = m.AccountID);

-- Kiểm tra nhanh sau khi chạy
SELECT t.TournamentID, t.Name, t.Format, COUNT(r.RegistrationID) AS SoDon, SUM(r.Status = 'APPROVED') AS DaDuyet
FROM Tournament t LEFT JOIN Registration r ON r.TournamentID = t.TournamentID
WHERE t.Name LIKE 'Giải test ·%' GROUP BY t.TournamentID, t.Name, t.Format;
