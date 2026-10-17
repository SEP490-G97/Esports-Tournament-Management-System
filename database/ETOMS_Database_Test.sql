/*===============================================================================
  QUAN TRỌNG - PHẢI ĐỌC TRƯỚC KHI CHẠY
  Script này CHÈN dữ liệu mẫu rồi thử vi phạm từng quy tắc nghiệp vụ.
  Nó KHÔNG tự dọn dữ liệu, nên chỉ chạy đúng trên database VỪA TẠO MỚI.
  Chạy lần thứ hai mà không tạo lại database sẽ báo trùng khoá và sai kết quả.

  TRÌNH TỰ ĐÚNG:
    1. Mở ETOMS_Database.sql, BỎ CHÚ THÍCH phần BƯỚC 0 (xoá database cũ).
    2. Chạy ETOMS_Database.sql  -> tạo lại ETOMS từ đầu.
    3. Chạy file này            -> phải ra "Đạt: 23 / 23".
===============================================================================
  CHẠY BẰNG sqlcmd: phải thêm -f 65001 để đọc đúng UTF-8, nếu không dấu tiếng Việt
  trong câu lệnh sẽ bị hỏng. SSMS đọc đúng sẵn, không cần làm gì.
*/

/*-------------------------------------------------------------------------------
  Chạy được ở cả SQL Server lẫn Azure SQL: không dùng USE, mà kiểm tra đang ở
  đúng database hay chưa. Hãy CHỌN database ETOMS trước khi chạy file này.
-------------------------------------------------------------------------------*/
IF DB_NAME() IN (N'master', N'tempdb', N'model', N'msdb')
BEGIN
    PRINT N'DỪNG: đang ở database ' + DB_NAME() + N'. Hãy chọn database ETOMS rồi chạy lại.';
    PRINT N'(Các thông báo lỗi ngay sau dòng này là bình thường: script đã dừng, không tạo gì.)';
    SET NOEXEC ON;
END
GO

/*===============================================================================
  ETOMS - Bo kiem thu nghiep vu o muc DATABASE (23 muc)
  Chay sau khi ETOMS_Database.sql da tao xong. Chi doc/ghi du lieu thu.
===============================================================================*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

DECLARE @ok INT = 0, @fail INT = 0;

/*--- Du lieu nen -------------------------------------------------------------*/
DECLARE @gLol INT = (SELECT GameId FROM dbo.Game WHERE ShortCode = 'LOL');
DECLARE @gVal INT = (SELECT GameId FROM dbo.Game WHERE ShortCode = 'VAL');

INSERT INTO dbo.AppUser (Email, DisplayName, PasswordHash, Status) VALUES
 (N'btc@test.local',  N'Ban to chuc',  'x', 'ACTIVE'),
 (N'cap1@test.local', N'Doi truong A', 'x', 'ACTIVE'),
 (N'cap2@test.local', N'Doi truong B', 'x', 'ACTIVE'),
 (N'p1@test.local',   N'Nguoi choi 1', 'x', 'ACTIVE'),
 (N'tt1@test.local',  N'Trong tai 1',  'x', 'ACTIVE'),
 (N'tt2@test.local',  N'Trong tai 2',  'x', 'ACTIVE');

DECLARE @uBtc INT = (SELECT UserId FROM dbo.AppUser WHERE Email = N'btc@test.local');
DECLARE @uCa  INT = (SELECT UserId FROM dbo.AppUser WHERE Email = N'cap1@test.local');
DECLARE @uCb  INT = (SELECT UserId FROM dbo.AppUser WHERE Email = N'cap2@test.local');
DECLARE @uP1  INT = (SELECT UserId FROM dbo.AppUser WHERE Email = N'p1@test.local');
DECLARE @uTt1 INT = (SELECT UserId FROM dbo.AppUser WHERE Email = N'tt1@test.local');
DECLARE @uTt2 INT = (SELECT UserId FROM dbo.AppUser WHERE Email = N'tt2@test.local');

INSERT INTO dbo.Team (GameId, Name, Tag, CaptainUserId) VALUES
 (@gLol, N'Doi A', N'AAA', @uCa),
 (@gLol, N'Doi B', N'BBB', @uCb);

DECLARE @tA INT = (SELECT TeamId FROM dbo.Team WHERE Tag = N'AAA');
DECLARE @tB INT = (SELECT TeamId FROM dbo.Team WHERE Tag = N'BBB');

INSERT INTO dbo.TeamMembership (TeamId, GameId, UserId) VALUES (@tA, @gLol, @uP1);

INSERT INTO dbo.Tournament
 (Name, GameId, OrganizerUserId, Format, MaxTeams, StarterCount, MaxSubstitutes,
  RegistrationOpenAt, RegistrationCloseAt, StartAt)
VALUES
 (N'Giai thu nghiem', @gLol, @uBtc, 'SINGLE_ELIMINATION', 8, 5, 2,
  '2026-10-01', '2026-10-10', '2026-10-15');

DECLARE @tour INT = (SELECT TournamentId FROM dbo.Tournament WHERE Name = N'Giai thu nghiem');

INSERT INTO dbo.Registration (TournamentId, TeamId, GameId, Status) VALUES
 (@tour, @tA, @gLol, 'PENDING'), (@tour, @tB, @gLol, 'PENDING');

DECLARE @regA INT = (SELECT RegistrationId FROM dbo.Registration WHERE TournamentId=@tour AND TeamId=@tA);

INSERT INTO dbo.TournamentStage (TournamentId, StageNumber, Name, StageType)
VALUES (@tour, 1, N'Vong loai truc tiep', 'KNOCKOUT');

DECLARE @stage INT = (SELECT TournamentStageId FROM dbo.TournamentStage WHERE TournamentId=@tour);

INSERT INTO dbo.Bracket (TournamentStageId, TournamentId, Format) VALUES (@stage, @tour, 'SINGLE_ELIMINATION');
DECLARE @br INT = (SELECT BracketId FROM dbo.Bracket WHERE TournamentStageId=@stage);

-- Chi moi Trong tai 1 vao giai nay. Trong tai 2 KHONG duoc moi.
INSERT INTO dbo.TournamentReferee (TournamentId, UserId, Status) VALUES (@tour, @uTt1, 'ACCEPTED');

INSERT INTO dbo.Match (BracketId, TournamentId, RoundNumber, PositionInRound, TeamAId, TeamBId)
VALUES (@br, @tour, 1, 1, @tA, @tB);
DECLARE @m INT = (SELECT MatchId FROM dbo.Match WHERE BracketId=@br);

PRINT N'--- Da dung xong du lieu nen ---';

/*--- 1. Mot ban bao cao => WAITING -----------------------------------------*/
INSERT INTO dbo.MatchReport (MatchId, TeamId, ReportedTeamAScore, ReportedTeamBScore, ReportedByUserId)
VALUES (@m, @tA, 2, 1, @uCa);

DECLARE @s1 NVARCHAR(50) = (SELECT AgreementState FROM dbo.vw_MatchReportComparison WHERE MatchId=@m);
PRINT N'1. Sau 1 ban bao cao   -> ' + ISNULL(@s1, N'(khong co dong)');
IF @s1 = N'WAITING' SET @ok += 1; ELSE SET @fail += 1;

/*--- 2. Hai ban khop => MATCHED --------------------------------------------*/
INSERT INTO dbo.MatchReport (MatchId, TeamId, ReportedTeamAScore, ReportedTeamBScore, ReportedByUserId)
VALUES (@m, @tB, 2, 1, @uCb);

DECLARE @s2 NVARCHAR(50) = (SELECT AgreementState FROM dbo.vw_MatchReportComparison WHERE MatchId=@m);
PRINT N'2. Sau 2 ban bao cao khop -> ' + ISNULL(@s2, N'(khong co dong)');
IF @s2 = N'MATCHED' SET @ok += 1; ELSE SET @fail += 1;

/*--- 3. Con trong bao nhieu suat -------------------------------------------
  Suat CHI bi chiem khi don da duoc DUYET. Don PENDING duoc dem rieng.
  Nen truoc khi duyet: con 8/8 va PendingCount = 2. Sau khi duyet: con 6/8.  */
DECLARE @free0 NVARCHAR(20) = (SELECT CAST(FreeSlot AS NVARCHAR(20)) FROM dbo.vw_TournamentSlot WHERE TournamentId=@tour);
DECLARE @pend0 NVARCHAR(20) = (SELECT CAST(PendingCount AS NVARCHAR(20)) FROM dbo.vw_TournamentSlot WHERE TournamentId=@tour);
PRINT N'3a. Chua duyet don     -> con ' + ISNULL(@free0,N'?') + N'/8 suat, ' + ISNULL(@pend0,N'?') + N' don cho duyet';

UPDATE dbo.Registration SET Status='APPROVED' WHERE TournamentId=@tour;

DECLARE @free NVARCHAR(20) = (SELECT CAST(FreeSlot AS NVARCHAR(20)) FROM dbo.vw_TournamentSlot WHERE TournamentId=@tour);
PRINT N'3b. Sau khi duyet 2 don -> con ' + ISNULL(@free, N'?') + N'/8 suat';
IF @free0 = N'8' AND @pend0 = N'2' AND @free = N'6' SET @ok += 1; ELSE SET @fail += 1;

/*--- 4. Xac nhan ket qua (VERIFIED) ----------------------------------------*/
BEGIN TRY
    UPDATE dbo.Match
       SET Status='VERIFIED', TeamAScore=2, TeamBScore=1, WinnerTeamId=@tA
     WHERE MatchId=@m;
    PRINT N'4. Xac nhan ket qua    -> OK';
    SET @ok += 1;
END TRY
BEGIN CATCH
    PRINT N'4. Xac nhan ket qua    -> THAT BAI: ' + ERROR_MESSAGE();
    SET @fail += 1;
END CATCH

/*--- 5..13. Cac thao tac PHAI bi chan --------------------------------------*/
DECLARE @n INT = 5, @desc NVARCHAR(200);

-- 5. Sua ban bao cao da nop
BEGIN TRY
    UPDATE dbo.MatchReport SET ReportedTeamAScore = 9 WHERE MatchId=@m AND TeamId=@tA;
    PRINT N' 5. KHONG chan duoc: sua ban bao cao da nop'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK  5. Da chan: sua ban bao cao da nop'; SET @ok += 1; END CATCH

-- 6. Xoa AuditLog
INSERT INTO dbo.AuditLog (ActionType, ObjectType) VALUES ('TEST', 'SMOKE');
BEGIN TRY
    DELETE FROM dbo.AuditLog WHERE ActionType='TEST';
    PRINT N' 6. KHONG chan duoc: xoa AuditLog'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK  6. Da chan: xoa AuditLog'; SET @ok += 1; END CATCH

-- 7. Sua ket qua da VERIFIED
BEGIN TRY
    UPDATE dbo.Match SET TeamAScore = 5 WHERE MatchId=@m;
    PRINT N' 7. KHONG chan duoc: sua ket qua da VERIFIED'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK  7. Da chan: sua ket qua da VERIFIED'; SET @ok += 1; END CATCH

-- 8. Don dang ky trung cho cung 1 doi
BEGIN TRY
    INSERT INTO dbo.Registration (TournamentId, TeamId, GameId) VALUES (@tour, @tA, @gLol);
    PRINT N' 8. KHONG chan duoc: don dang ky trung'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK  8. Da chan: don dang ky trung'; SET @ok += 1; END CATCH

-- 9. Ban bao cao thu 3
BEGIN TRY
    INSERT INTO dbo.MatchReport (MatchId, TeamId, ReportedTeamAScore, ReportedTeamBScore, ReportedByUserId)
    VALUES (@m, @tA, 0, 2, @uCa);
    PRINT N' 9. KHONG chan duoc: ban bao cao thu 3'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK  9. Da chan: ban bao cao thu 3'; SET @ok += 1; END CATCH

-- 10. Xoa tai khoan
BEGIN TRY
    DELETE FROM dbo.AppUser WHERE UserId=@uP1;
    PRINT N'10. KHONG chan duoc: xoa tai khoan'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 10. Da chan: xoa tai khoan'; SET @ok += 1; END CATCH

-- 11. Them tran khi bracket da khoa
UPDATE dbo.Bracket SET Status='LOCKED', LockedAt=SYSUTCDATETIME(), LockedByUserId=@uBtc WHERE BracketId=@br;
BEGIN TRY
    INSERT INTO dbo.Match (BracketId, TournamentId, RoundNumber, PositionInRound) VALUES (@br, @tour, 1, 2);
    PRINT N'11. KHONG chan duoc: them tran khi bracket da khoa'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 11. Da chan: them tran khi bracket da khoa'; SET @ok += 1; END CATCH

-- 12. Mot nguoi vao 2 doi cung 1 game
BEGIN TRY
    INSERT INTO dbo.TeamMembership (TeamId, GameId, UserId) VALUES (@tB, @gLol, @uP1);
    PRINT N'12. KHONG chan duoc: 1 nguoi vao 2 doi cung 1 game'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 12. Da chan: 1 nguoi vao 2 doi cung 1 game'; SET @ok += 1; END CATCH

-- 13. Timeline sai (MSG-TOUR-001): dong dang ky sau ngay khai mac
BEGIN TRY
    INSERT INTO dbo.Tournament
     (Name, GameId, OrganizerUserId, Format, MaxTeams, StarterCount, MaxSubstitutes,
      RegistrationOpenAt, RegistrationCloseAt, StartAt)
    VALUES
     (N'Giai timeline sai', @gLol, @uBtc, 'SINGLE_ELIMINATION', 8, 5, 2,
      '2026-10-01', '2026-11-20', '2026-10-15');
    PRINT N'13. KHONG chan duoc: timeline dang ky sai (MSG-TOUR-001)'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 13. Da chan: timeline dang ky sai (MSG-TOUR-001)'; SET @ok += 1; END CATCH

-- 14. Dang ky doi LMHT vao giai Valorant (khoa ngoai ghep)
INSERT INTO dbo.Tournament
 (Name, GameId, OrganizerUserId, Format, MaxTeams, StarterCount, MaxSubstitutes,
  RegistrationOpenAt, RegistrationCloseAt, StartAt)
VALUES
 (N'Giai Valorant', @gVal, @uBtc, 'SINGLE_ELIMINATION', 8, 5, 2,
  '2026-10-01', '2026-10-10', '2026-10-15');
DECLARE @tourV INT = (SELECT TournamentId FROM dbo.Tournament WHERE Name = N'Giai Valorant');
BEGIN TRY
    INSERT INTO dbo.Registration (TournamentId, TeamId, GameId) VALUES (@tourV, @tA, @gLol);
    PRINT N'14. KHONG chan duoc: doi LMHT vao giai Valorant'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 14. Da chan: doi LMHT vao giai Valorant'; SET @ok += 1; END CATCH

-- 15. Dau tieng Viet co luu dung khong
DECLARE @vn INT = (SELECT COUNT(*) FROM dbo.NotificationTemplate
                   WHERE BodyTemplate LIKE N'%Tài khoản đang bị khoá%');
IF @vn = 1
BEGIN PRINT N'OK 15. Dau tieng Viet luu dung trong database'; SET @ok += 1; END
ELSE
BEGIN PRINT N'15. SAI: dau tieng Viet bi loi trong database'; SET @fail += 1; END

-- 16. Gan trong tai CHUA duoc moi vao giai nay -> phai bi chan
BEGIN TRY
    UPDATE dbo.Match SET RefereeUserId = @uTt2 WHERE MatchId = @m;
    PRINT N'16. KHONG chan duoc: gan trong tai chua duoc moi vao giai'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 16. Da chan: gan trong tai chua duoc moi vao giai'; SET @ok += 1; END CATCH

-- 17. Gan trong tai DA duoc moi -> phai thanh cong
BEGIN TRY
    UPDATE dbo.Match SET RefereeUserId = @uTt1 WHERE MatchId = @m;
    PRINT N'OK 17. Gan duoc trong tai da duoc moi vao giai'; SET @ok += 1;
END TRY BEGIN CATCH
    PRINT N'17. SAI: khong gan duoc trong tai hop le - ' + ERROR_MESSAGE(); SET @fail += 1;
END CATCH

-- 18. Tran KHONG co trong tai van hop le (TOUR-13: quyen phan xu ve Ban to chuc)
BEGIN TRY
    UPDATE dbo.Match SET RefereeUserId = NULL WHERE MatchId = @m;
    PRINT N'OK 18. Tran khong co trong tai van hop le (TOUR-13)'; SET @ok += 1;
END TRY BEGIN CATCH
    PRINT N'18. SAI: khong de trong duoc trong tai - ' + ERROR_MESSAGE(); SET @fail += 1;
END CATCH

-- 19. The phat cua giai nay tro toi tran cua GIAI KHAC -> phai bi chan
INSERT INTO dbo.TournamentStage (TournamentId, StageNumber, Name, StageType)
VALUES (@tourV, 1, N'Vong loai truc tiep', 'KNOCKOUT');
DECLARE @stageV INT = (SELECT TournamentStageId FROM dbo.TournamentStage WHERE TournamentId=@tourV);
INSERT INTO dbo.Bracket (TournamentStageId, TournamentId, Format) VALUES (@stageV, @tourV, 'SINGLE_ELIMINATION');
DECLARE @brV INT = (SELECT BracketId FROM dbo.Bracket WHERE TournamentStageId=@stageV);
BEGIN TRY
    -- Bracket cua giai Valorant nhung khai TournamentId cua giai LMHT
    INSERT INTO dbo.Match (BracketId, TournamentId, RoundNumber, PositionInRound)
    VALUES (@brV, @tour, 1, 1);
    PRINT N'19. KHONG chan duoc: tran khai sai giai so voi nhanh dau'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 19. Da chan: tran khai sai giai so voi nhanh dau'; SET @ok += 1; END CATCH

-- 20. The phat cua giai LMHT nhung tro toi tran cua giai Valorant -> phai bi chan
INSERT INTO dbo.Match (BracketId, TournamentId, RoundNumber, PositionInRound)
VALUES (@brV, @tourV, 1, 1);
DECLARE @mV INT = (SELECT MatchId FROM dbo.Match WHERE BracketId=@brV);
BEGIN TRY
    INSERT INTO dbo.Penalty (TeamId, TournamentId, MatchId, Severity, Reason, IssuedByUserId)
    VALUES (@tA, @tour, @mV, 'WARNING', N'Thu the phat tro sai giai', @uBtc);
    PRINT N'20. KHONG chan duoc: the phat tro toi tran cua giai khac'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 20. Da chan: the phat tro toi tran cua giai khac'; SET @ok += 1; END CATCH

PRINT N'';

-- 21. Phuong thuc xep hat giong: chi RANDOM_DRAW moi duoc luu seed boc tham
BEGIN TRY
    UPDATE dbo.Bracket SET SeedingMethod='MANUAL', SeedingRandomSeed=12345 WHERE BracketId=@br;
    PRINT N'21. KHONG chan duoc: luu seed boc tham khi khong phai RANDOM_DRAW'; SET @fail += 1;
    UPDATE dbo.Bracket SET SeedingRandomSeed=NULL WHERE BracketId=@br;
END TRY BEGIN CATCH PRINT N'OK 21. Da chan: luu seed boc tham khi phuong thuc khong phai RANDOM_DRAW'; SET @ok += 1; END CATCH

-- 22. Boc tham hop le: RANDOM_DRAW kem seed -> phai cho phep, va tai lap duoc
BEGIN TRY
    UPDATE dbo.Bracket SET SeedingMethod='RANDOM_DRAW', SeedingRandomSeed=20261006 WHERE BracketId=@br;
    IF (SELECT SeedingRandomSeed FROM dbo.Bracket WHERE BracketId=@br) = 20261006
    BEGIN PRINT N'OK 22. Boc tham luu duoc seed de tai lap'; SET @ok += 1; END
    ELSE BEGIN PRINT N'22. Seed boc tham khong luu dung'; SET @fail += 1; END
END TRY BEGIN CATCH PRINT N'22. Chan nham boc tham hop le: ' + ERROR_MESSAGE(); SET @fail += 1; END CATCH

-- 23. Phuong thuc xep hat giong la khong hop le -> phai bi chan
BEGIN TRY
    UPDATE dbo.Bracket SET SeedingMethod='BOC_THAM_TAY' WHERE BracketId=@br;
    PRINT N'23. KHONG chan duoc: phuong thuc xep hat giong khong hop le'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 23. Da chan: phuong thuc xep hat giong khong hop le'; SET @ok += 1; END CATCH

/*-------------------------------------------------------------------------------
  24..32 - NAM MODULE MOI (bo sung 17/10/2026)
  Kiem dung nhung rang buoc ma mo ta chuc nang doi hoi, khong kiem bay dat.
-------------------------------------------------------------------------------*/

-- 24. GRL-01: MOT tua game phai duoc co NHIEU bo luat.
--     Day la dieu bang GameRule cu KHONG cho (UNIQUE tren GameId).
BEGIN TRY
    INSERT INTO dbo.Ruleset (GameId, Name, MinStarters, MaxStarters, MaxSubstitutes,
                             DefaultFormat, DefaultBestOf, AllowedEvidenceTypes)
    VALUES (@gLol, N'Tournament Draft', 5, 5, 3, 'SINGLE_ELIMINATION', 5, N'png,jpg');
    IF (SELECT COUNT(*) FROM dbo.Ruleset WHERE GameId=@gLol) >= 3
    BEGIN PRINT N'OK 24. Mot tua game co nhieu bo luat (GRL-01)'; SET @ok += 1; END
    ELSE BEGIN PRINT N'24. Khong tao duoc bo luat thu ba'; SET @fail += 1; END
END TRY BEGIN CATCH PRINT N'24. Chan nham bo luat thu ba: ' + ERROR_MESSAGE(); SET @fail += 1; END CATCH

-- 25. GRL-01: cung mot tua game thi TEN bo luat khong duoc trung
BEGIN TRY
    INSERT INTO dbo.Ruleset (GameId, Name, MinStarters, MaxStarters, MaxSubstitutes,
                             DefaultFormat, DefaultBestOf, AllowedEvidenceTypes)
    VALUES (@gLol, N'Draft Pick', 5, 5, 3, 'SINGLE_ELIMINATION', 3, N'png,jpg');
    PRINT N'25. KHONG chan duoc: trung ten bo luat trong cung mot game'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 25. Da chan: trung ten bo luat trong cung mot game'; SET @ok += 1; END CATCH

-- 26. GRL-01: game doi khang don -> bo luat khai so nguoi bang 1 phai hop le
BEGIN TRY
    INSERT INTO dbo.Ruleset (GameId, Name, MinStarters, MaxStarters, MaxSubstitutes,
                             DefaultFormat, DefaultBestOf, AllowedEvidenceTypes)
    VALUES (@gVal, N'Doi khang don', 1, 1, 0, 'SINGLE_ELIMINATION', 3, N'png,jpg');
    PRINT N'OK 26. Bo luat thi dau don (1 nguoi) hop le'; SET @ok += 1;
END TRY BEGIN CATCH PRINT N'26. Chan nham bo luat thi dau don: ' + ERROR_MESSAGE(); SET @fail += 1; END CATCH

-- 27. ORG-02 / ORG-09: tu choi hoac dinh chi don vi BAT BUOC kem ly do
DECLARE @org INT;
INSERT INTO dbo.Organization (Name, OrgType, OwnerUserId, Status)
VALUES (N'CLB Thu nghiem', 'SCHOOL_CLUB', @uBtc, 'PENDING');
SET @org = SCOPE_IDENTITY();
BEGIN TRY
    UPDATE dbo.Organization SET Status='REJECTED', ReviewNote=NULL WHERE OrganizationId=@org;
    PRINT N'27. KHONG chan duoc: tu choi don vi ma khong co ly do'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 27. Da chan: tu choi don vi ma khong co ly do (ORG-02)'; SET @ok += 1; END CATCH

-- 28. ORG-02: tu choi KEM ly do thi phai cho phep
BEGIN TRY
    UPDATE dbo.Organization SET Status='REJECTED', ReviewNote=N'Thieu giay to xac nhan cua truong'
    WHERE OrganizationId=@org;
    PRINT N'OK 28. Tu choi don vi kem ly do thi duoc'; SET @ok += 1;
    UPDATE dbo.Organization SET Status='APPROVED', ReviewNote=NULL WHERE OrganizationId=@org;
END TRY BEGIN CATCH PRINT N'28. Chan nham tu choi co ly do: ' + ERROR_MESSAGE(); SET @fail += 1; END CATCH

-- 29. ORG-06: mot don vi chi co DUNG MOT chu nhiem dang hoat dong
INSERT INTO dbo.OrganizationMember (OrganizationId, UserId, OrgRole) VALUES (@org, @uBtc, 'OWNER');
BEGIN TRY
    INSERT INTO dbo.OrganizationMember (OrganizationId, UserId, OrgRole) VALUES (@org, @uCa, 'OWNER');
    PRINT N'29. KHONG chan duoc: hai chu nhiem cung mot don vi'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 29. Da chan: hai chu nhiem cung mot don vi (ORG-06)'; SET @ok += 1; END CATCH

-- 30. ORG-04: mot nguoi thuoc NHIEU don vi thi phai duoc
DECLARE @org2 INT;
INSERT INTO dbo.Organization (Name, OrgType, OwnerUserId, Status)
VALUES (N'Cong ty Thu nghiem', 'EVENT_COMPANY', @uCa, 'APPROVED');
SET @org2 = SCOPE_IDENTITY();
BEGIN TRY
    INSERT INTO dbo.OrganizationMember (OrganizationId, UserId, OrgRole) VALUES (@org2, @uBtc, 'OPERATOR');
    PRINT N'OK 30. Mot nguoi thuoc nhieu don vi (ORG-04)'; SET @ok += 1;
END TRY BEGIN CATCH PRINT N'30. Chan nham nguoi thuoc nhieu don vi: ' + ERROR_MESSAGE(); SET @fail += 1; END CATCH

-- 31. SPN-02: mot nha tai tro xuat hien o nhieu giai VOI HANG KHAC NHAU
DECLARE @spn INT;
INSERT INTO dbo.Sponsor (OrganizationId, Name) VALUES (@org2, N'Nha tai tro Thu nghiem');
SET @spn = SCOPE_IDENTITY();
BEGIN TRY
    INSERT INTO dbo.TournamentSponsor (TournamentId, SponsorId, Tier) VALUES (@tour,  @spn, 'GOLD');
    INSERT INTO dbo.TournamentSponsor (TournamentId, SponsorId, Tier) VALUES (@tourV, @spn, 'SILVER');
    IF (SELECT COUNT(DISTINCT Tier) FROM dbo.TournamentSponsor WHERE SponsorId=@spn) = 2
    BEGIN PRINT N'OK 31. Mot nha tai tro, hai giai, hai hang khac nhau (SPN-02)'; SET @ok += 1; END
    ELSE BEGIN PRINT N'31. Khong luu duoc hai hang khac nhau'; SET @fail += 1; END
END TRY BEGIN CATCH PRINT N'31. Chan nham gan nha tai tro: ' + ERROR_MESSAGE(); SET @fail += 1; END CATCH

-- 32. ITG-01: dau hieu bat thuong phai thuoc dung bon loai da dinh nghia
BEGIN TRY
    INSERT INTO dbo.IntegritySignal (MatchId, SignalType) VALUES (@m, 'PHAN_TICH_ANH');
    PRINT N'32. KHONG chan duoc: loai dau hieu khong co trong dinh nghia'; SET @fail += 1;
END TRY BEGIN CATCH PRINT N'OK 32. Da chan: loai dau hieu khong co trong dinh nghia (ITG-01)'; SET @ok += 1; END CATCH

PRINT N'================= TONG KET =================';
PRINT N'Dat:    ' + CAST(@ok   AS NVARCHAR(10)) + N' / 32';
PRINT N'Khong dat: ' + CAST(@fail AS NVARCHAR(10));
GO

SET NOEXEC OFF;
GO
