using AAEmu.Game.Models.Game.AI.v2.Behaviors.Archer;
using AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;
using AAEmu.Game.Models.Game.AI.v2.Framework;

namespace AAEmu.Game.Models.Game.AI.v2.AiCharacters;

/// <summary>
/// AI character for archers that hold their position. Builds the state machine and custom combat transition.
/// </summary>
public class ArcherHoldPositionAiCharacter : NpcAi
{
    /// <summary>
    /// Builds the state machine for the Archer Hold Position AI.
    /// Registers all behaviors and their transitions.
    /// </summary>
    protected override void Build()
    {
        // Spawning state
        AddBehavior(BehaviorKind.Spawning, new SpawningBehavior());

        // HoldPosition state with transitions
        AddBehavior(BehaviorKind.HoldPosition, new HoldPositionBehavior())
            .SetDefaultBehavior()
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.ArcherAttack)
            .AddTransition(TransitionEvent.ReturnToIdlePos, BehaviorKind.ReturnState)
            .AddTransition(TransitionEvent.OnTalk, BehaviorKind.Talk);

        // RunCommandSet state
        AddBehavior(BehaviorKind.RunCommandSet, new RunCommandSetBehavior())
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.ArcherAttack)
            .AddTransition(TransitionEvent.OnTalk, BehaviorKind.Talk);

        // Talk state
        AddBehavior(BehaviorKind.Talk, new TalkBehavior())
            .AddTransition(TransitionEvent.OnReturnToTalkPos, BehaviorKind.ReturnState)
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.ArcherAttack);

        // Alert state
        AddBehavior(BehaviorKind.Alert, new AlertBehavior())
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.ArcherAttack);

        // Archer attack state
        AddBehavior(BehaviorKind.ArcherAttack, new ArcherAttackBehavior())
            .AddTransition(TransitionEvent.OnNoAggroTarget, BehaviorKind.ReturnState);

        // FollowPath state
        AddBehavior(BehaviorKind.FollowPath, new FollowPathBehavior())
            .AddTransition(TransitionEvent.OnTalk, BehaviorKind.Talk);

        // FollowUnit state
        AddBehavior(BehaviorKind.FollowUnit, new FollowUnitBehavior())
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.ArcherAttack)
            .AddTransition(TransitionEvent.OnTalk, BehaviorKind.Talk);

        // Return, Dead, Despawning, and Idle states
        AddBehavior(BehaviorKind.ReturnState, new ReturnStateBehavior());
        AddBehavior(BehaviorKind.Dead, new DeadBehavior());
        AddBehavior(BehaviorKind.Despawning, new DespawningBehavior());
        AddBehavior(BehaviorKind.Idle, new IdleBehavior());
    }

    /// <summary>
    /// Switches the AI to the ArcherAttack behavior (combat mode).
    /// </summary>
    public override void GoToCombat()
    {
        SetCurrentBehavior(BehaviorKind.ArcherAttack);
    }
}
