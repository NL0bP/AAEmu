using System;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G;

/// <summary>
/// Client sync packet sent after a buff temporarily replaces one skill in the action bar with another.
/// We currently only parse and acknowledge it so it no longer falls into Unknown packet handling.
/// </summary>
public class CSSkillReplaceSyncPacket : GamePacket
{
    public uint OriginalSkillId { get; private set; }
    public byte ActionSlot { get; private set; }
    public ushort BuffId { get; private set; }
    public ushort ReplacedSkillId { get; private set; }
    public byte[] RawPayload { get; private set; } = Array.Empty<byte>();

    public CSSkillReplaceSyncPacket() : base(CSOffsets.CSSkillReplaceSyncPacket, 5)
    {
    }

    public override void Read(PacketStream stream)
    {
        RawPayload = stream.ReadBytes(stream.Count - stream.Pos);

        if (RawPayload.Length >= 4)
            OriginalSkillId = BitConverter.ToUInt32(RawPayload, 0);

        // Observed client payload layout for temporary skill replacement:
        // [0..3]   original skill id (u32)
        // [5]      action slot
        // [10..11] source buff id (u16)
        // [14..15] replacement skill id (u16)
        if (RawPayload.Length >= 6)
            ActionSlot = RawPayload[5];
        if (RawPayload.Length >= 12)
            BuffId = BitConverter.ToUInt16(RawPayload, 10);
        if (RawPayload.Length >= 16)
            ReplacedSkillId = BitConverter.ToUInt16(RawPayload, 14);

        Logger.Debug(
            "CSSkillReplaceSyncPacket: char={0}, oldSkill={1}, newSkill={2}, buff={3}, slot={4}, rawLen={5}",
            Connection.ActiveChar?.Name ?? "<none>",
            OriginalSkillId,
            ReplacedSkillId,
            BuffId,
            ActionSlot,
            RawPayload.Length);

        Connection.ActiveChar?.Skills.RegisterTemporarySkillReplacement(OriginalSkillId, ReplacedSkillId, BuffId, ActionSlot);
    }
}
