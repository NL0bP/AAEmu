-- -------------------------------------------------
-- Persist skills unlocked via ChangeSkillActiveType effect (emotes, dances, etc.)
-- -------------------------------------------------
DROP TABLE IF EXISTS `skill_active_types`;
CREATE TABLE `skill_active_types` (
  `owner` INT(11) NOT NULL COMMENT 'character id',
  `skill_id` INT(11) NOT NULL COMMENT 'unlocked skill id (e.g. emote)',
  `active_type` TINYINT(3) NOT NULL DEFAULT 1 COMMENT '1=all, 2=female, 3=male variant',
  PRIMARY KEY (`owner`, `skill_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='Tracks skills unlocked via ChangeSkillActiveType effect';
