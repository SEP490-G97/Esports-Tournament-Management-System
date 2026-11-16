-- Sao lưu trước khi bỏ bảng (2026-11-16 18:01 UTC). Chạy file này để khôi phục.
SET NAMES utf8mb4 COLLATE utf8mb4_0900_as_ci;

CREATE TABLE `SystemAnnouncement` (
  `SystemAnnouncementID` int NOT NULL AUTO_INCREMENT,
  `Title` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci NOT NULL,
  `Body` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci NOT NULL,
  `LinkUrl` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci DEFAULT NULL,
  `TargetRoles` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci NOT NULL DEFAULT 'ALL',
  `Channel` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci NOT NULL DEFAULT 'IN_APP',
  `InAppCount` int NOT NULL DEFAULT '0',
  `EmailCount` int NOT NULL DEFAULT '0',
  `SentByAccountID` int NOT NULL,
  `SentAt` datetime NOT NULL DEFAULT (utc_timestamp()),
  PRIMARY KEY (`SystemAnnouncementID`),
  KEY `IX_SystemAnnouncement_SentAt` (`SentAt`),
  KEY `FK_SystemAnnouncement_By` (`SentByAccountID`),
  CONSTRAINT `FK_SystemAnnouncement_By` FOREIGN KEY (`SentByAccountID`) REFERENCES `Account` (`AccountID`),
  CONSTRAINT `CK_SystemAnnouncement_Channel` CHECK ((`Channel` in (_utf8mb4'IN_APP',_utf8mb4'EMAIL',_utf8mb4'BOTH'))),
  CONSTRAINT `CK_SystemAnnouncement_Counts` CHECK (((`InAppCount` >= 0) and (`EmailCount` >= 0)))
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_as_ci;

INSERT INTO `SystemAnnouncement` (`SystemAnnouncementID`,`Title`,`Body`,`LinkUrl`,`TargetRoles`,`Channel`,`InAppCount`,`EmailCount`,`SentByAccountID`,`SentAt`) VALUES (1,'Tesst','Dokkuhai Tesst app notify',NULL,'ALL','EMAIL',0,62,64,'2026-11-15 11:09:54');
INSERT INTO `SystemAnnouncement` (`SystemAnnouncementID`,`Title`,`Body`,`LinkUrl`,`TargetRoles`,`Channel`,`InAppCount`,`EmailCount`,`SentByAccountID`,`SentAt`) VALUES (2,'Allooooo','Aloooooooo',NULL,'ADMIN','IN_APP',2,0,64,'2026-11-15 11:39:04');
INSERT INTO `SystemAnnouncement` (`SystemAnnouncementID`,`Title`,`Body`,`LinkUrl`,`TargetRoles`,`Channel`,`InAppCount`,`EmailCount`,`SentByAccountID`,`SentAt`) VALUES (3,'Testttt','Aloooooo',NULL,'ADMIN','IN_APP',2,0,64,'2026-11-15 11:39:35');
INSERT INTO `SystemAnnouncement` (`SystemAnnouncementID`,`Title`,`Body`,`LinkUrl`,`TargetRoles`,`Channel`,`InAppCount`,`EmailCount`,`SentByAccountID`,`SentAt`) VALUES (4,'Alooooo','Alooooooooooooo',NULL,'CUSTOM','BOTH',1,1,64,'2026-11-15 19:17:45');
INSERT INTO `SystemAnnouncement` (`SystemAnnouncementID`,`Title`,`Body`,`LinkUrl`,`TargetRoles`,`Channel`,`InAppCount`,`EmailCount`,`SentByAccountID`,`SentAt`) VALUES (5,'Ê m','ê msdsd',NULL,'CUSTOM','IN_APP',1,0,64,'2026-11-15 19:19:43');

CREATE TABLE `SystemBanner` (
  `SystemBannerID` int NOT NULL AUTO_INCREMENT,
  `Title` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci NOT NULL,
  `Description` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci DEFAULT NULL,
  `ImageUrl` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci NOT NULL,
  `LinkUrl` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci DEFAULT NULL,
  `BannerType` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci NOT NULL DEFAULT 'EVENT',
  `IsActive` tinyint(1) NOT NULL DEFAULT '1',
  `DisplayOrder` int NOT NULL DEFAULT '1',
  `StartAt` datetime NOT NULL,
  `EndAt` datetime NOT NULL,
  `CreatedByAccountID` int NOT NULL,
  `CreatedAt` datetime NOT NULL DEFAULT (utc_timestamp()),
  `UpdatedAt` datetime NOT NULL DEFAULT (utc_timestamp()),
  PRIMARY KEY (`SystemBannerID`),
  KEY `IX_SystemBanner_Live` (`IsActive`,`StartAt`,`EndAt`),
  KEY `FK_SystemBanner_By` (`CreatedByAccountID`),
  CONSTRAINT `FK_SystemBanner_By` FOREIGN KEY (`CreatedByAccountID`) REFERENCES `Account` (`AccountID`),
  CONSTRAINT `CK_SystemBanner_Order` CHECK ((`DisplayOrder` >= 1)),
  CONSTRAINT `CK_SystemBanner_Period` CHECK ((`StartAt` < `EndAt`)),
  CONSTRAINT `CK_SystemBanner_Type` CHECK ((`BannerType` in (_utf8mb4'EVENT',_utf8mb4'GIVEAWAY')))
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_as_ci;

INSERT INTO `SystemBanner` (`SystemBannerID`,`Title`,`Description`,`ImageUrl`,`LinkUrl`,`BannerType`,`IsActive`,`DisplayOrder`,`StartAt`,`EndAt`,`CreatedByAccountID`,`CreatedAt`,`UpdatedAt`) VALUES (2,'CS2 at EWC 26: Spirit close out the event with a win','Team Spirit has defeated FUT Esports in the Grand Finals of Counter-Strike 2 at the Esports World Cup 2026. The finals reached their pinnacle at the Accor Arena, in Paris, France, marking the thrilling conclusion to seven-weeks of competition at EWC 26. Congratulations to Team Spirit.','https://esportsworldcup.com/_next/image?url=https%3A%2F%2Fd3h9qea4qy4169.cloudfront.net%2F83c3458d_8b88_40ef_ba2d_887796272c1a_5fe14b2c64.jpg&w=1920&q=50','https://esportsworldcup.com/en/news/cs2-at-ewc26-spirit-close-out-the-event-with-a-win','EVENT',1,1,'2026-11-15 11:27:00','2026-11-22 11:27:00',64,'2026-11-15 11:29:05','2026-11-15 17:12:20');
INSERT INTO `SystemBanner` (`SystemBannerID`,`Title`,`Description`,`ImageUrl`,`LinkUrl`,`BannerType`,`IsActive`,`DisplayOrder`,`StartAt`,`EndAt`,`CreatedByAccountID`,`CreatedAt`,`UpdatedAt`) VALUES (3,'Dota 2 at EWC 26 Champions: PVISION bounces back to crush BB Team','PVISION defeated BB Team in a thrilling four-game series to claim the Dota 2 at Esports World Cup 2026 championship. After dropping the opening game, PVISION battled back in a marathon Game 2 that lasted over an hour before closing out the series with two straight victories. Congratulations to PVISION.','https://esportsworldcup.com/_next/image?url=https%3A%2F%2Fd3h9qea4qy4169.cloudfront.net%2FEWC_26_20260719_CER_GEN_TOT_X_X_D2_PVIS_X_205442_8260_HQ_2af4d1bac2.JPG&w=1920&q=50','https://esportsworldcup.com/en/news/dota-2-at-ewc26-champions-pvision-bounces-back-to-crush-bb-team','EVENT',1,2,'2026-11-15 11:31:00','2026-11-22 11:31:00',64,'2026-11-15 11:31:55','2026-11-15 17:12:20');

CREATE TABLE `TournamentFollow` (
  `TournamentFollowID` int NOT NULL AUTO_INCREMENT,
  `AccountID` int NOT NULL,
  `TournamentID` int NOT NULL,
  `FollowedAt` datetime NOT NULL DEFAULT (utc_timestamp()),
  PRIMARY KEY (`TournamentFollowID`),
  UNIQUE KEY `UQ_TournamentFollow` (`AccountID`,`TournamentID`),
  KEY `FK_TournamentFollow_Tour` (`TournamentID`),
  CONSTRAINT `FK_TournamentFollow_Acc` FOREIGN KEY (`AccountID`) REFERENCES `Account` (`AccountID`),
  CONSTRAINT `FK_TournamentFollow_Tour` FOREIGN KEY (`TournamentID`) REFERENCES `Tournament` (`TournamentID`)
) ENGINE=InnoDB AUTO_INCREMENT=9 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_as_ci;

INSERT INTO `TournamentFollow` (`TournamentFollowID`,`AccountID`,`TournamentID`,`FollowedAt`) VALUES (1,14,3,'2026-10-20 00:00:00');
INSERT INTO `TournamentFollow` (`TournamentFollowID`,`AccountID`,`TournamentID`,`FollowedAt`) VALUES (2,19,3,'2026-10-20 00:00:00');
INSERT INTO `TournamentFollow` (`TournamentFollowID`,`AccountID`,`TournamentID`,`FollowedAt`) VALUES (3,24,3,'2026-10-20 00:00:00');
INSERT INTO `TournamentFollow` (`TournamentFollowID`,`AccountID`,`TournamentID`,`FollowedAt`) VALUES (4,29,3,'2026-10-20 00:00:00');
INSERT INTO `TournamentFollow` (`TournamentFollowID`,`AccountID`,`TournamentID`,`FollowedAt`) VALUES (8,12,4,'2026-11-01 00:00:00');

