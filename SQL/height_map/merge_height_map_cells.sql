SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- Создаем таблицу с последней версией структуры и правильным AUTO_INCREMENT
DROP TABLE IF EXISTS `height_map_cells`;
CREATE TABLE `height_map_cells` (
  `id` int NOT NULL AUTO_INCREMENT,
  `zone_id` int NOT NULL,
  `cell_x` int NOT NULL,
  `cell_y` int NOT NULL,
  `avg_z` float NOT NULL COMMENT 'Средняя высота в ячейке',
  `min_z` float NOT NULL COMMENT 'Минимальная высота в ячейке',
  `max_z` float NOT NULL COMMENT 'Максимальная высота в ячейке',
  `point_count` int NOT NULL DEFAULT 1 COMMENT 'Количество точек в ячейке',
  `last_update` datetime NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `zone_cell` (`zone_id`,`cell_x`,`cell_y`),
  KEY `zone_coords` (`zone_id`,`cell_x`,`cell_y`)
) ENGINE=InnoDB AUTO_INCREMENT=105250 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci ROW_FORMAT=DYNAMIC;

-- Вставляем данные с обработкой конфликтов
INSERT INTO `height_map_cells` 
VALUES
-- Данные из первого файла (height_map_cells.sql)
(99939,178,3799,3404,107.566,107.566,107.566,35,'2025-04-13 17:52:56'),
...
(102978,191,4148,2242,104.456,104.455,104.457,5,'2025-04-13 20:43:37'),
-- Данные из второго файла (height_map_cells_2.sql)
(102979,260,922,1137,149.414,149.414,149.414,26,'2025-04-24 00:59:29'),
...
(105249,178,3875,3440,193.251,193.251,193.251,30,'2025-04-24 01:38:57')
AS new_data
ON DUPLICATE KEY UPDATE 
  avg_z = IF(new_data.last_update > height_map_cells.last_update, new_data.avg_z, height_map_cells.avg_z),
  min_z = IF(new_data.last_update > height_map_cells.last_update, new_data.min_z, height_map_cells.min_z),
  max_z = IF(new_data.last_update > height_map_cells.last_update, new_data.max_z, height_map_cells.max_z),
  point_count = IF(new_data.last_update > height_map_cells.last_update, new_data.point_count, height_map_cells.point_count),
  last_update = IF(new_data.last_update > height_map_cells.last_update, new_data.last_update, height_map_cells.last_update);

SET FOREIGN_KEY_CHECKS = 1;