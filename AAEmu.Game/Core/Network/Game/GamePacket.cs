using AAEmu.Commons.Cryptography;
using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Connections;

namespace AAEmu.Game.Core.Network.Game;

public abstract class GamePacket(ushort typeId, byte level) : PacketBase<GameConnection>(typeId)
{
    public byte Level { get; set; } = level;

    /// <summary>
    /// This is called in Encode after Read() in the case of GamePackets
    /// The purpose is to separate packet data from packet behavior
    /// </summary>
    public virtual void Execute() { }

    public override PacketStream Encode()
    {
        var ps = new PacketStream();
        byte count = 0;
        try
        {
            // After X2EnterWorldResponse the client rejects plain level-1; auto-upgrade to StoC level 5.
            var level = Level;
            if (level == 1 && Connection is { EncryptionActive: true })
                level = 5;

            // Frame: [u16 len][sig=0xDD][level][body] — client consumes 0xDD then dispatches on level.
            var packet = new PacketStream()
                .Write((byte)0xdd)
                .Write(level);

            switch (level)
            {
                case 5:
                    // StoCEncrypt(crc8 | SCMessageCount | TypeId | body)
                    count = EncryptionManager.Instance.GetSCMessageCount(Connection.Id, Connection.AccountId);
                    var bodyCrc = new PacketStream()
                        .Write(count)
                        .Write(TypeId)
                        .Write(this);
                    EncryptionManager.Instance.IncSCMsgCount(Connection.Id, Connection.AccountId);
                    var crc8 = EncryptionManager.Instance.Crc8(bodyCrc);
                    var data = new PacketStream()
                        .Write(crc8)
                        .Write(bodyCrc, false);
                    packet.Write(EncryptionManager.Instance.StoCEncrypt(data), false);
                    break;
                case 1:
                    packet
                        .Write((byte)0) // hash
                        .Write((byte)0) // count
                        .Write(TypeId)
                        .Write(this);
                    break;
                default: // level 2 and others: plaintext
                    packet
                        .Write(TypeId)
                        .Write(this);
                    break;
            }

            ps.Write(packet);
        }
        catch (Exception ex)
        {
            Logger.Fatal(ex);
            throw;
        }

        var logString = $"GamePacket: S->C type [{Level}:{TypeId:X3}] C:[{count}:{EncryptionManager.Instance.GetSCMessageCount(Connection.Id, Connection.AccountId)}] {ToString()?.Substring(23)}{Verbose()}";
        switch (LogLevel)
        {
            case PacketLogLevel.Trace:
                Logger.Trace(logString);
                break;
            case PacketLogLevel.Debug:
                Logger.Debug(logString);
                break;
            case PacketLogLevel.Info:
                Logger.Info(logString);
                break;
            case PacketLogLevel.Warning:
                Logger.Warn(logString);
                break;
            case PacketLogLevel.Error:
                Logger.Error(logString);
                break;
            case PacketLogLevel.Fatal:
                Logger.Fatal(logString);
                break;
            case PacketLogLevel.Off:
            default:
                break;
        }

        return ps;
    }

    public override PacketBase<GameConnection> Decode(PacketStream ps)
    {
        try
        {
            Read(ps);

            var logString = $"GamePacket: C->S type {TypeId:X3} {ToString()?.Substring(23)}{Verbose()}";
            switch (LogLevel)
            {
                case PacketLogLevel.Trace:
                    Logger.Trace(logString);
                    break;
                case PacketLogLevel.Debug:
                    Logger.Debug(logString);
                    break;
                case PacketLogLevel.Info:
                    Logger.Info(logString);
                    break;
                case PacketLogLevel.Warning:
                    Logger.Warn(logString);
                    break;
                case PacketLogLevel.Error:
                    Logger.Error(logString);
                    break;
                case PacketLogLevel.Fatal:
                    Logger.Fatal(logString);
                    break;
                case PacketLogLevel.Off:
                default:
                    break;
            }

            Execute();
        }
        catch (Exception ex)
        {
            Logger.Error("GamePacket: C->S type {0:X3} {1}", TypeId, ToString()?.Substring(23));
            Logger.Fatal(ex);
            throw;
        }

        return this;
    }
}
