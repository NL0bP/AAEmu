using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Static;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.slaves;

namespace AAEmu.Game.Models.Game.Skills.Resolvers;

using UnitMate = AAEmu.Game.Models.Game.Units.Mate;

public static class SkillContextResolver
{
    public static BaseUnit AuthoritativeResolve(Character player, SkillTemplate skillTemplate, ref SkillCaster skillCaster, ref SkillCastTarget skillCastTarget)
    {
        if (player == null || skillCaster == null)
        {
            return null;
        }

        var effectiveCaster = ResolveEffectiveCaster(player, skillTemplate, skillCaster);
        if (effectiveCaster != null)
        {
            skillCaster.ObjId = effectiveCaster.ObjId;
        }

        skillCastTarget = NormalizeTarget(skillTemplate, player, effectiveCaster ?? player, skillCastTarget);
        return effectiveCaster;
    }

    public static BaseUnit ResolveEffectiveCaster(Character player, SkillTemplate skillTemplate, SkillCaster skillCaster)
    {
        if (player == null)
        {
            return null;
        }

        if (RequiresAttachedCaster(player, skillTemplate, skillCaster))
        {
            var attachedCaster = ResolveAttachedBaseUnit(player);
            if (attachedCaster != null)
            {
                return attachedCaster;
            }
        }

        if (skillCaster?.ObjId > 0)
        {
            var packetCaster = WorldManager.Instance.GetBaseUnit(skillCaster.ObjId);
            if (packetCaster != null && IsAllowedPacketCaster(player, skillTemplate, skillCaster, packetCaster))
            {
                return packetCaster;
            }
        }

        return player;
    }

    public static BaseUnit ResolveAttachedBaseUnit(Character player)
    {
        for (var transform = player?.Transform?.Parent; transform != null; transform = transform.Parent)
        {
            if (transform.GameObject is BaseUnit baseUnit)
            {
                return baseUnit;
            }
        }

        return null;
    }

    public static bool IsControlledBy(Character player, BaseUnit unit)
    {
        if (player == null || unit == null)
        {
            return false;
        }

        if (unit.ObjId == player.ObjId)
        {
            return true;
        }

        if (unit is UnitMate mate)
        {
            if (mate.OwnerObjId == player.ObjId)
            {
                return true;
            }

            foreach (var passenger in mate.Passengers.Values)
            {
                if (passenger.ObjId == player.ObjId)
                {
                    return true;
                }
            }
        }

        if (unit is Slave slave)
        {
            if (slave.OwnerObjId == player.ObjId || slave.Summoner?.ObjId == player.ObjId)
            {
                return true;
            }

            foreach (var character in slave.AttachedCharacters.Values)
            {
                if (character?.ObjId == player.ObjId)
                {
                    return true;
                }
            }
        }

        return IsInTransformParentChain(player, unit);
    }

    public static SkillCastTarget NormalizeTarget(SkillTemplate skillTemplate, Character player, BaseUnit effectiveCaster, SkillCastTarget originalTarget)
    {
        if (skillTemplate == null)
        {
            return originalTarget;
        }

        if (skillTemplate.TargetSelection == SkillTargetSelection.Source || skillTemplate.TargetType == SkillTargetType.Self)
        {
            return CreateUnitTarget(effectiveCaster?.ObjId ?? player?.ObjId ?? originalTarget?.ObjId ?? 0);
        }

        if (skillTemplate.TargetType == SkillTargetType.Parent)
        {
            var parentTarget = ResolveRootParentTarget(effectiveCaster) ?? ResolveAttachedBaseUnit(player);
            if (parentTarget != null)
            {
                return CreateUnitTarget(parentTarget.ObjId);
            }
        }

        return originalTarget;
    }

    public static SkillCastTarget BuildRiderTarget(SkillTemplate skillTemplate, Character player, SkillCastTarget originalTarget)
    {
        if (player == null)
        {
            return originalTarget;
        }

        if (skillTemplate?.TargetSelection == SkillTargetSelection.Source || skillTemplate?.TargetType == SkillTargetType.Self)
        {
            return CreateUnitTarget(player.ObjId);
        }

        return CloneOrCreateTarget(originalTarget, player.ObjId);
    }

    public static SkillCastTarget CloneOrCreateTarget(SkillCastTarget target, uint fallbackObjId)
    {
        if (target == null)
        {
            return CreateUnitTarget(fallbackObjId);
        }

        switch (target)
        {
            case SkillCastUnitTarget unitTarget:
                return CreateUnitTarget(unitTarget.ObjId);
            case SkillCastDoodadTarget doodadTarget:
                return new SkillCastDoodadTarget { ObjId = doodadTarget.ObjId };
            case SkillCastItemTarget itemTarget:
                return new SkillCastItemTarget
                {
                    ObjId = itemTarget.ObjId,
                    Id = itemTarget.Id,
                    ItemType = itemTarget.ItemType,
                    Grade = itemTarget.Grade
                };
            case SkillCastPositionTarget positionTarget:
                return new SkillCastPositionTarget
                {
                    ObjId = positionTarget.ObjId,
                    PosX = positionTarget.PosX,
                    PosY = positionTarget.PosY,
                    PosZ = positionTarget.PosZ,
                    PosRot = positionTarget.PosRot,
                    ObjId1 = positionTarget.ObjId1,
                    ObjId2 = positionTarget.ObjId2,
                    ObjId3 = positionTarget.ObjId3
                };
            case SkillCastPosition2Target position2Target:
                return new SkillCastPosition2Target
                {
                    PosX = position2Target.PosX,
                    PosY = position2Target.PosY,
                    PosZ = position2Target.PosZ,
                    EndPosX = position2Target.EndPosX,
                    EndPosY = position2Target.EndPosY,
                    EndPosZ = position2Target.EndPosZ,
                    NormX = position2Target.NormX,
                    NormY = position2Target.NormY,
                    NormZ = position2Target.NormZ
                };
            case SkillCastPosition3Target position3Target:
                return new SkillCastPosition3Target
                {
                    PosX = position3Target.PosX,
                    PosY = position3Target.PosY,
                    PosZ = position3Target.PosZ,
                    Pitch = position3Target.Pitch
                };
            default:
                return CreateUnitTarget(fallbackObjId);
        }
    }

    private static bool RequiresAttachedCaster(Character player, SkillTemplate skillTemplate, SkillCaster skillCaster)
    {
        if (player.AttachedPoint == AttachPointKind.None)
        {
            return false;
        }

        return skillCaster?.Type == SkillCasterType.Mount || skillTemplate?.SourceMount == true;
    }

    private static bool IsAllowedPacketCaster(Character player, SkillTemplate skillTemplate, SkillCaster skillCaster, BaseUnit packetCaster)
    {
        if (packetCaster.ObjId == player.ObjId)
        {
            return true;
        }

        if (RequiresAttachedCaster(player, skillTemplate, skillCaster))
        {
            return IsControlledBy(player, packetCaster);
        }

        if (player.AttachedPoint != AttachPointKind.None)
        {
            return false;
        }

        return IsControlledBy(player, packetCaster);
    }

    private static bool IsInTransformParentChain(Character player, BaseUnit unit)
    {
        for (var transform = player?.Transform?.Parent; transform != null; transform = transform.Parent)
        {
            if (transform.GameObject == unit)
            {
                return true;
            }
        }

        return false;
    }

    private static BaseUnit ResolveRootParentTarget(BaseUnit baseUnit)
    {
        var current = baseUnit;
        BaseUnit topLevel = null;
        while (current != null)
        {
            if (current.ParentObj is BaseUnit parentObj)
            {
                topLevel = parentObj;
                current = parentObj;
                continue;
            }

            if (current.Transform?.Parent?.GameObject is BaseUnit parentTransformObj)
            {
                topLevel = parentTransformObj;
                current = parentTransformObj;
                continue;
            }

            break;
        }

        return topLevel;
    }

    private static SkillCastUnitTarget CreateUnitTarget(uint objId)
    {
        return new SkillCastUnitTarget(objId);
    }
}
