using AAEmu.Game.Models.Game.NPChar;

namespace AAEmu.Game.Models.Game.Trading
{
    public class SpecialtyNpc
    {
        public uint Id { get; set; }
        //public string Name { get; set; } // there is no such field in the database for version 3030
        public uint NpcId { get; set;}
        public uint SpecialtyBundleId { get; set; }
    }
}
