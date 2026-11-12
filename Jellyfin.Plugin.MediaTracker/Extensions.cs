using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MediaTracker.Api.DataContracts.BaseModel;
using Jellyfin.Plugin.MediaTracker.Api.DataContracts.Users.Playback;
using Jellyfin.Plugin.MediaTracker.Api.DataContracts.Users.Watched;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;

namespace Jellyfin.Plugin.MediaTracker;

/// <summary>
/// Class for server plugin extension functions.
/// </summary>
public static class Extensions
{
  /// <summary>
  /// Checks if an episode is paused.
  /// </summary>
  /// <param name="episode">The <see cref="ServerShowEpisode"/>.</param>
  /// <returns><see cref="bool"/> indicating if the episode is paused.</returns>
  public static bool IsPaused(ServerShowEpisode episode)
  {
    return episode != null && episode.PausedAt != null;
  }

  /// <summary>
  /// Checks if an episode is watched.
  /// </summary>
  /// <param name="episode">The <see cref="ServerShowEpisode"/>.</param>
  /// <returns><see cref="bool"/> indicating if the episode is watched.</returns>
  public static bool IsWatched(ServerShowEpisode episode)
  {
    return episode != null && episode.Plays > 0;
  }

  /// <summary>
  /// Gets the season number of an <see cref="Episode"/>.
  /// </summary>
  /// <param name="episode">The <see cref="Episode"/>.</param>
  /// <returns>int.</returns>
  public static int GetSeasonNumber(this Episode episode)
      => (episode.ParentIndexNumber != 0 ? episode.ParentIndexNumber ?? 1 : episode.ParentIndexNumber).Value;

  /// <summary>
  /// Gets a watched match for a movie.
  /// </summary>
  /// <param name="item">The <see cref="BaseItem"/>.</param>
  /// <param name="results">>The <see cref="IEnumerable{ServerMovieWatched}"/>.</param>
  /// <returns>ServerMovieWatched.</returns>
  public static ServerMovieWatched FindMatch(BaseItem item, IEnumerable<ServerMovieWatched> results)
  {
    return results.FirstOrDefault(i => IsMatch(item, i));
  }

  /// <summary>
  /// Gets a paused match for a movie.
  /// </summary>
  /// <param name="item">The <see cref="BaseItem"/>.</param>
  /// <param name="results">>The <see cref="IEnumerable{ServerMoviePaused}"/>.</param>
  /// <returns>ServerMoviePaused.</returns>
  public static ServerMoviePaused FindMatch(BaseItem item, IEnumerable<ServerMoviePaused> results)
  {
    return results.FirstOrDefault(i => IsMatch(item, i));
  }

  /// <summary>
  /// Gets a watched match for a show.
  /// </summary>
  /// <param name="item">The <see cref="BaseItem"/>.</param>
  /// <param name="results">>The <see cref="IEnumerable{ServerShow}"/>.</param>
  /// <returns>ServerShow.</returns>
  public static ServerShow FindMatch(BaseItem item, IEnumerable<ServerShow> results)
  {
    return results.FirstOrDefault(i => IsMatch(item, i));
  }

  /// <summary>
  /// Gets a match for an episode from a show's episodes.
  /// </summary>
  /// <param name="item">The <see cref="Episode"/>.</param>
  /// <param name="show">>The <see cref="ServerShow"/>.</param>
  /// <returns>ServerShowEpisode.</returns>
  public static ServerShowEpisode FindMatch(Episode item, ServerShow show)
  {
    var season = show?.Seasons?.FirstOrDefault(s => s.Season == item.GetSeasonNumber());
    return season?.Episodes?.FirstOrDefault(e => item.ContainsEpisodeNumber(e.Number));
  }

  /// <summary>
  /// Checks if an <see cref="Episode"/> has any tv provider id that the server episodes carry.
  /// </summary>
  /// <param name="item">The <see cref="Episode"/>.</param>
  /// <returns><see cref="bool"/> indicating if the <see cref="Episode"/> has any tv provider id.</returns>
  public static bool HasAnyProviderTvId(Episode item)
  {
    return item.HasProviderId(MetadataProvider.Tvdb)
        || item.HasProviderId(MetadataProvider.Imdb)
        || item.HasProviderId(MetadataProvider.Tmdb);
  }

  /// <summary>
  /// Checks if a <see cref="BaseItem"/> matches a <see cref="ServerIMDBandTMDBId"/>.
  /// </summary>
  /// <param name="item">The <see cref="BaseItem"/>.</param>
  /// <param name="id">The IEnumerable of <see cref="ServerIMDBandTMDBId"/>.</param>
  /// <returns><see cref="bool"/> indicating if the <see cref="BaseItem"/> matches a <see cref="ServerIMDBandTMDBId"/>.</returns>
  private static bool IsMatch(BaseItem item, ServerIMDBandTMDBId id)
  {
    if (item.TryGetProviderId(MetadataProvider.Imdb, out var imdbId)
        && string.Equals(imdbId, id.Imdb, StringComparison.Ordinal))
    {
      return true;
    }

    if (item.TryGetProviderId(MetadataProvider.Tmdb, out var tmdbId)
        && string.Equals(tmdbId, id.Tmdb?.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
    {
      return true;
    }

    return false;
  }

  /// <summary>
  /// Checks if a <see cref="BaseItem"/> matches a <see cref="ServerTVId"/>.
  /// </summary>
  /// <param name="item">The <see cref="BaseItem"/>.</param>
  /// <param name="tvId">The IEnumerable of <see cref="ServerTVId"/>.</param>
  /// <returns><see cref="bool"/> indicating if the <see cref="BaseItem"/> matches a <see cref="ServerTVId"/>.</returns>
  private static bool IsMatch(BaseItem item, ServerTVId tvId)
  {
    if (item.TryGetProviderId(MetadataProvider.Tvdb, out var tvdbId)
        && string.Equals(tvdbId, tvId.Tvdb?.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
    {
      return true;
    }

    if (item.TryGetProviderId(MetadataProvider.Tmdb, out var tmdbId)
        && string.Equals(tmdbId, tvId.Tmdb?.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
    {
      return true;
    }

    if (item.TryGetProviderId(MetadataProvider.Imdb, out var imdbId)
        && string.Equals(imdbId, tvId.Imdb, StringComparison.Ordinal))
    {
      return true;
    }

    return false;
  }
}
