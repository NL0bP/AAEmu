using AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;
using AAEmu.Game.Models.Game.AI.v2.Framework;

namespace AAEmu.Game.Models.Game.AI.v2.AiCharacters;

/// <summary>
/// AI character for dummy (inactive or placeholder) NPCs. Only supports basic states.
/// </summary>
public class DummyAiCharacter : NpcAi
{
    /// <summary>
    /// Builds the state machine for the Dummy AI.
    /// Registers only minimal behaviors.
    /// </summary>
    protected override void Build()
    {
        // Spawning state
        AddBehavior(BehaviorKind.Spawning, new SpawningBehavior());
        // Dummy state (default)
        AddBehavior(BehaviorKind.Dummy, new DummyBehavior()).SetDefaultBehavior();
        // Dead and Despawning states
        AddBehavior(BehaviorKind.Dead, new DeadBehavior());
        AddBehavior(BehaviorKind.Despawning, new DespawningBehavior());
    }

    /// <summary>
    /// Switches the AI to the Dummy behavior (idle for dummy NPCs).
    /// </summary>
    public override void GoToIdle()
    {
        SetCurrentBehavior(BehaviorKind.Dummy);
    }

    /// <summary>
    /// Switches the AI to the Dummy behavior when running command set (dummy NPCs do not process commands).
    /// </summary>
    public override void GoToRunCommandSet()
    {
        SetCurrentBehavior(BehaviorKind.Dummy);
    }
}
