# User guide

[Home](../README.md) · Read the [known risks](security.md) before enabling this version.

## Listening

1. Enable ExtendedRadio and install Live Radio, then restart the game.
2. Open the native radio panel in your city. **Game stations** is the home page.
3. Select **Live Radio** to open **Favorites**. Use **Explore** to search Radio Browser by station name and country/region. **All regions** searches worldwide.
4. Use a station's **Play** or **Favorites** button. Saving a favorite does not start playback. Keep up to 50 favorites and 20 recently played stations.
5. Use the original radio controls to pause, mute and change volume. Closing the panel keeps playback running. Pause disconnects; resume returns to the current live broadcast.

Explore loads up to 50 rows at a time, with a 1,000-station limit per search. Startup and opening Favorites do not request a station list. Opening the radio panel may fetch missing artwork metadata for saved stations and cache their icons. Explorer artwork is also downloaded and validated in the background. Only locally cached images are displayed. Missing or failed images use the station’s initial.

The interface follows the **game's language**: English is the default and fallback; Traditional Chinese is available for `zh-HANT`, `zh-TW` and `zh-HK`. Station names and broadcaster-supplied metadata are preserved. The settings entry is **Live Radio**; the internal ID and existing data paths remain `LiveRadio`.

## Add a station URL

Select **Live Radio → Add station**, enter a name and a direct HTTP(S) audio or `.m3u8` URL, then **Save to favorites**. A station website is not a stream URL. URLs with embedded usernames/passwords, fragments, or non-HTTP protocols are rejected. Query parameters are preserved, so expiring signed URLs must be replaced when they expire.

**Auto** recognizes `.mp3`, `.aac`, `.ogg`, `.opus` and `.m3u8`; other URLs are detected by FFmpeg. For an extensionless direct MP3 URL without FFmpeg, choose **MP3** explicitly. Manual stations stay on your computer and are not submitted to Radio Browser. Removing a favorite offers Undo for 10 seconds.

## FFmpeg setup

| Stream | Without FFmpeg | With FFmpeg |
| --- | --- | --- |
| Direct MP3 | Included NLayer decoder | Same included decoder |
| AAC / HE-AAC | Can be saved; setup guidance | Plays |
| OGG Vorbis / Opus | Can be saved; setup guidance | Plays |
| HLS (`.m3u8`) | Can be saved; setup guidance | Plays supported audio tracks |
| Auto, unknown extension | Needs FFmpeg or an explicit format | Detects supported formats |

1. Open the [FFmpeg download page](https://ffmpeg.org/download.html#build-windows) and choose a Windows build linked there. FFmpeg itself distributes source code; Windows binaries are built by other providers.
2. Extract it to a permanent folder. A system-wide installation is not required.
3. In **Options → Live Radio → Audio decoder**, set the full path to the real `bin/ffmpeg.exe`, for example `C:/Tools/ffmpeg/bin/ffmpeg.exe`. Do not select a launcher or Chocolatey shim.
4. Return to the browser and select **Check again**, then **Play**. If already listening, pause and resume after changing the path.

Leaving the path empty searches the real Chocolatey installation and the current process's PATH. PATH changes after game startup may require a restart; setting the full path avoids that. Live Radio does not download, install or update FFmpeg automatically. Current checks used the existing FFmpeg 8.1 gyan.dev essentials build; other builds need the relevant demuxers, codecs and network protocols.

HLS/Auto prebuffers six seconds and holds up to twenty seconds of PCM, which can increase live latency. Pausing disconnects; resuming reconnects to the current broadcast. DRM, login/cookies, custom authentication headers, webpage scraping, arbitrary playlists and video are unsupported. MP3 supports ICY titles; other formats show station name and playback state.

MP3 uses the bundled NLayer decoder, not the game’s native decoder. Continuous native Vorbis/Opus decoding remains unverified. See [development](development.md) and [validation](validation.md) for implementation and test details.

## Local data and network use

Favorites, custom URLs, recent stations and directory cache are stored under `%USERPROFILE%/AppData/LocalLow/Colossal Order/Cities Skylines II/ModsData/LiveRadio/stations.json`. Existing favorites are retained. Older data is backed up during migration; unreadable data is preserved. Artwork is cached in the neighboring `Icons` folder. These files are not included in release packages or city save data.

Radio Browser receives search terms, station metadata requests and a station-selection counter for its directory entries. The selected station receives audio requests; artwork hosts receive icon requests. Custom stations do not send selection counters to Radio Browser. Audio uses a bounded memory buffer and is not recorded to disk. Signed stream URLs are stored locally in plain text; omit your station data when sharing diagnostics.
