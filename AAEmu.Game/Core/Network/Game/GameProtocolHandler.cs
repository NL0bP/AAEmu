using System.Collections.Concurrent;
using System.Text;

using AAEmu.Commons.Cryptography;
using AAEmu.Commons.Exceptions;
using AAEmu.Commons.Network;
using AAEmu.Commons.Network.Core;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Connections;

using NLog;

namespace AAEmu.Game.Core.Network.Game;

public class GameProtocolHandler : BaseProtocolHandler
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// List of packet handlers (level, id, class type)
    /// </summary>
    private readonly ConcurrentDictionary<byte, ConcurrentDictionary<uint, Type>> _packets;

    public GameProtocolHandler()
    {
        _packets = new ConcurrentDictionary<byte, ConcurrentDictionary<uint, Type>>();
        _packets.TryAdd(1, new ConcurrentDictionary<uint, Type>());
        _packets.TryAdd(2, new ConcurrentDictionary<uint, Type>());
        _packets.TryAdd(3, new ConcurrentDictionary<uint, Type>());
        _packets.TryAdd(4, new ConcurrentDictionary<uint, Type>());
        _packets.TryAdd(5, new ConcurrentDictionary<uint, Type>());
        _packets.TryAdd(6, new ConcurrentDictionary<uint, Type>());
    }

    public override void OnConnect(ISession session)
    {
        Logger.Info($"Connect from {session.Ip} established, session id: {session.SessionId}");
        try
        {
            var con = new GameConnection(session);
            con.OnConnect();
            GameConnectionTable.Instance.AddConnection(con);
        }
        catch (Exception e)
        {
            session.Close();
            Logger.Error(e);
        }
    }

    public override void OnDisconnect(ISession session)
    {
        try
        {
            var con = GameConnectionTable.Instance.GetConnection(session.SessionId);
            if (con != null)
            {
                if (con.ActiveChar != null)
                {
                    Managers.ChatManager.Instance.LeaveAllChannels(con.ActiveChar);
                }
                con.OnDisconnect();
                StreamManager.Instance.RemoveToken(con.Id);
                GameConnectionTable.Instance.RemoveConnection(session.SessionId);
            }
            else
            {
                Logger.Error($"{nameof(OnDisconnect)}: connection for session id {session.SessionId} is null");
            }
        }
        catch (Exception e)
        {
            session.Close();
            Logger.Error(e);
        }

        Logger.Info($"Client from {session.Ip} disconnected");
    }

    public override void OnReceive(ISession session, byte[] buf, int offset, int bytes)
    {
        try
        {
            var connection = GameConnectionTable.Instance.GetConnection(session.SessionId);
            if (connection == null)
            {
                Logger.Error($"{nameof(OnReceive)}: connection for session id {session.SessionId} is null");
                return;
            }

            OnReceive(connection, buf, offset, bytes);
        }
        catch (Exception e)
        {
            session.Close();
            Logger.Error(e);
        }
    }

    public void OnReceive(GameConnection connection, byte[] buf, int offset, int bytes)
    {
        try
        {
            var stream = new PacketStream();
            if (connection.LastPacket != null)
            {
                stream.Insert(0, connection.LastPacket);
                connection.LastPacket = null;
            }
            stream.Insert(stream.Count, buf, offset, bytes);
            while (stream is { Count: > 0 })
            {
                ushort len;
                try
                {
                    len = stream.ReadUInt16();
                }
                catch (MarshalException)
                {
                    stream.Rollback();
                    connection.LastPacket = stream;
                    stream = null;
                    continue;
                }

                var packetLen = len + stream.Pos;
                if (packetLen <= stream.Count)
                {
                    stream.Rollback();
                    var stream2 = new PacketStream();
                    stream2.Replace(stream, 0, packetLen);
                    if (stream.Count > packetLen)
                    {
                        var stream3 = new PacketStream();
                        stream3.Replace(stream, packetLen, stream.Count - packetLen);
                        stream = stream3;
                    }
                    else
                        stream = null;

                    stream2.ReadUInt16(); // len
                    stream2.ReadByte();   // sig (0xDD) / unk
                    var level = stream2.ReadByte();

                    PacketStream bodyStream;
                    byte lookupLevel = level;
                    ushort type;
                    byte[] encryptedInput = null;

                    if (level == 5)
                    {
                        // Encrypted C->S: [len][sig][level=5][hash][AES cipher]
                        // Decrypt → [crc8][count][type u16][body] (same shape as S->C body).
                        var input = new byte[packetLen - 2];
                        Array.Copy(stream2.Buffer, 2, input, 0, packetLen - 2);
                        encryptedInput = input;

                        var decrypted = EncryptionManager.Instance.TryDecodeLevel5(
                            input,
                            connection.Id,
                            connection.AccountId,
                            IsAcceptableLevel5Plaintext,
                            out var plain);

                        if (!decrypted || plain == null || plain.Length < 4)
                        {
                            Logger.Warn(
                                "C2S level-5 decrypt failed (len={0}, plain={1}) from {2}",
                                packetLen, plain?.Length ?? -1, connection.Ip);
                            continue;
                        }

                        bodyStream = new PacketStream();
                        bodyStream.Insert(0, plain, 0, plain.Length);
                        bodyStream.ReadByte();          // crc8
                        bodyStream.ReadByte();          // count
                        type = bodyStream.ReadUInt16(); // opcode

                        // AAC registers most CS handlers on level 5; fall back to level 1 (pre-keys packets).
                        lookupLevel = 5;
                        if (!_packets[5].ContainsKey(type) && _packets[1].ContainsKey(type))
                            lookupLevel = 1;
                    }
                    else
                    {
                        if (level == 1)
                        {
                            _ = stream2.ReadByte(); // hash/crc
                            _ = stream2.ReadByte(); // counter
                        }
                        type = stream2.ReadUInt16();
                        bodyStream = stream2;
                    }

                    Type classType = null;
                    if (_packets.TryGetValue(lookupLevel, out var levelMap))
                        levelMap.TryGetValue(type, out classType);

                    if (classType == null)
                    {
                        HandleUnknownPacket(connection, type, level, bodyStream);
                    }
                    else
                    {
                        var packet = (GamePacket)Activator.CreateInstance(classType);
                        packet!.Level = level;
                        packet.Connection = connection;
                        if (Logger.IsDebugEnabled && type == Packets.C2G.CSOffsets.CSCreateCharacterPacket)
                        {
                            var raw = new PacketStream().Replace(bodyStream, bodyStream.Pos, bodyStream.Count - bodyStream.Pos).GetBytes();
                            Logger.Debug($"Raw CSCreateCharacterPacket plaintext ({raw.Length} bytes): {Convert.ToHexString(raw)}");
                            if (encryptedInput != null)
                                Logger.Debug($"Raw CSCreateCharacterPacket ciphertext ({encryptedInput.Length} bytes): {Convert.ToHexString(encryptedInput)}");
                        }
                        packet.Decode(bodyStream);
                    }
                }
                else
                {
                    stream.Rollback();
                    connection.LastPacket = stream;
                    stream = null;
                }
            }
        }
        catch (Exception e)
        {
            connection?.Shutdown();
            Logger.Error(e);
        }
    }

    public void RegisterPacket(uint type, byte level, Type classType)
    {
        _packets[level][type] = classType;
    }

    /// <summary>
    /// Fine-tune validator: plaintext = [crc8][count][type u16][body]. Accept known CS opcodes.
    /// </summary>
    private bool IsAcceptableLevel5Plaintext(byte[] plain)
    {
        if (plain == null || plain.Length < 4)
            return false;

        if (!EncryptionManager.Instance.IsAdjustCryptConstantEnable)
            return true;

        var type = BitConverter.ToUInt16(plain, 2);
        return _packets[5].ContainsKey(type) || _packets[1].ContainsKey(type);
    }

    private static void HandleUnknownPacket(GameConnection connection, uint type, byte level, PacketStream stream)
    {
        var dump = new StringBuilder();
        for (var i = stream.Pos; i < stream.Count; i++)
            dump.AppendFormat("{0:x2} ", stream.Buffer[i]);
        Logger.Error($"Unknown packet 0x{type:x2}({level}) from {connection.Ip}:\n{dump}");
    }
}
