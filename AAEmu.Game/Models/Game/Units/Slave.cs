using System;
using System.Collections.Generic;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.AI_old;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Slaves;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.DoodadObj.Static;
using AAEmu.Game.Models.Game.Formulas;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Units.Static;
using AAEmu.Game.Models.Game.World;
using Jitter.Dynamics;
using NLog;

namespace AAEmu.Game.Models.Game.Units
{
    public class Slave : Unit
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();

        public override UnitTypeFlag TypeFlag { get; } = UnitTypeFlag.Slave;
        public uint Id { get; set; }
        public uint TemplateId { get; set; }
        public uint BondingObjId { get; set; } = 0;

        public SlaveTemplate Template { get; set; }
        // public Character Driver { get; set; }
        public Character Summoner { get; set; }
        public List<Doodad> AttachedDoodads { get; set; }
        public List<Slave> AttachedSlaves { get; set; }
        public Dictionary<AttachPointKind, Character> AttachedCharacters { get; set; }
        public DateTime SpawnTime { get; set; }
        public sbyte ThrottleRequest { get; set; }
        public sbyte Throttle { get; set; }
        public float Speed { get; set; }
        public sbyte SteeringRequest { get; set; }
        public sbyte Steering { get; set; }
        public float RotSpeed { get; set; }
        public short RotationZ { get; set; }
        public float RotationDegrees { get; set; }
        public AttachPointKind AttachPointId { get; set; } = AttachPointKind.System;
        public uint OwnerObjId { get; set; }
        public RigidBody RigidBody { get; set; }

        public Slave()
        {
            UnitType = BaseUnitType.Slave;
            WorldPos = new WorldPos();
            Position = new Point();
            Ai_old = new TransferAi(this, 500f);
        }

        #region Attributes

        [UnitAttribute(UnitAttribute.GlobalCooldownMul)]
        public override float GlobalCooldownMul
        {
            get
            {
                var res = CalculateWithBonuses(0, UnitAttribute.GlobalCooldownMul);

                return (int)(100000f / (res + 1000f));
            }
        }

        [UnitAttribute(UnitAttribute.Str)]
        public int Str
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.Str);
                var parameters = new Dictionary<string, double> { ["level"] = Level };
                var result = formula.Evaluate(parameters);
                var res = result;
                foreach (var item in Equipment.Items)
                    if (item is EquipItem equip)
                        res += equip.Str;
                res = CalculateWithBonuses(res, UnitAttribute.Str);

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.Dex)]
        public int Dex
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.Dex);
                var parameters = new Dictionary<string, double> { ["level"] = Level };
                var res = formula.Evaluate(parameters);
                foreach (var item in Equipment.Items)
                    if (item is EquipItem equip)
                        res += equip.Dex;
                res = CalculateWithBonuses(res, UnitAttribute.Dex);

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.Sta)]
        public int Sta
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.Sta);
                var parameters = new Dictionary<string, double> { ["level"] = Level };
                var res = formula.Evaluate(parameters);
                foreach (var item in Equipment.Items)
                    if (item is EquipItem equip)
                        res += equip.Sta;
                res = CalculateWithBonuses(res, UnitAttribute.Sta);

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.Int)]
        public int Int
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.Int);
                var parameters = new Dictionary<string, double> { ["level"] = Level };
                var res = formula.Evaluate(parameters);
                foreach (var item in Equipment.Items)
                    if (item is EquipItem equip)
                        res += equip.Int;
                res = CalculateWithBonuses(res, UnitAttribute.Int);

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.Spi)]
        public int Spi
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.Spi);
                var parameters = new Dictionary<string, double> { ["level"] = Level };
                var res = formula.Evaluate(parameters);
                foreach (var item in Equipment.Items)
                    if (item is EquipItem equip)
                        res += equip.Spi;
                res = CalculateWithBonuses(res, UnitAttribute.Spi);

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.Fai)]
        public int Fai
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.Fai);
                var parameters = new Dictionary<string, double> { ["level"] = Level };
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.Fai);

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.MaxHealth)]
        public override int MaxHp
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.MaxHealth);
                var parameters = new Dictionary<string, double>();
                parameters["level"] = Level;
                parameters["str"] = Str;
                parameters["dex"] = Dex;
                parameters["sta"] = Sta;
                parameters["int"] = Int;
                parameters["spi"] = Spi;
                parameters["fai"] = Fai;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.MaxHealth);

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.HealthRegen)]
        public override int HpRegen
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.HealthRegen);
                var parameters = new Dictionary<string, double>();
                parameters["level"] = Level;
                parameters["str"] = Str;
                parameters["dex"] = Dex;
                parameters["sta"] = Sta;
                parameters["int"] = Int;
                parameters["spi"] = Spi;
                parameters["fai"] = Fai;
                var res = formula.Evaluate(parameters);
                // res += Spi / 10;
                res = CalculateWithBonuses(res, UnitAttribute.HealthRegen);

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.PersistentHealthRegen)]
        public override int PersistentHpRegen
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave,
                    UnitFormulaKind.PersistentHealthRegen);
                var parameters = new Dictionary<string, double>();
                parameters["level"] = Level;
                parameters["str"] = Str;
                parameters["dex"] = Dex;
                parameters["sta"] = Sta;
                parameters["int"] = Int;
                parameters["spi"] = Spi;
                parameters["fai"] = Fai;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.PersistentHealthRegen);
                res /= 5;

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.MaxMana)]
        public override int MaxMp
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.MaxMana);
                var parameters = new Dictionary<string, double>();
                parameters["level"] = Level;
                parameters["str"] = Str;
                parameters["dex"] = Dex;
                parameters["sta"] = Sta;
                parameters["int"] = Int;
                parameters["spi"] = Spi;
                parameters["fai"] = Fai;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.MaxMana);

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.ManaRegen)]
        public override int MpRegen
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.ManaRegen);
                var parameters = new Dictionary<string, double>();
                parameters["level"] = Level;
                parameters["str"] = Str;
                parameters["dex"] = Dex;
                parameters["sta"] = Sta;
                parameters["int"] = Int;
                parameters["spi"] = Spi;
                parameters["fai"] = Fai;
                var res = formula.Evaluate(parameters);
                res += Spi / 10;
                res = CalculateWithBonuses(res, UnitAttribute.ManaRegen);

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.PersistentManaRegen)]
        public override int PersistentMpRegen
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave,
                    UnitFormulaKind.PersistentManaRegen);
                var parameters = new Dictionary<string, double>();
                parameters["level"] = Level;
                parameters["str"] = Str;
                parameters["dex"] = Dex;
                parameters["sta"] = Sta;
                parameters["int"] = Int;
                parameters["spi"] = Spi;
                parameters["fai"] = Fai;
                var res = formula.Evaluate(parameters);
                res /= 5; // TODO ...
                res = CalculateWithBonuses(res, UnitAttribute.PersistentManaRegen);

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.IncomingDamageMul)]
        public override float IncomingDamageMul
        {
            get
            {
                var res = 0d;
                res = CalculateWithBonuses(res, UnitAttribute.IncomingDamageMul);
                res = res / 1000;
                res = 1 + res;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.IncomingMeleeDamageMul)]
        public override float IncomingMeleeDamageMul
        {
            get
            {
                var res = 0d;
                res = CalculateWithBonuses(res, UnitAttribute.IncomingMeleeDamageMul);
                res = CalculateWithBonuses(res, UnitAttribute.IncomingDamageMul);
                res = res / 1000;
                res = 1 + res;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.IncomingRangedDamageMul)]
        public override float IncomingRangedDamageMul
        {
            get
            {
                var res = 0d;
                res = CalculateWithBonuses(res, UnitAttribute.IncomingRangedDamageMul);
                res = CalculateWithBonuses(res, UnitAttribute.IncomingDamageMul);
                res = res / 1000;
                res = 1 + res;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.IncomingSpellDamageMul)]
        public override float IncomingSpellDamageMul
        {
            get
            {
                var res = 0d;
                res = CalculateWithBonuses(res, UnitAttribute.IncomingSpellDamageMul);
                res = CalculateWithBonuses(res, UnitAttribute.IncomingDamageMul);
                res = res / 1000;
                res = 1 + res;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.CastingTimeMul)]
        public override float CastTimeMul
        {
            get
            {
                var res = 0d;
                res = CalculateWithBonuses(res, UnitAttribute.CastingTimeMul);
                res = (res + 1000.00000000) / 1000;
                return (float)Math.Max(res, 0f);
            }
        }

        [UnitAttribute(UnitAttribute.MeleeDamageMul)]
        public override float MeleeDamageMul
        {
            get
            {
                double res = 0f;
                res = CalculateWithBonuses(res, UnitAttribute.MeleeDamageMul);
                res = (res + 1000.00000000) / 1000;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.RangedDamageMul)]
        public override float RangedDamageMul
        {
            get
            {
                double res = 0f;
                res = CalculateWithBonuses(res, UnitAttribute.RangedDamageMul);
                res = (res + 1000.00000000) / 1000;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.SpellDamageMul)]
        public override float SpellDamageMul
        {
            get
            {
                double res = 0f;
                res = CalculateWithBonuses(res, UnitAttribute.SpellDamageMul);
                res = (res + 1000.00000000) / 1000;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.IncomingHealMul)]
        public override float IncomingHealMul
        {
            get
            {
                double res = 0f;
                res = CalculateWithBonuses(res, UnitAttribute.IncomingHealMul);
                res = (res + 1000.00000000) / 1000;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.HealMul)]
        public override float HealMul
        {
            get
            {
                double res = 0f;
                res = CalculateWithBonuses(res, UnitAttribute.HealMul);
                res = (res + 1000.00000000) / 1000;
                return (float)res;
            }
        }

        public override float LevelDps
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.LevelDps);
                var parameters = new Dictionary<string, double>();
                parameters["level"] = Level;
                parameters["str"] = Str;
                parameters["dex"] = Dex;
                parameters["sta"] = Sta;
                parameters["int"] = Int;
                parameters["spi"] = Spi;
                parameters["fai"] = Fai;
                parameters["ab_level"] = Level; // TODO : Make AbilityLevel
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
                var res = (weapon?.Dps ?? 0) * 1000f;
                res += Str / 5f * 1000f;
                res = (float)CalculateWithBonuses(res, UnitAttribute.MainhandDps);

                return (int)(res);
            }
        }

        [UnitAttribute(UnitAttribute.MeleeDpsInc)]
        public override int DpsInc
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.MeleeDpsInc);
                var parameters = new Dictionary<string, double>();
                parameters["level"] = Level;
                parameters["str"] = Str;
                parameters["dex"] = Dex;
                parameters["sta"] = Sta;
                parameters["int"] = Int;
                parameters["spi"] = Spi;
                parameters["fai"] = Fai;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.MeleeDpsInc);

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
                // res += Str / 10f;
                res = (float)CalculateWithBonuses(res, UnitAttribute.OffhandDps);

                return (int)(res * 1000);
            }
        }

        [UnitAttribute(UnitAttribute.RangedDps)]
        public override int RangedDps
        {
            get
            {
                var weapon = (Weapon)Equipment.GetItemBySlot((int)EquipmentItemSlot.Ranged);
                var res = (weapon?.Dps ?? 0) * 1000f;
                res += Dex / 5f * 1000f;
                res = (float)CalculateWithBonuses(res, UnitAttribute.RangedDps);

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.RangedDpsInc)]
        public override int RangedDpsInc
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.RangedDpsInc);
                var parameters = new Dictionary<string, double>();
                parameters["level"] = Level;
                parameters["str"] = Str;
                parameters["dex"] = Dex;
                parameters["sta"] = Sta;
                parameters["int"] = Int;
                parameters["spi"] = Spi;
                parameters["fai"] = Fai;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.RangedDpsInc);

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.SpellDps)]
        public override int MDps
        {
            get
            {
                var weapon = (Weapon)Equipment.GetItemBySlot((int)EquipmentItemSlot.Mainhand);
                var res = (weapon?.MDps ?? 0) * 1000f;
                res += Int / 5f * 1000f;
                res = (float)CalculateWithBonuses(res, UnitAttribute.SpellDps);

                return (int)(res);
            }
        }

        [UnitAttribute(UnitAttribute.SpellDpsInc)]
        public override int MDpsInc
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.SpellDpsInc);
                var parameters = new Dictionary<string, double>();
                parameters["level"] = Level;
                parameters["str"] = Str;
                parameters["dex"] = Dex;
                parameters["sta"] = Sta;
                parameters["int"] = Int;
                parameters["spi"] = Spi;
                parameters["fai"] = Fai;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.SpellDpsInc);

                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.HealDps)]
        public override int HDps
        {
            get
            {
                var weapon = (Weapon)Equipment.GetItemBySlot((int)EquipmentItemSlot.Mainhand);
                var res = (weapon?.HDps ?? 0) * 1000;
                res += Spi / 5f * 1000f;
                res = CalculateWithBonuses(res, UnitAttribute.HealDps);
                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.HealDpsInc)]
        public override int HDpsInc
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.HealDpsInc);
                var parameters = new Dictionary<string, double>();
                parameters["level"] = Level;
                parameters["spi"] = Spi;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.HealDpsInc);
                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.MeleeAntiMiss)]
        public override float MeleeAccuracy
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.MeleeAntiMiss);
                var parameters = new Dictionary<string, double>();
                parameters["str"] = Str; //Str not needed, but maybe we use later
                parameters["spi"] = Spi;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.MeleeAntiMiss);
                res = (1f - ((Facets / 10f) - res) * (1f / Facets)) * 100f;
                res = ((res + 100f) - Math.Abs((res - 100f))) / 2f;
                res = (Math.Abs(res) + res) / 2f;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.MeleeCritical)]
        public override float MeleeCritical
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.MeleeCritical);
                var parameters = new Dictionary<string, double>();
                parameters["str"] = Str; //Str not needed, but maybe we use later
                parameters["dex"] = Dex;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.MeleeCritical);
                res = res * (1f / Facets) * 100;
                res = res + (MeleeCriticalMul / 10);
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.MeleeCriticalBonus)]
        public override float MeleeCriticalBonus
        {
            get
            {
                var res = 1500f;
                res = (float)CalculateWithBonuses(res, UnitAttribute.MeleeCriticalBonus);
                return (res - 1000f) / 10f;
            }
        }

        [UnitAttribute(UnitAttribute.MeleeCriticalMul)]
        public override float MeleeCriticalMul
        {
            get
            {
                float res = 0;
                res = (float)CalculateWithBonuses(res, UnitAttribute.MeleeCriticalMul);
                return res;
            }
        }

        [UnitAttribute(UnitAttribute.RangedAntiMiss)]
        public override float RangedAccuracy
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.RangedAntiMiss);
                var parameters = new Dictionary<string, double>();
                parameters["dex"] = Dex; //Str not needed, but maybe we use later
                parameters["spi"] = Spi;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.RangedAntiMiss);
                res = (1f - ((Facets / 10f) - res) * (1f / Facets)) * 100f;
                res = ((res + 100f) - Math.Abs((res - 100f))) / 2f;
                res = (Math.Abs(res) + res) / 2f;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.RangedCritical)]
        public override float RangedCritical
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.RangedCritical);
                var parameters = new Dictionary<string, double>();
                parameters["dex"] = Dex; //Str not needed, but maybe we use later
                parameters["int"] = Int;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.RangedCritical);
                res = res * (1f / Facets) * 100;
                res = res + (RangedCriticalMul / 10);
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.RangedCriticalBonus)]
        public override float RangedCriticalBonus
        {
            get
            {
                var res = 1500f;
                res = (float)CalculateWithBonuses(res, UnitAttribute.RangedCriticalBonus);
                return (res - 1000f) / 10f;
            }
        }

        [UnitAttribute(UnitAttribute.RangedCriticalMul)]
        public override float RangedCriticalMul
        {
            get
            {
                float res = 0;
                res = (float)CalculateWithBonuses(res, UnitAttribute.RangedCriticalMul);
                return res;
            }
        }

        [UnitAttribute(UnitAttribute.SpellAntiMiss)]
        public override float SpellAccuracy
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.SpellAntiMiss);
                var parameters = new Dictionary<string, double>();
                parameters["int"] = Int;
                parameters["spi"] = Spi;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.SpellAntiMiss);
                res = (1f - ((Facets / 10f) - res) * (1f / Facets)) * 100f;
                res = ((res + 100f) - Math.Abs((res - 100f))) / 2f;
                res = (Math.Abs(res) + res) / 2f;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.SpellCritical)]
        public override float SpellCritical
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.SpellCritical);
                var parameters = new Dictionary<string, double>();
                parameters["int"] = Int; //Str not needed, but maybe we use later
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.SpellCritical);
                res = (float)CalculateWithBonuses(res, UnitAttribute.SpellDamageCritical);
                res = res * (1f / Facets) * 100;
                res = res + (SpellCriticalMul / 10);
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.SpellCriticalBonus)]
        public override float SpellCriticalBonus
        {
            get
            {
                var res = 1500f;
                res = (float)CalculateWithBonuses(res, UnitAttribute.SpellCriticalBonus);
                res = (float)CalculateWithBonuses(res, UnitAttribute.SpellDamageCriticalBonus);
                return (res - 1000f) / 10f;
            }
        }

        [UnitAttribute(UnitAttribute.SpellCriticalMul)]
        public override float SpellCriticalMul
        {
            get
            {
                double res = 0;
                res = CalculateWithBonuses(res, UnitAttribute.SpellCriticalMul);
                res = (float)CalculateWithBonuses(res, UnitAttribute.SpellDamageCriticalMul);
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.HealCritical)]
        public override float HealCritical
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.HealCritical);
                var parameters = new Dictionary<string, double>();
                parameters["spi"] = Spi; //Str not needed, but maybe we use later
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.HealCritical);
                res = res * (1f / Facets) * 100;
                res = res + (HealCriticalMul / 10);
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.HealCriticalBonus)]
        public override float HealCriticalBonus
        {
            get
            {
                var res = 1500f;
                res = (float)CalculateWithBonuses(res, UnitAttribute.HealCriticalBonus);
                return (res - 1000f) / 10f;
            }
        }

        [UnitAttribute(UnitAttribute.HealCriticalMul)]
        public override float HealCriticalMul
        {
            get
            {
                double res = 0;
                res = CalculateWithBonuses(res, UnitAttribute.HealCriticalMul);
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.Armor)]
        public override int Armor
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.Armor);
                var parameters = new Dictionary<string, double>();
                parameters["level"] = Level;
                parameters["str"] = Str;
                parameters["dex"] = Dex;
                parameters["sta"] = Sta;
                parameters["int"] = Int;
                parameters["spi"] = Spi;
                parameters["fai"] = Fai;
                var res = (int)formula.Evaluate(parameters);
                foreach (var item in Equipment.Items)
                {
                    switch (item)
                    {
                        case Armor armor:
                            res += armor.BaseArmor;
                            break;
                        case Weapon weapon:
                            res += weapon.Armor;
                            break;
                        case Accessory accessory:
                            res += accessory.BaseArmor;
                            break;
                    }
                }

                res = (int)CalculateWithBonuses(res, UnitAttribute.Armor);

                return res;
            }
        }

        [UnitAttribute(UnitAttribute.MagicResist)]
        public override int MagicResistance
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.MagicResist);
                var parameters = new Dictionary<string, double>();
                parameters["level"] = Level;
                parameters["str"] = Str;
                parameters["dex"] = Dex;
                parameters["sta"] = Sta;
                parameters["int"] = Int;
                parameters["spi"] = Spi;
                parameters["fai"] = Fai;
                var res = (int)formula.Evaluate(parameters);
                foreach (var item in Equipment.Items)
                {
                    switch (item)
                    {
                        case Armor armor:
                            res += armor.BaseMagicResistance;
                            break;
                        case Accessory accessory:
                            res += accessory.BaseMagicResistance;
                            break;
                    }
                }

                res = (int)CalculateWithBonuses(res, UnitAttribute.MagicResist);

                return res;
            }
        }

        [UnitAttribute(UnitAttribute.IgnoreArmor)]
        public override int DefensePenetration
        {
            get
            {
                var res = CalculateWithBonuses(0, UnitAttribute.IgnoreArmor);
                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.MagicPenetration)]
        public override int MagicPenetration
        {
            get
            {
                var res = CalculateWithBonuses(0, UnitAttribute.MagicPenetration);
                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.BattleResist)]
        public override int BattleResist
        {
            get
            {
                var res = (int)CalculateWithBonuses(0, UnitAttribute.BattleResist);
                return res;
            }
        }

        [UnitAttribute(UnitAttribute.BullsEye)]
        public override int BullsEye
        {
            get
            {
                var res = (int)CalculateWithBonuses(0, UnitAttribute.BullsEye);
                return res;
            }
        }

        [UnitAttribute(UnitAttribute.Flexibility)]
        public override int Flexibility
        {
            get
            {
                var res = (int)CalculateWithBonuses(0, UnitAttribute.Flexibility);
                return res;
            }
        }

        [UnitAttribute(UnitAttribute.Facets)]
        public override int Facets
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.Facet);
                var parameters = new Dictionary<string, double>();
                parameters["level"] = Level;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.Facets);
                return (int)res;
            }
        }

        [UnitAttribute(UnitAttribute.Dodge)]
        public override float DodgeRate
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.Dodge);
                var parameters = new Dictionary<string, double>();
                parameters["dex"] = Dex;
                parameters["int"] = Int;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.Dodge);
                res = (res * (1f / Facets) * 100f);
                res += CalculateWithBonuses(0f, UnitAttribute.DodgeMul) / 10f;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.MeleeParry)]
        public override float MeleeParryRate
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.MeleeParry);
                var parameters = new Dictionary<string, double>();
                parameters["str"] = Str;
                parameters["sta"] = Sta;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.MeleeParry);
                res = (res * (1f / Facets) * 100f);
                res += CalculateWithBonuses(0f, UnitAttribute.MeleeParryMul) / 10f;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.RangedParry)]
        public override float RangedParryRate
        {
            get
            {
                //RangedParry Formula == 0
                double res = 0;
                res = CalculateWithBonuses(res, UnitAttribute.RangedParry);
                res = (res * (1f / Facets) * 100f);
                res += CalculateWithBonuses(0f, UnitAttribute.RangedParryMul) / 10f;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.Block)]
        public override float BlockRate
        {
            get
            {
                var offhand = Equipment.GetItemBySlot((int)EquipmentItemSlot.Offhand);
                if (offhand != null && offhand.Template is WeaponTemplate template)
                {
                    var slotId = (EquipmentItemSlotType)template.HoldableTemplate.SlotTypeId;
                    if (slotId != EquipmentItemSlotType.Shield)
                        return 0f;
                }
                else if (offhand == null)
                    return 0f;
                var formula =
                    FormulaManager.Instance.GetUnitFormula(FormulaOwnerType.Slave, UnitFormulaKind.Block);
                var parameters = new Dictionary<string, double>();
                parameters["str"] = Str;
                var res = formula.Evaluate(parameters);
                res = CalculateWithBonuses(res, UnitAttribute.Block);
                res = (res * (1f / Facets) * 100f);
                res += CalculateWithBonuses(0f, UnitAttribute.BlockMul) / 10f;
                return (float)res;
            }
        }

        [UnitAttribute(UnitAttribute.LungCapacity)]
        public uint LungCapacity
        {
            get => (uint)CalculateWithBonuses(60000, UnitAttribute.LungCapacity);
        }

        [UnitAttribute(UnitAttribute.FallDamageMul)]
        public float FallDamageMul
        {
            get => (float)CalculateWithBonuses(1d, UnitAttribute.FallDamageMul);
        }

        #endregion
        
        public override void AddVisibleObject(Character character)
        {
            character.SendPacket(new SCUnitStatePacket(this));
            character.SendPacket(new SCUnitPointsPacket(ObjId, Hp, Mp, HighAbilityRsc));
            character.SendPacket(new SCSlaveStatePacket(ObjId, TlId, Summoner.Name, Summoner.ObjId, Template.Id));
        }

        public override void RemoveVisibleObject(Character character)
        {
            if (character.CurrentTarget != null && character.CurrentTarget == this)
            {
                character.CurrentTarget = null;
                character.SendPacket(new SCTargetChangedPacket(character.ObjId, 0));
            }

            character.SendPacket(new SCUnitsRemovedPacket(new[] {ObjId}));
        }
        
        public override void BroadcastPacket(GamePacket packet, bool self)
        {
            foreach (var character in WorldManager.Instance.GetAround<Character>(this))
                character.SendPacket(packet);
        }

        /// <summary>
        /// Moves a slave by X, Y & Z. Also moves attached slaves, doodads & driver
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void Move(float newX, float newY, float newZ)
        {
            var xD = newX - Position.X;
            var yD = newY - Position.Y;
            var zD = newZ - Position.Z;
            SetPosition(newX, newY, newZ);
            
            foreach (var doodad in AttachedDoodads)
            {
                doodad.SetPosition(doodad.Position.X + xD, doodad.Position.Y + yD, doodad.Position.Z + zD);
            }
            
            foreach (var attachedSlave in AttachedSlaves)
            {
                attachedSlave.Move(newX, newY, newZ);
            }
            
            // Driver?.SetPosition(Driver.Position.X + xD, Driver.Position.Y + yD, Driver.Position.Z + zD);
            foreach (var character in AttachedCharacters.Values)
            {
                character?.SetPosition(character.Position.X + xD, character.Position.Y + yD, character.Position.Z + zD);
            }
        }
    }
}
