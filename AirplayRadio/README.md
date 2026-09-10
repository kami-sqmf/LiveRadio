# 📡 Airplay Radio

[English](README.md) · [繁體中文](README.zh-TW.md) · [User guide](USAGE.md) · [Release preparation](docs/release-readiness.md)

Play audio from your iPhone or iPad through the native **Cities: Skylines II** radio panel. Airplay Radio receives AirPlay audio inside the game process and adds its own station alongside Live Radio and the built-in stations.

**Version 0.3.1 is published on [Paradox Mods](https://mods.paradoxplaza.com/mods/158648/Windows), ID 158648.** See the [release record](docs/release-readiness.md) for the verified listing and outstanding checks.

## ✨ Features

- Stream phone audio to the game's Radio mixer.
- See the title, artist and circular artwork when supplied by the phone.
- Use previous, next and play/pause when the sender provides compatible remote controls. Unsupported senders retain local pause.
- Boost quiet sources with **0 to +12 dB audio gain**, with smooth changes and stereo-linked peak limiting.
- Keep using the game's local radio volume and mute controls.
- English and Traditional Chinese interface, following the game language. Other languages fall back to English.
- No companion EXE, Bonjour service, GStreamer installation or separately installed FFmpeg executable. The bundled native receiver includes its audio decoders.

## 🚀 Getting started

1. Enable **Airplay Radio** and [ExtendedRadio (75862)](https://mods.paradoxplaza.com/mods/75862/Windows) in the same playset.
2. Load a city and select the **Airplay Radio** station in the native radio panel.
3. Connect the phone and PC to the same trusted local network.
4. On the phone, open the **AirPlay audio output** selector and choose `Airplay Radio - your computer name`. This is audio output, not screen mirroring.
5. Start playback on the phone. For quiet audio, try **Options → Airplay Radio → Audio gain (dB)** at +6 dB.

Switching to another station or disabling the receiver stops reception. Select Airplay Radio again and reconnect from the phone to return. Closing the radio panel itself does not switch stations.

See the [user guide](USAGE.md) for troubleshooting and diagnostic counters.

## 🎛️ Controls and settings

| Setting | Default | What it does |
| --- | --- | --- |
| Enable receiver | On | Runs the receiver while Airplay Radio is selected in a city. |
| Restart receiver | Button | Restarts reception; reconnect from the phone afterward. |
| Phone media controls | On | Allows previous, next and play/pause through a compatible sender. Turn off to force local pause. |
| Audio gain (dB) | 0 | Adds up to +12 dB locally. High gain can reduce musical dynamics through peak limiting. |
| Show media artwork | On | Displays phone-supplied JPEG/PNG covers, or the station icon when absent. |
| Receiver status | Read-only | Shows playback metadata and remote-control availability. |
| Audio diagnostics | Read-only | Shows periodically sampled buffer and transport counters. |

Phone and game volume are independent; their sliders do not synchronize. Gain cannot restore a signal muted by the phone. Remote availability and playback-state reporting vary by sender. A successful command response is distinct from support for continuous state polling.

## Phone-call output

Choose call output on the iPhone or in the calling app. During a Phone call, tap Audio and choose the phone's own receiver or speaker; see [Apple's call audio guide](https://support.apple.com/guide/iphone/iph3c9951d7/ios).

Airplay Radio cannot change the phone's call output and has no reliable call-state or call-audio classification. If an app sends call audio in the received AirPlay stream, the mod cannot separate it from media audio. Automatic blocking of all call audio is therefore not guaranteed. Phone and third-party calling apps need separate real-device checks. Disable the receiver to stop reception immediately when needed.

## 📦 Requirements and scope

Windows x64 and ExtendedRadio are required. Recorded game tests use **Cities: Skylines II 1.6.0f1**; other game versions are unverified. Live Radio is optional and is not a dependency. No phone companion app is required for compatible iPhone/iPad AirPlay audio output. Android native casting is outside this version's supported scope.

This is an experimental audio receiver, not a complete AirPlay 2 implementation. It does not provide video mirroring, multi-room synchronization, a playlist/next-track queue, or universal DRM compatibility. It uses at least 500 ms of prebuffering and does not yet schedule playback against the sender's NTP clock or correct long-term clock drift. Brief buffering events remain possible.

Use a trusted local network. The receiver has no user pairing PIN, and the upstream native protocol core has not had a complete security review for this mod. Do not forward its ports to the public Internet. The mod does not create firewall rules automatically.

## 🖼️ Artwork and local data

Artwork comes from the sender, without an online image search. The receiver accepts JPEG/PNG artwork up to 2 MiB and 4096 pixels per side, then creates a circular 192 px thumbnail in a background worker. The network icon stays the approved broadcast symbol.

Game user data contains `ModsData/AirplayRadio/device-id.txt`, `receiver.key`, and up to eight processed images under `Artwork/`. Logs are written to `Logs/AirplayRadio.log`. These device files, artwork caches, personal logs and savegames are excluded from the release package.

## 📚 Documentation

English is the primary documentation language. Traditional Chinese translations are available from the language links.

| Document | Contents |
| --- | --- |
| [User guide](USAGE.md) | Connection, playback controls, call output and troubleshooting. |
| [Changelog](CHANGELOG.md) | Changes by release. |
| [Validation](VALIDATION.md) | Test evidence and remaining real-device checks. |
| [Release preparation](docs/release-readiness.md) | Package contents, publisher drafts and release checklist. |
| [English store description](docs/paradox-description.en.md) | Primary listing copy; [Traditional Chinese](docs/paradox-description.zh-TW.md) is also available. |
| [Store images](release-assets/README.md) | Inspected screenshot provenance and usage. |
| [Third-party notices](THIRD-PARTY-NOTICES.md) | Source revisions, dependencies and licenses. |
| [Icon history](assets/GENERATION.md) | Artwork generation and revisions. |

## 🧱 How it works

- Audio is received and decoded to 44.1 kHz stereo floating-point PCM, then played through Unity's Radio mixer. The ring buffer holds up to two seconds and prebuffers at least 500 ms, or one requested audio block when larger. Overflow drops the oldest samples; the audio callback does not wait for the decoder lock.
- Metadata is displayed only when supplied by the sender. Artwork is decoded and rendered in a background worker, with bounded input and cache sizes.
- Remote controls use the DACP identity and credentials from the actual RTSP peer. Service discovery and bounded HTTP requests run in the background. Track commands are not automatically retried. Changing stations, stopping reception or replacing the connection cancels the old remote session.
- Station changes, receiver shutdown and mod disposal stop networking in the background before native resource cleanup. Emergency broadcasts mute the receiver output.

## 🛠️ Build, package and validation

Copy the containing repository's `Local.props.example` to `Local.props` and set `GameManagedPath` and `ExtendedRadioPath`, with the .NET SDK installed. The native toolchain uses MSYS2 UCRT64: GCC, CMake, Ninja, pkgconf, OpenSSL and libplist, plus MSYS make and diffutils. Run the following from the containing workspace:

```powershell
# First build: fetch FFmpeg n8.0.1, pinned to commit
# 894da5ca7d742e4429ffb2af534fcda0103ef593.
git clone --depth 1 --branch n8.0.1 https://github.com/FFmpeg/FFmpeg.git .work/airplay-ffmpeg

./AirplayRadio/scripts/build.ps1
./AirplayRadio/scripts/test.ps1

# Close the game before installing the local build.
./AirplayRadio/scripts/install-local.ps1

# Prepare release files and local publisher drafts.
./AirplayRadio/scripts/package-release.ps1
./AirplayRadio/scripts/prepare-publish.ps1
```

For subsequent builds, `build.ps1 -SkipCodecs` reuses the already-built minimal FFmpeg libraries. `scripts/build-codecs.sh` checks the exact source revision and builds only the required static decoders and resampler. It does not produce a FFmpeg executable. The native import check rejects non-system runtime DLL dependencies.

Build output is in `artifacts/AirplayRadio`, including the managed/native DLLs, icon, debug symbols, documentation and licenses. The release ZIP excludes debug symbols and personal data. `install-local.ps1` installs locally; it does not publish to Paradox Mods. The default `PublishConfiguration.xml` uses English listing text; separate English and Traditional Chinese drafts are also provided.

The package script uses an explicit runtime allowlist, includes licenses and documentation, and records SHA-256 hashes. Publishing is a separate action. Automated tests do not replace real-device and in-game acceptance.

## 🤖 Authorship and license

The game integration, receiver bridge and local modifications were developed using Codex, an AI coding agent. The station artwork was developed with AI assistance. The bundled upstream code retains its original authorship and licenses.

`AirplayRadioNative.dll` is built from third-party source as well as this mod's bridge:

- [UxPlay](https://github.com/FDH2/UxPlay): AirPlay/RAOP protocol core, including contributions inherited from RPiPlay, ShairPlay, AirplayServer and PlayFair.
- [UxPlayEnhanced](https://github.com/Kylepossible/UxPlayEnhanced): embedded mDNS responder.
- [FFmpeg](https://github.com/FFmpeg/FFmpeg): statically linked ALAC/AAC/PCM decoders and audio resampling.
- OpenSSL, libplist, llhttp and MinGW/GCC runtime components: cryptography, property-list and HTTP parsing, threading and runtime support.

`AirplayRadio.dll` contains the managed game integration, including adapter patterns from the parent Live Radio project. The locally implemented DACP client used AirPlay protocol documentation and Shairport Sync as protocol references. Pinned source revisions, individual licenses and local modifications are recorded in [Third-party notices](THIRD-PARTY-NOTICES.md).

Airplay Radio uses **GPL-3.0-or-later**, with third-party notices included. The parent Live Radio project's MIT license is unchanged. No claim of Apple certification or affiliation is made.

This directory contains the mod sources, vendored receiver, build scripts and retained notices. Complete corresponding-source materials for covered dependencies and GPL integration review remain pending; see the [publication review](docs/publication-review.md).
