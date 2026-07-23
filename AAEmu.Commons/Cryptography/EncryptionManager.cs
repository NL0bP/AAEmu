/*
 * by uranusq https://github.com/NL0bP/aaa_emulator
 * by Nikes
 * by NLObP
 *
 * AAC 3.5.x EncryptionManager — structure/state ported from Nikes-cn-10.0.2.13
 * (HashMap msgKey, SeqOffset, CsSeq/CsMSeq/CsNum, dual XorKey1/2, CSDecrypt flow),
 * with cry-constant fine-tune kept (C1/C2 hidden by Themida in crynetwork_dump).
 */
using System.Security.Cryptography;

using AAEmu.Commons.IO;
using AAEmu.Commons.Network;
using AAEmu.Commons.Utils;

using NLog;

namespace AAEmu.Commons.Cryptography;

/// <summary>
/// Game (world) channel encryption for AAC Classic 3.5.x.
///  * S->C: keyless length-seeded stream cipher (StoCEncrypt).
///  * C->S: DecodeXor + AES-128-CBC after CSAesXorKey exchange.
///  * Cry constants C1/C2 come from Configurations/xorKeyValue.txt and can be fine-tuned live.
/// </summary>
public class EncryptionManager : Singleton<EncryptionManager>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const int DwKeySize = 1024;

    // Keyed by ConnectionId (unique per session) — AAC multi-session safety.
    private Dictionary<uint, ConnectionKeychain> _connectionKeys = new();

    private static bool AdjustCryptConstantEnable;
    private static string XorKeyValueFilePath;

    /// <summary>How many C1 steps to try on one ciphertext before giving up.</summary>
    private const int FineTuneMaxAttemptsPerPacket = 64;
    private const uint XorKeyConstant1Min = 0x75A02400;
    private const uint XorKeyConstant1Max = 0x75A024FF;

    // AAC 3.5.3.0 defaults (commented known-good); overridden by xorKeyValue.txt when present.
    private const uint DefaultCryConst1 = 0x75A02453;
    private const uint DefaultCryConst2 = 0xB27645B4;

    // Head→XorKey derivation seeds (AAC 3.5.3.0). Same algebraic form as 10.0, different immediates.
    private const uint HeadXorA = 0x15351715u;
    private const uint HeadXorB = 0x070F1F23u;
    // Optional second key (10.0-style dual derive); kept for diagnostics / alternate try.
    private const uint HeadXorA2 = 0xFF217A82u;
    private const uint HeadXorB2 = 0x1F23070Fu;

    public bool IsAdjustCryptConstantEnable => AdjustCryptConstantEnable;

    public void Load()
    {
        _connectionKeys = new Dictionary<uint, ConnectionKeychain>();
        LoadCryptConfig();
        Logger.Info("Loaded Encryption Manager. AdjustCryptConstantEnable={0}", AdjustCryptConstantEnable);
    }

    private ConnectionKeychain GetOrCreateConnectionKeys(uint connectionId, ulong accountId)
    {
        if (_connectionKeys.TryGetValue(connectionId, out var keys))
            return keys;
        return GenerateRsaKeyPair(connectionId, accountId);
    }

    private ConnectionKeychain GenerateRsaKeyPair(uint connectionId, ulong accountId)
    {
        if (_connectionKeys.Remove(connectionId))
            Logger.Warn("Replacing RSA key pair for ConnectionId={0}, AccountId={1}", connectionId, accountId);

        var rsa = new RSACryptoServiceProvider(DwKeySize);
        var keys = new ConnectionKeychain(connectionId, rsa);
        _connectionKeys[connectionId] = keys;
        Logger.Debug("[{0}] Generated RSA key pair for connection {1}.", accountId, connectionId);
        return keys;
    }

    [Obsolete("Use RemoveConnectionKeysByConnectionId instead")]
    public void RemoveConnectionKeys(ulong accountId) { }

    public void RemoveConnectionKeysByConnectionId(uint connectionId)
    {
        if (_connectionKeys.Remove(connectionId))
            Logger.Trace("Removed connection keychain for ConnectionId={0}.", connectionId);
    }

    /// <summary>
    /// Writes RSA pub params for X2EnterWorldResponse (AAC layout: Modulus|125 zero|Exponent).
    /// </summary>
    public PacketStream WriteKeyParams(uint connectionId, ulong accountId, PacketStream stream)
    {
        var keychain = GetOrCreateConnectionKeys(connectionId, accountId);
        var p = keychain.RsaKeyPair.ExportParameters(false);
        stream.Write(p.Modulus);
        stream.Write(new byte[125]);
        stream.Write(p.Exponent);
        return stream;
    }

    public void StoreClientKeys(byte[] aesKeyEncrypted, byte[] xorKeyEncrypted, ulong accountId, uint connectionId)
    {
        if (!_connectionKeys.TryGetValue(connectionId, out var keys))
        {
            Logger.Warn("StoreClientKeys: no RSA key for ConnectionId={0}, AccountId={1}", connectionId, accountId);
            return;
        }

        try
        {
            var xorRaw = keys.RsaKeyPair.Decrypt(xorKeyEncrypted, false);
            var aesKey = keys.RsaKeyPair.Decrypt(aesKeyEncrypted, false);
            keys.XorRaw = xorRaw;
            keys.AesKey = aesKey;

            var head = BitConverter.ToUInt32(xorRaw, 0);
            keys.Head = head;

            // Binary key derivation (same shape as 10.0 sub_39573D20; AAC 3.5.3.0 immediates).
            // XorKey1 = head * (head ^ A) ^ B; working xor = XorKey1² (applied in CsDecodeXor).
            keys.XorKey1 = unchecked(head * (head ^ HeadXorA) ^ HeadXorB);
            keys.XorKey2 = unchecked(head * (head ^ HeadXorA2) ^ HeadXorB2);
            keys.XorKey = unchecked(keys.XorKey1 * keys.XorKey1);

            keys.RecievedKeys = true;
            keys.CsNum = 0;
            keys.CsSeq = 0;
            keys.CsMSeq = 0;
            keys.IV = new byte[16];

            EnsureCryConstants(keys);

            Logger.Warn(
                "StoreClientKeys ok acc={0} conn={1} head={2:X8} XorKey1={3:X8} XorKey={4:X8} C1={5:X8} C2={6:X8}",
                accountId, connectionId, head, keys.XorKey1, keys.XorKey,
                keys.XorKeyConstant1, keys.XorKeyConstant2);
        }
        catch (CryptographicException ex)
        {
            Logger.Error(ex, "StoreClientKeys RSA decrypt failed (acc={0}, conn={1})", accountId, connectionId);
            _connectionKeys.Remove(connectionId);
        }
    }

    public byte GetSCMessageCount(uint connectionId, ulong accountId) =>
        GetOrCreateConnectionKeys(connectionId, accountId).SCMessageCount;

    public void IncSCMsgCount(uint connectionId, ulong accountId) =>
        GetOrCreateConnectionKeys(connectionId, accountId).SCMessageCount++;

    public byte GetAndIncSCMessageCount(uint connectionId, ulong accountId)
    {
        var keys = GetOrCreateConnectionKeys(connectionId, accountId);
        return keys.SCMessageCount++;
    }

    /// <summary>Packet checksum: c = c * 0x13 + b.</summary>
    public byte Crc8(byte[] data)
    {
        uint checksum = 0;
        foreach (var b in data)
        {
            checksum *= 0x13;
            checksum += b;
        }
        return (byte)checksum;
    }

    #region S->C StoC stream cipher

    private static byte Inline(ref uint cry)
    {
        cry += 0x2FCBD5u;
        var n = (byte)((cry >> 16) & 0xF7);
        return n == 0 ? (byte)0xFE : n;
    }

    public byte[] StoCEncrypt(byte[] body)
    {
        var length = body.Length;
        var cry = (uint)(length ^ 0x1F2175A0);
        var array = new byte[length];
        var n = 4 * (length / 4);
        for (var i = n - 1; i >= 0; i--)
            array[i] = (byte)(body[i] ^ Inline(ref cry));
        for (var i = n; i < length; i++)
            array[i] = (byte)(body[i] ^ Inline(ref cry));
        return array;
    }

    #endregion

    #region C->S decryption (DecodeXor + AES-128-CBC)

    private static readonly int[] HashMap = BuildHashMap();

    private static int[] BuildHashMap()
    {
        var m = new int[256];
        for (var i = 0; i < 16; i++)
            m[0x30 + i] = i + 1; // 0x30..0x3F -> 1..16  (== hash - 47)
        return m;
    }

    private static byte Add(ref uint cry)
    {
        cry += 0x2FCBD5u;
        var n = (byte)((cry >> 16) & 0xF7);
        return n == 0 ? (byte)0xFE : n;
    }

    private static byte MakeSeq(ConnectionKeychain k)
    {
        k.CsMSeq += 0x2FA245u;
        var result = (byte)((k.CsMSeq >> 14) & 0x73);
        return result == 0 ? (byte)0xFE : result;
    }

    private static int SeqOffset(byte seq)
    {
        if (seq == 0) return 9;
        if (seq % 3 == 0) return 5;
        if (seq % 5 == 0) return 2;
        if (seq % 7 == 0) return 11;
        if (seq % 9 == 0) return 3;
        if (seq % 11 == 0) return 7;
        return 4;
    }

    /// <summary>Legacy entry — decrypt without opcode validation.</summary>
    public byte[] Decode(byte[] data, uint connectionId, ulong accountId)
    {
        TryDecodeLevel5(data, connectionId, accountId, _ => true, out var plaintext);
        return plaintext ?? Array.Empty<byte>();
    }

    /// <summary>
    /// Decrypts one level-5 C->S frame body (<paramref name="data"/> = frame minus length u16,
    /// i.e. [unk][level][hash][cipher...]). When AdjustCryptConstantEnable, retries the same
    /// ciphertext with C1++ (restoring IV/seq on each miss) until validator accepts or attempts run out.
    /// </summary>
    public bool TryDecodeLevel5(
        byte[] data,
        uint connectionId,
        ulong accountId,
        Func<byte[], bool> isAcceptablePlaintext,
        out byte[] plaintext)
    {
        plaintext = null;

        if (!_connectionKeys.TryGetValue(connectionId, out var keys) || !keys.RecievedKeys || data.Length < 4)
            return false;

        var cipherLen = data.Length - 3;
        if (cipherLen <= 0 || cipherLen % 16 != 0)
            return false;

        EnsureCryConstants(keys);
        ClampXorKeyConstant1(keys);

        if (keys.CsNum == 0)
        {
            keys.CsSeq = 0;
            keys.CsMSeq = 0;
            keys.IV = new byte[16];
        }

        var maxAttempts = AdjustCryptConstantEnable ? FineTuneMaxAttemptsPerPacket : 1;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var snap = SnapshotCryptState(keys);
            try
            {
                var (xored, _) = CsDecodeXor(data, keys, keys.XorKey1);
                var plain = CsDecodeAes(xored, keys);
                if (isAcceptablePlaintext(plain))
                {
                    keys.CsNum++;
                    keys.CSMessageCount++;
                    if (AdjustCryptConstantEnable && attempt > 0)
                    {
                        Logger.Warn(
                            "Crypt fine-tune OK after {0} tries: C1={1:X8} C2={2:X8} conn={3}",
                            attempt + 1, keys.XorKeyConstant1, keys.XorKeyConstant2, connectionId);
                        SaveXorKeyConstants(keys);
                    }

                    plaintext = plain;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Crypt fine-tune decrypt exception on attempt {0}", attempt + 1);
            }

            RestoreCryptState(keys, snap);

            if (!AdjustCryptConstantEnable)
                break;

            AdvanceXorKeyConstant1(keys);
            Logger.Warn(
                "Crypt fine-tune miss #{0}: next C1={1:X8} C2={2:X8} conn={3}",
                attempt + 1, keys.XorKeyConstant1, keys.XorKeyConstant2, connectionId);
            SaveXorKeyConstants(keys);
        }

        plaintext = Array.Empty<byte>();
        return false;
    }

    private (byte[] data, uint realLen) CsDecodeXor(byte[] bodyPacket, ConnectionKeychain k, uint xorKeyBase)
    {
        var mBody = new byte[bodyPacket.Length - 3];
        Array.Copy(bodyPacket, 3, mBody, 0, mBody.Length);

        var msgKey = (uint)(bodyPacket.Length / 16 - 1) << 4;
        msgKey += (uint)HashMap[bodyPacket[2]];

        var xorKey = unchecked(xorKeyBase * xorKeyBase);
        k.XorKey = xorKey;
        var mul = unchecked(msgKey * xorKey);
        // Known-good comment for 3.5.3.0: mul ^ (MakeSeq + 0x75A02453) ^ 0xB27645B4
        var cry = unchecked(mul ^ ((uint)MakeSeq(k) + k.XorKeyConstant1) ^ k.XorKeyConstant2);

        var offset = SeqOffset(k.CsSeq);
        var array = new byte[mBody.Length];
        var n = offset * (mBody.Length / offset);
        for (var i = n - 1; i >= 0; i--)
            array[i] = (byte)(mBody[i] ^ Add(ref cry));
        for (var i = n; i < mBody.Length; i++)
            array[i] = (byte)(mBody[i] ^ Add(ref cry));

        k.CsSeq = (byte)(k.CsSeq + MakeSeq(k) + 1);
        return (array, msgKey);
    }

    private static byte[] CsDecodeAes(byte[] cipher, ConnectionKeychain k)
    {
        var iv = (byte[])k.IV.Clone();
        var blocks = cipher.Length / 16;
        if (blocks >= 1)
        {
            k.IV = new byte[16];
            Array.Copy(cipher, (blocks - 1) * 16, k.IV, 0, 16);
        }

        using var aes = Aes.Create();
        aes.KeySize = 128;
        aes.BlockSize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = k.AesKey;
        aes.IV = iv;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(cipher, 0, cipher.Length);
    }

    #endregion

    #region Fine-tune / config

    private void EnsureCryConstants(ConnectionKeychain keys)
    {
        if (keys.XorKeyConstant1 != 0 && keys.XorKeyConstant2 != 0)
            return;
        LoadXorKeyConstant(keys);
        if (keys.XorKeyConstant1 == 0)
            keys.XorKeyConstant1 = DefaultCryConst1;
        if (keys.XorKeyConstant2 == 0)
            keys.XorKeyConstant2 = DefaultCryConst2;
    }

    private static void LoadXorKeyConstant(ConnectionKeychain keys)
    {
        XorKeyValueFilePath ??= Path.Combine(FileManager.AppPath, "Configurations", "xorKeyValue.txt");
        if (!File.Exists(XorKeyValueFilePath))
            return;

        using var reader = new StreamReader(XorKeyValueFilePath);
        while (!reader.EndOfStream)
        {
            var line1 = reader.ReadLine();
            var val1 = reader.ReadLine();
            if (line1 == "XorKeyConstant1:" && !string.IsNullOrWhiteSpace(val1))
                keys.XorKeyConstant1 = Convert.ToUInt32(val1, 16);

            var line2 = reader.ReadLine();
            var val2 = reader.ReadLine();
            if (line2 == "XorKeyConstant2:" && !string.IsNullOrWhiteSpace(val2))
                keys.XorKeyConstant2 = Convert.ToUInt32(val2, 16);

            _ = reader.ReadLine();
            var val3 = reader.ReadLine()?.ToLowerInvariant();
            AdjustCryptConstantEnable = val3 == "true";
        }
    }

    private static void LoadCryptConfig()
    {
        XorKeyValueFilePath = Path.Combine(FileManager.AppPath, "Configurations", "xorKeyValue.txt");
        if (!File.Exists(XorKeyValueFilePath))
        {
            AdjustCryptConstantEnable = false;
            return;
        }

        using var reader = new StreamReader(XorKeyValueFilePath);
        while (!reader.EndOfStream)
        {
            _ = reader.ReadLine();
            _ = reader.ReadLine();
            _ = reader.ReadLine();
            _ = reader.ReadLine();
            _ = reader.ReadLine();
            var val3 = reader.ReadLine()?.ToLowerInvariant();
            AdjustCryptConstantEnable = val3 == "true";
        }
    }

    private static CryptStateSnapshot SnapshotCryptState(ConnectionKeychain keys)
    {
        var iv = new byte[16];
        Buffer.BlockCopy(keys.IV, 0, iv, 0, 16);
        return new CryptStateSnapshot(iv, keys.CsSeq, keys.CsMSeq, keys.CsNum, keys.CSMessageCount);
    }

    private static void RestoreCryptState(ConnectionKeychain keys, CryptStateSnapshot snap)
    {
        Buffer.BlockCopy(snap.IV, 0, keys.IV, 0, 16);
        keys.CsSeq = snap.CsSeq;
        keys.CsMSeq = snap.CsMSeq;
        keys.CsNum = snap.CsNum;
        keys.CSMessageCount = snap.CSMessageCount;
    }

    private static void ClampXorKeyConstant1(ConnectionKeychain keys)
    {
        if (keys.XorKeyConstant1 < XorKeyConstant1Min || keys.XorKeyConstant1 > XorKeyConstant1Max)
            keys.XorKeyConstant1 = XorKeyConstant1Min;
    }

    private void AdvanceXorKeyConstant1(ConnectionKeychain keys)
    {
        keys.XorKeyConstant1++;
        if (keys.XorKeyConstant1 <= XorKeyConstant1Max)
            return;

        keys.XorKeyConstant1 = XorKeyConstant1Min;
        var tuneL = (byte)Random.Shared.Next(0x01, 0xFF);
        var tuneR = (byte)Random.Shared.Next(0x01, 0xFF);
        var result = keys.XorKeyConstant2 & 0x00FFFF00;
        result |= (uint)tuneL << 24;
        result |= tuneR;
        keys.XorKeyConstant2 = result;
        Logger.Warn("Crypt fine-tune: C1 wrapped, new C2={0:X8}", keys.XorKeyConstant2);
    }

    private static void SaveXorKeyConstants(ConnectionKeychain keys)
    {
        if (string.IsNullOrEmpty(XorKeyValueFilePath))
            return;
        using var writer = new StreamWriter(XorKeyValueFilePath, false);
        writer.WriteLine("XorKeyConstant1:");
        writer.WriteLine(keys.XorKeyConstant1.ToString("X8"));
        writer.WriteLine("XorKeyConstant2:");
        writer.WriteLine(keys.XorKeyConstant2.ToString("X8"));
        writer.WriteLine("AdjustCryptConstantEnable:");
        writer.WriteLine(AdjustCryptConstantEnable.ToString());
    }

    private readonly struct CryptStateSnapshot
    {
        public byte[] IV { get; }
        public byte CsSeq { get; }
        public uint CsMSeq { get; }
        public uint CsNum { get; }
        public byte CSMessageCount { get; }

        public CryptStateSnapshot(byte[] iv, byte csSeq, uint csMSeq, uint csNum, byte csMessageCount)
        {
            IV = iv;
            CsSeq = csSeq;
            CsMSeq = csMSeq;
            CsNum = csNum;
            CSMessageCount = csMessageCount;
        }
    }

    #endregion
}
