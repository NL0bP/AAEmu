using System;
using System.Threading;
using System.Threading.Tasks;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Static;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.slaves;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSStartSkillPacket : GamePacket
{
    public CSStartSkillPacket() : base(CSOffsets.CSStartSkillPacket, 5)
    {
    }

    public override void Read(PacketStream stream)
    {
        // Ignore if there is no active character set
        if (Connection.ActiveChar == null)
            return;

        var player = Connection.ActiveChar;

        // Will delay for 150 Milliseconds to eliminate the hanging of the skill
        using var source = new CancellationTokenSource();
        var t = Task.Run(async delegate
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), source.Token);
            return 0;
        });
        try
        {
            t.Wait();
        }
        catch (AggregateException ae)
        {
            foreach (var e in ae.InnerExceptions)
                Logger.Trace("{0}: {1}", e.GetType().Name, e.Message);
        }

        var skillId = stream.ReadUInt32();

        var skillCasterType = stream.ReadByte(); // кто применяет
        var skillCaster = SkillCaster.GetByType((SkillCasterType)skillCasterType);
        skillCaster.Read(stream);

        var skillCastTargetType = stream.ReadByte(); // на кого применяют
        var skillCastTarget = SkillCastTarget.GetByType((SkillCastTargetType)skillCastTargetType);
        skillCastTarget.Read(stream);

        var flag = stream.ReadByte();
        var flagType = flag & 63; // in 1.2 = 15, in 3+ = 63
        var skillObject = SkillObject.GetByType((SkillObjectType)flagType);
        if (flagType > 0) skillObject.Read(stream);

        Logger.Info($"StartSkill: Id {skillId}, flag {flag}, caster={skillCaster.ObjId}, target={skillCastTarget.ObjId}");

        var skillResult = SkillResult.Success;
        var skillResultErrorValue = 0u;
        Skill skill = null;

        if (skillCaster is SkillCasterUnit scu)
        {
            var unit = WorldManager.Instance.GetUnit(scu.ObjId);
            if (unit is Character character)
                Logger.Info($"{character.Name}:{character.ObjId} is using skill={skillId}");
        }

        // Check if player is mounted/riding and handle mount/pet skills
        if (Connection.ActiveChar?.AttachedPoint != AttachPointKind.None)
        {
            // Player is on a mount/pet - handle mount skills
            Logger.Info($"MOUNT SKILL: Player {player.Name} is mounted at {player.AttachedPoint}, skillId={skillId}, caster={skillCaster.ObjId}, casterType={skillCaster.Type}");

            var caster = WorldManager.Instance.GetBaseUnit(skillCaster.ObjId);
            var mate = caster as Mate;
            var slave = caster as Slave;
            var mountAttachedSkill = 0u;

            if ((mate != null) || (slave != null))
            {
                // check if it's a mate or slave skill and return its rider/operator related skill
                mountAttachedSkill = MateManager.Instance.GetMountAttachedSkills(skillId, player.AttachedPoint);
            }

            // Use the main skill on the mate/slave
            if (skillCaster is SkillCasterMount scm)
            {
                // Direct mount skill
                Logger.Trace($"SkillCasterMount - MountSkillTemplateId {scm.MountSkillTemplateId}");
                skill = new Skill(SkillManager.Instance.GetSkillTemplate(skillId));
                if (skill.Use(caster, skillCaster, skillCastTarget, skillObject, false, out skillResultErrorValue) != SkillResult.Success)
                {
                    // skill.Stop(caster, null, skillCaster);
                }
            }

            // If no rider/operator skill is linked, we can stop here
            if (mountAttachedSkill == 0)
                return;

            // Use player's currently selected for the rider/operator skill
            var riderTarget = player.CurrentTarget as Unit;

            // Get the actual mount/pet unit for proper range calculation
            var petCaster = WorldManager.Instance.GetBaseUnit(skillCaster.ObjId);
            if (petCaster == null)
            {
                Logger.Warn($"Mount/pet with ObjId {skillCaster.ObjId} not found in world");
                return;
            }

            // Create skill for rider/operator with correct caster (the mount/pet, not the player)
            var riderSkill = new Skill(SkillManager.Instance.GetSkillTemplate(mountAttachedSkill));
            var riderCaster = SkillCaster.GetByType(SkillCasterType.Unit);
            riderCaster.ObjId = skillCaster.ObjId; // Use the mount/pet's ObjId as caster

            var riderTargetCaster = SkillCastTarget.GetByType(SkillCastTargetType.Unit);
            riderTargetCaster.ObjId = (riderTarget ?? player).ObjId;

            // Execute the rider/operator skill with mount/pet as caster for proper range calculation
            skillResult = riderSkill.Use(petCaster, riderCaster, riderTargetCaster, null, false, out skillResultErrorValue);
        }
        else if (player?.AttachedPoint == AttachPointKind.None && skillCaster.ObjId != player?.ObjId)
        {
            // Player is not mounted but caster is not the player - this is a pet/mount skill
            Logger.Info($"PET SKILL: Player {player?.Name} not mounted, but caster {skillCaster.ObjId} != player {player?.ObjId}, skillId={skillId}");

            var caster = WorldManager.Instance.GetBaseUnit(skillCaster.ObjId);
            if (caster == null)
            {
                Logger.Warn($"Caster with ObjId {skillCaster.ObjId} not found in world");
                return;
            }

            // Use the pet/mount as caster for proper range calculation
            skill = new Skill(SkillManager.Instance.GetSkillTemplate(skillId));
            skillResult = skill.Use(caster, skillCaster, skillCastTarget, skillObject, false, out skillResultErrorValue);

            // IMPORTANT: Update player's combat activity when pet attacks
            // This ensures player's combat state is properly managed
            if (player != null)
            {
                player.LastCombatActivity = DateTime.UtcNow;
                player.IsInBattle = true;
                Logger.Trace($"Updated player {player.Name} combat state due to pet attack");
            }
        }
        else if (player.IsAutoAttack && skillId == player.AutoAttackTask?.Skill?.Template?.Id)
        {
            // Same as already executing auto-skill, just send the success result.
            skill = player.AutoAttackTask.Skill;
            skillResult = SkillResult.Success;
        }
        else if (SkillManager.Instance.IsDefaultSkill(skillId) || SkillManager.Instance.IsCommonSkill(skillId) && !(skillCaster is SkillItem))
        {
            // Is it a common skill?
            Logger.Trace($"Using common skill {skillId}, caster={skillCaster.ObjId}, player={player?.Name}:{player?.ObjId}, attached={player?.AttachedPoint}");
            skill = new Skill(SkillManager.Instance.GetSkillTemplate(skillId)); // TODO: переделать / rewrite ...

            // Check if player is mounted and use pet as caster for proper range calculation
            BaseUnit casterUnit = player;
            if (player?.AttachedPoint != AttachPointKind.None)
            {
                // Player is mounted, use the mount/pet as caster for proper range calculation
                var petCaster = WorldManager.Instance.GetBaseUnit(skillCaster.ObjId);
                if (petCaster != null)
                {
                    casterUnit = petCaster;
                    Logger.Trace($"Using mounted skill with pet caster {skillCaster.ObjId} for player {player.Name}");
                }
                else
                {
                    Logger.Warn($"Mount/pet with ObjId {skillCaster.ObjId} not found in world for common skill");
                }
            }

            skillResult = skill.Use(casterUnit, skillCaster, skillCastTarget, skillObject, false, out skillResultErrorValue);

            // Check if this is a basic combat skill and player is the direct caster (not mounted)
            if ((skillResult == SkillResult.Success) && (skillId < 5000) && (skillCaster.ObjId == player.ObjId) && (player?.AttachedPoint == AttachPointKind.None))
            {
                // All basic combat skills are below ID 5000, only 2 (melee),3 (offhand) and 4 (ranged) exist, next actual skill used is 5001
                player.IsAutoAttack = true;
                player.StartAutoSkill(skill);
            }
        }
        else if (skillCaster is SkillItem si)
        {
            // A skill triggered by a item
            // var item = player.Inventory.GetItemById(si.ItemId);
            // добавил проверку на ItemBindType.BindOnPickup для записи портала с помощью камина в доме
            if (si.SkillSourceItem == null || skillId != si.SkillSourceItem.Template.UseSkillId && si.SkillSourceItem.Template.BindType != ItemBindType.BindOnPickup)
                return;
            // si.ItemTemplateId = item.TemplateId;
            skill = new Skill(SkillManager.Instance.GetSkillTemplate(skillId));
            skillResult = skill.Use(player, skillCaster, skillCastTarget, skillObject, false, out skillResultErrorValue);
        }
        else if (player.Skills.Skills.ContainsKey(skillId))
        {
            // Is it one of our learned character skills?
            var template = SkillManager.Instance.GetSkillTemplate(skillId);
            skill = new Skill(template, Connection.ActiveChar);
            skillResult = skill.Use(Connection.ActiveChar, skillCaster, skillCastTarget, skillObject, false, out skillResultErrorValue);
        }
        else if (skillId > 0 && player.Skills.IsVariantOfSkill(skillId))
        {
            // Variant of learned skill?
            skill = new Skill(SkillManager.Instance.GetSkillTemplate(skillId));
            skillResult = skill.Use(Connection.ActiveChar, skillCaster, skillCastTarget, skillObject, false, out skillResultErrorValue);
        }
        else
        {
            // No idea what this is
            Logger.Warn($"StartSkill: Id {skillId}, undefined use type");
            // If it's a valid skill cast it. This fixes interactions with quest items/doodads.
            skill = new Skill(SkillManager.Instance.GetSkillTemplate(skillId));
            skillResult = skill.Use(Connection.ActiveChar, skillCaster, skillCastTarget, skillObject, false, out skillResultErrorValue);
        }

        // HACKFIX: dismount from slave
        if (skillId == (uint)SkillConstants.Dismount)
        {
            var slave = (Slave)WorldManager.Instance.GetBaseUnit(skillCastTarget.ObjId);
            if (slave != null)
            {
                SlaveManager.Instance.UnbindSlave(Connection.ActiveChar, slave.TlId, AttachUnitReason.SlaveUnbinding);
            }
        }

        if (skillResult != SkillResult.Success)
        {
            // It actually sends a skill started packet, but not a skill fired or stopped
            var scSkillStartedPacket = new SCSkillStartedPacket(skillId, 0, skillCaster, skillCastTarget, skill, skillObject);
            scSkillStartedPacket.RealCastTimeDiv10 = 0;
            scSkillStartedPacket.BaseCastTimeDiv10 = 0;
            // ExtraData at the end of the packet is used to mark a use error
            scSkillStartedPacket.SetSkillResult(skillResult);
            scSkillStartedPacket.SetResultUInt(skillResultErrorValue);
            player.SendPacket(scSkillStartedPacket);
        }
    }
}
