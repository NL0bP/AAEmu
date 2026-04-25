using System;
using System.Threading;
using System.Threading.Tasks;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Achievement.Enums;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Static;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Resolvers;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.slaves;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSStartSkillPacket : GamePacket
{
    private enum SkillExecutionMode
    {
        Mounted,
        Pet,
        AutoAttack,
        Common,
        Item,
        Learned,
        TemporaryReplacement,
        Variant,
        Unknown
    }

    private sealed class SkillStartRequest
    {
        public uint SkillId { get; init; }
        public SkillCaster SkillCaster { get; set; }
        public SkillCastTarget SkillCastTarget { get; set; }
        public SkillObject SkillObject { get; init; }
        public byte Flag { get; init; }
    }

    private sealed class SkillExecutionResult
    {
        public SkillResult Result { get; init; }
        public uint ErrorValue { get; init; }
        public Skill Skill { get; init; }
        public bool SuppressResponse { get; init; }

        public static SkillExecutionResult Success(Skill skill = null)
        {
            return new SkillExecutionResult { Result = SkillResult.Success, Skill = skill };
        }

        public static SkillExecutionResult Error(SkillResult result, uint errorValue = 0, Skill skill = null)
        {
            return new SkillExecutionResult { Result = result, ErrorValue = errorValue, Skill = skill };
        }

        public static SkillExecutionResult Silent()
        {
            return new SkillExecutionResult { Result = SkillResult.Failure, SuppressResponse = true };
        }
    }

    public CSStartSkillPacket() : base(CSOffsets.CSStartSkillPacket, 5)
    {
    }

    public override void Read(PacketStream stream)
    {
        var player = Connection?.ActiveChar;
        if (player == null)
        {
            return;
        }

        DelaySkillStart();

        if (!TryReadRequest(stream, out var request))
        {
            return;
        }

        var skillTemplate = SkillManager.Instance.GetSkillTemplate(request.SkillId);
        var skillCaster = request.SkillCaster;
        var skillCastTarget = request.SkillCastTarget;
        var effectiveCaster = SkillContextResolver.AuthoritativeResolve(player, skillTemplate, ref skillCaster, ref skillCastTarget);
        request.SkillCaster = skillCaster;
        request.SkillCastTarget = skillCastTarget;

        Logger.Info($"StartSkill: Id {request.SkillId}, flag {request.Flag}, caster={request.SkillCaster.ObjId}, target={request.SkillCastTarget.ObjId}");
        LogCharacterSkillUsage(request.SkillCaster, request.SkillId);

        var result = skillTemplate == null
            ? SkillExecutionResult.Error(SkillResult.InvalidSkill)
            : ExecuteSkill(player, request, skillTemplate, effectiveCaster);

        HandleDismountSkill(player, request.SkillId, request.SkillCastTarget);
        HandleSkillResult(player, request, result);
    }

    private static void DelaySkillStart()
    {
        // Preserve the legacy packet throttle used to avoid client-side stuck skill starts.
        using var source = new CancellationTokenSource();
        var delayTask = Task.Run(async delegate
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), source.Token);
            return 0;
        });

        try
        {
            delayTask.Wait();
        }
        catch (AggregateException ae)
        {
            foreach (var e in ae.InnerExceptions)
            {
                Logger.Trace("{0}: {1}", e.GetType().Name, e.Message);
            }
        }
    }

    private static bool TryReadRequest(PacketStream stream, out SkillStartRequest request)
    {
        request = null;
        if (stream?.LeftBytes < 8)
        {
            return false;
        }

        var skillId = stream.ReadUInt32();
        if (skillId == 0)
        {
            return false;
        }

        if (!TryReadCaster(stream, out var skillCaster))
        {
            return false;
        }

        if (!TryReadTarget(stream, out var skillCastTarget))
        {
            return false;
        }

        if (!TryReadObject(stream, out var skillObject, out var flag))
        {
            return false;
        }

        request = new SkillStartRequest
        {
            SkillId = skillId,
            SkillCaster = skillCaster,
            SkillCastTarget = skillCastTarget,
            SkillObject = skillObject,
            Flag = flag
        };
        return true;
    }

    private static bool TryReadCaster(PacketStream stream, out SkillCaster skillCaster)
    {
        skillCaster = null;
        if (stream.LeftBytes < 1)
        {
            return false;
        }

        var type = (SkillCasterType)stream.ReadByte();
        skillCaster = SkillCaster.GetByType(type);
        if (skillCaster == null)
        {
            return false;
        }

        skillCaster.Read(stream);
        return true;
    }

    private static bool TryReadTarget(PacketStream stream, out SkillCastTarget skillCastTarget)
    {
        skillCastTarget = null;
        if (stream.LeftBytes < 1)
        {
            return false;
        }

        var type = (SkillCastTargetType)stream.ReadByte();
        skillCastTarget = SkillCastTarget.GetByType(type);
        if (skillCastTarget == null)
        {
            return false;
        }

        skillCastTarget.Read(stream);
        return true;
    }

    private static bool TryReadObject(PacketStream stream, out SkillObject skillObject, out byte flag)
    {
        skillObject = null;
        flag = 0;
        if (stream.LeftBytes < 1)
        {
            return false;
        }

        flag = stream.ReadByte();
        var flagType = flag & 63;
        skillObject = SkillObject.GetByType((SkillObjectType)flagType);
        if (skillObject == null)
        {
            return false;
        }

        if (flagType > 0)
        {
            skillObject.Read(stream);
        }

        return true;
    }

    private static void LogCharacterSkillUsage(SkillCaster skillCaster, uint skillId)
    {
        if (skillCaster is not SkillCasterUnit skillCasterUnit)
        {
            return;
        }

        var unit = WorldManager.Instance.GetUnit(skillCasterUnit.ObjId);
        if (unit is Character character)
        {
            Logger.Info($"{character.Name}:{character.ObjId} is using skill={skillId}");
        }
    }

    private static SkillExecutionResult ExecuteSkill(Character player, SkillStartRequest request, SkillTemplate skillTemplate, BaseUnit effectiveCaster)
    {
        return ResolveExecutionMode(player, request, skillTemplate) switch
        {
            SkillExecutionMode.Mounted => HandleMountedSkill(player, request, skillTemplate, effectiveCaster),
            SkillExecutionMode.Pet => HandlePetSkill(player, request, skillTemplate),
            SkillExecutionMode.AutoAttack => SkillExecutionResult.Success(player.AutoAttackTask.Skill),
            SkillExecutionMode.Common => HandleCommonSkill(player, request, skillTemplate),
            SkillExecutionMode.Item => HandleItemSkill(player, request, skillTemplate),
            SkillExecutionMode.Learned => HandleLearnedSkill(player, request, skillTemplate),
            SkillExecutionMode.TemporaryReplacement => HandleTemporaryReplacementSkill(player, request),
            SkillExecutionMode.Variant => HandleVariantSkill(player, request, skillTemplate),
            _ => HandleUnknownSkill(player, request, skillTemplate)
        };
    }

    private static SkillExecutionMode ResolveExecutionMode(Character player, SkillStartRequest request, SkillTemplate skillTemplate)
    {
        if (player.AttachedPoint != AttachPointKind.None && (skillTemplate.SourceMount || request.SkillCaster is SkillCasterMount))
        {
            return SkillExecutionMode.Mounted;
        }

        if (player.AttachedPoint == AttachPointKind.None && request.SkillCaster.ObjId != player.ObjId)
        {
            return SkillExecutionMode.Pet;
        }

        if (player.IsAutoAttack && request.SkillId == player.AutoAttackTask?.Skill?.Template?.Id)
        {
            return SkillExecutionMode.AutoAttack;
        }

        if (IsDefaultOrCommonSkill(request.SkillId, request.SkillCaster))
        {
            return SkillExecutionMode.Common;
        }

        if (request.SkillCaster is SkillItem)
        {
            return SkillExecutionMode.Item;
        }

        if (player.Skills.Skills.ContainsKey(request.SkillId))
        {
            return SkillExecutionMode.Learned;
        }

        if (request.SkillId > 0 && player.Skills.TryCreateTemporaryReplacementSkill(request.SkillId, out _))
        {
            return SkillExecutionMode.TemporaryReplacement;
        }

        return request.SkillId > 0 && player.Skills.IsVariantOfSkill(request.SkillId)
            ? SkillExecutionMode.Variant
            : SkillExecutionMode.Unknown;
    }

    private static bool IsDefaultOrCommonSkill(uint skillId, SkillCaster skillCaster)
    {
        return SkillManager.Instance.IsDefaultSkill(skillId) ||
               (SkillManager.Instance.IsCommonSkill(skillId) && skillCaster is not SkillItem);
    }

    private static SkillExecutionResult HandleMountedSkill(Character player, SkillStartRequest request, SkillTemplate template, BaseUnit effectiveCaster)
    {
        Logger.Info($"MOUNT SKILL: Player {player.Name} is mounted at {player.AttachedPoint}, skillId={request.SkillId}, caster={request.SkillCaster.ObjId}, casterType={request.SkillCaster.Type}");

        var caster = effectiveCaster ?? SkillContextResolver.ResolveEffectiveCaster(player, template, request.SkillCaster);
        if (caster == null || !SkillContextResolver.IsControlledBy(player, caster))
        {
            return SkillExecutionResult.Error(SkillResult.NoPerm, request.SkillCaster.ObjId);
        }

        var mountAttachedSkill = ResolveMountAttachedSkill(request.SkillId, player.AttachedPoint, caster);
        var mountCaster = request.SkillCaster as SkillCasterMount;
        var directResult = SkillExecutionResult.Success();

        if (mountCaster != null || template.SourceMount)
        {
            Logger.Trace($"SkillCasterMount - MountSkillTemplateId {mountCaster?.MountSkillTemplateId ?? 0}");
            var skill = new Skill(template);
            var serverCaster = new SkillCasterUnit(caster.ObjId);
            Logger.Trace($"MOUNT SKILL SERVER CASTER: skillId={request.SkillId}, requestCasterType={request.SkillCaster.Type}, requestCaster={request.SkillCaster.ObjId}, serverCasterType={serverCaster.Type}, serverCaster={serverCaster.ObjId}, targetType={request.SkillCastTarget.Type}, target={request.SkillCastTarget.ObjId}");
            var skillResult = skill.Use(caster, serverCaster, request.SkillCastTarget, request.SkillObject, false, out var errorValue);
            directResult = skillResult == SkillResult.Success
                ? SkillExecutionResult.Success(skill)
                : SkillExecutionResult.Error(skillResult, errorValue, skill);

            if (directResult.Result != SkillResult.Success)
            {
                return directResult;
            }
        }

        if (mountAttachedSkill == 0)
        {
            return directResult;
        }

        return ExecuteRiderSkill(player, mountAttachedSkill, request.SkillCastTarget, request.SkillObject);
    }

    private static uint ResolveMountAttachedSkill(uint skillId, AttachPointKind attachPoint, BaseUnit caster)
    {
        return caster is Mate or Slave
            ? MateManager.Instance.GetMountAttachedSkills(skillId, attachPoint)
            : 0;
    }

    private static SkillExecutionResult ExecuteRiderSkill(Character player, uint riderSkillId, SkillCastTarget originalTarget, SkillObject skillObject)
    {
        var riderTemplate = SkillManager.Instance.GetSkillTemplate(riderSkillId);
        if (riderTemplate == null)
        {
            return SkillExecutionResult.Error(SkillResult.InvalidSkill);
        }

        var riderSkill = new Skill(riderTemplate);
        var riderCaster = SkillCaster.GetByType(SkillCasterType.Unit);
        riderCaster.ObjId = player.ObjId;
        var riderTarget = SkillContextResolver.BuildRiderTarget(riderTemplate, player, originalTarget);

        Logger.Info($"RIDER SKILL: Player {player.Name} is mounted at {player.AttachedPoint}, skillId={riderSkillId}, caster={player.ObjId}, casterType={riderCaster.Type}");

        var result = riderSkill.Use(player, riderCaster, riderTarget, skillObject, false, out var errorValue);
        return result == SkillResult.Success
            ? SkillExecutionResult.Success(riderSkill)
            : SkillExecutionResult.Error(result, errorValue, riderSkill);
    }

    private static SkillExecutionResult HandlePetSkill(Character player, SkillStartRequest request, SkillTemplate template)
    {
        Logger.Info($"PET SKILL: Player {player?.Name} not mounted, but caster {request.SkillCaster.ObjId} != player {player?.ObjId}, skillId={request.SkillId}");

        var caster = WorldManager.Instance.GetBaseUnit(request.SkillCaster.ObjId);
        if (caster == null)
        {
            Logger.Warn($"Caster with ObjId {request.SkillCaster.ObjId} not found in world");
            return SkillExecutionResult.Silent();
        }

        if (!SkillContextResolver.IsControlledBy(player, caster))
        {
            Logger.Warn($"Player {player?.Name}:{player?.ObjId} tried to use uncontrolled caster {caster.ObjId} for skillId={request.SkillId}");
            return SkillExecutionResult.Error(SkillResult.NoPerm, caster.ObjId);
        }

        var skill = new Skill(template);
        var result = skill.Use(caster, request.SkillCaster, request.SkillCastTarget, request.SkillObject, false, out var errorValue);

        player.LastCombatActivity = DateTime.UtcNow;
        player.IsInBattle = true;
        Logger.Trace($"Updated player {player.Name} combat state due to pet attack");

        return result == SkillResult.Success
            ? SkillExecutionResult.Success(skill)
            : SkillExecutionResult.Error(result, errorValue, skill);
    }

    private static SkillExecutionResult HandleCommonSkill(Character player, SkillStartRequest request, SkillTemplate template)
    {
        Logger.Trace($"Using common skill {request.SkillId}, caster={request.SkillCaster.ObjId}, player={player?.Name}:{player?.ObjId}, attached={player?.AttachedPoint}");

        var skill = new Skill(template);
        var result = skill.Use(player, request.SkillCaster, request.SkillCastTarget, request.SkillObject, false, out var errorValue);
        return result == SkillResult.Success
            ? SkillExecutionResult.Success(skill)
            : SkillExecutionResult.Error(result, errorValue, skill);
    }

    private static SkillExecutionResult HandleItemSkill(Character player, SkillStartRequest request, SkillTemplate template)
    {
        if (request.SkillCaster is not SkillItem skillItem)
        {
            return SkillExecutionResult.Error(SkillResult.InvalidSource);
        }

        if (skillItem.SkillSourceItem == null ||
            request.SkillId != skillItem.SkillSourceItem.Template.UseSkillId &&
            skillItem.SkillSourceItem.Template.BindType != ItemBindType.BindOnPickup)
        {
            return SkillExecutionResult.Silent();
        }

        var skill = new Skill(template);
        var result = skill.Use(player, request.SkillCaster, request.SkillCastTarget, request.SkillObject, false, out var errorValue);
        return result == SkillResult.Success
            ? SkillExecutionResult.Success(skill)
            : SkillExecutionResult.Error(result, errorValue, skill);
    }

    private static SkillExecutionResult HandleLearnedSkill(Character player, SkillStartRequest request, SkillTemplate template)
    {
        var skill = new Skill(template, player);
        var result = skill.Use(player, request.SkillCaster, request.SkillCastTarget, request.SkillObject, false, out var errorValue);
        return result == SkillResult.Success
            ? SkillExecutionResult.Success(skill)
            : SkillExecutionResult.Error(result, errorValue, skill);
    }

    private static SkillExecutionResult HandleTemporaryReplacementSkill(Character player, SkillStartRequest request)
    {
        if (!player.Skills.TryCreateTemporaryReplacementSkill(request.SkillId, out var skill))
        {
            return SkillExecutionResult.Error(SkillResult.InvalidSkill);
        }

        var result = skill.Use(player, request.SkillCaster, request.SkillCastTarget, request.SkillObject, false, out var errorValue);
        return result == SkillResult.Success
            ? SkillExecutionResult.Success(skill)
            : SkillExecutionResult.Error(result, errorValue, skill);
    }

    private static SkillExecutionResult HandleVariantSkill(Character player, SkillStartRequest request, SkillTemplate template)
    {
        var skill = new Skill(template, player);
        var result = skill.Use(player, request.SkillCaster, request.SkillCastTarget, request.SkillObject, false, out var errorValue);
        return result == SkillResult.Success
            ? SkillExecutionResult.Success(skill)
            : SkillExecutionResult.Error(result, errorValue, skill);
    }

    private static SkillExecutionResult HandleUnknownSkill(Character player, SkillStartRequest request, SkillTemplate template)
    {
        Logger.Warn($"StartSkill: Id {request.SkillId}, undefined use type");

        var skill = new Skill(template);
        var result = skill.Use(player, request.SkillCaster, request.SkillCastTarget, request.SkillObject, false, out var errorValue);
        return result == SkillResult.Success
            ? SkillExecutionResult.Success(skill)
            : SkillExecutionResult.Error(result, errorValue, skill);
    }

    private static void HandleDismountSkill(Character player, uint skillId, SkillCastTarget skillCastTarget)
    {
        if (skillId != (uint)SkillConstants.Dismount)
        {
            return;
        }

        var slave = WorldManager.Instance.GetBaseUnit(skillCastTarget.ObjId) as Slave;
        if (slave != null)
        {
            SlaveManager.Instance.UnbindSlave(player, slave.TlId, AttachUnitReason.SlaveUnbinding);
        }
    }

    private static void HandleSkillResult(Character player, SkillStartRequest request, SkillExecutionResult result)
    {
        if (result.Result == SkillResult.Success)
        {
            player.Achievements?.TrackRecordProgress(CharRecordKind.UseSkill, request.SkillId);
            return;
        }

        if (result.SuppressResponse)
        {
            return;
        }

        var scSkillStartedPacket = new SCSkillStartedPacket(request.SkillId, 0, request.SkillCaster, request.SkillCastTarget, result.Skill, request.SkillObject);
        scSkillStartedPacket.RealCastTimeDiv10 = 0;
        scSkillStartedPacket.BaseCastTimeDiv10 = 0;
        scSkillStartedPacket.SetSkillResult(result.Result);
        scSkillStartedPacket.SetResultUInt(result.ErrorValue);
        player.SendPacket(scSkillStartedPacket);
    }
}
