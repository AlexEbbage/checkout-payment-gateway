using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.Api.Application.Idempotency;

public static class IdempotencyHasher
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Hash<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonSerializerOptions);
        return Hash(json);
    }

    public static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
