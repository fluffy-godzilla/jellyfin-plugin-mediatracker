#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MediaTracker.Api.DataContracts.Users.Watched;

/// <summary>
/// The server users season watched class.
/// </summary>
public class ServerSeasonWatched
{
  /// <summary>
  /// Gets or sets the season number.
  /// </summary>
  [JsonPropertyName("number")]
  public int Number { get; set; }

  /// <summary>
  /// Gets or sets the episodes.
  /// </summary>
  [JsonPropertyName("episodes")]
  public IReadOnlyList<ServerEpisodeWatched> Episodes { get; set; }
}
