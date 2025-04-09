using System;
using System.Collections.Concurrent;

using AAEmu.Commons.Utils;

namespace AAEmu.Game.Core.Managers
{
    public class CooldownManager : Singleton<CooldownManager>
    {
        // Ключ – Tuple (npcId, skillId), значение – время окончания cooldown.
        private readonly ConcurrentDictionary<(uint npcId, uint skillId), DateTime> _cooldowns = new();

        /// <summary>
        /// Sets or updates the cooldown for a skill of a specific NPC.
        /// </summary>
        public void SetCooldown(uint npcId, uint skillId, TimeSpan cooldown)
        {
            var expireTime = DateTime.UtcNow.Add(cooldown);
            _cooldowns[(npcId, skillId)] = expireTime;
        }

        /// <summary>
        /// Checks if the skill is currently in cooldown for the given NPC.
        /// </summary>
        public bool IsInCooldown(uint npcId, uint skillId)
        {
            if (_cooldowns.TryGetValue((npcId, skillId), out var expireTime))
            {
                // Если сейчас меньше expireTime, значит скилл на cooldown.
                return DateTime.UtcNow < expireTime;
            }
            return false;
        }

        /// <summary>
        /// Removes expired cooldown entries.
        /// Можно вызывать периодически например, каждые несколько секунд.
        /// </summary>
        public void CleanupExpiredCooldowns()
        {
            var now = DateTime.UtcNow;
            foreach (var entry in _cooldowns)
            {
                if (now >= entry.Value)
                    _cooldowns.TryRemove(entry.Key, out _);
            }
        }
    }
}
