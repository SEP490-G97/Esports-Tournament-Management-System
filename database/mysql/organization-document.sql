-- Bang OrganizationDocument bi thieu trong script schema chinh (database/ETOMS_Database.sql va ban
-- MySQL cua no) du code C# (Models/OrganizationDocument.cs, EtomsDbContext.cs) da scaffold san va
-- OrganizationService dang dung that (upload/xet duyet giay to don vi). Thieu bang nay lam
-- GET /api/organizations/review-queue va POST /api/organizations/{id}/documents loi 500
-- "Table 'etoms_db.OrganizationDocument' doesn't exist".
-- Cau truc khop dung cau hinh EF trong Repositories/EtomsDbContext.cs (dong ~168-185).
-- Ap dung: mysql -h <server> -u <user> -p --default-character-set=utf8mb4 <ten_db> < organization-document.sql

SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS OrganizationDocument
(
    OrganizationDocumentID INT NOT NULL AUTO_INCREMENT,
    OrganizationID         INT NOT NULL,
    DocumentType           VARCHAR(100) NOT NULL,
    FileUrl                VARCHAR(500) NOT NULL,
    OriginalFileName       VARCHAR(255) NOT NULL,
    UploadedAt              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT PK_OrganizationDocument      PRIMARY KEY (OrganizationDocumentID),
    CONSTRAINT FK_OrganizationDocument_Org  FOREIGN KEY (OrganizationID) REFERENCES Organization (OrganizationID)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_0900_as_ci;
