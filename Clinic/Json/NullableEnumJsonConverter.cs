using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clinic.Json
{
    /// <summary>
    /// Reads/writes a nullable enum as its string name in JSON.
    ///
    /// Missing, null or blank values become null so the [Required] validation
    /// attribute can produce a friendly message instead of a raw deserialization
    /// exception. Truly invalid values produce a JsonException with a
    /// user-friendly message (which ModelStateErrorMapper then keeps as-is).
    /// </summary>
    public sealed class NullableEnumJsonConverter<T> : JsonConverter<T?>
        where T : struct, Enum
    {
        private readonly string _friendlyName;

        public NullableEnumJsonConverter(string friendlyName)
        {
            _friendlyName = friendlyName;
        }

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();

                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                if (Enum.TryParse<T>(value, ignoreCase: true, out var parsed))
                {
                    return parsed;
                }

                if (int.TryParse(value, out var number) && Enum.IsDefined(typeof(T), number))
                {
                    return (T)(object)number;
                }
            }

            throw new JsonException($"Please select a valid {_friendlyName}.");
        }

        public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value.ToString());
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
