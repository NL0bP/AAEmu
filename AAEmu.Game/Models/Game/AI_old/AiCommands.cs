using AAEmu.Game.Models.Game.AI_old.Static;

namespace AAEmu.Game.Models.Game.AI_old
{
    public class AiCommands
    {
        public uint Id { get; set; }
        public uint CmdSetId { get; set; }
        public AiCommandCategory CmdId { get; set; }
        public uint Param1 { get; set; }
        public string Param2 { get; set; }
    }
}
