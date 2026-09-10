# Airplay Radio user guide

[Main documentation](README.md) · [繁體中文](USAGE.zh-TW.md)

## First connection

1. Enable Airplay Radio and ExtendedRadio (75862) in the same playset.
2. Load a city, open the native radio panel and select the Airplay Radio network and station.
3. Connect the iPhone/iPad and PC to the same trusted local network.
4. Open the phone's AirPlay **audio output** selector, choose `Airplay Radio - your computer name`, and start playback. Use audio output, not screen mirroring.

Switching stations or disabling the receiver disconnects it. Select Airplay Radio again and reconnect from the phone to return.

## Volume and gain

Game radio volume and mute control the PC output. Phone volume controls the incoming audio; the two interfaces do not synchronize.

For quiet sources, open **Options → Airplay Radio → Playback → Audio gain (dB)**:

- 0 dB: original level and the default.
- +6 dB: approximately twice the signal amplitude.
- +12 dB: approximately four times the amplitude; this is the maximum.

Changes take effect immediately without reconnecting. The peak limiter controls amplified peaks; high gain may reduce musical dynamics. Local gain cannot restore a signal muted by the phone.

## Media controls and artwork

When the sender provides compatible controls, the game's previous, next and play/pause buttons send commands to the phone. Basic remote senders may not report playback state continuously. Without a remote service, pause is local and the phone keeps playing.

**Show media artwork** is enabled by default. The phone app decides whether to supply and update artwork; the mod does not search online for images. Missing artwork falls back to the broadcast icon. The network icon on the left stays unchanged.

## Keeping call audio on the phone

Call output is controlled by the iPhone or calling app. During a Phone call, tap **Audio** and select the phone's own receiver or speaker; see [Apple's call audio guide](https://support.apple.com/guide/iphone/iph3c9951d7/ios). For other calling apps, use their audio output selector.

Airplay Radio cannot change the phone's call output and has no reliable call-state or call-audio classification. If an app sends call audio in the same AirPlay stream, the mod cannot separate it from media audio. Automatic blocking of all call audio is not guaranteed; Phone and third-party calling apps require separate real-device checks. Turn off **Enable receiver** to stop reception immediately when needed.

## Interface language

Settings, playback messages and known receiver statuses follow the game's language. Traditional Chinese uses Chinese; English and other untranslated languages use English. Titles, artists and device names retain the sender's original text.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| Receiver not visible on the phone | Load a city and select Airplay Radio. Check that both devices share a local network and are not isolated from each other. |
| Station missing | Enable ExtendedRadio and Airplay Radio in the same playset. Restart the game and inspect the mod log. |
| Connected but silent | Start playback on the phone, unmute both ends and raise radio volume. Gain cannot fix a source that is muted. |
| Cannot change tracks | Check remote-control status. The sender may not provide a compatible remote service. |
| No artwork | Enable Show media artwork. The sender may not supply JPEG/PNG artwork, or the image may exceed the supported limits. |
| Brief interruptions | Compare their timing with log entries for track changes, pauses, resend requests and buffer starvation. A cumulative counter alone cannot establish a network fault. |

Use **Restart receiver** after a network change or stalled connection, then reconnect from the phone.

## Diagnostics and local data

During playback, statistics are logged every 15 seconds to `Logs/AirplayRadio.log` under the game's user-data directory. `underruns` counts buffer starvation, `resendPackets` includes repeated resend requests, `decodeErrors` counts decoder errors, `gainDb` is local gain and `limitedFrames` counts frames processed by peak limiting.

Settings and receiver data stay in the game's user-data directory. `ModsData/AirplayRadio` contains device identity, a receiver key and a small artwork cache. Exclude these personal files from release packages. Check device and media information before sharing logs.

See [VALIDATION.md](VALIDATION.md) for test evidence and limitations.
