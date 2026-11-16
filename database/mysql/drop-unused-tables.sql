-- 17/11/2026 — Bỏ 3 bảng code không dùng (người dùng yêu cầu "bỏ những bảng không cần dùng cho bớt"): 36 -> 33 bảng.
--   SystemAnnouncement, SystemBanner: không màn hình/API nào đọc ghi (thông báo dùng bảng Notification).
--   TournamentFollow: chưa có chức năng theo dõi giải nào dùng.
-- Đã sao lưu cấu trúc + dữ liệu ở backup-unused-tables-20261117.sql (chạy file đó để khôi phục).
-- Không bảng nào khác tham chiếu tới 3 bảng này (chỉ chúng tham chiếu Account/Tournament).
SET NAMES utf8mb4 COLLATE utf8mb4_0900_as_ci;

DROP TABLE IF EXISTS TournamentFollow;
DROP TABLE IF EXISTS SystemBanner;
DROP TABLE IF EXISTS SystemAnnouncement;
