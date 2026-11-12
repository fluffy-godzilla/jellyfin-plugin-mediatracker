#pragma warning disable CA1819 // Properties should not return arrays

using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.MediaTracker.Model;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MediaTracker.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
  /// <summary>
  /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
  /// </summary>
  public PluginConfiguration()
  {
    ServerUsers = [];
  }

  /// <summary>
  /// Gets or sets the server users.
  /// </summary>
  public ServerUser[] ServerUsers { get; set; }

  /// <summary>
  /// Adds a user to the server users.
  /// </summary>
  /// <param name="userGuid">The user Guid.</param>
  public void AddUser(Guid userGuid)
  {
    var users = ServerUsers.ToList();
    var user = new ServerUser
    {
      LinkedMbUserId = userGuid
    };
    users.Add(user);
    ServerUsers = [.. users];
  }

  /// <summary>
  /// Removes a user from the server users.
  /// </summary>
  /// <param name="userGuid">The user id.</param>
  public void RemoveUser(Guid userGuid)
  {
    var users = ServerUsers.ToList();
    users.RemoveAll(user => user.LinkedMbUserId == userGuid);
    ServerUsers = [.. users];
  }

  /// <summary>
  /// Gets a list of all server users.
  /// </summary>
  /// <returns>IReadonlyList{ServerUser} with all server users.</returns>
  public IReadOnlyList<ServerUser> GetAllServerUsers()
  {
    return [.. ServerUsers];
  }
}
