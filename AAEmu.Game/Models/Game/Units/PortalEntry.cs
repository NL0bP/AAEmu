using System;
using System.Collections.Generic;

namespace AAEmu.Game.Models.Game.Units
{
    public class PortalEntry
    {
        public uint EnterPortalNpcId { get; set; }
        public uint ExitPortalNpcId { get; set; }
        public uint PortalSkillId { get; set; }

        public PortalEntry(uint enterId, uint exitId, uint skillId)
        {
            EnterPortalNpcId = enterId;
            ExitPortalNpcId = exitId;
            PortalSkillId = skillId;
        }
    }

    public static class PortalSelector
    {
        private static readonly List<PortalEntry> Portals =
        [
            new PortalEntry(3891, 6629, 11216),   // Teleport
            new PortalEntry(22023, 22024, 50817), // Cow-Splash Teleport
            new PortalEntry(22136, 22137, 51310), // Winter Maiden Teleport
            new PortalEntry(22163, 22164, 51506), // Blue Dragon Energy Teleport
            new PortalEntry(22184, 22185, 51672)  // Infinite Abyss Teleport
        ];

        private static readonly Random Random = new();

        /// <summary>
        /// Selects a random portal entry from the predefined list.
        /// </summary>
        public static PortalEntry GetRandomPortal()
        {
            var index = Random.Next(Portals.Count);
            return Portals[index];
        }
    }
}
