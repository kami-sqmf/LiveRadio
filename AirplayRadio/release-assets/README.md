# Airplay Radio store images

`thumbnail.png` is the approved square broadcast artwork. It is the single raster icon copy retained in Git; the duplicate `assets/station.png` stays local and is ignored. The game uses the circular vector `assets/station.svg`.

The user supplied two real 3840 × 2160 game captures on 2026-09-10. Full originals are retained locally as `radio.en.png` and `settings.en.png`, and ignored by Git. A source checkout contains only the final thumbnail, cropped panel and this record.

For publication, `radio-panel.en.png` is a lossless crop of `radio.en.png`: x = 2224, y = 792, width = 1520, height = 1156, using zero-based source coordinates. It includes the full radio panel and bottom playback controls. No UI, metadata or artwork was generated, resized or retouched. The crop was visually inspected.

Only `radio-panel.en.png` is listed as a screenshot in the publisher configuration. The settings page and full game captures are retained locally and excluded from the listing. Both language configurations use the English crop until an inspected Traditional Chinese panel capture is available.

| File | SHA-256 |
| --- | --- |
| `radio.en.png` | `3bd5f47e0a2ead695ae894a69f423a3aa1667c388b289cd752309c12e0d303e2` |
| `settings.en.png` | `362dfda0ee3ca20aa67fa0b27fcc39b98d976a83eb8df38937107ccbb0858816` |
| `radio-panel.en.png` | `ef44447f88e77e04f3df3e7a6c9649331e41bf59cfb3c005d05d414ccf0138bb` |

The screenshots establish visible layout and artwork, not control delivery, audio stability or gain persistence. Sender metadata is preserved exactly. Store image files are publisher assets, excluded from the runtime ZIP.
