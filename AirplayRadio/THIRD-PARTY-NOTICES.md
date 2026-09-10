# Airplay Radio third-party sources

Airplay Radio prototype code: GPL-3.0-or-later (see LICENSE). The existing Live Radio project remains MIT.

## Binary composition

`AirplayRadioNative.dll` compiles the vendored UxPlay protocol sources, PlayFair, llhttp and the UxPlayEnhanced embedded mDNS implementation with this mod's `native/receiver.cpp` bridge. It statically links the minimal FFmpeg avcodec/swresample/avutil libraries, OpenSSL libcrypto, libplist, winpthreads and compiler runtime components, as specified in `native/CMakeLists.txt`. These are incorporated upstream implementations, not solely design references.

`AirplayRadio.dll` contains the managed game integration, settings, audio lifecycle, gain, artwork and DACP client. Adapter patterns were reused from the parent Live Radio project. Game/Unity assemblies and ExtendedRadio are runtime dependencies supplied separately, not included as DLLs in this release ZIP. The .NET reference-assemblies NuGet package is a build dependency.

The AI development disclosure applies to the integration and local changes; it does not claim authorship of the bundled upstream sources.

## Sources and licenses

| Component | Source / pinned version | License |
| --- | --- | --- |
| UxPlay protocol core, with RPiPlay, ShairPlay, AirplayServer and PlayFair contributions | https://github.com/FDH2/UxPlay commit 5de48c3d7ba07de396639dd3704de908b649f37f | GPL-3.0-or-later aggregate; individual LGPL-2.1-or-later and other notices retained in sources |
| Embedded mDNS responder | https://github.com/Kylepossible/UxPlayEnhanced commit 77b88a7b05c67c7d4a388d2fe8fe15c8078b564d, src/dnssd_embedded.c | LGPL-2.1-or-later, per source header |
| llhttp | bundled UxPlay lib/llhttp | MIT; licenses/llhttp-MIT.txt |
| FFmpeg avcodec, swresample, avutil | https://github.com/FFmpeg/FFmpeg commit 894da5ca7d742e4429ffb2af534fcda0103ef593 (n8.0.1) | LGPL-2.1-or-later in this minimal configuration |
| OpenSSL | MSYS2 UCRT64 3.6.1, https://github.com/openssl/openssl | Apache-2.0; licenses/OpenSSL.txt |
| libplist | MSYS2 UCRT64 2.7.0, https://github.com/libimobiledevice/libplist | LGPL-2.1-or-later |
| winpthreads / MinGW runtime | MSYS2 UCRT64, https://www.mingw-w64.org/ | licenses/winpthreads.txt |
| GCC runtime | GCC 15.2.0 | GPL with GCC Runtime Library Exception; licenses/GCC-Runtime-Exception.txt |
| ExtendedRadio adapter pattern and Unity radio integration | parent Live Radio repository | MIT; licenses/LiveRadio-MIT.txt |

Vendored sources reside in native/vendor. The UxPlay README preserves upstream contributor attribution.
Local changes to upstream code in prototype releases through 0.3.1 (2026-09-10): raop.c tolerates cleanup after partial initialization and exports DACP identity together with the actual RTSP peer/connection lifetime; raop.h exposes the added bridge callbacks; raop_rtp.c reports resend-request counts, bounds queued cover data, frees replaced cover allocations and delivers ordered empty-cover events; raop_handlers.h handles image/none and limits artwork bodies; crypto.c opens identity key paths as UTF-8 on Windows. The custom CMake configuration replaces dnssd.c with the embedded responder, excludes UxPlay's executable/renderers, and links a minimal static FFmpeg decoder directly to the receiver bridge.

The managed DACP client is a local implementation using .NET Framework networking. Protocol references: https://nto.github.io/AirPlay.html#audio-remotecontrol and https://github.com/mikebrady/shairport-sync/blob/master/dacp.c (command paths, service discovery, and playback-state values). No Shairport Sync binary, Bonjour service, or new third-party runtime library is bundled for remote control.

The receiver bridge does not execute GStreamer, FFmpeg, UxPlay or another process. Build/test tools are development-only and are not shipped as runtime executables.

## Distribution status

The runtime ZIP includes notices and license texts, but is not a complete corresponding-source distribution. Before public binary distribution, prepare the source matching the released binaries, local changes, required build materials and covered dependency sources, and provide clear source-access instructions alongside the download. Upstream project links alone do not provide this mod's modified corresponding source. See GPLv3 sections 1, 5 and 6 in LICENSE.

The release checklist also tracks review of license compatibility for the in-process integration with the proprietary game and separately supplied dependencies. Attribution and a GPL label alone do not establish permission to distribute the combined integration.

The synthesized 440 Hz tone.alac fixture contains no third-party recording. It was generated with:

```text
ffmpeg -f lavfi -i sine=frequency=440:sample_rate=44100:duration=1 -ac 2 -c:a alac -frames:a 1 -map 0:a -f data tone.alac
```
