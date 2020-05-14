using System;
using System.Collections.Generic;
using System.Linq;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Error;
using AAEmu.Game.Models.Game.Faction;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Skills.Effects;
using AAEmu.Game.Models.Game.Skills.Plots;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Tasks.Skills;
using AAEmu.Game.Utils;
using NLog;

namespace AAEmu.Game.Models.Game.Skills
{
    public class Skill
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();

        public uint Id { get; set; }
        //public string Name { get; set; }
        //public int Cost { get; set; }
        //public bool Desc { get; set; }
        //public int IconId { get; set; }
        //public bool Show { get; set; }
        //public int StartAnimId { get; set; }
        //public int FireAnimId { get; set; }
        //public int AbilityId { get; set; }
        //public uint ManaCost { get; set; }
        //public uint TimingId { get; set; }
        //public int WeaponSlotForAutoattackId { get; set; }
        //public uint CooldownTime { get; set; }
        //public uint CastingTime { get; set; }
        //public bool IgnoreGlobalCooldown { get; set; }
        //public byte EffectDelay { get; set; }
        //public byte EffectSpeed { get; set; }
        //public byte EffectRepeatCount { get; set; }
        //public byte EffectRepeatTick { get; set; }
        //public uint CategoryId { get; set; }
        //public int ActiveWeaponId { get; set; }
        //public int TargetTypeId { get; set; }
        //public int TargetSelectionId { get; set; }
        //public int TargetRelationId { get; set; }
        //public int TargetAreaCount { get; set; }
        //public int TargetAreaRadius { get; set; }
        //public bool TargetSiege { get; set; }
        //public int WeaponSlotForAngleId { get; set; }
        //public int TargetAngle { get; set; }
        //public int WeaponSlotForRangeId { get; set; }
        //public uint MinRange { get; set; }
        //public uint MaxRange { get; set; }
        //public bool KeepStealth { get; set; }
        //public bool StopAutoattack { get; set; }
        //public int Aggro { get; set; }
        //public int FxGroupId { get; set; }
        //public int ProjectileId { get; set; }
        //public bool CheckObstacle { get; set; }
        //public uint ChannelingTime { get; set; }
        //public uint ChannelingTick { get; set; }
        //public uint ChannelingMana { get; set; }
        //public int ChannelingAnimId { get; set; }
        //public int ChannelingTargetBuffId { get; set; }
        //public int TargetAreaAngle { get; set; }
        //public int AbilityLevel { get; set; }
        //public int ChannelingDoodadId { get; set; }
        //public int CooldownTagId { get; set; }
        //public int SkillControllerId { get; set; }
        //public int RepeatCount { get; set; }
        //public int RepeatTick { get; set; }
        //public int ToggleBuffId { get; set; }
        //public bool TargetDead { get; set; }
        //public int ChannelingBuffId { get; set; }
        //public int ReagentCorpseStatusId { get; set; }
        //public bool SourceDead { get; set; }
        //public int LevelStep { get; set; }
        //public uint ValidHeight { get; set; }
        //public uint TargetValidHeight { get; set; }
        //public bool SourceMount { get; set; }
        //public bool StopCastingOnBigHit { get; set; }
        //public bool StopChannelingOnBigHit { get; set; }
        //public bool AutoLearn { get; set; }
        //public bool NeedLearn { get; set; }
        //public int MainhandToolId { get; set; }
        //public int OffhandToolId { get; set; }
        //public int FrontAngle { get; set; }
        //public uint ManaLevelMd { get; set; }
        //public int TwohandFireAnimId { get; set; }
        //public int Unmount { get; set; }
        //public int DamageTypeId { get; set; }
        //public int AllowToPrisoner { get; set; }
        //public int MilestoneId { get; set; }
        //public int MatchAnimation { get; set; }
        //public int PlotId { get; set; }
        //public int UseAnimTime { get; set; }
        //public int StartAutoattack { get; set; }
        //public int ConsumeIp { get; set; }
        //public int SourceStun { get; set; }
        //public int TargetAlive { get; set; }
        //public int WebDesc { get; set; }
        //public int TargetWater { get; set; }
        //public int UseSkillCamera { get; set; }
        //public int ControllerCamera { get; set; }
        //public int CameraSpeed { get; set; }
        //public int ControllerCameraSpeed { get; set; }
        //public int CameraMaxDistance { get; set; }
        //public int CameraDuration { get; set; }
        //public int CameraAcceleration { get; set; }
        //public int CameraSlowDownDistance { get; set; }
        //public int CameraHoldZ { get; set; }
        //public int CastingInc { get; set; }
        //public int CastingCancelable { get; set; }
        //public int CastingDelayable { get; set; }
        //public int ChannelingCancelable { get; set; }
        //public int TargetOffsetAngle { get; set; }
        //public int TargetOffsetDistance { get; set; }
        //public int ActabilityGroupId { get; set; }
        //public int PlotOnly { get; set; }
        //public int PitchAngle { get; set; }
        //public int SkillControllerAtEnd { get; set; }
        //public int EndSkillController { get; set; }
        //public int StringInstrumentStartAnimId { get; set; }
        //public int PercussionInstrumentStartAnimId { get; set; }
        //public int TubeInstrumentStartAnimId { get; set; }
        //public int StringInstrumenFireAnimId { get; set; }
        //public int PercussionInstrumentFireFnimId { get; set; }
        //public int TubeInstrumenFireAnimId { get; set; }
        //public int OrUnitReqs { get; set; }
        //public int DefaultGcd { get; set; }
        //public int ShowTargetCastingTime { get; set; }
        //public int ValidHeightEdgeToEdge { get; set; }
        //public int KinkEquipSlotId { get; set; }
        //public int KinkBackpackTypeId { get; set; }
        //public uint KeepManaRegen { get; set; }
        //public uint CrimePoint { get; set; }
        //public bool IevelRuleNoConsideration { get; set; }
        //public bool UseWeaponCooldownTime { get; set; }
        //public bool SynergyIcon1Buffkind { get; set; }
        //public int SynergyIcon1Id { get; set; }
        //public bool SynergyIcon2Buffkind { get; set; }
        //public int SynergyIcon2Id { get; set; }
        //public int CombatDiceId { get; set; }
        //public bool CanActiveWeaponWithoutAnim { get; set; }
        //public uint CustomGcd { get; set; }
        //public bool CancelOngoingBuffs { get; set; }
        //public bool CancelOngoingBuffExceptionTagId { get; set; }
        //public bool SourceCannotUseWhileWalk { get; set; }
        //public bool SourceMountMate { get; set; }
        //public bool MatchAnimationCount { get; set; }
        //public int DalWieldFireAnimId { get; set; }
        //public bool AutoFire { get; set; }
        //public bool CheckTerrain { get; set; }
        //public bool TargetOnlyWater { get; set; }
        //public bool SourceNotSwim { get; set; }
        //public bool TargetPreoccupied { get; set; }
        //public bool StopChannelingOnStartSkill { get; set; }
        //public bool StopCastingByTurn { get; set; }
        //public bool TargetMyNpc { get; set; }
        //public int GainLifePoint { get; set; }
        //public bool TargetFishing { get; set; }
        //public bool SourceNoSlave { get; set; }
        //public bool AutoReuse { get; set; }
        //public int AutoReuseDelay { get; set; }
        //public bool Sourcenotcollided { get; set; }
        //public uint SkillPoints { get; set; }
        //public int DoodadHitFamily { get; set; }
        //public bool SensitiveOperation { get; set; }
        //public bool NameTr { get; set; }
        //public bool DescTr { get; set; }
        //public bool WebDescTr { get; set; }
        //public bool FirstReagentOnly { get; set; }
        //public bool SourceAlive { get; set; }
        //public int TargetDecalRadius { get; set; }
        //public int DoodadBundleId { get; set; }

        ////public bool BuildPlot { get; set; }
        ////public int ParsePlot { get; set; }
        ////public int Cast { get; set; }
        ////public int Channeling { get; set; }
        ////public int Stop { get; set; }
        ////public int Apply { get; set; }

        public SkillTemplate Template { get; set; }
        public uint TemplateId { get; set; }
        public byte Level { get; set; }
        public ushort TlId { get; set; } // Skill Id
        public ushort Pid { get; set; } // Plot Id

        public Skill()
        {
        }

        public Skill(SkillTemplate template)
        {
            Id = template.Id;
            Template = template;
            Level = 1;
        }

        public void Use(Unit caster, SkillCaster casterType, SkillCastTarget targetType, SkillObject skillObject = null)
        {
            var target = (BaseUnit)caster;

            if (skillObject == null)
            {
                skillObject = new SkillObject();
            }
            var effects = caster.Effects.GetEffectsByType(typeof(BuffTemplate));
            foreach (var effect in effects)
            {
                if (((BuffTemplate)effect.Template).RemoveOnStartSkill || ((BuffTemplate)effect.Template).RemoveOnUseSkill)
                {
                    effect.Exit();
                }
            }
            effects = caster.Effects.GetEffectsByType(typeof(BuffEffect));
            foreach (var effect in effects)
            {
                if (((BuffEffect)effect.Template).Buff.RemoveOnStartSkill || ((BuffEffect)effect.Template).Buff.RemoveOnUseSkill)
                {
                    effect.Exit();
                }
            }
            switch (Template.TargetType)
            {
                case SkillTargetType.Self:
                    {
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                            targetType.ObjId = target.ObjId;
                        break;
                    }
                case SkillTargetType.Friendly:
                    {
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget); // TODO ...
                            return;
                        }
                        if (caster.Faction.GetRelationState(target.Faction.Id) != RelationState.Friendly)
                        {
                            caster.SendErrorMessage(ErrorMessageType.FactionRelationAlreadyHostile);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.Party:
                    {
                        // TODO ...
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.Raid:
                    {
                        // TODO ...
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.Hostile:
                    {
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget); // TODO ...
                            return;
                        }
                        if (caster.Faction.GetRelationState(target.Faction.Id) != RelationState.Hostile)
                        {
                            caster.SendErrorMessage(ErrorMessageType.FactionRelationAlreadyFriendly);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.AnyUnit:
                    {
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.Pos:
                    {
                        var positionTarget = (SkillCastPositionTarget)targetType;
                        var positionUnit = new BaseUnit
                        {
                            Position = new Point(positionTarget.PosX, positionTarget.PosY, positionTarget.PosZ)
                            {
                                ZoneId = caster.Position.ZoneId,
                                WorldId = caster.Position.WorldId
                            },
                            Region = caster.Region
                        };
                        target = positionUnit;
                        break;
                    }
                case SkillTargetType.Line:
                    {
                        // TODO ...
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.Doodad:
                    {
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidDoodadTarget);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.Item:
                    {
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.Pet:
                    {
                        // TODO ...
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.BallisticPos:
                    {
                        // TODO ...
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.SummonPos:
                    {
                        // TODO ...
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.RelativePos:
                    {
                        // TODO ...
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.SourcePos:
                    {
                        // TODO ...
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.ArtilleryPos:
                    {
                        // TODO ...
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.Others:
                    {
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget); // TODO ...
                            return;
                        }
                        if (caster.ObjId == target.ObjId)
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.FriendlyOthers:
                    {
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget); // TODO ...
                            return;
                        }
                        if (caster.ObjId == target.ObjId)
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        if (caster.Faction.GetRelationState(target.Faction.Id) != RelationState.Friendly)
                        {
                            caster.SendErrorMessage(ErrorMessageType.FactionRelationAlreadyHostile);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.CursorPos:
                    {
                        // TODO ...
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        break;
                    }
                case SkillTargetType.GeneralUnit:
                    {
                        if (targetType.Type == SkillCastTargetType.Unit || targetType.Type == SkillCastTargetType.Doodad)
                        {
                            target = targetType.ObjId > 0 ? WorldManager.Instance.GetBaseUnit(targetType.ObjId) : caster;
                            targetType.ObjId = target.ObjId;
                        }
                        else
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget); // TODO ...
                            return;
                        }
                        if (caster.ObjId == target.ObjId)
                        {
                            caster.SendErrorMessage(ErrorMessageType.InvalidTarget);
                            return;
                        }
                        break;
                    }
                default:
                    {
                        caster.SendErrorMessage(ErrorMessageType.InvalidTarget); // TODO ...
                        return;
                    }
            }

            caster.SkillId = Id;
            //caster.IsInBattle = true;
            TlId = (ushort)TlIdManager.Instance.GetNextId();

            if (Template.Plot != null)
            {
                var eventTemplate = Template.Plot.EventTemplate;
                caster.Step = new PlotStep
                {
                    Event = eventTemplate,
                    Flag = 2
                };
                if (!eventTemplate.СheckСonditions(caster, casterType, target, targetType, skillObject))
                {
                    caster.Step.Flag = 0;
                }
                var res = true;
                if (caster.Step.Flag != 0)
                {
                    var callCounter = new Dictionary<uint, int> { { caster.Step.Event.Id, 1 } };
                    foreach (var evnt in eventTemplate.NextEvents)
                    {
                        res = res && BuildPlot(caster, casterType, target, targetType, skillObject, evnt, caster.Step, callCounter);
                    }
                }
                ParsePlot(caster, casterType, target, targetType, skillObject, caster.Step);
                //if (!res)
                //    return;
                StopPlotEvent(caster);
                Cast(caster, casterType, target, targetType, skillObject);
            }
            else
            {
                if (Template.CastingTime > 0)
                {
                    // Deffered Cast Skill
                    caster.BroadcastPacket(new SCSkillStartedPacket(Id, TlId, casterType, targetType, this, skillObject), true);
                    caster.SkillTask = new CastTask(this, caster, casterType, target, targetType, skillObject);
                    TaskManager.Instance.Schedule(caster.SkillTask, TimeSpan.FromMilliseconds(Template.CastingTime));
                }
                else if (caster is Character && (Id == 2 || Id == 3 || Id == 4) && !caster.IsAutoAttack)
                {
                    // MeleeCast with autoattack
                    caster.IsAutoAttack = true; // enable auto attack
                    caster.BroadcastPacket(new SCSkillStartedPacket(Id, TlId, casterType, targetType, this, skillObject), true);
                    caster.AutoAttackTask = new MeleeCastTask(this, caster, casterType, target, targetType, skillObject);
                    TaskManager.Instance.Schedule(caster.AutoAttackTask, TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(1000));
                }
                else
                {
                    // Instant Cast Skill
                    //caster.BroadcastPacket(new SCSkillStartedPacket(Id, TlId, casterType, targetType, this, skillObject), true);
                    Cast(caster, casterType, target, targetType, skillObject);
                }
            }
        }

        public bool BuildPlot(Unit caster, SkillCaster casterType, BaseUnit target, SkillCastTarget targetType, SkillObject skillObject, PlotNextEvent nextEvent, PlotStep baseStep, Dictionary<uint, int> counter)
        {
            if (counter.ContainsKey(nextEvent.Event.Id))
            {
                var nextCount = counter[nextEvent.Event.Id] + 1;
                if (nextCount > nextEvent.Event.Tickets)
                {
                    return true;
                }
                counter[nextEvent.Event.Id] = nextCount;
            }
            else
            {
                counter.Add(nextEvent.Event.Id, 1);
            }
            if (nextEvent.Delay > 0)
            {
                baseStep.Delay = nextEvent.Delay;
                caster.SkillTask = new PlotTask(this, caster, casterType, target, targetType, skillObject, nextEvent, counter);
                TaskManager.Instance.Schedule(caster.SkillTask, TimeSpan.FromMilliseconds(nextEvent.Delay));
                return false;
            }
            if (nextEvent.Speed > 0)
            {
                baseStep.Speed = nextEvent.Speed;
                caster.SkillTask = new PlotTask(this, caster, casterType, target, targetType, skillObject, nextEvent, counter);
                var dist = MathUtil.CalculateDistance(caster.Position, target.Position, true);
                TaskManager.Instance.Schedule(caster.SkillTask, TimeSpan.FromSeconds(dist / nextEvent.Speed));
                return false;
            }
            var step = new PlotStep
            {
                Event = nextEvent.Event,
                Flag = 2,
                Casting = nextEvent.Casting,
                Channeling = nextEvent.Channeling
            };
            foreach (var condition in nextEvent.Event.Conditions)
            {
                if (condition.Condition.Check(caster, casterType, target, targetType, skillObject))
                    continue;

                step.Flag = 0;
                break;
            }
            baseStep.Steps.AddLast(step);
            if (step.Flag == 0)
            {
                return true;
            }
            var res = true;
            foreach (var e in nextEvent.Event.NextEvents)
            {
                res = res && BuildPlot(caster, casterType, target, targetType, skillObject, e, step, counter);
            }
            return res;
        }

        public void ParsePlot(Unit caster, SkillCaster casterType, BaseUnit target, SkillCastTarget targetType, SkillObject skillObject, PlotStep step)
        {
            _log.Debug("Plot: StepId {0}, Flsg {1}", step.Event.Id, step.Flag);
            caster.Step = step;
            if (step.Flag != 0)
            {
                foreach (var eff in step.Event.Effects)
                {
                    var template = SkillManager.Instance.GetEffectTemplate(eff.ActualId, eff.ActualType);
                    switch (template)
                    {
                        case AggroEffect _:
                        case AcceptQuestEffect _:
                        case BubbleEffect _:
                        //case CinemaEffect _:
                        case SpecialEffect _:
                        case BuffEffect _:
                            caster.Step.Flag = 6; // чтобы в template.Apply был доступ
                            step.Flag = 6; // чтобы в template.Apply был доступ
                            break;
                        default:
                            caster.Step.Flag = 2; // чтобы в template.Apply был доступ
                            step.Flag = 2; // чтобы в template.Apply был доступ
                            break;
                    }
                    caster.TlId = TlId;

                    template?.Apply(caster, casterType, target, targetType, new CastPlot(caster.Step.Event.PlotId, TlId, caster.Step.Event.Id, Template.Id), this, skillObject, DateTime.Now);
                }
            }

            var time = (ushort)(step.Flag != 0 ? step.Delay / 10 + 10 : 0);
            var objId = step.Casting || step.Channeling ? caster.ObjId : 0;
            var casterPlotObj = new PlotObject(caster);
            var targetPlotObj = new PlotObject(target);
            caster.BroadcastPacket(new SCPlotEventPacket(TlId, caster.Step.Event.Id, Template.Id, casterPlotObj, targetPlotObj, objId, time, caster.Step.Flag), true);

            foreach (var st in step.Steps)
            {
                ParsePlot(caster, casterType, target, targetType, skillObject, st);
            }
        }

        public void Cast(Unit caster, SkillCaster casterType, BaseUnit target, SkillCastTarget targetType, SkillObject skillObject)
        {
            caster.SkillTask = null;

            if (Id == 2 || Id == 3 || Id == 4)
            {
                if (caster is Character && caster.CurrentTarget == null)
                {
                    StopSkill(caster);
                    return;
                }

                // Get a random number (from 0 to n)
                var value = Rand.Next(0, 1);
                // для skillId = 2
                // 87 (35) - удар наотмаш, chr
                //  2 (37) - удар сбоку, NPC
                //  3 (46) - удар сбоку, chr
                //  1 (35) - удар похож на 2 удар сбоку, NPC
                // 91 - удар сверху (немного справа)
                // 92 - удар наотмашь слева вниз направо
                //  0 - удар не наносится (расстояние большое и надо подойти поближе), f=1, c=15
                var effectDelay = new Dictionary<int, short> { { 0, 49 }, { 1, 45 } };
                var fireAnimId = new Dictionary<int, int> { { 0, 91 }, { 1, 92 } };
                var effectDelay2 = new Dictionary<int, short> { { 0, 49 }, { 1, 43 } };
                var fireAnimId2 = new Dictionary<int, int> { { 0, 1 }, { 1, 2 } };

                var trg = (Unit)target;
                var dist = MathUtil.CalculateDistance(caster.Position, trg.Position, true);

                _log.Warn("SCSkillFiredPacket ObjId: {0}, Id: {1}, TlId: {2}", caster.ObjId, Id, TlId);


                if (dist - 1 >= SkillManager.Instance.GetSkillTemplate(Id).MinRange && dist - 1 <= SkillManager.Instance.GetSkillTemplate(Id).MaxRange)
                {
                    caster.BroadcastPacket(caster is Character
                            ? new SCSkillFiredPacket(Id, TlId, casterType, targetType, this, skillObject, effectDelay[value], fireAnimId[value])    // character
                            : new SCSkillFiredPacket(Id, TlId, casterType, targetType, this, skillObject, effectDelay2[value], fireAnimId2[value]), // npc
                        true);
                }
                else
                {
                    caster.BroadcastPacket(caster is Character
                            ? new SCSkillFiredPacket(Id, TlId, casterType, targetType, this, skillObject, effectDelay[value], fireAnimId[value], false)     // character
                            : new SCSkillFiredPacket(Id, TlId, casterType, targetType, this, skillObject, effectDelay2[value], fireAnimId2[value], false),  // npc
                        true);

                    if (caster is Character chr)
                    {
                        chr.SendMessage("Target is too far ...");
                    }
                    return;
                }
            }
            else
            {
                caster.BroadcastPacket(new SCSkillFiredPacket(Id, TlId, casterType, targetType, this, skillObject), true);
            }

            if (Template.ChannelingTime > 0)
            {
                if (Template.ChannelingBuffId != 0)
                {
                    var buff = SkillManager.Instance.GetBuffTemplate(Template.ChannelingBuffId);
                    buff?.Apply(caster, casterType, target, targetType, new CastSkill(Template.Id, TlId), this, skillObject, DateTime.Now);
                }

                caster.SkillTask = new ChannelingTask(this, caster, casterType, target, targetType, skillObject);
                TaskManager.Instance.Schedule(caster.SkillTask, TimeSpan.FromMilliseconds(Template.ChannelingTime));
            }
            else
            {
                Channeling(caster, casterType, target, targetType, skillObject);
            }
        }

        public void Channeling(Unit caster, SkillCaster casterType, BaseUnit target, SkillCastTarget targetType, SkillObject skillObject)
        {
            if (caster == null)
            {
                return;
            }
            caster.SkillTask = null;
            if (Template.ChannelingBuffId != 0)
            {
                // убираем баффы
                caster.Effects.RemoveEffect(Template.ChannelingBuffId, Template.Id);
            }
            if (Template.ToggleBuffId != 0) // здесь делаем Баффы
            {
                // ставим баффы
                var buff = SkillManager.Instance.GetBuffTemplate(Template.ToggleBuffId);
                buff?.Apply(caster, casterType, target, targetType, new CastSkill(Template.Id, TlId), this, skillObject, DateTime.Now);
            }
            if (Template.EffectDelay > 0)
            {
                // Deffered Apply Skill Task
                TaskManager.Instance.Schedule(new ApplySkillTask(this, caster, casterType, target, targetType, skillObject), TimeSpan.FromMilliseconds(Template.EffectDelay));
            }
            else
            {
                // Instant Apply Skill Task
                Apply(caster, casterType, target, targetType, skillObject);
            }
        }

        public void Apply(Unit caster, SkillCaster casterType, BaseUnit targetSelf, SkillCastTarget targetType, SkillObject skillObject)
        {
            var cooldownEnd = DateTime.Now.AddMilliseconds(Template.CooldownTime);
            caster.Cooldowns[Id] = cooldownEnd;


            var targets = new List<BaseUnit>(); // TODO crutches
            if (Template.TargetAreaRadius > 0)
            {
                var obj = WorldManager.Instance.GetAround<BaseUnit>(targetSelf, Template.TargetAreaRadius);
                targets.AddRange(obj);
            }
            else
            {
                targets.Add(targetSelf);
            }

            var effectsToApply = new List<EffectToApply>();
            foreach (var effect in Template.Effects)
            {
                _log.Warn(effect.Template?.ToString());
                //var targets = GetTargets(caster, targetType, Template.TargetType, (SkillEffectTypes)effect.ApplicationMethodId);
                foreach (var target in targets)
                {
                    if (effect.StartLevel > caster.Level || effect.EndLevel < caster.Level)
                        continue;

                    if (effect.Friendly && !effect.NonFriendly && caster.Faction.GetRelationState(target.Faction.Id) != RelationState.Friendly)
                        continue;

                    if (!effect.Friendly && effect.NonFriendly && caster.Faction.GetRelationState(target.Faction.Id) != RelationState.Hostile)
                        continue;

                    if (effect.Front && !effect.Back && !MathUtil.IsFront(caster, target))
                        continue;

                    if (!effect.Front && effect.Back && MathUtil.IsFront(caster, target))
                        continue;

                    if (effect.SourceBuffTagId > 0 && !caster.Effects.CheckBuffs(SkillManager.Instance.GetBuffsByTagId(effect.SourceBuffTagId)))
                        continue;

                    if (effect.SourceNoBuffTagId > 0 && caster.Effects.CheckBuffs(SkillManager.Instance.GetBuffsByTagId(effect.SourceNoBuffTagId)))
                        continue;

                    if (effect.TargetBuffTagId > 0 && !target.Effects.CheckBuffs(SkillManager.Instance.GetBuffsByTagId(effect.TargetBuffTagId)))
                        continue;

                    if (effect.TargetNoBuffTagId > 0 && target.Effects.CheckBuffs(SkillManager.Instance.GetBuffsByTagId(effect.TargetNoBuffTagId)))
                        continue;

                    if (effect.Chance < 100 && Rand.Next(100) > effect.Chance)
                        continue;

                    if (caster is Character character && effect.ConsumeItemId != 0 && effect.ConsumeItemCount > 0)
                    {
                        if (effect.ConsumeSourceItem)
                        {
                            var item = ItemManager.Instance.Create(effect.ConsumeItemId, effect.ConsumeItemCount, 0);
                            var res = character.Inventory.AddItem(item);
                            if (res == null)
                            {
                                ItemIdManager.Instance.ReleaseId((uint)res.Id);
                                continue;
                            }

                            var tasks = new List<ItemTask>();
                            if (res.Id != item.Id)
                            {
                                tasks.Add(new ItemCountUpdate(res, item.Count));
                            }
                            else
                            {
                                tasks.Add(new ItemAdd(item));
                            }
                            character.SendPacket(new SCItemTaskSuccessPacket(ItemTaskType.SkillEffectConsumption, tasks, new List<ulong>()));
                        }
                        else
                        {
                            var inventory = character.Inventory.CheckItems(SlotType.Inventory, effect.ConsumeItemId, effect.ConsumeItemCount);
                            var equipment = character.Inventory.CheckItems(SlotType.Equipment, effect.ConsumeItemId, effect.ConsumeItemCount);
                            if (!(inventory || equipment))
                            {
                                continue;
                            }
                            var tasks = new List<ItemTask>();

                            if (inventory)
                            {
                                var items = character.Inventory.RemoveItem(effect.ConsumeItemId, effect.ConsumeItemCount);
                                foreach (var (item, count) in items)
                                {
                                    if (item.Count == 0)
                                    {
                                        tasks.Add(new ItemRemove(item));
                                    }
                                    else
                                    {
                                        tasks.Add(new ItemCountUpdate(item, -count));
                                    }
                                }
                            }
                            else if (equipment)
                            {
                                var item = character.Inventory.GetItemByTemplateId(effect.ConsumeItemId);
                                character.Inventory.RemoveItem(item, true);
                                tasks.Add(new ItemRemove(item));
                            }
                            character.SendPacket(new SCItemTaskSuccessPacket(ItemTaskType.SkillEffectConsumption, tasks, new List<ulong>()));
                        }
                    }
                    if (effect.Template == null)
                    {
                        continue;
                    }
                    var effectToApply = new EffectToApply(effect.Template, caster, casterType, target, targetType, new CastSkill(Template.Id, TlId), this, skillObject);
                    effectsToApply.Add(effectToApply);
                }
            }
            foreach (var effect in effectsToApply)
            {
                effect.Apply();
            }
            if (Template.ConsumeLaborPower > 0 && caster is Character chart)
            {
                chart.ChangeLabor((short)-Template.ConsumeLaborPower, Template.ActabilityGroupId);
            }
            caster.BroadcastPacket(new SCSkillEndedPacket(TlId), true);
            //TlIdManager.Instance.ReleaseId(TlId);
            //TlId = 0;
            if (Template.CastingTime > 0)
            {
                caster.BroadcastPacket(new SCSkillStoppedPacket(caster.ObjId, Template.Id), true);
            }
        }

        public void Stop(Unit caster)
        {
            if (Template.ChannelingBuffId != 0)
            {
                caster.Effects.RemoveEffect(Template.ChannelingBuffId, Template.Id);
            }

            if (Template.ToggleBuffId != 0)
            {
                caster.Effects.RemoveEffect(Template.ToggleBuffId, Template.Id);
            }
            caster.BroadcastPacket(new SCCastingStoppedPacket(TlId, 0), true);
            caster.BroadcastPacket(new SCSkillEndedPacket(TlId), true);
            caster.SkillTask = null;
            TlIdManager.Instance.ReleaseId(TlId);
            TlId = 0;
        }

        public void StopPlotEvent(Unit caster)
        {
            caster.BroadcastPacket(new SCPlotEndedPacket(TlId), true);
            caster.SkillTask = null;
            TlIdManager.Instance.ReleaseId(TlId);
            TlId = 0;
        }

        private async void StopSkill(Unit caster)
        {
            await caster.AutoAttackTask.Cancel();
            caster.BroadcastPacket(new SCSkillEndedPacket(TlId), true);
            //caster.BroadcastPacket(new SCSkillStoppedPacket(caster.ObjId, Id), true);
            caster.AutoAttackTask = null;
            caster.IsAutoAttack = false; // turned off auto attack
            TlIdManager.Instance.ReleaseId(TlId);
            TlId = 0;
        }
    }
}
