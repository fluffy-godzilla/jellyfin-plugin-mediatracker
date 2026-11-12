using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MediaTracker.Helpers;

/// <summary>
/// Converts Unix timestamps to and from UTC <see cref="DateTime"/> values.
/// </summary>
public class UnixDateTimeConverter : JsonConverter<DateTime>
{
  /// <inheritdoc/>
  public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    long timestamp = reader.TokenType switch
    {
      JsonTokenType.Number => reader.GetInt64(),
      JsonTokenType.String when long.TryParse(reader.GetString(), out long parsed) => parsed,
      _ => throw new JsonException($"Unexpected token {reader.TokenType} when parsing Unix timestamp.")
    };

    // 13+ digits indicates milliseconds; 10 digits indicates seconds
    DateTimeOffset dateTimeOffset = Math.Abs(timestamp) >= 1_000_000_000_000L
        ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
        : DateTimeOffset.FromUnixTimeSeconds(timestamp);

    return dateTimeOffset.UtcDateTime;
  }

  /// <inheritdoc/>
  public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
  {
    long seconds = new DateTimeOffset(value.ToUniversalTime()).ToUnixTimeSeconds();
    writer.WriteNumberValue(seconds);
  }
}
