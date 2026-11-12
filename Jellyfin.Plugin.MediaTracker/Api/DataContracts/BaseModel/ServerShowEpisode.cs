using System;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.MediaTracker.Helpers;

namespace Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;

/// <summary>
/// The server show episode class.
/// </summary>
public class ServerShowEpisode : ServerTVId
{
  /// <summary>
  /// Gets or sets the season number.
  /// </summary>
  [JsonPropertyName("seasonNumber")]
  public int Season { get; set; }

  /// <summary>
  /// Gets or sets the episode number.
  /// </summary>
  [JsonPropertyName("episodeNumber")]
  public int Number { get; set; }

  /// <summary>
  /// Gets or sets the episode title.
  /// </summary>
  [JsonPropertyName("title")]
  public string Title { get; set; }

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
