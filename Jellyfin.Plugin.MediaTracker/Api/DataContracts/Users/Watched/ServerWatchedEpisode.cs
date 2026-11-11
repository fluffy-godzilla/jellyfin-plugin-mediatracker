using System.Text.Json.Serialization;
using Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;

namespace Jellyfin.Plugin.MediaTracker.Api.DataContracts.Users.Watched;

/// <summary>
/// The server users watched episode class.
/// </summary>
public class ServerWatchedEpisode
{
  /// <summary>
  /// Gets or sets the amount of plays.
  /// </summary>
  [JsonPropertyName("plays")]
  public int Plays { get; set; }

  /// <summary>
  /// Gets or sets the last updated date.
  /// </summary>
  [JsonPropertyName("last_updated_at")]
  public string LastUpdatedAt { get; set; }

  /// <summary>
  /// Gets or sets the last watched date.
  /// </summary>
  [JsonPropertyName("last_watched_at")]
  public string LastWatchedAt { get; set; }

  /// <summary>
  /// Gets or sets the episode.
  /// </summary>
  [JsonPropertyName("episode")]
  public ServerEpisode Episode { get; set; }

  /// <summary>
  /// Gets or sets the show.
  /// </summary>
  [JsonPropertyName("show")]
  public ServerShow Show { get; set; }
}
