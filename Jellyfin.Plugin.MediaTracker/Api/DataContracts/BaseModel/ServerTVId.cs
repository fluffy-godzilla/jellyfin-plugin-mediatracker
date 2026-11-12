#nullable enable

using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;

/// <summary>
/// The server tv id class.
/// </summary>
public class ServerTVId : ServerIMDBandTMDBId
{
  /// <summary>
  /// Gets or sets the TVDb id.
  /// </summary>
  [JsonPropertyName("tvdbId")]
  public int? Tvdb { get; set; }
}
