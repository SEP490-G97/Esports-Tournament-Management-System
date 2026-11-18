-- 17/11/2026 — Hướng 1 "Ban tổ chức là tổ chức": thêm vai trò Trọng tài (REFEREE) cho thành viên đơn vị.
-- Chủ nhiệm phân vai ở trang Quản lý đơn vị; chỉ Trọng tài của đơn vị đứng tên giải mới được gán vào trận.
-- Chỉ đổi ràng buộc CHECK, không đụng dữ liệu. Chạy lại nhiều lần được.
SET NAMES utf8mb4 COLLATE utf8mb4_0900_as_ci;

ALTER TABLE OrganizationMember DROP CHECK CK_OrganizationMember_Role;
ALTER TABLE OrganizationMember
    ADD CONSTRAINT CK_OrganizationMember_Role CHECK (Role IN ('CHAIR','TOURNAMENT_OPERATOR','MARKETING','REFEREE'));
