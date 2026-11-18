-- UC-43 (15/11/2026): đơn xin làm Ban tổ chức ghi được mong muốn / lý do và email đề xuất cho tài khoản BTC
-- riêng mà Admin sẽ cấp. Chỉ THÊM 2 cột cho phép NULL, không đụng dữ liệu cũ. Chạy lại bao nhiêu lần cũng được.
-- Lược đồ gốc đã cập nhật cùng lúc: ETOMS_Database_MySQL.sql, ETOMS_Database.sql (SQL Server).
SET NAMES utf8mb4 COLLATE utf8mb4_0900_as_ci;
SET @has_note := (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Account' AND COLUMN_NAME = 'OrganizerRequestNote');
SET @ddl := IF(@has_note = 0, 'ALTER TABLE Account ADD COLUMN OrganizerRequestNote VARCHAR(1000) NULL AFTER OrganizerRequestedAt', 'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
SET @has_email := (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Account' AND COLUMN_NAME = 'OrganizerRequestEmail');
SET @ddl := IF(@has_email = 0, 'ALTER TABLE Account ADD COLUMN OrganizerRequestEmail VARCHAR(254) NULL AFTER OrganizerRequestNote', 'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
