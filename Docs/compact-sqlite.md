# compact.sqlite3 Reference

This document explains the structure and usage of the game data SQLite database `compact.sqlite3` used by AAEmu.Game.

## Location & Access
ONLY USE THE ONE IN AAEmu.Game/Data/ FOLDER! The others are copied to their environment on build. (Like Relase/Debug) 
- File: `AAEmu.Game/Data/compact.sqlite3` (copied to output via project settings)
- Local path example: `/mnt/d/Repos/AAEmu.GameData/compact.sqlite3`
- Access: `Microsoft.Data.Sqlite` via helpers:
  - `AAEmu.Game/Utils/DB/SQLite.cs` (creates read-only connection)
  - `AAEmu.Game/Utils/DB/CompactSqliteHelper.cs` (tables, columns, indices, row counts)

Build copy behavior and verification
- On build, the file is copied to `AAEmu.Game/bin/<Config>/net9.0/Data/compact.sqlite3` and that runtime copy is what the server reads.
- When applying patches, prefer applying to `AAEmu.Game/Data/compact.sqlite3` and then rebuild. Alternatively patch the runtime copy directly and avoid rebuilding afterward (rebuild may overwrite your changes).
- Quick verification after build: `sqlite3 AAEmu.Game/bin/<Config>/net9.0/Data/compact.sqlite3 "SELECT COUNT(1) FROM sqlite_master;"` or specific row checks.

## Purpose & Characteristics
- Read-only content DB extracted from client assets; not relational gameplay state.
- Holds static definitions for items, skills, doodads, quests, FX, crafting, UI text hooks, etc.
- SQLite types observed: `INT`, `REAL`, `TEXT`, `NUM` (used as boolean 0/1), occasional `PRIMARY KEY` on integer IDs.
 - Localization: many human-readable fields (names, descriptions, UI strings) are in Korean. Searching by English text may not yield results; prefer IDs, category keys, or explicit queries that target known columns. Localization exists within the compact.sqlite3's 'localized_texts' table. It is recommended to first identify the Korean equivalent to any English string to lookup. Korean string is in the 'ko' column, and English string is in the 'en_us' column.

## High-Level Schema (Domains)
- Core entities: `items`, `skills`, `effects`, `projectiles`, `unit_modifiers`, `unit_attribute_limits`, `unit_reqs`, `tags`, `icons`.
- World/doodads: many `doodad_*` tables (bundles, families, spawns, craft, loot, water volumes, medicalingredient_mines).
- Quests: `quest_*` (categories, objectives, actions like `quest_act_supply_coppers`, `quest_act_supply_exps`, guards, phase checks).
- Crafting: `crafts`, `craft_packs`, `craft_pack_crafts`, `train_craft_*`.
- Equipment: `equip_pack_*`, `equip_slot_groups`, `equip_slot_group_maps`, `mate_equip_*`.
- Combat/buffs: `buff_triggers`, `buff_tick_effects`, `aoe_diminishings`, `reset_aoe_diminishing_effects`, `aoe_shapes`, `combat_sounds`.
- FX/visuals: `fx_groups`, `fx_particles`, `fx_materials`, `fx_cgfs`, `fx_cgas`, `fx_decals`, `fx_chrs`, `bubble_effects`, `spawn_effects`.
- Economy/auction: `auction_a_categories`, `auction_b_categories`, `auction_c_categories`, `merchant_packs`.
- Housing/ships: `housing_*`, `item_housing_*`, `shipyard_steps`, `item_shipyards`.
- Meta/util: `schema_migrations`, `ai_files`, `world_groups`, `districts`, `game_rule_events`, `game_score_rules`.

## Selected Tables (examples)
- `icons(id INT, filename TEXT, name TEXT)`
- `fx_groups(id INT, name TEXT)`
- `equip_slot_groups(id INT, name TEXT)`
- `craft_packs(id INT, name TEXT)`
- `chat_spam_rules(id INT, name TEXT)` and `chat_spam_rule_details(...)`
- Inventory expansion: `bag_expands(is_bank NUM, step INT, price INT, item_id INT, item_count INT, currency_id INT)`
  - Server computes next size as `50 + 10 * (1 + step)` where `step = (currentSlots - 50) / 10`.
  - Add more rows for higher `step` values to raise the cap (e.g., steps 8, 9, 10 → 140/150/160 slots).
- `auction_a_categories(id INT, name TEXT)` (also `_b_`, `_c_` variants)
- `aoe_diminishings(id INT, rate REAL)`
- `quest_act_supply_exps(id INT, exp INT)`; `quest_act_supply_coppers(id INT, amount INT)`
- `imprint_ucc_effects(id INT, item_id INT)`
- Many `doodad_*` and `quest_*` tables with integer FK-like columns by naming convention (not enforced by SQLite).

## Skills/Plots Quick Reference
- Execution paths:
  - Effect-driven (no `plot_id`): skill applies `skill_effects` directly via `Skill.ApplyEffects()`.
  - Plot-driven (`plot_id` > 0): sequenced via plots (`plots`, `plot_events`, `plot_effects`, `plot_next_events`), with optional conditions (`plot_conditions`, `plot_event_conditions`, `plot_aoe_conditions`).
  - Movement controllers: `skill_controllers` drive motion (e.g., leap); independent of plots.
- Projectiles: mostly client visuals. Server does not simulate flight; `SpecialEffect.Projectile` is a no-op. Plots compute travel delay when timing at impact is required.
- CC semantics live on `buffs` (boolean flags) and are applied via `buff_effects`.
- See `Docs/skills-plots.md` for a deeper dive, examples, and ready-to-run SQL.

### Items → Effects Chain (How items apply buffs)
- Typical pipeline when an item applies a buff via “Use”: 
  1) `items.use_skill_id` → skill row
  2) `skill_effects(skill_id)` → `effects(id)` fan-out
  3) `effects.actual_type='BuffEffect'` → `effects.actual_id` references `buff_effects.id`
  4) `buff_effects(buff_id)` → the concrete `buffs.id`
  5) Numeric changes live on modifiers tied to the buff:
     - `unit_modifiers(owner_type='Buff', owner_id=<buff_id>, unit_attribute_id, value)`
     - `skill_modifiers(owner_type='Buff', owner_id=<buff_id>, skill_attribute_id, unit_modifier_type_id, value)`

- Quick lookup template (replace Xs accordingly):
  - Item → Skill: `SELECT id, name, use_skill_id FROM items WHERE id = <ITEM_ID>;`
  - Skill → Effect: `SELECT id, effect_id FROM skill_effects WHERE skill_id = <SKILL_ID> ORDER BY weight;`
  - Effect → BuffEffect: `SELECT id, actual_type, actual_id FROM effects WHERE id = <EFFECT_ID>;` (expect `actual_type='BuffEffect'`)
  - BuffEffect → Buff: `SELECT id, buff_id FROM buff_effects WHERE id = <actual_id>;`
  - Buff → Modifiers:
    - `SELECT id, unit_attribute_id, unit_modifier_type_id, value FROM unit_modifiers WHERE owner_type='Buff' AND owner_id=<BUFF_ID>;`
    - `SELECT id, skill_attribute_id, unit_modifier_type_id, value FROM skill_modifiers WHERE owner_type='Buff' AND owner_id=<BUFF_ID>;`

- Example (Vocation Expertise Tonic, item_id=8000020):
  - `items(8000020).use_skill_id = 8000013`
  - `skill_effects(skill_id=8000013) → effect_id=8000032`
  - `effects(8000032) → actual_type='BuffEffect', actual_id=8000022`
  - `buff_effects(8000022) → buff_id=8000010`
  - Modifiers on `buff_id=8000010`:
    - Vocation gain: `unit_modifiers(owner_type='Buff', owner_id=8000010, unit_attribute_id=137, value=10)`
    - Production time: `skill_modifiers(owner_type='Buff', owner_id=8000010, skill_attribute_id=4, unit_modifier_type_id=1, value=-10)`

New attribute (loot amount multiplier)
- Purpose: multiply non-coin loot item counts by x*(1+y), where `y` is the effect value interpreted as percent/100.
- Server attribute: `UnitAttribute.LootItemCountMul = 187`.
- Semantics: values are VALUE-type and represent percent offset; use `value=100` for +100% (2x items), `value=50` for +50% (1.5x), etc.
- Example (Lucky Quicksilver Tonic): add a row to `unit_modifiers` for its buff id:
  - `unit_modifiers(owner_type='Buff', owner_id=<buff_id>, unit_attribute_id=187, unit_modifier_type_id=0, value=100)`
  - Effect applies to NPC drops and loot from doodads/skills that use loot packs; coins follow gold multipliers, not this effect.

### Production-Time Reduction (cast-time and timers)
- Concept: “Decrease production time” is modeled as cast-time reductions on relevant skills and as reductions on pure timers.
- Tags: Tag id `1157` denotes “Decrease production time”. Skills associated with production/gathering are tagged with `1157` (see `tagged_skills`).
- Skill modifiers: Cast-time reductions live in `skill_modifiers` with `owner_type='Buff'`, `owner_id=<buff_id>`, `skill_attribute_id=4` (CastTime), `unit_modifier_type_id=1` (Percent), negative values to reduce time (e.g., `-10`, `-80`).
- Cast formula (server): `cast_time_ms = unit.CastTimeMul * ApplyModifiers(skill, CastTime, base_cast_ms)` where `ApplyModifiers` aggregates modifiers for the skill id and for its tags (including 1157).
- Pure timers: Some interactions use phase timers (not a skill cast). The server applies tag 1157 reductions to doodad phase timers via `DoodadFuncTimer` as a runtime behavior.

Useful lookups
- Check a production skill is tagged: `SELECT 1 FROM tagged_skills WHERE tag_id=1157 AND skill_id=<SKILL_ID> LIMIT 1;`
- Inspect cast-time modifiers for a buff: `SELECT id, owner_type, owner_id, tag_id, skill_attribute_id, unit_modifier_type_id, value FROM skill_modifiers WHERE owner_type='Buff' AND owner_id=<BUFF_ID> AND skill_attribute_id=4;`
- The common attributes referenced in this domain:
  - UnitAttribute 137 = `LivingPointGainMul` (vocation badges multiplier)
  - SkillAttribute 4 = `CastTime`

## Querying & Inspection
- C# connection (read-only): `using var conn = SQLite.CreateConnection();`
- Tables: `CompactSqliteHelper.GetTables()`
- Columns: `CompactSqliteHelper.GetTableColumns("skills")`
- Row count: `CompactSqliteHelper.GetRowCount("skills")`
- Indices: `CompactSqliteHelper.GetIndices("items")`
- Manual SQL (example): `SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;`

Note on sandboxing/approvals:
- If the environment blocks launching `sqlite3`, request elevated permissions instead of assuming it is unavailable.
- Prefer `-readonly` when querying `compact.sqlite3` (e.g., `sqlite3 -readonly AAEmu.Game/Data/compact.sqlite3 ...`).

### Searching Text (Korean content)
- Names/descriptions for items, skills, quests, and events are often stored in Korean.
- Strategies:
  - Search by numeric IDs or category keys you can cross-reference from code/configs.
  - Use exact column queries, e.g., `SELECT id, name FROM items WHERE name LIKE '%<hangul or keyword>%';`.
  - If you only know an English concept, find the ID in code first, then look up the row in SQLite.

### Inspector CLI (auto graph/summary)
- Generate artifacts without MySQL: `dotnet run --project AAEmu.Game -- inspect-db --out=Docs/DbInspect`
- Outputs:
  - `schema_snapshot.json`: tables, columns, inferred edges
  - `schema_graph.dot`: Graphviz DOT of table relations
  - `README.md`: top tables, frequent *_id columns, sample edges
- Render DOT to PNG (locally): `dot -Tpng Docs/DbInspect/schema_graph.dot -o Docs/DbInspect/schema_graph.png`

### Common Skills/Plots Queries
- Skills with plots: `SELECT COUNT(*) AS total, SUM(plot_id>0) AS with_plot FROM skills;`
- Skills with controllers: `SELECT SUM(skill_controller_id>0), COUNT(*) FROM skills;`
- Plot effect type mix: `SELECT e.actual_type,COUNT(*) FROM plot_effects pe JOIN effects e ON e.id=pe.actual_id GROUP BY e.actual_type ORDER BY COUNT(*) DESC;`
- Distribution by English name (replace X):
  `SELECT COUNT(*), SUM(plot_id>0), SUM(skill_controller_id>0), SUM(projectile_id>0)
   FROM skills s JOIN localized_texts lt ON lt.tbl_name='skills' AND lt.tbl_column_name='name' AND lt.ko=s.name
   WHERE lt.en_us='X';`

## Conventions & Joins
- IDs are integer keys; relationships are implied by column names (e.g., `*_id`, `*_category_id`).
- Booleans are often stored as `NUM` with values `0/1`.
- Join patterns follow names, e.g., `craft_pack_crafts.craft_id -> crafts.id`, `*_effects.*_id -> effects.id`, `*_categories.*_id -> *_categories.id`.

## Interaction Map & Change Planning
- Chain examples:
  - Items → Skills → Effects: items may reference `skill_id`; a skill may fan out to `effects`, `projectiles`, or `buff_*` via mapping tables.
  - Quests → Actions/Checks: quest tables link to `quest_act_*` and `quest_*_checks`, which may reference doodads/NPCs.
  - Doodads → Groups/Bundles/Funcs: doodad families/bundles tie into loot, craft packs, water volumes, etc.
- Before patching, inventory the full impact:
  - Identify all tables/columns referencing your target entity (e.g., any `*_id` pointing to the same domain).
  - Verify indices for join columns; add `CREATE INDEX IF NOT EXISTS` to avoid regressions.
  - Prefer additive changes: new rows, new columns with defaults, new indices; avoid renames/drops.
  - Validate sample joins and counts before/after to ensure semantics remain intact.

## Grepping Effectively (schema & data)
- Prefer the SQLite CLI for reliable results on a binary DB:
  - Tables with a reference: `sqlite3 compact.sqlite3 "SELECT name FROM sqlite_master WHERE type='table' AND sql LIKE '%skill_id%';"`
  - Dump schema: `sqlite3 compact.sqlite3 ".schema" | less`
  - Find indices on a column: `sqlite3 compact.sqlite3 "SELECT name, tbl_name FROM sqlite_master WHERE type='index' AND sql LIKE '%(skill_id)%';"`
  - List candidate FK-like columns: `sqlite3 compact.sqlite3 "SELECT m.name, p.name FROM sqlite_master m JOIN pragma_table_info(m.name) p WHERE p.name LIKE '%_id' ORDER BY m.name;"`
- If `sqlite3` is unavailable, approximate with binary-safe greps (schema only):
  - `LC_ALL=C tr '\0-\11\13-\37' '\n' < compact.sqlite3 | grep -a '^CREATE TABLE'`
  - `strings -a compact.sqlite3 | grep -E 'CREATE TABLE|skill_id|item_id'`
- Reduce noise:
  - Grep table DDL only (from `sqlite_master`) instead of full data.
  - Narrow to domains (e.g., `^CREATE TABLE (items|skills|effects)`), then expand as needed.
  - Use `LIMIT` on exploratory queries and inspect with `SELECT COUNT(1)` for scale.
 - Repo-wide grep hygiene: exclude `.sqlite3` files unless you explicitly intend to search within them to avoid noisy matches from the large binary. Examples:
   - `rg -S <term> -g '!**/*.sqlite3'`
   - `grep -R --binary-files=without-match --exclude='*.sqlite3' <term> .`
   - When you do want to search inside: prefer `sqlite3` queries; falling back to `strings -a compact.sqlite3 | grep <term>` can work for quick checks but may miss multi-byte text.

## Maintenance & Troubleshooting
- Do not edit in place; `compact.sqlite3` is an external asset. Replace the file to update.
- Ensure placement under `.../AAEmu.Game/bin/<Config>/net9.0/Data/compact.sqlite3`.
- On startup, `AAEmu.Game` validates presence; missing file logs a fatal error. Use `CompactSqliteHelper.LogSchemaOverview()` to sanity-check.

Client UI localization caveat
- Server `localized_texts` updates are useful for diagnostics and any server-originated text, but the game client renders UI/tooltips from client packs. Changing item/buff text in server DB does not change client UI strings. Update client assets if you need visible UI changes.

## Patch Workflow (.sql scripts)
- Policy: Do not mutate the DB from runtime. If changes are needed, create standalone `.sql` patch files for a human to apply.
- Where to store: `Docs/sqlite-patches/` (include one file per change; see README in that folder).
- Naming: `YYYYMMDD_short-description.sql` (e.g., `20250807_add-index-icons-name.sql`).
- Authoring guidelines:
  - Wrap in a transaction: `BEGIN TRANSACTION; ... COMMIT;`
  - Make patches idempotent using `IF NOT EXISTS` and existence checks.
  - Prefer additive changes (new tables/columns/indices). Avoid destructive DDL.
  - Always update localization when in-game text mentions affected values. Typical table: `localized_texts(tbl_name, tbl_column_name, idx, ko, en_us, ...)`.
    - Find rows tied to an entity: `SELECT id FROM localized_texts WHERE tbl_name='skills' AND tbl_column_name='desc' AND idx=<SKILL_ID>;`
    - Update both Korean (`ko`) and English (`en_us`) strings for consistency.
    - Safe pattern: `UPDATE localized_texts SET en_us = REPLACE(en_us,'10%','80%') WHERE id IN (...);` and mirror for `ko`.
  - Track application via `schema_migrations` (present in DB): insert a unique version string.
- Apply (by maintainer):
  - Backup the DB file first.
  - Run with SQLite CLI: `sqlite3 path/to/compact.sqlite3 ".read path/to/patch.sql"`
  - Verify with `PRAGMA index_list('<table>');` and sample `SELECT` queries.

### Example: Increase Inventory Cap
- Use the patch in `Docs/sqlite-patches/20250808_increase-inventory-cap.sql` to add steps 8–10 for `bag_expands` (inventory).
- Adjust `item_id` or pricing as desired; defaults to consume 1x Expansion Scroll (8000025).
- Restart `AAEmu.Game` after applying so `CharacterManager` reloads the new rows.