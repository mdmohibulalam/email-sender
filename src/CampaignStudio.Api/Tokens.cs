using System.Security.Cryptography;
using System.Text;

namespace CampaignStudio.Api;

public sealed class UnsubscribeTokens(StudioSettings settings)
{
    public string Create(Guid id) => id.ToString("N") + "." + Convert.ToHexStringLower(
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.TokenSecret), Encoding.ASCII.GetBytes(id.ToString("N"))));
    public bool TryRead(string token, out Guid id)
    {
        id = Guid.Empty;
        if (token.Length != 97 || token[32] != '.' || !Guid.TryParseExact(token[..32], "N", out id)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Create(id)), Encoding.ASCII.GetBytes(token));
    }
}
