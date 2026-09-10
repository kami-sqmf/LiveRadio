# Airplay Radio prototype validation

Publication update: version 0.3.1 was published publicly on 2026-09-10 as [Paradox Mods ID 158648](https://mods.paradoxplaza.com/mods/158648/Windows). The logged-out page confirmed the version, dependency and cropped screenshot. Historical build/device evidence below is unchanged; publication adds no new runtime acceptance.

## Store crop — 2026-09-10

The publication screenshot is now a lossless 1520 × 1156 crop of the supplied radio capture, retaining the complete radio panel and its transport controls. Both publisher configurations reference only `radio-panel.en.png`; the original radio and settings captures are retained locally. See `release-assets/README.md` for crop coordinates and hashes. This asset change does not change binaries or establish additional device validation. Publication status is recorded separately in `docs/publication-review.md`.

## 0.3.1 — release preparation and localization, 2026-09-10

Documentation now defaults to English: README, user guide, changelog and release checklist. Traditional Chinese copies use .zh-TW.md, and the older README.en.md path redirects readers to the main README. Complete English build instructions replace the former dependency on the Chinese guide. The default local PublishConfiguration.xml is a copy of the English draft. This documentation change does not change game binaries or runtime language selection.

Settings now use per-locale entries, matching Live Radio's English fallback and Traditional Chinese locale aliases (zh-HANT, zh-TW, zh-HK). Playback and remote-control messages retain localized templates; known native receiver status messages are translated in the managed layer. Sender metadata and diagnostic field names are not translated. Settings are grouped into Receiver, Playback and Status.

Release build succeeded with zero warnings/errors. Both native checks and the managed checks passed, including language switching without rebuilding messages, locale fallback and literal braces in sender-provided text. Existing gain, artwork, remote HTTP and receiver-lifetime regression checks passed. Version 0.3.1 was installed with the game closed and its managed DLL hash checked.

The user subsequently supplied two full-resolution English screenshots on 2026-09-10. Both were inspected and copied unchanged into release-assets; source and destination SHA-256 hashes match. They show an actual sender-provided title and circular cover, the circular broadcast network icon, localized English settings, +2 dB gain and unavailable playback-state polling. They support the visible English layout and artwork display, not successful controls, gain persistence, live locale switching or long-term stability. Traditional Chinese layout acceptance remains pending. Both localized publisher drafts use these English captures until inspected Chinese captures are supplied. Listing text and local publisher XML are preparation only; this version was not uploaded to Paradox Mods.


## 0.3.0 — local gain and sender artwork, 2026-09-10

Adds a persistent 0 to +12 dB settings slider (default 0), applied to AirPlay PCM before the game Radio mixer. Target changes are smoothed over about 10 ms. A stereo-linked peak limiter has immediate attack and about 100 ms release; the default path preserves samples exactly. Game and phone volume remain independent. The audio callback performs no gain-related allocations or blocking locks.

The receiver now exposes versioned, bounded cover data from the existing JPEG/PNG callback. Image/none and empty-image events clear artwork in order on the RTP worker; replacing queued cover data frees the old allocation. Background .NET code checks input type, size and dimensions, center-crops and renders antialiased circular 192 px PNG thumbnails, and maintains an eight-file content-addressed cache. Only the station icon changes; the network icon remains the approved SVG. UI application checks the receiver instance and native artwork revision before accepting a worker result. It does not download artwork from other services or add a helper process.

Automated checks passed:

- Both native test executables, including real RTSP image/none, cover byte copying, size bounds, stale-version rejection and clear handling. Holding the cover mutex does not block native audio output.
- Gain amplitude at +6/+12 dB, gradual changes, clamped settings inputs, full-scale bounds, stereo balance, silence preservation, limiter telemetry, and exact bypass after returning to 0 dB. A float-state convergence issue found by the return-to-zero regression was fixed with double-precision filter state.
- Actual .NET PNG/JPEG decoding and thumbnail rendering: expected dimensions, source color, transparent corners and partially transparent antialiased edges; content-addressed reuse, cache bounds and preservation of unrelated files; rejection of invalid data, oversized dimensions and oversized byte payloads.
- Managed/native artwork access for empty, stale and disposed sessions, plus existing DLL loading, UTF-8 paths, concurrent audio/disposal, remote-control and decoder checks.
- Release managed/native build and package native-import checks passed. The initial managed restore emitted NU1900 because NuGet vulnerability metadata could not be reached in the restricted environment; compilation succeeded. No new NuGet package was added.

Still requires the next in-game/device test: gain slider persistence and perceived loudness, limiter sound on actual music, artwork supplied by this user's phone/player, native panel/transport refresh on track changes, show-artwork toggle, and station-switch/reconnection fallback. Automated image rendering and native callbacks do not prove that every phone app sends a thumbnail. Earlier 0.2.0 user acceptance remains recorded below.

## User follow-up and log review — 2026-09-10, version 0.2.0

The user reports that the functions work normally. The reviewed log snapshot covers audio health samples from 17:12:01.570 through 17:17:46.974 (22 samples). This is a short real-device test, not a long soak test.

- All 14 logged controls received HTTP 200: next 5, previous 3, pause 3, play 3. Together with the user's report, this supports successful remote operation on this sender.
- The first discovery attempt failed; a retry found the service. Playback-state polling did not succeed. This does not by itself establish that the sender never supports polling; a transient query failure also produces that status. Phone-side actions may therefore not update the game pause display.
- Latest cumulative audio counters: 5 underruns, 1 native ring contention, 121 requested resend packets (including repeated requests), 0 decoder errors, 0 overflow-dropped samples, 0 managed lifecycle read misses. Sampled queue depth was 390–877 ms; these periodic samples do not show the instantaneous minimum.
- The first underruns/resends occurred during repeated pause/play/track changes. Later underruns increased without additional logged game remote commands or resend requests. Phone-side actions are not logged, so these events cannot be conclusively attributed to network loss or to uninterrupted-playback stutters.
- A 5744 ms main-thread update gap appears in the 17:16:16 sample. SceneFlow.log records autosave from 17:16:04.638 to 17:16:12.460, which overlaps that sampling interval. The underrun counter was still 2 in the 17:16:16 sample and rose to 3 in the next sample; the data does not prove that autosave caused an audio interruption.
- The maximum input gap of 5234 ms is cumulative and spans explicit pause/resume operations; it is not evidence of a 5-second network outage.
- No warning/error entries appeared in the captured AirplayRadio.log; LiveRadio.log showed normal initialization and panel activity.

Conclusion: remote control is working on the tested setup, and no managed metadata/audio lock misses were recorded. Audio is not completely free of buffer/transport events. More event-level timing or a steady playback run without phone/game controls would be needed to isolate the remaining underruns. Snapshot retained locally at `.work/airplay-log-review/2026-09-10-0.2.0.log` (not part of the distributed package).

## 0.2.0 — phone controls and intermittent-audio investigation

The user reports occasional brief stutters on 0.1.2. Its log records startup only, so there is no evidence yet to attribute the observed events to network loss. Inspection found that metadata reads and Unity audio callbacks share an exclusive managed lock; the callback emits silence when that lock is held. Version 0.2.0 uses concurrent lifecycle readers and a separate text buffer lock, increases prebuffering from 200 to 500 ms (or one requested audio block, whichever is larger), shortens native ring critical sections, and adds diagnostics. These address plausible software causes; real-device improvement is not yet verified. Full RTP/NTP clock scheduling and drift correction remain outside this patch.

Remote control captures the DACP ID, token and actual RTSP peer in one snapshot, clears it only when that connection closes, discovers the matching service on the peer, and sends bounded HTTP requests from a separate background worker. It hooks native previous/next/play/pause only for Airplay Radio. Local volume/mute are unchanged; unsupported remote discovery retains local pause, and rejected controls are reported. Basic senders may not expose playback-state polling. Successful HTTP acknowledgement does not independently prove that a real phone app acted on a command.

Automated checks passed on Windows x64:

- Actual native RTSP OPTIONS captures peer-bound remote identity; stale connection closure cannot clear a newer identity; malformed remote header values are rejected.
- DNS compressed SRV matching, wrong-device rejection, truncated records and compression cycles; bounded DMAP play-state parsing.
- Fake phone over real loopback HTTP: authentication header, next/pause/play, rejection of previous, no redirect following, single delivery, cancellation of a stalled request without retry.
- Audio callbacks run while a metadata lifecycle reader is held, plus existing concurrent read/disposal tests.
- Synthetic burst delivery every 200 ms sustains 20 seconds of consumed audio with no underruns; real starvation and ring contention increment separate counters. This is a deterministic buffer simulation, not a real-time Wi-Fi soak test.
- Existing PCM/ALAC decoder, AAC initialization, DLL loading, UTF-8 key, resource cleanup and package import checks.

Manual acceptance still required: real iPhone/iPad DACP discovery and commands; game Harmony integration, pause-state display, switching back to Live Radio, circular SVG appearance, and stutter comparison using the new diagnostics. No claim is made that the user's stutters are fixed or that their current sender supports DACP. The circular SVG prepared for 0.1.3 is included in 0.2.0.

## 0.1.3 — circular station icon

The station and network now reference a circular SVG with transparent corners, keeping the approved mint broadcast tower and concentric arcs. The SVG uses geometric paths instead of scaling the large raster image down into the native panel. In-game edge appearance still requires visual verification at the user's display scale.

## 0.1.1 — 2026-09-10 initialization fix

The first game run loaded the mod settings but failed before station registration:
`ArgumentException: Invalid path` at `Path.GetDirectoryName` in `Mod.Tick`.
The game loads mod assemblies from memory, so `Assembly.Location` is not a usable installation path.
The fix resolves the receiver through `ModManager.TryGetExecutableAsset(this, out asset)` and `asset.path`.
The restart button also retries initialization when no controller exists.
Regression coverage loads a managed assembly from bytes (empty Location), resolves the packaged native DLL via the executable asset path, and rejects an empty asset path with a descriptive error.
The subsequent user test confirmed registration and playback; see below.

## User device test — 2026-09-10, version 0.1.1

The user reported that playback works, titles are displayed, and local pause works.
Two supplied screenshots show the Airplay Radio station in the native panel, a media title in the transport bar, and a `Receiving audio` status with artist/device metadata in settings. The game version shown is 1.6.0f1 (428.e384). Live Radio's panel tabs are present concurrently.

This confirms the initial installation-path fix, in-game native receiver loading, station visibility, and user-reported end-to-end playback/title/local-pause behavior on this setup. Audio and pause behavior are user-reported, not independently measurable from the screenshots. Phone model, OS, sender app and negotiated codec were not recorded.

Follow-up feedback: switching stations and reconnecting works, and extended playback was reported stable (duration not recorded). Phone volume/mute changes affect audio but do not update the game controls; game volume does not update the phone. These are separate gain stages, with no bidirectional volume synchronization implemented. Previous/next do not control the phone. The program panel still showed its initial title while the transport bar updated.

## 0.1.2 — program metadata and artwork

The station schedule now receives the same title/status as the transport bar and triggers the program binding when changed. A new generated station icon is packaged and served through a dedicated UI host. These changes require in-game visual verification. Phone previous/next and bidirectional volume remain unimplemented.

Local run: 2026-09-09, Windows x64. Target game references: Cities: Skylines II 1.6.0.0, ExtendedRadio 75862_21. Mod target: .NET Framework 4.8.

## Passed

- Native DLL compiled with GCC 15.2.0, minimal FFmpeg n8.0.1, static OpenSSL/libplist/winpthreads.
- Managed mod compiled against the installed game assemblies: zero errors and warnings.
- Runtime package imports checked with objdump: only Windows system DLLs; no external FFmpeg, GStreamer, Bonjour or helper EXE.
- Native receiver: empty buffer returns zero-filled audio, three start/RTSP OPTIONS/stop cycles return HTTP/RTSP 200.
- Decoder: real compressed ALAC fixture produces nonzero PCM; PCM stereo channel order and amplitude verified; AAC-LC and AAC-ELD decoder initialization succeeds.
- Source-volume conversion, two-second queue bound/drop behavior, nonblocking read under producer contention, flush discarding stale audio.
- Nested DMAP song metadata parsing and rejection of truncated/oversized lengths.
- Packaged DLL loaded from C# through LoadLibraryEx/GetProcAddress in .NET Framework 4.8, without MSYS2 on PATH.
- Three managed start/stop cycles, simultaneous reads/disposal, repeated disposal and UTF-8 key file persistence.

The native upstream build emits compiler warnings in optional HLS code and FFmpeg AAC/SBR code. No full upstream security review has been performed. HLS/video support is not advertised or implemented by the bridge.

## Still requires real devices / game

- Discovery, pairing and streaming across additional iPhone/iPad, OS and sender-app combinations; the initial user setup works.
- Actual AAC packet playback (only initialization tested); ALAC test uses a synthetic compressed fixture.
- Game-side volume/mute acoustic behavior, emergency broadcasts and broader playback coexistence with Live Radio. Station switching/reconnection passed the user's initial test.
- Wi-Fi/firewall combinations, reconnection after sleep, multi-output routing and measured long-duration clock drift/latency. Extended playback was reported stable on the user's setup, without a recorded duration.

No claim is made that the above manual checks passed. The package is a local prototype, not a validated release.

## Suggested first device test

1. Close the game and run scripts/install-local.ps1. Enable ExtendedRadio and Airplay Radio.
2. Load a city; select Airplay Radio. In Options, confirm the receiver is waiting.
3. On the same LAN, choose `Airplay Radio - computer-name` from the iPhone's audio output selector and start music.
4. Check sound, station volume/mute, title and artist when supplied by the phone.
5. Pause/resume locally; switch to Live Radio or a built-in station and back. Reconnect the phone when returning.
6. Inspect Logs/AirplayRadio.log if initialization fails. Do not infer success merely from the station being visible.
