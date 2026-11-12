using System;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.MediaTracker.Model;

namespace Jellyfin.Plugin.MediaTracker.Helpers;

internal static class UserHelper
{
  public static ServerUser GetServerUser(string userId)
  {
    return GetServerUser(Guid.Parse(userId));
  }

  public static ServerUser GetServerUser(User user)
  {
    return GetServerUser(user.Id);
  }

  public static ServerUser GetServerUser(Guid userGuid)
  {
    var users = Plugin.Instance.PluginConfiguration.GetAllServerUsers();
    if (users.Count == 0)
    {
      return null;
    }

    return users.FirstOrDefault(user =>
    {
      if (user.LinkedMbUserId == Guid.Empty || string.IsNullOrWhiteSpace(user.ApiToken) || string.IsNullOrWhiteSpace(user.ApiServerUrl))
      {
        return false;
      }

      if (user.LinkedMbUserId.Equals(userGuid))
      {
        return true;
      }

      return false;
    });
  }
}
