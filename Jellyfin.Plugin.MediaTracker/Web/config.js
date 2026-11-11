const MediaTrackerConfigPage = {
  pluginUniqueId: '4e47221f-3a04-42ab-90c6-d458d6509b30',
  loadConfiguration: function (userId, page) {
    ApiClient.getPluginConfiguration(MediaTrackerConfigPage.pluginUniqueId).then(function (config) {
      let currentUserConfig = config.ServerUsers.filter(function (curr) {
        return curr.LinkedMbUserId == userId;
        //return true;
      })[0];
      // User doesn't have a config, so create a default one.
      if (!currentUserConfig) {
        // You don't have to put every property in here, just the ones the UI is expecting (below)
        currentUserConfig = {
          ApiServerUrl: null,
          ApiToken: null,
          SkipUnwatchedImportFromServer: true,
          SkipWatchedImportFromServer: false,
          SkipPlaybackProgressImportFromServer: false,
          PostWatchedHistory: true,
          PostUnwatchedHistory: false,
          PostSetWatched: true,
          PostSetUnwatched: false,
          ExtraLogging: false,
          Scrobble: true,
        };
      }
      page.querySelector('#apiServerUrl').value = currentUserConfig.ApiServerUrl;
      page.querySelector('#apiToken').value = currentUserConfig.ApiToken;
      page.querySelector('#chkSkipUnwatchedImportFromServer').checked = currentUserConfig.SkipUnwatchedImportFromServer;
      page.querySelector('#chkSkipWatchedImportFromServer').checked = currentUserConfig.SkipWatchedImportFromServer;
      page.querySelector('#chkSkipPlaybackProgressImportFromServer').checked = currentUserConfig.SkipPlaybackProgressImportFromServer;
      page.querySelector('#chkPostWatchedHistory').checked = currentUserConfig.PostWatchedHistory;
      page.querySelector('#chkPostUnwatchedHistory').checked = currentUserConfig.PostUnwatchedHistory;
      page.querySelector('#chkPostSetWatched').checked = currentUserConfig.PostSetWatched;
      page.querySelector('#chkPostSetUnwatched').checked = currentUserConfig.PostSetUnwatched;
      page.querySelector('#chkExtraLogging').checked = currentUserConfig.ExtraLogging;
      page.querySelector('#chkScrobble').checked = currentUserConfig.Scrobble;
      Dashboard.hideLoadingMsg();
    });
  },
  populateUsers: function (users) {
    let html = '';
    for (let i = 0, length = users.length; i < length; i++) {
      const user = users[i];
      html += '<option value="' + user.Id + '">' + user.Name + '</option>';
    }
    document.querySelector('#selectUser').innerHTML = html;
  }
};


function save(page) {
  return new Promise((resolve) => {
    const currentUserId = page.querySelector('#selectUser').value;
    ApiClient.getPluginConfiguration(MediaTrackerConfigPage.pluginUniqueId).then(function (config) {
      let currentUserConfig = config.ServerUsers.filter(function (curr) {
        return curr.LinkedMbUserId == currentUserId;
      })[0];
      // User doesn't have a config, so create a default one.
      if (!currentUserConfig) {
        currentUserConfig = {};
        config.ServerUsers.push(currentUserConfig);
      }
      currentUserConfig.ApiServerUrl = page.querySelector('#apiServerUrl').value;
      currentUserConfig.ApiToken = page.querySelector('#apiToken').value;
      currentUserConfig.SkipUnwatchedImportFromServer = page.querySelector('#chkSkipUnwatchedImportFromServer').checked;
      currentUserConfig.SkipWatchedImportFromServer = page.querySelector('#chkSkipWatchedImportFromServer').checked;
      currentUserConfig.SkipPlaybackProgressImportFromServer = page.querySelector('#chkSkipPlaybackProgressImportFromServer').checked;
      currentUserConfig.PostWatchedHistory = page.querySelector('#chkPostWatchedHistory').checked;
      currentUserConfig.PostUnwatchedHistory = page.querySelector('#chkPostUnwatchedHistory').checked;
      currentUserConfig.PostSetWatched = page.querySelector('#chkPostSetWatched').checked;
      currentUserConfig.PostSetUnwatched = page.querySelector('#chkPostSetUnwatched').checked;
      currentUserConfig.ExtraLogging = page.querySelector('#chkExtraLogging').checked;
      currentUserConfig.Scrobble = page.querySelector('#chkScrobble').checked;
      currentUserConfig.LinkedMbUserId = currentUserId;
      if (currentUserConfig.UserName == '') {
        config.ServerUsers.remove(config.ServerUsers.indexOf(currentUserConfig));
      }
      ApiClient.updatePluginConfiguration(MediaTrackerConfigPage.pluginUniqueId, config).then(function (result) {
        Dashboard.processPluginConfigurationUpdateResult(result);
        ApiClient.getUsers().then(function (users) {
          const currentUserId = page.querySelector('#selectUser').value;
          MediaTrackerConfigPage.populateUsers(users);
          page.querySelector('#selectUser').value = currentUserId;
          MediaTrackerConfigPage.loadConfiguration(currentUserId, page);
          resolve();
        });
      });
    });
  });
}

export default function (view) {
  view.querySelector('#selectUser').addEventListener('change', function () {
    MediaTrackerConfigPage.loadConfiguration(this.value, view);
  });

  view.querySelector('#mediaTrackerConfigurationForm').addEventListener('submit', function (e) {
    save(view);
    e.preventDefault();
    return false;
  });

  view.addEventListener('viewshow', function () {
    const page = this;
    ApiClient.getUsers().then(function (users) {
      MediaTrackerConfigPage.populateUsers(users);
      const currentUserId = page.querySelector('#selectUser').value;
      MediaTrackerConfigPage.loadConfiguration(currentUserId, page);
    });
  });
}
