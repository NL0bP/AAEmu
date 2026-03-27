using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

using AAEmu.Commons.Network;
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
    private static readonly WaitCallback s_SaveCallback = obj =>
    {
        var doodad = (Doodad)obj;
        try
        {
            doodad.Save();
        }
        finally
        {
            Interlocked.Exchange(ref doodad._saveQueued, 0);
        }
    };

    private static readonly WaitCallback s_DeleteCallback = obj =>
    {
        var request = (DeleteRequest)obj;
        try
        {
            using var connection = MySQL.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = request.IsWorldDoodad
                ? "DELETE FROM world_doodads WHERE id = @id"
                : "DELETE FROM doodads WHERE id = @id";
            command.Parameters.AddWithValue("@id", request.DoodadId);
            command.Prepare();
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting doodad from DB: id={0}, isWorldDoodad={1}", request.DoodadId, request.IsWorldDoodad);
        }
    };

    private readonly struct DeleteRequest
    {
        public DeleteRequest(uint doodadId, bool isWorldDoodad)
        {
            DoodadId = doodadId;
            IsWorldDoodad = isWorldDoodad;
        }

        public uint DoodadId { get; }
        public bool IsWorldDoodad { get; }
    }

    private static readonly ConcurrentQueue<(Doodad Doodad, uint Phase)> _pendingBroadcasts = new();
    private static readonly ConcurrentDictionary<uint, Doodad> _dirtyDoodads = new();
    private static long _lastSaveTick = 0;

    private float _scale;
    private int _saveQueued;
    private uint _cachedTimeLeft;
    private long _timeLeftCacheTick;
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
            return CurrentFuncs?.Count > 0 ? (DoodadFuncPermission)CurrentFuncs[0].PermId : DoodadFuncPermission.Any;
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
                _timeLeftCacheTick = 0;
                _cachedTimeLeft = 0;

                if (IsPersistent)
                {
                    ScheduleSave();
                }

                CurrentFuncs = DoodadManager.Instance.GetFuncsForGroup(_funcGroupId);
                CurrentPhaseFuncs = DoodadManager.Instance.GetPhaseFunc(_funcGroupId);

                // Register new ToD triggers (if any)
                CurrentToDTriggers.Clear();
                if (CurrentPhaseFuncs != null)
                {
                    foreach (var currentPhaseFunc in CurrentPhaseFuncs)
                    {
                        if (currentPhaseFunc.FuncType != "DoodadFuncTod")
                            continue;
                        var todPhaseFunc = DoodadManager.Instance.GetPhaseFuncTemplate(currentPhaseFunc.FuncId, currentPhaseFunc.FuncType);
                        if (todPhaseFunc is not DoodadFuncTod doodadFuncTod)
                        {
                            Logger.Error("DoodadFuncTod is not a DoodadFuncTod");
                            continue;
                        }

                        CurrentToDTriggers.TryAdd(doodadFuncTod.TodAsHours, doodadFuncTod.NextPhase);
                    }
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
                    ScheduleSave();
                }
            }
        }
    }
    public FarmType FarmType { get; set; }
    public uint QuestGlow { get; set; } //0 off // 1 on
    public int PuzzleGroup { get; set; } = -1; // -1 off
    public DoodadSpawner Spawner { get; set; }
    public uint SourceTemplateId { get; set; }
    public DoodadFuncTask FuncTask { get; set; }
    public DateTime FreshnessTime { get; set; }
    public bool IsRebuy { get; set; }
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
            var now = Environment.TickCount64;
            if (now - _timeLeftCacheTick < 100)
                return _cachedTimeLeft;

            _timeLeftCacheTick = now;

            var utcNow = DateTime.UtcNow;

            if (CurrentPhaseFuncs != null)
            {
                foreach (var func in CurrentPhaseFuncs)
                {
                    var template = DoodadManager.Instance.GetPhaseFuncTemplate(func.FuncId, func.FuncType);
                    if (template is DoodadFuncFinal doodadFuncFinal)
                    {
                        if (doodadFuncFinal.After > 0)
                        {
                            var left = (PhaseTime + TimeSpan.FromMilliseconds(doodadFuncFinal.After) - utcNow).TotalMilliseconds;
                            _cachedTimeLeft = (uint)Math.Round(Math.Max(1, left));
                            return _cachedTimeLeft;
                        }
                    }
                }
            }

            if (FuncTask != null)
            {
                if (GrowthTime > utcNow)
                {
                    _cachedTimeLeft = (uint)(GrowthTime - utcNow).TotalMilliseconds;
                    return _cachedTimeLeft;
                }
            }

            if (GrowthTime > utcNow)
            {
                _cachedTimeLeft = (uint)(GrowthTime - utcNow).TotalMilliseconds;
                return _cachedTimeLeft;
            }

            _cachedTimeLeft = 0;
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
    private HashSet<uint> _visitedPhases = new();
    public List<AreaTrigger> AttachAreaTriggers { get; set; } = [];
    public bool IsRespawnScheduled { get; set; } = false;

    public Doodad()
    {
        _scale = 1f;
        PlantTime = DateTime.MinValue;
        AttachPoint = AttachPointKind.System;
        Seat = new VehicleSeat(this);
        ListGroupId = new List<uint>(4);
        CurrentFuncs = null;
        CurrentPhaseFuncs = null;
        CurrentToDTriggers = new Dictionary<float, int>(2);
        IsRespawnScheduled = false;
    }

    public void SetScale(float scale)
    {
        _scale = scale;
    }

    /*
     * 1. Создание (посадка) Doodad запускает на стартовой фазе PhaseFunc;
     * 2. Ждем взаимодействия с Doodad;
     * 3. Непосредствено взаимодействие начинается с выполнения Func с учётом SkillId;
     * 4. Далее на следующей фазе начинаем выполнение с фазовых функций, а затем сами функции, если перед этим прошли проверки в фазовых функциях;
     */
    public void SetData(int data)
    {
        _data = data;
    }

    /// <summary>
    /// Uses the given Doodad with the specified caster and skill.
    /// </summary>
    public void Use(BaseUnit caster, uint startedSkillId = 0, int funcGroupId = 0)
    {
        if (caster == null)
            return;

        EnsurePersistentWorldState(caster);

        if (funcGroupId > 0)
            FuncGroupId = (uint)funcGroupId;

        var skillId = startedSkillId;
        int maxIterations = 10;
        int iteration = 0;

        _visitedPhases.Clear();

        while (iteration++ < maxIterations)
        {
            LogUse(caster, skillId);
            ToNextPhase = false;
            ListGroupId.Clear();

            if (_visitedPhases.Contains(FuncGroupId))
            {
                Logger.Error("Doodad phase cycle detected: {TemplateId}, stopping execution", TemplateId);
                _visitedPhases.Clear();
                return;
            }
            _visitedPhases.Add(FuncGroupId);

            var funcWithSkill = DoodadManager.Instance.GetFunc(FuncGroupId, skillId);
            var allFuncsForGroup = DoodadManager.Instance.GetFuncsForGroup(FuncGroupId);

            if (skillId == 0)
            {
                if (ExecuteFuncsWithoutSkill(caster, startedSkillId, allFuncsForGroup))
                    return;
            }
            else
            {
                if (ExecuteFuncWithSkill(caster, startedSkillId, funcWithSkill))
                    return;
            }

            if (!ToNextPhase)
            {
                return;
            }

            int nextPhase;
            if (OverridePhase > 0)
            {
                nextPhase = OverridePhase;
                OverridePhase = 0;
            }
            else if (OverridePhase == -1)
            {
                OverridePhase = 0;
                return;
            }
            else
            {
                nextPhase = (int)FuncGroupId;
            }

            if (nextPhase <= 0)
                return;

            bool stop = DoPhaseFuncs(caster, ref nextPhase);

            BroadcastPacket(new SCDoodadPhaseChangedPacket(this), true);

            if (stop)
            {
                return;
            }

            skillId = 0;

            if (ExecuteFuncsWithoutSkill(caster, 0, DoodadManager.Instance.GetFuncsForGroup(FuncGroupId)))
            {
                return;
            }

            if (OverridePhase > 0)
            {
                nextPhase = OverridePhase;
                OverridePhase = 0;

                FuncGroupId = (uint)nextPhase;

                stop = DoPhaseFuncs(caster, ref nextPhase);

                BroadcastPacket(new SCDoodadPhaseChangedPacket(this), true);

                if (stop)
                {
                    return;
                }
            }
        }

        Logger.Error("Doodad phase chain exceeded max iterations: {TemplateId}", TemplateId);
        _visitedPhases.Clear();
    }

    private void LogUse(BaseUnit caster, uint skillId)
    {
        if (caster is Character)
            Logger.Warn("Doodad: {ObjId}. Use: TemplateId: {TemplateId}, current phase: {FuncGroupId}, SkillId: {SkillId}", ObjId, TemplateId, FuncGroupId, skillId);
    }

    private bool ExecuteFuncsWithoutSkill(BaseUnit caster, uint startedSkillId, List<DoodadFunc> allFuncsForGroup)
    {
        foreach (var func in allFuncsForGroup)
        {
            if (func.FuncType is not ("DoodadFuncLootItem" or "DoodadFuncLootPack" or "DoodadFuncCutdowning"))
            {
                continue;
            }

            if (DoFunc(caster, startedSkillId, func))
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
        foreach (var func in allFuncsForGroup)
        {
            if (func.FuncType is "DoodadFuncBuildConditionUiOpen" or "DoodadFuncItemChangerUiOpen" or "DoodadFuncResidentTownhallUiOpen")
                return true;
        }
        return false;
    }

    private bool ExecuteFuncWithSkill(BaseUnit caster, uint startedSkillId, DoodadFunc funcWithSkill)
    {
        if (funcWithSkill == null)
            return false;

        var result = DoFunc(caster, startedSkillId, funcWithSkill);

        if (result)
        {
            return true;
        }

        return false;
    }

    private void LogPhaseCheckFailure(BaseUnit caster, uint skillId)
    {
        if (caster is Character)
        {
            Logger.Debug("Doodad: {ObjId}. Use: Failed condition check! TemplateId: {TemplateId}, current phase: {FuncGroupId}, SkillId: {SkillId}", ObjId, TemplateId, FuncGroupId, skillId);
            Logger.Debug("Doodad: {ObjId}. Use: Waiting for interaction with Doodad TemplateId: {TemplateId}, current phase: {FuncGroupId}", ObjId, TemplateId, FuncGroupId);
        }
    }

    public bool DoFunc(BaseUnit caster, uint skillId, DoodadFunc func)
    {
        if (func == null)
        {
            return true;
        }

        func.Use(caster, this, skillId, func.NextPhase);

        if (func.SoundId > 0)
            BroadcastPacket(new SCDoodadSoundPacket(this, func.SoundId), true);

        if (ToNextPhase)
        {
            if (func.NextPhase == -1)
            {
                if (!HasOnlyGroupKindStart())
                {
                    TryCancelFuncTask();
                    DespawnOrDeleteDoodad();
                }
                return true;
            }
            else if (func.NextPhase == 0)
            {
                return true;
            }

            if (OverridePhase == 0)
            {
                OverridePhase = func.NextPhase;
            }
            return false;
        }

        return true;
    }

    private void LogFuncExecution(BaseUnit caster, uint skillId, string message)
    {
        if (caster is Character)
            Logger.Debug("Doodad: {ObjId}. DoFunc: {Message}: TemplateId {TemplateId}, current phase {FuncGroupId}, SkillId {SkillId}", ObjId, message, TemplateId, FuncGroupId, skillId);
    }

    private void DespawnOrDeleteDoodad()
    {
        if (Spawner is not null)
            Spawner.Despawn(this);
        else
            Delete();
    }

    private bool DoPhaseFuncs(BaseUnit caster, ref int nextPhase)
    {
        int maxIterations = 10;
        int iteration = 0;

        while (iteration++ < maxIterations)
        {
            if (nextPhase <= 0)
            {
                return true;
            }

            if (FuncGroupId != (uint)nextPhase)
            {
                FuncGroupId = (uint)nextPhase;
            }

            if (ListGroupId.Contains((uint)nextPhase))
            {
                ListGroupId.Clear();
                return true;
            }
            ListGroupId.Add((uint)nextPhase);

            TryCancelFuncTask();

            var phaseFuncs = DoodadManager.Instance.GetPhaseFunc(FuncGroupId);

            if (phaseFuncs.Count == 0)
            {
                return false;
            }

            bool stop = false;
            bool phaseChanged = false;

            foreach (var phaseFunc in phaseFuncs)
            {
                if (phaseFunc == null)
                    continue;

                PhaseRatio = Random.Shared.Next(0, 10000);
                stop = phaseFunc.Use(caster, this);

                if (OverridePhase == -1)
                {
                    OverridePhase = 0;
                    return true;
                }

                if (OverridePhase != 0 && stop)
                {
                    nextPhase = OverridePhase;
                    OverridePhase = 0;
                    phaseChanged = true;
                    break;
                }

                if (stop)
                {
                    break;
                }
            }

            if (phaseChanged)
            {
                continue;
            }

            if (!_deleted)
                ScheduleSave();

            return stop;
        }

        Logger.Error("DoPhaseFuncs exceeded max iterations: {TemplateId}", TemplateId);
        return true;
    }

    public bool DoPhaseFunc(Character caster, DoodadPhaseFunc phaseFunc)
    {
        if (phaseFunc == null)
            return true;

        var nextPhase = (int)FuncGroupId;
        if (nextPhase <= 0)
            return true;

        TryCancelFuncTask();

        var stop = phaseFunc.Use(caster, this);

        if (OverridePhase != 0 && stop && FuncGroupId != OverridePhase)
        {
            FuncGroupId = (uint)OverridePhase;
            OverridePhase = 0;
        }

        DoChangePhase(caster, (int)FuncGroupId);

        if (!_deleted)
            ScheduleSave();

        return stop;
    }

    private void LogPhaseExecution(BaseUnit caster, string message)
    {
        if (caster is Character)
            Logger.Debug("Doodad: {ObjId}. {Message}: TemplateId: {TemplateId}, current phase: {FuncGroupId}", ObjId, message, TemplateId, FuncGroupId);
    }

    private void TryCancelFuncTask()
    {
        if (FuncTask != null)
        {
            FuncTask.Cancel();
            FuncTask = null;
        }
    }

    public bool DoChangePhase(BaseUnit caster, int nextPhase)
    {
        if (nextPhase <= 0)
            return false;

        EnsurePersistentWorldState(caster);

        LogPhaseChange(caster, nextPhase);

        var previousPhase = FuncGroupId;

        FuncGroupId = (uint)nextPhase;

        bool stop = DoPhaseFuncs(caster, ref nextPhase);

        BroadcastPacket(new SCDoodadPhaseChangedPacket(this), true);

        return stop;
    }

    private void LogPhaseChange(BaseUnit caster, int nextPhase)
    {
        if (caster is Character)
            Logger.Debug("Doodad: {ObjId}. DoChangePhase: TemplateId: {TemplateId}, ObjId: {ObjId}, next phase: {NextPhase}", ObjId, TemplateId, ObjId, nextPhase);
    }

    public bool DoChangeOtherDoodadPhase(BaseUnit caster, Doodad doodad, int nextPhase)
    {
        EnsurePersistentWorldState(caster);

        FuncGroupId = (uint)nextPhase;

        if (nextPhase <= 0) { return false; }

        Logger.Debug("Doodad: {ObjId}. DoChangePhase: TemplateId: {TemplateId}, ObjId: {ObjId}, next phase: {NextPhase}", ObjId, TemplateId, ObjId, nextPhase);
        BroadcastPacket(new SCDoodadPhaseChangedPacket(doodad), true);

        return false;
    }

    private bool HasOnlyGroupKindStart()
    {
        foreach (var funcGroup in Template.FuncGroups)
        {
            if (funcGroup.GroupKindId is DoodadFuncGroups.DoodadFuncGroupKind.Normal or DoodadFuncGroups.DoodadFuncGroupKind.End)
                return false;
        }
        return true;
    }

    public bool IsGroupKindStart(uint funcGroupId)
    {
        foreach (var funcGroup in Template.FuncGroups)
        {
            if (funcGroup.GroupKindId == DoodadFuncGroups.DoodadFuncGroupKind.Start && funcGroup.Id == funcGroupId)
                return true;
        }
        return false;
    }

    public uint GetFuncGroupId()
    {
        foreach (var funcGroup in Template.FuncGroups)
        {
            if (funcGroup.GroupKindId == DoodadFuncGroups.DoodadFuncGroupKind.Start)
                return funcGroup.Id;
        }
        return 0;
    }

    public void OnSkillHit(BaseUnit caster, uint skillId)
    {
        EnsurePersistentWorldState(caster);

        var funcs = DoodadManager.Instance.GetFuncsForGroup(FuncGroupId);
        if (funcs == null) return;

        ExecuteSkillHitFuncs(caster, skillId, funcs);
    }

    private void ExecuteSkillHitFuncs(BaseUnit caster, uint skillId, List<DoodadFunc> funcs)
    {
        foreach (var func in funcs)
        {
            if (func.FuncType == "DoodadFuncSkillHit")
                Use(caster, skillId);
        }
    }

    /// <summary>
    /// Initialization of the current doodad phase
    /// </summary>
    public void InitDoodad()
    {
        ApplyClimateSettings();
        PerformPhaseChange();
    }

    private void ApplyClimateSettings()
    {
        var growTime = Template.TotalDoodadGrowthTime / AppConfiguration.Instance.World.GrowthRate;
        if (Template.TotalDoodadGrowthTime > 0 && ZoneManager.DoodadHasMatchingClimate(this))
            growTime = (int)Math.Round(growTime * 0.73f);

        GrowthTime = PlantTime.AddMilliseconds(growTime);
    }

    /// <summary>
    /// Performs the phase change for the doodad.
    /// FIX: Do not overwrite already loaded FuncGroupId for persisted doodads
    /// </summary>
    private void PerformPhaseChange()
    {
        var obj = WorldManager.Instance.GetUnit(OwnerObjId);
        if (FuncGroupId == 0)
            FuncGroupId = GetFuncGroupId();
        PerformPhaseChange(obj);
    }

    private void EnsurePersistentWorldState(BaseUnit caster)
    {
        if (IsPersistent)
            return;

        if (caster is not Character)
            return;

        if (OwnerType != DoodadOwnerType.System)
            return;

        if (Spawner == null)
            return;

        IsPersistent = true;
        SourceTemplateId = Spawner.UnitId > 0 ? Spawner.UnitId : TemplateId;
    }

    private bool IsWorldPersistentDoodad()
    {
        return OwnerType == DoodadOwnerType.System && SourceTemplateId > 0;
    }

    public void PerformPhaseChange(BaseUnit obj)
    {
        TryCancelFuncTask();

        var timer = new System.Timers.Timer(5);
        timer.AutoReset = false;
        timer.Elapsed += (sender, e) =>
        {
            DoChangePhase(obj, (int)FuncGroupId);
            timer.Dispose();
        };
        timer.Start();
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

    public static uint GetItemTemplateIdByDoodadTemplateId(uint doodadTemplateId)
    {
        var res = ItemManager.GetPutDownBackpackEffectData(doodadTemplateId);
        return res.Count > 0 ? res[0].UsedItemId : 0u;
    }

    public PacketStream Write(PacketStream stream)
    {
        stream.WriteBc(ObjId);

        WriteTemplateData(stream);
        WritePositionData(stream);
        WriteAdditionalData(stream);

        return stream;
    }

    private void WriteTemplateData(PacketStream stream)
    {
        stream.WritePisc(TemplateId, FuncGroupId, ItemTemplateId, QuestGlow);
        stream.Write(Flag);
        stream.WriteBc(OwnerObjId);
        stream.WriteBc(ParentObjId);
        stream.Write((byte)AttachPoint);
    }

    private void WritePositionData(PacketStream stream)
    {
        stream.WritePosition(Transform.Local.Position.X, Transform.Local.Position.Y, Transform.Local.Position.Z);
        var (roll, pitch, yaw) = Transform.Local.ToRollPitchYawShorts();
        stream.Write(roll);
        stream.Write(pitch);
        stream.Write(yaw);
    }

    private void WriteAdditionalData(PacketStream stream)
    {
        stream.Write(Scale);
        stream.Write(OwnerId);
        stream.Write(UccId);
        stream.Write(ItemTemplateId);
        stream.Write(TimeLeft);
        stream.Write(PlantTime);
        stream.Write(0);
        stream.Write(PuzzleGroup);
        stream.Write((byte)OwnerType);
        stream.Write(OwnerDbId);
        stream.Write(Data);
        var usedItemId = GetItemTemplateIdByDoodadTemplateId(TemplateId);
        if (usedItemId != 0)
        {
            stream.Write(FreshnessTime);
            stream.Write(OwnerId);
            stream.Write((short)14);
        }
        stream.Write(0u);
    }

    public override void Delete()
    {
        TryCancelFuncTask();

        base.Delete();
        _deleted = true;
        RemoveAreaTriggers();
        DeleteAssociatedItem();
        DeleteFromDatabase();
        SpawnManager.Instance.RemovePlayerDoodad(this);
        IsPersistent = false;
    }

    private void RemoveAreaTriggers()
    {
        for (int i = AttachAreaTriggers.Count - 1; i >= 0; i--)
        {
            AreaTriggerManager.Instance.RemoveAreaTrigger(AttachAreaTriggers[i]);
        }
        AttachAreaTriggers.Clear();
    }

    private void DeleteAssociatedItem()
    {
        if (ItemId > 0)
        {
            var item = ItemManager.Instance.GetItemByItemId(ItemId);
            if (item is { HoldingContainer.ContainerType: SlotType.Invalid or SlotType.Money })
                item.HoldingContainer.RemoveItem(ItemTaskType.Invalid, item, true);
        }
    }

    private void DeleteFromDatabase()
    {
        if (!IsPersistent)
            return;

        var doodadId = DbId;
        if (doodadId <= 0)
            return;

        ThreadPool.QueueUserWorkItem(s_DeleteCallback, new DeleteRequest(doodadId, IsWorldPersistentDoodad()));
    }

    public void Save()
    {
        if (!IsPersistent)
            return;

        try
        {
            DbId = DbId > 0 ? DbId : DoodadIdManager.Instance.GetNextId();
            using var connection = MySQL.CreateConnection();
            using var command = connection.CreateCommand();
            PrepareSaveCommand(command);
            command.Prepare();
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error saving doodad to DB: id={0}, templateId={1}", DbId, TemplateId);
        }
    }

    /// <summary>
    /// Планирует фоновое сохранение без блокировки игрового тика.
    /// </summary>
    private void ScheduleSave()
    {
        if (!IsPersistent)
            return;

        if (Interlocked.Exchange(ref _saveQueued, 1) == 1)
            return;

        if (DbId == 0)
            DbId = DoodadIdManager.Instance.GetNextId();

        _dirtyDoodads[DbId] = this;
    }

    private void PrepareSaveCommand(MySqlCommand command)
    {
        if (IsWorldPersistentDoodad())
        {
            PrepareWorldSaveCommand(command);
            return;
        }

        var parentDoodadId = GetParentDoodadId();

        command.CommandText =
            "REPLACE INTO doodads (`id`, `owner_id`, `owner_type`, `attach_point`, `template_id`, `current_phase_id`, `plant_time`, `growth_time`, `phase_time`, `freshness_time`, `x`, `y`, `z`, `roll`, `pitch`, `yaw`, `scale`, `item_id`, `house_id`, `parent_doodad`, `item_template_id`, `item_container_id`, `data`, `farm_type`) " +
            "VALUES(@id, @owner_id, @owner_type, @attach_point, @template_id, @current_phase_id, @plant_time, @growth_time, @phase_time, @freshness_time, @x, @y, @z, @roll, @pitch, @yaw, @scale, @item_id, @house_id, @parent_doodad, @item_template_id, @item_container_id, @data, @farm_type)";
        command.Parameters.AddWithValue("@id", DbId);
        command.Parameters.AddWithValue("@owner_id", OwnerId);
        command.Parameters.AddWithValue("@owner_type", (byte)OwnerType);
        command.Parameters.AddWithValue("@attach_point", (byte)AttachPoint);
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
        command.Parameters.AddWithValue("@farm_type", (byte)FarmType);
    }

    private void PrepareWorldSaveCommand(MySqlCommand command)
    {
        var x = RoundWorldCoord(Transform?.Local.Position.X ?? 0f);
        var y = RoundWorldCoord(Transform?.Local.Position.Y ?? 0f);
        var z = RoundWorldCoord(Transform?.Local.Position.Z ?? 0f);

        command.CommandText =
            "REPLACE INTO world_doodads (`id`, `source_template_id`, `template_id`, `x`, `y`, `z`, `roll`, `pitch`, `yaw`, `current_phase_id`, `plant_time`, `growth_time`, `phase_time`, `freshness_time`, `scale`, `data`) " +
            "VALUES(@id, @source_template_id, @template_id, @x, @y, @z, @roll, @pitch, @yaw, @current_phase_id, @plant_time, @growth_time, @phase_time, @freshness_time, @scale, @data)";
        command.Parameters.AddWithValue("@id", DbId);
        command.Parameters.AddWithValue("@source_template_id", SourceTemplateId);
        command.Parameters.AddWithValue("@template_id", TemplateId);
        command.Parameters.AddWithValue("@x", x);
        command.Parameters.AddWithValue("@y", y);
        command.Parameters.AddWithValue("@z", z);
        command.Parameters.AddWithValue("@roll", Transform?.Local.Rotation.X ?? 0f);
        command.Parameters.AddWithValue("@pitch", Transform?.Local.Rotation.Y ?? 0f);
        command.Parameters.AddWithValue("@yaw", Transform?.Local.Rotation.Z ?? 0f);
        command.Parameters.AddWithValue("@current_phase_id", FuncGroupId);
        command.Parameters.AddWithValue("@plant_time", PlantTime);
        command.Parameters.AddWithValue("@growth_time", GrowthTime);
        command.Parameters.AddWithValue("@phase_time", PhaseTime);
        command.Parameters.AddWithValue("@freshness_time", FreshnessTime);
        command.Parameters.AddWithValue("@scale", Scale);
        command.Parameters.AddWithValue("@data", Data);
    }

    private static decimal RoundWorldCoord(float value)
    {
        return decimal.Round((decimal)value, 3, MidpointRounding.AwayFromZero);
    }

    private uint GetParentDoodadId()
    {
        if (Transform?.Parent?.GameObject is Doodad pDoodad && pDoodad.DbId > 0)
            return pDoodad.DbId;

        return 0u;
    }

    public void DoDespawn(Doodad doodad)
    {
        Spawner.DoDespawn(doodad);
    }

    public override bool AllowRemoval()
    {
        foreach (var child in Transform.Children)
        {
            if (child.GameObject is Doodad { IsPersistent: true })
                return false;
        }

        return base.AllowRemoval();
    }

    public virtual ulong GetItemContainerId()
    {
        return 0;
    }

    public PacketStream WriteFishFinderUnit(PacketStream stream)
    {
        stream.WriteBc(ObjId);
        stream.Write(Template.Id);
        stream.WritePosition(Transform.World.Position);

        return stream;
    }

    /// <summary>
    /// Обработка отложенных пакетов и сохранений
    /// </summary>
    public static void ProcessPendingOperations(bool forceSave = false)
    {
        while (_pendingBroadcasts.TryDequeue(out var item))
        {
            item.Doodad.BroadcastPacket(new SCDoodadPhaseChangedPacket(item.Doodad), true);
        }

        FlushSaves(forceSave);
    }

    /// <summary>
    /// Сохранение грязных doodad'ов
    /// </summary>
    private static void FlushSaves(bool forceSave = false)
    {
        var currentTick = Environment.TickCount64;
        if (!forceSave && currentTick - _lastSaveTick < 5000)
            return;

        foreach (var doodad in _dirtyDoodads.Values)
        {
            if (forceSave)
            {
                try
                {
                    doodad.Save();
                }
                finally
                {
                    Interlocked.Exchange(ref doodad._saveQueued, 0);
                }
            }
            else
            {
                ThreadPool.QueueUserWorkItem(s_SaveCallback, doodad);
                Interlocked.Exchange(ref doodad._saveQueued, 0);
            }
        }
        _dirtyDoodads.Clear();
        Interlocked.Exchange(ref _lastSaveTick, currentTick);
    }
}
