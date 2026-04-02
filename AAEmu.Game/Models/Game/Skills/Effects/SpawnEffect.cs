using System;
using AAEmu.Commons.Utils;
using System.Linq;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills.Effects.Enums;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Static;
using AAEmu.Game.Models.Tasks;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.Skills.Effects;

public class SpawnEffect : EffectTemplate
{
    public BaseUnitType OwnerTypeId { get; set; }
    public uint SubType { get; set; }
    public uint PosDirId { get; set; }
    //public float PosAngle { get; set; } // there is no such field in the database for version 3.0.3.0
    public float PosAngleMax { get; set; }
    public float PosAngleMin { get; set; }
    public float PosDistanceMax { get; set; }
    public float PosDistanceMin { get; set; }
    public uint OriDirId { get; set; }
    public float OriAngle { get; set; }
    public bool UseSummonerFaction { get; set; }
    public float LifeTime { get; set; }
    public bool DespawnOnCreatorDeath { get; set; }
    public bool UseSummonerAggroTarget { get; set; }
    public MateState MateStateId { get; set; }

    public override bool OnActionTime => false;

    public override void Apply(BaseUnit caster, SkillCaster casterObj, BaseUnit target, SkillCastTarget targetObj,
        CastAction castObj, EffectSource source, SkillObject skillObject, DateTime time,
        CompressedGamePackets packetBuilder = null)
    {
        Logger.Info($"SpawnEffect: OwnerTypeId={OwnerTypeId}, SubType={SubType}, UseSummonerFaction={UseSummonerFaction}, LifeTime={LifeTime}");

        //var random = new Random();
        var PosAngle = (float)(PosAngleMin + (PosAngleMax - PosAngleMin) * Rand.NextDouble());
        float PosDistance;
        if (PosDistanceMin != 0 && PosDistanceMax != 0)
        {
            PosDistance = (float)(PosDistanceMin + (PosDistanceMax - PosDistanceMin) * Rand.NextDouble());
        }
        else
        {
            PosDistanceMin = 2;
            PosDistanceMax = 3;
            PosDistance = (float)(PosDistanceMin + (PosDistanceMax - PosDistanceMin) * Rand.NextDouble());
        }

        // dir id 1 = relative to target/spawner.
        // dir id 2 = relative to caster.
        var positionRelativeToUnit = PosDirId switch
        {
            1 => target,
            2 => caster,
            _ => caster
        };

        var orientationRelativeToUnit = OriDirId switch
        {
            1 => target,
            2 => caster,
            _ => caster
        };

        if (positionRelativeToUnit == null || orientationRelativeToUnit == null)
        {
            Logger.Warn($"SpawnEffect: Unhandled PosDirId {PosDirId} or OriDirId {OriDirId}");
            return;
        }

        switch (OwnerTypeId)
        {
            case BaseUnitType.Npc:
                {
                    var spawner = SpawnManager.Instance.GetNpcSpawner(SubType, target);
                    if (spawner == null)
                    {
                        Logger.Info($"SpawnEffect: SubType={SubType} not found in spawners.");
                        return;
                    }

                    var (xx, yy) = MathUtil.AddDistanceToFrontDeg(PosDistance, positionRelativeToUnit.Transform.World.Position.X, positionRelativeToUnit.Transform.World.Position.Y, PosAngle);

                    // TODO: Not sure if this is needed.
                    //var zz = WorldManager.Instance.GetHeight(target.Transform.ZoneId, xx, yy);
                    //if (zz == 0) {
                    //	zz = target.Transform.World.Position.Z;
                    //}

                    spawner.Position.X = xx;
                    spawner.Position.Y = yy;
                    spawner.Position.Z = positionRelativeToUnit.Transform.World.Position.Z;

                    spawner.Position.Yaw = orientationRelativeToUnit.Transform.World.Rotation.Z + PosAngle.DegToRad();

                    spawner.RespawnTime = 0; // don't respawn

                    spawner.DoSpawnEffect(spawner.Id, this, caster, target);
                    break;
                }
            case BaseUnitType.Slave:
                {
                    if (caster is Character player)
                    {
                        // TODO: Implement OriDirId, PosDirId and MateStateId
                        using var transform = positionRelativeToUnit.Transform.CloneDetached();
                        transform.World.AddDistanceToFront(PosDistance);
                        transform.World.Rotate(transform.World.Rotation with { Z = orientationRelativeToUnit.Transform.World.Rotation.Z + OriAngle.DegToRad() });

                        var slave = SlaveManager.Instance.Create(SubType, false, transform);
                        if (slave is { Template: null })
                        {
                            Logger.Info($"SpawnEffect: SubType={SubType} not found...");
                            return;
                        }
                        player.ForceDismountAndDespawn(slave, 500000); // delete Slave after 8min 20s
                    }
                    break;
                }
            case BaseUnitType.Mate:
                {
                    if (caster is not Character owner)
                        break;

                    var activeMates = MateManager.Instance.GetActiveMates(owner.ObjId);
                    if (activeMates is { Count: > 0 })
                    {
                        foreach (var activeMate in activeMates
                                     .Where(m => m != null && m.MateType == MateType.Battle && m.ItemId == 0)
                                     .ToArray())
                        {
                            MateManager.Instance.RemoveActiveMateAndDespawn(owner, activeMate);
                        }
                    }

                    var template = NpcManager.Instance.GetTemplate(SubType);
                    if (template == null)
                    {
                        Logger.Info($"SpawnEffect: Mate SubType={SubType} not found...");
                        return;
                    }

                    var mate = new global::AAEmu.Game.Models.Game.Units.Mate
                    {
                        ObjId = ObjectIdManager.Instance.GetNextId(),
                        TlId = (ushort)TlIdManager.Instance.GetNextId(),
                        OwnerId = owner.Id,
                        OwnerObjId = owner.ObjId,
                        Id = MateIdManager.Instance.GetNextId(),
                        ItemId = 0,
                        Name = template.Name,
                        TemplateId = template.Id,
                        Template = template,
                        ModelId = template.ModelId,
                        Faction = owner.Faction,
                        Level = template.Level,
                        Hp = 100,
                        Mp = 100,
                        UserState = (byte)MateStateId,
                        Experience = ExperienceManager.Instance.GetExpForLevel(template.Level, true),
                        Mileage = 0,
                        SpawnDelayTime = 0,
                        MateType = MateType.Battle
                    };

                    mate.Transform = positionRelativeToUnit.Transform.CloneDetached(mate);
                    if (targetObj is SkillCastPositionTarget positionTarget)
                    {
                        mate.Transform.Local.SetPosition(positionTarget.PosX, positionTarget.PosY, positionTarget.PosZ);
                        mate.Transform.Local.SetZRotation(positionTarget.PosRot.DegToRad());
                    }
                    else if (targetObj is SkillCastPosition2Target position2Target)
                    {
                        mate.Transform.Local.SetPosition(position2Target.PosX, position2Target.PosY, position2Target.PosZ);
                    }
                    else if (targetObj is SkillCastPosition3Target position3Target)
                    {
                        mate.Transform.Local.SetPosition(position3Target.PosX, position3Target.PosY, position3Target.PosZ);
                    }
                    else
                    {
                        mate.Transform.World.AddDistanceToFront(PosDistance);
                        mate.Transform.World.Rotate(mate.Transform.World.Rotation with { Z = orientationRelativeToUnit.Transform.World.Rotation.Z + OriAngle.DegToRad() });
                    }

                    var mateSkills = MateManager.Instance.GetMateSkills(template.Id);
                    if (mateSkills is { Count: > 0 })
                        mate.Skills.AddRange(mateSkills);

                    foreach (var buffId in template.Buffs)
                    {
                        var buff = SkillManager.Instance.GetBuffTemplate(buffId);
                        if (buff == null)
                            continue;

                        var obj = new SkillCasterUnit(mate.ObjId);
                        buff.Apply(mate, obj, mate, null, null, new EffectSource(), null, DateTime.UtcNow);
                    }

                    mate.Hp = mate.MaxHp;
                    mate.Mp = mate.MaxMp;

                    Logger.Info($"SpawnEffect: creating mate summon npcId={template.Id}, objId={mate.ObjId}, tlId={mate.TlId}, owner={owner.Name} ({owner.ObjId})");
                    mate.Events.OnDeath += (_, _) =>
                    {
                        var currentMate = MateManager.Instance.GetActiveMateByMateObjId(owner.ObjId, mate.ObjId);
                        if (currentMate != null)
                            MateManager.Instance.RemoveActiveMateAndDespawn(owner, currentMate);
                    };
                    MateManager.Instance.AddActiveMateAndSpawn(owner, mate);
                    Logger.Info($"SpawnEffect: mate summon spawned npcId={template.Id}, objId={mate.ObjId}, tlId={mate.TlId}");
                    mate.PostUpdateCurrentHp(mate, 0, mate.Hp, KillReason.Unknown);

                    if (LifeTime > 0)
                    {
                        TaskManager.Instance.Schedule(new ResetAttemptsTask(() =>
                        {
                            var currentMate = MateManager.Instance.GetActiveMateByTlId(owner.ObjId, mate.TlId);
                            if (currentMate != null)
                                MateManager.Instance.RemoveActiveMateAndDespawn(owner, currentMate);
                        }), TimeSpan.FromSeconds(LifeTime));
                    }
                    break;
                }
            case BaseUnitType.Character:
                break;
            case BaseUnitType.Housing:
                break;
            case BaseUnitType.Transfer:
                break;
            case BaseUnitType.Shipyard:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
