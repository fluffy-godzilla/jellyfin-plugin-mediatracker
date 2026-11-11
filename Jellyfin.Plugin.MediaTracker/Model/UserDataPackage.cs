using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.MediaTracker.Model;

/// <summary>
/// Class that contains all the items to be reported to server and supporting properties.
/// </summary>
internal sealed class UserDataPackage
{
  public UserDataPackage()
  {
    SeenMovies = [];
    UnSeenMovies = [];
    SeenEpisodes = [];
    UnSeenEpisodes = [];
  }

  public Guid? CurrentSeriesId { get; set; }

  public ICollection<Movie> SeenMovies { get; set; }

  public ICollection<Movie> UnSeenMovies { get; set; }

  public ICollection<Episode> SeenEpisodes { get; set; }

  public ICollection<Episode> UnSeenEpisodes { get; set; }
}
