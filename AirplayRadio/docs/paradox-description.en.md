# 📡 Airplay Radio

Bring your iPhone or iPad audio into the native Cities: Skylines II radio panel.

## ✨ Features

- 📱 Receive AirPlay audio directly inside the game. No companion program to launch.
- 🎵 Display the current title and artist when supplied by the sender.
- 🖼️ Show phone-supplied cover art as a circular station thumbnail.
- ⏯️ Control previous, next and play/pause on compatible senders.
- 🔊 Add 0 to +12 dB of local audio gain for quiet sources, with peak limiting.
- 🎛️ Use the game's radio volume and mute controls.
- 🌐 English and Traditional Chinese, following the game's language.

## 🚀 Start listening

1. Enable Airplay Radio and ExtendedRadio in the same playset.
2. Load a city and select the Airplay Radio station.
3. Put the phone and PC on the same trusted local network.
4. Choose “Airplay Radio - your computer name” from the phone's AirPlay audio output menu and start playback.

For quiet sources, try Options → Airplay Radio → Audio gain (dB) at +6 dB. High gain may reduce musical dynamics through peak limiting.

## 📦 Requirements

Windows x64. Tested game version: Cities: Skylines II **1.6.0f1**; other versions are unverified.

Requires **ExtendedRadio (ID 75862)** in the same playset:
https://mods.paradoxplaza.com/mods/75862/Windows

Live Radio is optional. No separate FFmpeg executable, Bonjour service or GStreamer installation is required; the receiver and decoders are bundled inside this mod.

## ℹ️ Compatibility and limitations

This is an experimental AirPlay audio receiver. It does not provide screen mirroring, multi-room synchronization, playlist/next-track information or complete AirPlay 2 support. Android native casting and all DRM sources are not guaranteed to work.

Artwork, metadata, remote commands and playback-state reporting depend on the phone app. Unsupported remote senders use local pause. Phone and game volume controls remain independent. Switching stations or disabling the receiver disconnects it; reconnect from the phone when returning.

Call output is controlled on the phone or in the calling app. The mod cannot change the phone's call output or reliably filter call audio if the sender includes it in the stream.

Playback uses buffering and may have brief interruptions. Full sender-clock scheduling and long-term clock drift correction are not implemented. Current tests do not establish compatibility with every phone or network.

Use only a trusted local network: the receiver has no user pairing PIN, and the native protocol core has not undergone a complete security review for this mod. Do not forward its ports to the Internet. The mod does not automatically change firewall rules.

Device identity and a small processed-artwork cache stay in the game's local user-data directory. Artwork is received from the phone, without online image searches.

## 🤖 AI disclosure and license

The game integration, receiver bridge and local modifications were developed using Codex, an AI coding agent, with AI-assisted station artwork. Bundled upstream code retains its original authorship and licenses.

## 📚 Open-source credits

The native receiver incorporates source from [UxPlay](https://github.com/FDH2/UxPlay), including its inherited RPiPlay, ShairPlay, AirplayServer and PlayFair contributions. Its embedded mDNS responder comes from [UxPlayEnhanced](https://github.com/Kylepossible/UxPlayEnhanced), and audio decoding/resampling uses statically linked [FFmpeg](https://github.com/FFmpeg/FFmpeg) libraries.

It also includes OpenSSL, libplist, llhttp and MinGW/GCC runtime components. The game adapter builds on Live Radio's integration patterns. The DACP client is locally implemented with AirPlay documentation and Shairport Sync as protocol references. Exact source revisions, local modifications and component licenses are listed in the bundled THIRD-PARTY-NOTICES.md and license files.

Author: kami-sqmf. Airplay Radio is GPL-3.0-or-later; bundled component notices are included. It is an independent mod, with no claim of Apple certification or affiliation.

Mod source, build instructions and the outstanding publication review: [Airplay Radio source](https://github.com/kami-sqmf/LiveRadio/tree/main/AirplayRadio).

## 🆕 Version 0.3.1

Adds separate English and Traditional Chinese settings and playback messages. Includes phone controls, circular sender artwork, local audio gain and buffer diagnostics from 0.3.0. Real-device and automated validation are recorded in the included documentation.
