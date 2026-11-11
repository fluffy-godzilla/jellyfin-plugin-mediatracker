using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediaTracker.Api;
using Jellyfin.Plugin.MediaTracker.Helpers;
using Jellyfin.Plugin.MediaTracker.Model;
using Jellyfin.Plugin.MediaTracker.Model.Enums;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaTracker;

/// <summary>
/// Class ServerEntryPoint.
/// </summary>
public class ServerEntryPoint : IHostedService, IDisposable
{
  private readonly ILogger<ServerEntryPoint> _logger;
  private readonly ISessionManager _sessionManager;
  private readonly IUserDataManager _userDataManager;
  private readonly UserDataManagerEventsHelper _userDataManagerEventsHelper;
  private readonly ServerApi _serverApi;
  private readonly Dictionary<Guid, PlaybackState> _playbackState;

  /// <summary>
  /// Initializes a new instance of the <see cref="ServerEntryPoint"/> class.
  /// </summary>
  /// <param name="sessionManager">The <see cref="ISessionManager"/>.</param>
  /// <param name="userDataManager">The <see cref="IUserDataManager"/>.</param>
  /// <param name="libraryManager">The <see cref="ILibraryManager"/>.</param>
  /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
  /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/>.</param>
  /// <param name="appHost">The <see cref="IServerApplicationHost"/>.</param>
  /// <param name="userManager">The <see cref="IUserManager"/>.</param>
  public ServerEntryPoint(
      ISessionManager sessionManager,
      IUserDataManager userDataManager,
      ILibraryManager libraryManager,
      ILoggerFactory loggerFactory,
      IHttpClientFactory httpClientFactory,
      IServerApplicationHost appHost,
      IUserManager userManager)
  {
    _sessionManager = sessionManager;
    _userDataManager = userDataManager;

    _logger = loggerFactory.CreateLogger<ServerEntryPoint>();
    _playbackState = [];

    _serverApi = new ServerApi(loggerFactory.CreateLogger<ServerApi>(), httpClientFactory, appHost, userDataManager, userManager);
    _userDataManagerEventsHelper = new UserDataManagerEventsHelper(loggerFactory.CreateLogger<UserDataManagerEventsHelper>(), _serverApi);
  }

  /// <summary>
  /// User data was saved.
  /// </summary>
  /// <param name="sender">The sending entity.</param>
  /// <param name="userDataSaveEventArgs">The <see cref="UserDataSaveEventArgs"/>.</param>
  private void OnUserDataSaved(object sender, UserDataSaveEventArgs userDataSaveEventArgs)
  {
    // Ignore change events for any reason other than manually toggling played.
    if (userDataSaveEventArgs.SaveReason != UserDataSaveReason.TogglePlayed)
    {
      return;
    }

    if (userDataSaveEventArgs.Item != null)
    {
      // Determine if user has server credentials
      var serverUser = UserHelper.GetServerUser(userDataSaveEventArgs.UserId);

      // Can't progress if user has no server credentials
      if (serverUser == null || !_serverApi.CanSync(userDataSaveEventArgs.Item, serverUser))
      {
        return;
      }

      if (!serverUser.PostSetWatched && !serverUser.PostSetUnwatched)
      {
        // User doesn't want to post any status changes at all
        return;
      }

      // We have a user who wants to post updates and the item is in a server monitored location
      _userDataManagerEventsHelper.ProcessUserDataSaveEventArgs(userDataSaveEventArgs, serverUser);
    }
  }

  /// <summary>
  /// Media playback has startet.
  /// Let server know that the user has started playback.
  /// </summary>
  /// <param name="sender">The sending entity.</param>
  /// <param name="playbackProgressEventArgs">The <see cref="PlaybackProgressEventArgs"/>.</param>
  private async void KernelPlaybackStart(object sender, PlaybackProgressEventArgs playbackProgressEventArgs)
  {
    _logger.LogDebug("Playback started");

    if (playbackProgressEventArgs.Users == null || playbackProgressEventArgs.Users.Count == 0 || playbackProgressEventArgs.Item == null)
    {
      _logger.LogError("Event details incomplete. Cannot process current media");
      return;
    }

    if (playbackProgressEventArgs.Item is not Movie && playbackProgressEventArgs.Item is not Episode)
    {
      _logger.LogDebug("Syncing playback of {Item} is not supported by server.", playbackProgressEventArgs.Item.Path);
      return;
    }

    foreach (var user in playbackProgressEventArgs.Users)
    {
      var serverUser = UserHelper.GetServerUser(user);

      if (serverUser == null)
      {
        _logger.LogDebug("Could not match user {User} with any stored server credentials.", user.Username);
        continue;
      }

      if (!serverUser.Scrobble)
      {
        _logger.LogDebug("User {User} disabled scrobbling to server.", user.Username);
        continue;
      }

      if (!_serverApi.CanSync(playbackProgressEventArgs.Item, serverUser))
      {
        _logger.LogDebug("Syncing playback of {Item} is forbidden for user {User}.", playbackProgressEventArgs.Item.Path, user.Username);
        continue;
      }

      var video = playbackProgressEventArgs.Item as Video;
      var playbackPositionTicks = playbackProgressEventArgs.PlaybackPositionTicks ?? 0L;
      var progressPercent = video.RunTimeTicks.HasValue && video.RunTimeTicks != 0
                              ? (float)playbackPositionTicks / video.RunTimeTicks.Value * 100.0f
                              : 0.0f;
      var deviceName = playbackProgressEventArgs.DeviceName ?? "Unknown Device";

      _logger.LogDebug("User {User} started watching item {Item}.", user.Username, playbackProgressEventArgs.Item.Path);

      try
      {
        _playbackState[serverUser.LinkedMbUserId] = new PlaybackState
        {
          IsPaused = false,
          PlaybackPositionTicks = playbackPositionTicks,
          PlaybackTime = DateTime.UtcNow
        };

        _logger.LogDebug("Sending {VideoType} playback status (Watching) update to server for user {User}.", video.GetType().Name, user.Username);

        switch (video)
        {
          case Movie movie:
            await _serverApi.SendMovieStatusUpdateAsync(
                movie,
                MediaStatus.Watching,
                serverUser,
                progressPercent,
                deviceName).ConfigureAwait(false);
            break;
          case Episode episode:
            await _serverApi.SendEpisodeStatusUpdateAsync(
                episode,
                MediaStatus.Watching,
                serverUser,
                progressPercent,
                deviceName).ConfigureAwait(false);
            break;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Exception occurred while sending a playback status update to server for user {User}.", user.Username);
      }
    }
  }

  /// <summary>
  /// Media playback has progressed.
  /// Let server know that the user has progressed in playback.
  /// </summary>
  /// <param name="sender">The sending entity.</param>
  /// <param name="playbackProgressEventArgs">The <see cref="PlaybackProgressEventArgs"/>.</param>
  private async void KernelPlaybackProgress(object sender, PlaybackProgressEventArgs playbackProgressEventArgs)
  {
    _logger.LogDebug("Playback progressed");

    if (playbackProgressEventArgs.Users == null || playbackProgressEventArgs.Users.Count == 0 || playbackProgressEventArgs.Item == null)
    {
      _logger.LogError("Event details incomplete. Cannot process current media");
      return;
    }

    if (playbackProgressEventArgs.Item is not Movie && playbackProgressEventArgs.Item is not Episode)
    {
      _logger.LogDebug("Syncing playback of {Item} is not supported by server.", playbackProgressEventArgs.Item.Path);
      return;
    }

    foreach (var user in playbackProgressEventArgs.Users)
    {
      var serverUser = UserHelper.GetServerUser(user);

      if (serverUser == null)
      {
        _logger.LogDebug("Could not match user {User} with any stored server credentials.", user.Username);
        continue;
      }

      if (!serverUser.Scrobble)
      {
        _logger.LogDebug("User {User} disabled scrobbling to server.", user.Username);
        continue;
      }

      if (!_serverApi.CanSync(playbackProgressEventArgs.Item, serverUser))
      {
        _logger.LogDebug("Syncing playback of {Item} is forbidden for user {User}.", playbackProgressEventArgs.Item.Path, user.Username);
        continue;
      }

      if (!_playbackState.TryGetValue(serverUser.LinkedMbUserId, out var state))
      {
        state = new PlaybackState();
      }

      var video = playbackProgressEventArgs.Item as Video;
      var playbackPositionTicks = playbackProgressEventArgs.PlaybackPositionTicks ?? 0L;
      var realTimeDifferenceInSeconds = Math.Round((DateTime.UtcNow - state.PlaybackTime).TotalSeconds);
      var tickDifferenceInSeconds = Math.Round(TimeSpan.FromTicks(playbackPositionTicks - state.PlaybackPositionTicks).TotalSeconds);
      var progressPercent = video.RunTimeTicks.HasValue && video.RunTimeTicks != 0
                              ? (float)playbackPositionTicks / video.RunTimeTicks.Value * 100.0f
                              : 0.0f;
      var deviceName = playbackProgressEventArgs.DeviceName ?? "Unknown Device";

      try
      {
        if (!_playbackState.TryGetValue(serverUser.LinkedMbUserId, out state))
        {
          _logger.LogWarning("Received playback progress from user {User} but initial state was never set - setting it now!", user.Username);
          _playbackState[serverUser.LinkedMbUserId] = new PlaybackState
          {
            IsPaused = false,
            PlaybackPositionTicks = playbackPositionTicks,
            PlaybackTime = DateTime.UtcNow
          };
          continue;
        }

        state.PlaybackPositionTicks = playbackPositionTicks;
        state.PlaybackTime = DateTime.UtcNow;

        // Mark as skipped if tick difference is less than -10 seconds
        // or tick difference is ahead of real time difference by more than 10 seconds.
        var playbackSkipped = tickDifferenceInSeconds < -10 || tickDifferenceInSeconds > realTimeDifferenceInSeconds + 10;
        if (playbackProgressEventArgs.IsPaused == state.IsPaused && !playbackSkipped)
        {
          _logger.LogDebug("Playback state did not change.");
          continue;
        }

        if (playbackSkipped)
        {
          _logger.LogDebug("Playback skipped.");
        }

        state.IsPaused = playbackProgressEventArgs.IsPaused;
        var status = state.IsPaused ? MediaStatus.Paused : MediaStatus.Watching;
        _logger.LogDebug("Sending {VideoType} playback status ({PlaybackStatus}) update to server for user {User}.", video.GetType().Name, status, user.Username);

        switch (video)
        {
          case Movie movie:
            await _serverApi.SendMovieStatusUpdateAsync(
                movie,
                status,
                serverUser,
                progressPercent,
                deviceName).ConfigureAwait(false);
            break;
          case Episode episode:
            await _serverApi.SendEpisodeStatusUpdateAsync(
                episode,
                status,
                serverUser,
                progressPercent,
                deviceName).ConfigureAwait(false);
            break;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Exception occurred while sending a playback status update to server for user {User}.", user.Username);
      }
    }
  }

  /// <summary>
  /// Media playback has stopped.
  /// Depending on playback progress, let server know the user has completed watching the item.
  /// </summary>
  /// <param name="sender">The sending entity.</param>
  /// <param name="playbackStoppedEventArgs">The <see cref="PlaybackStopEventArgs"/>.</param>
  private async void KernelPlaybackStopped(object sender, PlaybackStopEventArgs playbackStoppedEventArgs)
  {
    if (playbackStoppedEventArgs.Users == null || playbackStoppedEventArgs.Users.Count == 0 || playbackStoppedEventArgs.Item == null)
    {
      _logger.LogError("Event details incomplete. Cannot process current media");
      return;
    }

    if (playbackStoppedEventArgs.Item is not Movie && playbackStoppedEventArgs.Item is not Episode)
    {
      _logger.LogDebug("Syncing playback of {Item} is not supported by server.", playbackStoppedEventArgs.Item.Path);
      return;
    }

    _logger.LogDebug("Playback stopped");

    foreach (var user in playbackStoppedEventArgs.Users)
    {
      var serverUser = UserHelper.GetServerUser(user);

      if (serverUser == null)
      {
        _logger.LogDebug("Could not match user {User} with any stored server credentials.", user.Username);
        continue;
      }

      if (!serverUser.Scrobble)
      {
        _logger.LogDebug("User {User} disabled scrobbling to server.", user.Username);
        continue;
      }

      if (!_serverApi.CanSync(playbackStoppedEventArgs.Item, serverUser))
      {
        _logger.LogDebug("Syncing playback of {Item} is forbidden for user {User}.", playbackStoppedEventArgs.Item.Name, user.Username);
        continue;
      }

      var video = playbackStoppedEventArgs.Item as Video;
      var deviceName = playbackStoppedEventArgs.DeviceName ?? "Unknown Device";

      try
      {
        if (playbackStoppedEventArgs.PlayedToCompletion)
        {
          _logger.LogDebug("User {User} completed watching item {Item}. Scrobbling.", user.Username, playbackStoppedEventArgs.Item.Name);
          _logger.LogDebug("Sending {VideoType} playback status (Stop) update to server for user {User}.", video.GetType().Name, user.Username);

          switch (video)
          {
            case Movie movie:
              await _serverApi.SendMovieStatusUpdateAsync(
                  movie,
                  MediaStatus.Stop,
                  serverUser,
                  100,
                  deviceName).ConfigureAwait(false);
              break;
            case Episode episode:
              await _serverApi.SendEpisodeStatusUpdateAsync(
                  episode,
                  MediaStatus.Stop,
                  serverUser,
                  100,
                  deviceName).ConfigureAwait(false);
              break;
          }
        }
        else
        {
          var progressPercent = video.RunTimeTicks.HasValue && video.RunTimeTicks != 0 ?
              (float)(playbackStoppedEventArgs.PlaybackPositionTicks ?? 0) / video.RunTimeTicks.Value * 100.0f : 0.0f;

          _logger.LogDebug("User {User} didn't watch item {Item} until the end. Not scrobbling but pausing playback at current playback position.", user.Username, playbackStoppedEventArgs.Item.Name);

          switch (video)
          {
            case Movie movie:
              await _serverApi.SendMovieStatusUpdateAsync(
                  movie,
                  MediaStatus.Paused,
                  serverUser,
                  progressPercent,
                  deviceName).ConfigureAwait(false);
              break;
            case Episode episode:
              await _serverApi.SendEpisodeStatusUpdateAsync(
                  episode,
                  MediaStatus.Paused,
                  serverUser,
                  progressPercent,
                  deviceName).ConfigureAwait(false);
              break;
          }
        }

        _playbackState.Remove(serverUser.LinkedMbUserId);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Exception occurred while sending a playback status update to server for user {User}.", user.Username);
      }
    }
  }

  /// <inheritdoc />
  public Task StartAsync(CancellationToken cancellationToken)
  {
    _userDataManager.UserDataSaved += OnUserDataSaved;
    _sessionManager.PlaybackStart += KernelPlaybackStart;
    _sessionManager.PlaybackProgress += KernelPlaybackProgress;
    _sessionManager.PlaybackStopped += KernelPlaybackStopped;

    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public Task StopAsync(CancellationToken cancellationToken)
  {
    _userDataManager.UserDataSaved -= OnUserDataSaved;
    _sessionManager.PlaybackStart -= KernelPlaybackStart;
    _sessionManager.PlaybackStopped -= KernelPlaybackStopped;
    _sessionManager.PlaybackProgress -= KernelPlaybackProgress;
    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// Dispose.
  /// </summary>
  /// <param name="disposing">Whether to dispose.</param>
  protected virtual void Dispose(bool disposing)
  {
    if (disposing)
    {
      _userDataManagerEventsHelper?.Dispose();
    }
  }
}
