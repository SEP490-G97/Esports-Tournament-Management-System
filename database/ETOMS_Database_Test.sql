/*===============================================================================
  ETOMS - Kiem thu rang buoc nghiep vu cua luoc do v2 (25 bang)
  SEP490_G97

  Script CHEN du lieu thu roi co tinh vi pham tung quy tac nghiep vu, de chung
  minh rang chinh CO SO DU LIEU chan duoc, khong phai tang ung dung chan.

  CHAY LAI BAO NHIEU LAN CUNG DUOC: dau file tu don du lieu thu cua lan truoc.

  TRONG SSMS: chon database ETOMS roi bam F5. Phai ra "Dat: 12 / 12".

  BANG sqlcmd (bat buoc -f 65001, thieu la dau tieng Viet hong):
    sqlcmd -S .\SQLEXPRESS -E -d ETOMS -f 65001 -i ETOMS_Database_Test.sql

  Neu Account hoac Game dang rong thi chay ETOMS_Database.sql truoc.
===============================================================================*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

/*-------------------------------------------------------------------------------
  DỌN DỮ LIỆU THỬ CỦA LẦN CHẠY TRƯỚC.
  Nhờ bước này mà file chạy lại được nhiều lần mà không cần tạo lại database.
  Xóa theo thứ tự ngược, và CHỈ xóa đúng những dòng do chính script này tạo ra
  (nhận ra qua email @test.local, tag đội AAA/BBB, tên giải có chữ "thử").
-------------------------------------------------------------------------------*/
DELETE p FROM dbo.Penalty p
  JOIN dbo.Tournament t ON t.TournamentID = p.TournamentID WHERE t.Name LIKE N'%thử nghiệm v2%';
DELETE d FROM dbo.Dispute d
  JOIN dbo.Match m ON m.MatchID = d.MatchID
  JOIN dbo.Tournament t ON t.TournamentID = m.TournamentID WHERE t.Name LIKE N'%thử nghiệm v2%';
DELETE r FROM dbo.MatchReport r
  JOIN dbo.Match m ON m.MatchID = r.MatchID
  JOIN dbo.Tournament t ON t.TournamentID = m.TournamentID WHERE t.Name LIKE N'%thử nghiệm v2%';
DELETE m FROM dbo.Match m
  JOIN dbo.Tournament t ON t.TournamentID = m.TournamentID WHERE t.Name LIKE N'%thử nghiệm v2%';
DELETE n FROM dbo.BracketNode n
  JOIN dbo.Tournament t ON t.TournamentID = n.TournamentID WHERE t.Name LIKE N'%thử nghiệm v2%';
DELETE FROM dbo.Tournament WHERE Name LIKE N'%thử nghiệm v2%' OR Name = N'Giải mốc sai';
DELETE tm FROM dbo.TeamMember tm JOIN dbo.Team t ON t.TeamID = tm.TeamID WHERE t.Tag IN (N'AAA', N'BBB');
DELETE FROM dbo.Team WHERE Tag IN (N'AAA', N'BBB');
DELETE FROM dbo.Account WHERE Email IN (N'cap1@test.local', N'cap2@test.local', N'btc@test.local');
PRINT N'--- Đã dọn dữ liệu thử của lần chạy trước ---';
GO

DECLARE @ok INT = 0, @fail INT = 0;
DECLARE @gLol INT = (SELECT GameID FROM dbo.Game WHERE ShortCode = 'LOL');
DECLARE @gVal INT = (SELECT GameID FROM dbo.Game WHERE ShortCode = 'VAL');
DECLARE @adm  INT = (SELECT AccountID FROM dbo.Account WHERE Email = N'admin@etoms.local');

/* du lieu nen */
INSERT INTO dbo.Account (Email, PasswordHash, FullName, Role, Status, EmailVerifiedAt) VALUES
 (N'cap1@test.local', 'x', N'Đội trưởng A', 'PLAYER', 'ACTIVE', SYSUTCDATETIME()),
 (N'cap2@test.local', 'x', N'Đội trưởng B', 'PLAYER', 'ACTIVE', SYSUTCDATETIME()),
 (N'btc@test.local',  'x', N'Ban tổ chức',  'ORGANIZER', 'ACTIVE', SYSUTCDATETIME());
DECLARE @uA INT = (SELECT AccountID FROM dbo.Account WHERE Email = N'cap1@test.local');
DECLARE @uB INT = (SELECT AccountID FROM dbo.Account WHERE Email = N'cap2@test.local');
DECLARE @uO INT = (SELECT AccountID FROM dbo.Account WHERE Email = N'btc@test.local');

INSERT INTO dbo.Team (GameID, CaptainAccountID, Name, Tag) VALUES
 (@gLol, @uA, N'Đội Thử A', N'AAA'), (@gLol, @uB, N'Đội Thử B', N'BBB');
DECLARE @tA INT = (SELECT TeamID FROM dbo.Team WHERE Tag = N'AAA');
DECLARE @tB INT = (SELECT TeamID FROM dbo.Team WHERE Tag = N'BBB');

INSERT INTO dbo.TeamMember (TeamID, GameID, AccountID, Direction, Status, JoinedAt)
VALUES (@tA, @gLol, @uA, 'JOIN_BY_CODE', 'ACTIVE', SYSUTCDATETIME());

INSERT INTO dbo.Tournament (GameID, OrganizerAccountID, Name, Format, MaxTeams, StarterCount,
    MaxSubstitutes, RegOpenAt, RegCloseAt, StartAt, PenaltyThreshold, Status)
VALUES (@gLol, @uO, N'Giải thử nghiệm v2', 'SINGLE_ELIMINATION', 8, 5, 3,
    '2026-11-01', '2026-11-10', '2026-11-15', 3, 'PUBLISHED');
DECLARE @tour INT = (SELECT TournamentID FROM dbo.Tournament WHERE Name = N'Giải thử nghiệm v2');

INSERT INTO dbo.BracketNode (TournamentID, StageType, NodeType, RoundNo, PositionInRound, Status)
VALUES (@tour, 'KNOCKOUT', 'MATCH_SLOT', 1, 1, 'LOCKED');
DECLARE @node INT = (SELECT TOP 1 BracketNodeID FROM dbo.BracketNode WHERE TournamentID = @tour);

INSERT INTO dbo.Match (TournamentID, BracketNodeID, TeamAID, TeamBID, BestOf, Status)
VALUES (@tour, @node, @tA, @tB, 3, 'WAITING_REPORT');
DECLARE @m INT = (SELECT TOP 1 MatchID FROM dbo.Match WHERE TournamentID = @tour);
PRINT N'--- Đã dựng xong dữ liệu nền ---';

-- 1. Mốc thời gian sai thứ tự phải bị chặn
BEGIN TRY
    INSERT INTO dbo.Tournament (GameID, OrganizerAccountID, Name, Format, MaxTeams, StarterCount,
        MaxSubstitutes, RegOpenAt, RegCloseAt, StartAt)
    VALUES (@gLol, @uO, N'Giải mốc sai', 'ROUND_ROBIN', 4, 5, 2, '2026-12-20', '2026-12-10', '2026-12-25');
    PRINT N'1. KHONG chan duoc: moc thoi gian sai thu tu'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 1. Da chan: moc dang ky sai thu tu'; SET @ok += 1; END CATCH

-- 2. Hủy giải mà không có lý do phải bị chặn
BEGIN TRY
    UPDATE dbo.Tournament SET Status = 'CANCELLED', CancelReason = NULL WHERE TournamentID = @tour;
    PRINT N'2. KHONG chan duoc: huy giai khong co ly do'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 2. Da chan: huy giai ma khong co ly do'; SET @ok += 1; END CATCH

-- 3. Một người vào hai đội cùng một tựa game phải bị chặn
BEGIN TRY
    INSERT INTO dbo.TeamMember (TeamID, GameID, AccountID, Direction, Status, JoinedAt)
    VALUES (@tB, @gLol, @uA, 'JOIN_BY_CODE', 'ACTIVE', SYSUTCDATETIME());
    PRINT N'3. KHONG chan duoc: 1 nguoi vao 2 doi cung 1 game'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 3. Da chan: 1 nguoi vao 2 doi cung 1 tua game'; SET @ok += 1; END CATCH

-- 4. Hai báo cáo mù hợp lệ, bản thứ ba của cùng một đội phải bị chặn
INSERT INTO dbo.MatchReport (MatchID, TeamID, ReportedByAccountID, ReportedScoreA, ReportedScoreB)
VALUES (@m, @tA, @uA, 2, 1), (@m, @tB, @uB, 2, 1);
BEGIN TRY
    INSERT INTO dbo.MatchReport (MatchID, TeamID, ReportedByAccountID, ReportedScoreA, ReportedScoreB)
    VALUES (@m, @tA, @uA, 2, 0);
    PRINT N'4. KHONG chan duoc: ban bao cao thu ba'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 4. Da chan: doi nop hai ban bao cao cho mot tran'; SET @ok += 1; END CATCH

-- 5. Trận chốt mà không nói tỉ số đến từ đâu phải bị chặn (ràng buộc LI-01)
BEGIN TRY
    UPDATE dbo.Match SET Status = 'VERIFIED', ScoreA = 2, ScoreB = 1, ResultSource = NULL WHERE MatchID = @m;
    PRINT N'5. KHONG chan duoc: chot tran ma khong khai nguon ti so'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 5. Da chan: chot tran ma khong khai nguon ti so (LI-01)'; SET @ok += 1; END CATCH

-- 6. Phán quyết mà thiếu lý do phải bị chặn
INSERT INTO dbo.Dispute (MatchID, OpenReason, Status) VALUES (@m, 'CONFLICTING_REPORT', 'OPEN');
DECLARE @d INT = (SELECT TOP 1 DisputeID FROM dbo.Dispute WHERE MatchID = @m);
BEGIN TRY
    UPDATE dbo.Dispute SET Status = 'RESOLVED', FinalScoreA = 2, FinalScoreB = 1,
        RulingReason = NULL, ResolvedByAccountID = @uO WHERE DisputeID = @d;
    PRINT N'6. KHONG chan duoc: phan quyet ma khong co ly do'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 6. Da chan: phan quyet ma khong viet ly do'; SET @ok += 1; END CATCH

-- 7. Khiếu nại lần thứ hai phải bị chặn
UPDATE dbo.Dispute SET AppealCount = 1, AppealStatus = 'PENDING', AppealReason = N'Xin xem lại' WHERE DisputeID = @d;
BEGIN TRY
    UPDATE dbo.Dispute SET AppealCount = 2 WHERE DisputeID = @d;
    PRINT N'7. KHONG chan duoc: khieu nai lan thu hai'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 7. Da chan: khieu nai lan thu hai'; SET @ok += 1; END CATCH

-- 8. Hai tranh chấp đang mở trên cùng một trận phải bị chặn
BEGIN TRY
    INSERT INTO dbo.Dispute (MatchID, OpenReason, Status) VALUES (@m, 'REPORT_TIMEOUT', 'OPEN');
    PRINT N'8. KHONG chan duoc: hai tranh chap dang mo tren mot tran'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 8. Da chan: hai tranh chap dang mo tren mot tran'; SET @ok += 1; END CATCH

-- 9. Án phạt tính điểm và so được với ngưỡng của giải
INSERT INTO dbo.Penalty (TournamentID, TeamID, Severity, Points, Reason, IssuedByAccountID)
VALUES (@tour, @tA, 'MAJOR', 3, N'Vắng mặt không lý do', @uO);
IF (SELECT ActivePoints FROM dbo.vw_TeamPenaltyPoint WHERE TeamID = @tA) >=
   (SELECT PenaltyThreshold FROM dbo.vw_TeamPenaltyPoint WHERE TeamID = @tA)
BEGIN PRINT N'OK 9. Diem phat da dat nguong tuoc quyen cua giai'; SET @ok += 1; END
ELSE BEGIN PRINT N'9. View diem phat tinh sai'; SET @fail += 1; END

-- 10. Hủy án phạt mà không trỏ án cũ phải bị chặn
BEGIN TRY
    INSERT INTO dbo.Penalty (TournamentID, TeamID, Severity, Points, Reason, IssuedByAccountID, CancelsPenaltyID)
    VALUES (@tour, @tA, 'CANCELLATION', 0, N'Cấp nhầm', @uO, NULL);
    PRINT N'10. KHONG chan duoc: huy an phat ma khong tro an cu'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 10. Da chan: huy an phat ma khong tro an cu'; SET @ok += 1; END CATCH

-- 11. Check-in thủ công mà không có lý do phải bị chặn
BEGIN TRY
    UPDATE dbo.Match SET CheckInMode = 'MANUAL_OVERRIDE', OverrideReason = NULL WHERE MatchID = @m;
    PRINT N'11. KHONG chan duoc: check-in thu cong khong ly do'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 11. Da chan: check-in thu cong ma khong co ly do'; SET @ok += 1; END CATCH

-- 12. Dấu tiếng Việt lưu đúng
IF (SELECT FullName FROM dbo.Account WHERE Email = N'cap1@test.local') = N'Đội trưởng A'
BEGIN PRINT N'OK 12. Dau tieng Viet luu dung trong database'; SET @ok += 1; END
ELSE BEGIN PRINT N'12. Dau tieng Viet bi hong'; SET @fail += 1; END

PRINT N'================= TONG KET =================';
PRINT N'Dat:       ' + CAST(@ok   AS NVARCHAR(10)) + N' / 12';
PRINT N'Khong dat: ' + CAST(@fail AS NVARCHAR(10));
GO
