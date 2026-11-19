using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace Launchpad.Security;

public interface IDeviceKey : IDisposable
{
    string PublicKey { get; }

    byte[] Sign(byte[] data);

    string Storage { get; }
}

public sealed class DeviceKeyStore(ISecretStore secrets, bool useCng)
{
    public DeviceKeyStore(ISecretStore secrets) : this(secrets, OperatingSystem.IsWindows())
    {
    }

    public IDeviceKey? Open(string slot)
    {
        if (useCng && OperatingSystem.IsWindows())
            return CngDeviceKey.Open(slot);

        return SoftwareDeviceKey.Open(secrets, slot);
    }

    public IDeviceKey Create(string slot)
    {
        Delete(slot);

        if (useCng && OperatingSystem.IsWindows())
            return CngDeviceKey.Create(slot);

        return SoftwareDeviceKey.Create(secrets, slot);
    }

    public void Delete(string slot)
    {
        if (useCng && OperatingSystem.IsWindows())
            CngDeviceKey.Delete(slot);
        else
            secrets.Delete(SoftwareDeviceKey.SecretName(slot));
    }
}

[SupportedOSPlatform("windows")]
internal sealed class CngDeviceKey : IDeviceKey
{
    private readonly CngKey _key;
    private readonly ECDsaCng _ecdsa;

    private CngDeviceKey(CngKey key, string storage)
    {
        _key = key;
        _ecdsa = new ECDsaCng(key);
        Storage = storage;
        PublicKey = Convert.ToBase64String(_ecdsa.ExportSubjectPublicKeyInfo());
    }

    public string PublicKey { get; }
    public string Storage { get; }

    private static IEnumerable<(CngProvider Provider, string Label)> Providers()
    {
        yield return (CngProvider.MicrosoftPlatformCryptoProvider, "the TPM (Windows Platform Crypto Provider)");
        yield return (CngProvider.MicrosoftSoftwareKeyStorageProvider, "Windows key storage (non-exportable)");
    }

    private static string KeyName(string slot) => "MonkeForge.Launchpad." + slot;

    public static CngDeviceKey? Open(string slot)
    {
        foreach ((CngProvider provider, var label) in Providers())
        {
            try
            {
                if (CngKey.Exists(KeyName(slot), provider))
                    return new CngDeviceKey(CngKey.Open(KeyName(slot), provider), label);
            }
            catch (CryptographicException)
            {
            }
        }

        return null;
    }

    public static CngDeviceKey Create(string slot)
    {
        CryptographicException? last = null;

        foreach ((CngProvider provider, var label) in Providers())
        {
            try
            {
                var parameters = new CngKeyCreationParameters
                {
                    Provider = provider,
                    ExportPolicy = CngExportPolicies.None,
                    KeyUsage = CngKeyUsages.Signing,
                    KeyCreationOptions = CngKeyCreationOptions.OverwriteExistingKey,
                };

                return new CngDeviceKey(CngKey.Create(CngAlgorithm.ECDsaP256, KeyName(slot), parameters), label);
            }
            catch (CryptographicException ex)
            {
                last = ex;
            }
        }

        throw new CryptographicException("Windows could not create a device key.", last);
    }

    public static void Delete(string slot)
    {
        foreach ((CngProvider provider, _) in Providers())
        {
            try
            {
                if (CngKey.Exists(KeyName(slot), provider))
                    CngKey.Open(KeyName(slot), provider).Delete();
            }
            catch (CryptographicException)
            {
            }
        }
    }

    public byte[] Sign(byte[] data) => _ecdsa.SignData(data, HashAlgorithmName.SHA256);

    public void Dispose()
    {
        _ecdsa.Dispose();
        _key.Dispose();
    }
}

internal sealed class SoftwareDeviceKey : IDeviceKey
{
    private readonly ECDsa _key;

    private SoftwareDeviceKey(ECDsa key, string storage)
    {
        _key = key;
        Storage = storage;
        PublicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
    }

    public string PublicKey { get; }
    public string Storage { get; }

    public static string SecretName(string slot) => "device-" + slot;

    public static SoftwareDeviceKey? Open(ISecretStore secrets, string slot)
    {
        var pkcs8 = secrets.Get(SecretName(slot));

        if (pkcs8 == null)
            return null;

        try
        {
            ECDsa key = ECDsa.Create();
            key.ImportPkcs8PrivateKey(pkcs8, out _);
            return new SoftwareDeviceKey(key, secrets.Description);
        }
        catch (CryptographicException)
        {
            secrets.Delete(SecretName(slot));
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs8);
        }
    }

    public static SoftwareDeviceKey Create(ISecretStore secrets, string slot)
    {
        ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pkcs8 = key.ExportPkcs8PrivateKey();

        try
        {
            secrets.Set(SecretName(slot), pkcs8);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs8);
        }

        return new SoftwareDeviceKey(key, secrets.Description);
    }

    public byte[] Sign(byte[] data) => _key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    public void Dispose() => _key.Dispose();
}
