-- Sửa dữ liệu demo lệch (15/11/2026): 7 giải được nạp thẳng trạng thái ONGOING/COMPLETED nhưng
-- KHÔNG khóa nhánh (BRK-03) và không trận nào có kết quả -> trang Nhánh đấu báo "chưa khóa nên chưa công bố".
--   1) Khóa nhánh 7 giải 6, 9, 12, 15, 16, 19, 21 (người khóa = BTC của giải, như BracketService.LockBracketAsync).
--   2) Hai giải ĐÃ KẾT THÚC (9 Valorant, 16 PUBG Mobile): mỗi trận có hai báo cáo mù khớp nhau (MatchReport
--      MATCHED) -> trận VERIFIED / AUTO_MATCHED (đúng LI-01), đội thắng được đẩy lên trận cha theo
--      ResultService.AdvanceWinnerAsync (vị trí lẻ -> TeamA, chẵn -> TeamB).
-- An toàn khi chạy lại: mọi UPDATE đều có điều kiện trạng thái cũ, INSERT có NOT EXISTS.
SET NAMES utf8mb4 COLLATE utf8mb4_0900_as_ci;

-- ===== 1) Khóa nhánh =====
UPDATE Tournament
SET BracketLockedAt = GREATEST(RegCloseAt, DATE_SUB(StartAt, INTERVAL 1 DAY)),
    BracketLockedByAccountID = OrganizerAccountID
WHERE TournamentID IN (6, 9, 12, 15, 16, 19, 21)
  AND Status IN ('ONGOING', 'COMPLETED')
  AND BracketLockedAt IS NULL;

UPDATE BracketNode SET Status = 'LOCKED'
WHERE TournamentID IN (6, 9, 12, 15, 16, 19, 21) AND Status = 'DRAFT';

-- ===== 2a) Giải 16 — PUBG Mobile Mùa Hè 2026 (4 đội, BO1) =====
-- Bán kết 63: Bão Cát (24) 1-0 Chiến Thần (25); bán kết 64: Độc Hành (26) 0-1 Sa Mạc (23)
-- Chung kết 65: Bão Cát (24) 1-0 Sa Mạc (23) -> Bão Cát vô địch.
UPDATE `Match` SET ScheduledAt = '2026-07-20 07:00:00', TeamACheckedInAt = '2026-07-20 06:45:00', TeamBCheckedInAt = '2026-07-20 06:47:00',
    ScoreA = 1, ScoreB = 0, WinnerTeamID = 24, Status = 'VERIFIED', ResultSource = 'AUTO_MATCHED', VerifiedAt = '2026-07-20 08:10:00'
WHERE MatchID = 63 AND TournamentID = 16 AND Status = 'PENDING' AND TeamAID = 24 AND TeamBID = 25;
UPDATE `Match` SET ScheduledAt = '2026-07-20 09:00:00', TeamACheckedInAt = '2026-07-20 08:44:00', TeamBCheckedInAt = '2026-07-20 08:46:00',
    ScoreA = 0, ScoreB = 1, WinnerTeamID = 23, Status = 'VERIFIED', ResultSource = 'AUTO_MATCHED', VerifiedAt = '2026-07-20 10:05:00'
WHERE MatchID = 64 AND TournamentID = 16 AND Status = 'PENDING' AND TeamAID = 26 AND TeamBID = 23;
UPDATE `Match` SET TeamAID = 24, TeamBID = 23
WHERE MatchID = 65 AND TournamentID = 16 AND Status = 'PENDING' AND TeamAID IS NULL AND TeamBID IS NULL;
UPDATE `Match` SET ScheduledAt = '2026-07-23 12:00:00', TeamACheckedInAt = '2026-07-23 11:40:00', TeamBCheckedInAt = '2026-07-23 11:42:00',
    ScoreA = 1, ScoreB = 0, WinnerTeamID = 24, Status = 'VERIFIED', ResultSource = 'AUTO_MATCHED', VerifiedAt = '2026-07-23 13:15:00'
WHERE MatchID = 65 AND TournamentID = 16 AND Status = 'PENDING' AND TeamAID = 24 AND TeamBID = 23;

-- ===== 2b) Giải 9 — Valorant Mùa Xuân 2026 (6 đội, 2 suất đặc cách, BO1) =====
-- Vòng 1 — 70: Phượng Hoàng (15) 1-0 Hỏa Long (17); 71: Team Dung (7) 0-1 Thần Sấm (16)
-- Bán kết — 72: Bạch Tuyết (18, đặc cách) 1-0 Phượng Hoàng (15); 73: Lửa Đỏ (8, đặc cách) 0-1 Thần Sấm (16)
-- Chung kết 74: Bạch Tuyết (18) 1-0 Thần Sấm (16) -> Bạch Tuyết vô địch.
UPDATE `Match` SET ScheduledAt = '2026-08-20 07:00:00', TeamACheckedInAt = '2026-08-20 06:45:00', TeamBCheckedInAt = '2026-08-20 06:50:00',
    ScoreA = 1, ScoreB = 0, WinnerTeamID = 15, Status = 'VERIFIED', ResultSource = 'AUTO_MATCHED', VerifiedAt = '2026-08-20 08:20:00'
WHERE MatchID = 70 AND TournamentID = 9 AND Status = 'PENDING' AND TeamAID = 15 AND TeamBID = 17;
UPDATE `Match` SET ScheduledAt = '2026-08-20 09:30:00', TeamACheckedInAt = '2026-08-20 09:12:00', TeamBCheckedInAt = '2026-08-20 09:15:00',
    ScoreA = 0, ScoreB = 1, WinnerTeamID = 16, Status = 'VERIFIED', ResultSource = 'AUTO_MATCHED', VerifiedAt = '2026-08-20 10:40:00'
WHERE MatchID = 71 AND TournamentID = 9 AND Status = 'PENDING' AND TeamAID = 7 AND TeamBID = 16;
-- Node 79 (vị trí 2) -> TeamB của trận 72; node 81 (vị trí 4) -> TeamB của trận 73.
UPDATE `Match` SET TeamBID = 15 WHERE MatchID = 72 AND TournamentID = 9 AND Status = 'PENDING' AND TeamAID = 18 AND TeamBID IS NULL;
UPDATE `Match` SET TeamBID = 16 WHERE MatchID = 73 AND TournamentID = 9 AND Status = 'PENDING' AND TeamAID = 8 AND TeamBID IS NULL;
UPDATE `Match` SET ScheduledAt = '2026-08-22 07:00:00', TeamACheckedInAt = '2026-08-22 06:40:00', TeamBCheckedInAt = '2026-08-22 06:44:00',
    ScoreA = 1, ScoreB = 0, WinnerTeamID = 18, Status = 'VERIFIED', ResultSource = 'AUTO_MATCHED', VerifiedAt = '2026-08-22 08:15:00'
WHERE MatchID = 72 AND TournamentID = 9 AND Status = 'PENDING' AND TeamAID = 18 AND TeamBID = 15;
UPDATE `Match` SET ScheduledAt = '2026-08-22 09:30:00', TeamACheckedInAt = '2026-08-22 09:10:00', TeamBCheckedInAt = '2026-08-22 09:11:00',
    ScoreA = 0, ScoreB = 1, WinnerTeamID = 16, Status = 'VERIFIED', ResultSource = 'AUTO_MATCHED', VerifiedAt = '2026-08-22 10:35:00'
WHERE MatchID = 73 AND TournamentID = 9 AND Status = 'PENDING' AND TeamAID = 8 AND TeamBID = 16;
-- Node 82 (vị trí 1) -> TeamA, node 83 (vị trí 2) -> TeamB của chung kết 74.
UPDATE `Match` SET TeamAID = 18, TeamBID = 16
WHERE MatchID = 74 AND TournamentID = 9 AND Status = 'PENDING' AND TeamAID IS NULL AND TeamBID IS NULL;
UPDATE `Match` SET ScheduledAt = '2026-08-24 12:00:00', TeamACheckedInAt = '2026-08-24 11:41:00', TeamBCheckedInAt = '2026-08-24 11:45:00',
    ScoreA = 1, ScoreB = 0, WinnerTeamID = 18, Status = 'VERIFIED', ResultSource = 'AUTO_MATCHED', VerifiedAt = '2026-08-24 13:20:00'
WHERE MatchID = 74 AND TournamentID = 9 AND Status = 'PENDING' AND TeamAID = 18 AND TeamBID = 16;

-- ===== 2c) Hai báo cáo mù khớp nhau cho mỗi trận đã xác nhận ở trên (đội trưởng nộp) =====
INSERT INTO MatchReport (MatchID, TeamID, ReportedByAccountID, ReportedScoreA, ReportedScoreB, PerGameScores, Status, SubmittedAt)
SELECT m.MatchID, m.TeamAID, t.CaptainAccountID, m.ScoreA, m.ScoreB, NULL, 'MATCHED', DATE_SUB(m.VerifiedAt, INTERVAL 5 MINUTE)
FROM `Match` m JOIN Team t ON t.TeamID = m.TeamAID
WHERE m.MatchID IN (63, 64, 65, 70, 71, 72, 73, 74) AND m.Status = 'VERIFIED'
  AND NOT EXISTS (SELECT 1 FROM MatchReport r WHERE r.MatchID = m.MatchID AND r.TeamID = m.TeamAID);
INSERT INTO MatchReport (MatchID, TeamID, ReportedByAccountID, ReportedScoreA, ReportedScoreB, PerGameScores, Status, SubmittedAt)
SELECT m.MatchID, m.TeamBID, t.CaptainAccountID, m.ScoreA, m.ScoreB, NULL, 'MATCHED', m.VerifiedAt
FROM `Match` m JOIN Team t ON t.TeamID = m.TeamBID
WHERE m.MatchID IN (63, 64, 65, 70, 71, 72, 73, 74) AND m.Status = 'VERIFIED'
  AND NOT EXISTS (SELECT 1 FROM MatchReport r WHERE r.MatchID = m.MatchID AND r.TeamID = m.TeamBID);
