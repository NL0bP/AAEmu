-- Prevent duplicate persistent house structural doodads from restart-time placeholder saves.
-- Keeps one existing row per house attach point before adding the integrity guard.

DELETE d
FROM `doodads` d
JOIN `doodads` keep
  ON keep.`owner_type` = d.`owner_type`
 AND keep.`house_id` = d.`house_id`
 AND keep.`attach_point` = d.`attach_point`
 AND keep.`id` < d.`id`
WHERE d.`owner_type` = 3
  AND d.`house_id` > 0
  AND d.`attach_point` <> 0;

ALTER TABLE `doodads`
  ADD COLUMN `structural_attach_point` int UNSIGNED
    GENERATED ALWAYS AS (
      CASE
        WHEN `owner_type` = 3 AND `house_id` > 0 AND `attach_point` <> 0 THEN `attach_point`
        ELSE NULL
      END
    ) STORED
    COMMENT 'Generated key part used to enforce one structural doodad per house attach point',
  ADD UNIQUE KEY `ux_doodads_house_structural_attach_point` (`owner_type`, `house_id`, `structural_attach_point`);
