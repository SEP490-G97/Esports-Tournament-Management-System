-- 18/11/2026 — Dữ liệu luật theo tựa game (GRL) cho ĐỦ 6 game + gắn bộ luật cho các giải test (góp ý nhóm mục 11).
-- Trước đây chỉ Valorant (2 bản đồ, 4 vị trí) và Liên Quân (5 vị trí) có bộ luật; LMHT, PUBG Mobile, Tốc Chiến, FC Online trống.
-- CHỈ THÊM / ĐỔI TÊN, chạy lại được (kiểm theo tên). Không xóa dòng nào.
--   * Đổi tên vị trí sang tiếng Việt (giữ nguyên mã => đội hình đã xếp không bị ảnh hưởng):
--       Valorant: Duelist/Controller/Sentinel/Ini -> Đối Đầu/Kiểm Soát/Hộ Vệ/Khởi Tranh; thêm "Linh hoạt" (5 người chính thức
--       mà chỉ 4 vị trí thì không xếp được vì 2 người chính thức không được trùng vị trí).
--       Liên Quân: Rừng/Mid/Ad/Support -> Đi rừng/Đường giữa/Xạ thủ/Trợ thủ.
--   * Bản đồ = tên bản đồ trong game (danh từ riêng). FC Online là bóng đá: không có bản đồ, không có vị trí theo đội hình.
SET NAMES utf8mb4 COLLATE utf8mb4_0900_as_ci;
SET @val := (SELECT GameID FROM Game WHERE ShortCode = 'VAL'), @lqm := (SELECT GameID FROM Game WHERE ShortCode = 'LQM'),
    @lol := (SELECT GameID FROM Game WHERE ShortCode = 'LOL'), @pum := (SELECT GameID FROM Game WHERE ShortCode = 'PUM'),
    @lmtc := (SELECT GameID FROM Game WHERE ShortCode = 'LMTC'), @fco := (SELECT GameID FROM Game WHERE ShortCode = 'FCO');

-- ===== Bộ luật còn thiếu =====
INSERT INTO GameRuleset (GameID, Name, MinStarters, MaxStarters, MaxSubstitutes, DefaultFormat, AllowedEvidenceTypes, MaxEvidenceFileSizeMB, Status)
SELECT x.g, x.n, x.mi, x.ma, x.su, x.f, 'png,jpg,jpeg,mp4', 10, 'ACTIVE' FROM (
              SELECT @lol AS g, '5v5 Chuẩn' AS n, 5 AS mi, 5 AS ma, 3 AS su, 'SINGLE_ELIMINATION' AS f
    UNION ALL SELECT @pum, 'Đội 4 người', 4, 4, 1, 'ROUND_ROBIN'
    UNION ALL SELECT @lmtc, '5v5 Chuẩn', 5, 5, 2, 'SINGLE_ELIMINATION'
    UNION ALL SELECT @fco, 'Đồng đội 3 người', 3, 3, 1, 'SINGLE_ELIMINATION'
) x
WHERE x.g IS NOT NULL AND NOT EXISTS (SELECT 1 FROM GameRuleset r WHERE r.GameID = x.g AND r.Name = x.n);

SET @rVal := (SELECT GameRulesetID FROM GameRuleset WHERE GameID = @val AND Name = '5v5 Chuẩn'),
    @rLqm := (SELECT GameRulesetID FROM GameRuleset WHERE GameID = @lqm AND Name = '5vs5'),
    @rLol := (SELECT GameRulesetID FROM GameRuleset WHERE GameID = @lol AND Name = '5v5 Chuẩn'),
    @rPum := (SELECT GameRulesetID FROM GameRuleset WHERE GameID = @pum AND Name = 'Đội 4 người'),
    @rLmtc := (SELECT GameRulesetID FROM GameRuleset WHERE GameID = @lmtc AND Name = '5v5 Chuẩn'),
    @rFco := (SELECT GameRulesetID FROM GameRuleset WHERE GameID = @fco AND Name = 'Đồng đội 3 người');

-- ===== Đổi tên vị trí cũ sang tiếng Việt (giữ mã) =====
UPDATE GamePosition SET Name = CASE Name WHEN 'Duelist' THEN 'Đối Đầu' WHEN 'Controller' THEN 'Kiểm Soát' WHEN 'Sentinel' THEN 'Hộ Vệ' WHEN 'Ini' THEN 'Khởi Tranh' END
WHERE GameRulesetID = @rVal AND Name IN ('Duelist', 'Controller', 'Sentinel', 'Ini');
UPDATE GamePosition SET Name = CASE Name WHEN 'Rừng' THEN 'Đi rừng' WHEN 'Mid' THEN 'Đường giữa' WHEN 'Ad' THEN 'Xạ thủ' WHEN 'Support' THEN 'Trợ thủ' END
WHERE GameRulesetID = @rLqm AND Name IN ('Rừng', 'Mid', 'Ad', 'Support');

-- ===== Vị trí =====
INSERT INTO GamePosition (GameRulesetID, Name, SortOrder, Status)
SELECT x.r, x.n, x.s, 'ACTIVE' FROM (
              SELECT @rVal AS r, 'Linh hoạt' AS n, 5 AS s
    UNION ALL SELECT @rLol, 'Đường trên', 1 UNION ALL SELECT @rLol, 'Đi rừng', 2 UNION ALL SELECT @rLol, 'Đường giữa', 3
    UNION ALL SELECT @rLol, 'Xạ thủ', 4 UNION ALL SELECT @rLol, 'Hỗ trợ', 5
    UNION ALL SELECT @rLmtc, 'Đường Baron', 1 UNION ALL SELECT @rLmtc, 'Đi rừng', 2 UNION ALL SELECT @rLmtc, 'Đường giữa', 3
    UNION ALL SELECT @rLmtc, 'Đường Rồng', 4 UNION ALL SELECT @rLmtc, 'Hỗ trợ', 5
    UNION ALL SELECT @rPum, 'Chỉ huy', 1 UNION ALL SELECT @rPum, 'Xung phong', 2 UNION ALL SELECT @rPum, 'Bắn tỉa', 3 UNION ALL SELECT @rPum, 'Hỗ trợ', 4
) x
WHERE x.r IS NOT NULL AND NOT EXISTS (SELECT 1 FROM GamePosition p WHERE p.GameRulesetID = x.r AND p.Name = x.n);

-- ===== Bản đồ =====
INSERT INTO GameMap (GameRulesetID, Name, Status)
SELECT x.r, x.n, 'ACTIVE' FROM (
              SELECT @rVal AS r, 'Haven' AS n UNION ALL SELECT @rVal, 'Split' UNION ALL SELECT @rVal, 'Lotus' UNION ALL SELECT @rVal, 'Sunset'
    UNION ALL SELECT @rVal, 'Icebox' UNION ALL SELECT @rVal, 'Pearl' UNION ALL SELECT @rVal, 'Abyss'
    UNION ALL SELECT @rLol, 'Summoner''s Rift'
    UNION ALL SELECT @rLmtc, 'Summoner''s Rift'
    UNION ALL SELECT @rLqm, 'Bản đồ 5v5 tiêu chuẩn'
    UNION ALL SELECT @rPum, 'Erangel' UNION ALL SELECT @rPum, 'Miramar' UNION ALL SELECT @rPum, 'Sanhok' UNION ALL SELECT @rPum, 'Vikendi' UNION ALL SELECT @rPum, 'Livik'
) x
WHERE x.r IS NOT NULL AND NOT EXISTS (SELECT 1 FROM GameMap m WHERE m.GameRulesetID = x.r AND m.Name = x.n);

-- ===== Gắn bộ luật cho các giải test (để test xếp vị trí / cấm chọn bản đồ ngay) =====
INSERT INTO TournamentRuleset (TournamentID, GameRulesetID)
SELECT t.TournamentID, CASE t.GameID WHEN @val THEN @rVal WHEN @lol THEN @rLol WHEN @fco THEN @rFco END
FROM Tournament t
WHERE t.Name LIKE 'Giải test ·%' AND t.Status NOT IN ('COMPLETED', 'CANCELLED')
  AND t.GameID IN (@val, @lol, @fco)
  AND NOT EXISTS (SELECT 1 FROM TournamentRuleset tr WHERE tr.TournamentID = t.TournamentID);

-- ===== Kiểm tra =====
SELECT g.Title, r.GameRulesetID, r.Name AS BoLuat, r.Status,
       (SELECT COUNT(*) FROM GamePosition p WHERE p.GameRulesetID = r.GameRulesetID AND p.Status = 'ACTIVE') AS ViTri,
       (SELECT COUNT(*) FROM GameMap m WHERE m.GameRulesetID = r.GameRulesetID AND m.Status = 'ACTIVE') AS BanDo,
       (SELECT COUNT(*) FROM TournamentRuleset tr WHERE tr.GameRulesetID = r.GameRulesetID) AS SoGiaiDung
FROM GameRuleset r JOIN Game g ON g.GameID = r.GameID ORDER BY g.GameID, r.GameRulesetID;
