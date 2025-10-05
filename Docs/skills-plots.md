# Skills, Plots, Controllers — Execution Model and Queries

This guide explains how skills execute in AAEmu, when plots are used, how projectiles and controllers interact, and how to verify behavior quickly via compact.sqlite3. It complements Docs/compact-sqlite.md.

## TL;DR
- Use plots for multi-stage, conditional, timed sequences (cast → travel → impact; conditional CC; cinematics).
- Use the plain effect path for simple, single-tick skills (damage, a buff, a dispel, etc.).
- Movement (leap/dash) is driven by skill controllers, not plots. Projectiles are primarily client visuals.

## Where Things Live (Code)
- Skill load/execute: `AAEmu.Game/Core/Managers/SkillManager.cs`, `AAEmu.Game/Models/Game/Skills/Skill.cs`.
- Plots: `AAEmu.Game/Core/Managers/PlotManager.cs`, `AAEmu.Game/Models/Game/Skills/Plots/*` (PlotTree, nodes, packets).
- Controllers (movement): `AAEmu.Game/Models/Game/Skills/SkillControllers/*` (`SkillControllerKind`, `LeapSkillController`).
- Effects: `AAEmu.Game/Models/Game/Skills/Effects/*`, `SpecialEffectType.cs`.

## Execution Paths
- Effect-driven (no plot):
  - Skill applies `Template.Effects` directly via `Skill.ApplyEffects()` with per-effect gating (relation, front/back, tags, buffs, chance, level range, etc.).
  - Packets: `SCSkillStartedPacket` (if casting), then `SCSkillFiredPacket` and eventual `SCSkillEndedPacket`.
- Plot-driven (plot_id > 0):
  - `PlotManager` loads `plots`, `plot_events`, `plot_effects`, `plot_next_events`, `plot_conditions`, `plot_event_conditions`, `plot_aoe_conditions` and builds `PlotTree`.
  - Plots sequence effects, compute delays (incl. projectile travel), and branch conditionally. Packets: `SCPlotEventPacket` emitted during plot execution.
- Controllers (movement):
  - `skills.skill_controller_id` → `SkillControllerTemplate` → `SkillControllerKind` (e.g., Leap) executed for movement; independent of plots.

## Projectiles
- DB (`projectiles`) contains FX/bone hookups. Server does not simulate flight/collision; the visual is client-led.
- The server treats `SpecialEffect.Projectile` as a no-op (logging only) to avoid duplicate simulation.
- If timing at impact matters, plots compute delays (see `PlotNextEvent.GetProjectileDelay`) and apply effects at the hit moment.

## Cast Time Modifiers (Production Time)
- Cast time calculation: `castTimeMs = unit.CastTimeMul * ApplyModifiers(skill, SkillAttribute.CastTime, baseCastMs)`.
- `ApplyModifiers` aggregates modifiers:
  - Skill-specific (`skill_modifiers.owner_type='Skill' AND owner_id=<skill_id>`)
  - Tag-based (`skill_modifiers.owner_type` tied to tag mappings). Production-time uses tag id `1157` (“Decrease production time”).
- Production-time reductions are represented as `skill_modifiers` with `skill_attribute_id=4` (CastTime), `unit_modifier_type_id=1` (Percent), negative values to reduce time (e.g., `-10`, `-80`).
- Some interactions are pure timers (not skills). Those phase timers are reduced by the same tag via `DoodadFuncTimer` at runtime.

Inspection helpers (sqlite3):
- Confirm a skill is production-tagged: `SELECT 1 FROM tagged_skills WHERE tag_id=1157 AND skill_id=<SKILL_ID> LIMIT 1;`
- View modifiers for a buff: `SELECT id, owner_type, owner_id, tag_id, skill_attribute_id, unit_modifier_type_id, value FROM skill_modifiers WHERE owner_type='Buff' AND owner_id=<BUFF_ID> AND skill_attribute_id=4;`

## Crowd Control Semantics
- CC flags are defined on `buffs` (e.g., `stun`, `root`, `sleep`, `knock_down`). Skills apply them via `BuffEffect` from `skill_effects`.
- Controllers abort if unit becomes stunned/rooted/etc. (`LeapSkillController` checks).

## Quick Verification Queries (sqlite3)

Note: Many human-readable names are in Korean. Use `localized_texts` to find English labels. Prefer read-only mode: `sqlite3 -readonly path/to/compact.sqlite3 "..."`.

- Global counts:
  - Skills with plots: `SELECT COUNT(*) AS total, SUM(plot_id>0) AS with_plot FROM skills;`
  - Plot events: `SELECT COUNT(*) FROM plot_events;`
  - Skills with controllers: `SELECT SUM(skill_controller_id>0), COUNT(*) FROM skills;`

- Distribution by English name (replace X):
  - Overwhelm: `SELECT COUNT(*), SUM(plot_id>0), SUM(skill_controller_id>0), SUM(projectile_id>0)
                 FROM skills s JOIN localized_texts lt ON lt.tbl_name='skills' AND lt.tbl_column_name='name' AND lt.ko=s.name
                 WHERE lt.en_us='Overwhelm';`
  - Freezing Arrow: same with `'Freezing Arrow'`.
  - Earthen Grip: same with `'Earthen Grip'`.
  - Wallop: same with `'Wallop'`.

- Plot structure size for a given plot:
  - `SELECT COUNT(*) FROM plot_events WHERE plot_id=<PLOT_ID>;`
  - `SELECT COUNT(*) FROM plot_effects WHERE event_id IN (SELECT id FROM plot_events WHERE plot_id=<PLOT_ID>);`
  - `SELECT COUNT(*) FROM plot_next_events WHERE event_id IN (SELECT id FROM plot_events WHERE plot_id=<PLOT_ID>);`

- Effect stack for a specific skill id:
  - Core skill row: `SELECT id,name,ability_id,cooldown_time,casting_time,min_range,max_range,plot_id,skill_controller_id,projectile_id FROM skills WHERE id=<SKILL_ID>;`
  - Linked effects: `SELECT se.effect_id,e.actual_type,e.actual_id FROM skill_effects se JOIN effects e ON e.id=se.effect_id WHERE se.skill_id=<SKILL_ID> ORDER BY se.weight;`
  - Inspect effect rows, e.g., `damage_effects`, `buff_effects` (+ join to `buffs`), `special_effects`, `dispel_effects`.

## Worked Examples

Below are representative examples. IDs can differ across content versions; use `localized_texts` to find your target rows.

- Overwhelm (e.g., skills.id=10648)
  - Signals: `plot_id=625`, `skill_controller_id=8231` (Leap), no projectile.
  - Plot shape: 15 plot_events, 23 plot_effects, 19 plot_next_events.
  - Effects: Damage (level-scaled melee), multiple stun/knockdown buffs, knockback special effect, and a dispel (cure) by tag.
  - Why plot: orchestrates leap + CC + optional conditionals and timing.

- Freezing Arrow (e.g., skills.id=10667)
  - Signals: `projectile_id=3`, `plot_id=278`, cast time ~2000 ms.
  - Plot shape: 16 plot_events, 30 plot_effects, 31 plot_next_events.
  - Effects: Damage → chill/slow buff → damage → freeze (stun) buff; plot delays at projectile impact time.
  - Why plot: needs impact-timed sequencing and conditional CC chaining.

- Earthen Grip (e.g., skills.id=10652)
  - Signals: `projectile_id=138`, no plot, no controller; cast ~1000 ms.
  - Effects: Multiple root buffs (different durations), optional dispel; applied immediately at cast completion.
  - Why no plot: single-target, unconditional CC; projectile is visual only.

- Wallop (e.g., skills.id=12029)
  - Signals: no plot, no controller, no projectile.
  - Effects: Damage (+ level scaling), a SpecialEffect to “call” sub-skill, and Charge stacks; simple one-tick behavior.

## Gotchas & Tips
- Localization: English names live in `localized_texts`; Korean names are in the base tables. Always join by KO to find EN rows.
- Read-only access: Prefer `-readonly` to avoid locks and seccomp issues in automated environments.
- Plot vs projectile: Having a projectile does not imply plots. Plots are for timing/branching; projectile-only skills usually apply effects immediately.

## Future Improvements (Notes)
- Precompute `PlotEventTemplate.HasSpecialEffects()` post-load to reduce load-order coupling between PlotManager and SkillManager.
- Normalize controller abort guards across controller kinds (stun/root/knockdown/fastened checks).
- Add integration checks for: (a) projectile+plot delays impact, (b) projectile-only applies immediately, (c) controller aborts on CC.