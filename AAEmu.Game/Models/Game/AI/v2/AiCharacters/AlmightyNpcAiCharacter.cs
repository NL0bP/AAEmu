using AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;
using AAEmu.Game.Models.Game.AI.v2.Framework;

namespace AAEmu.Game.Models.Game.AI.v2.AiCharacters;

/// <summary>
/// AI character for 'Almighty' NPCs. Handles state machine construction and custom combat transition.
/// </summary>
public class AlmightyNpcAiCharacter : NpcAi
{
    /// <summary>
    /// Builds the state machine for the Almighty NPC AI.
    /// Registers all behaviors and their transitions.
    /// </summary>
    protected override void Build()
    {
        // Spawning state
        AddBehavior(BehaviorKind.Spawning, new SpawningBehavior());

        // Idle state with transitions
        AddBehavior(BehaviorKind.Idle, new IdleBehavior())
            .SetDefaultBehavior()
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.AlmightyAttack)
            .AddTransition(TransitionEvent.ReturnToIdlePos, BehaviorKind.ReturnState)
            .AddTransition(TransitionEvent.OnTalk, BehaviorKind.Talk);

        // RunCommandSet state
        AddBehavior(BehaviorKind.RunCommandSet, new RunCommandSetBehavior())
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.AlmightyAttack)
            .AddTransition(TransitionEvent.OnTalk, BehaviorKind.Talk);

        // Talk state
        AddBehavior(BehaviorKind.Talk, new TalkBehavior())
            .AddTransition(TransitionEvent.OnReturnToTalkPos, BehaviorKind.ReturnState)
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.AlmightyAttack);

        // Alert state
        AddBehavior(BehaviorKind.Alert, new AlertBehavior())
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.AlmightyAttack);

        // Almighty attack state
        AddBehavior(BehaviorKind.AlmightyAttack, new AlmightyAttackBehavior())
            .AddTransition(TransitionEvent.OnNoAggroTarget, BehaviorKind.ReturnState);

        // FollowPath state
        AddBehavior(BehaviorKind.FollowPath, new FollowPathBehavior())
            .AddTransition(TransitionEvent.OnTalk, BehaviorKind.Talk);

        // FollowUnit state
        AddBehavior(BehaviorKind.FollowUnit, new FollowUnitBehavior())
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.AlmightyAttack)
            .AddTransition(TransitionEvent.OnTalk, BehaviorKind.Talk);

        // Return, Dead, and Despawning states
        AddBehavior(BehaviorKind.ReturnState, new ReturnStateBehavior());
        AddBehavior(BehaviorKind.Dead, new DeadBehavior());
        AddBehavior(BehaviorKind.Despawning, new DespawningBehavior());
    }

    /// <summary>
    /// Switches the AI to the AlmightyAttack behavior (combat mode).
    /// </summary>
    public override void GoToCombat()
    {
        SetCurrentBehavior(BehaviorKind.AlmightyAttack);
    }
}
