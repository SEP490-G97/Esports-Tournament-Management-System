/*===============================================================================
  ETOMS - Kiem thu rang buoc nghiep vu cua luoc do v2.1 (27 bang)
  SEP490_G97

  Script CHEN du lieu thu roi co tinh vi pham tung quy tac nghiep vu, de chung
  minh rang chinh CO SO DU LIEU chan duoc, khong phai tang ung dung chan.

  CHAY LAI BAO NHIEU LAN CUNG DUOC, KE CA TREN DATABASE DANG CO DU LIEU CUA NHOM:
  toan bo phep thu chay trong MOT transaction va luon ROLLBACK o cuoi, nen khong de
  lai dong nao va KHONG BAO GIO xoa du lieu cua nguoi khac. Du lieu thu dung ten
  rieng (@kiemthu.local, doi KTA/KTB, "Giai kiem thu rang buoc") de khong trung.

  TRONG SSMS: chon database ETOMS roi bam F5. Phai ra "Dat: 22 / 22".

  BANG sqlcmd (bat buoc -f 65001, thieu la dau tieng Viet hong):
    sqlcmd -S .\SQLEXPRESS -E -d ETOMS -f 65001 -i ETOMS_Database_Test.sql

  Neu Account hoac Game dang rong thi chay ETOMS_Database.sql truoc.
===============================================================================*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

/*-------------------------------------------------------------------------------
  KHÔNG có bước xóa dữ liệu: mọi phép thử nằm trong MỘT transaction và cuối file
  luôn ROLLBACK. Database sau khi chạy y như trước khi chạy, kể cả khi nhóm đang
  dùng nó để demo. ID của dữ liệu thử lấy bằng SCOPE_IDENTITY() nên không lẫn với
  dòng có sẵn.
-------------------------------------------------------------------------------*/
DECLARE @ok INT = 0, @fail INT = 0;
DECLARE @gLol INT = (SELECT GameID FROM dbo.Game WHERE ShortCode = 'LOL');
DECLARE @gVal INT = (SELECT GameID FROM dbo.Game WHERE ShortCode = 'VAL');
DECLARE @adm  INT = (SELECT AccountID FROM dbo.Account WHERE Email = N'admin@etoms.local');
DECLARE @uA INT, @uB INT, @uO INT, @tA INT, @tB INT, @tour INT, @node INT, @m INT;

BEGIN TRANSACTION;

/* du lieu nen - ten rieng, khong trung du lieu cua nhom */
INSERT INTO dbo.Account (Email, PasswordHash, FullName, Role, Status, EmailVerifiedAt)
VALUES (N'kt.cap1@kiemthu.local', 'x', N'Đội trưởng A', 'PLAYER', 'ACTIVE', SYSUTCDATETIME());
SET @uA = SCOPE_IDENTITY();
INSERT INTO dbo.Account (Email, PasswordHash, FullName, Role, Status, EmailVerifiedAt)
VALUES (N'kt.cap2@kiemthu.local', 'x', N'Đội trưởng B', 'PLAYER', 'ACTIVE', SYSUTCDATETIME());
SET @uB = SCOPE_IDENTITY();
INSERT INTO dbo.Account (Email, PasswordHash, FullName, Role, Status, EmailVerifiedAt)
VALUES (N'kt.btc@kiemthu.local', 'x', N'Ban tổ chức', 'ORGANIZER', 'ACTIVE', SYSUTCDATETIME());
SET @uO = SCOPE_IDENTITY();

INSERT INTO dbo.Team (GameID, CaptainAccountID, Name, Tag) VALUES (@gLol, @uA, N'Đội kiểm thử A', N'KTA');
SET @tA = SCOPE_IDENTITY();
INSERT INTO dbo.Team (GameID, CaptainAccountID, Name, Tag) VALUES (@gLol, @uB, N'Đội kiểm thử B', N'KTB');
SET @tB = SCOPE_IDENTITY();

INSERT INTO dbo.TeamMember (TeamID, GameID, AccountID, Direction, Status, JoinedAt)
VALUES (@tA, @gLol, @uA, 'JOIN_BY_CODE', 'ACTIVE', SYSUTCDATETIME());

INSERT INTO dbo.Tournament (GameID, OrganizerAccountID, Name, Format, MaxTeams, StarterCount,
    MaxSubstitutes, RegOpenAt, RegCloseAt, StartAt, PenaltyThreshold, Status)
VALUES (@gLol, @uO, N'Giải kiểm thử ràng buộc', 'SINGLE_ELIMINATION', 8, 5, 3,
    '2026-11-01', '2026-11-10', '2026-11-15', 3, 'PUBLISHED');
SET @tour = SCOPE_IDENTITY();

INSERT INTO dbo.BracketNode (TournamentID, StageType, NodeType, RoundNo, PositionInRound, Status)
VALUES (@tour, 'KNOCKOUT', 'MATCH_SLOT', 1, 1, 'LOCKED');
SET @node = SCOPE_IDENTITY();

INSERT INTO dbo.Match (TournamentID, BracketNodeID, TeamAID, TeamBID, BestOf, Status)
VALUES (@tour, @node, @tA, @tB, 3, 'WAITING_REPORT');
SET @m = SCOPE_IDENTITY();
PRINT N'--- Đã dựng xong dữ liệu nền (trong transaction) ---';

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
IF (SELECT FullName FROM dbo.Account WHERE Email = N'kt.cap1@kiemthu.local') = N'Đội trưởng A'
BEGIN PRINT N'OK 12. Dau tieng Viet luu dung trong database'; SET @ok += 1; END
ELSE BEGIN PRINT N'12. Dau tieng Viet bi hong'; SET @fail += 1; END

/*-------------------------------------------------------------------------------
  v2.1 (31/10/2026) - cac luat moi theo Report 3 da chot 46 UC
-------------------------------------------------------------------------------*/

-- 13. Khóa tài khoản KHÔNG cần lý do phải ĐƯỢC PHÉP (UC-43, BR-AUTH-05)
BEGIN TRY
    UPDATE dbo.Account SET Status = 'BANNED', BanReason = NULL WHERE AccountID = @uB;
    UPDATE dbo.Account SET Status = 'ACTIVE' WHERE AccountID = @uB;
    PRINT N'OK 13. Khoa tai khoan khong can ly do: duoc phep'; SET @ok += 1;
END TRY BEGIN CATCH PRINT N'13. SAI: van bat ly do khi khoa tai khoan'; SET @fail += 1; END CATCH

-- 14. Yêu cầu quyền Organizer mà không có thời điểm gửi phải bị chặn (UC-43)
BEGIN TRY
    UPDATE dbo.Account SET OrganizerRequestStatus = 'PENDING', OrganizerRequestedAt = NULL WHERE AccountID = @uA;
    PRINT N'14. KHONG chan duoc: yeu cau Organizer khong co thoi diem'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 14. Da chan: yeu cau Organizer khong co thoi diem gui'; SET @ok += 1; END CATCH

-- 15. Giải loại trực tiếp mà khai số bảng phải bị chặn (BR-BRK-05)
BEGIN TRY
    UPDATE dbo.Tournament SET GroupCount = 2 WHERE TournamentID = @tour;
    PRINT N'15. KHONG chan duoc: giai loai truc tiep khai so bang'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 15. Da chan: giai loai truc tiep khai so bang'; SET @ok += 1; END CATCH

-- 16. Giải HYBRID đã công bố mà chưa khai số đội đi tiếp phải bị chặn (BR-BRK-05)
BEGIN TRY
    INSERT INTO dbo.Tournament (GameID, OrganizerAccountID, Name, Format, GroupCount, AdvancePerGroup,
        MaxTeams, StarterCount, MaxSubstitutes, RegOpenAt, RegCloseAt, StartAt, Status)
    VALUES (@gLol, @uO, N'Giải thử nghiệm v2 hybrid', 'HYBRID', 2, NULL, 8, 5, 3,
        '2026-11-01', '2026-11-10', '2026-11-15', 'PUBLISHED');
    PRINT N'16. KHONG chan duoc: HYBRID cong bo ma thieu so doi di tiep'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 16. Da chan: HYBRID cong bo ma thieu so doi di tiep'; SET @ok += 1; END CATCH

-- 17. Khóa nhánh mà không ghi ai khóa phải bị chặn (BR-BRK-04)
BEGIN TRY
    UPDATE dbo.Tournament SET BracketLockedAt = SYSUTCDATETIME(), BracketLockedByAccountID = NULL WHERE TournamentID = @tour;
    PRINT N'17. KHONG chan duoc: khoa nhanh khong ghi nguoi khoa'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 17. Da chan: khoa nhanh ma khong ghi nguoi khoa'; SET @ok += 1; END CATCH

-- 18. Bác khiếu nại mà không viết lý do phải bị chặn (BR-RES-09)
BEGIN TRY
    UPDATE dbo.Dispute SET AppealStatus = 'REJECTED', AppealOutcomeNote = NULL WHERE DisputeID = @d;
    PRINT N'18. KHONG chan duoc: bac khieu nai khong ly do'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 18. Da chan: bac khieu nai ma khong viet ly do'; SET @ok += 1; END CATCH

-- 19. Ghim hai tin lên banner cùng lúc phải bị chặn (UC-43)
-- (trong transaction: tam bo ghim tin dang ghim cua nhom de phep thu chinh xac; ROLLBACK se tra lai)
UPDATE dbo.ContentItem SET IsPinned = 0 WHERE IsPinned = 1;
INSERT INTO dbo.ContentItem (Kind, TournamentID, Title, Body, Status, IsPinned, AuthorAccountID, PublishedAt)
VALUES ('NEWS', @tour, N'Tin thử nghiệm v2 số 1', N'Nội dung', 'PUBLISHED', 1, @uO, SYSUTCDATETIME());
BEGIN TRY
    INSERT INTO dbo.ContentItem (Kind, TournamentID, Title, Body, Status, IsPinned, AuthorAccountID, PublishedAt)
    VALUES ('NEWS', @tour, N'Tin thử nghiệm v2 số 2', N'Nội dung', 'PUBLISHED', 1, @uO, SYSUTCDATETIME());
    PRINT N'19. KHONG chan duoc: ghim hai tin cung luc'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 19. Da chan: ghim hai tin len banner cung luc'; SET @ok += 1; END CATCH

-- 20. Hai mẫu cho cùng một loại thông báo phải bị chặn (UC-43, NOT-04)
-- (trong transaction: tam bo mau PENALTY co san; ROLLBACK se tra lai)
DELETE FROM dbo.ContentItem WHERE Kind = 'TEMPLATE' AND NotificationType = 'PENALTY';
INSERT INTO dbo.ContentItem (Kind, NotificationType, Title, Body, AuthorAccountID)
VALUES ('TEMPLATE', 'PENALTY', N'Mẫu thử nghiệm v2', N'Đội {team} nhận {points} điểm phạt.', @adm);
BEGIN TRY
    INSERT INTO dbo.ContentItem (Kind, NotificationType, Title, Body, AuthorAccountID)
    VALUES ('TEMPLATE', 'PENALTY', N'Mẫu thử nghiệm v2 trùng', N'Nội dung', @adm);
    PRINT N'20. KHONG chan duoc: hai mau cho mot loai thong bao'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 20. Da chan: hai mau cho cung mot loai thong bao'; SET @ok += 1; END CATCH

-- 21. Hai link cộng tác viên trùng mã phải bị chặn (UC-42)
INSERT INTO dbo.PromotionLink (TournamentID, Code, Label, CreatedByAccountID)
VALUES (@tour, 'KTRB01', N'Cộng tác viên A', @uO);
BEGIN TRY
    INSERT INTO dbo.PromotionLink (TournamentID, Code, Label, CreatedByAccountID)
    VALUES (@tour, 'KTRB01', N'Cộng tác viên B', @uO);
    PRINT N'21. KHONG chan duoc: hai link trung ma'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 21. Da chan: hai link cong tac vien trung ma'; SET @ok += 1; END CATCH

-- 22. Giấy tờ định danh là MỘT loại chung, không còn loại thẻ sinh viên riêng (UC-08)
BEGIN TRY
    INSERT INTO dbo.PlayerCredential (AccountID, CredentialType, FileUrl)
    VALUES (@uA, 'IDENTITY_DOCUMENT', N'/uploads/test/giay-to.png');
    BEGIN TRY
        INSERT INTO dbo.PlayerCredential (AccountID, CredentialType, FileUrl)
        VALUES (@uA, 'STUDENT_CARD', N'/uploads/test/the-sv.png');
        PRINT N'22. SAI: van nhan loai STUDENT_CARD'; SET @fail += 1;
    END TRY BEGIN CATCH PRINT N'OK 22. Giay to dinh danh dung mot loai chung, STUDENT_CARD bi chan'; SET @ok += 1; END CATCH
END TRY BEGIN CATCH PRINT N'22. SAI: khong nhan duoc IDENTITY_DOCUMENT'; SET @fail += 1; END CATCH

PRINT N'================= TONG KET =================';
PRINT N'Dat:       ' + CAST(@ok   AS NVARCHAR(10)) + N' / 22';
PRINT N'Khong dat: ' + CAST(@fail AS NVARCHAR(10));

ROLLBACK TRANSACTION;
PRINT N'--- Da ROLLBACK: database tro ve y nhu truoc khi chay ---';
GO
