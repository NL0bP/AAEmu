using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Commons.Network;
using AAEmu.Commons.Utils;
using AAEmu.Commons.Utils.DB;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.CommonFarm.Static;
using AAEmu.Game.Models.Game.DoodadObj.Funcs;
using AAEmu.Game.Models.Game.DoodadObj.Static;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Tasks.Doodads;

using MySql.Data.MySqlClient;

/*
 *-----------------------------------------------------------------------------------------------------------------
 *                        How doodad works
 *-----------------------------------------------------------------------------------------------------------------
 [Doodad] Chain: TemplateId 2336 (water the flowerbed)
 [Doodad] FuncGroupId : 4651 - start
 [Doodad] PhaseFunc: GroupId 4651, FuncId 250, FuncType DoodadFuncTod : NextPhase 5136, tod 2000
 [Doodad] Func: GroupId 4651, FuncId 543, FuncType DoodadFuncFakeUse, NextPhase 4652, Skill 0

 [Doodad] FuncGroupId : 4652 - normal
 [Doodad] PhaseFunc: GroupId 4652, FuncId 822, FuncType DoodadFuncTimer : delay=30000, nextPhase=4651
 [Doodad] Func: GroupId 4652, FuncId 0

 [Doodad] FuncGroupId : 5136 - normal
 [Doodad] PhaseFunc: GroupId 5136, FuncId 251, FuncType DoodadFuncTod : NextPhase 4651, tod 600
 [Doodad] Func: GroupId 5136, FuncId 775, FuncType DoodadFuncFakeUse, NextPhase 5137, Skill 0

 [Doodad] FuncGroupId : 5137 - normal
 [Doodad] PhaseFunc: GroupId 5137, FuncId 1001, FuncType DoodadFuncTimer : delay=30000, nextPhase=5136
 [Doodad] Func: GroupId 5137, FuncId 0
*-----------------------------------------------------------------------------------------------------------------
method public void Use(BaseUnit caster, uint skillId) runs in a loop:

2. start Func(functions) func.Use(caster, this, skillId, func.NextPhase)
   - one function is selected by the GetFunc(FuncGroupId, skillId) method
   such as DoodadFuncUse, DoodadFuncFakeUse, DoodadFuncLootItem, etc.
2.1. function launch
2.2. checking NextPhase and the presence of the function
2.2.1. no function - exit (we stop at this phase to wait for interaction)
2.2.2. NextPhase = 0 or -1 - exit,
2.2.3. goto 2.3., or transition to the next phase execution
2.3. transition to execution of NextPhase (repetition in an infinite loop, transition to step 1.)

1. start PhaseFunc (phase functions) - phaseFunc.Use(caster, this) - returns the result on interrupt and phase change
   with check for phase change depending on the time of day - DoodadFuncTod,
   check for quest - DoodadFuncRequireQuest, execution can be flagged.
   check - DoodadFuncRatioChange, phase change depending on the percentage hit.
   timer start - DoodadFuncTimer, with transition to execution
   doodad respawn - DoodadFuncFinal
   plant growth - DoodadFuncGrowth, etc.
   - GetPhaseFunc(FuncGroupId) - returns the list of phase functions
1.1. phase change
1.2. validity check - immediate loop termination
1.3. timers waiting for subsequent execution from the new phase
1.4. there may be no phase function (stop at this phase to wait for interaction), or there may be several (execute in a loop)

*-----------------------------------------------------------------------------------------------------------------
 */
namespace AAEmu.Game.Models.Game.DoodadObj;

public class Doodad : BaseUnit
{
    private float _scale;
    public byte Flag { get; set; }
    private int _data;
    private uint _funcGroupId;

    //public uint TemplateId { get; set; } // moved to BaseUnit
    public uint DbId { get; set; }
    public bool IsPersistent { get; set; }
    public DoodadTemplate Template { get; set; }
    public override float Scale => _scale;

    public DoodadFuncPermission FuncPermission
    {
        get
        {
            foreach (var currentFunc in CurrentFuncs)
            {
                return (DoodadFuncPermission)currentFunc.PermId;
            }

            return DoodadFuncPermission.Any;
        }
    }

    public uint FuncGroupId
    {
        get => _funcGroupId;
        set
        {
            if (value != _funcGroupId)
            {
                _funcGroupId = value;
                PhaseTime = DateTime.UtcNow; // Save PhaseTime at start of new phase (group)
                if (IsPersistent)
                {
                    Save();
                }

                CurrentFuncs = DoodadManager.Instance.GetFuncsForGroup(_funcGroupId);
                CurrentPhaseFuncs = DoodadManager.Instance.GetPhaseFunc(_funcGroupId);

                // Register new ToD triggers (if any)
                CurrentToDTriggers.Clear();
                foreach (var currentPhaseFunc in CurrentPhaseFuncs)
                {
                    if (currentPhaseFunc.FuncType != "DoodadFuncTod")
                        continue;
                    var todPhaseFunc = DoodadManager.Instance.GetPhaseFuncTemplate(currentPhaseFunc.FuncId, currentPhaseFunc.FuncType);
                    if (todPhaseFunc is not DoodadFuncTod doodadFuncTod)
                    {
                        Logger.Error($"DoodadFuncTod is not a DoodadFuncTod");
                        continue;
                    }

                    CurrentToDTriggers.TryAdd(doodadFuncTod.TodAsHours, doodadFuncTod.NextPhase);
                }
            }
        }
    }

    // public string FuncType { get; set; }
    public ulong ItemId { get; set; }
    public ulong UccId { get; set; }
    public uint ItemTemplateId { get; set; }
    public DateTime GrowthTime { get; set; }
    public DateTime PlantTime { get; set; }
    public DateTime PhaseTime { get; set; }
    public uint OwnerId { get; set; }
    public uint OwnerObjId { get; set; }
    public uint ParentObjId { get; set; }
    public DoodadOwnerType OwnerType { get; set; }
    public AttachPointKind AttachPoint { get; set; }
    public uint OwnerDbId { get; set; }
    public uint Type2 { get; set; } = 0;

    public int Data
    {
        get => _data;
        set
        {
            if (value != _data)
            {
                _data = value;
                if (IsPersistent)
                {
                    Save();
                }
            }
        }
    }
    public FarmType FarmType { get; set; }
    public uint QuestGlow { get; set; } //0 off // 1 on
    public int PuzzleGroup { get; set; } = -1; // -1 off
    public DoodadSpawner Spawner { get; set; }
    public DoodadFuncTask FuncTask { get; set; }
    public DateTime FreshnessTime { get; set; }
    //public bool IsGoods { get; set; } // указываем, что это бэкпак региональных товаров
    public List<DoodadFunc> CurrentFuncs { get; set; }
    public List<DoodadPhaseFunc> CurrentPhaseFuncs { get; set; }
    /// <summary>
    /// ToD, next_phase
    /// </summary>
    public Dictionary<float, int> CurrentToDTriggers { get; set; }

    /// <summary>
    /// Time left to show on Doodads in milliseconds
    /// </summary>
    public uint TimeLeft
    {
        get
        {
            // This probably needs a better way to calculate, like a separate field to store the end-time
            foreach (var func in CurrentPhaseFuncs)
            {
                var template = DoodadManager.Instance.GetPhaseFuncTemplate(func.FuncId, func.FuncType);
                if (template is DoodadFuncFinal doodadFuncRecoverItemTemplate)
                {
                    if (doodadFuncRecoverItemTemplate.After > 0)
                    {
                        var left = (PhaseTime + TimeSpan.FromMilliseconds(doodadFuncRecoverItemTemplate.After) -
                                    DateTime.UtcNow).TotalMilliseconds;
                        return (uint)Math.Round(Math.Max(1, left));
                    }
                }
            }

            if (GrowthTime > DateTime.UtcNow)
            {
                return (uint)(GrowthTime - DateTime.UtcNow).TotalMilliseconds;
            }

            return 0;
        }
    }

    public bool ToNextPhase { get; set; }
    public int PhaseRatio { get; set; }
    public int CumulativePhaseRatio { get; set; }

    /// <summary>
    /// Used to indicate the starting phase of the doodad should be overriden when loading player doodads
    /// </summary>
    public int OverridePhase { get; set; }

    /// <summary>
    /// Used to indicate that the phase starting time should be overriden on timing related funcs
    /// </summary>
    public DateTime OverridePhaseTime { get; set; } = DateTime.MinValue;

    private bool _deleted = false;
    public VehicleSeat Seat { get; set; }
    private List<uint> ListGroupId { get; set; }
    public List<AreaTrigger> AttachAreaTriggers { get; set; } = [];
    public bool IsRespawnScheduled { get; set; } = false;

    public Doodad()
    {
        _scale = 1f;
        PlantTime = DateTime.MinValue;
        AttachPoint = AttachPointKind.System;
        Seat = new VehicleSeat(this);
        ListGroupId = [];
        CurrentFuncs = [];
        CurrentPhaseFuncs = [];
        CurrentToDTriggers = new Dictionary<float, int>();
        IsRespawnScheduled = false;
    }

    public void SetScale(float scale)
    {
        _scale = scale;
    }

    /* Unused
    private bool CheckPhase(uint anotherPhase)
    {
        return ListGroupId.Any(phase => phase == anotherPhase);
    }

    private bool CheckFunc(uint anotherPhase)
    {
        return ListFuncGroupId.Any(phase => phase == anotherPhase);
    }*/

    /*
     * 1. Создание (посадка) Doodad запускает на стартовой фазе PhaseFunc;
     * 2. Ждем взаимодействия с Doodad;
     * 3. Непосредствено взаимодействие начинается с выполнения Func с учётом SkillId;
     * 4. Далее на следующей фазе начинаем выполнение с фазовых функций, а затем сами функции, если перед этим прошли проверки в фазовых функциях;
     *
     * 1. Creation (landing) Doodad launches on the PhaseFunc start phase;
     * 2. Looking forward to interacting with Doodad;
     * 3. Direct interaction starts with execution of a Func, taking into account the SkillId;
     * 4. Then in the next phase we start execution with the phase functions and then the functions themselves, if the checks in the phase functions have been passed before;
     */
    public void SetData(int data)
    {
        _data = data;
    }

    /// <summary>
    /// Uses the given Doodad with the specified caster and skill.
    /// </summary>
    /// <param name="caster">The unit using the Doodad.</param>
    /// <param name="startedSkillId">The skill ID used to interact with the Doodad.</param>
    /// <param name="funcGroupId">The function group ID used to interact with the Doodad.</param>
    public void Use(BaseUnit caster, uint startedSkillId = 0, int funcGroupId = 0)
    {
        if (caster == null)
            return;

        if (funcGroupId > 0)
            FuncGroupId = (uint)funcGroupId;

        var skillId = startedSkillId;

        while (true)
        {
            LogUse(caster, skillId);

            ToNextPhase = false; // by default, do not execute the next phase
            ListGroupId.Clear();

            // First, find the functions, then execute them
            var funcWithSkill = DoodadManager.Instance.GetFunc(FuncGroupId, skillId);
            var allFuncsForGroup = DoodadManager.Instance.GetFuncsForGroup(FuncGroupId);

            if (allFuncsForGroup.Count <= 0)
            {
                // Phase has no functions
                return;
            }

            if (skillId == 0)
            {
                // Execute functions without skill
                if (ExecuteFuncsWithoutSkill(caster, startedSkillId, allFuncsForGroup))
                    return;
            }
            else
            {
                // Execute function with skill
                if (ExecuteFuncWithSkill(caster, startedSkillId, funcWithSkill))
                    return;
            }

            // Then execute the phase functions (FuncGroupId may change to a different one)
            if (DoChangePhase(caster, (int)FuncGroupId) || !ToNextPhase)
            {
                // Did not pass the quest conditions check or there is no phase function
                LogPhaseCheckFailure(caster, skillId);
                return;
            }

            skillId = 0;
        }
    }

    /// <summary>
    /// Logs the use of the Doodad.
    /// </summary>
    /// <param name="caster">The unit using the Doodad.</param>
    /// <param name="skillId">The skill ID used to interact with the Doodad.</param>
    private void LogUse(BaseUnit caster, uint skillId)
    {
        if (caster is Character)
            Logger.Warn($"Use: TemplateId {TemplateId}, Using phase {FuncGroupId} with SkillId {skillId}");
        else
            Logger.Trace($"Use: TemplateId {TemplateId}, Using phase {FuncGroupId} with SkillId {skillId}");
    }

    /// <summary>
    /// Executes functions without skill.
    /// </summary>
    /// <param name="caster">The unit using the Doodad.</param>
    /// <param name="startedSkillId">The skill ID used to interact with the Doodad.</param>
    /// <param name="allFuncsForGroup">List of all functions for the current group.</param>
    /// <returns>Returns true if the execution of functions is complete.</returns>
    private bool ExecuteFuncsWithoutSkill(BaseUnit caster, uint startedSkillId, List<DoodadFunc> allFuncsForGroup)
    {
        foreach (var funcWithoutSkill in allFuncsForGroup.Where(f => f.FuncType is "DoodadFuncLootItem" or "DoodadFuncLootPack" or "DoodadFuncCutdowning"))
        {
            if (DoFunc(caster, startedSkillId, funcWithoutSkill))
            {
                ListGroupId.Clear();
                return true;
            }
        }

        return false;
    }
    private bool CheckFuncsWithSkill()
    {
        var allFuncsForGroup = DoodadManager.Instance.GetFuncsForGroup(FuncGroupId);
        var count = allFuncsForGroup.Where(f => f.FuncType is "DoodadFuncUse" /*or "DoodadFuncFakeUse"*/ or "DoodadFuncItemChangerUiOpen").Count();
        return count > 0;
    }

    /// <summary>
    /// Executes a function with skill.
    /// </summary>
    /// <param name="caster">The unit using the Doodad.</param>
    /// <param name="startedSkillId">The skill ID used to interact with the Doodad.</param>
    /// <param name="funcWithSkill">The function executed with skill.</param>
    /// <returns>Returns true if the execution of the function is complete.</returns>
    private bool ExecuteFuncWithSkill(BaseUnit caster, uint startedSkillId, DoodadFunc funcWithSkill)
    {
        if (DoFunc(caster, startedSkillId, funcWithSkill))
        {
            // FuncGroupId will be either the current phase, func.NextPhase, or OverridePhase
            DoChangePhase(caster, (int)FuncGroupId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Logs the failure of phase condition checks.
    /// </summary>
    /// <param name="caster">The unit using the Doodad.</param>
    /// <param name="skillId">The skill ID used to interact with the Doodad.</param>
    private void LogPhaseCheckFailure(BaseUnit caster, uint skillId)
    {
        if (caster is Character)
        {
            Logger.Debug($"Use: Did not pass the conditions check! TemplateId {TemplateId}, Using phase {FuncGroupId} with SkillId {skillId}");
            Logger.Debug($"Use: Looking forward to interacting with doodad TemplateId {TemplateId}, Using phase {FuncGroupId}");
        }
        else
            Logger.Trace($"Use: Did not pass the conditions check! TemplateId {TemplateId}, Using phase {FuncGroupId} with SkillId {skillId}");
    }

    /// <summary>
    /// Executes the specified function for the Doodad.
    /// </summary>
    /// <param name="caster">The unit using the Doodad.</param>
    /// <param name="skillId">The skill ID used to interact with the Doodad.</param>
    /// <param name="func">The function to be executed.</param>
    /// <returns>If TRUE, then we stop further execution of functions and wait for interaction.</returns>
    public bool DoFunc(BaseUnit caster, uint skillId, DoodadFunc func)
    {
        // If there is no function, complete the cycle
        if (func == null)
        {
            LogFuncExecution(caster, skillId, "Finished execution with func = null");
            return true;
        }

        // Perform the function
        func.Use(caster, this, skillId, func.NextPhase);
        if (func.SoundId > 0)
            BroadcastPacket(new SCDoodadSoundPacket(this, func.SoundId), true);

        if (ToNextPhase)
        {
            if (func.NextPhase == -1)
            {
                // Do not transition to another phase, stay on the current phase
                // This check is needed for Windstone id=1473
                if (!HasOnlyGroupKindStart())
                {
                    TryCancelFuncTask($"DoFunc::DoodadFuncTimer: The current timer has been canceled. TemplateId {TemplateId}, ObjId {ObjId}, nextPhase {func.NextPhase}");
                    DespawnOrDeleteDoodad();
                }

                return true;
            }

            // Transition to another phase is required
            FuncGroupId = (uint)(OverridePhase > 0 ? OverridePhase : func.NextPhase);
            OverridePhase = 0;
        }
        else
        {
            LogFuncExecution(caster, skillId, $"Finished execution without ToNextPhase = {ToNextPhase}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Logs the execution of the function.
    /// </summary>
    /// <param name="caster">The unit using the Doodad.</param>
    /// <param name="skillId">The skill ID used to interact with the Doodad.</param>
    /// <param name="message">The log message.</param>
    private void LogFuncExecution(BaseUnit caster, uint skillId, string message)
    {
        if (caster is Character)
            Logger.Debug($"DoFunc: {message}: TemplateId {TemplateId}, Using phase {FuncGroupId} with SkillId {skillId}");
        else
            Logger.Trace($"DoFunc: {message}: TemplateId {TemplateId}, Using phase {FuncGroupId} with SkillId {skillId}");
    }

    /// <summary>
    /// Despawns or deletes the Doodad.
    /// </summary>
    private void DespawnOrDeleteDoodad()
    {
        if (Spawner is not null)
            Spawner.Despawn(this);
        else
            Delete();
    }

    /// <summary>
    /// Executes the phase functions for the Doodad and changes the phase if necessary.
    /// </summary>
    /// <param name="caster">The unit using the Doodad.</param>
    /// <param name="nextPhase">The next phase to transition to.</param>
    /// <returns>Returns true if the phase check did not pass and must be aborted.</returns>
    private bool DoPhaseFuncs(BaseUnit caster, ref int nextPhase)
    {
        if (nextPhase <= 0)
            return true;

        // Change the phase
        FuncGroupId = (uint)nextPhase;

        if (!ListGroupId.Contains((uint)nextPhase))
            ListGroupId.Add((uint)nextPhase); // to check CheckPhase()
        else
        {
            var funcs = DoodadManager.Instance.GetFuncsForGroup(FuncGroupId);
            if (funcs.Count > 0)
            {
                // For example, if this is ID=2231, Target, we need to break the recursion
                LogPhaseExecution(caster, "Finished execution with recurse");
                ListGroupId.Clear();
                return true;
            }

            // For example, if this is ID=898, Prison Gate, we do not need to break the recursion
            ListGroupId.Clear();
        }

        TryCancelFuncTask($"DoPhaseFuncs:DoodadFuncTimer: The current timer has been canceled. TemplateId {TemplateId}, ObjId {ObjId}, nextPhase {nextPhase}.");

        LogPhaseExecution(caster, $"DoPhaseFuncs: TemplateId {TemplateId}, ObjId {ObjId}, nextPhase {nextPhase}");

        var phaseFuncs = DoodadManager.Instance.GetPhaseFunc(FuncGroupId);
        if (phaseFuncs.Count == 0)
            return false; // No phase functions for FuncGroupId

        var stop = false;

        // Perform the phase functions one after the other
        foreach (var phaseFunc in phaseFuncs)
        {
            if (phaseFunc == null)
                continue;

            PhaseRatio = Rand.Next(0, 10000); // Check the chance for each phase function

            stop = phaseFunc.Use(caster, this);
            if (stop)
                break; // Interrupt execution of phase functions and switch to OverridePhase
        }

        if (OverridePhase != 0 && stop && FuncGroupId != OverridePhase)
        {
            nextPhase = OverridePhase;
            OverridePhase = 0;
            return DoPhaseFuncs(caster, ref nextPhase);
        }

        if (!_deleted)
            Save(); // Save the doodad in the database

        return stop; // If true, it did not pass the check for the quest and must be aborted
    }

    /// <summary>
    /// Executes the phase function for the Doodad and changes the phase if necessary.
    /// </summary>
    /// <param name="caster">The unit using the Doodad.</param>
    /// <param name="phaseFunc">Executes the phase function</param>
    /// <returns>Returns true if the phase check did not pass and must be aborted.</returns>
    public bool DoPhaseFunc(Character caster, DoodadPhaseFunc phaseFunc)
    {
        if (phaseFunc == null)
            return true;

        var nextPhase = (int)FuncGroupId;
        if (nextPhase <= 0)
            return true;

        TryCancelFuncTask($"DoPhaseFunc:DoodadFuncTimer: The current timer has been canceled. TemplateId {TemplateId}, ObjId {ObjId}, nextPhase {nextPhase}.");
        LogPhaseExecution(caster, $"DoPhaseFunc: TemplateId {TemplateId}, ObjId {ObjId}, nextPhase {nextPhase}");

        var stop = phaseFunc.Use(caster, this);

        // Perform the phase functions one after the other
        if (OverridePhase != 0 && stop && FuncGroupId != OverridePhase)
        {
            FuncGroupId = (uint)OverridePhase;
            OverridePhase = 0;
        }

        DoChangePhase(caster, (int)FuncGroupId);
        
        if (!_deleted)
            Save(); // Save the doodad in the database

        return stop; // If true, it did not pass the check for the quest and must be aborted
    }

    /// <summary>
    /// Logs the execution of the phase functions.
    /// </summary>
    /// <param name="caster">The unit using the Doodad.</param>
    /// <param name="message">The log message.</param>
    private void LogPhaseExecution(BaseUnit caster, string message)
    {
        if (caster is Character)
            Logger.Debug($"{message}: TemplateId {TemplateId}, Using phase {FuncGroupId}");
        else
            Logger.Trace($"{message}: TemplateId {TemplateId}, Using phase {FuncGroupId}");
    }

    /// <summary>
    /// Cancels the current function task if it exists and logs the provided message.
    /// </summary>
    /// <param name="logMessage">The message to log when the task is canceled.</param>
    private void TryCancelFuncTask(string logMessage)
    {
        if (FuncTask != null)
        {
            FuncTask.Cancel();
            FuncTask = null;
            Logger.Debug(logMessage);
        }
    }

    /// <summary>
    /// Start phase functions and phase change
    /// </summary>
    /// <param name="caster">The unit using the Doodad.</param>
    /// <param name="nextPhase">The next phase to transition to.</param>
    /// <returns>if TRUE, it did not pass the check for the quest (it must be aborted)</returns>
    public bool DoChangePhase(BaseUnit caster, int nextPhase)
    {
        if (nextPhase <= 0)
            return false;

        LogPhaseChange(caster, nextPhase);

        bool stop;
        if (CheckFuncsWithSkill())
            stop = true;
        else
            stop = DoPhaseFuncs(caster, ref nextPhase);

        // The phase change packet call must be after the phase functions to have the correct FuncGroupId in the packet
        BroadcastPacket(new SCDoodadPhaseChangedPacket(this), true); // Change the phase to display doodad

        return stop; // If true, it did not pass the check for the quest (it must be aborted)
    }

    /// <summary>
    /// Logs the phase change.
    /// </summary>
    /// <param name="caster">The unit using the Doodad.</param>
    /// <param name="nextPhase">The next phase to transition to.</param>
    private void LogPhaseChange(BaseUnit caster, int nextPhase)
    {
        if (caster is Character)
            Logger.Debug($"DoChangePhase: TemplateId {TemplateId}, ObjId {ObjId}, nextPhase {nextPhase}");
        else
            Logger.Trace($"DoChangePhase: TemplateId {TemplateId}, ObjId {ObjId}, nextPhase {nextPhase}");
    }

    /// <summary>
    /// Changes the phase of another doodad.
    /// </summary>
    /// <param name="caster">The unit using the Doodad.</param>
    /// <param name="doodad">The doodad whose phase is to be changed.</param>
    /// <param name="nextPhase">The next phase to transition to.</param>
    /// <returns>Returns false if the phase change did not pass the check for the quest (it must be aborted).</returns>
    public bool DoChangeOtherDoodadPhase(BaseUnit caster, Doodad doodad, int nextPhase)
    {
        //var prevFuncGroupId = FuncGroupId;
        FuncGroupId = (uint)nextPhase;

        if (nextPhase <= 0) { return false; }

        Logger.Debug($"DoChangePhase: TemplateId {TemplateId}, ObjId {ObjId}, nextPhase {nextPhase}");
        //var stop = DoPhaseFuncs(caster, ref nextPhase);
        // the phase change packet call must be after the phase functions to have the correct FuncGroupId in the packet
        BroadcastPacket(new SCDoodadPhaseChangedPacket(doodad), true); // change the phase to display doodad

        return false; // if true, it did not pass the check for the quest (it must be aborted)
    }

    private bool HasOnlyGroupKindStart()
    {
        return Template.FuncGroups.All(funcGroup =>
            funcGroup.GroupKindId is not (DoodadFuncGroups.DoodadFuncGroupKind.Normal
                or DoodadFuncGroups.DoodadFuncGroupKind.End));
    }

    public bool IsGroupKindStart(uint funcGroupId)
    {
        return Template.FuncGroups.Where(funcGroup =>
                funcGroup.GroupKindId == DoodadFuncGroups.DoodadFuncGroupKind.Start)
            .Any(funcGroup => funcGroupId == funcGroup.Id);
    }

    public uint GetFuncGroupId()
    {
        return (from funcGroup in Template.FuncGroups
                where funcGroup.GroupKindId == DoodadFuncGroups.DoodadFuncGroupKind.Start
                select funcGroup.Id).FirstOrDefault();
    }

    public void OnSkillHit(BaseUnit caster, uint skillId)
    {
        var funcs = DoodadManager.Instance.GetFuncsForGroup(FuncGroupId);
        if (funcs == null) return;

        ExecuteSkillHitFuncs(caster, skillId, funcs);
    }

    /// <summary>
    /// Executes the skill hit functions.
    /// </summary>
    /// <param name="caster">The unit using the Doodad.</param>
    /// <param name="skillId">The skill ID used to interact with the Doodad.</param>
    /// <param name="funcs">The list of functions to execute.</param>
    private void ExecuteSkillHitFuncs(BaseUnit caster, uint skillId, List<DoodadFunc> funcs)
    {
        foreach (var func in funcs.Where(func => func.FuncType == "DoodadFuncSkillHit"))
            Use(caster, skillId);
    }

    /// <summary>
    /// Initialization of the current doodad phase
    /// </summary>
    public void InitDoodad()
    {
        ApplyClimateSettings();
        PerformPhaseChange();
    }

    /// <summary>
    /// Applies climate settings to the doodad.
    /// </summary>
    private void ApplyClimateSettings()
    {
        var growTime = Template.TotalDoodadGrowthTime / AppConfiguration.Instance.World.GrowthRate;
        if (Template.TotalDoodadGrowthTime > 0 && ZoneManager.DoodadHasMatchingClimate(this))
            growTime = (int)Math.Round(growTime * 0.73f);

        GrowthTime = PlantTime.AddMilliseconds(growTime);
    }

    /// <summary>
    /// Performs the phase change for the doodad.
    /// </summary>
    private void PerformPhaseChange()
    {
        var unit = WorldManager.Instance.GetUnit(OwnerObjId);
        DoChangePhase(unit, (int)FuncGroupId);
    }

    public override void BroadcastPacket(GamePacket packet, bool self)
    {
        foreach (var character in WorldManager.GetAround<Character>(this))
            character.SendPacket(packet);
    }

    public override void AddVisibleObject(Character character)
    {
        character.SendPacket(new SCDoodadCreatedPacket(this));
        base.AddVisibleObject(character);
    }

    public override void RemoveVisibleObject(Character character)
    {
        base.RemoveVisibleObject(character);
        character.SendPacket(new SCDoodadRemovedPacket(ObjId));
    }

    /// <summary>
    /// GetItemTemplateIdByDoodadTemplateId - если doodad это backpack, то вернуть TemplateId предмета, который заменит этот doodad у персонажа.
    /// </summary>
    /// <param name="doodadTemplateId"></param>
    /// <returns></returns>
    public static uint GetItemTemplateIdByDoodadTemplateId(uint doodadTemplateId)
    {
        var res = ItemManager.GetPutDownBackpackEffectData(doodadTemplateId);
        return res.Count > 0 ? res[0].UsedItemId : 0u;

        //return doodadTemplateId switch
        //{
        //    7809 => 31872, // груз мятных леденцов -> мятные леденцы
        //    7861 => 31912, // груз настойки алоэ -> настойка алоэ
        //    _ => 0
        //};
    }

    public PacketStream Write(PacketStream stream)
    {
        stream.WriteBc(ObjId); // The object # in the list

        WriteTemplateData(stream);
        WritePositionData(stream);
        WriteAdditionalData(stream);

        return stream;
    }

    /// <summary>
    /// Writes the template data to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    private void WriteTemplateData(PacketStream stream)
    {
        // TemplateId - The template id needed for that object, the client then uses the template configurations, not the server
        // CurrentPhaseId / FuncGroupId - doodad_func_group_id
        // Здесь должны вставить: doodadTemplateId -> itemTemplateId
        // можно найти: backpack_doodad_id->PutdownBackPackEffect->effect->skill_effect->skill->templateId
        // ItemTemplateId от BackPack, который лежит на земле: Item->skill->PutdownBackPackEffect->backpack_doodad_id
        // Doodad ID=7794 Solzreed Dried Food [Interaction - Backpack] -> ID=31857 Solzreed Dried Food -> Skill ID=24870 Drop Specialty
        // PutdownBackPackEffect : backpack_doodad_id->effect_id, 
        // QuestGlow - When this is higher than 0 it shows a blue orb over the doodad

        var usedItemId = GetItemTemplateIdByDoodadTemplateId(TemplateId);
        stream.WritePisc(TemplateId, FuncGroupId, usedItemId, QuestGlow);
        stream.Write(Flag);
        stream.WriteBc(OwnerObjId);      // The creator of the object
        stream.WriteBc(ParentObjId);     // Things like boats or cars
        stream.Write((byte)AttachPoint); // attachPoint, relative to the parentObj (Door or window on a house, seats on carriage, etc.)
    }

    /// <summary>
    /// Writes the position data to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    private void WritePositionData(PacketStream stream)
    {
        if (AttachPoint > 0 || ParentObjId > 0)
        {
            stream.WritePosition(Transform.Local.Position.X, Transform.Local.Position.Y, Transform.Local.Position.Z);
            var (roll, pitch, yaw) = Transform.Local.ToRollPitchYawShorts();
            stream.Write(roll);
            stream.Write(pitch);
            stream.Write(yaw);
        }
        else
        {
            stream.WritePosition(Transform.World.Position.X, Transform.World.Position.Y, Transform.World.Position.Z);
            var (roll, pitch, yaw) = Transform.World.ToRollPitchYawShorts();
            stream.Write(roll);
            stream.Write(pitch);
            stream.Write(yaw);
        }
    }

    /// <summary>
    /// Writes additional data to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    private void WriteAdditionalData(PacketStream stream)
    {
        stream.Write(Scale);           // The size of the object
        stream.Write(OwnerId);         // characterId
        stream.Write(UccId);           // type(id)
        stream.Write(ItemTemplateId);  // type(id)
        stream.Write(TimeLeft);        // growing
        stream.Write(PlantTime);       // plantTime
        stream.Write(0);               // family
        stream.Write(PuzzleGroup);     // puzzleGroup
        stream.Write((byte)OwnerType); // ownerType
        stream.Write(OwnerDbId);       // dbHouseId
        stream.Write(Data);            // data - attachPointId для хранения в базе данных
        var usedItemId = GetItemTemplateIdByDoodadTemplateId(TemplateId);
        if (usedItemId != 0)
        {
            stream.Write(FreshnessTime); // freshnessTime
            stream.Write(OwnerId);       // type crafter?
            stream.Write((short)14);     // type
        }
        stream.Write(0u); // type
    }

    public override void Delete()
    {
        base.Delete();
        _deleted = true;
        RemoveAreaTriggers();
        DeleteAssociatedItem();
        DeleteFromDatabase();
        SpawnManager.Instance.RemovePlayerDoodad(this);
        IsPersistent = false;
    }

    /// <summary>
    /// Removes area triggers associated with the doodad.
    /// </summary>
    private void RemoveAreaTriggers()
    {
        var triggersToRemove = new List<AreaTrigger>(AttachAreaTriggers);
        foreach (var areaTrigger in triggersToRemove)
            AreaTriggerManager.Instance.RemoveAreaTrigger(areaTrigger);

        AttachAreaTriggers.Clear();
    }

    /// <summary>
    /// Deletes the associated item if expired.
    /// </summary>
    private void DeleteAssociatedItem()
    {
        if (ItemId > 0)
        {
            var item = ItemManager.Instance.GetItemByItemId(ItemId);
            if (item is { HoldingContainer.ContainerType: SlotType.Invalid or SlotType.Money })
                item.HoldingContainer.RemoveItem(ItemTaskType.Invalid, item, true);
        }
    }

    /// <summary>
    /// Deletes the doodad from the database.
    /// </summary>
    private void DeleteFromDatabase()
    {
        if (!IsPersistent)
            return;

        using var connection = MySQL.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM doodads WHERE id = @id";
        command.Parameters.AddWithValue("@id", DbId);
        command.Prepare();
        command.ExecuteNonQuery();
    }

    public void Save()
    {
        if (!IsPersistent)
            return;

        DbId = DbId > 0 ? DbId : DoodadIdManager.Instance.GetNextId();
        using var connection = MySQL.CreateConnection();
        using var command = connection.CreateCommand();
        PrepareSaveCommand(command);
        command.Prepare();
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Prepares the save command for the doodad.
    /// </summary>
    /// <param name="command">The command to prepare.</param>
    private void PrepareSaveCommand(MySqlCommand command)
    {
        var parentDoodadId = GetParentDoodadId();

        command.CommandText =
            "REPLACE INTO doodads (`id`, `owner_id`, `owner_type`, `attach_point`, `template_id`, `current_phase_id`, `plant_time`, `growth_time`, `phase_time`, `freshness_time`, `x`, `y`, `z`, `roll`, `pitch`, `yaw`, `scale`, `item_id`, `house_id`, `parent_doodad`, `item_template_id`, `item_container_id`, `data`, `farm_type`) " +
            "VALUES(@id, @owner_id, @owner_type, @attach_point, @template_id, @current_phase_id, @plant_time, @growth_time, @phase_time, @freshness_time, @x, @y, @z, @roll, @pitch, @yaw, @scale, @item_id, @house_id, @parent_doodad, @item_template_id, @item_container_id, @data, @farm_type)";
        command.Parameters.AddWithValue("@id", DbId);
        command.Parameters.AddWithValue("@owner_id", OwnerId);
        command.Parameters.AddWithValue("@owner_type", OwnerType);
        command.Parameters.AddWithValue("@attach_point", AttachPoint);
        command.Parameters.AddWithValue("@template_id", TemplateId);
        command.Parameters.AddWithValue("@current_phase_id", FuncGroupId);
        command.Parameters.AddWithValue("@plant_time", PlantTime);
        command.Parameters.AddWithValue("@growth_time", GrowthTime);
        command.Parameters.AddWithValue("@phase_time", PhaseTime);
        command.Parameters.AddWithValue("@freshness_time", FreshnessTime);
        command.Parameters.AddWithValue("@x", Transform?.Local.Position.X ?? 0f);
        command.Parameters.AddWithValue("@y", Transform?.Local.Position.Y ?? 0f);
        command.Parameters.AddWithValue("@z", Transform?.Local.Position.Z ?? 0f);
        command.Parameters.AddWithValue("@roll", Transform?.Local.Rotation.X ?? 0f);
        command.Parameters.AddWithValue("@pitch", Transform?.Local.Rotation.Y ?? 0f);
        command.Parameters.AddWithValue("@yaw", Transform?.Local.Rotation.Z ?? 0f);
        command.Parameters.AddWithValue("@scale", Scale);
        command.Parameters.AddWithValue("@item_id", ItemId);
        command.Parameters.AddWithValue("@house_id", OwnerDbId);
        command.Parameters.AddWithValue("@parent_doodad", parentDoodadId);
        command.Parameters.AddWithValue("@item_template_id", ItemTemplateId);
        command.Parameters.AddWithValue("@item_container_id", GetItemContainerId());
        command.Parameters.AddWithValue("@data", Data);
        command.Parameters.AddWithValue("@farm_type", FarmType);
    }

    /// <summary>
    /// Gets the parent doodad ID.
    /// </summary>
    /// <returns>The parent doodad ID.</returns>
    private uint GetParentDoodadId()
    {
        if (Transform?.Parent?.GameObject is Doodad pDoodad && pDoodad.DbId > 0)
            return pDoodad.DbId;

        return 0u;
    }

    /// <summary>
    /// Despawns the specified doodad.
    /// </summary>
    /// <param name="doodad">The doodad to despawn.</param>
    public void DoDespawn(Doodad doodad)
    {
        Spawner.DoDespawn(doodad);
    }

    /// <summary>
    /// Determines whether the doodad can be removed.
    /// </summary>
    /// <returns>Returns true if the doodad can be removed; otherwise, false.</returns>
    public override bool AllowRemoval()
    {
        // Only allow removal if there is no other persistent Doodads stacked on top of this
        foreach (var child in Transform.Children)
        {
            if (child.GameObject is Doodad { IsPersistent: true })
                return false;
        }

        return base.AllowRemoval();
    }

    /// <summary>
    /// Return the associated ItemContainerId for this Doodad
    /// </summary>
    /// <returns></returns>
    public virtual ulong GetItemContainerId()
    {
        return 0;
    }

    /// <summary>
    /// Writes the fish finder unit data to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <returns>The updated stream.</returns>
    public PacketStream WriteFishFinderUnit(PacketStream stream)
    {
        stream.WriteBc(ObjId);
        stream.Write(Template.Id);
        stream.WritePosition(Transform.World.Position);

        return stream;
    }
}
