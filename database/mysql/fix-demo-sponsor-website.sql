-- 18/11/2026 — Nút "Website nhà tài trợ" ở trang chủ mở ra trang lỗi 404: hai nhà tài trợ DEMO được nhập website giả
-- (https://example.com/cyber, https://example.com/gear — tên miền mẫu, không có trang thật).
-- Bỏ website giả (NULL) => trang chủ / trang giải không hiện nút website cho nhà tài trợ demo nữa.
-- Chỉ sửa đúng 2 dòng demo có website example.com; nhà tài trợ thật do BTC nhập không bị đụng. Chạy lại được.
SET NAMES utf8mb4 COLLATE utf8mb4_0900_as_ci;

UPDATE TournamentSponsor SET WebsiteUrl = NULL
WHERE WebsiteUrl IN ('https://example.com/cyber', 'https://example.com/gear') AND Name LIKE '%(demo)';

SELECT TournamentSponsorID, Name, WebsiteUrl FROM TournamentSponsor ORDER BY TournamentSponsorID;
