-- -------------------------------------------------
-- CHANGE characters fields
-- -------------------------------------------------
ALTER TABLE `characters` MODIFY COLUMN `created_at` datetime(0) NULL DEFAULT CURRENT_TIMESTAMP AFTER `slots`;
ALTER TABLE `characters` MODIFY COLUMN `updated_at` datetime(0) NULL DEFAULT NULL AFTER `created_at`;
