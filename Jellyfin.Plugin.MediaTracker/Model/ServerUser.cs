#pragma warning disable CA1819

using System;

namespace Jellyfin.Plugin.MediaTracker.Model;

/// <summary>
/// MediaTracker user class.
/// </summary>
public class ServerUser
{
  /// <summary>
  /// Initializes a new instance of the <see cref="ServerUser"/> class.
  /// </summary>
  public ServerUser()
  {
    ApiServerUrl = null;
    ApiToken = null;
    LinkedMbUserId = Guid.Empty;
    SkipUnwatchedImportFromServer = true;
    SkipWatchedImportFromServer = false;
    SkipPlaybackProgressImportFromServer = false;
    PostWatchedHistory = true;
    PostUnwatchedHistory = false;
    PostSetWatched = true;
    PostSetUnwatched = false;
    ExtraLogging = false;
    Scrobble = true;
    LastSyncFromServerAt = DateTime.MinValue;
  }

  /// <summary>
  /// Gets or sets the API Server URL.
  /// </summary>
  public string ApiServerUrl { get; set; }

  /// <summary>
  /// Gets or sets the API token.
  /// </summary>
  public string ApiToken { get; set; }

  /// <summary>
  /// Gets or sets the linked Mb user id.
  /// </summary>
  public Guid LinkedMbUserId { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the skip unwatched import option is enabled or not.
  /// </summary>
  public bool SkipUnwatchedImportFromServer { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the skip playback progress import option is enabled or not.
  /// </summary>
  public bool SkipPlaybackProgressImportFromServer { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the skip watched import option is enabled or not.
  /// </summary>
  public bool SkipWatchedImportFromServer { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the watch history should be posted or not.
  /// </summary>
  public bool PostWatchedHistory { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the unwatch history should be posted or not.
  /// </summary>
  public bool PostUnwatchedHistory { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether setting an item to watched should be posted or not.
  /// </summary>
  public bool PostSetWatched { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether the setting an item to unwatched should be posted or not.
  /// </summary>
  public bool PostSetUnwatched { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether extra logging is enabled or not.
  /// </summary>
  public bool ExtraLogging { get; set; }

  /// <summary>
  /// Gets or sets a value indicating whether scrobbling should take place or not.
  /// </summary>
  public bool Scrobble { get; set; }

  /// <summary>
  /// Gets or sets the date the last import from server started, used to detect
  /// library items added since then. MinValue means never synced.
  /// </summary>
  public DateTime LastSyncFromServerAt { get; set; }
}
