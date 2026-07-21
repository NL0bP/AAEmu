using AAEmu.Commons.Network;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Static;
using AAEmu.Game.Models.Game.Gimmicks;
using AAEmu.Game.Models.Game.Housing;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Shipyard;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.StaticValues;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCUnitStatePacket : GamePacket
{
    private readonly Unit _unit;
    private readonly BaseUnitType _baseUnitType;
    private ModelPostureType _modelPostureType;
    private bool _activateModelPosture = true;
    private bool _hideSpawnEffect;

    public SCUnitStatePacket(Unit unit, bool hideSpawnEffect = false) : base(SCOffsets.SCUnitStatePacket, 5)
    {
        _unit = unit;
        _modelPostureType = unit.ModelPostureType;
        switch (_unit)
        {
            case Character:
                _baseUnitType = BaseUnitType.Character;
                _modelPostureType = ModelPostureType.None;
                break;
            case Npc npc:
                _baseUnitType = BaseUnitType.Npc;
                _modelPostureType = npc.AnimActionId > 0 ? ModelPostureType.ActorModelState : ModelPostureType.None;
                break;
            case Slave:
                _baseUnitType = BaseUnitType.Slave;
                _modelPostureType = _unit.ModelId == 895 ? ModelPostureType.TurretState : ModelPostureType.None;
                _hideSpawnEffect = hideSpawnEffect;
                break;
            case House:
                _baseUnitType = BaseUnitType.Housing;
                _modelPostureType = ModelPostureType.HouseState;
                break;
            case Transfer:
                _baseUnitType = BaseUnitType.Transfer;
                _modelPostureType = ModelPostureType.TurretState;
                break;
            case Mate:
                _baseUnitType = BaseUnitType.Mate;
                _modelPostureType = ModelPostureType.None;
                break;
            case Shipyard:
                _baseUnitType = BaseUnitType.Shipyard;
                _modelPostureType = ModelPostureType.None;
                break;
        }
    }

    private static bool ShouldHideOwnerName(Unit unit)
    {
        if (unit is not Npc npc || npc.Template is null)
            return false;

        return npc.OwnerId > 0 && npc.Template.FactionId == FactionsEnum.Fish;
    }

    public override PacketStream Write(PacketStream stream)
    {
        #region NetUnit
        stream.WriteBc(_unit.ObjId);
        stream.Write(_unit.Name);

        // Cache character & npc
        var character = _unit as Character;
        var npc = _unit as Npc;

        #region BaseUnitType
        stream.Write((byte)_baseUnitType);
        switch (_baseUnitType)
        {
            case BaseUnitType.Character:
                stream.Write(character?.Id ?? 0u); // type(id)
                stream.Write(0L);                  // v
                break;
            case BaseUnitType.Npc:
                stream.WriteBc(npc!.ObjId);    // objId
                stream.Write(npc.TemplateId); // npc templateId
                stream.Write(npc.OwnerId);    // type(id) (ownerId?)
                stream.Write((byte)0);        // clientDriven
                break;
            case BaseUnitType.Slave:
                var slave = (Slave)_unit;
                stream.Write(slave.Id);             // Id ? slave.Id
                stream.Write(slave.TlId);           // tl
                stream.Write(slave.TemplateId);     // templateId
                stream.Write(slave.Summoner?.Id ?? 0); // ownerId
                break;
            case BaseUnitType.Housing:
                var house = (House)_unit;
                var buildStep = house.CurrentStep == -1
                    ? 0
                    : -house.Template.BuildSteps.Count + house.CurrentStep;

                stream.Write(house.TlId); // tl
                stream.Write(house.TemplateId); // templateId
                stream.Write((short)buildStep); // buildstep
                break;
            case BaseUnitType.Transfer:
                var transfer = (Transfer)_unit;
                stream.Write(transfer.TlId); // tl
                stream.Write(transfer.TemplateId); // templateId
                break;
            case BaseUnitType.Mate:
                var mount = (Mate)_unit;
                stream.Write(mount.TlId);       // tl
                stream.Write(mount.TemplateId); // teplateId
                stream.Write(mount.OwnerId);    // characterId (masterId)
                break;
            case BaseUnitType.Shipyard:
                var shipyard = (Shipyard)_unit;
                stream.Write(shipyard.ShipyardData.Id);         // type(id)
                stream.Write(shipyard.ShipyardData.TemplateId); // type(id)
                break;
        }
        #endregion BaseUnitType

        if (_unit.OwnerId > 0 && !ShouldHideOwnerName(_unit)) // master
        {
            var name = NameManager.Instance.GetCharacterName(_unit.OwnerId);
            stream.Write(name ?? "");
        }
        else
            stream.Write("");

        stream.WritePosition(_unit.Transform.Local.Position);
        stream.Write(_unit.Scale); // scale
        stream.Write(_unit.Level); // level
        stream.Write(_unit.HeirLevel); // hierarchy level for 3.0.3.0
        stream.Write((byte)0); // level added in 3.5.0.3

        for (var i = 0; i < 4; i++)
            stream.Write((sbyte)-1); // slot for 3.0.3.0

        stream.Write(_unit.ModelId); // modelRef

        #region CharacterInfo_3EB0

        Inventory_Equip3(stream, _unit); // Equip character

        #endregion CharacterInfo_3EB0

        stream.Write(_unit.ModelParams); // CustomModel_3570

        stream.WriteBc(0);
        stream.Write(_unit.Hp * 100); // preciseHealth
        stream.Write(_unit.Mp * 100); // preciseMana

        #region AttachPoint1
        switch (_unit)
        {
            case Gimmick:
            case Portal:
            case Character:
            case Npc:
            case House:
            case Mate:
            case Shipyard:
                stream.Write((byte)AttachPointKind.System);   // point
                break;
            case Slave unit:
                stream.Write(unit.AttachPointId);
                if (unit.AttachPointId > -1)
                    stream.WriteBc(unit.OwnerObjId);
                break;
            case Transfer unit:
                if (unit.BondingObjId != 0)
                {
                    stream.Write((byte)unit.AttachPointId);  // point
                    stream.WriteBc(unit.BondingObjId); // point to the owner where to attach
                }
                else
                    stream.Write((byte)AttachPointKind.System);   // point
                break;
        }
        #endregion AttachPoint1

        #region AttachPoint2
        switch (_unit)
        {
            case Character:
                switch (character.Bonding)
                {
                    case null:
                        stream.Write((byte)AttachPointKind.System);   // point
                        break;
                    default:
                        stream.Write(character.Bonding);
                        break;
                }
                break;
            case Npc:
            case House:
            case Mate:
            case Shipyard:
            case Transfer:
                stream.Write((byte)AttachPointKind.System);   // point
                break;
            case Slave unit:
                if (unit.BondingObjId > 0)
                {
                    stream.WriteBc(unit.BondingObjId);
                    stream.Write(0);  // space
                    stream.Write(0);  // spot
                    stream.Write(0);  // type
                }
                else
                    stream.Write((byte)AttachPointKind.System);   // point
                break;
        }
        #endregion AttachPoint2

        #region UnitModelPosture

        Unit.ModelPosture(stream, _unit, (_unit as Npc)?.AnimActionId ?? 0, _activateModelPosture);

        #endregion

        stream.Write(_unit.ActiveWeapon);

        switch (_unit)
        {
            case Character:
                {
                    var learnedSkillCount = character.Skills.Skills.Values.Count;
                    var passiveBuffCount = character.Skills.PassiveBuffs.Values.Count;

                    stream.Write((byte)learnedSkillCount);       // learnedSkillCount
                    stream.Write((byte)passiveBuffCount); // passiveBuffCount
                    stream.Write(character.HighAbilityRsc); // highAbilityRsc

                    var arrSkills = character.Skills.Skills.Values
                        .Select(skill => (long)skill.Id)
                        .ToArray();
                    stream.WritePiscW(learnedSkillCount, arrSkills);

                    var arrBuffs = character.Skills.PassiveBuffs.Values
                        .Select(buff => (long)buff.Id)
                        .ToArray();
                    stream.WritePiscW(passiveBuffCount, arrBuffs);
                    break;
                }
            case Npc:
                {
                    var skills = new List<NpcSkill>();

                    if (npc.Template.BaseSkillId > 0)
                    {
                        var baseSkill = new NpcSkill
                        {
                            Id = 0,
                            OwnerId = npc.TemplateId,
                            OwnerType = "Npc",
                            SkillId = (uint)npc.Template.BaseSkillId,
                            SkillUseCondition = SkillUseConditionKind.InCombat,
                            SkillUseParam1 = 0,
                            SkillUseParam2 = 0
                        };
                        skills.Add(baseSkill);
                    }

                    foreach (var sl in npc.Template.Skills.Values)
                        skills.AddRange(sl);

                    stream.Write((byte)skills.Count);    // learnedSkillCount
                    stream.Write((byte)npc.Template.PassiveBuffs.Count); // passiveBuffCount
                    stream.Write(npc.HighAbilityRsc); // highAbilityRsc

                    var arrSkills = skills
                        .Select(skill => (long)skill.SkillId)
                        .ToArray();
                    stream.WritePiscW(arrSkills.Length, arrSkills);

                    var arrBuffs = npc.Template.PassiveBuffs
                        .Select(buff => (long)buff.PassiveBuffId)
                        .ToArray();
                    stream.WritePiscW(arrBuffs.Length, arrBuffs);

                    break;
                }
            case Slave slave:
                {
                    stream.Write((byte)0); // learnedSkillCount
                    stream.Write((byte)slave.Template.PassiveBuffs.Count); // passiveBuffCount
                    stream.Write(slave.HighAbilityRsc); // highAbilityRsc

                    var arrBuffs = slave.Template.PassiveBuffs
                        .Select(buff => (long)buff.PassiveBuffId)
                        .ToArray();
                    stream.WritePiscW(arrBuffs.Length, arrBuffs);

                    break;
                }
            default:
                {
                    stream.Write((byte)0); // learnedSkillCount
                    stream.Write((byte)0); // passiveBuffCount
                    stream.Write(0);       // highAbilityRsc
                    break;
                }
        }

        // Rotation
        if (_baseUnitType == BaseUnitType.Housing)
            stream.Write(_unit.Transform.Local.Rotation.Z); // должно быть float
        else
        {
            var (roll, pitch, yaw) = _unit.Transform.Local.ToRollPitchYawSBytes();
            stream.Write(roll);
            stream.Write(pitch);
            stream.Write(yaw);
        }

        switch (_unit)
        {
            case Character:
                stream.Write(character.RaceGender);
                break;
            case Npc:
                stream.Write(npc.RaceGender);
                break;
            default:
                stream.Write(_unit.RaceGender);
                break;
        }

        if (_unit is Character)
        {
            // ???, ??? and Appellation (Title)
            stream.WritePisc(0, 0, character.Appellations.ActiveAppellation, 0);      // pisc
            // Faction and Guild
            stream.WritePisc((uint)(character.Faction?.Id ?? 0), (uint)(character.Expedition?.Id ?? 0), 0, 0); // pisc
            // PvP Honor gained and PvP Kills
            stream.WritePisc(character.HonorGainedInCombat, character.HostileFactionKills, 0, 0); // pisc
        }
        else if (_unit is Npc)
        {
            stream.WritePisc(0, npc?.Spawner?.SpawnerId ?? 0, 0, 0); // TODO второе число больше нуля, что это за число? это spawnerId
            stream.WritePisc((uint)(_unit.Faction?.Id ?? 0), (uint)(_unit.Expedition?.Id ?? 0), 0, 0); // pisc
            stream.WritePisc(0, 0, 0, 0); // pisc
        }
        else
        {
            stream.WritePisc(0, 0, 0, 0); // TODO второе число больше нуля, что это за число?
            stream.WritePisc((uint)(_unit.Faction?.Id ?? 0), (uint)(_unit.Expedition?.Id ?? 0), 0, 0); // pisc
            stream.WritePisc(0, 0, 0, 0); // pisc
        }

        switch (_unit)
        {
            case Character:
                {
                    var flags = new BitSet(16); // short
                    if (character.Invisible)
                        flags.Set(5);
                    if (character.IsInBattle)
                        flags.Set(9);
                    if (character.IdleStatus)
                        flags.Set(13);
                    stream.Write(flags.ToByteArray()); // flags(ushort)
                    break;
                }
            case Npc:
                stream.Write((ushort)8192); // flags - нейтральный флаг, нет дополнительных данных в пакете
                break;
            case Slave:
                if (_hideSpawnEffect)
                    stream.Write((ushort)0); // flags
                else
                    stream.Write((ushort)0x800); // flags - Spawn is done from the portal
                break;
            default:
                stream.Write((ushort)0); // flags
                break;
        }

        if (_unit is Character player)
        {
            #region read_Abilities_6300
            var activeAbilities = character.Abilities.GetActiveAbilities();
            foreach (var ability in character.Abilities.Values) // size=29 in 5070
            {
                stream.Write(ability.Exp);
                stream.Write(ability.Order);
            }

            stream.Write((byte)activeAbilities.Count); // nActive
            foreach (var ability in activeAbilities)
            {
                stream.Write((byte)ability); // active
            }
            #endregion read_Abilities_6300

            #region read_Exp_Order_6460
            foreach (var ability in character.Abilities.Values) // size=29 in 5070
            {
                stream.Write(ability.Exp);
                stream.Write(ability.Order);  // ability.Order
                stream.Write(false);          // canNotLevelUp
            }

            byte nHighActive = 0;
            byte nActive = 0;
            stream.Write(nHighActive); // nHighActive
            stream.Write(nActive);     // nActive
            while (nHighActive > 0)
            {
                while (nActive > 0)
                {
                    stream.Write(0); // active
                    nActive--;
                }
                nHighActive--;
            }
            #endregion read_Exp_Order_6460

            stream.WriteBc(0);     // objId
            stream.Write((byte)0); // camp

            #region Stp
            stream.Write((byte)30);  // stp
            stream.Write((byte)60);  // stp
            stream.Write((byte)50);  // stp
            stream.Write((byte)0);   // stp
            stream.Write((byte)40);  // stp
            stream.Write((byte)100); // stp

            stream.Write((byte)7); // flags
            character.VisualOptions.Write(stream, 0x20); // cosplay_visual
            #endregion

            stream.Write(2); // premium

            #region Stats
            for (uint j = 0; j < 5; j++)
            {
                stream.Write(0); // stats
            }

            stream.Write(0);  // extendMaxStats
            stream.Write(0);  // applyExtendCount
            stream.Write(0);  // applyNormalCount
            stream.Write(0);  // applySpecialCount
            #endregion Stats

            stream.WritePisc(0, 0, 0, 0);
            stream.WritePisc(0, 0);
            stream.Write((byte)0); // accountPrivilege
        }
        #endregion NetUnit

        #region NetBuff

        var goodBuffs = new List<Buff>();
        var badBuffs = new List<Buff>();
        var hiddenBuffs = new List<Buff>();

        _unit.Buffs.GetAllBuffs(goodBuffs, badBuffs, hiddenBuffs, false);
        FilterNpcTemplatePassiveBuffDuplicates(goodBuffs, badBuffs, hiddenBuffs);

        stream.Write((byte)goodBuffs.Count); // TODO max 32
        foreach (var buff in goodBuffs)
        {
            WriteBuff(stream, buff);
        }

        stream.Write((byte)badBuffs.Count); // TODO max 24 for 1.2, 20 for 3.0.3.0
        foreach (var buff in badBuffs)
        {
            WriteBuff(stream, buff);
        }

        stream.Write((byte)hiddenBuffs.Count); // TODO max 24 for 1.2, 28 for 3.0.3.0
        foreach (var buff in hiddenBuffs)
        {
            WriteBuff(stream, buff);
        }
        #endregion NetBuff

        return stream;
    }

    private static void WriteBuff(PacketStream stream, Buff buff)
    {
        stream.Write(buff.Index);              // Id
        stream.Write(buff.SkillCaster);        // skillCaster
        stream.Write(buff.Caster?.Id ?? 0);    // type(id)
        stream.Write(buff.Caster?.Level ?? 1); // sourceLevel
        stream.Write(buff.AbLevel);            // sourceAbLevel
        stream.WritePisc(0, buff.GetTimeElapsed(), 0, 0u); // add in 3.0.3.0
        stream.WritePisc(buff.Template.BuffId, 1, 0, 0u);  // add in 3.0.3.0
    }

    private void FilterNpcTemplatePassiveBuffDuplicates(List<Buff> goodBuffs, List<Buff> badBuffs, List<Buff> hiddenBuffs)
    {
        if (_unit is not Npc npc || npc.Template?.PassiveBuffs == null || npc.Template.PassiveBuffs.Count == 0)
            return;

        var passiveBuffIds = new HashSet<uint>();
        for (var i = 0; i < npc.Template.PassiveBuffs.Count; i++)
        {
            var buffId = npc.Template.PassiveBuffs[i]?.PassiveBuff?.BuffId ?? 0;
            if (buffId > 0)
                passiveBuffIds.Add(buffId);
        }

        if (passiveBuffIds.Count == 0)
            return;

        RemoveBuffsByTemplateBuffId(goodBuffs, passiveBuffIds);
        RemoveBuffsByTemplateBuffId(badBuffs, passiveBuffIds);
        RemoveBuffsByTemplateBuffId(hiddenBuffs, passiveBuffIds);
    }

    private static void RemoveBuffsByTemplateBuffId(List<Buff> buffs, HashSet<uint> buffIds)
    {
        for (var i = buffs.Count - 1; i >= 0; i--)
        {
            var buff = buffs[i];
            if (buff?.Template is null)
                continue;

            if (buffIds.Contains(buff.Template.BuffId))
                buffs.RemoveAt(i);
        }
    }

    #region CharacterInfo_3EB0

    private static void Inventory_Equip3(PacketStream stream, Unit unit)
    {
        var items = new List<Item>();

        switch (unit)
        {
            case Character character:
                {
                    items = character.Inventory.Equipment.GetSlottedItemsList();
                    WriteEquip(stream, items);
                    var itemFlags = CalculateItemFlags(items);
                    stream.Write(itemFlags); // ItemFlags flags for 3.0.3.0
                    break;
                }
            case House house:
                {
                    items = house.Equipment.GetSlottedItemsList();
                    WriteEquip(stream, items);
                    break;
                }
            case Mate mate:
                {
                    items = mate.Equipment.GetSlottedItemsList();
                    WriteEquip(stream, items);
                    break;
                }
            case Slave slave:
                {
                    items = slave.Equipment.GetSlottedItemsList();
                    WriteEquip(stream, items);
                    break;
                }
            case Npc npc:
                {
                    items = npc.Equipment.GetSlottedItemsList();
                    var validFlags = CalculateValidFlags(items);
                    stream.Write((uint)validFlags);

                    if (validFlags <= 0)
                    {
                        unit.ModelParams.SetType(UnitCustomModelType.Skin); // дополнительная проверка, что у NPC нет тела и лица
                        return;
                    }

                    for (var i = 0; i < items.Count; i++)
                    {
                        var item = npc.Equipment.GetItemBySlot(i);

                        if (item is BodyPart)
                        {
                            stream.Write(item.TemplateId);
                        }
                        else if (item != null)
                        {
                            if (i == 27) // Cosplay
                            {
                                stream.Write(item);
                            }
                            else
                            {
                                stream.Write(item.TemplateId);
                                stream.Write(0L);
                                stream.Write((byte)0);
                            }
                        }
                    }
                    break;
                }
            // for transfer and Shipyard
            default:
                {
                    stream.Write(0u); // validFlags for 3.0.3.0
                    break;
                }
        }
    }

    private static void WriteEquip(PacketStream stream, List<Item> items)
    {
        var validFlags = CalculateValidFlags(items);
        stream.Write((uint)validFlags); // validFlags for 3.0.3.0
        WriteItems(stream, items);
    }

    private static void WriteItems(PacketStream stream, List<Item> items)
    {
        foreach (var item in items)
        {
            if (item != null)
            {
                stream.Write(item);
            }
        }
    }

    private static int CalculateValidFlags(List<Item> items)
    {
        var validFlags = 0;
        var index = 0;
        foreach (var item in items)
        {
            if (item != null)
            {
                validFlags |= 1 << index;
            }

            index++;
        }

        return validFlags;
    }

    private static int CalculateItemFlags(List<Item> items)
    {
        var itemFlags = 0;
        var index = 0;

        foreach (var tmp in items
                     .Where(item => item != null)
                     .Select(item => (int)item.ItemFlags << index))
        {
            ++index;
            itemFlags |= tmp;
        }

        return itemFlags;
    }

    #endregion CharacterInfo_3EB0

    public override string Verbose()
    {
        return " - " + _baseUnitType + " - " + _unit?.DebugName();
    }
}
