-- -------------------------------------------------
-- Persist static/world doodad state by source template and world position
-- -------------------------------------------------
CREATE TABLE `world_doodads` (
  `id` int unsigned NOT NULL AUTO_INCREMENT,
  `source_template_id` int unsigned NOT NULL COMMENT 'Original static doodad template used to match spawn data',
  `template_id` int unsigned NOT NULL COMMENT 'Current doodad template to restore',
  `x` decimal(13,3) NOT NULL,
  `y` decimal(13,3) NOT NULL,
  `z` decimal(13,3) NOT NULL,
  `roll` float NOT NULL DEFAULT '0',
  `pitch` float NOT NULL DEFAULT '0',
  `yaw` float NOT NULL DEFAULT '0',
  `current_phase_id` int unsigned NOT NULL,
  `plant_time` datetime NOT NULL,
  `growth_time` datetime NOT NULL,
  `phase_time` datetime NOT NULL,
  `freshness_time` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `scale` float NOT NULL DEFAULT '1',
  `data` int NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `ux_world_doodads_anchor` (`source_template_id`, `x`, `y`, `z`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Persistent state overrides for static world doodads';
