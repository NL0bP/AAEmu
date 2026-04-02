using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Templates;

using MySql.Data.MySqlClient;

namespace AAEmu.Game.Models.Game.Char;

public class CharacterSkills(Character owner)
{
    public sealed class TemporarySkillReplacement
    {
        public uint OriginalSkillId { get; init; }
        public uint ReplacementSkillId { get; init; }
        public uint BuffId { get; init; }
        public byte Slot { get; init; }
    }

    private enum SkillType : byte
    {
        Skill = 1,
        Buff = 2
    }

    private readonly List<uint> _removed = new();
    private readonly Dictionary<uint, TemporarySkillReplacement> _temporaryReplacementsByNewSkill = new();
    public Dictionary<uint, Skill> Skills { get; } = new();
    public Dictionary<uint, PassiveBuff> PassiveBuffs { get; } = new();
    private Character Owner { get; } = owner;

    /// <summary>
    /// Tries to learn a new skill if the player meets all conditions.
    /// </summary>
    /// <param name="skillId">Id of the skill to learn.</param>
    public void AddSkill(uint skillId)
    {
        var template = SkillManager.Instance.GetSkillTemplate(skillId);

        // 1. Skill must belong to one of the active ability trees (if any)
        if (template.AbilityId > 0 &&
            !IsAbilityActive(template.AbilityId))
            return;

        // 2. Must have enough skill points
        var availablePoints = ExperienceManager.Instance.GetSkillPointsForLevel(Owner.Level) - GetUsedSkillPoints(AbilityType.General);
        if (template.SkillPoints > availablePoints)
            return;

        // 3. Learn or resend already known skill
        if (Skills.TryGetValue(skillId, out var existingSkill))
        {
            Owner.SendPacket(new SCSkillLearnedPacket(existingSkill));
        }
        else
        {
            AddSkill(template, 1, true);
        }

        // 4. Refresh buffs tied to ability trees
        RefreshAbilityBuffs();
    }

    /// <summary>
    /// Checks if given ability id is currently active on the owner.
    /// </summary>
    private bool IsAbilityActive(AbilityType abilityId)
    {
        return abilityId == Owner.Ability1 ||
               abilityId == Owner.Ability2 ||
               abilityId == Owner.Ability3;
    }

    /// <summary>
    /// Updates buffs for all active ability trees.
    /// </summary>
    private void RefreshAbilityBuffs()
    {
        var abilities = new[] { Owner.Ability1, Owner.Ability2, Owner.Ability3 };

        foreach (var ability in abilities)
        {
            if (ability == AbilityType.None)
                continue;

            var usedPoints = GetUsedSkillPoints(ability);
            var buffId = SkillManager.Instance.GetIdByAbilityAndReqPoints(ability, usedPoints);

            if (buffId.HasValue)
                Owner.Skills.AddBuff(buffId.Value);
        }
    }

    /// <summary>
    /// Adds a Skill and optionally sends a SCSkillLearnedPacket
    /// </summary>
    /// <param name="template"></param>
    /// <param name="level"></param>
    /// <param name="packet"></param>
    public void AddSkill(SkillTemplate template, byte level, bool packet)
    {
        var skill = new Skill
        {
            Id = template.Id,
            Template = template,
            Level = (template.LevelStep > 0 ? (byte)(((Owner.GetAbLevel(template.AbilityId) - (template.AbilityLevel)) / template.LevelStep) + 1) : (byte)1)
        };
        Skills.Add(skill.Id, skill);

        if (packet)
            Owner.SendPacket(new SCSkillLearnedPacket(skill));
    }

    /// <summary>
    /// Tries to learn a passive buff if the player meets all conditions.
    /// </summary>
    /// <param name="buffId">Id from passive_buffs table.</param>
    public void AddBuff(uint buffId)
    {
        var template = SkillManager.Instance.GetPassiveBuffTemplate(buffId);
        if (template == null)
            return;

        // 1. Must belong to an active ability tree (if any)
        if (template.AbilityId > 0 && !IsAbilityActive(template.AbilityId))
            return;

        // 2. Must have enough points invested in this tree (uncomment if required)
        //if (GetUsedSkillPoints(template.AbilityId) < template.ReqPoints)
        //    return;

        // 3. Already learned
        if (PassiveBuffs.ContainsKey(buffId))
            return;

        // 4. Add and apply
        var buff = new PassiveBuff { Id = buffId, Template = template };
        PassiveBuffs.Add(buff.Id, buff);
        Owner.BroadcastPacket(new SCBuffLearnedPacket(Owner.ObjId, buff.Id), true);
        buff.Apply(Owner);
    }

    /// <summary>
    /// Resets all skills from a specific ability Skill Tree
    /// </summary>
    /// <param name="abilityId"></param>
    public void Reset(AbilityType abilityId)
    {
        if (abilityId == AbilityType.None)
            return;

        // TODO: with price...
        foreach (var skill in new List<Skill>(Skills.Values))
        {
            if (skill.Template.AbilityId != abilityId)
                continue;
            Skills.Remove(skill.Id);
            _removed.Add(skill.Id);
        }

        foreach (var buff in new List<PassiveBuff>(PassiveBuffs.Values))
        {
            if (buff.Template.AbilityId != abilityId)
                continue;
            buff.Remove(Owner);
            PassiveBuffs.Remove(buff.Id);
            _removed.Add(buff.Id);
        }

        Owner.BroadcastPacket(new SCSkillsResetPacket(Owner.ObjId, abilityId), true);
    }

    /// <summary>
    /// Get skill points invested in total or for a specific tree
    /// </summary>
    /// <param name="ability">Ability whose Skill Tree to check. Use AbilityType.General if you want the total for all learned skills</param>
    /// <returns>Number of skill points invested</returns>
    private int GetUsedSkillPoints(AbilityType ability)
    {
        var points = 0;

        // Count points for Active Skills
        foreach (var skill in Skills.Values)
            if (ability == AbilityType.General || skill.Template.AbilityId == ability)
                points += skill.Template.SkillPoints;

        // Count points for Passive Skills (for Version 1.2)
        //foreach (var buff in PassiveBuffs.Values)
        //    if (ability == AbilityType.General || buff.Template.AbilityId == ability)
        //        points += 1; // buff.Template?.ReqPoints ?? 1;

        return points;
    }

    // TODO : Optimize this by storing a map of derivative skills and their matches
    public bool IsVariantOfSkill(uint skillId)
    {
        var skillTemplate = SkillManager.Instance.GetSkillTemplate(skillId);

        if (skillTemplate == null)
            return false;

        return Skills.Values.Any(skill =>
            skill.Template.AbilityId == skillTemplate.AbilityId &&
            skill.Template.AbilityLevel == skillTemplate.AbilityLevel);
    }

    public void RegisterTemporarySkillReplacement(uint originalSkillId, uint replacementSkillId, uint buffId, byte slot)
    {
        if (originalSkillId == 0 || replacementSkillId == 0 || buffId == 0)
            return;

        if (!Skills.ContainsKey(originalSkillId) && !IsVariantOfSkill(originalSkillId))
            return;

        var replacementTemplate = SkillManager.Instance.GetSkillTemplate(replacementSkillId);
        if (replacementTemplate == null)
            return;

        _temporaryReplacementsByNewSkill[replacementSkillId] = new TemporarySkillReplacement
        {
            OriginalSkillId = originalSkillId,
            ReplacementSkillId = replacementSkillId,
            BuffId = buffId,
            Slot = slot
        };
    }

    public bool TryCreateTemporaryReplacementSkill(uint replacementSkillId, out Skill skill)
    {
        skill = null;
        CleanupExpiredTemporaryReplacements();

        if (!_temporaryReplacementsByNewSkill.TryGetValue(replacementSkillId, out var replacement))
            return false;

        if (!Owner.Buffs.CheckBuff(replacement.BuffId))
        {
            _temporaryReplacementsByNewSkill.Remove(replacementSkillId);
            return false;
        }

        var template = SkillManager.Instance.GetSkillTemplate(replacementSkillId);
        if (template == null)
            return false;

        skill = new Skill(template, Owner);
        return true;
    }

    public void CleanupExpiredTemporaryReplacements()
    {
        if (_temporaryReplacementsByNewSkill.Count == 0)
            return;

        foreach (var replacement in _temporaryReplacementsByNewSkill.Values.ToList())
        {
            if (!Owner.Buffs.CheckBuff(replacement.BuffId))
                _temporaryReplacementsByNewSkill.Remove(replacement.ReplacementSkillId);
        }
    }

    public List<HeirSkill> GetHeroSkillsFromSkills()
    {
        var heirSkills = new List<HeirSkill>();

        foreach (var skill in Skills.Values)
        {
            // Пропускаем обычные (не heir) умения
            if (skill.Id < 36400)
                continue;

            // Получаем детали heir-навыка
            var heirSkillDetail = SkillManager.Instance.GetHeirSkillDetail(skill.Id);
            if (heirSkillDetail == null)
                continue;

            // Формируем итоговую структуру HeirSkill
            var heirSkill = new HeirSkill();
            heirSkill.Id = heirSkillDetail.HeirSkillId;
            heirSkill.SkillId = SkillManager.Instance.GetSkillIdByHeirSkillId(heirSkillDetail.HeirSkillId) ?? 0;
            heirSkill.HeirSkillId = heirSkillDetail.SkillId;
            heirSkill.SkillLevel = skill.Level;
            heirSkill.Ability = (byte)skill.Template.AbilityId;
            heirSkill.HighAbility = (byte)skill.Template.HighAbilityId;
            heirSkill.ActiveType = false;

            heirSkills.Add(heirSkill);
        }

        return heirSkills;
    }

    public void AddHeroSkill(uint HeirSkillId, uint skillId, bool isChange = false, bool packet = false)
    {
        // Check if what we want to learn is part of an active skill tree (or not part of one)
        var template = SkillManager.Instance.GetSkillTemplate(skillId);
        if (template.AbilityId > 0 &&
            template.AbilityId != Owner.Ability1 &&
            template.AbilityId != Owner.Ability2 &&
            template.AbilityId != Owner.Ability3)
            return;

        if (packet)
        {
            if (isChange)
            {
                Owner.Skills.HeroReset(HeirSkillId, (byte)template.AbilityId, skillId, true);
                Owner.SendPacket(new SCActivatedHeirSkillPacket(HeirSkillId, skillId, true));
            }
            else
                Owner.SendPacket(new SCActivatedHeirSkillPacket(HeirSkillId, skillId, false));
        }

        var skill = new Skill
        {
            Id = template.Id,
            Template = template,
            Level = (template.LevelStep > 0 ? (byte)(((Owner.GetAbLevel(template.AbilityId) - (template.AbilityLevel)) / template.LevelStep) + 1) : (byte)1)
        };
        Skills.TryAdd(skill.Id, skill);
    }

    public void AddHeroSkill(uint skillId)
    {
        // Check if what we want to learn is part of an active skill tree (or not part of one)
        var template = SkillManager.Instance.GetSkillTemplate(skillId);
        if (template.AbilityId > 0 &&
            template.AbilityId != Owner.Ability1 &&
            template.AbilityId != Owner.Ability2 &&
            template.AbilityId != Owner.Ability3)
            return;

        var skill = new Skill
        {
            Id = template.Id,
            Template = template,
            Level = (template.LevelStep > 0 ? (byte)(((Owner.GetAbLevel(template.AbilityId) - (template.AbilityLevel)) / template.LevelStep) + 1) : (byte)1)
        };
        Skills.Add(skill.Id, skill);
    }

    public void HeroReset(uint resetKind, byte abilityId, uint skillId, bool isChange = false)
    {
        if (Owner.Money < 6500)
        {
            Owner.SendErrorMessage(ErrorMessageType.NotEnoughMoney);
            return;
        }

        var tasks = new List<ItemTask>();

        if (isChange)
        {
            var removeSkillId = 0u;

            // Получаем информацию о новом скилле
            var newSkillDetail = SkillManager.Instance.GetHeirSkillDetail(skillId);
            if (newSkillDetail == null)
            {
                //Owner.SendErrorMessage(ErrorMessageType.InvalidSkill);
                return;
            }

            var newSkillTemplate = SkillManager.Instance.GetHeirSkillTemplate(newSkillDetail.HeirSkillId);
            if (newSkillTemplate == null)
            {
                //Owner.SendErrorMessage(ErrorMessageType.InvalidSkill);
                return;
            }

            // Ищем скиллы для замены среди изученных скиллов того же abilityId
            foreach (var skill in new List<Skill>(Skills.Values))
            {
                if (skill.Template.AbilityId != (AbilityType)abilityId)
                    continue;

                if (skill.Id <= 36400) // с 36401 начинаются героические скиллы
                    continue;

                // Получаем информацию о текущем изученном скилле
                var currentSkillDetail = SkillManager.Instance.GetHeirSkillDetail(skill.Id);
                if (currentSkillDetail == null)
                    continue;

                var currentSkillsTemplate = SkillManager.Instance.GetHeirSkillsDetail(currentSkillDetail.HeirSkillId);
                foreach (var detailTemplate in currentSkillsTemplate)
                {
                    if (detailTemplate == null)
                        continue;
                    // Проверяем, являются ли скиллы взаимозаменяемыми (одинаковый Pos)
                    if (detailTemplate.HeirSkillId == newSkillDetail.HeirSkillId)
                    {
                        removeSkillId = skill.Id;
                        Skills.Remove(skill.Id);
                        _removed.Add(skill.Id);
                        break;
                    }
                }
            }

            Owner.SendPacket(new SCResetHeirSkillPacket(resetKind, removeSkillId, abilityId));
        }
        else
        {
            // Удаляем skillId
            Skills.Remove(skillId);
            _removed.Add(skillId);
            Owner.SendPacket(new SCResetHeirSkillPacket(resetKind, skillId, abilityId));
        }

        //Owner.ChangeMoney(SlotType.Inventory, -6500);
        tasks.Add(new MoneyChange(-6500));
        Owner.SendPacket(new SCItemTaskSuccessPacket(ItemTaskType.HeirSkillReset, tasks, []));
    }

    #region database
    public void Load(MySqlConnection connection)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM skills WHERE `owner` = @owner";
            command.Parameters.AddWithValue("@owner", Owner.Id);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var type = (SkillType)Enum.Parse(typeof(SkillType), reader.GetString("type"), true);
                    switch (type)
                    {
                        case SkillType.Skill:
                            var skill = new Skill
                            {
                                Id = reader.GetUInt32("id"),
                                Level = reader.GetByte("level")
                            };

                            if (skill.Id > 36400) // с 36401 начинаются героические скиллы
                                AddHeroSkill(skill.Id);
                            else
                                AddSkill(skill.Id);

                            break;
                        case SkillType.Buff:
                            var buffId = reader.GetUInt32("id");
                            var buff = new PassiveBuff { Id = buffId, Template = SkillManager.Instance.GetPassiveBuffTemplate(buffId) };
                            PassiveBuffs.Add(buff.Id, buff);
                            buff.Apply(Owner);
                            break;
                    }
                }
            }
        }

        foreach (var skill in Skills.Values)
            if (skill != null)
                skill.Template = SkillManager.Instance.GetSkillTemplate(skill.Id);
    }

    public void Save(MySqlConnection connection, MySqlTransaction transaction)
    {
        if (_removed.Count > 0)
        {
            using var command = connection.CreateCommand();
            command.Connection = connection;
            command.Transaction = transaction;

            command.CommandText = "DELETE FROM skills WHERE owner = @owner AND id IN(" + string.Join(",", _removed) + ")";
            command.Parameters.AddWithValue("@owner", Owner.Id);
            command.Prepare();
            command.ExecuteNonQuery();
            _removed.Clear();
        }

        foreach (var skill in Skills.Values)
        {
            using var command = connection.CreateCommand();
            command.Connection = connection;
            command.Transaction = transaction;

            command.CommandText = "REPLACE INTO skills(`id`,`level`,`type`,`owner`) VALUES (@id, @level, @type, @owner)";
            command.Parameters.AddWithValue("@id", skill.Id);
            command.Parameters.AddWithValue("@level", skill.Level);
            command.Parameters.AddWithValue("@type", (byte)SkillType.Skill);
            command.Parameters.AddWithValue("@owner", Owner.Id);
            command.ExecuteNonQuery();
        }

        foreach (var buff in PassiveBuffs.Values)
        {
            using var command = connection.CreateCommand();
            command.Connection = connection;
            command.Transaction = transaction;

            command.CommandText = "REPLACE INTO skills(`id`,`level`,`type`,`owner`) VALUES(@id,@level,@type,@owner)";
            command.Parameters.AddWithValue("@id", buff.Id);
            command.Parameters.AddWithValue("@level", 1);
            command.Parameters.AddWithValue("@type", (byte)SkillType.Buff);
            command.Parameters.AddWithValue("@owner", Owner.Id);
            command.ExecuteNonQuery();
        }
    }

    #endregion
}
