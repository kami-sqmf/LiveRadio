# Changelog

[Main documentation](README.md) · [繁體中文](CHANGELOG.zh-TW.md)

## 0.3.1 — 2026-09-10

- English and Traditional Chinese settings and playback messages follow the game language, with English fallback. Existing notices update when the language changes.
- Settings grouped into Receiver, Playback and Status.
- Prepare bilingual listing text, inspected English screenshots, a runtime allowlist package and SHA-256 manifest.
- Make English the primary language for the README, user guide, release checklist and default publisher draft; retain Traditional Chinese translations.
- Document phone-call output limitations. No automatic call filtering or phone-routing control was added.

## 0.3.0 — 2026-09-10

- Add local 0 to +12 dB gain, smooth adjustments and stereo-linked peak limiting.
- Receive phone JPEG/PNG artwork and render bounded, cached circular thumbnails in the background.

## 0.2.0 — 2026-09-10

- Add sender DACP discovery and previous/next/play/pause controls.
- Separate metadata and audio locks, increase prebuffer reserve and log audio-health counters.

## 0.1.x — 2026-09-09–10

- Initial in-process receiver, native station registration, executable-asset path fix, live program metadata and station artwork.
