using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.MediaTracker.Api;
using Jellyfin.Plugin.MediaTracker.Api.DataContracts.Users.Watched;
using Jellyfin.Plugin.MediaTracker.Helpers;
using Jellyfin.Plugin.MediaTracker.Model;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;

namespace Jellyfin.Plugin.MediaTracker.ScheduledTasks;

/// <summary>
/// Task that will Sync each users MediaTracker profile with their local library. This task will only include
/// watched/seen states.
/// </summary>
public class SyncFromServerTask : IScheduledTask
{
  private readonly IUserManager _userManager;
  private readonly IUserDataManager _userDataManager;
  private readonly ILogger<SyncFromServerTask> _logger;

  private readonly ServerApi _serverApi;

  /// <summary>
  /// Initializes a new instance of the <see cref="SyncFromServerTask"/> class.
  /// </summary>
  /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
  /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
  /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/> interface.</param>
  /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
  /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
  /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
  public SyncFromServerTask(
      ILoggerFactory loggerFactory,
      IUserManager userManager,
      IUserDataManager userDataManager,
      IHttpClientFactory httpClientFactory,
      IServerApplicationHost appHost,
      ILibraryManager libraryManager)
  {
    _userManager = userManager;
    _userDataManager = userDataManager;
    _logger = loggerFactory.CreateLogger<SyncFromServerTask>();

    _serverApi = new ServerApi(loggerFactory.CreateLogger<ServerApi>(), httpClientFactory, appHost, userDataManager, userManager);
  }

  /// <inheritdoc />
  public string Key => "MediaTrackerSyncFromServerTask";

  /// <inheritdoc />
  public string Name => "Import watched states and playback progress from MediaTracker server";

  /// <inheritdoc />
  public string Description => "Imports each user's watched status and playback progress from the configured MediaTracker server to all items in the user's library";

  /// <inheritdoc />
  public string Category => "MediaTracker";

  /// <inheritdoc />
  public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Enumerable.Empty<TaskTriggerInfo>();

  /// <inheritdoc />
  public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
  {
    var users = _userManager.GetUsers().Where(user => UserHelper.GetServerUser(user) != null).ToList();

    // No point going further if we don't have users.
    if (users.Count == 0)
    {
      _logger.LogDebug("No Users returned");
      return;
    }

    // Purely for progress reporting
    var percentPerUser = 100d / users.Count;
    double currentProgress = 0;
    var numComplete = 0;

    foreach (var user in users)
    {
      try
      {
        await SyncServerDataForUser(user, currentProgress, progress, percentPerUser, cancellationToken).ConfigureAwait(false);

        numComplete++;
        currentProgress = percentPerUser * numComplete;
        progress.Report(currentProgress);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error syncing server data for user {UserName}", user.Username);
      }
    }
  }

  private async Task SyncServerDataForUser(User user, double currentProgress, IProgress<double> progress, double percentPerUser, CancellationToken cancellationToken)
  {
    var serverUser = UserHelper.GetServerUser(user);

    if (serverUser.SkipWatchedImportFromServer && serverUser.SkipPlaybackProgressImportFromServer)
    {
      _logger.LogDebug("User {Name} disabled (un)watched and playback syncing.", user.Username);
      return;
    }

    // TODO: finish this ...
  }

  /// <summary>
  /// Replaces the watched shows with the extended=progress variant so episodes without provider ids can be matched.
  /// </summary>
  /// <returns>True if the progress data was fetched, false if the request failed.</returns>
  private async Task<bool> FetchWatchedShowsProgress(List<ServerShowWatched> serverWatchedShows, ServerUser serverUser, User user, CancellationToken cancellationToken)
  {
    try
    {
      var watchedShowsProgress = await _serverApi.SendGetWatchedShowsProgressRequest(serverUser, cancellationToken).ConfigureAwait(false);
      serverWatchedShows.Clear();
      serverWatchedShows.AddRange(watchedShowsProgress);
      _logger.LogInformation("Server watched shows progress for user {User}: {Count}", user.Username, watchedShowsProgress.Count);
      return true;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      _logger.LogWarning(ex, "Failed to fetch watched shows progress for user {User} - episodes without provider ids will keep their local watched state", user.Username);
      return false;
    }
  }

  private static string GetVerboseEpisodeData(Episode episode)
  {
    var episodeString = new StringBuilder()
        .Append("Episode: ")
        .Append(episode.GetSeasonNumber().ToString(CultureInfo.InvariantCulture))
        .Append('x')
        .Append(episode.IndexNumber != null ? episode.IndexNumber : "null")
        .Append(" '").Append(episode.Name).Append("' ")
        .Append("Series: '")
        .Append(episode.Series != null
            ? !string.IsNullOrWhiteSpace(episode.Series.Name)
                ? episode.Series.Name
                : "null property"
            : "null class")
        .Append("' ")
        .Append("Tvdb id: ")
        .Append(episode.GetProviderId(MetadataProvider.Tvdb) ?? "null").Append(' ')
        .Append("Tmdb id: ")
        .Append(episode.GetProviderId(MetadataProvider.Tmdb) ?? "null").Append(' ')
        .Append("Imdb id: ")
        .Append(episode.GetProviderId(MetadataProvider.Imdb) ?? "null").Append(' ')
        .Append("TvRage id: ")
        .Append(episode.GetProviderId(MetadataProvider.TvRage) ?? "null");

    return episodeString.ToString();
  }
}
