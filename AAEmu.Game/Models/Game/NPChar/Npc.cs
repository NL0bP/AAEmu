using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.AI.AStar;
using AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;
using AAEmu.Game.Models.Game.AI.v2.Framework;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Formulas;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.SkillControllers;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Game.Units.Static;
using AAEmu.Game.Models.StaticValues;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.NPChar;

public partial class Npc : Unit
{
    public override UnitTypeFlag TypeFlag { get; } = UnitTypeFlag.Npc;
    public override BaseUnitType BaseUnitType => BaseUnitType.Npc;
    public override ModelPostureType ModelPostureType { get => AnimActionId > 0 ? ModelPostureType.ActorModelState : ModelPostureType.None; }

    //public uint TemplateId { get; set; } // moved to BaseUnit
    public NpcTemplate Template { get; set; }
    //public Item[] Equip { get; set; }
    public NpcSpawner Spawner { get; set; }
    public bool IsDespawnScheduled { get; set; } = false;

    public override UnitCustomModelParams ModelParams => Template.ModelParams;

    /// <summary>
    /// This is the "Idle Animation Id" that is used in UnitModelChangePosture, it can change depending on the time of the day
    /// </summary>
    public uint AnimActionId
    {
        get
        {
            switch (Template.NpcPostureSets.Count)
            {
                // If no postures, just return 0
                case 0:
                    return 0;
                // If only one, always return that one
                case 1:
                    return Template.NpcPostureSets.FirstOrDefault()?.AnimActionId ?? 0;
                default:
                    {
                        // If more than one, we need to grab the Time of Day first
                        var myTime = TimeManager.Instance.GetTime;
                        return Template.NpcPostureSets.FirstOrDefault(x => x.StartTodTime <= myTime)?.AnimActionId ?? 0;
                    }
            }
        }
    }

    public override float Scale => Template.Scale;

    public override byte RaceGender => (byte)(16 * Template.Gender + Template.Race);

    public NpcAi Ai { get; set; } // New framework
    public ConcurrentDictionary<uint, Aggro> AggroTable { get; }

    public BaseUnit CurrentAggroTarget
    {
        get => _currentAggroTarget;
        set
        {
            if (_currentAggroTarget == value)
                return;

            if (value != null)
                SendPacketToPlayers([value], new SCAggroTargetChangedPacket(ObjId, value.ObjId));
            // BroadcastPacket(new SCAggroTargetChangedPacket(ObjId, value.ObjId), false);

            _currentAggroTarget = value;
        }
    }

    public bool CanFly { get; set; } // TODO: mark NPCs that can fly so that they don't land on the ground when calculating the Z height

    /// <summary>
    /// Tagging works differently to Aggro and has its own system 
    /// </summary>
    public Tagging CharacterTagging { get; set; }


    public override float BaseMoveSpeed
    {
        get
        {
            var model = ModelManager.Instance.GetActorModel(Template.ModelId);
            if (model == null)
                return 1f;
            // TODO: Implement stance switching mechanic
            if (!model.Stances.TryGetValue(CurrentGameStance, out var stance))
                return 1f;

            // Returning? Use sprint speed
            if (Ai?.GetCurrentBehavior() is ReturnStateBehavior rsb)
                return stance.AiMoveSpeedSprint;

            // In combat, use running speed
            if (IsInBattle)
                return Math.Min(stance.AiMoveSpeedRun, stance.MaxSpeed);

            // Not in combat (should be roaming), use walk speed
            return Math.Min(stance.AiMoveSpeedWalk, stance.MaxSpeed);
        }
    }

    private GameStanceType _currentGameStance = GameStanceType.Combat;
    private BaseUnit _currentAggroTarget;

    public GameStanceType CurrentGameStance
    {
        get => _currentGameStance;
        set
        {
            if (_currentGameStance == value)
                return;

            if (CanFly)
            {
                _currentGameStance = GameStanceType.Fly;
                return;
            }

            if (IsUnderWater)
            {
                _currentGameStance = value == GameStanceType.Combat ? GameStanceType.CoSwim : GameStanceType.Swim;
                return;
            }

            _currentGameStance = value;
        }
    }

    public MoveTypeAlertness CurrentAlertness { get; set; }

    #region Attributes
    [UnitAttribute(UnitAttribute.Str)]
    public int Str
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.Str);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            //parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            //parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            parameters["heir_level"] = Template.HeirLevel;
            var res = (int)formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.Str))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }

            return res;
        }
    }

    [UnitAttribute(UnitAttribute.Dex)]
    public int Dex
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.Dex);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            //parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            //parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            parameters["heir_level"] = Template.HeirLevel;
            var res = (int)formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.Dex))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }
            return res;
        }
    }

    [UnitAttribute(UnitAttribute.Sta)]
    public int Sta
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.Sta);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            //parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            //parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            parameters["heir_level"] = Template.HeirLevel;
            var res = (int)formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.Sta))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }
            return res;
        }
    }

    [UnitAttribute(UnitAttribute.Int)]
    public int Int
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.Int);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            //parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            //parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            parameters["heir_level"] = Template.HeirLevel;
            var res = (int)formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.Int))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }
            return res;
        }
    }

    [UnitAttribute(UnitAttribute.Spi)]
    public int Spi
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.Spi);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            //parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            //parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            parameters["heir_level"] = Template.HeirLevel;
            var res = (int)formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.Spi))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }
            return res;
        }
    }

    [UnitAttribute(UnitAttribute.Fai)]
    public int Fai
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.Fai);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            //parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            //parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            //parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            var res = (int)formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.Fai))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }
            return res;
        }
    }

    [UnitAttribute(UnitAttribute.MaxHealth)]
    public override int MaxHp
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.MaxHealth);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            //parameters["str"] = Str;
            //parameters["dex"] = Dex;
            parameters["sta"] = Sta;
            //parameters["int"] = Int;
            //parameters["spi"] = Spi;
            //parameters["fai"] = Fai;
            parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            parameters["heir_level"] = Template.HeirLevel;
            var res = (int)formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.MaxHealth))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }
            return res;
        }
    }

    [UnitAttribute(UnitAttribute.HealthRegen)]
    public override int HpRegen
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.HealthRegen);
            var parameters = new Dictionary<string, double>();
            //parameters["level"] = Level;
            //parameters["str"] = Str;
            //parameters["dex"] = Dex;
            //parameters["sta"] = Sta;
            //parameters["int"] = Int;
            parameters["spi"] = Spi;
            //parameters["fai"] = Fai;
            //parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            //parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            //parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            var res = (int)formula.Evaluate(parameters);
            res += Spi / 10;
            foreach (var bonus in GetBonuses(UnitAttribute.HealthRegen))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }
            return res;
        }
    }

    [UnitAttribute(UnitAttribute.PersistentHealthRegen)]
    public override int PersistentHpRegen
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.PersistentHealthRegen);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            //parameters["str"] = Str;
            //parameters["dex"] = Dex;
            //parameters["sta"] = Sta;
            //parameters["int"] = Int;
            //parameters["spi"] = Spi;
            //parameters["fai"] = Fai;
            //parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            //parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            //parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            //parameters["heir_level"] = Template.HeirLevel;
            var res = (int)formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.PersistentHealthRegen))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }
            return res;
        }
    }

    [UnitAttribute(UnitAttribute.MaxMana)]
    public override int MaxMp
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.MaxMana);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            //parameters["str"] = Str;
            //parameters["dex"] = Dex;
            //parameters["sta"] = Sta;
            parameters["int"] = Int;
            //parameters["spi"] = Spi;
            //parameters["fai"] = Fai;
            //parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            parameters["heir_level"] = Template.HeirLevel;
            var res = (int)formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.MaxMana))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }
            return res;
        }
    }

    [UnitAttribute(UnitAttribute.ManaRegen)]
    public override int MpRegen
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.ManaRegen);
            var parameters = new Dictionary<string, double>();
            //parameters["level"] = Level;
            //parameters["str"] = Str;
            //parameters["dex"] = Dex;
            //parameters["sta"] = Sta;
            //parameters["int"] = Int;
            parameters["spi"] = Spi;
            //parameters["fai"] = Fai;
            //parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            //parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            //parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            var res = (int)formula.Evaluate(parameters);
            res += Spi / 10;
            foreach (var bonus in GetBonuses(UnitAttribute.ManaRegen))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }
            return res;
        }
    }

    [UnitAttribute(UnitAttribute.PersistentManaRegen)]
    public override int PersistentMpRegen
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.PersistentManaRegen);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            //parameters["str"] = Str;
            //parameters["dex"] = Dex;
            //parameters["sta"] = Sta;
            //parameters["int"] = Int;
            //parameters["spi"] = Spi;
            //parameters["fai"] = Fai;
            //parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            //parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            //parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            var res = (int)formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.PersistentManaRegen))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }
            return res;
        }
    }

    public override float LevelDps
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.LevelDps);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            //parameters["str"] = Str;
            //parameters["dex"] = Dex;
            //parameters["sta"] = Sta;
            //parameters["int"] = Int;
            //parameters["spi"] = Spi;
            //parameters["fai"] = Fai;
            parameters["ab_level"] = 0;
            parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            parameters["heir_level"] = Template.HeirLevel;
            var res = formula.Evaluate(parameters);
            return (float)res;
        }
    }

    [UnitAttribute(UnitAttribute.MainhandDps)]
    public override int Dps
    {
        get
        {
            var weapon = (Weapon)Equipment.GetItemBySlot((int)EquipmentItemSlot.Mainhand);
            var res = weapon?.Dps ?? 0;
            res += Str / 10f;
            foreach (var bonus in GetBonuses(UnitAttribute.MainhandDps))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }

            return (int)(res * 1000);
        }
    }

    [UnitAttribute(UnitAttribute.MeleeDpsInc)]
    public override int DpsInc
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.MeleeDpsInc);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            parameters["str"] = Str;
            //parameters["dex"] = Dex;
            //parameters["sta"] = Sta;
            //parameters["int"] = Int;
            //parameters["spi"] = Spi;
            //parameters["fai"] = Fai;
            parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            parameters["heir_level"] = Template.HeirLevel;
            var res = formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.MeleeDpsInc))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }

            return (int)res;
        }
    }

    [UnitAttribute(UnitAttribute.OffhandDps)]
    public override int OffhandDps
    {
        get
        {
            var weapon = (Weapon)Equipment.GetItemBySlot((int)EquipmentItemSlot.Offhand);
            var res = weapon?.Dps ?? 0;
            res += Str / 10f;
            foreach (var bonus in GetBonuses(UnitAttribute.OffhandDps))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }

            return (int)(res * 1000);
        }
    }

    [UnitAttribute(UnitAttribute.RangedDps)]
    public override int RangedDps
    {
        get
        {
            var weapon = (Weapon)Equipment.GetItemBySlot((int)EquipmentItemSlot.Ranged);
            var res = weapon?.Dps ?? 0;
            res += Dex / 10f;
            foreach (var bonus in GetBonuses(UnitAttribute.RangedDps))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }

            return (int)(res * 1000);
        }
    }

    [UnitAttribute(UnitAttribute.RangedDpsInc)]
    public override int RangedDpsInc
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.RangedDpsInc);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            //parameters["str"] = Str;
            parameters["dex"] = Dex;
            //parameters["sta"] = Sta;
            //parameters["int"] = Int;
            //parameters["spi"] = Spi;
            //parameters["fai"] = Fai;
            parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            parameters["heir_level"] = Template.HeirLevel;
            var res = formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.RangedDpsInc))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }

            return (int)res;
        }
    }

    [UnitAttribute(UnitAttribute.SpellDps)]
    public override int MDps
    {
        get
        {
            var weapon = (Weapon)Equipment.GetItemBySlot((int)EquipmentItemSlot.Mainhand);
            var res = weapon?.MDps ?? 0;
            res += Int / 10f;
            foreach (var bonus in GetBonuses(UnitAttribute.SpellDps))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }

            return (int)(res * 1000);
        }
    }

    [UnitAttribute(UnitAttribute.SpellDpsInc)]
    public override int MDpsInc
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.SpellDpsInc);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            //parameters["str"] = Str;
            //parameters["dex"] = Dex;
            //parameters["sta"] = Sta;
            parameters["int"] = Int;
            //parameters["spi"] = Spi;
            //parameters["fai"] = Fai;
            parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            parameters["heir_level"] = Template.HeirLevel;
            var res = formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.SpellDpsInc))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }

            return (int)res;
        }
    }

    [UnitAttribute(UnitAttribute.Armor)]
    public override int Armor
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.Armor);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            //parameters["str"] = Str;
            //parameters["dex"] = Dex;
            //parameters["sta"] = Sta;
            //parameters["int"] = Int;
            //parameters["spi"] = Spi;
            //parameters["fai"] = Fai;
            parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            parameters["heir_level"] = Template.HeirLevel;
            var res = (int)formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.Armor))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }
            return res;
        }
    }

    [UnitAttribute(UnitAttribute.MagicResist)]
    public override int MagicResistance
    {
        get
        {
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.MagicResist);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            //parameters["str"] = Str;
            //parameters["dex"] = Dex;
            //parameters["sta"] = Sta;
            //parameters["int"] = Int;
            //parameters["spi"] = Spi;
            //parameters["fai"] = Fai;
            parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            parameters["heir_level"] = Template.HeirLevel;
            var res = (int)formula.Evaluate(parameters);
            foreach (var bonus in GetBonuses(UnitAttribute.MagicResist))
            {
                if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    res += (int)(res * bonus.Value / 100f);
                else
                    res += bonus.Value;
            }
            return res;
        }
    }

    public int KillExp
    {
        get
        {
            if (Template.NoExp)
                return 0;
            var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Npc, UnitFormulaKind.KillExp);
            var parameters = new Dictionary<string, double>();
            parameters["level"] = Level;
            //parameters["str"] = Str;
            //parameters["dex"] = Dex;
            //parameters["sta"] = Sta;
            //parameters["int"] = Int;
            //parameters["spi"] = Spi;
            //parameters["fai"] = Fai;
            //parameters["npc_template"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId);
            //parameters["npc_kind"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId);
            parameters["npc_grade"] = FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId);
            parameters["heir_level"] = Template.HeirLevel;
            var res = formula.Evaluate(parameters);
            res *= Template.ExpMultiplier;
            res += Template.ExpAdder;
            return (int)res;
        }
    }

    #endregion

    public Npc()
    {
        Name = "";
        AggroTable = new ConcurrentDictionary<uint, Aggro>();
        CharacterTagging = new Tagging(this); // Adding because Tagging works differently than Aggro
        //Equip = new Item[28];
    }

    public override void DoDie(BaseUnit killer, KillReason killReason)
    {
        var eligiblePlayers = GetEligiblePlayers();

        if (eligiblePlayers.Count == 0 && killer is Character singleKiller)
        {
            ProcessSingleKill(singleKiller);
        }
        else
        {
            ProcessTeamKill(eligiblePlayers);
        }

        base.DoDie(killer, killReason);
        ClearAllAggroTargetsAndCheckCombatState();
        CharacterTagging.ClearAllTaggers();
        CurrentAggroTarget = null;

        Spawner?.DecreaseCount(this);
        Ai?.GoToDead();
    }

    private HashSet<Character> GetEligiblePlayers()
    {
        var players = new HashSet<Character>();

        if (CharacterTagging.TagTeam != 0)
        {
            var team = TeamManager.Instance.GetActiveTeam(CharacterTagging.TagTeam);
            if (team != null)
            {
                foreach (var member in team.Members)
                {
                    if (member?.Character != null &&
                        member.Character.GetDistanceTo(this, true) <= Items.Containers.LootingContainer.MaxLootingRange)
                    {
                        players.Add(member.Character);
                    }
                }
            }
            else if (CharacterTagging.Tagger != null)
            {
                players.Add(CharacterTagging.Tagger);
            }
        }
        else if (CharacterTagging.Tagger != null)
        {
            players.Add(CharacterTagging.Tagger);
        }

        return players;
    }

    private void ProcessSingleKill(Character killerCharacter)
    {
        QuestManager.Instance.DoOnMonsterHuntEvents(killerCharacter, this);
        killerCharacter.AddExp(KillExp, true);

        var mates = MateManager.Instance.GetActiveMates(killerCharacter.ObjId);
        if (mates != null)
        {
            foreach (var mate in mates)
            {
                mate?.AddExp(KillExp);
                killerCharacter.SendDebugMessage($"Pet gained {KillExp} XP");
            }
        }
    }

    private void ProcessTeamKill(HashSet<Character> players)
    {
        var isFullTeam = false;
        var isRaid = false;

        if (CharacterTagging.TagTeam != 0)
        {
            var team = TeamManager.Instance.GetActiveTeam(CharacterTagging.TagTeam);
            if (team != null)
            {
                if (!team.IsParty)
                {
                    isRaid = true;
                }
                else if (team.MembersCount() > 3)
                {
                    isFullTeam = true;
                }
            }
        }

        foreach (var player in players)
        {
            var plKillXP = 0;
            var mateKillXP = 0;
            float plMod = 1f, mateMod = 1f;

            if (isRaid)
            {
                plMod = 0.33f;
                mateMod = 0.66f;
            }
            else if (isFullTeam)
            {
                plMod = mateMod = 0.66f;
            }
            else if (players.Count > 1 && players.Count <= 3)
            {
                plMod = (players.Count == 2) ? 0.90f : 0.875f;
                mateMod = plMod;
            }

            // Если уровень игрока отличается от уровня NPC более чем на 10 уровней — опыт не начисляется
            if (player.Level < this.Level - 10 || player.Level >= this.Level + 10)
            {
                // Не начислять опыт
            }
            else
            {
                var levelDiffFactor = 1.0f;
                var levelDifference = player.Level - this.Level;
                if (levelDifference > 0)
                {
                    levelDiffFactor = 1.0f - 0.1f * levelDifference;
                }
                else if (levelDifference < 0)
                {
                    levelDiffFactor = 1.0f + 0.1f * -levelDifference;
                }

                plKillXP = (int)(KillExp * plMod * levelDiffFactor);
                mateKillXP = (int)(KillExp * mateMod * levelDiffFactor);

                player.AddExp(plKillXP, true);

                var mates = MateManager.Instance.GetActiveMates(player.ObjId);
                if (mates != null)
                {
                    foreach (var mate in mates)
                    {
                        mate?.AddExp(mateKillXP);
                        player.SendDebugMessage($"Pet gained {mateKillXP} XP");
                    }
                }
            }

            QuestManager.Instance.DoOnMonsterHuntEvents(player, this);
        }
    }

    private void ClearAllAggroTargetsAndCheckCombatState()
    {
        List<Character> playerAggroList = [];
        // Generate a list of all player that we had aggro on
        foreach (var (objId, aggro) in AggroTable)
        {
            var unit = WorldManager.Instance.GetGameObject(objId);
            if (unit is Character player)
                playerAggroList.Add(player);
        }
        // Clear the aggro table
        AggroTable.Clear();

        // Check if those target players still have aggro on something else, if not, clear their combat timers
        foreach (var player in playerAggroList)
        {
            ClearAggroOfUnit(player);
            if (player.IsInAggroListOf.Count <= 0)
            {
                // Cancel combat
                player.IsInBattle = false;
            }
        }
    }

    public override void AddVisibleObject(Character character)
    {
        character.SendPacket(new SCUnitStatePacket(this));
        //character.SendPacket(new SCUnitPointsPacket(ObjId, Hp, Mp, HighAbilityRsc));

        base.AddVisibleObject(character);
    }

    public override void RemoveVisibleObject(Character character)
    {
        base.RemoveVisibleObject(character);

        character.SendPacket(new SCUnitsRemovedPacket([ObjId]));
    }

    public void AddUnitAggro(AggroKind kind, Unit unit, int amount)
    {
        if (unit == null)
            return;

        // Приведение к Character, если применимо
        var character = unit as Character;

        // Проверка, что ни NPC, ни цель не находятся под эффектами, предотвращающими бой
        var selfNoFight = Buffs.CheckBuffTag((uint)TagsEnum.NoFight) || Buffs.CheckBuffTag((uint)TagsEnum.Returning);
        var targetNoFight = unit.Buffs?.CheckBuffTag((uint)TagsEnum.NoFight) == true || unit.Buffs?.CheckBuffTag((uint)TagsEnum.Returning) == true;
        if (selfNoFight || targetNoFight)
        {
            ClearAggroOfUnit(unit);
            return;
        }

        // Если тип аггро – урон, добавляем теггинг
        if (kind == AggroKind.Damage)
        {
            CharacterTagging.AddTagger(unit, amount);
        }

        // Применяем модификаторы аггро (объединены в одну операцию)
        var modifier = (unit.AggroMul / 100.0f) * (IncomingAggroMul / 100.0f);
        amount = (int)(amount * modifier);

        // Попытка получить существующую запись аггро, либо создать новую
        if (!AggroTable.TryGetValue(unit.ObjId, out var aggro))
        {
            aggro = new Aggro(unit);
            aggro.AddAggro(kind, amount);
            if (AggroTable.TryAdd(unit.ObjId, aggro))
            {
                unit.Events.OnHealed += OnAbuserHealed;
                unit.Events.OnDeath += OnAbuserDied;
            }

            // Если у NPC задан идентификатор квеста, добавляем его игроку, если его еще нет
            if (Template.EngageCombatGiveQuestId > 0 && character != null)
            {
                if (!character.Quests.IsQuestComplete(Template.EngageCombatGiveQuestId) &&
                    !character.Quests.HasQuest(Template.EngageCombatGiveQuestId))
                {
                    character.Quests.AddQuest(Template.EngageCombatGiveQuestId);
                }
            }

            // Отправляем пакет начального удара
            unit.SendPacketToPlayers(new Unit[] { this, unit }, new SCCombatFirstHitPacket(this.ObjId, unit.ObjId, 0));
        }
        else
        {
            aggro.AddAggro(kind, amount);
        }

        // Если цель является игроком, добавляем NPC в его список аггро (при соблюдении условий)
        if (character != null)
        {
            if (aggro.TotalAggro > 0 && !IsDead && Hp > 0 && !character.IsInAggroListOf.ContainsKey(this.ObjId))
            {
                character.IsInAggroListOf.Add(this.ObjId, this);
            }
            QuestManager.Instance.DoOnAggroEvents(character, this);
        }
    }

    public void ClearAggroOfUnit(Unit unit)
    {
        if (unit is null)
            return;

        // Удаляем NPC из списка аггро у персонажа, если цель является персонажем
        if (unit is Character player && player.IsInAggroListOf.ContainsKey(this.ObjId))
        {
            player.IsInAggroListOf.Remove(this.ObjId);
        }

        // Попытка удалить аггро для данного юнита
        if (AggroTable.TryRemove(unit.ObjId, out var removedAggro))
        {
            unit.Events.OnHealed -= OnAbuserHealed;
            unit.Events.OnDeath -= OnAbuserDied;
        }
        else
        {
            Logger.Warn("Failed to remove unit[{0}] aggro from NPC[{1}]", unit.ObjId, this.ObjId);
        }

        // Если таблица аггро стала пустой, инициируем возвращение NPC на позицию спавна
        if (AggroTable.IsEmpty)
        {
            CheckIfEmptyAggroToReturn(unit);
        }
    }

    //Tagging!

    private static void CheckIfEmptyAggroToReturn(IBaseUnit unit)
    {
        if (unit is not Npc npc)
            return;

        // If aggro table is empty, and too far from spawn, trigger a return to spawn effect.
        if (!npc.AggroTable.IsEmpty)
            return;

        if (npc.Ai != null)
        {
            var distanceToIdle = MathUtil.CalculateDistance(npc.Ai.IdlePosition, npc.Transform.World.Position, true);
            if (distanceToIdle > 4)
                npc.Ai.GoToReturn();
        }

        npc.IsInBattle = false;
    }

    private void CheckIfEmptyAggroToReturn()
    {
        // If aggro table is empty, and too far from spawn, trigger a return to spawn effect.
        if (AggroTable.IsEmpty)
        {
            if (Ai != null)
            {
                var distanceToIdle = MathUtil.CalculateDistance(Ai.IdlePosition, Ai.Owner.Transform.World.Position, true);
                if (distanceToIdle > 4)
                    Ai.GoToReturn();
            }

            IsInBattle = false;
        }
    }

    public void ClearAllAggro()
    {
        // Сначала очищаем информацию о теггерах.
        CharacterTagging.ClearAllTaggers();

        // Получаем список идентификаторов юнитов, имеющих аггро.
        var unitIds = AggroTable.Keys.ToList();

        // Отписываем обработчики событий для каждого юнита.
        foreach (var id in unitIds)
        {
            var unit = WorldManager.Instance.GetUnit(id);
            if (unit != null)
            {
                unit.Events.OnHealed -= OnAbuserHealed;
                unit.Events.OnDeath -= OnAbuserDied;
            }
        }

        // Формируем список затронутых персонажей.
        var affectedPlayers = unitIds.Select(id => WorldManager.Instance.GetUnit(id))
            .OfType<Character>()
            .Distinct()
            .ToList();

        // Очищаем таблицу аггро.
        AggroTable.Clear();

        // Для каждого персонажа очищаем аггро данного NPC и, если он больше не имеет аггро,
        // завершаем состояние боя.
        foreach (var player in affectedPlayers)
        {
            ClearAggroOfUnit(player);
            if (player.IsInAggroListOf.Count <= 0)
            {
                player.IsInBattle = false;
            }
        }

        // Если таблица аггро пуста (что должно быть всегда после Clear),
        // проверяем расстояние от позиции спавна и инициируем возврат NPC при необходимости.
        if (AggroTable.IsEmpty)
        {
            CheckIfEmptyAggroToReturn();
        }
    }

    public void OnAbuserHealed(object sender, OnHealedArgs args)
    {
        AddUnitAggro(AggroKind.Heal, args.Healer, args.HealAmount);
    }

    public void OnAbuserDied(object sender, OnDeathArgs args)
    {
        ClearAggroOfUnit(args.Victim);
    }

    public void OnDamageReceived(Unit attacker, int amount)
    {
        // 25 means "dummy" AI -> should not respond!
        // if (Template.AiFileId != 25 && (Patrol == null || Patrol.PauseAuto(this)))
        // {
        //     CurrentTarget = attacker;
        //     BroadcastPacket(new SCCombatEngagedPacket(attacker.ObjId), true); // caster
        //     BroadcastPacket(new SCCombatEngagedPacket(ObjId), true);    // target
        //     BroadcastPacket(new SCCombatFirstHitPacket(ObjId, attacker.ObjId, 0), true);
        //     BroadcastPacket(new SCAggroTargetChangedPacket(ObjId, attacker.ObjId), true);
        //     BroadcastPacket(new SCTargetChangedPacket(ObjId, attacker.ObjId), true);
        //
        //     // TaskManager.Instance.Schedule(new UnitMove(new Track(), this), TimeSpan.FromMilliseconds(100));
        // }

        if (attacker == null || amount <= 0)
            return;

        // Увеличиваем аггро для атакующего
        AddUnitAggro(AggroKind.Damage, attacker, amount);

        // Уведомляем AI о возможном изменении цели
        Ai?.OnAggroTargetChanged();
        /*
            var topAbuser = AggroTable.GetTopTotalAggroAbuserObjId();
            if ((CurrentTarget?.ObjId ?? 0) != topAbuser)
            {
                CurrentAggroTarget = topAbuser;
                var unit = WorldManager.Instance.GetUnit(topAbuser);
                SetTarget(unit);
                Ai?.OnAggroTargetChanged();
            }
        */
    }

    /// <summary>
    /// Minimum distance to move the unit.
    /// </summary>
    private const float MinimumMovementDistance = 0.01f;
    /// <summary>
    /// Minimum distance to target.
    /// </summary>
    private const float MinimumTargetDistance = 1f;
    /// <summary>
    /// Tolerance for height adjustment.
    /// </summary>
    internal const float HeightTolerance = 1f; // порог допуска для корректировки высоты
    /// <summary>
    /// Coefficient for height adjustment.
    /// </summary>
    internal const float interpolationCoefficient = 0.5f; // коэффициент интерполяции

    public void MoveTowards(Vector3 other, float distance, byte actorFlags = 4)
    {

        distance *= Ai.Owner.MoveSpeedMul;
        if (distance < MinimumMovementDistance)
            return;

        var isImpaired = Buffs.HasEffectsMatchingCondition(e =>
                            e.Template.Stun ||
                            e.Template.Sleep ||
                            e.Template.Root ||
                            e.Template.Knockdown ||
                            e.Template.Fastened);
        if (isImpaired || Ai.Owner.IsDead)
            return;

        var isShackled = Ai.Owner.Buffs.CheckBuffs(SkillManager.Instance.GetBuffsByTagId((uint)SkillConstants.Shackle));
        var isSnared = Ai.Owner.Buffs.CheckBuffs(SkillManager.Instance.GetBuffsByTagId((uint)SkillConstants.Snare));
        if (isShackled || isSnared)
            return;

        if ((ActiveSkillController?.State ?? SkillController.SCState.Ended) == SkillController.SCState.Running)
            return;

        var oldPosition = Transform.Local.ClonePosition();
        var currentPosition = Transform.Local.Position;
        var targetDistance = MathUtil.CalculateDistance(currentPosition, other, true);
        if (targetDistance <= MinimumTargetDistance)
            return;

        var travelDistance = Math.Min(targetDistance, distance);
        var (newX, newY, newZ) = World.Transform.PositionAndRotation.AddDistanceToFront(travelDistance, targetDistance, currentPosition, other);
        Transform.Local.SetPosition(newX, newY, newZ);

        if (!CanFly)
        {
            // Используем высоту ландшафта, иначе высоту ближайшего персонажа, если он найден
            var referenceHeight = GetReferenceHeight(newX, newY);
            if (referenceHeight != 0 && Math.Abs(newZ - referenceHeight) < HeightTolerance)
            {
                newZ = Lerp(newZ, referenceHeight, interpolationCoefficient);
                currentPosition.Z = newZ;
                Transform.Local.SetHeight(newZ);
            }
            else
            {
                currentPosition.Z = referenceHeight;
                Transform.Local.SetHeight(referenceHeight);
            }
        }
        
        if (currentPosition.Z == 0f)
        {
            currentPosition.Z = Spawner.Position.Z;
            Transform.Local.SetHeight(Spawner.Position.Z);
        }

        var angle = MathUtil.CalculateAngleFrom(currentPosition, other);
        var (velX, velY) = MathUtil.AddDistanceToFront(4000, 0, 0, (float)angle.DegToRad());
        Transform.Local.SetRotationDegree(0f, 0f, (float)angle - 90);
        var (rx, ry, rz) = Transform.Local.ToRollPitchYawSBytesMovement();

        var moveType = (UnitMoveType)MoveType.GetType(MoveTypeEnum.Unit);
        moveType.X = Transform.Local.Position.X;
        moveType.Y = Transform.Local.Position.Y;
        moveType.Z = Transform.Local.Position.Z;
        moveType.VelX = (short)velX;
        moveType.VelY = (short)velY;
        moveType.RotationX = rx;
        moveType.RotationY = ry;
        moveType.RotationZ = rz;
        moveType.ActorFlags = actorFlags;
        moveType.Flags = MoveTypeFlags.Moving | (IsInBattle ? MoveTypeFlags.InCombat : 0);
        moveType.DeltaMovement = [0, 127, 0];
        moveType.Stance = CurrentGameStance;
        moveType.Alertness = CurrentAlertness;
        moveType.Time = (uint)(DateTime.UtcNow - DateTime.UtcNow.Date).TotalMilliseconds;

        CheckMovedPosition(oldPosition);
        BroadcastPacket(new SCOneUnitMovementPacket(ObjId, moveType), false);
    }

    public void LookTowards(Vector3 other, byte flags = 4)
    {
        var pos = Transform.Local.Position;
        // Replace the problematic line with the following code:
        var currentRotation = Transform.Local.Rotation.Z; // Accessing the Z rotation directly from the Rotation property
        // Вычисляем целевой угол поворота с поправкой
        var targetRotation = (float)(MathUtil.CalculateAngleFrom(pos, other) - 90);
        // Плавная интерполяция угла (коэффициент можно настроить)
        var newRotation = LerpAngle(currentRotation, targetRotation, 0.5f);
        Transform.Local.SetRotationDegree(0f, 0f, newRotation);
        var (rx, ry, rz) = Transform.Local.ToRollPitchYawSBytesMovement();
        if (!CanFly)
        {
            var newZ = pos.Z;
            // Используем высоту ландшафта, иначе высоту ближайшего персонажа, если он найден
            var referenceHeight = GetReferenceHeight(pos.X, pos.Y);
            if (referenceHeight != 0 && Math.Abs(newZ - referenceHeight) < HeightTolerance)
            {
                newZ = Lerp(newZ, referenceHeight, interpolationCoefficient);
                pos.Z = newZ;
                Transform.Local.SetHeight(newZ);
            }
            else
            {
                pos.Z = referenceHeight;
                Transform.Local.SetHeight(referenceHeight);
            }
        }

        if (pos.Z == 0f)
        {
            pos.Z = Spawner.Position.Z;
            Transform.Local.SetHeight(Spawner.Position.Z);
        }

        // Формирование пакета движения
        var moveType = (UnitMoveType)MoveType.GetType(MoveTypeEnum.Unit);
        moveType.X = pos.X;
        moveType.Y = pos.Y;
        moveType.Z = pos.Z;
        moveType.VelX = 0;
        moveType.VelY = 0;
        moveType.VelZ = 0;
        moveType.RotationX = rx;
        moveType.RotationY = ry;
        moveType.RotationZ = rz;
        moveType.ActorFlags = flags; // напр., 5 - ходьба, 4 - бег, 3 - стоять на месте
        moveType.Flags = MoveTypeFlags.Moving | (IsInBattle ? MoveTypeFlags.InCombat : 0);
        moveType.DeltaMovement = [0, 0, 0];
        moveType.Stance = 0; // COMBAT = 0, IDLE = 1
        moveType.Alertness = CurrentAlertness;
        moveType.Time = (uint)(DateTime.UtcNow - DateTime.UtcNow.Date).TotalMilliseconds;

        // Отправка пакета после проверки изменений
        CheckMovedPosition(Transform.Local.ClonePosition());
        BroadcastPacket(new SCOneUnitMovementPacket(ObjId, moveType), false);
    }

    /// <summary>
    /// Плавно интерполирует поворот с учётом "обёртки" углов (0-360 градусов).
    /// </summary>
    /// <param name="from">Начальный угол.</param>
    /// <param name="to">Целевой угол.</param>
    /// <param name="t">Коэффициент интерполяции от 0 до 1.</param>
    /// <returns>Новый угол.</returns>
    private float LerpAngle(float from, float to, float t)
    {
        // Находим минимальное изменение с учетом оборачивания
        var delta = ((to - from + 540) % 360) - 180;
        return from + delta * t;
    }

    public void StopMovement()
    {
        // Кэшируем текущую позицию
        var pos = Transform.Local.Position;
        var rollPitchYaw = Transform.Local.ToRollPitchYawSBytesMovement();
        if (!CanFly)
        {
            var newZ = pos.Z;
            // Используем высоту ландшафта, иначе высоту ближайшего персонажа, если он найден
            var referenceHeight = GetReferenceHeight(pos.X, pos.Y);
            if (referenceHeight != 0 && Math.Abs(newZ - referenceHeight) < HeightTolerance)
            {
                newZ = Lerp(newZ, referenceHeight, interpolationCoefficient);
                pos.Z = newZ;
                Transform.Local.SetHeight(newZ);
            }
            else
            {
                pos.Z = referenceHeight;
                Transform.Local.SetHeight(referenceHeight);
            }
        }
        
        if (pos.Z == 0f)
        {
            pos.Z = Spawner.Position.Z;
            Transform.Local.SetHeight(Spawner.Position.Z);
        }

        var moveType = (UnitMoveType)MoveType.GetType(MoveTypeEnum.Unit);
        moveType.X = pos.X;
        moveType.Y = pos.Y;
        moveType.Z = pos.Z;
        moveType.VelX = 0;
        moveType.VelY = 0;
        moveType.VelZ = 0;
        // Обнуляем вращение по X и Y, а для Z задаем сохранённое значение
        moveType.RotationX = 0;
        moveType.RotationY = 0;
        moveType.RotationZ = rollPitchYaw.Item3;
        moveType.Flags = MoveTypeFlags.Stopping | (IsInBattle ? MoveTypeFlags.InCombat : 0);
        moveType.DeltaMovement = [0, 0, 0];
        moveType.Stance = CurrentGameStance;
        moveType.Alertness = CurrentAlertness;
        moveType.Time = (uint)(DateTime.UtcNow - DateTime.UtcNow.Date).TotalMilliseconds;

        BroadcastPacket(new SCOneUnitMovementPacket(ObjId, moveType), false);
    }

    public override void OnSkillEnd(Skill skill)
    {
        // AI?.OnSkillEnd(skill);
    }

    public void SetTarget(Unit other)
    {
        CurrentTarget = other;
        BroadcastPacket(new SCTargetChangedPacket(ObjId, other?.ObjId ?? 0), true);
        Ai.AlreadyTargeted = other != null;
    }

    public void FindPath(Unit abuser)
    {
        if (abuser == null)
            return;

        // Получаем мировые позиции владельца и цели
        var ownerPos = Ai.Owner.Transform.World.Position;
        var targetPos = abuser.Transform.World.Position;

        // Создаем точки для начала и конца пути
        var startPoint = new Point(ownerPos.X, ownerPos.Y, ownerPos.Z);
        var endPoint = new Point(targetPos.X, targetPos.Y, targetPos.Z);

        // Настраиваем узел пути
        Ai.PathNode.pos1 = startPoint;
        Ai.PathNode.pos2 = endPoint;
        Ai.PathNode.ZoneKey = Ai.Owner.Transform.ZoneId;

        // Вычисляем путь от точки старта до точки назначения
        Ai.PathNode.findPath = Ai.PathNode.FindPath(startPoint, endPoint);

        var pathCount = Ai.PathNode.findPath?.Count ?? 0;
        Logger.Trace($"AStar: points found Total: {pathCount}");

        if (Ai.PathNode.findPath != null)
        {
            for (var i = 0; i < pathCount; i++)
            {
                var point = Ai.PathNode.findPath[i];
                Logger.Trace($"AStar: point {i} coordinates X:{point.X}, Y:{point.Y}, Z:{point.Z}");
            }
        }
    }

    /// <summary>
    /// Find the nearest point
    /// </summary>
    /// <param name="unit"></param>
    /// <returns>return coordinates and change the index of the current point</returns>
    public Vector3 GetClosestPoint(BaseUnit unit)
    {
        // TODO взять точку к которой движемся
        if (Ai.PathNode.findPath == null)
            return Vector3.Zero;

        var pos = new Point(unit.Transform.World.Position.X, unit.Transform.World.Position.Y, unit.Transform.World.Position.Z);
        Ai.PathNode.Current = AiGeoDataManager.FindClosestIndexPoint(Ai.PathNode.findPath, pos);

        return new Vector3(Ai.PathNode.findPath[(int)Ai.PathNode.Current].X, Ai.PathNode.findPath[(int)Ai.PathNode.Current].Y, Ai.PathNode.findPath[(int)Ai.PathNode.Current].Z);
    }

    public void DoDespawn(Npc npc)
    {
        Spawner.DoDespawn(npc);
    }

    /// <summary>
    /// Returns the ranking in this Npc's aggro table in percent
    /// </summary>
    /// <param name="objId"></param>
    /// <returns>Position in the aggro table ranking in percent, 0 = most aggro, 100 = no aggro</returns>
    public float GetAggroRatingInPercent(uint objId)
    {
        // grab a sorted copy of the aggro list
        var sortedAggro = AggroTable.OrderBy(x => x.Value.TotalAggro).ToList();

        // Find our position in the list
        var pos = 0;
        for (; pos < sortedAggro.Count; pos++)
        {
            if (sortedAggro[pos].Key == objId)
                break;
        }

        // If at the end of the list (not found), don't round anything, always return 100
        if (pos >= sortedAggro.Count)
            return 100f;

        // Return the position in the list 0 = most aggro, 100 = least aggro
        return 1f / sortedAggro.Count * pos;
    }
}
