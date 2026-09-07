# Live Radio

Internet radio in the native Cities: Skylines II radio panel. **0.4.4 release — not published on Paradox Mods.** [繁體中文](README.zh-TW.md)

Search Radio Browser by name and region, save up to 50 favorites, keep 20 recent stations, or add your own HTTP(S) stream URL. Play, pause, mute and volume use the game's controls. The interface follows the game language: English or Traditional Chinese, with English fallback.

## Requirements and quick start

1. Use Cities: Skylines II on Windows. Current game checks used **1.6.0f1**; other versions are unverified.
2. Enable [ExtendedRadio](https://mods.paradoxplaza.com/mods/75862/Windows) (ID **75862**) in the same playset.
3. Read the [known risks](docs/security.md). This version can request local/private-network services, exhaust resources through MP3 format changes, and write misleading diagnostic lines. These issues remain unresolved.
4. For a local package, build and run the [installation steps](docs/development.md) with the game closed. Restart the game and open its radio panel → **Live Radio** → **Favorites** or **Explore**.
5. Direct MP3 works with the bundled NLayer decoder. AAC/HE-AAC, OGG Vorbis/Opus, HLS and unknown-format Auto URLs need separately installed [FFmpeg](https://ffmpeg.org/download.html#build-windows). Set the real executable under **Options → Live Radio → Audio decoder**, then select **Check again**. See the [setup guide](docs/usage.md).

Saving a favorite does not play it. Closing the panel keeps playback running. Pause disconnects; resume returns to live audio. Custom stations stay local. Opening the panel may fetch missing artwork; saved URLs are plain text.

## Documentation

| Document | Purpose |
| --- | --- |
| [User guide](docs/usage.md) | Search, custom URLs, format support, FFmpeg setup and local data |
| [Known risks / 已知風險](docs/security.md) | Unresolved network, decoder and diagnostic risks |
| [Development / 開發指南](docs/development.md) | Build, install, architecture and native decoder investigation |
| [Validation / 驗證紀錄](docs/validation.md) | Evidence, limitations and remaining game checks |
| [Release preparation / 發布準備](docs/release-readiness.zh-TW.md) | GitHub and Paradox Mods checklist |
| [Paradox listing draft](docs/paradox-description.en.md) | English listing text, dependency and limitations |
| [Changelog](CHANGELOG.md) | Version changes |
| [Third-party notices](THIRD-PARTY-NOTICES.md) | Dependency notices and [Unicode license](licenses/UNICODE-LICENSE.txt) |

## Maintenance and license

**All code in this project was developed using Codex.** Third-party components retain their own authorship and licenses.

**Further updates are unlikely. You are welcome to fork this project and continue development yourself.** Fixes, support responses and compatibility with future game updates are not promised.

Source: [kami-sqmf/LiveRadio](https://github.com/kami-sqmf/LiveRadio). Issues can be recorded on [GitHub](https://github.com/kami-sqmf/LiveRadio/issues), without a response commitment. Licensed under [MIT](LICENSE); retain the license and [third-party notices](THIRD-PARTY-NOTICES.md) in redistributed copies.
