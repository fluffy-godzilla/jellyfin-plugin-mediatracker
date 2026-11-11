using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Data.Enums;
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
}
