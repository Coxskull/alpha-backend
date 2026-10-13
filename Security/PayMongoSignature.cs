using System.Security.Cryptography;
using System.Text;
namespace Alpha.API.Security;

public static class PayMongoSignature
{
    public static bool Verify(string body, string header, string secret, bool liveMode, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(secret)) return false;
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in header.Split(','))
        {
            var pair = part.Trim().Split('=', 2);
            if (pair.Length != 2 || !fields.TryAdd(pair[0], pair[1])) return false;
        }
        if (!fields.TryGetValue("t", out var timestamp) || !long.TryParse(timestamp, out var seconds)
            || Math.Abs((decimal)now.ToUnixTimeSeconds() - seconds) > 300
            || !fields.TryGetValue(liveMode ? "li" : "te", out var signature) || signature.Length != 64) return false;
        try
        {
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(timestamp + "." + body));
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature));
        }
        catch (FormatException) { return false; }
    }
}
