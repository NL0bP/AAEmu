using System.Collections.Generic;

using AAEmu.Commons.Network;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Housing;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Shipyard;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCUnitStatePacket : GamePacket
    {
        private readonly Unit _unit;
        private readonly byte _baseUnitType;
        private byte _modelPostureType;
        private ushort _tlId;
        private uint _objId;
        private byte _attachPoint;

        public SCUnitStatePacket(ushort tlId, uint objId, Unit unit, byte attachPoint) : base(SCOffsets.SCUnitStatePacket, 1)
        {
            _tlId = tlId;
            _objId = objId;
            _unit = unit;
            _attachPoint = attachPoint;

            switch (_unit)
            {
                case Character _:
                    _baseUnitType = (byte)BaseUnitType.Character;
                    _modelPostureType = (byte)ModelPostureType.None;
                    break;
                case Npc npc:
                    {
                        _baseUnitType = (byte)BaseUnitType.Npc;
                        if (npc.Template.AnimActionId > 0)
                        {
                            _modelPostureType = (byte)ModelPostureType.ActorModelState;
                        }
                        else
                        {
                            _modelPostureType = (byte)ModelPostureType.None;
                        }

                        break;
                    }
                case Slave _:
                    _baseUnitType = (byte)BaseUnitType.Slave;
                    _modelPostureType = (byte)ModelPostureType.None; // было TurretState = 8
                    break;
                case House _:
                    _baseUnitType = (byte)BaseUnitType.Housing;
                    _modelPostureType = (byte)ModelPostureType.HouseState;
                    break;
                case Transfer _:
                    _baseUnitType = (byte)BaseUnitType.Transfer;
                    _modelPostureType = (byte)ModelPostureType.TurretState;
                    break;
                case Mount _:
                    _baseUnitType = (byte)BaseUnitType.Mate;
                    _modelPostureType = (byte)ModelPostureType.None;
                    break;
                case Shipyard _:
                    _baseUnitType = (byte)BaseUnitType.Shipyard;
                    _modelPostureType = (byte)ModelPostureType.None;
                    break;
            }


        }
        public SCUnitStatePacket(Unit unit) : base(SCOffsets.SCUnitStatePacket, 1)
        {
            _unit = unit;
            _attachPoint = 255;
            switch (_unit)
            {
                case Character _:
                    _baseUnitType = (byte)BaseUnitType.Character;
                    _modelPostureType = (byte)ModelPostureType.None;
                    break;
                case Npc npc:
                {
                    _baseUnitType = (byte)BaseUnitType.Npc;
                    if (npc.Template.AnimActionId > 0)
                    {
                        _modelPostureType = (byte)ModelPostureType.ActorModelState;
                    }
                    else
                    {
                        _modelPostureType = (byte)ModelPostureType.None;
                    }

                    break;
                }
                case Slave _:
                    _baseUnitType = (byte)BaseUnitType.Slave;
                    _modelPostureType = (byte)ModelPostureType.None; // было TurretState = 8
                    break;
                case House _:
                    _baseUnitType = (byte)BaseUnitType.Housing;
                    _modelPostureType = (byte)ModelPostureType.HouseState;
                    break;
                case Transfer _:
                    _baseUnitType = (byte)BaseUnitType.Transfer;
                    _modelPostureType = (byte)ModelPostureType.TurretState;
                    break;
                case Mount _:
                    _baseUnitType = (byte)BaseUnitType.Mate;
                    _modelPostureType = (byte)ModelPostureType.None;
                    break;
                case Shipyard _:
                    _baseUnitType = (byte)BaseUnitType.Shipyard;
                    _modelPostureType = (byte)ModelPostureType.None;
                    break;
            }
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.WriteBc(_unit.ObjId);
            stream.Write(_unit.Name);
            stream.Write(_baseUnitType);
            switch (_baseUnitType) // BaseUnitType?
            {
                case (byte)BaseUnitType.Character:
                    var character = (Character)_unit;
                    stream.Write(character.Id);     // type(id)
                    stream.Write(0L);               // v?
                    break;
                case (byte)BaseUnitType.Npc:
                    var npc = (Npc)_unit;
                    stream.WriteBc(npc.ObjId);
                    stream.Write(npc.TemplateId);   // npc templateId
                    stream.Write(0u);               // type(id)
                    stream.Write((byte)0);          // clientDriven
                    break;
                case (byte)BaseUnitType.Slave:
                    var slave = (Slave)_unit;
                    stream.Write(0u);               // type(id)
                    stream.Write(slave.TlId);       // tl
                    stream.Write(slave.TemplateId); // type(id)
                    stream.Write(0u);               // type(id)
                    break;
                case (byte)BaseUnitType.Housing:
                    var house = (House)_unit;
                    var buildStep = house.CurrentStep == -1
                        ? 0
                        : -house.Template.BuildSteps.Count + house.CurrentStep;

                    stream.Write(house.TlId);       // tl
                    stream.Write(house.TemplateId); // house templateId
                    stream.Write((short)buildStep); // buildstep
                    break;
                case (byte)BaseUnitType.Transfer:
                    var transfer = (Transfer)_unit;
                    stream.Write(transfer.TlId);       // tl
                    stream.Write(transfer.TemplateId); // transfer templateId
                    break;
                case (byte)BaseUnitType.Mate:
                    var mount = (Mount)_unit;
                    stream.Write(mount.TlId);       // tl
                    stream.Write(mount.TemplateId); // npc teplateId
                    stream.Write(mount.OwnerId);    // characterId (masterId)
                    break;
                case (byte)BaseUnitType.Shipyard:
                    var shipyard = (Shipyard)_unit;
                    stream.Write(shipyard.Template.Id);         // type(id)
                    stream.Write(shipyard.Template.TemplateId); // type(id)
                    break;
            }
            if (_unit.OwnerId > 0) // master
            {
                var name = NameManager.Instance.GetCharacterName(_unit.OwnerId);
                stream.Write(name ?? "");
            }
            else
            {
                stream.Write("");
            }
            stream.WritePosition(_unit.Position.X, _unit.Position.Y, _unit.Position.Z);
            stream.Write(_unit.Scale);
            stream.Write(_unit.Level);
            stream.Write(_unit.ModelId); // modelRef
            if (_unit is Character character1)
            {
                foreach (var item in character1.Inventory.Equip)
                {
                    if (item == null)
                    {
                        stream.Write(0);
                    }
                    else
                    {
                        stream.Write(item);
                    }
                }
            }
            else
            {
                var v4 = 0;
                do
                {
                    var item = _unit.Equip[v4];

                    //if (v4 - 19 > 6 || _baseUnitType == 2)
                    if (!(item is BodyPart) || _baseUnitType == (byte)BaseUnitType.Slave)
                    {
                        if (v4 != 27 || _baseUnitType != (byte)BaseUnitType.Npc)
                        {
                            switch (_baseUnitType)  // Item [0..18]
                            {
                                case (byte)BaseUnitType.Slave:
                                case (byte)BaseUnitType.Housing:
                                case (byte)BaseUnitType.Mate:
                                    {
                                        if (item == null)
                                        {
                                            stream.Write(0);
                                        }
                                        else
                                        {
                                            stream.Write(item);
                                        }

                                        break;
                                    }
                                case (byte)BaseUnitType.Npc:
                                    {
                                        if (item == null)
                                        {
                                            stream.Write(0);
                                        }
                                        else
                                        {
                                            stream.Write(item.TemplateId);
                                            if (item.TemplateId != 0)
                                            {
                                                stream.Write(0L);
                                                stream.Write((byte)0);
                                            }
                                        }

                                        break;
                                    }
                                case (byte)BaseUnitType.Transfer:
                                case (byte)BaseUnitType.Shipyard:
                                    break;
                                default:
                                    break;
                            }
                        }
                        else // Cosplay [27]
                        {
                            if (item == null)
                            {
                                stream.Write(0);
                            }
                            else
                            {
                                stream.Write(item);
                            }
                        }
                    }
                    else // somehow_special [19..26]
                    {
                        stream.Write(item.TemplateId); // somehow_special (here we derive the parts of the body: face, hair, body)
                    }

                    v4++;
                } while (v4 < 28);
            }

            stream.Write(_unit.ModelParams);
            stream.WriteBc(0);
            stream.Write(_unit.Hp * 100); // preciseHealth
            stream.Write(_unit.Mp * 100); // preciseMana
            stream.Write((byte)_attachPoint); // point // TODO UnitAttached
            if (_attachPoint != 255) // -1
            {
                stream.WriteBc(_objId); // _tlId ? указываем на хозяина, куда надо прикрепить
            }

            if (_unit is Character character2)
            {
                if (character2.Bonding == null)
                {
                    stream.Write((sbyte)-1); // point
                }
                else
                {
                    stream.Write(character2.Bonding);
                }
            }
            else if (_unit is Slave slave)
            {
                if (slave.BondingObjId > 0)
                {
                    stream.WriteBc(slave.BondingObjId);
                }
                else
                {
                    stream.Write((sbyte)-1);
                }
            }
            else if (_unit is Transfer transfer)
            {
                if (transfer.BondingObjId > 0)
                {
                    stream.WriteBc(transfer.BondingObjId);
                }
                else
                {
                    stream.Write((sbyte)-1);
                }
            }
            else
            {
                stream.Write((sbyte)-1); // point
            }

            if (_baseUnitType == 1) // NPC
            {
                if (_unit is Npc npc)
                {
                    // TODO UnitModelPosture
                    if (npc.Faction.GuardHelp)
                    {
                        stream.Write(_modelPostureType); // type // оставим это для того, чтобы NPC могли заниматься своими делами
                    }
                    else
                    {
                        _modelPostureType = (byte)ModelPostureType.None; // type //для NPC на которых можно напасть и чтобы они шевелили ногами (для людей особенно)
                        stream.Write(_modelPostureType);
                    }
                }
            }
            else // other
            {
                stream.Write(_modelPostureType);
            }

            stream.Write(false); // isLooted

            switch (_modelPostureType)
            {
                case (byte)ModelPostureType.HouseState: // house_state
                    for (var i = 0; i < 2; i++)
                    {
                        stream.Write(true); // door
                    }

                    for (var i = 0; i < 6; i++)
                    {
                        stream.Write(true); // window
                    }

                    break;
                case (byte)ModelPostureType.ActorModelState: // actor_model_state
                    var npc = (Npc)_unit;
                    stream.Write(npc.Template.AnimActionId);
                    stream.Write(true); // active
                    break;
                case (byte)ModelPostureType.FarmfieldState: // farmfield_state
                    stream.Write(0u); // type(id)
                    stream.Write(0f); // growRate
                    stream.Write(0); // randomSeed
                    stream.Write(false); // isWithered
                    stream.Write(false); // isHarvested
                    break;
                case (byte)ModelPostureType.TurretState: // turret_state
                    stream.Write(0f); // pitch
                    stream.Write(0f); // yaw
                    break;
                case (byte)ModelPostureType.None:
                    break;
                default:
                    break;
            }

            stream.Write(_unit.ActiveWeapon);

            if (_unit is Character character3)
            {
                stream.Write((byte)character3.Skills.Skills.Count);
                foreach (var skill in character3.Skills.Skills.Values)
                {
                    stream.Write(skill.Id);
                    stream.Write(skill.Level);
                }

                stream.Write(character3.Skills.PassiveBuffs.Count);
                foreach (var buff in character3.Skills.PassiveBuffs.Values)
                {
                    stream.Write(buff.Id);
                }
            }
            else
            {
                stream.Write((byte)0); // learnedSkillCount
                stream.Write(0);       // learnedBuffCount
            }

            stream.Write(_unit.Position.RotationX);
            stream.Write(_unit.Position.RotationY);
            stream.Write(_unit.Position.RotationZ);

            switch (_unit)
            {
                case Character chr:
                    stream.Write(chr.RaceGender);
                    break;
                case Npc npc:
                    stream.Write(npc.RaceGender);
                    break;
                default:
                    stream.Write(_unit.RaceGender);
                    break;
            }

            if (_unit is Character character4)
            {
                stream.WritePisc(0, 0, character4.Appellations.ActiveAppellation, 0); // pish ... pisc
            }
            else
            {
                stream.WritePisc(0, 0, 0, 0); // pish ... pisc
            }

            stream.WritePisc(_unit.Faction?.Id ?? 0, _unit.Expedition?.Id ?? 0, 0, 0); // pish ... pisc

            if (_unit is Character character5)
            {
                var flags = new BitSet(16);

                if (character5.Invisible)
                {
                    flags.Set(5);
                }

                if (character5.IdleStatus)
                {
                    flags.Set(13);
                }

                stream.WritePisc(0, 0); // очки чести полученные в PvP, кол-во убийств в PvP
                stream.Write(flags.ToByteArray()); // flags(ushort)
                /*
                 * 0x01 - 8bit - режим боя
                 * 0x04 - 6bit - невидимость?
                 * 0x08 - 5bit - дуэль
                 * 0x40 - 2bit - gmmode, дополнительно 7 байт
                 * 0x80 - 1bit - дополнительно tl(ushort), tl(ushort), tl(ushort), tl(ushort)
                 * 0x0100 - 16bit - дополнительно 3 байт (bc), firstHitterTeamId(uint)
                 * 0x0400 - 14bit - надпись "Отсутсвует" под именем
                 */
            }
            else
            {
                stream.WritePisc(0, 0); // pish ... pisc
                stream.Write((ushort)0); // flags
            }

            if (_unit is Character character6)
            {
                var activeAbilities = character6.Abilities.GetActiveAbilities();
                foreach (var ability in character6.Abilities.Values)
                {
                    stream.Write(ability.Exp);
                    stream.Write(ability.Order);
                }

                stream.Write((byte)activeAbilities.Count);
                foreach (var ability in activeAbilities)
                {
                    stream.Write((byte)ability);
                }

                stream.WriteBc(0);

                character6.VisualOptions.Write(stream, 31);

                stream.Write(1); // premium

                for (var i = 0; i < 6; i++)
                {
                    stream.Write(0); // pStat
                }
            }

            var goodBuffs = new List<Effect>();
            var badBuffs = new List<Effect>();
            var hiddenBuffs = new List<Effect>();
            _unit.Effects.GetAllBuffs(goodBuffs, badBuffs, hiddenBuffs);

            stream.Write((byte)goodBuffs.Count); // TODO max 32
            foreach (var effect in goodBuffs)
            {
                stream.Write(effect.Index);
                stream.Write(effect.Template.BuffId);
                stream.Write(effect.SkillCaster);
                /*
                   stream.Write(ItemId);
                   stream.Write(ItemTemplateId);
                   stream.Write(Type1);
                   stream.Write(Type2);
                */
                stream.Write(0u); // type(id)
                stream.Write(effect.Caster.Level); // sourceLevel
                stream.Write((short)1); // sourceAbLevel
                stream.Write(effect.Duration); // totalTime
                stream.Write(effect.GetTimeElapsed()); // elapsedTime
                stream.Write((uint)effect.Tick); // tickTime
                stream.Write(0); // tickIndex
                stream.Write(1); // stack
                stream.Write(0); // charged
                stream.Write(0u); // type(id) -> cooldownSkill
            }

            stream.Write((byte)badBuffs.Count); // TODO max 24
            foreach (var effect in badBuffs)
            {
                stream.Write(effect.Index);
                stream.Write(effect.Template.BuffId);
                stream.Write(effect.SkillCaster);
                stream.Write(0u); // type(id)
                stream.Write(effect.Caster.Level); // sourceLevel
                stream.Write((short)1); // sourceAbLevel
                stream.Write(effect.Duration); // totalTime
                stream.Write(effect.GetTimeElapsed()); // elapsedTime
                stream.Write((uint)effect.Tick); // tickTime
                stream.Write(0); // tickIndex
                stream.Write(1); // stack
                stream.Write(0); // charged
                stream.Write(0u); // type(id) -> cooldownSkill
            }

            stream.Write((byte)hiddenBuffs.Count); // TODO max 24
            foreach (var effect in hiddenBuffs)
            {
                stream.Write(effect.Index);
                stream.Write(effect.Template.BuffId);
                stream.Write(effect.SkillCaster);
                stream.Write(0u); // type(id)
                stream.Write(effect.Caster.Level); // sourceLevel
                stream.Write((short)1); // sourceAbLevel
                stream.Write(effect.Duration); // totalTime
                stream.Write(effect.GetTimeElapsed()); // elapsedTime
                stream.Write((uint)effect.Tick); // tickTime
                stream.Write(0); // tickIndex
                stream.Write(1); // stack
                stream.Write(0); // charged
                stream.Write(0u); // type(id) -> cooldownSkill
            }

            //            for (var i = 0; i < 255; i++)
            //                stream.Write(0);

            return stream;
        }
    }
}
