using System;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;
using Jellyfin.Plugin.MediaTracker.Helpers;

namespace Jellyfin.Plugin.MediaTracker.Api.DataContracts.Users.Playback;

/// <summary>
/// The server movie paused class.
/// </summary>
public class ServerMoviePaused : ServerMovie
{
  /// <summary>
  /// Gets or sets the progress.
  /// </summary>
  [JsonPropertyName("progress")]
  public double Progress { get; set; }

  /// <summary>
  /// Gets or sets the paused date.
  /// </summary>
  [JsonPropertyName("pausedAt")]
  [JsonConverter(typeof(UnixDateTimeConverter))]
  public DateTime? PausedAt { get; set; }
}
