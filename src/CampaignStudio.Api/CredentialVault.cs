using System.Security.Cryptography;
using System.Text;
namespace CampaignStudio.Api;

public sealed class CredentialVault(StudioSettings settings)
{
    private readonly byte[] key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.TokenSecret), "CampaignStudio.SMTP.v1"u8.ToArray());
    public string Seal(string value)
    {
        byte[] clear = Encoding.UTF8.GetBytes(value), nonce = RandomNumberGenerator.GetBytes(12), tag = new byte[16], cipher = new byte[clear.Length];
        try { using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, clear, cipher, tag); return "v1:" + Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray()); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public string Open(string value)
    {
        if (!value.StartsWith("v1:", StringComparison.Ordinal)) throw new CryptographicException("Credential format is invalid.");
        byte[] data; try { data = Convert.FromBase64String(value[3..]); } catch (FormatException) { throw new CryptographicException("Credential format is invalid."); }
        if (data.Length < 28) throw new CryptographicException("Credential format is invalid.");
        byte[] clear = new byte[data.Length - 28];
        try { using var aes = new AesGcm(key, 16); aes.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), clear); return Encoding.UTF8.GetString(clear); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
}
