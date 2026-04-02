-- -------------------------------------------------
-- Adding fields to the `doodads` table
-- -------------------------------------------------
ALTER TABLE doodads
    ADD COLUMN freshness_time DATETIME NULL DEFAULT NULL
	AFTER phase_time;
