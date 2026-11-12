// TODO: FINISH

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;
using Jellyfin.Plugin.MediaTracker.Model;
using Jellyfin.Plugin.MediaTracker.Model.Enums;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Plugin.MediaTracker.Api;

/// <summary>
/// MediaTracker API client class.
/// </summary>
public class ServerApi
{
  // mediatracker server caps page size at 40, so we need quite a few pages to get all items. 5000 is a safe upper limit.
  private const int MaxPages = 5000;

  private const int MaxRetryAttempts = 3;

  private static readonly SemaphoreSlim _serverResourcePool = new SemaphoreSlim(1, 1);
  private static readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(3);
  private static readonly TimeSpan _tooManyRequestDelay = TimeSpan.FromSeconds(1);
  private static readonly TimeSpan _gatewayDelay = TimeSpan.FromSeconds(5);

  private readonly ILogger<ServerApi> _logger;
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly IServerApplicationHost _appHost;
  private readonly IUserDataManager _userDataManager;
  private readonly IUserManager _userManager;
  private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Options;

  /// <summary>
  /// Initializes a new instance of the <see cref="ServerApi"/> class.
  /// </summary>
  /// <param name="logger">The logger.</param>
  /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/>.</param>
  /// <param name="appHost">The <see cref="IServerApplicationHost"/>.</param>
  /// <param name="userDataManager">The <see cref="IUserDataManager"/>.</param>
  /// <param name="userManager">The <see cref="IUserManager"/>.</param>
  public ServerApi(
    ILogger<ServerApi> logger,
    IHttpClientFactory httpClientFactory,
    IServerApplicationHost appHost,
    IUserDataManager userDataManager,
    IUserManager userManager)
  {
    _httpClientFactory = httpClientFactory;
    _appHost = appHost;
    _userDataManager = userDataManager;
    _userManager = userManager;
    _logger = logger;
  }

  /// <summary>
  /// Checks whether it's possible/allowed to sync a <see cref="BaseItem"/> for a <see cref="ServerUser"/>.
  /// </summary>
  /// <param name="item">Item to check.</param>
  /// <param name="serverUser">The server user to check for.</param>
  /// <returns><see cref="bool"/> indicating if it's possible/allowed to sync this item.</returns>
  public bool CanSync(BaseItem item, ServerUser serverUser)
  {
    if (item.Path == null || item.LocationType == LocationType.Virtual)
    {
      return false;
    }

    if (item is Movie movie)
    {
      return movie.HasProviderId(MetadataProvider.Imdb)
          || movie.HasProviderId(MetadataProvider.Tmdb);
    }

    if (item is Episode episode
        && episode.Series != null
        && !episode.IsMissingEpisode
        && (episode.IndexNumber.HasValue
            || HasAnyProviderTvIds(episode)
        ))
    {
      var series = episode.Series;

      return HasAnyProviderTvIds(series);
    }

    return false;
  }

  /// <summary>
  /// Report to server that a movie is being watched or has been watched.
  /// </summary>
  /// <param name="movie">The movie being watched/scrobbled.</param>
  /// <param name="mediaStatus">The <see cref="MediaStatus"/> indicating whether a movie is being watched or scrobbled.</param>
  /// <param name="serverUser">The <see cref="ServerUser"/> who's watch progress is being updated.</param>
  /// <param name="progressPercent">The progress percentage.</param>
  /// <param name="deviceName">The device name.</param>
  /// <returns>Task{bool}.</returns>
  public async Task<bool> SendMovieStatusUpdateAsync(Movie movie, MediaStatus mediaStatus, ServerUser serverUser, float progressPercent, string deviceName)
  {
    const string apiPath = "/api/progress/by-external-id?token={apiToken}";

    var imdbId = TryParseInt64(movie.GetProviderId(MetadataProvider.Imdb));
    var tmdbId = TryParseInt64(movie.GetProviderId(MetadataProvider.Tmdb));

    if (imdbId == null && tmdbId == null)
    {
      _logger.LogError("Both imdb and tmdb id are absent");
      return false;
    }

    string action = null;
    switch (mediaStatus)
    {
      case MediaStatus.Watching:
        action = "playing";
        break;
      case MediaStatus.Paused:
        action = "paused";
        break;
    }

    var progress = progressPercent / 100f;
    var durationInMs = movie.RunTimeTicks.HasValue ? TimeSpan.FromTicks(movie.RunTimeTicks.Value).TotalMilliseconds : 0.0;

    _logger.LogInformation("Updating progress for movie '{0}' - {1:0.00}% - {2}", movie.Name, progressPercent, action);

    var data = new
    {
      mediaType = "movie",
      id = new
      {
        imdbId,
        tmdbId
      },
      action,
      progress,
      duration = durationInMs,
      device = deviceName,
    };

    return await PutToServer<bool>(apiPath, data, serverUser, CancellationToken.None).ConfigureAwait(false);
  }

  /// <summary>
  /// Reports to server that an episode is being watched or has been watched.
  /// </summary>
  /// <param name="episode">The <see cref="Episode"/> being watched.</param>
  /// <param name="mediaStatus">The <see cref="MediaStatus"/> indicating whether an episode is being watched or scrobbled.</param>
  /// <param name="serverUser">The <see cref="ServerUser"/> who's watch progress is being updated.</param>
  /// <param name="progressPercent">The progress percentage.</param>
  /// <param name="deviceName">The device name.</param>
  /// <returns>Task{bool}.</returns>
  public async Task<bool> SendEpisodeStatusUpdateAsync(Episode episode, MediaStatus mediaStatus, ServerUser serverUser, float progressPercent, string deviceName)
  {
    const string apiPath = "/api/progress/by-external-id?token={apiToken}";

    if (episode == null || episode.Series == null)
    {
      _logger.LogError("Episode or series is null");
      return false;
    }

    var imdbId = TryParseInt64(episode.GetProviderId(MetadataProvider.Imdb));
    var tmdbId = TryParseInt64(episode.Series.GetProviderId(MetadataProvider.Tmdb));

    if (imdbId == null && tmdbId == null)
    {
      _logger.LogError("Both imdb and tmdb id are absent");
      return false;
    }

    var seasonNumber = episode.Season.IndexNumber;
    var episodeNumber = episode.IndexNumber;
    var durationInMs = episode.RunTimeTicks.HasValue ? TimeSpan.FromTicks(episode.RunTimeTicks.Value).TotalMilliseconds : 0.0;

    string action = null;
    switch (mediaStatus)
    {
      case MediaStatus.Watching:
        action = "playing";
        break;
      case MediaStatus.Paused:
        action = "paused";
        break;
    }

    var progress = progressPercent / 100f;

    _logger.LogInformation("Updating progress for episode of '{0}' S{1:00}E{2:00} - {3:0.00}% - {4}", episode.SeriesName, seasonNumber, episodeNumber, progressPercent, action);

    var data = new
    {
      mediaType = "tv",
      id = new
      {
        imdbId,
        tmdbId
      },
      seasonNumber,
      episodeNumber,
      action,
      progress,
      duration = durationInMs,
      device = deviceName,
    };

    return await PutToServer<bool>(apiPath, data, serverUser, CancellationToken.None).ConfigureAwait(false);
  }

  /// <summary>
  /// Send a list of movies to server that have been marked as watched or unwatched.
  /// </summary>
  /// <param name="movies">The list of movies to send.</param>
  /// <param name="serverUser">The <see cref="ServerUser"/> who's library is being updated.</param>
  /// <param name="seen">True if movies are being marked seen, false otherwise.</param>
  /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
  /// <returns>Task{bool}.</returns>
  public async Task<bool> SendMoviePlaystateUpdates(
      ICollection<Movie> movies,
      ServerUser serverUser,
      bool seen,
      CancellationToken cancellationToken)
  {
    foreach (var movie in movies)
    {
      if (movie == null)
      {
        _logger.LogError("Movie is null");
        continue;
      }

      var result = await SendMoviePlaystateUpdate(movie, seen, serverUser, cancellationToken).ConfigureAwait(false);
      if (!result)
      {
        _logger.LogWarning("Failed to send movie playstate update for {Movie}", movie.Name);
      }
    }

    return true;
  }

  /// <summary>
  /// Send a list of episodes to server that have been marked watched or unwatched.
  /// </summary>
  /// <param name="episodes">The list of episodes to send.</param>
  /// <param name="serverUser">The <see cref="ServerUser"/> who's library is being updated.</param>
  /// <param name="seen">True if episodes are being marked seen, false otherwise.</param>
  /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
  /// <returns>Task{bool}.</returns>
  public async Task<bool> SendEpisodePlaystateUpdates(
      ICollection<Episode> episodes,
      ServerUser serverUser,
      bool seen,
      CancellationToken cancellationToken)
  {
    foreach (var episode in episodes)
    {
      if (episode == null)
      {
        _logger.LogError("Episode is null");
        continue;
      }

      var result = await SendEpisodePlaystateUpdate(episode, seen, serverUser, cancellationToken).ConfigureAwait(false);
      if (!result)
      {
        _logger.LogWarning("Failed to send episode playstate update for {Episode}", episode.Name);
      }
    }

    return true;
  }

  private async Task<bool> SendMoviePlaystateUpdate(Movie movie, bool seen, ServerUser serverUser, CancellationToken cancellationToken)
  {
    const string apiPath = "/api/seen/by-external-id?token={apiToken}";

    var imdbId = TryParseInt64(movie.GetProviderId(MetadataProvider.Imdb));
    var tmdbId = TryParseInt64(movie.GetProviderId(MetadataProvider.Tmdb));

    if (imdbId == null && tmdbId == null)
    {
      _logger.LogError("Both imdb and tmdb id are absent for movie {Movie}", movie.Name);
      return false;
    }

    var duration = movie.RunTimeTicks.HasValue ? TimeSpan.FromTicks(movie.RunTimeTicks.Value).TotalMilliseconds : 0.0;

    _logger.LogInformation("Updating seen state for movie '{0}' - {1}", movie.Name, seen ? "seen" : "unseen");

    var data = new
    {
      mediaType = "movie",
      id = new
      {
        imdbId,
        tmdbId
      },
      duration,
      unset = !seen ? true : (bool?)null
    };

    return await PutToServer<bool>(apiPath, data, serverUser, cancellationToken).ConfigureAwait(false);
  }

  private async Task<bool> SendEpisodePlaystateUpdate(Episode episode, bool seen, ServerUser serverUser, CancellationToken cancellationToken)
  {
    const string apiPath = "/api/seen/by-external-id?token={apiToken}";

    if (episode.Series == null)
    {
      _logger.LogError("Episode {Episode} has no series", episode.Name);
      return false;
    }

    var imdbId = TryParseInt64(episode.GetProviderId(MetadataProvider.Imdb));
    var tmdbId = TryParseInt64(episode.Series.GetProviderId(MetadataProvider.Tmdb));

    if (imdbId == null && tmdbId == null)
    {
      _logger.LogError("Both imdb and tmdb id are absent for episode {Episode}", episode.Name);
      return false;
    }

    var seasonNumber = episode.Season.IndexNumber;
    var episodeNumber = episode.IndexNumber;
    var duration = episode.RunTimeTicks.HasValue ? TimeSpan.FromTicks(episode.RunTimeTicks.Value).TotalMilliseconds : 0.0;

    _logger.LogInformation("Updating seen state for episode of '{0}' S{1:00}E{2:00} - {3}", episode.SeriesName, seasonNumber, episodeNumber, seen ? "seen" : "unseen");

    var data = new
    {
      mediaType = "tv",
      id = new
      {
        imdbId,
        tmdbId
      },
      seasonNumber,
      episodeNumber,
      duration,
      unset = !seen ? true : (bool?)null
    };

    return await PutToServer<bool>(apiPath, data, serverUser, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  /// Get all watched movies.
  /// </summary>
  /// <param name="serverUser">The <see cref="ServerUser"/>.</param>
  /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
  /// <returns>Task{List{DataContracts.Users.Watched.ServerMovieWatched}}.</returns>
  public async Task<List<DataContracts.Users.Watched.ServerMovieWatched>> SendGetAllWatchedMoviesRequest(ServerUser serverUser, CancellationToken cancellationToken)
  {
    const string apiPath = "/api/items/paginated?token={apiToken}&mediaType=movie&onlySeenItems=true&page={page}";
    return await GetFromServerWithPaging<DataContracts.Users.Watched.ServerMovieWatched>(apiPath, serverUser, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  /// Get all paused movies.
  /// </summary>
  /// <param name="serverUser">The <see cref="ServerUser"/>.</param>
  /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
  /// <returns>Task{List{DataContracts.Users.Playback.ServerMoviePaused}}.</returns>
  public async Task<List<DataContracts.Users.Playback.ServerMoviePaused>> SendGetAllPausedMoviesRequest(ServerUser serverUser, CancellationToken cancellationToken)
  {
    const string apiPath = "/api/items/paginated?token={apiToken}&mediaType=movie&onlyWithProgress=true&page={page}";
    return await GetFromServerWithPaging<DataContracts.Users.Playback.ServerMoviePaused>(apiPath, serverUser, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  /// Get all watched shows.
  /// </summary>
  /// <param name="serverUser">The <see cref="ServerUser"/>.</param>
  /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
  /// <returns>Task{List{ServerShow}}.</returns>
  public async Task<List<ServerShow>> SendGetWatchedShowsRequest(ServerUser serverUser, CancellationToken cancellationToken)
  {
    const string apiPath = "/api/items/paginated?token={apiToken}&mediaType=tv&onlySeenItems=true&page={page}";
    return await GetFromServerWithPaging<ServerShow>(apiPath, serverUser, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  /// Get the show details, including seasons, episodes and playstate/progress.
  /// </summary>
  /// <param name="show">The <see cref="ServerShow"/>.</param>
  /// <param name="serverUser">The <see cref="ServerUser"/>.</param>
  /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
  /// <returns>Task{ServerShow}.</returns>
  internal async Task<ServerShow> SendGetShowDetailsRequest(ServerShow show, ServerUser serverUser, CancellationToken cancellationToken)
  {
    string apiPath = $"/api/details/{show.Id}?token={{apiToken}}";
    return await GetFromServer<ServerShow>(apiPath, serverUser, cancellationToken).ConfigureAwait(false);
  }

  private async Task<T> GetFromServer<T>(string apiPath, ServerUser serverUser, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(serverUser);

    var apiPathUpdated = apiPath.Replace("{apiToken}", serverUser.ApiToken.ToString(CultureInfo.InvariantCulture), StringComparison.InvariantCulture);
    var uri = new Uri(new Uri(serverUser.ApiServerUrl), apiPathUpdated);

    var httpClient = GetHttpClient();

    await _serverResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

    try
    {
      var response = await RetryHttpRequest(async () => await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false), cancellationToken, retryServerError: true).ConfigureAwait(false);
      if (response.StatusCode == HttpStatusCode.NotFound)
      {
        return default;
      }

      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      _serverResourcePool.Release();
    }
  }

  private async Task<List<T>> GetFromServerWithPaging<T>(string apiPath, ServerUser serverUser, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(serverUser);

    var apiPathUpdated = apiPath.Replace("{apiToken}", serverUser.ApiToken.ToString(CultureInfo.InvariantCulture), StringComparison.InvariantCulture);
    var uri = new Uri(new Uri(serverUser.ApiServerUrl), apiPathUpdated);

    var httpClient = GetHttpClient();
    var page = 1;
    var result = new List<T>();

    while (page <= MaxPages)
    {
      var urlWithPage = uri.ToString()
          .Replace("{page}", page.ToString(CultureInfo.InvariantCulture), StringComparison.InvariantCulture);

      ServerPagedData<T> pageResult;
      int pageCount;

      await _serverResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

      try
      {
        var response = await RetryHttpRequest(async () => await httpClient.GetAsync(urlWithPage, cancellationToken).ConfigureAwait(false), cancellationToken, retryServerError: true).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
          return result;
        }

        response.EnsureSuccessStatusCode();
        pageResult = await response.Content.ReadFromJsonAsync<ServerPagedData<T>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        pageCount = pageResult.TotalPages;
      }
      finally
      {
        _serverResourcePool.Release();
      }

      if (pageResult == null || pageResult.Data.Count == 0)
      {
        break;
      }

      result.AddRange(pageResult.Data);

      if (pageCount == 0 || page >= pageCount)
      {
        break;
      }

      page++;
    }

    if (page > MaxPages)
    {
      _logger.LogWarning("Stopped requesting pages from the server after the maximum of {MaxPages} pages", MaxPages);
    }

    return result;
  }

  /// <summary>
  /// Puts data to server API path, authenticating with <see cref="ServerUser"/>.
  /// </summary>
  /// <param name="apiPath">The API Path.</param>
  /// <param name="data">The data object.</param>
  /// <param name="serverUser">The <see cref="ServerUser"/>.</param>
  /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
  private async Task<T> PutToServer<T>(string apiPath, object data, ServerUser serverUser, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(serverUser);

    var apiPathUpdated = apiPath.Replace("{apiToken}", serverUser.ApiToken.ToString(CultureInfo.InvariantCulture), StringComparison.InvariantCulture);
    var uri = new Uri(new Uri(serverUser.ApiServerUrl), apiPathUpdated);
    if (serverUser.ExtraLogging)
    {
      _logger.LogInformation("Put to {Url}: {@JsonData}", uri, data);
    }

    var httpClient = GetHttpClient();

    var bytes = JsonSerializer.SerializeToUtf8Bytes(data, _jsonOptions);
    using var content = new ByteArrayContent(bytes);
    content.Headers.Add(HeaderNames.ContentType, MediaTypeNames.Application.Json);

    await _serverResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

    try
    {
      var response = await RetryHttpRequest(async () => await httpClient.PutAsync(uri, content, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
      if (response.StatusCode == HttpStatusCode.NotFound)
      {
        return default;
      }

      response.EnsureSuccessStatusCode();
      if (response.Content.Headers.ContentType != null &&
          response.Content.Headers.ContentType.MediaType != null &&
          response.Content.Headers.ContentType.MediaType.Equals(MediaTypeNames.Application.Json, StringComparison.OrdinalIgnoreCase))
      {
        return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken).ConfigureAwait(false);
      }

      if (typeof(T) == typeof(string))
      {
        return (T)(object)await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
      }

      if (typeof(T) == typeof(bool))
      {
        return (T)(object)true;
      }

      // everything else is unsupported, return default
      return default;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Exception handled in PutToServer");
      throw;
    }
    finally
    {
      _serverResourcePool.Release();
    }
  }

  private async Task<HttpResponseMessage> RetryHttpRequest(Func<Task<HttpResponseMessage>> function, CancellationToken cancellationToken, bool retryServerError = false)
  {
    HttpResponseMessage response = null;
    Exception lastException = null;

    for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
    {
      TimeSpan delay;

      try
      {
        response = await function().ConfigureAwait(false);
        var statusCode = response.StatusCode;

        if (statusCode == HttpStatusCode.Locked)
        {
          _logger.LogError("The server account is locked - the user needs to contact server support before syncing can continue");
          return response;
        }

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
          delay = GetRetryAfterDelay(response) ?? GetBackoffDelay(_tooManyRequestDelay, attempt);
          _logger.LogDebug("Too many requests while communicating with server - waiting {Time}s", delay.TotalSeconds);
        }
        else if (statusCode == HttpStatusCode.BadGateway
            || statusCode == HttpStatusCode.GatewayTimeout
            || statusCode == HttpStatusCode.ServiceUnavailable)
        {
          delay = GetRetryAfterDelay(response) ?? GetBackoffDelay(_gatewayDelay, attempt);
          _logger.LogDebug("Connectivity error while communicating with server - waiting {Time}s", delay.TotalSeconds);
        }
        else if (retryServerError && statusCode == HttpStatusCode.InternalServerError)
        {
          // Only GETs retry 500s: a POST may already have been applied
          delay = GetBackoffDelay(_gatewayDelay, attempt);
          _logger.LogDebug("Server error while communicating with server - waiting {Time}s", delay.TotalSeconds);
        }
        else
        {
          return response;
        }
      }
      catch (HttpRequestException ex)
      {
        response = null;
        lastException = ex;
        delay = GetBackoffDelay(_gatewayDelay, attempt);
        _logger.LogWarning(ex, "Request to server failed - waiting {Time}s", delay.TotalSeconds);
      }
      catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
      {
        response = null;
        lastException = ex;
        delay = GetBackoffDelay(_gatewayDelay, attempt);
        _logger.LogWarning(ex, "Request to server timed out - waiting {Time}s", delay.TotalSeconds);
      }

      if (attempt < MaxRetryAttempts - 1)
      {
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
      }
    }

    if (response == null)
    {
      throw new HttpRequestException($"Request to server failed after {MaxRetryAttempts} attempts", lastException);
    }

    return response;
  }

  private static TimeSpan? GetRetryAfterDelay(HttpResponseMessage response)
  {
    var retryAfter = response.Headers.RetryAfter;
    if (retryAfter == null)
    {
      return null;
    }

    var delay = retryAfter.Delta ?? (retryAfter.Date.HasValue ? retryAfter.Date.Value - DateTimeOffset.UtcNow : null);
    if (!delay.HasValue)
    {
      return null;
    }

    return delay.Value > TimeSpan.Zero ? delay.Value : TimeSpan.Zero;
  }

  private static TimeSpan GetBackoffDelay(TimeSpan baseDelay, int attempt)
  {
    var backoff = baseDelay.TotalMilliseconds * Math.Pow(2, attempt);
    var jittered = (backoff / 2) + (Random.Shared.NextDouble() * backoff / 2);

    return TimeSpan.FromMilliseconds(jittered);
  }

  private HttpClient GetHttpClient()
  {
    var client = _httpClientFactory.CreateClient(NamedClient.Default);
    client.Timeout = _requestTimeout;
    return client;
  }

  private static long? TryParseInt64(string value)
  {
    if (value == null)
    {
      return null;
    }

    if (long.TryParse(value, out long res))
    {
      return res;
    }

    return null;
  }

  private bool HasAnyProviderTvIds(BaseItem item)
  {
    return item.HasProviderId(MetadataProvider.Imdb)
           || item.HasProviderId(MetadataProvider.Tmdb)
           || item.HasProviderId(MetadataProvider.Tvdb)
           || item.HasProviderId(MetadataProvider.TvRage);
  }
}
