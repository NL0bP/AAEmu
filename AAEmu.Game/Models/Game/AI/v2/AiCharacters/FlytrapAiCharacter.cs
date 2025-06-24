using AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;
using AAEmu.Game.Models.Game.AI.v2.Behaviors.Flytrap;
using AAEmu.Game.Models.Game.AI.v2.Framework;

namespace AAEmu.Game.Models.Game.AI.v2.AiCharacters;

/// <summary>
/// AI character for flytrap NPCs. Builds the state machine and custom alert/combat transitions.
/// </summary>
public class FlytrapAiCharacter : NpcAi
{
    /// <summary>
    /// Builds the state machine for the Flytrap AI.
    /// Registers all behaviors and their transitions.
    /// </summary>
    protected override void Build()
    {
        // Spawning state
        AddBehavior(BehaviorKind.Spawning, new SpawningBehavior());

        // HoldPosition state with transitions
        AddBehavior(BehaviorKind.HoldPosition, new HoldPositionBehavior())
            .SetDefaultBehavior()
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.FlytrapAttack)
            .AddTransition(TransitionEvent.ReturnToIdlePos, BehaviorKind.ReturnState)
            .AddTransition(TransitionEvent.OnTalk, BehaviorKind.Talk);

        // RunCommandSet state
        AddBehavior(BehaviorKind.RunCommandSet, new RunCommandSetBehavior())
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.FlytrapAttack)
            .AddTransition(TransitionEvent.OnTalk, BehaviorKind.Talk);

        // Talk state
        AddBehavior(BehaviorKind.Talk, new TalkBehavior())
            .AddTransition(TransitionEvent.OnReturnToTalkPos, BehaviorKind.ReturnState)
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.FlytrapAttack);

        // Flytrap alert state
        AddBehavior(BehaviorKind.FlytrapAlert, new FlytrapAlertBehavior())
            .AddTransition(TransitionEvent.OnAggroTargetChanged, BehaviorKind.FlytrapAttack);

        // Flytrap attack state
        AddBehavior(BehaviorKind.FlytrapAttack, new FlytrapAttackBehavior());

        // Return, Dead, Despawning, and Idle states
        AddBehavior(BehaviorKind.ReturnState, new ReturnStateBehavior());
        AddBehavior(BehaviorKind.Dead, new DeadBehavior());
        AddBehavior(BehaviorKind.Despawning, new DespawningBehavior());
        AddBehavior(BehaviorKind.Idle, new IdleBehavior());
    }

    /// <summary>
    /// Switches the AI to the FlytrapAlert behavior (alert mode).
    /// </summary>
    public override void GoToAlert()
    {
        SetCurrentBehavior(BehaviorKind.FlytrapAlert);
    }

    /// <summary>
    /// Switches the AI to the FlytrapAttack behavior (combat mode).
    /// </summary>
    public override void GoToCombat()
    {
        SetCurrentBehavior(BehaviorKind.FlytrapAttack);
    }
}
