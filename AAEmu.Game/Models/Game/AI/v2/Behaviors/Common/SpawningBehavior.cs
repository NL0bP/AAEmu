using System;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.AI.v2.Params.Almighty;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

/// <summary>
/// Handles the spawning state for an NPC. Triggers spawn events, applies spawn skills, and transitions to the next state.
/// </summary>
public class SpawningBehavior : BaseCombatBehavior
{
    private bool _usedSpawnSkills;
    private bool _isInitialized;

    public override void Enter()
    {
        if (!ValidateEnterState())
            return;

        InitializeSpawningState();
        _isInitialized = true;
        //Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered spawning state");
    }

    private bool ValidateEnterState()
    {
        if (Ai?.Owner == null)
        {
            Logger.Warn($"SpawningBehavior.Enter: Ai or Owner is null");
            return false;
        }
        return true;
    }

    private void InitializeSpawningState()
    {
        // Set initial stance and alertness
        Ai.Owner.CurrentGameStance = GameStanceType.Relaxed;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Idle;

        // Trigger spawn event
        if (Ai.Owner is { } npc)
        {
            npc.Events.OnSpawn(this, new OnSpawnArgs { Npc = npc });
        }

        // Check for immediate aggression if configured
        var aiParams = Ai.Owner.Template.AiParams as AlmightyNpcAiParams;
        if (aiParams != null && aiParams.AlertToAttack && aiParams.AlertDuration == 0)
        {
            CheckAggression();
        }
        _usedSpawnSkills = false;
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;

        // Use spawn skills if available and not already used
        TryUseSpawnSkills();

        // Transition to the next state after spawn logic
        Ai.GoToRunCommandSet();
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
        {
            Logger.Warn($"SpawningBehavior.Tick called before initialization for unit {Ai?.Owner?.ObjId}");
            return false;
        }
        if (Ai?.Owner == null)
        {
            Logger.Warn($"SpawningBehavior.Tick called with null Ai or Owner");
            return false;
        }
        return true;
    }

    private void TryUseSpawnSkills()
    {
        if (_usedSpawnSkills)
            return;

        if (Ai.Owner.Template.Skills.TryGetValue(SkillUseConditionKind.OnSpawn, out var skills))
        {
            _usedSpawnSkills = true;
            foreach (var npcSkill in skills)
            {
                var skillTemplate = SkillManager.Instance.GetSkillTemplate(npcSkill.SkillId);
                if (skillTemplate == null)
                    continue;

                var skill = new Skill(skillTemplate);
                var skillCaster = SkillCaster.GetByType(SkillCasterType.Unit);
                skillCaster.ObjId = Ai.Owner.ObjId;
                var skillTarget = SkillCastTarget.GetByType(SkillCastTargetType.Unit);
                skillTarget.ObjId = Ai.Owner.ObjId;
                skill.Use(Ai.Owner, skillCaster, skillTarget, null, true, out _);
            }
        }
    }

    public override void Exit()
    {
        _isInitialized = false;
        _usedSpawnSkills = false;
    }
}
