using System;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;
using Jellyfin.Plugin.MediaTracker.Helpers;

namespace Jellyfin.Plugin.MediaTracker.Api.DataContracts.Users.Watched;

/// <summary>
/// The server movie watched class.
/// </summary>
public class ServerMovieWatched : ServerMovie
{
  /// <summary>
  /// Gets or sets the amount of plays.
  /// </summary>
  [JsonPropertyName("seen")]
  [JsonConverter(typeof(BoolToIntConverter))]
  public int Plays { get; set; }

  /// <summary>
  /// Gets or sets the last watched date.
  /// </summary>
  [JsonPropertyName("lastSeenAt")]
  [JsonConverter(typeof(UnixDateTimeConverter))]
  public DateTime? LastWatchedAt { get; set; }
}
