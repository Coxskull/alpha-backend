using System.Text;
namespace Alpha.API.Security;

public static class RuntimeSecurity
{
    public static void Validate(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("A runtime JWT signing key of at least 32 bytes is required.");
        // One-way fingerprint of the signing key exposed by prior tracked configuration.
        if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(key))) == "C8ACB776850CA25633CFF90334408706EBCB70831EC7E3511BB27326B532F9E5")
            throw new InvalidOperationException("The previously exposed signing key must be replaced.");
        if (string.IsNullOrWhiteSpace(configuration["DATABASE_URL"]))
            throw new InvalidOperationException("A runtime DATABASE_URL is required.");
    }
}
