-- TEAM-01 (15/11/2026): thêm trường giới thiệu đội (catalogue ghi "Slogan") — không bắt buộc.
-- Chỉ THÊM một cột cho phép NULL, không đụng dữ liệu cũ. Chạy lại bao nhiêu lần cũng được.
-- Lược đồ gốc đã cập nhật cùng lúc: ETOMS_Database_MySQL.sql, ETOMS_Database.sql (SQL Server).
SET NAMES utf8mb4 COLLATE utf8mb4_0900_as_ci;
SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Team' AND COLUMN_NAME = 'Description');
SET @ddl := IF(@has_col = 0, 'ALTER TABLE Team ADD COLUMN Description VARCHAR(500) NULL AFTER LogoUrl', 'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
