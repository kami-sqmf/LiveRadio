# 📻 Live Radio

Listen to internet radio in the native Cities: Skylines II radio panel.

## ✨ Features

- 🔍 Search Radio Browser by station name and country/region.
- ⭐ Save up to 50 favorites and keep 20 recently played stations.
- ➕ Add custom HTTP(S) audio or HLS stream URLs; saving does not start playback.
- 🎛️ Use the game's play, pause, mute and volume controls.
- 🖼️ Station artwork with initial icons when unavailable.
- 🌐 English and Traditional Chinese, following the game's language.

## 📦 Requirements

Windows; game checks used Cities: Skylines II **1.6.0f1**. Other game versions are unverified.

Enable **ExtendedRadio (Paradox Mods ID 75862)** in the same playset:
https://mods.paradoxplaza.com/mods/75862/Windows

**Direct MP3 uses the bundled NLayer decoder. AAC/HE-AAC, OGG Vorbis/Opus, HLS and unknown-format Auto URLs need separately installed FFmpeg.** FFmpeg is not bundled or downloaded automatically. Set its real executable path under Options → Live Radio → Audio decoder.

## ⚠️ Limitations and known risks

Station availability and geographic restrictions depend on the broadcaster. HLS buffering can increase live delay. DRM, login/cookies, custom authentication headers and station webpage URLs are unsupported. Pausing disconnects; resuming returns to live audio. Closing the panel keeps playback running.

Favorites and custom URLs stay on your computer in plain text. Directory, stream and artwork hosts receive their respective requests. Opening the panel may fetch saved-station artwork. Custom stations are not submitted to Radio Browser.

Unresolved risks: station/artwork URLs and HLS nested resources can make requests to local/private-network services; frequent MP3 format changes can exhaust resources; crafted station names can insert misleading diagnostic lines. Existing URL restrictions and bounded audio buffers do not resolve these issues. Read the full disclosure before enabling the mod:
https://github.com/kami-sqmf/LiveRadio/blob/main/docs/security.md

**Open items in 0.4.4.** The following are not completed for this release:
- 🧪 Clean environment test and error handling: only Live Radio and ExtendedRadio enabled, tested with FFmpeg missing, no network, dead stations and the dependency missing.
- 🔇 Audio cleanup: emergency broadcasts, switching back to game stations, and no leftover stream or process after quitting the game.

## 🔧 Maintenance and source

**Further updates are unlikely. You are welcome to fork the MIT-licensed source and continue development yourself.** Fixes, support responses and future game compatibility are not promised.

Source and forks: https://github.com/kami-sqmf/LiveRadio
Issue reports (no response guarantee): https://github.com/kami-sqmf/LiveRadio/issues

## 🤖 AI disclosure

**All code in this project was developed using Codex, an AI coding agent.** Third-party components retain their own authorship and licenses.

## 🆕 Version 0.4.4

Corrected station icon positioning, verified in Favorites and Explore. Includes local artwork caching, MP3 reconnect-buffer preservation, format-change handling and the FFmpeg queue contention fix from earlier versions. Longer listening and remaining game checks are documented in the repository; not all scenarios have been validated.

Author: kami-sqmf. Source license: MIT. Third-party notices are included in the package.
