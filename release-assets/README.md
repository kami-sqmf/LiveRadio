# Release images

The screenshots were recaptured with the English game interface on 2026-09-07. Station names retain their original languages. Use these English captures for the listing instead of the earlier Chinese captures.

| Image | Contents |
| --- | --- |
| [Favorites](favorites.en.jpg) | Six favorites, station icons and HLS playback |
| [Explore](explore.en.jpg) | Country selection and station results |
| [Recent](recent.en.jpg) | Recent stations and playback controls |
| [Add station](add-station.en.jpg) | Manual station form and supported formats |
| [Native channel program](program.en.jpg) | Native Game stations panel with Listen.moe Kpop · LiveRadio selected and its Channel Program shown |
| [Thumbnail](thumbnail.png) | Original Live Radio title artwork |

The browser screenshots are cropped to the radio menu and playback controls (864 × 661). The native Channel Program screenshot is cropped to the station list and program pane (868 × 583). Both exclude the map and surrounding game interface. Their contents have not been redrawn. They show Live Radio 0.4.4, normal build, in Cities: Skylines II 1.6.0f1. Other mods were enabled in this playset; these images are not evidence of a clean-playset test. Platform image requirements still need to be checked at upload time.

The settings screenshot was removed at the author's request. Original full-window captures are backed up locally under `.work/release-assets-before-crop` and are not included in the current release assets.

During this session, Hit FM HLS ran from 20:17:44 to 20:21:15 (Asia/Taipei), using FFmpeg and the Radio mixer at 48 kHz stereo. Logs reported zero underruns, dropped data and lock misses. The initial 76,800 silent samples did not increase during that segment. Opening the pause menu released the stream and audio output; returning to the game reconnected. The native pause button also changed the UI to paused. This is runtime evidence, not a listening-quality assessment.

The Add station screenshot shows the empty form only. Automated text injection did not populate its field; full IME, manual add/remove/undo and WASD acceptance remain unverified. The full release checklist remains in [release readiness](../docs/release-readiness.zh-TW.md).

