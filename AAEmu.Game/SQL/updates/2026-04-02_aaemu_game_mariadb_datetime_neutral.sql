-- -------------------------------------------------
-- MariaDB 10.11 compatibility: neutral datetime scheme
-- Converts legacy out-of-range sentinels (year < 1000) to NULL
-- -------------------------------------------------

ALTER TABLE `attendances`
    MODIFY COLUMN `account_attendance` DATETIME NULL DEFAULT NULL;

UPDATE `attendances`
SET `account_attendance` = NULL
WHERE `account_attendance` IS NOT NULL
  AND `account_attendance` < '1000-01-01 00:00:00';

ALTER TABLE `characters`
    MODIFY COLUMN `dead_time` DATETIME NULL DEFAULT NULL,
    MODIFY COLUMN `rez_time` DATETIME NULL DEFAULT NULL,
    MODIFY COLUMN `leave_time` DATETIME NULL DEFAULT NULL,
    MODIFY COLUMN `delete_request_time` DATETIME NULL DEFAULT NULL,
    MODIFY COLUMN `transfer_request_time` DATETIME NULL DEFAULT NULL,
    MODIFY COLUMN `delete_time` DATETIME NULL DEFAULT NULL,
    MODIFY COLUMN `created_at` DATETIME NULL DEFAULT CURRENT_TIMESTAMP,
    MODIFY COLUMN `updated_at` DATETIME NULL DEFAULT NULL;

UPDATE `characters` SET `dead_time` = NULL WHERE `dead_time` IS NOT NULL AND `dead_time` < '1000-01-01 00:00:00';
UPDATE `characters` SET `rez_time` = NULL WHERE `rez_time` IS NOT NULL AND `rez_time` < '1000-01-01 00:00:00';
UPDATE `characters` SET `leave_time` = NULL WHERE `leave_time` IS NOT NULL AND `leave_time` < '1000-01-01 00:00:00';
UPDATE `characters` SET `delete_request_time` = NULL WHERE `delete_request_time` IS NOT NULL AND `delete_request_time` < '1000-01-01 00:00:00';
UPDATE `characters` SET `transfer_request_time` = NULL WHERE `transfer_request_time` IS NOT NULL AND `transfer_request_time` < '1000-01-01 00:00:00';
UPDATE `characters` SET `delete_time` = NULL WHERE `delete_time` IS NOT NULL AND `delete_time` < '1000-01-01 00:00:00';
UPDATE `characters` SET `created_at` = NULL WHERE `created_at` IS NOT NULL AND `created_at` < '1000-01-01 00:00:00';
UPDATE `characters` SET `updated_at` = NULL WHERE `updated_at` IS NOT NULL AND `updated_at` < '1000-01-01 00:00:00';

ALTER TABLE `expedition_applicants`
    MODIFY COLUMN `reg_time` DATETIME NULL DEFAULT NULL;

UPDATE `expedition_applicants`
SET `reg_time` = NULL
WHERE `reg_time` IS NOT NULL
  AND `reg_time` < '1000-01-01 00:00:00';

ALTER TABLE `expedition_recruitments`
    MODIFY COLUMN `reg_time` DATETIME NULL DEFAULT NULL,
    MODIFY COLUMN `end_time` DATETIME NULL DEFAULT NULL;

UPDATE `expedition_recruitments`
SET `reg_time` = NULL
WHERE `reg_time` IS NOT NULL
  AND `reg_time` < '1000-01-01 00:00:00';

UPDATE `expedition_recruitments`
SET `end_time` = NULL
WHERE `end_time` IS NOT NULL
  AND `end_time` < '1000-01-01 00:00:00';

ALTER TABLE `expeditions`
    MODIFY COLUMN `protect_time` DATETIME NULL DEFAULT NULL,
    MODIFY COLUMN `last_exp_update_time` DATETIME NULL DEFAULT NULL;

UPDATE `expeditions`
SET `protect_time` = NULL
WHERE `protect_time` IS NOT NULL
  AND `protect_time` < '1000-01-01 00:00:00';

UPDATE `expeditions`
SET `last_exp_update_time` = NULL
WHERE `last_exp_update_time` IS NOT NULL
  AND `last_exp_update_time` < '1000-01-01 00:00:00';

ALTER TABLE `items`
    MODIFY COLUMN `unsecure_time` DATETIME NULL DEFAULT NULL,
    MODIFY COLUMN `unpack_time` DATETIME NULL DEFAULT NULL,
    MODIFY COLUMN `created_at` DATETIME NULL DEFAULT NULL,
    MODIFY COLUMN `freshness_time` DATETIME NULL DEFAULT NULL,
    MODIFY COLUMN `charge_use_skill_time` DATETIME NULL DEFAULT NULL,
    MODIFY COLUMN `charge_start_time` DATETIME NULL DEFAULT NULL,
    MODIFY COLUMN `charge_proc_time` DATETIME NULL DEFAULT NULL;

UPDATE `items` SET `unsecure_time` = NULL WHERE `unsecure_time` IS NOT NULL AND `unsecure_time` < '1000-01-01 00:00:00';
UPDATE `items` SET `unpack_time` = NULL WHERE `unpack_time` IS NOT NULL AND `unpack_time` < '1000-01-01 00:00:00';
UPDATE `items` SET `created_at` = NULL WHERE `created_at` IS NOT NULL AND `created_at` < '1000-01-01 00:00:00';
UPDATE `items` SET `freshness_time` = NULL WHERE `freshness_time` IS NOT NULL AND `freshness_time` < '1000-01-01 00:00:00';
UPDATE `items` SET `charge_use_skill_time` = NULL WHERE `charge_use_skill_time` IS NOT NULL AND `charge_use_skill_time` < '1000-01-01 00:00:00';
UPDATE `items` SET `charge_start_time` = NULL WHERE `charge_start_time` IS NOT NULL AND `charge_start_time` < '1000-01-01 00:00:00';
UPDATE `items` SET `charge_proc_time` = NULL WHERE `charge_proc_time` IS NOT NULL AND `charge_proc_time` < '1000-01-01 00:00:00';

ALTER TABLE `doodads`
    MODIFY COLUMN `freshness_time` DATETIME NULL DEFAULT NULL;

UPDATE `doodads`
SET `freshness_time` = NULL
WHERE `freshness_time` IS NOT NULL
  AND `freshness_time` < '1000-01-01 00:00:00';

ALTER TABLE `world_doodads`
    MODIFY COLUMN `freshness_time` DATETIME NULL DEFAULT NULL;

UPDATE `world_doodads`
SET `freshness_time` = NULL
WHERE `freshness_time` IS NOT NULL
  AND `freshness_time` < '1000-01-01 00:00:00';

ALTER TABLE `uccs`
    MODIFY COLUMN `modified` DATETIME NULL DEFAULT NULL;

UPDATE `uccs`
SET `modified` = NULL
WHERE `modified` IS NOT NULL
  AND `modified` < '1000-01-01 00:00:00';
