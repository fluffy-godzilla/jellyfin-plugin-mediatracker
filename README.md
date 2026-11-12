# jellyfin-plugin-mediatracker
[Jellyfin](https://github.com/jellyfin/jellyfin) plugin for [MediaTracker](https://github.com/fluffy-godzilla/MediaTracker)

### This is a fork of [Bonukai's MediaTracker Jellyfin Plugin](https://github.com/bonukai/jellyfin-plugin-mediatracker)

## Features
- allows per user configuration
- scrobbles seen/unseen state and progress from jellyfin to mediatracker server
- imports progress and seen/unseen state from mediatracker server into jellyfin (useful when upgrading a jellyfin server with new software is not an option and you decided to just configure a new instance)


## Requirements
[fluffy-godzilla/mediatracker](https://github.com/fluffy-godzilla/mediatracker) server version: `>=0.2.17`

## Installation
- Add new Repository in Jellyfin (Dashboard -> Plugins -> Repositories -> +) from url
```
https://raw.githubusercontent.com/fluffy-godzilla/jellyfin-plugin-mediatracker/main/manifest.json
```
- Install MediaTracker plugin from Catalogue (Dashboard -> Plugins -> Catalogue) 

## Configuration

Set your MediaTracker instance url and API key in the plugin settings.
