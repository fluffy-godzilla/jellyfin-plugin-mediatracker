using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MediaTracker.Helpers
{
  /// <summary>Converts JSON booleans to integer values.</summary>
  public class BoolToIntConverter : JsonConverter<int>
  {
    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <inheritdoc/>
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      return reader.TokenType switch
      {
        JsonTokenType.True => 1,
        JsonTokenType.False => 0,
        JsonTokenType.Null => 0,
        // Optional: handles cases where the JSON already sends 1 or 0
        JsonTokenType.Number => reader.GetInt32(),
        _ => throw new JsonException($"Unexpected token {reader.TokenType} when converting boolean to int.")
      };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
      writer.WriteBooleanValue(value != 0);
    }
  }
}
