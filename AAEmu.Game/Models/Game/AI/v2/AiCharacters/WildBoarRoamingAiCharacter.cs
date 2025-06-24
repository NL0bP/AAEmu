using AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;
using AAEmu.Game.Models.Game.AI.v2.Behaviors.WildBoar;
using AAEmu.Game.Models.Game.AI.v2.Framework;

namespace AAEmu.Game.Models.Game.AI.v2.AiCharacters;

/// <summary>
/// AI character for wild boars that roam. Builds the state machine and custom combat transition.
/// </summary>
public class WildBoarRoamingAiCharacter : NpcAi
{
    /// <summary>
    /// Builds the state machine for the Wild Boar Roaming AI.
    /// Registers all behaviors and their transitions.
    /// </summary>
    protected override void Build()
    {
        // Spawning state
        AddBehavior(BehaviorKind.Spawning, new SpawningBehavior());

        // Roaming state with transitions
        AddBehavior(BehaviorKind.Roaming, new RoamingBehavior())
            .SetDefaultBehavior()
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.WildBoarAttack)
            .AddTransition(TransitionEvent.OnTalk, BehaviorKind.Talk);

        // RunCommandSet state
        AddBehavior(BehaviorKind.RunCommandSet, new RunCommandSetBehavior())
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.WildBoarAttack)
            .AddTransition(TransitionEvent.OnTalk, BehaviorKind.Talk);

        // Talk state
        AddBehavior(BehaviorKind.Talk, new TalkBehavior())
            .AddTransition(TransitionEvent.OnReturnToTalkPos, BehaviorKind.ReturnState)
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.WildBoarAttack);

        // Alert state
        AddBehavior(BehaviorKind.Alert, new AlertBehavior())
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.WildBoarAttack);

        // WildBoar attack state
        AddBehavior(BehaviorKind.WildBoarAttack, new WildBoarAttackBehavior())
            .AddTransition(TransitionEvent.OnNoAggroTarget, BehaviorKind.ReturnState);

        // FollowPath state
        AddBehavior(BehaviorKind.FollowPath, new FollowPathBehavior())
            .AddTransition(TransitionEvent.OnTalk, BehaviorKind.Talk);

        // FollowUnit state
        AddBehavior(BehaviorKind.FollowUnit, new FollowUnitBehavior())
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.WildBoarAttack)
            .AddTransition(TransitionEvent.OnTalk, BehaviorKind.Talk);

        // Return, Dead, Despawning, and Idle states
        AddBehavior(BehaviorKind.ReturnState, new ReturnStateBehavior());
        AddBehavior(BehaviorKind.Dead, new DeadBehavior());
        AddBehavior(BehaviorKind.Despawning, new DespawningBehavior());
        AddBehavior(BehaviorKind.Idle, new IdleBehavior());
    }

    // Optionally override GoToIdle to use Roaming state as idle
    // public override void GoToIdle()
    // {
    //     SetCurrentBehavior(BehaviorKind.Roaming);
    // }

    /// <summary>
    /// Switches the AI to the WildBoarAttack behavior (combat mode).
    /// </summary>
    public override void GoToCombat()
    {
        SetCurrentBehavior(BehaviorKind.WildBoarAttack);
    }
}
