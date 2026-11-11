using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;

/// <summary>
/// The server id class.
/// </summary>
public class ServerId
{
  /// <summary>
  /// Gets or sets the server item id.
  /// </summary>
  [JsonPropertyName("server")]
  public int? Server { get; set; }

  /// <summary>
  /// Gets or sets the item slug.
  /// </summary>
  [JsonPropertyName("slug")]
  public string Slug { get; set; }
}
