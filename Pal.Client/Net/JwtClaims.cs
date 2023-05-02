using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pal.Client.Net
{
    internal sealed class JwtClaims
    {
        [JsonPropertyName("nameid")]
        public Guid NameId { get; set; }

        [JsonPropertyName("role")]
        [JsonConverter(typeof(JwtRoleConverter))]
        public List<string> Roles { get; set; } = new();

        [JsonPropertyName("nbf")]
        [JsonConverter(typeof(JwtDateConverter))]
        public DateTimeOffset NotBefore { get; set; }

        [JsonPropertyName("exp")]
        [JsonConverter(typeof(JwtDateConverter))]
        public DateTimeOffset ExpiresAt { get; set; }

        public static JwtClaims FromAuthToken(string authToken)
        {
            if (string.IsNullOrEmpty(authToken))
                throw new ArgumentException("Server sent no auth token", nameof(authToken));

            string[] parts = authToken.Split('.');
            if (parts.Length != 3)
                throw new ArgumentException("Unsupported token type", nameof(authToken));

            // fix padding manually
            string payload = parts[1].Replace(",", "=").Replace("-", "+").Replace("/", "_");
            if (payload.Length % 4 == 2)
                payload += "==";
            else if (payload.Length % 4 == 3)
                payload += "=";

            string content = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            return JsonSerializer.Deserialize<JwtClaims>(content) ?? throw new InvalidOperationException("token deserialization returned null");
        }
    }

    internal sealed class JwtRoleConverter : JsonConverter<List<string>>
    {
        public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return new List<string> { reader.GetString() ?? throw new JsonException("no value present") };
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                List<string> result = new();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        result.Sort();
                        return result;
                    }

                    if (reader.TokenType != JsonTokenType.String)
                        throw new JsonException("string expected");

                    result.Add(reader.GetString() ?? throw new JsonException("no value present"));
                }

                throw new JsonException("read to end of document");
            }
            else
                throw new JsonException("bad token type");
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options) => throw new NotImplementedException();
    }

    public sealed class JwtDateConverter : JsonConverter<DateTimeOffset>
    {
        static readonly DateTimeOffset Zero = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.Number)
                throw new JsonException("bad token type");

            return Zero.AddSeconds(reader.GetInt64());
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) => throw new NotImplementedException();
    }
}
