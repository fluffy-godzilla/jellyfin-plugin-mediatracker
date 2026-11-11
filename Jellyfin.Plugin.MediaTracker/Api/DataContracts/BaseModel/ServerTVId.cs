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
  [JsonPropertyName("tvdb")]
  public string? Tvdb { get; set; }

  /// <summary>
  /// Gets or sets the TVRage id.
  /// </summary>
  [JsonPropertyName("tvrage")]
  public string? Tvrage { get; set; }
}
