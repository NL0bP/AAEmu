/*
 * by uranusq https://github.com/NL0bP/aaa_emulator
 * by Nikes
 * by NLObP
 * Structure aligned with Nikes-cn-10.0.2.13 ConnectionKeychain (CsSeq / CsMSeq / CsNum),
 * plus AAC 3.5 tuneable cry constants (Themida hides them in crynetwork).
 */
using System.Security.Cryptography;

namespace AAEmu.Commons.Cryptography;

/// <summary>
/// Per-connection key material for the encrypted game channel (AAC 3.5.x).
/// Server generates RSA-1024, sends pub key in X2EnterWorldResponse; client returns
/// AES + XOR keys (RSA-encrypted) in CSAesXorKey.
/// </summary>
public class ConnectionKeychain
{
    public uint ConnectionId { get; set; }
    public RSACryptoServiceProvider RsaKeyPair { get; set; }

    /// <summary>True once CSAesXorKey has been received and decrypted.</summary>
    public bool RecievedKeys { get; set; }

    public byte[] AesKey { get; set; }
    public byte[] XorRaw { get; set; }
    public byte[] IV { get; set; }

    /// <summary>Raw XOR "head" dword from CSAesXorKey.</summary>
    public uint Head { get; set; }

    /// <summary>Derived XOR keys (head transform). Decode uses XorKey1 squared, like 10.0.</summary>
    public uint XorKey1 { get; set; }
    public uint XorKey2 { get; set; }

    /// <summary>Legacy alias: squared working key (XorKey1 * XorKey1).</summary>
    public uint XorKey { get; set; }

    /// <summary>Cry formula: mul ^ (MakeSeq + C1) ^ C2 — tuneable (not visible in packed DLL).</summary>
    public uint XorKeyConstant1 { get; set; }
    public uint XorKeyConstant2 { get; set; }

    public byte SCMessageCount { get; set; }
    public byte CSMessageCount { get; set; }

    // C->S DecodeXor running state (10.0 layout). Reset when CsNum == 0.
    public byte CsSeq { get; set; }   // stride selector, advances per packet
    public uint CsMSeq { get; set; }  // MakeSeq accumulator
    public uint CsNum { get; set; }   // encrypted packet counter

    public ConnectionKeychain(uint connId, RSACryptoServiceProvider kp)
    {
        ConnectionId = connId;
        RsaKeyPair = kp;
        AesKey = new byte[16];
        XorRaw = new byte[16];
        IV = new byte[16];
    }
}
