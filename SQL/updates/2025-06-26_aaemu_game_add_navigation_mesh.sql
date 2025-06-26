-- --------------------------------------------
-- Table structure for navigation_mesh_raw
-- Содержит все исходные (неагрегированные) треугольники от клиентов.
-- --------------------------------------------
CREATE TABLE `navigation_mesh_raw` (
    `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
    `zone_id` INT UNSIGNED NOT NULL,
    `ax` FLOAT NOT NULL,
    `ay` FLOAT NOT NULL,
    `az` FLOAT NOT NULL,
    `bx` FLOAT NOT NULL,
    `by` FLOAT NOT NULL,
    `bz` FLOAT NOT NULL,
    `cx` FLOAT NOT NULL,
    `cy` FLOAT NOT NULL,
    `cz` FLOAT NOT NULL,
    `timestamp` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    INDEX (`zone_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- --------------------------------------------
-- Table structure for navigation_mesh
-- Содержит только агрегированные (сжатые) треугольники.
-- Таблица всегда перезаписывается.
-- --------------------------------------------
CREATE TABLE `navigation_mesh` (
    `zone_id` INT UNSIGNED NOT NULL,
    `ax` FLOAT NOT NULL,
    `ay` FLOAT NOT NULL,
    `az` FLOAT NOT NULL,
    `bx` FLOAT NOT NULL,
    `by` FLOAT NOT NULL,
    `bz` FLOAT NOT NULL,
    `cx` FLOAT NOT NULL,
    `cy` FLOAT NOT NULL,
    `cz` FLOAT NOT NULL,
    PRIMARY KEY (`zone_id`, `ax`, `ay`, `az`, `bx`, `by`, `bz`, `cx`, `cy`, `cz`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
