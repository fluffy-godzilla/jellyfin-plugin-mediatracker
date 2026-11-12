using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;

/// <summary>
/// The server show class.
/// </summary>
public class ServerShow : ServerTVId
{
  /// <summary>
  /// Gets or sets the show title.
  /// </summary>
  [JsonPropertyName("title")]
  public string Title { get; set; }

  /// <summary>
  /// Gets or sets the seasons.
  /// </summary>
  [JsonPropertyName("seasons")]
  public IReadOnlyList<ServerShowSeason> Seasons { get; set; }
}
