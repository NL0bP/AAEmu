using System.Numerics;

using AAEmu.Commons.Network;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Static;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCEnvDamagePacket : GamePacket
{
    private EnvSource _source;
    private uint _target;
    private uint _amount;
    private uint _gimmickId;
    private Vector3 _position;
    private float _collisionImpact;
    private byte _p;

    public SCEnvDamagePacket(EnvSource source, uint target, uint amount, uint gimmickId = 0, Vector3 position = new Vector3(), float collisionImpact = 0, byte p = 0) : base(SCOffsets.SCEnvDamagePacket, 5)
    {
        _source = source;
        _target = target;
        _amount = amount;
        _gimmickId = gimmickId;
        _position = position;
        _collisionImpact = collisionImpact;
        _p = p;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write((byte)_source);
        stream.WriteBc(_target);
        stream.Write(_amount);
        if (_source == EnvSource.Gimmick)
            stream.Write(_gimmickId);
        if (_source == EnvSource.Collision)
        {
            stream.Write(Helpers.ConvertLongX(_position.X));
            stream.Write(Helpers.ConvertLongY(_position.Y));
            stream.Write(_position.Z);
            stream.Write(_collisionImpact);
            stream.Write(_p);
        }
        return stream;
    }
}
