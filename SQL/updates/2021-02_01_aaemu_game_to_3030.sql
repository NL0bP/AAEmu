SET FOREIGN_KEY_CHECKS=0;

CREATE TABLE `aaemu_game_3030`.`accounts`  (
  `account_id` int(11) NOT NULL,
  `credits` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`account_id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8 COLLATE = utf8_general_ci ROW_FORMAT = DYNAMIC;

ALTER TABLE `aaemu_game_3030`.`auction_house` ADD COLUMN `end_time` datetime(0) NOT NULL AFTER `creation_time`;

ALTER TABLE `aaemu_game_3030`.`auction_house` DROP COLUMN `time_left`;

ALTER TABLE `aaemu_game_3030`.`auction_house` DROP COLUMN `item_name`;

ALTER TABLE `aaemu_game_3030`.`auction_house` DROP COLUMN `category_a`;

ALTER TABLE `aaemu_game_3030`.`auction_house` DROP COLUMN `category_b`;

ALTER TABLE `aaemu_game_3030`.`auction_house` DROP COLUMN `category_c`;

ALTER TABLE `aaemu_game_3030`.`characters` ADD COLUMN `pvp_honor` int(11) NOT NULL DEFAULT 0 AFTER `crime_record`;

ALTER TABLE `aaemu_game_3030`.`characters` ADD COLUMN `hostile_faction_kills` int(11) NOT NULL DEFAULT 0 AFTER `pvp_honor`;

CREATE TABLE `aaemu_game_3030`.`doodads`  (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `owner_id` int(11) NULL DEFAULT NULL,
  `owner_type` tinyint(4) UNSIGNED NULL DEFAULT 255,
  `template_id` int(11) NOT NULL,
  `current_phase_id` int(11) NOT NULL,
  `plant_time` datetime(0) NOT NULL,
  `growth_time` datetime(0) NOT NULL,
  `phase_time` datetime(0) NOT NULL,
  `x` float NOT NULL,
  `y` float NOT NULL,
  `z` float NOT NULL,
  `rotation_x` tinyint(4) NOT NULL,
  `rotation_y` tinyint(4) NOT NULL,
  `rotation_z` tinyint(4) NOT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8 COLLATE = utf8_general_ci ROW_FORMAT = Dynamic;

ALTER TABLE `aaemu_game_3030`.`housings` ADD COLUMN `place_date` datetime(0) NOT NULL DEFAULT CURRENT_TIMESTAMP(0) AFTER `permission`;

ALTER TABLE `aaemu_game_3030`.`housings` ADD COLUMN `protected_until` datetime(0) NOT NULL DEFAULT CURRENT_TIMESTAMP(0) AFTER `place_date`;

ALTER TABLE `aaemu_game_3030`.`housings` ADD COLUMN `faction_id` int(10) UNSIGNED NOT NULL DEFAULT 1 AFTER `protected_until`;

ALTER TABLE `aaemu_game_3030`.`housings` ADD COLUMN `sell_to` int(10) UNSIGNED NOT NULL DEFAULT 0 AFTER `faction_id`;

ALTER TABLE `aaemu_game_3030`.`housings` ADD COLUMN `sell_price` bigint(20) NOT NULL DEFAULT 0 AFTER `sell_to`;

ALTER TABLE `aaemu_game_3030`.`mails` MODIFY COLUMN `extra` bigint(20) NOT NULL AFTER `returned`;

SET FOREIGN_KEY_CHECKS=1;