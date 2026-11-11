using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Jellyfin.Plugin.MediaTracker.Api;
using Jellyfin.Plugin.MediaTracker.Model;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaTracker.Helpers;

/// <summary>
/// Helper class used to update the watched status of movies/episodes.
/// Attempts to organise requests to lower API calls.
/// </summary>
internal sealed class UserDataManagerEventsHelper : IDisposable
{
  private readonly ILogger<UserDataManagerEventsHelper> _logger;
  private readonly ServerApi _serverApi;
  private readonly Dictionary<Guid, UserDataPackage> _userDataPackages;
  private Timer _queueTimer;

  /// <summary>
  /// Initializes a new instance of the <see cref="UserDataManagerEventsHelper"/> class.
  /// </summary>
  /// <param name="logger">The <see cref="ILogger{UserDataManagerEventsHelper}"/>.</param>
  /// <param name="serverApi">The <see cref="ServerApi"/>.</param>
  public UserDataManagerEventsHelper(ILogger<UserDataManagerEventsHelper> logger, ServerApi serverApi)
  {
    _userDataPackages = [];
    _logger = logger;
    _serverApi = serverApi;
  }

  /// <summary>
  /// Process user data save event for server users.
  /// </summary>
  /// <param name="userDataSaveEventArgs">The <see cref="UserDataSaveEventArgs"/>.</param>
  /// <param name="serverUser">The <see cref="ServerUser"/>.</param>
  public void ProcessUserDataSaveEventArgs(UserDataSaveEventArgs userDataSaveEventArgs, ServerUser serverUser)
  {
    lock (_userDataPackages)
    {
      _userDataPackages.TryGetValue(serverUser.LinkedMbUserId, out var userPackage);

      userPackage ??= new UserDataPackage();

      if (_queueTimer == null)
      {
        _queueTimer = new Timer(
            OnTimerCallback,
            null,
            TimeSpan.FromMilliseconds(5000),
            Timeout.InfiniteTimeSpan);
      }
      else
      {
        _queueTimer.Change(TimeSpan.FromMilliseconds(5000), Timeout.InfiniteTimeSpan);
      }

      if (userDataSaveEventArgs.Item is Movie movie)
      {
        if (serverUser.PostSetWatched && userDataSaveEventArgs.UserData.Played)
        {
          userPackage.SeenMovies.Add(movie);

          // Force update server if we have more than 100 seen movies in the queue due to API
          if (userPackage.SeenMovies.Count >= 100)
          {
            _serverApi.SendMoviePlaystateUpdates(
                userPackage.SeenMovies.ToList(),
                serverUser,
                true,
                CancellationToken.None).ConfigureAwait(false);
            userPackage.SeenMovies.Clear();
          }
        }
        else if (serverUser.PostSetUnwatched)
        {
          userPackage.UnSeenMovies.Add(movie);

          // Force update server if we have more than 100 unseen movies in the queue due to API
          if (userPackage.UnSeenMovies.Count >= 100)
          {
            _serverApi.SendMoviePlaystateUpdates(
                userPackage.UnSeenMovies.ToList(),
                serverUser,
                false,
                CancellationToken.None).ConfigureAwait(false);
            userPackage.UnSeenMovies.Clear();
          }
        }
      }
      else if (userDataSaveEventArgs.Item is Episode episode)
      {
        // If it's not the series we're currently storing, upload our episodes and reset the arrays
        if (userPackage.CurrentSeriesId != null)
        {
          if (!userPackage.CurrentSeriesId.Equals(episode.Series.Id))
          {
            if (userPackage.SeenEpisodes.Count != 0)
            {
              _serverApi.SendEpisodePlaystateUpdates(
                  userPackage.SeenEpisodes.ToList(),
                  serverUser,
                  true,
                  CancellationToken.None).ConfigureAwait(false);
              userPackage.SeenEpisodes.Clear();
            }

            if (userPackage.UnSeenEpisodes.Count != 0)
            {
              _serverApi.SendEpisodePlaystateUpdates(
                  userPackage.UnSeenEpisodes.ToList(),
                  serverUser,
                  false,
                  CancellationToken.None).ConfigureAwait(false);
              userPackage.UnSeenEpisodes.Clear();
            }

            userPackage.CurrentSeriesId = episode.Series.Id;
          }
          else
          {
            // Force update server if we have more than 100 seen episodes in the queue due to API
            if (userPackage.SeenEpisodes.Count >= 100)
            {
              _serverApi.SendEpisodePlaystateUpdates(
                  userPackage.SeenEpisodes.ToList(),
                  serverUser,
                  true,
                  CancellationToken.None).ConfigureAwait(false);
              userPackage.SeenEpisodes.Clear();
            }

            // Force update server if we have more than 100 unseen episodes in the queue due to API
            if (userPackage.UnSeenEpisodes.Count >= 100)
            {
              _serverApi.SendEpisodePlaystateUpdates(
                  userPackage.UnSeenEpisodes.ToList(),
                  serverUser,
                  false,
                  CancellationToken.None).ConfigureAwait(false);
              userPackage.UnSeenEpisodes.Clear();
            }
          }
        }

        if (serverUser.PostSetWatched && userDataSaveEventArgs.UserData.Played)
        {
          userPackage.SeenEpisodes.Add(episode);
        }
        else if (serverUser.PostSetUnwatched)
        {
          userPackage.UnSeenEpisodes.Add(episode);
        }
      }

      _userDataPackages[serverUser.LinkedMbUserId] = userPackage;
    }
  }

  private void OnTimerCallback(object state)
  {
    Dictionary<Guid, UserDataPackage> userDataQueue;
    lock (_userDataPackages)
    {
      if (_userDataPackages.Count == 0)
      {
        _logger.LogInformation("No events... stopping queue timer");
        return;
      }

      userDataQueue = new Dictionary<Guid, UserDataPackage>(_userDataPackages);
      _userDataPackages.Clear();
    }

    foreach (var package in userDataQueue)
    {
      if (package.Value.UnSeenMovies.Count != 0)
      {
        _serverApi.SendMoviePlaystateUpdates(
            package.Value.UnSeenMovies.ToList(),
            UserHelper.GetServerUser(package.Key),
            false,
            CancellationToken.None).ConfigureAwait(false);
        package.Value.UnSeenMovies.Clear();
      }

      if (package.Value.SeenMovies.Count != 0)
      {
        _serverApi.SendMoviePlaystateUpdates(
            package.Value.SeenMovies.ToList(),
            UserHelper.GetServerUser(package.Key),
            true,
            CancellationToken.None).ConfigureAwait(false);
        package.Value.SeenMovies.Clear();
      }

      if (package.Value.UnSeenEpisodes.Count != 0)
      {
        _serverApi.SendEpisodePlaystateUpdates(
            package.Value.UnSeenEpisodes.ToList(),
            UserHelper.GetServerUser(package.Key),
            false,
            CancellationToken.None).ConfigureAwait(false);
        package.Value.UnSeenEpisodes.Clear();
      }

      if (package.Value.SeenEpisodes.Count != 0)
      {
        _serverApi.SendEpisodePlaystateUpdates(
            package.Value.SeenEpisodes.ToList(),
            UserHelper.GetServerUser(package.Key),
            true,
            CancellationToken.None).ConfigureAwait(false);
        package.Value.SeenEpisodes.Clear();
      }
    }
  }

  public void Dispose()
  {
    _queueTimer?.Dispose();
  }
}
