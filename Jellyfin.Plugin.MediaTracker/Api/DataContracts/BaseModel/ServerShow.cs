using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;

/// <summary>
/// The server show class.
/// </summary>
public class ServerShow
{
  /// <summary>
  /// Gets or sets the show title.
  /// </summary>
  [JsonPropertyName("title")]
  public string Title { get; set; }

  /// <summary>
  /// Gets or sets the show year.
  /// </summary>
  [JsonPropertyName("year")]
  public int? Year { get; set; }

  /// <summary>
  /// Gets or sets the show ids.
  /// </summary>
  [JsonPropertyName("ids")]
  public ServerShowId Ids { get; set; }
}
