using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NLog;

namespace AAEmu.Game.Models.Game.Units.slaves;

public class ShipComponentParser
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    public class ShipComponentData
    {
        // Basic attributes
        public int Id { get; set; }  // Component ID from the first column
        public int RepairCost { get; set; }
        public string RepairMaterial { get; set; } = "곤의 모래"; // Default material
        public List<string> CompatibleShips { get; set; } = new List<string>();
        public string InstallablePart { get; set; }
        public int Health { get; set; }
        public int HealthBonus { get; set; }
        public int Weight { get; set; }
        public string Description { get; set; }
        public int Tier { get; set; }  // Enhancement tier (0-12)
        public int VisualId { get; set; }  // Visual/display ID
        public int DbId { get; set; }  // Database ID

        // Weapon attributes
        public float MinRange { get; set; }
        public float MaxRange { get; set; }
        public float ExplosionRadius { get; set; }
        public int MinDamage { get; set; }
        public int MaxDamage { get; set; }
        public int SiegeMinDamage { get; set; }
        public int SiegeMaxDamage { get; set; }
        public float ProjectileSpeed { get; set; } // m/s
        public float Cooldown { get; set; } // seconds
        public string DamageType { get; set; } // "일반", "공성" etc
        public bool IsTwoSlot { get; set; } // Takes 2 slots

        // Special effects
        public List<string> AddedSkills { get; set; } = new List<string>();
        public List<ComponentEffect> Effects { get; set; } = new List<ComponentEffect>();
        public float SiegeDamageMultiplier { get; set; } = 1f; // For sails (돛)
        public float SpeedBoost { get; set; } // For propulsion systems
        public float DamageReduction { get; set; } // % damage reduction

        // Ship-specific bonuses
        public float LeviathanSlowEffect { get; set; }
        public float KnockdownChance { get; set; }
    }

    public class ComponentEffect
    {
        public string Description { get; set; }
        public float Value { get; set; }
        public string Unit { get; set; }
        public float EffectCooldown { get; set; } // in seconds
        public int Charges { get; set; } // For special ammo etc
    }

    // Parses the full component data from string
    public static ShipComponentData ParseComponentData(int templateId, string input)
    {
        var data = new ShipComponentData();

        // First parse the ID and basic attributes from the start
        ParseIdAndBasicAttributes(templateId, input, data);

        // Then parse weapon-specific attributes if present
        if (data.InstallablePart != null && (data.InstallablePart.Contains("함포") || data.InstallablePart.Contains("거대 함포")))
        {
            ParseWeaponAttributes(input, data);
        }

        // Parse propulsion/sail attributes
        if (data.InstallablePart != null && (data.InstallablePart.Contains("돛") || data.InstallablePart.Contains("추진 장치")))
        {
            ParseMovementAttributes(input, data);
        }

        // Parse all special effects
        ParseSpecialEffects(input, data);

        return data;
    }

    private static void ParseIdAndBasicAttributes(int templateId, string input, ShipComponentData data)
    {
        // Parse ID from the very first number
        data.Id = templateId;

        //// Repair cost and material
        //var repairMatch = Regex.Match(input, @"완파시 복원 비용: \|nc;([^|]+)\|r");
        //if (repairMatch.Success)
        //{
        //    var parts = repairMatch.Groups[1].Value.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
        //    if (parts.Length >= 2)
        //    {
        //        data.RepairMaterial = parts[0];
        //        data.RepairCost = int.Parse(parts[1].Replace("개", ""));
        //    }
        //}
        // Repair cost parsing - finds only the numeric value
        var repairMatch = Regex.Match(input, @"완파시 복원 비용: \|nc;[^\d]*(\d+)");
        if (repairMatch.Success)
        {
            data.RepairCost = int.Parse(repairMatch.Groups[1].Value);
            //data.RepairMaterial = "곤의 모래"; // Default material
        }

        // Compatible ships
        var shipsMatch = Regex.Match(input, @"장착가능 함선: \|nc;(.*?)\|r");
        if (shipsMatch.Success)
        {
            data.CompatibleShips.AddRange(shipsMatch.Groups[1].Value.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries));
        }

        // Installable part
        var partMatch = Regex.Match(input, @"장착가능 부위: \|nc;(.*?)\|r");
        if (partMatch.Success)
            data.InstallablePart = partMatch.Groups[1].Value;

        // Health (either regular or bonus)
        var healthMatch = Regex.Match(input, @"(선박 생명력|생명력): \|nc;(\d+)\|r( 증가)?");
        if (healthMatch.Success)
        {
            if (healthMatch.Groups[3].Success)
                data.HealthBonus = int.Parse(healthMatch.Groups[2].Value);
            else
                data.Health = int.Parse(healthMatch.Groups[2].Value);
        }

        // Weight
        var weightMatch = Regex.Match(input, @"무게: \|nc;(\d+)");
        if (weightMatch.Success)
            data.Weight = int.Parse(weightMatch.Groups[1].Value);

        // Description
        var descMatch = Regex.Match(input, @"- \|ng;(.*?)\|r");
        if (descMatch.Success)
            data.Description = descMatch.Groups[1].Value;

        // Tier and IDs from the end
        var endParts = input.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (endParts.Length >= 4)
        {
            data.Tier = int.Parse(endParts[endParts.Length - 4]);
            data.VisualId = int.Parse(endParts[endParts.Length - 2]);
            data.DbId = int.Parse(endParts[endParts.Length - 1]);
        }
    }

    private static void ParseWeaponAttributes(string input, ShipComponentData data)
    {
        // Attack range
        var rangeMatch = Regex.Match(input, @"사거리: 최소 \|nc;(\d+)m\|r ~ 최대 \|nc;(\d+)m\|r");
        if (rangeMatch.Success)
        {
            data.MinRange = float.Parse(rangeMatch.Groups[1].Value);
            data.MaxRange = float.Parse(rangeMatch.Groups[2].Value);
        }
        else
        {
            // Some weapons have single range value
            var singleRange = Regex.Match(input, @"사거리: \|nc;(\d+)m\|r");
            if (singleRange.Success)
                data.MaxRange = data.MinRange = float.Parse(singleRange.Groups[1].Value);
        }

        // Explosion radius
        var explosionMatch = Regex.Match(input, @"(폭발 반경|범위 폭): \|nc;(\d+)m\|r");
        if (explosionMatch.Success)
            data.ExplosionRadius = float.Parse(explosionMatch.Groups[2].Value);

        // Damage values
        var damageMatch = Regex.Match(input, @"(일반|공성) 공격력: \|nc;(\d+)~(\d+)\|r");
        if (damageMatch.Success)
        {
            data.DamageType = damageMatch.Groups[1].Value;
            if (damageMatch.Groups[1].Value == "일반")
            {
                data.MinDamage = int.Parse(damageMatch.Groups[2].Value);
                data.MaxDamage = int.Parse(damageMatch.Groups[3].Value);
            }
            else
            {
                data.SiegeMinDamage = int.Parse(damageMatch.Groups[2].Value);
                data.SiegeMaxDamage = int.Parse(damageMatch.Groups[3].Value);
            }
        }
        else
        {
            // Some have single damage value
            var singleDamage = Regex.Match(input, @"(일반|공성) 공격력: \|nc;(\d+)\|r");
            if (singleDamage.Success)
            {
                int dmg = int.Parse(singleDamage.Groups[2].Value);
                if (singleDamage.Groups[1].Value == "일반")
                    data.MinDamage = data.MaxDamage = dmg;
                else
                    data.SiegeMinDamage = data.SiegeMaxDamage = dmg;
            }
        }

        // Projectile speed
        var speedMatch = Regex.Match(input, @"탄속: \|nc;(\d+) m/s\|r");
        if (speedMatch.Success)
            data.ProjectileSpeed = float.Parse(speedMatch.Groups[1].Value);

        // Cooldown
        var cooldownMatch = Regex.Match(input, @"(재사용시간|재사용 시간): \|nc;([\d.]+)초?\|r");
        if (cooldownMatch.Success)
            data.Cooldown = float.Parse(cooldownMatch.Groups[2].Value);

        // Two-slot items
        data.IsTwoSlot = input.Contains("선수상 장착 칸을 |nc;2|r칸 사용합니다");
    }

    private static void ParseMovementAttributes(string input, ShipComponentData data)
    {
        // Speed boost for propulsion systems
        var boostMatch = Regex.Match(input, @"이동 속도 \|nc;(\d+)%\|r 증가");
        if (boostMatch.Success)
            data.SpeedBoost = float.Parse(boostMatch.Groups[1].Value) / 100f;

        // Damage reduction
        var reductionMatch = Regex.Match(input, @"공성 피해 감소: \|nc;(\d+)%\|r");
        if (reductionMatch.Success)
            data.DamageReduction = float.Parse(reductionMatch.Groups[1].Value) / 100f;

        // Sail damage multiplier
        var sailDmgMatch = Regex.Match(input, @"돛에 \|nc;(\d+)\|r배 추가 공성 피해");
        if (sailDmgMatch.Success)
            data.SiegeDamageMultiplier = float.Parse(sailDmgMatch.Groups[1].Value);
    }

    private static void ParseSpecialEffects(string input, ShipComponentData data)
    {
        // Added skills
        var skillMatches = Regex.Matches(input, @"\|nc;\[(.*?)\] 기술 추가\|r");
        foreach (Match match in skillMatches)
            data.AddedSkills.Add(match.Groups[1].Value);

        // All effects
        var effectMatches = Regex.Matches(input, @"- \|ni;(.*?)\|r");
        foreach (Match match in effectMatches)
        {
            var effect = new ComponentEffect { Description = match.Groups[1].Value };

            // Effect value and unit
            var valueMatch = Regex.Match(effect.Description, @"\|nc;([\d.]+)(%?)\|r");
            if (valueMatch.Success)
            {
                effect.Value = float.Parse(valueMatch.Groups[1].Value);
                effect.Unit = valueMatch.Groups[2].Value;
            }

            // Cooldown in minutes
            var cdMinMatch = Regex.Match(effect.Description, @"재사용 시간 \|nc;(\d+)\|r분");
            if (cdMinMatch.Success)
                effect.EffectCooldown = float.Parse(cdMinMatch.Groups[1].Value) * 60;

            // Cooldown in seconds
            var cdSecMatch = Regex.Match(effect.Description, @"재사용 시간 \|nc;(\d+)\|r초");
            if (cdSecMatch.Success)
                effect.EffectCooldown = float.Parse(cdSecMatch.Groups[1].Value);

            // Charges/ammo count
            var chargesMatch = Regex.Match(effect.Description, @"장전 횟수: \|nc;(\d+)\|r");
            if (chargesMatch.Success)
                effect.Charges = int.Parse(chargesMatch.Groups[1].Value);

            // Special effect detection
            if (effect.Description.Contains("레비아탄에게 느려짐 효과"))
                data.LeviathanSlowEffect = effect.Value;

            if (effect.Description.Contains("넘어짐 효과 발생"))
                data.KnockdownChance = effect.Value;

            data.Effects.Add(effect);
        }
    }

    public static void PrintComponentData(ShipComponentData data)
    {
        Console.WriteLine($"=== Компонент #{data.Id} (Tier {data.Tier}) ===");

        Console.WriteLine($"Тип: {data.InstallablePart}");
        Console.WriteLine($"Ремонт: {data.RepairCost} {data.RepairMaterial}");
        Console.WriteLine($"Совместимость: {string.Join(", ", data.CompatibleShips)}");

        if (data.Health > 0) Console.WriteLine($"Прочность: {data.Health}");
        if (data.HealthBonus > 0) Console.WriteLine($"Бонус прочности: +{data.HealthBonus}");
        Console.WriteLine($"Вес: {data.Weight}кг");

        if (!string.IsNullOrEmpty(data.Description))
            Console.WriteLine($"Описание: {data.Description}");

        // Weapon stats
        if (data.MaxRange > 0)
        {
            Console.WriteLine("\nБоевые характеристики:");
            Console.WriteLine($"Дальность: {data.MinRange}-{data.MaxRange}m");
            if (data.ExplosionRadius > 0)
                Console.WriteLine($"Радиус взрыва: {data.ExplosionRadius}m");

            if (data.MinDamage > 0)
                Console.WriteLine($"Урон: {data.MinDamage}-{data.MaxDamage}");

            if (data.SiegeMinDamage > 0)
                Console.WriteLine($"Осадный урон: {data.SiegeMinDamage}-{data.SiegeMaxDamage}");

            if (data.ProjectileSpeed > 0)
                Console.WriteLine($"Скорость снаряда: {data.ProjectileSpeed} m/s");

            if (data.Cooldown > 0)
                Console.WriteLine($"Перезарядка: {data.Cooldown}сек");
        }

        // Movement stats
        if (data.SpeedBoost > 0)
            Console.WriteLine($"Ускорение: +{data.SpeedBoost * 100}%");

        if (data.DamageReduction > 0)
            Console.WriteLine($"Сопротивление урону: {data.DamageReduction * 100}%");

        // Special effects
        if (data.AddedSkills.Count > 0)
        {
            Console.WriteLine("\nОсобые навыки:");
            foreach (var skill in data.AddedSkills)
                Console.WriteLine($"- {skill}");
        }

        if (data.Effects.Count > 0)
        {
            Console.WriteLine("\nЭффекты:");
            foreach (var effect in data.Effects)
            {
                Console.WriteLine($"- {effect.Description}");
                if (effect.Value > 0)
                    Console.WriteLine($"  Значение: {effect.Value}{effect.Unit}");
                if (effect.EffectCooldown > 0)
                    Console.WriteLine($"  Перезарядка: {effect.EffectCooldown}сек");
                if (effect.Charges > 0)
                    Console.WriteLine($"  Зарядов: {effect.Charges}");
            }
        }

        Console.WriteLine($"\nID: DB={data.DbId}, Visual={data.VisualId}");
    }

    public static void LogComponentData(ShipComponentData data)
    {
        // Создаем форматированное сообщение
        var logMessage = new System.Text.StringBuilder();

        // Заголовок компонента
        logMessage.AppendLine($"=== Компонент #{data.Id} (Tier {data.Tier}) ===");
        logMessage.AppendLine($"Тип: {data.InstallablePart}");
        logMessage.AppendLine($"Ремонт: {data.RepairCost} {data.RepairMaterial}");
        logMessage.AppendLine($"Совместимость: {string.Join(", ", data.CompatibleShips)}");

        // Основные характеристики
        if (data.Health > 0) logMessage.AppendLine($"Прочность: {data.Health}");
        if (data.HealthBonus > 0) logMessage.AppendLine($"Бонус прочности: +{data.HealthBonus}");
        logMessage.AppendLine($"Вес: {data.Weight}кг");

        if (!string.IsNullOrEmpty(data.Description))
            logMessage.AppendLine($"Описание: {data.Description}");

        // Боевые характеристики (логируем как Info)
        if (data.MaxRange > 0)
        {
            logMessage.AppendLine("\nБоевые характеристики:");
            logMessage.AppendLine($"Дальность: {data.MinRange}-{data.MaxRange}m");
            if (data.ExplosionRadius > 0)
                logMessage.AppendLine($"Радиус взрыва: {data.ExplosionRadius}m");

            if (data.MinDamage > 0)
                logMessage.AppendLine($"Урон: {data.MinDamage}-{data.MaxDamage}");

            if (data.SiegeMinDamage > 0)
                logMessage.AppendLine($"Осадный урон: {data.SiegeMinDamage}-{data.SiegeMaxDamage}");

            if (data.ProjectileSpeed > 0)
                logMessage.AppendLine($"Скорость снаряда: {data.ProjectileSpeed} m/s");

            if (data.Cooldown > 0)
                logMessage.AppendLine($"Перезарядка: {data.Cooldown}сек");
        }

        // Характеристики движения (Debug уровень)
        var movementInfo = new System.Text.StringBuilder();
        if (data.SpeedBoost > 0)
            movementInfo.AppendLine($"Ускорение: +{data.SpeedBoost * 100}%");

        if (data.DamageReduction > 0)
            movementInfo.AppendLine($"Сопротивление урону: {data.DamageReduction * 100}%");

        if (movementInfo.Length > 0)
        {
            logMessage.AppendLine("\nХарактеристики движения:");
            logMessage.Append(movementInfo);
        }

        // Особые навыки (Info уровень)
        if (data.AddedSkills.Count > 0)
        {
            logMessage.AppendLine("\nОсобые навыки:");
            foreach (var skill in data.AddedSkills)
                logMessage.AppendLine($"- {skill}");
        }

        // Эффекты (Debug уровень)
        if (data.Effects.Count > 0)
        {
            var effectsInfo = new System.Text.StringBuilder("\nЭффекты:");
            foreach (var effect in data.Effects)
            {
                effectsInfo.AppendLine($"- {effect.Description}");
                if (effect.Value > 0)
                    effectsInfo.AppendLine($"  Значение: {effect.Value}{effect.Unit}");
                if (effect.EffectCooldown > 0)
                    effectsInfo.AppendLine($"  Перезарядка: {effect.EffectCooldown}сек");
                if (effect.Charges > 0)
                    effectsInfo.AppendLine($"  Зарядов: {effect.Charges}");
            }
            Logger.Debug(effectsInfo.ToString());
        }

        // ID компонента (Trace уровень)
        Logger.Trace($"ID компонента: DB={data.DbId}, Visual={data.VisualId}");

        // Основную информацию логируем как Info
        Logger.Info(logMessage.ToString());
    }
}
