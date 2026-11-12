using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;

/// <summary>
/// The server paged data class.
/// </summary>
/// <typeparam name="T">The type of elements in the list.</typeparam>
public class ServerPagedData<T>
{
  /// <summary>
  /// Gets or sets the from item index.
  /// </summary>
  [JsonPropertyName("from")]
  public int From { get; set; }

  /// <summary>
  /// Gets or sets the to item index.
  /// </summary>
  [JsonPropertyName("to")]
  public int To { get; set; }

  /// <summary>
  /// Gets or sets the page data.
  /// </summary>
  [JsonPropertyName("data")]
  public IReadOnlyList<T> Data { get; set; }

  /// <summary>
  /// Gets or sets the total item count.
  /// </summary>
  [JsonPropertyName("total")]
  public int Total { get; set; }

  /// <summary>
  /// Gets or sets the page number.
  /// </summary>
  [JsonPropertyName("page")]
  public int Page { get; set; }

  /// <summary>
  /// Gets or sets the total number of pages.
  /// </summary>
  [JsonPropertyName("totalPages")]
  public int TotalPages { get; set; }
}
