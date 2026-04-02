-- -------------------------------------------------
-- Adding fields to the `items` table
-- -------------------------------------------------
ALTER TABLE items
    ADD COLUMN freshness_time DATETIME NULL DEFAULT NULL
	AFTER charge_count;
ALTER TABLE items
    ADD COLUMN charge_use_skill_time DATETIME NULL DEFAULT NULL
	AFTER freshness_time;
ALTER TABLE items
    ADD COLUMN charge_start_time DATETIME NULL DEFAULT NULL
	AFTER charge_use_skill_time;
ALTER TABLE items
    ADD COLUMN charge_proc_time DATETIME NULL DEFAULT NULL
	AFTER charge_start_time;
