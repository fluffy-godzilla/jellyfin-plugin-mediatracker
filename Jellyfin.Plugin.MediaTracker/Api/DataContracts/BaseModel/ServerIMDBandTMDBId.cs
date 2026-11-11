#nullable enable

using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;

/// <summary>
/// The server IDMb and TMDb id class.
/// </summary>
public class ServerIMDBandTMDBId : ServerId
{
  /// <summary>
  /// Gets or sets the IMDb id.
  /// </summary>
  [JsonPropertyName("imdb")]
  public string? Imdb { get; set; }

  /// <summary>
  /// Gets or sets the TMDb id.
  /// </summary>
  [JsonPropertyName("tmdb")]
  public int? Tmdb { get; set; }
}
