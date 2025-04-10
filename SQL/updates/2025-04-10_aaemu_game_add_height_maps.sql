-- --------------------------------------------
-- Table structure for height_map_cells
-- --------------------------------------------
CREATE TABLE `height_map_cells` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `zone_id` int(11) NOT NULL,
  `cell_x` int(11) NOT NULL,
  `cell_y` int(11) NOT NULL,
  `avg_z` float NOT NULL COMMENT 'Средняя высота в ячейке',
  `min_z` float NOT NULL COMMENT 'Минимальная высота в ячейке',
  `max_z` float NOT NULL COMMENT 'Максимальная высота в ячейке',
  `point_count` int(11) NOT NULL DEFAULT 1 COMMENT 'Количество точек в ячейке',
  `last_update` datetime NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `zone_cell` (`zone_id`,`cell_x`,`cell_y`),
  KEY `zone_coords` (`zone_id`,`cell_x`,`cell_y`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;