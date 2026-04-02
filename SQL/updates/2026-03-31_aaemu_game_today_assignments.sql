CREATE TABLE IF NOT EXISTS `character_today_assignments` (
  `owner` int UNSIGNED NOT NULL,
  `real_step` int NOT NULL,
  `step_id` int NOT NULL,
  `group_id` int NOT NULL,
  `quest_context_id` int NOT NULL,
  `quest_id` bigint UNSIGNED NOT NULL DEFAULT 0,
  `quest_data` tinyblob NULL,
  `quest_status` tinyint UNSIGNED NOT NULL DEFAULT 0,
  `status` tinyint UNSIGNED NOT NULL DEFAULT 0,
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`owner`, `real_step`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
