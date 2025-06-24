using System;
using System.Numerics;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.AI.Enums;
using AAEmu.Game.Models.Game.AI.v2.Controls;
using AAEmu.Game.Models.Game.AI.v2.Params;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

/// <summary>
/// Handles the execution of AI command sets for NPCs.
/// Processes commands like following paths, using skills, and managing timeouts.
/// </summary>
public class RunCommandSetBehavior : BaseCombatBehavior
{
    private const float MinimumTickInterval = 0.1f; // 100ms between ticks
    private DateTime _lastTick;
    private bool _isInitialized;

    public override void Enter()
    {
        if (!ValidateEnterState())
            return;

        InitializeCommandState();
        _isInitialized = true;
        //Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered command set execution state");
    }

    private bool ValidateEnterState()
    {
        if (Ai?.Owner == null)
        {
            Logger.Warn($"RunCommandSetBehavior.Enter: Ai or Owner is null");
            return false;
        }
        return true;
    }

    private void InitializeCommandState()
    {
        Ai.Owner.CurrentGameStance = GameStanceType.Combat;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Combat;
        _lastTick = DateTime.UtcNow;
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;

        if (!ThrottleTick())
            return;

        ProcessCommandExecution(delta);
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
        {
            Logger.Warn($"RunCommandSetBehavior.Tick called before initialization for unit {Ai?.Owner?.ObjId}");
            return false;
        }

        if (Ai?.Owner == null)
        {
            Logger.Warn($"RunCommandSetBehavior.Tick called with null Ai or Owner");
            return false;
        }

        return true;
    }

    private bool ThrottleTick()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastTick).TotalSeconds < MinimumTickInterval)
            return false;

        _lastTick = now;
        return true;
    }

    private void ProcessCommandExecution(TimeSpan delta)
    {
        // Check if we're still waiting for current command to complete
        if (Ai.AiCurrentCommandRunTime > TimeSpan.Zero)
        {
            Ai.AiCurrentCommandRunTime -= delta;
            return;
        }

        // Process next command if available
        if (Ai.AiCurrentCommand != null || Ai.AiCommandsQueue.Count > 0)
        {
            ProcessNextCommand(delta);
            return;
        }

        // No more commands to execute
        //Logger.Debug($"Unit {Ai.Owner.ObjId} completed command set execution");
        Ai.GoToIdle();
    }

    private void ProcessNextCommand(TimeSpan delta)
    {
        // Get next command if needed
        if (Ai.AiCurrentCommand == null)
        {
            Ai.AiCurrentCommand = Ai.AiCommandsQueue.Dequeue();
            Ai.AiCurrentCommandStartTime = DateTime.UtcNow;
            //Logger.Debug($"Unit {Ai.Owner.ObjId} starting new command: {Ai.AiCurrentCommand.CmdId}");
        }

        // Process current command
        ExecuteCurrentCommand(Ai.AiCurrentCommand, delta);
    }

    private void ExecuteCurrentCommand(AiCommands aiCommand, TimeSpan delta)
    {
        // Check if command execution is complete
        if (Ai.AiCurrentCommandRunTime < TimeSpan.Zero)
        {
            CompleteCurrentCommand();
            return;
        }

        // Check if still waiting
        if (Ai.AiCurrentCommandRunTime > TimeSpan.Zero)
        {
            Ai.AiCurrentCommandRunTime -= delta;
            return;
        }

        Logger.Debug($"Unit {Ai.Owner.ObjId} ({Ai.Owner.TemplateId}) executing command: {aiCommand.CmdId}, Set: {aiCommand.CmdSetId}, P1: {aiCommand.Param1}, P2: {aiCommand.Param2}");

        try
        {
            ExecuteCommandAction(aiCommand);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error executing AI command {aiCommand.CmdId} for unit {Ai.Owner.ObjId}: {ex.Message}");
            CompleteCurrentCommand();
        }
    }

    private void ExecuteCommandAction(AiCommands aiCommand)
    {
        switch (aiCommand.CmdId)
        {
            case AiCommandCategory.FollowUnit:
                HandleFollowUnitCommand(aiCommand);
                break;
            case AiCommandCategory.FollowPath:
                HandleFollowPathCommand(aiCommand);
                break;
            case AiCommandCategory.UseSkill:
                HandleUseSkillCommand(aiCommand);
                break;
            case AiCommandCategory.Timeout:
                HandleTimeoutCommand(aiCommand);
                break;
            default:
                throw new NotSupportedException($"Command type {aiCommand.CmdId} is not supported");
        }
    }

    private void HandleFollowUnitCommand(AiCommands aiCommand)
    {
        Logger.Warn($"FollowUnit command not implemented for unit {Ai.Owner.ObjId}, CommandSet {aiCommand.CmdSetId}, P1 {aiCommand.Param1}, P2 {aiCommand.Param2}");
        CompleteCurrentCommand();
    }

    private void HandleFollowPathCommand(AiCommands aiCommand)
    {
        bool appendToQueue = aiCommand.Param1 == 1;
        Ai.LoadAiPathPoints(Ai.AiFileName, appendToQueue);

        if (appendToQueue)
        {
            // Add return command at the end of path
            Ai.PathHandler.AiPathPointsRemaining.Enqueue(new AiPathPoint
            {
                Position = Vector3.Zero,
                Action = AiPathPointAction.ReturnToCommandSet,
                Param = string.Empty
            });
            Ai.AiFileName = aiCommand.Param2;
        }
        else
        {
            Ai.AiFileName2 = aiCommand.Param2;
        }

        Ai.GoToFollowPath();
    }

    private void HandleUseSkillCommand(AiCommands aiCommand)
    {
        Ai.AiSkillId = aiCommand.Param1;
        var skillTemplate = SkillManager.Instance.GetSkillTemplate(Ai.AiSkillId);

        if (skillTemplate == null)
        {
            Logger.Warn($"Invalid skill template {Ai.AiSkillId} for unit {Ai.Owner.ObjId}");
            CompleteCurrentCommand();
            return;
        }

        var target = Ai.Owner.CurrentTarget as Unit ?? Ai.Owner;
        if (Ai.Owner.UseSkill(Ai.AiSkillId, target) == SkillResult.Success)
        {
            var cooldown = SkillManager.GetAttackDelay(skillTemplate, Ai.Owner, false, 0.0);
            Ai.AiCurrentCommandRunTime = TimeSpan.FromMilliseconds(cooldown);
        }
        else
        {
            CompleteCurrentCommand();
        }
    }

    private void HandleTimeoutCommand(AiCommands aiCommand)
    {
        Ai.AiTimeOut = aiCommand.Param1;
        Ai.AiCurrentCommandRunTime = TimeSpan.FromMilliseconds(Ai.AiTimeOut);
    }

    private void CompleteCurrentCommand()
    {
        Ai.AiCurrentCommand = null;
        Ai.AiCurrentCommandRunTime = TimeSpan.Zero;
    }

    public override void Exit()
    {
        if (!_isInitialized)
            return;

        //Logger.Debug($"Unit {Ai.Owner?.ObjId}:{Ai.Owner?.TemplateId} exiting command set execution state");
        _isInitialized = false;
    }
}
