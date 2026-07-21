using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.StaticValues;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCDoodadPhaseChangedPacket : GamePacket
{
    private Doodad _doodad;

    public SCDoodadPhaseChangedPacket(Doodad doodad) : base(SCOffsets.SCDoodadPhaseChangedPacket, 5)
    {
        _doodad = doodad;
        //Logger.Debug("[Doodad] [0] SCDoodadPhaseChangedPacket: TemplateId {0}, ObjId {1},  CurrentPhaseId {2}, TimeLeft {3}", _doodad.TemplateId, _doodad.ObjId, _doodad.FuncGroupId, _doodad.TimeLeft);
    }

    public override PacketStream Write(PacketStream stream)
    {
        //Logger.Debug("[Doodad] [2] SCDoodadPhaseChangedPacket: TemplateId {0}, ObjId {1},  CurrentPhaseId {2}, TimeLeft {3}", _doodad.TemplateId, _doodad.ObjId, _doodad.FuncGroupId, _doodad.TimeLeft);

        stream.WriteBc(_doodad.ObjId);
        stream.Write(_doodad.FuncGroupId);
        stream.Write(_doodad.TimeLeft);    // growing
        stream.Write(_doodad.PuzzleGroup); // puzzleGroup
        stream.Write(_doodad.ItemTemplateId); // type(id) for backpack e.g. Id=27606 Sturgeon Pack

        // Retail 3.5.0.3 always writes the 12-byte tail (freshnessTime u64 + crafter u32),
        // verified against packet capture. Missing it truncates the packet and the client drops phase updates.
        var itemCheck = TagsGameData.Instance.GetIdsByTagId(TagsGameData.TagType.Items, (uint)TagsEnum.TradePackStorageChest);
        if (itemCheck.Contains(_doodad.ItemTemplateId))
        {
            stream.Write(_doodad.FreshnessTime); // freshnessTime
            stream.Write(_doodad.OwnerId);       // crafter type
        }
        else
        {
            stream.Write(_doodad.FreshnessTime); // freshnessTime
            stream.Write(0u);                    // crafter type
        }

        return stream;
    }
}
