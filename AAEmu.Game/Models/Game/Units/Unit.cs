using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Connections;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Error;
using AAEmu.Game.Models.Game.Expeditions;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Plots;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units.Route;
using AAEmu.Game.Models.Tasks;
using AAEmu.Game.Models.Tasks.Skills;

using NLog;

namespace AAEmu.Game.Models.Game.Units
{
    public class Unit : BaseUnit
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();
        private Task _regenTask;
        private Task _comboTask;

        public uint ModelId { get; set; }
        public byte Level { get; set; }
        public int Hp { get; set; }
        public virtual int MaxHp { get; set; }
        public virtual int HpRegen { get; set; }
        public virtual int PersistentHpRegen { get; set; }
        public int Mp { get; set; }
        public virtual int MaxMp { get; set; }
        public virtual int MpRegen { get; set; }
        public virtual int PersistentMpRegen { get; set; }
        public virtual float LevelDps { get; set; }
        public virtual int Dps { get; set; }
        public virtual int DpsInc { get; set; }
        public virtual int OffhandDps { get; set; }
        public virtual int RangedDps { get; set; }
        public virtual int RangedDpsInc { get; set; }
        public virtual int MDps { get; set; }
        public virtual int MDpsInc { get; set; }
        public virtual int Armor { get; set; }
        public virtual int MagicResistance { get; set; }
        public BaseUnit CurrentTarget { get; set; }
        public virtual byte RaceGender => 0;
        public virtual UnitCustomModelParams ModelParams { get; set; }
        public byte ActiveWeapon { get; set; }
        public bool IdleStatus { get; set; }
        public bool ForceAttack { get; set; }
        public bool Invisible { get; set; }
        public uint OwnerId { get; set; }
        public SkillTask SkillTask { get; set; }
        public SkillTask AutoAttackTask { get; set; }
        public bool InCombo => _comboTask != null;
        public readonly ConcurrentDictionary<uint, (Unit unit, DateTime lastHit)> ComboUnits;
        public Dictionary<uint, List<Bonus>> Bonuses { get; set; }
        public Expedition Expedition { get; set; }
        public bool IsInBattle { get; set; }
        public bool IsInPatrol { get; set; }
        public List<int> SummarizeDamage { get; set; }
        public bool IsAutoAttack { get; set; }

        //X2::DescType<X2::SkillIdTag,unsigned int,X2::DS2> castingSkillId;
        //X2::DescType<X2::PlotIdTag,unsigned int,X2::DS2> castingPlotId;
        //X2::DescType<X2::SkillIdTag,unsigned int,X2::DS2> channelingSkillId;
        //X2::DescType<X2::PlotIdTag,unsigned int,X2::DS2> channelingPlotId;

        public uint SkillId { get; set; }
        public ushort TlId { get; set; }
        public PlotStep Step { get; set; }
        public GameConnection Connection { get; set; }
        public Dictionary<uint, DateTime> Cooldowns { get; set; }
        public Item[] Equip { get; set; }
        public DateTime GlobalCooldown { get; set; }
        public int ActiveControllerId { get; set; }


        /// <summary>
        /// Unit巡逻
        /// Unit patrol
        /// 指明Unit巡逻路线及速度、是否正在执行巡逻等行为
        /// Indicates the route and speed of the Unit patrol, whether it is performing patrols, etc.
        /// </summary>
        public Patrol Patrol { get; set; }
        public Simulation Simulation { get; set; }

        private readonly object _doDieLock = new object();

        public Unit()
        {
            Bonuses = new Dictionary<uint, List<Bonus>>();
            Cooldowns = new Dictionary<uint, DateTime>();
            IsInBattle = false;
            SummarizeDamage = new List<int> { 0, 0, 0 };
            Name = "";
            Equip = new Item[28];
            _regenTask = null;
            _comboTask = null;
        }

        public virtual void ReduceCurrentHp(Unit attacker, int value)
        {

            Hp = Math.Max(Hp - value, 0);
            if (Hp <= 0)
            {
                StopRegen();
                DoDie(attacker);
                return;
            }

            StartRegen();
            BroadcastPacket(new SCUnitPointsPacket(ObjId, Hp, Hp > 0 ? Mp : 0), true);
        }
        public virtual void ReduceCurrentMp(Unit attacker, int value)
        {
            attacker.Mp = Math.Max(attacker.Mp - value, 0);
            StartRegen();
            BroadcastPacket(new SCUnitPointsPacket(attacker.ObjId, attacker.Hp, attacker.Hp > 0 ? attacker.Mp : 0), true);
        }

        public virtual void DoDie(Unit killer)
        {
            lock (_doDieLock)
            {
                switch (killer)
                {
                    case Npc npc:
                        {
                            if (npc.CurrentTarget == null)
                            {
                                return;
                            }

                            var currentTarget = (Unit)npc.CurrentTarget;
                            currentTarget.Hp = 0;
                            currentTarget.Mp = 0;
                            npc.BroadcastPacket(new SCUnitDeathPacket(currentTarget.ObjId, 1, npc), true);
                            npc.BroadcastPacket(new SCAiAggroPacket(npc.ObjId, 0), true);
                            npc.BroadcastPacket(new SCCombatClearedPacket(currentTarget.ObjId), true);
                            npc.BroadcastPacket(new SCCombatClearedPacket(npc.ObjId), true);
                            npc.BroadcastPacket(new SCTargetChangedPacket(npc.ObjId, 0), true);

                            var character = (Character)npc.CurrentTarget;
                            character.SummarizeDamage[0] = 0;
                            character.StopRegen();
                            character.StopCombo(true);
                            character.Effects.RemoveEffectsOnDeath();
                            character.StopAutoSkill();
                            character.IsInBattle = false; // we need the character to be "not in battle"
                            character.DeadTime = DateTime.Now;
                            npc.CurrentTarget = null;
                            return;
                        }

                    case Character character:
                        {
                            if (character.CurrentTarget == null)
                            {
                                return;
                            }

                            var currentTarget = (Unit)character.CurrentTarget;
                            character.StopCombo(true);
                            character.StopAutoSkill();
                            currentTarget.StopRegen();
                            currentTarget.Effects.RemoveEffectsOnDeath();
                            currentTarget.Hp = 0;
                            currentTarget.Mp = 0;
                            character.SummarizeDamage[0] = 0;
                            character.BroadcastPacket(new SCUnitDeathPacket(currentTarget.ObjId, 1, character), true);

                            var lootDropItems = ItemManager.Instance.CreateLootDropItems(currentTarget.ObjId);
                            if (lootDropItems.Count > 0)
                            {
                                character.BroadcastPacket(new SCLootableStatePacket(currentTarget.ObjId, true), true);
                            }

                            character.BroadcastPacket(new SCAiAggroPacket(currentTarget.ObjId, 0), true);
                            character.BroadcastPacket(new SCCombatClearedPacket(currentTarget.ObjId), true);
                            character.BroadcastPacket(new SCCombatClearedPacket(character.ObjId), true);
                            character.BroadcastPacket(new SCTargetChangedPacket(character.ObjId, 0), true);
                            character.BroadcastPacket(new SCTargetChangedPacket(currentTarget.ObjId, 0), true);
                            character.IsInBattle = false; // we need the character to be "not in battle"
                            character.CurrentTarget = null;
                            return;
                        }
                }
            }
        }

        private async void StopAutoSkill()
        {
            if (AutoAttackTask != null)
            {
                await AutoAttackTask.Cancel();
            }

            AutoAttackTask = null;
            IsAutoAttack = false; // turned off auto attack
            //BroadcastPacket(new SCSkillEndedPacket(TlId), true);
            //BroadcastPacket(new SCSkillStoppedPacket(ObjId, SkillId), true);
            //TlIdManager.Instance.ReleaseId(TlId);
        }


        public void StartRegen()
        {
            if (_regenTask != null || Hp >= MaxHp && Mp >= MaxMp || Hp <= 0)
            {
                return;
            }

            _regenTask = new UnitPointsRegenTask(this);
            TaskManager.Instance.Schedule(_regenTask, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        public async void StopRegen()
        {
            if (_regenTask == null)
            {
                return;
            }

            await _regenTask.Cancel();
            _regenTask = null;
        }

        public void StartCombo(Unit attacker = null)
        {
            if (_comboTask == null)
            {
                _comboTask = new UnitComboTask(this);
                TaskManager.Instance.Schedule(_comboTask, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
                if (this is Character character)
                {
                    character.SendPacket(new SCCombatEngagedPacket(ObjId));
                }
            }

            if (attacker == null)
            {
                return;
            }

            var temp = (attacker, DateTime.Now);
            lock (ComboUnits)
            {
                if (ComboUnits.ContainsKey(attacker.ObjId))
                {
                    ComboUnits[attacker.ObjId] = temp;
                }
                else
                {
                    ComboUnits.TryAdd(attacker.ObjId, temp);
                    if (attacker is Character character)
                    {
                        character.SendPacket(new SCCombatEngagedPacket(ObjId));
                    }
                }
            }
        }

        public async void StopCombo(bool force = false)
        {
            if (_comboTask == null)
            {
                return;
            }

            await _comboTask.Cancel();
            _comboTask = null;

            if (this is Character character)
            {
                character.SendPacket(new SCCombatClearedPacket(ObjId));
            }

            lock (ComboUnits)
            {
                foreach (var (unit, _) in ComboUnits.Values)
                {
                    if (force)
                    {
                        unit.TryRemoveComboUnit(ObjId);
                    }

                    if (unit is Character temp)
                    {
                        temp.SendPacket(new SCCombatClearedPacket(ObjId));
                    }
                }

                ComboUnits.Clear();
            }
        }

        public bool TryRemoveComboUnit(uint objId)
        {
            bool result;
            lock (ComboUnits)
            {
                result = ComboUnits.TryRemove(objId, out _);
            }

            return result;
        }

        public void SetInvisible(bool value)
        {
            Invisible = value;
            BroadcastPacket(new SCUnitInvisiblePacket(ObjId, Invisible), true);
        }

        public void SetForceAttack(bool value)
        {
            ForceAttack = value;
            BroadcastPacket(new SCForceAttackSetPacket(ObjId, ForceAttack), true);
        }

        public override void AddBonus(uint bonusIndex, Bonus bonus)
        {
            var bonuses = Bonuses.ContainsKey(bonusIndex) ? Bonuses[bonusIndex] : new List<Bonus>();
            bonuses.Add(bonus);
            Bonuses[bonusIndex] = bonuses;
        }

        public override void RemoveBonus(uint bonusIndex, UnitAttribute attribute)
        {
            if (!Bonuses.ContainsKey(bonusIndex))
            {
                return;
            }

            var bonuses = Bonuses[bonusIndex];
            foreach (var bonus in new List<Bonus>(bonuses))
            {
                if (bonus.Template != null && bonus.Template.Attribute == attribute)
                {
                    bonuses.Remove(bonus);
                }
            }
        }

        public List<Bonus> GetBonuses(UnitAttribute attribute)
        {
            var result = new List<Bonus>();
            if (Bonuses == null)
            {
                return result;
            }

            foreach (var bonuses in new List<List<Bonus>>(Bonuses.Values))
            {
                foreach (var bonus in new List<Bonus>(bonuses))
                {
                    if (bonus.Template != null && bonus.Template.Attribute == attribute)
                    {
                        result.Add(bonus);
                    }
                }
            }
            return result;
        }

        public void SendPacket(GamePacket packet)
        {
            Connection?.SendPacket(packet);
        }

        public void SendErrorMessage(ErrorMessageType type)
        {
            SendPacket(new SCErrorMsgPacket(type, 0, true));
        }

        public bool CheckSkillCooldownsOkay(SkillTemplate template)
        {
            if (GetSkillCooldown(template.Id, template.IgnoreGlobalCooldown) > 0)
            {
                return false;
            }

            //if (template.SkillControllerId > 0 && !CheckActiveController(template.SkillControllerId))
            //return false;

            return true;
        }

        public int GetSkillCooldown(uint skillId, bool ignoreGCD = false)
        {
            int maxCooldown = Math.Max(ignoreGCD ? 0 : ((TimeSpan)(GlobalCooldown - DateTime.Now)).Milliseconds, Cooldowns.ContainsKey(skillId) ? ((TimeSpan)(Cooldowns[skillId] - DateTime.Now)).Milliseconds : 0);
            return Math.Max(maxCooldown, 0);
        }

        public void UpdateSkillCooldown(SkillTemplate skillTemplate, int customCooldown = 0)
        {
            var cooldownToAdd = skillTemplate.CooldownTime;
            if (customCooldown > 0)
            {
                cooldownToAdd = customCooldown;
            }

            ActiveControllerId = skillTemplate.SkillControllerId;

            if (!Cooldowns.ContainsKey(skillTemplate.Id))
            {
                Cooldowns.Add(skillTemplate.Id, DateTime.Now.AddMilliseconds(cooldownToAdd));
            }
            else if (Cooldowns[skillTemplate.Id] < DateTime.Now)
            {
                Cooldowns[skillTemplate.Id] = DateTime.Now.AddMilliseconds(cooldownToAdd);
            }
            else
            {
                return;
            }

            UpdateGlobalCooldown(skillTemplate);
        }

        public void UpdateGlobalCooldown(SkillTemplate skillTemplate)
        {
            if (skillTemplate == null)
            {
                return;
            }

            if (!skillTemplate.DefaultGcd)
            {
                GlobalCooldown = DateTime.Now.AddMilliseconds(skillTemplate.CustomGcd);
            }
            else
            {
                GlobalCooldown = DateTime.Now.AddMilliseconds(1000); //TODO: GlobalCooldown Calculations
            }

            if (this is Character)
            {
                //((Character)this).SendPacket(new SCCooldownsPacket((Character)this));
            }
        }
    }
}
