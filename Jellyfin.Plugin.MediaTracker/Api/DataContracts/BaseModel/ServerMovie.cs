using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;

/// <summary>
/// The server movie class.
/// </summary>
public class ServerMovie : ServerMovieId
{
  /// <summary>
  /// Gets or sets the movie title.
  /// </summary>
  [JsonPropertyName("title")]
  public string Title { get; set; }
}
