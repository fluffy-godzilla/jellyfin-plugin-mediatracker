using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;

/// <summary>
/// The server show season class.
/// </summary>
public class ServerShowSeason : ServerTVId
{
  /// <summary>
  /// Gets or sets the season number.
  /// </summary>
  [JsonPropertyName("seasonNumber")]
  public int Season { get; set; }

  /// <summary>
  /// Gets or sets the season title.
  /// </summary>
  [JsonPropertyName("title")]
  public string Title { get; set; }

  /// <summary>
  /// Gets or sets the episodes.
  /// </summary>
  [JsonPropertyName("episodes")]
  public IReadOnlyList<ServerShowEpisode> Episodes { get; set; }
}
