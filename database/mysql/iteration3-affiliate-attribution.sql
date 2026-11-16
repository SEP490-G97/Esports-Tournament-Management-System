-- Iteration 3: retain the affiliate link that referred a successful registration.
-- Requires the existing Registration and PromotionLink tables and MySQL 8.0+.
-- Back up the local database first. This is a one-time migration; do not rerun
-- after PromotionLinkID, its index, or its foreign key has been created.
-- Apply to a local development database first. Do not apply to the shared
-- database until the team database owner has reviewed and approved it.

ALTER TABLE Registration
    ADD COLUMN PromotionLinkID INT NULL AFTER SubmittedByAccountID,
    ADD INDEX IX_Registration_PromotionLink (PromotionLinkID),
    ADD CONSTRAINT FK_Registration_PromotionLink
        FOREIGN KEY (PromotionLinkID)
        REFERENCES PromotionLink (PromotionLinkID)
        ON DELETE SET NULL;
