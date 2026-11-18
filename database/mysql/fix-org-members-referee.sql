-- 17/11/2026 — Dữ liệu đơn vị theo luật mới (Hướng 1: thành viên đơn vị phải là tài khoản Ban tổ chức).
-- Chạy SAU org-member-referee.sql (cần vai trò REFEREE). Chạy lại nhiều lần được.
SET NAMES utf8mb4 COLLATE utf8mb4_0900_as_ci;

-- 1) player20 (tài khoản Tuyển thủ) đang có lời mời Truyền thông vào CLB Hòa Lạc — không còn hợp lệ: gỡ lời mời.
UPDATE OrganizationMember SET Status = 'REMOVED'
WHERE OrganizationID = 2 AND AccountID = (SELECT AccountID FROM Account WHERE Email = 'player20@demo.etoms') AND Status = 'INVITED';

-- 2) trongtai.long: tài khoản demo dành cho trọng tài, không thuộc đội nào, đang làm trọng tài 1 trận của giải CLB Hòa Lạc.
--    Đổi thành tài khoản Ban tổ chức + vai Trọng tài (REFEREE) đang hoạt động của CLB Hòa Lạc để trang Trọng tài có người chọn.
UPDATE Account SET Role = 'ORGANIZER'
WHERE Email = 'trongtai.long@demo.etoms' AND Role = 'PLAYER'
  AND NOT EXISTS (SELECT 1 FROM TeamMember m WHERE m.AccountID = Account.AccountID AND m.Status IN ('ACTIVE','PENDING'));
UPDATE OrganizationMember SET Role = 'REFEREE', Status = 'ACTIVE', JoinedAt = COALESCE(JoinedAt, UTC_TIMESTAMP())
WHERE OrganizationID = 2 AND AccountID = (SELECT AccountID FROM Account WHERE Email = 'trongtai.long@demo.etoms' AND Role = 'ORGANIZER');

SELECT a.Email, a.Role AS AccountRole, m.OrganizationID, m.Role AS OrgRole, m.Status
FROM OrganizationMember m JOIN Account a ON a.AccountID = m.AccountID
WHERE a.Email IN ('player20@demo.etoms', 'trongtai.long@demo.etoms');
