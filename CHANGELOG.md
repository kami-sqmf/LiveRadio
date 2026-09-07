# 📝 Changelog

[🏠 Home](README.md) · [🧪 Validation](docs/validation.md)

---

## 🚀 0.4.4 — release

- Set explicit station icon size and position; Favorites and Explore icons verified in Gameface.
- Consolidate documentation, disclose existing risks in both languages, and prepare publication materials. Playback code is unchanged by documentation preparation.

## 🔖 0.4.3

- Display validated local icon cache files instead of remote images in Gameface.
- Preserve queued MP3 samples during reconnect bursts; detect sample-rate/channel changes and rebuild output.
- Show a red Remove action for favorites.

## 🔖 0.4.2

- Remove producer-lock contention from FFmpeg PCM reads, correcting the 0.4.1 silence regression.
- Add buffer diagnostics and continuity checks.

## 🔖 0.4.1

- Add bounded waiting for HLS bursts, six-second prebuffering and twenty-second capacity.
- Fix native focus registration for multiple input controls.
- Known regression: audio callback lock contention; corrected in 0.4.2.

## 🔖 0.4.0

- Add English/Traditional Chinese localization, manual stations, HLS, artwork and FFmpeg guidance.

## 🔖 0.3.0–0.3.1

- Add the native station browser, Favorites/Explore/Recent, pagination and BCC aliases.
- Correct native input events, focus barriers, checkboxes and panel layout.

## 🔖 0.2.0

- Add FFmpeg AAC/Vorbis/Opus playback and directory integration alongside MP3 playback.
