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
  /// Checks if a <see cref="BaseItem"/> matches a <see cref="ServerMovie"/>.
  /// </summary>
  /// <param name="item">The <see cref="BaseItem"/>.</param>
  /// <param name="movie">The IEnumerable of <see cref="ServerMovie"/>.</param>
  /// <returns><see cref="bool"/> indicating if the <see cref="BaseItem"/> matches a <see cref="ServerMovie"/>.</returns>
  private static bool IsMatch(BaseItem item, ServerMovie movie)
  {
    if (item.TryGetProviderId(MetadataProvider.Imdb, out var imdbId)
        && string.Equals(imdbId, movie.Imdb, StringComparison.Ordinal))
    {
      return true;
    }

    if (item.TryGetProviderId(MetadataProvider.Tmdb, out var tmdbId)
        && string.Equals(tmdbId, movie.Tmdb?.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
    {
      return true;
    }

    return false;
  }
}
