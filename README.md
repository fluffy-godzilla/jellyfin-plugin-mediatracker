# jellyfin-plugin-mediatracker
[Jellyfin](https://github.com/jellyfin/jellyfin) plugin for [MediaTracker](https://github.com/fluffy-godzilla/MediaTracker)

### This is my fork of https://github.com/bonukai/jellyfin-plugin-mediatracker

## Requirements

Minimum MediaTracker version: `0.1.0`

## Features
- per user configuration
- progress and seen scrobbler
- ability to import seen state from mediatracker into jellyfin (useful when upgrading a jellyfin server with new software is not an option and you decided to just install a new one)

## Installation
- Add new Repository in Jellyfin (Dashboard -> Plugins -> Repositories -> +) from url
```
https://raw.githubusercontent.com/fluffy-godzilla/jellyfin-plugin-mediatracker/server-import/manifest.json
```
- Install MediaTracker plugin from Catalogue (Dashboard -> Plugins -> Catalogue) 

## Configuration

Set your MediaTracker instance url and API keys in the plugin settings
