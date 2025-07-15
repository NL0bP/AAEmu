-- -------------------------------------------------
-- Adding fields to the `housings` table
-- -------------------------------------------------
ALTER TABLE housings
ADD COLUMN already_paid BOOL NOT NULL DEFAULT FALSE AFTER allow_recover,
ADD COLUMN paid_weeks int NOT NULL DEFAULT 0 AFTER already_paid;
