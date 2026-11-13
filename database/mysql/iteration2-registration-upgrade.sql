-- Iteration 2 registration support. Run against the MySQL schema after taking a backup.
-- Requires MySQL 8.0.16+ (enforced CHECK constraints).
-- Constraint names and existing AuditLog actions were checked against etoms_db.

ALTER TABLE Registration
    ADD COLUMN WaitlistExpiresAt DATETIME NULL AFTER WaitlistPosition;

ALTER TABLE Registration
    DROP CHECK CK_Registration_Status,
    ADD CONSTRAINT CK_Registration_Status
        CHECK (Status IN ('DRAFT', 'PENDING', 'APPROVED', 'REJECTED', 'WAITLISTED',
                          'WITHDRAWN', 'DISQUALIFIED', 'EXPIRED'));

ALTER TABLE PaymentRecord
    DROP CHECK CK_PaymentRecord_Shape,
    ADD CONSTRAINT CK_PaymentRecord_Shape
        CHECK ((RecordKind = 'PROOF' AND (FileUrl IS NOT NULL OR Note IS NOT NULL))
            OR (RecordKind = 'REFUND' AND Note IS NOT NULL));

ALTER TABLE AuditLog
    DROP CHECK CK_AuditLog_Action,
    ADD CONSTRAINT CK_AuditLog_Action
        CHECK (ActionType IN ('FORCE_UPDATE', 'REMATCH', 'MANUAL_CHECKIN', 'DISQUALIFY',
                              'BAN', 'UNBAN', 'APPROVE', 'REJECT', 'RULEBOOK_EDIT',
                              'CANCEL_TOURNAMENT', 'GRANT_ORGANIZER', 'LOCK_BRACKET',
                              'WALKOVER', 'AUTO_VERIFY', 'ISSUE_PENALTY', 'CANCEL_PENALTY',
                              'SUSPEND_ORGANIZATION', 'TRANSFER_CHAIR', 'PROMOTE_WAITLIST',
                              'APPEAL_DECISION', 'VERIFY_PAYMENT', 'REFUND_RECORDED'));
