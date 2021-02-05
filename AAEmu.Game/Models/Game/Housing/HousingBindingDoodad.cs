using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Models.Game.Housing
{
    public class HousingBindingDoodad
    {
        public uint AttachPointId { get; set; }
        public uint DoodadId { get; set; }
        public bool ForceDbSave { get; set; }
        public uint HoudingId { get; set; }
        public Point Position { get; set; }
    }
}
