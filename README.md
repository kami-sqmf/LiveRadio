# 📻 Live Radio

[![Version](https://img.shields.io/badge/version-0.4.4-1f6feb?style=flat-square)](CHANGELOG.md)
[![License](https://img.shields.io/badge/license-MIT-2da44e?style=flat-square)](LICENSE)
[![Game](https://img.shields.io/badge/Cities%3A%20Skylines%20II-1.6.0f1-bf5af2?style=flat-square)](https://www.paradoxinteractive.com/games/cities-skylines-ii)
[![Requires](https://img.shields.io/badge/requires-ExtendedRadio%2075862-fb8500?style=flat-square)](https://mods.paradoxplaza.com/mods/75862/Windows)
[![Docs](https://img.shields.io/badge/docs-繁體中文-0a7ea4?style=flat-square)](README.zh-TW.md)

Internet radio in the native Cities: Skylines II radio panel. **0.4.4 published on [Paradox Mods](https://mods.paradoxplaza.com/mods/158298/Windows)** (ID 158298). [繁體中文](README.zh-TW.md)

Search Radio Browser by name and region, save up to 50 favorites, keep 20 recent stations, or add your own HTTP(S) stream URL. Play, pause, mute and volume use the game's controls. The interface follows the game language: English or Traditional Chinese, with English fallback.

---

## 🚀 Requirements and quick start

1. Use Cities: Skylines II on Windows. Current game checks used **1.6.0f1**; other versions are unverified.
2. Enable [ExtendedRadio](https://mods.paradoxplaza.com/mods/75862/Windows) (ID **75862**) in the same playset.
3. Read the [known risks](docs/security.md). This version can request local/private-network services, exhaust resources through MP3 format changes, and write misleading diagnostic lines. These issues remain unresolved.
4. For a local package, build and run the [installation steps](docs/development.md) with the game closed. Restart the game and open its radio panel → **Live Radio** → **Favorites** or **Explore**.
5. Direct MP3 works with the bundled NLayer decoder. AAC/HE-AAC, OGG Vorbis/Opus, HLS and unknown-format Auto URLs need separately installed [FFmpeg](https://ffmpeg.org/download.html#build-windows). Set the real executable under **Options → Live Radio → Audio decoder**, then select **Check again**. See the [setup guide](docs/usage.md).

Saving a favorite does not play it. Closing the panel keeps playback running. Pause disconnects; resume returns to live audio. Custom stations stay local. Opening the panel may fetch missing artwork; saved URLs are plain text.

---

## 📚 Documentation

| Document | Purpose |
| --- | --- |
| 🎧 [User guide](docs/usage.md) | Search, custom URLs, format support, FFmpeg setup and local data |
| ⚠️ [Known risks / 已知風險](docs/security.md) | Unresolved network, decoder and diagnostic risks |
| 🛠️ [Development / 開發指南](docs/development.md) | Build, install, architecture and native decoder investigation |
| 🧪 [Validation / 驗證紀錄](docs/validation.md) | Evidence, limitations and remaining game checks |
| 🚀 [Release preparation / 發布準備](docs/release-readiness.zh-TW.md) | GitHub and Paradox Mods checklist |
| 🛍️ [Paradox listing draft](docs/paradox-description.en.md) | English listing text, dependency and limitations |
| 📝 [Changelog](CHANGELOG.md) | Version changes |
| 📄 [Third-party notices](THIRD-PARTY-NOTICES.md) | Dependency notices and [Unicode license](licenses/UNICODE-LICENSE.txt) |

---

## 🤖 AI disclosure

> [!NOTE]
> **All code in this project was developed using Codex, an AI coding agent.** Third-party components retain their own authorship and licenses. What has and has not been checked is recorded in [validation](docs/validation.md) and [known risks](docs/security.md).

---

## ⚖️ Maintenance and license

**Further updates are unlikely. You are welcome to fork this project and continue development yourself.** Fixes, support responses and compatibility with future game updates are not promised.

Source: [kami-sqmf/LiveRadio](https://github.com/kami-sqmf/LiveRadio). Issues can be recorded on [GitHub](https://github.com/kami-sqmf/LiveRadio/issues), without a response commitment. Licensed under [MIT](LICENSE); retain the license and [third-party notices](THIRD-PARTY-NOTICES.md) in redistributed copies.
