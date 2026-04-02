using System;

using AAEmu.Commons.Network;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Effects;

public class ExtendChargeEffect : EffectTemplate
{
    public uint ChargeBuffId { get; set; }
    public DamageType DamageType { get; set; }
    public float DpsIncMultiplier { get; set; }
    public float DpsMultiplier { get; set; }
    public int FixedMax { get; set; }
    public int FixedMin { get; set; }
    public float LevelMd { get; set; }
    public int LevelVaEnd { get; set; }
    public int LevelVaStart { get; set; }
    public int PercentMax { get; set; }
    public int PercentMin { get; set; }
    public bool UseCurrentHealth { get; set; }
    public bool UseDpsCharge { get; set; }
    public bool UseFixedCharge { get; set; }
    public bool UseLevelCharge { get; set; }
    public bool UseMainhandWeapon { get; set; }
    public bool UseOffhandWeapon { get; set; }
    public bool UsePercentCharge { get; set; }
    public bool UseRangedWeapon { get; set; }

    public override bool OnActionTime => false;

    public override void Apply(BaseUnit caster, SkillCaster casterObj, BaseUnit target, SkillCastTarget targetObj,
        CastAction castObj, EffectSource source, SkillObject skillObject, DateTime time,
        CompressedGamePackets packetBuilder = null)
    {
        if (target is not Unit targetUnit || caster is not Unit casterUnit)
            return;

        var buffTemplate = SkillManager.Instance.GetBuffTemplate(ChargeBuffId);
        if (buffTemplate == null)
        {
            Logger.Warn("ExtendChargeEffect {0} missing buff template {1}", Id, ChargeBuffId);
            return;
        }

        var min = 0f;
        var max = 0f;

        if (UseFixedCharge)
        {
            min += FixedMin;
            max += FixedMax;
        }

        if (UseLevelCharge)
        {
            var levelBase = casterUnit.LevelDps * LevelMd;
            var levelModifier = (((source.Skill?.Level ?? 1) - 1) / 49f * (LevelVaEnd - LevelVaStart) + LevelVaStart) * 0.01f;
            min += levelBase - levelModifier * levelBase + 0.5f;
            max += (levelModifier + 1f) * levelBase + 0.5f;
        }

        if (UseDpsCharge)
        {
            var dpsInc = DamageType switch
            {
                DamageType.Melee => casterUnit.DpsInc,
                DamageType.Magic => casterUnit.MDps + casterUnit.MDpsInc,
                DamageType.Ranged => casterUnit.RangedDpsInc,
                _ => 0
            };

            max += dpsInc * 0.001f * DpsIncMultiplier;

            var weaponDamage = 0.0f;
            if (UseMainhandWeapon)
                weaponDamage += casterUnit.Dps * 0.001f;
            if (UseOffhandWeapon)
                weaponDamage += casterUnit.OffhandDps * 0.001f;
            if (UseRangedWeapon)
                weaponDamage += casterUnit.RangedDps * 0.001f;

            max += DpsMultiplier * weaponDamage;

            var castTimeMod = source.Skill?.Template.CastingTime ?? 0;
            var castMultiplier = castTimeMod <= 1000 ? 1f : castTimeMod * 0.001f;
            min *= castMultiplier;
            max *= castMultiplier;
        }

        if (UsePercentCharge)
        {
            var baseHealth = UseCurrentHealth ? targetUnit.Hp : targetUnit.MaxHp;
            min += baseHealth * (PercentMin / 100f);
            max += baseHealth * (PercentMax / 100f);
        }

        var damageMultiplier = DamageType switch
        {
            DamageType.Melee => casterUnit.MeleeDamageMul,
            DamageType.Magic => casterUnit.SpellDamageMul,
            DamageType.Ranged => casterUnit.RangedDamageMul,
            _ => 1f
        };

        min *= damageMultiplier;
        max *= damageMultiplier;

        if (source.Skill != null)
        {
            min = (float)caster.SkillModifiersCache.ApplyModifiers(source.Skill, SkillAttribute.Damage, min);
            max = (float)caster.SkillModifiersCache.ApplyModifiers(source.Skill, SkillAttribute.Damage, max);
        }

        if (max < min)
            max = min;

        var charge = (int)MathF.Round(max <= min ? min : Rand.Next(min, max));
        charge = Math.Max(0, charge);
        if (buffTemplate.MaxCharge > 0)
            charge = Math.Min(charge, buffTemplate.MaxCharge);

        var buff = new Buff(target, caster, casterObj, buffTemplate, source.Skill, time)
        {
            Charge = charge
        };

        target.Buffs.AddBuff(buff);
    }
}
