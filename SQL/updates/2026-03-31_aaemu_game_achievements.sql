CREATE TABLE IF NOT EXISTS `character_achievement_records` (
  `owner` int UNSIGNED NOT NULL,
  `record_id` int UNSIGNED NOT NULL,
  `amount` bigint UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (`owner`, `record_id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `character_achievements` (
  `owner` int UNSIGNED NOT NULL,
  `id` int UNSIGNED NOT NULL,
  `amount` int UNSIGNED NOT NULL DEFAULT 0,
  `completed_at` datetime NULL DEFAULT NULL,
  PRIMARY KEY (`owner`, `id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
