-- Patch file: remove_buff_trigger_17780.sql
-- Description: Removing the line with ID = 17780 from the Buff_Triggers table
-- Author: [NLObP]
-- Date: [22.04.2025]

-- Check the existence of the record before removal
DELETE FROM `buff_triggers` 
WHERE `id` = 17780;