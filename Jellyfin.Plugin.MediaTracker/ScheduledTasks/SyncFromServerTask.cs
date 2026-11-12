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
using Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;
using Jellyfin.Plugin.MediaTracker.Api.DataContracts.Users.Playback;
using Jellyfin.Plugin.MediaTracker.Api.DataContracts.Users.Watched;
using Jellyfin.Plugin.MediaTracker.Helpers;
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
  private readonly ILibraryManager _libraryManager;
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
    _libraryManager = libraryManager;
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

    if (serverUser.SkipUnwatchedImportFromServer
        && serverUser.SkipWatchedImportFromServer
        && serverUser.SkipPlaybackProgressImportFromServer)
    {
      _logger.LogDebug("User {Name} disabled (un)watched and playback syncing.", user.Username);
      return;
    }

    var movieSyncNeeded = true;
    var episodeSyncNeeded = true;

    List<ServerMovieWatched> serverWatchedMovies = new List<ServerMovieWatched>();
    List<ServerMoviePaused> serverPausedMovies = new List<ServerMoviePaused>();
    List<ServerShow> serverWatchedShows = new List<ServerShow>();

    try
    {
      /*
       * In order to be as accurate as possible. We need to download the user's show collection and the user's watched shows.
       * It's unfortunate that server doesn't explicitly supply a bulk method to determine shows that have not been watched
       * like they do for movies.
       */
      if (!(serverUser.SkipUnwatchedImportFromServer && serverUser.SkipWatchedImportFromServer))
      {
        // Removals bump the same dates as additions, so a synced domain fetches its full
        // lists: the item loop unmarks anything missing from them.
        if (movieSyncNeeded)
        {
          serverWatchedMovies.AddRange(await _serverApi.SendGetAllWatchedMoviesRequest(serverUser, cancellationToken).ConfigureAwait(false));
        }
      }

      if (!serverUser.SkipPlaybackProgressImportFromServer)
      {
        if (movieSyncNeeded)
        {
          serverPausedMovies.AddRange(await _serverApi.SendGetAllPausedMoviesRequest(serverUser, cancellationToken).ConfigureAwait(false));
        }
      }

      if (!(serverUser.SkipUnwatchedImportFromServer && serverUser.SkipWatchedImportFromServer && serverUser.SkipPlaybackProgressImportFromServer))
      {
        if (episodeSyncNeeded)
        {
          var shows = await _serverApi.SendGetWatchedShowsRequest(serverUser, cancellationToken).ConfigureAwait(false);
          foreach (var s in shows)
          {
            serverWatchedShows.Add(await _serverApi.SendGetShowDetailsRequest(s, serverUser, cancellationToken).ConfigureAwait(false));
          }
        }
      }
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Locked)
    {
      _logger.LogError(ex, "Skipping sync for user {User} because their server account is locked", user.Username);
      return;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Exception handled");
      throw;
    }

    _logger.LogInformation("Server watched movies for user {User}: {Count}", user.Username, serverWatchedMovies.Count);
    _logger.LogInformation("Server paused movies for user {User}: {Count}", user.Username, serverPausedMovies.Count);
    _logger.LogInformation("Server watched shows for user {User}: {Count}", user.Username, serverWatchedShows.Count);

    var includeItemTypes = new List<BaseItemKind>();
    if (movieSyncNeeded)
    {
      includeItemTypes.Add(BaseItemKind.Movie);
    }

    if (episodeSyncNeeded)
    {
      includeItemTypes.Add(BaseItemKind.Episode);
    }

    var baseQuery = new InternalItemsQuery(user)
    {
      IncludeItemTypes = includeItemTypes.ToArray(),
      IsVirtualItem = false,
      OrderBy = [
        (ItemSortBy.SeriesSortName, SortOrder.Ascending),
        (ItemSortBy.SortName, SortOrder.Ascending)
      ]
    };

    var totalCount = _libraryManager.GetCount(baseQuery);

    const int Limit = 100;
    int offset = 0, previousCount;

    // Purely for progress reporting
    var percentPerIteration = totalCount > 0 ? percentPerUser / (totalCount / (double)Limit) : 0;

    do
    {
      baseQuery.Limit = Limit;
      baseQuery.StartIndex = offset;

      var mediaItems = _libraryManager.GetItemList(baseQuery);

      previousCount = mediaItems.Count;
      offset += Limit;

      mediaItems = mediaItems.Where(i => _serverApi.CanSync(i, serverUser)).ToList();

      // Purely for progress reporting
      var percentPerItem = mediaItems.Count > 0 ? percentPerIteration / mediaItems.Count : 0;

      foreach (var movie in mediaItems.OfType<Movie>())
      {
        cancellationToken.ThrowIfCancellationRequested();
        var matchedWatchedMovie = Extensions.FindMatch(movie, serverWatchedMovies);
        var matchedPausedMovie = Extensions.FindMatch(movie, serverPausedMovies);
        var userData = _userDataManager.GetUserData(user, movie);
        bool changed = false;

        if (matchedWatchedMovie != null)
        {
          _logger.LogDebug("Movie is in watched list of user {User}: {Name}", user.Username, movie.Name);

          if (!serverUser.SkipWatchedImportFromServer)
          {
            DateTime? tLastPlayed = matchedWatchedMovie.LastWatchedAt;

            // Set movie as watched
            if (!userData.Played)
            {
              // Only change LastPlayedDate if not set or the local and remote are more than 10 minutes apart
              _logger.LogDebug("Marking movie as watched for user {User} locally: {Name}", user.Username, movie.Name);
              if (tLastPlayed == null && userData.LastPlayedDate == null)
              {
                _logger.LogDebug("Movie's local and remote last played date are missing, falling back to the current time for user {User} locally: {Name}", user.Username, movie.Name);
                userData.LastPlayedDate = DateTime.UtcNow;
              }

              if (tLastPlayed != null
                  && userData.LastPlayedDate != null
                  && (tLastPlayed.Value - userData.LastPlayedDate.Value).Duration() > TimeSpan.FromMinutes(10)
                  && userData.LastPlayedDate < tLastPlayed)
              {
                _logger.LogDebug("Setting movie's last played date to remote which is more than 10 minutes more recent than local (remote: {Remote} | local: {Local}) for user {User} locally: {Name}", tLastPlayed, userData.LastPlayedDate, user.Username, movie.Name);
                userData.LastPlayedDate = tLastPlayed;
              }

              userData.Played = true;
              changed = true;
            }

            // Keep the highest play count
            if (userData.PlayCount < matchedWatchedMovie.Plays)
            {
              _logger.LogDebug("Adjusting movie's play count to match a higher remote value (remote: {Remote} | local: {Local}) for user {User} locally: {Name}", matchedWatchedMovie.Plays, userData.PlayCount, user.Username, movie.Name);
              userData.PlayCount = matchedWatchedMovie.Plays;
              changed = true;
            }

            // Update last played if remote time is more recent
            if (tLastPlayed != null && (userData.LastPlayedDate == null || userData.LastPlayedDate < tLastPlayed))
            {
              _logger.LogDebug("Adjusting movie's last played date to match a more recent remote last played date (remote: {Remote} | local: {Local}) for user {User} locally: {Name}", tLastPlayed, userData.LastPlayedDate, user.Username, movie.Name);
              userData.LastPlayedDate = tLastPlayed;
              changed = true;
            }
          }
        }
        else if (!serverUser.SkipUnwatchedImportFromServer)
        {
          _logger.LogDebug("Movie is not in watched list: {Name}", movie.Name);

          // Set movie as unwatched
          if (userData.Played)
          {
            _logger.LogDebug("Marking movie as unwatched for user {User} locally: {Name}", user.Username, movie.Name);
            userData.Played = false;
            changed = true;
          }
        }

        if (!serverUser.SkipPlaybackProgressImportFromServer && matchedPausedMovie != null)
        {
          _logger.LogDebug("Movie is in paused list of user {User}: {Name}", user.Username, movie.Name);

          var lastPlayed = userData.LastPlayedDate;
          DateTime? paused = matchedPausedMovie.PausedAt;

          if (lastPlayed == null || (paused != null && (lastPlayed + TimeSpan.FromSeconds(1)) < paused))
          {
            _logger.LogDebug("Local last played date is missing or remote has more recent paused at date (remote: {Remote} | local: {Local}). Setting playback progress of movie for user {User} locally to {Progress}%: {Data}", paused, lastPlayed, user.Username, matchedPausedMovie.Progress, movie.Name);

            var runtimeTicks = movie.GetRunTimeTicksForPlayState();
            var serverPlaybackTicks = runtimeTicks != 0
                ? (long)(matchedPausedMovie.Progress * runtimeTicks)
                : 0;

            userData.PlaybackPositionTicks = serverPlaybackTicks;
            changed = true;
          }
        }

        // Only process if there's a change
        if (changed)
        {
          _userDataManager.SaveUserData(
              user,
              movie,
              userData,
              UserDataSaveReason.Import,
              cancellationToken);
        }

        // Purely for progress reporting
        currentProgress += percentPerItem;
        progress.Report(currentProgress);
      }

      foreach (var episode in mediaItems.OfType<Episode>())
      {
        cancellationToken.ThrowIfCancellationRequested();
        var matchedWatchedShow = Extensions.FindMatch(episode.Series, serverWatchedShows);
        var matchedEpisode = Extensions.FindMatch(episode, matchedWatchedShow);
        var userData = _userDataManager.GetUserData(user, episode);
        bool changed = false;
        bool episodeWatched = false;
        bool episodeWatchedUnknown = false;

        if (!serverUser.SkipWatchedImportFromServer && matchedWatchedShow != null)
        {
          if (matchedEpisode == null && !Extensions.HasAnyProviderTvId(episode))
          {
            // Without progress data the episode can't be matched at all, so its
            // watched state is unknown rather than unwatched
            episodeWatchedUnknown = true;
          }

          var matchedWatchedEpisode = Extensions.IsWatched(matchedEpisode) ? matchedEpisode : null;
          if (matchedWatchedEpisode != null)
          {
            DateTime? tLastPlayed = matchedWatchedEpisode.LastWatchedAt;

            _logger.LogDebug("Episode is in watched list of user {User}: {Data}", user.Username, GetVerboseEpisodeData(episode));

            episodeWatched = true;

            // Set episode as watched
            if (!userData.Played)
            {
              // Only change LastPlayedDate if not set or the local and remote are more than 10 minutes apart
              _logger.LogDebug("Marking episode as watched for user {User} locally: {Data}", user.Username, GetVerboseEpisodeData(episode));
              if (tLastPlayed == null && userData.LastPlayedDate == null)
              {
                _logger.LogDebug("Episode's local and remote last played date are missing, falling back to the current time for user {User} locally: {Data}", user.Username, GetVerboseEpisodeData(episode));
                userData.LastPlayedDate = DateTime.UtcNow;
              }

              if (tLastPlayed != null
                  && userData.LastPlayedDate != null
                  && (tLastPlayed.Value - userData.LastPlayedDate.Value).Duration() > TimeSpan.FromMinutes(10)
                  && userData.LastPlayedDate < tLastPlayed)
              {
                _logger.LogDebug("Setting episode's last played date to remote which is more than 10 minutes more recent than local (remote: {Remote} | local: {Local}) for user {User} locally: {Data}", tLastPlayed, userData.LastPlayedDate, user.Username, GetVerboseEpisodeData(episode));
                userData.LastPlayedDate = tLastPlayed;
              }

              userData.Played = true;
              changed = true;
            }

            // Update last played if remote time is more recent
            if (tLastPlayed != null && (userData.LastPlayedDate == null || userData.LastPlayedDate < tLastPlayed))
            {
              _logger.LogDebug("Adjusting episode's last played date to match a more recent remote last played date (remote: {Remote} | local: {Local}) for user {User} locally: {Name}", tLastPlayed, userData.LastPlayedDate, user.Username, episode.Name);
              userData.LastPlayedDate = tLastPlayed;
              changed = true;
            }

            // Keep the highest play count
            if (userData.PlayCount < matchedWatchedEpisode.Plays)
            {
              _logger.LogDebug("Adjusting episode's play count to match a higher remote value (remote: {Remote} | local: {Local}) for user {User} locally: {Data}", matchedWatchedEpisode.Plays, userData.PlayCount, user.Username, GetVerboseEpisodeData(episode));
              userData.PlayCount = matchedWatchedEpisode.Plays;
              changed = true;
            }
          }
          else
          {
            _logger.LogDebug("No watched episode data found for user {User} for {Data}", user.Username, GetVerboseEpisodeData(episode));
          }
        }
        else
        {
          _logger.LogDebug("No show data found for user {User} for {Data}", user.Username, GetVerboseEpisodeData(episode));
        }

        if (episodeWatchedUnknown)
        {
          _logger.LogDebug("Keeping local watched state of episode for user {User} because server progress is unavailable: {Data}", user.Username, GetVerboseEpisodeData(episode));
        }
        else if (!serverUser.SkipUnwatchedImportFromServer && !episodeWatched)
        {
          _logger.LogDebug("Episode not in watched list of user {User}: {Data}", user.Username, GetVerboseEpisodeData(episode));
          if (userData.Played)
          {
            _logger.LogDebug("Marking episode as unwatched for user {User} locally: {Data}", user.Username, GetVerboseEpisodeData(episode));
            userData.Played = false;
            changed = true;
          }
        }

        var matchedPausedEpisode = Extensions.IsPaused(matchedEpisode) ? matchedEpisode : null;
        if (!serverUser.SkipPlaybackProgressImportFromServer && matchedPausedEpisode != null)
        {
          _logger.LogDebug("Episode is in paused list of user {User}: {Data}", user.Username, GetVerboseEpisodeData(episode));

          var lastPlayed = userData.LastPlayedDate;
          DateTime? paused = matchedPausedEpisode.PausedAt;

          if (lastPlayed == null || (paused != null && (lastPlayed + TimeSpan.FromSeconds(1)) < paused))
          {
            _logger.LogDebug("Local last played date is missing or remote has more recent paused at date (remote: {Remote} | local: {Local}). Setting playback progress of episode for user {User} locally to {Progress}%: {Data}", paused, lastPlayed, user.Username, matchedPausedEpisode.Progress, GetVerboseEpisodeData(episode));

            var runtimeTicks = episode.GetRunTimeTicksForPlayState();
            var serverPlaybackTicks = runtimeTicks != 0
                ? (long)(matchedPausedEpisode.Progress * runtimeTicks)
                : 0;

            userData.PlaybackPositionTicks = serverPlaybackTicks;
            changed = true;
          }
        }

        // Only process if changed
        if (changed)
        {
          _userDataManager.SaveUserData(
              user,
              episode,
              userData,
              UserDataSaveReason.Import,
              cancellationToken);
        }

        // Purely for progress reporting
        currentProgress += percentPerItem;
        progress.Report(currentProgress);
      }
    }
    while (previousCount != 0);
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
