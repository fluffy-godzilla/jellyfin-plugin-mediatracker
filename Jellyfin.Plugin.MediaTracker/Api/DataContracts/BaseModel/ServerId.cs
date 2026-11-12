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
  [JsonPropertyName("id")]
  public int Id { get; set; }
}
