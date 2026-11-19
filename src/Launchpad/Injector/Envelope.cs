using System.Security.Cryptography;
using System.Text;

namespace Launchpad.Injector;

public static class Envelope
{
    public const string Info = "monkeforge-inject-v1";
    private static readonly byte[] Magic = "MFI1"u8.ToArray();
    private const int SaltBytes = 32;
    private const int IvBytes = 12;
    private const int TagBytes = 16;

    public static string Aad(string guid, string version, string fileId, string deviceId, string nonce) =>
        string.Join('|', Info, guid, version, fileId, deviceId, nonce);

    public static string SignedMessage(string nonce, string deviceId, string guid, string channel, string loader, string clientKey) =>
        string.Join('\n', Info, nonce, deviceId, guid, channel, loader, clientKey);

    public static byte[] Open(ReadOnlySpan<byte> blob, ECDiffieHellman ephemeral, string aad)
    {
        if (blob.Length < Magic.Length + 2 || !blob[..Magic.Length].SequenceEqual(Magic))
            throw new CryptographicException("The payload isn't a MonkeForge injector envelope.");

        var keyLength = (blob[4] << 8) | blob[5];
        var offset = 6;

        if (keyLength is < 64 or > 256 || blob.Length < offset + keyLength + SaltBytes + IvBytes + TagBytes)
            throw new CryptographicException("The payload envelope is truncated.");

        ReadOnlySpan<byte> serverKey = blob.Slice(offset, keyLength);
        offset += keyLength;
        ReadOnlySpan<byte> salt = blob.Slice(offset, SaltBytes);
        offset += SaltBytes;
        ReadOnlySpan<byte> iv = blob.Slice(offset, IvBytes);
        offset += IvBytes;
        ReadOnlySpan<byte> tag = blob.Slice(offset, TagBytes);
        offset += TagBytes;
        ReadOnlySpan<byte> ciphertext = blob[offset..];

        using ECDiffieHellman server = ECDiffieHellman.Create();
        server.ImportSubjectPublicKeyInfo(serverKey, out var read);

        if (read != keyLength || server.KeySize != 256 || server.ExportParameters(false).Curve.Oid?.Value != "1.2.840.10045.3.1.7")
            throw new CryptographicException("The payload was encrypted with an unexpected key type.");

        var shared = ephemeral.DeriveRawSecretAgreement(server.PublicKey);
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, salt.ToArray(), Encoding.ASCII.GetBytes(Info));
        var plain = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(iv, ciphertext, tag, plain, Encoding.UTF8.GetBytes(aad));
            return plain;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(plain);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(shared);
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
