# Publication review — 2026-09-10

[Store assets](../release-assets/README.md) · [Third-party notices](../THIRD-PARTY-NOTICES.md)

**Airplay Radio 0.3.1 was published publicly on [Paradox Mods](https://mods.paradoxplaza.com/mods/158648/Windows), ID 158648, on 2026-09-10.** The official Mod Publisher returned success, and the listing was independently opened while logged out: version 0.3.1, suggested game version 1.6.*, ExtendedRadio dependency 75862, and the cropped panel screenshot were confirmed.

Publication does not resolve the licensing and complete corresponding-source issues below. This is a source and architecture assessment, not a legal opinion from the rights holders.

## Panel crop and publication attempt

The user requested cropping the store images to the radio panel and then publishing Airplay Radio. The inspected lossless crop is `release-assets/radio-panel.en.png` (1520 × 1156). Both localized publisher configurations reference only this screenshot, with English as the primary listing language, version 0.3.1, game version 1.6.*, and ExtendedRadio dependency 75862.

Automatic approval review initially requested explicit confirmation of the destination and the specific 0.3.1 package with its unresolved GPL integration and corresponding-source review. After the user's explicit confirmation, the official publisher completed successfully and assigned ID 158648. The runtime binaries were not changed for this task.

The local ZIP representing the submitted content has SHA-256 `83b0b3f812fca6722971f03d4ecb8ad9ea12d42875060709de24467ac5b5667e`. Later documentation updates in Git are not claimed to be present in that already-published ZIP. Publisher drafts now preserve ID 158648 for future updates; do not publish a duplicate new listing.

## Evidence from this build

- `native/CMakeLists.txt` compiles the vendored UxPlay sources and PlayFair into a static core, then links that core into AirplayRadioNative.dll.
- `native/vendor/uxplay-lib/crypto.c` explicitly carries GPL version 3 or later, with upstream copyright notices. PlayFair also includes a GPLv3 license. The compiled native source is not entirely LGPL.
- `src/ReceiverSession.cs` loads the native DLL into the game's process and resolves its entry points. The managed adapter calls those entry points to exchange audio and receiver state while using the proprietary game's APIs.
- No exception authorizing this integration with Cities: Skylines II was found in the inspected bundled notices. We cannot grant an exception on behalf of upstream copyright holders.

The FSF explains that dynamically linked plug-ins which exchange calls and data structures can form a combined program. Its answer on GPL plug-ins for a nonfree host says such a combination needs suitable permission, such as a linking exception. Applying that guidance to this architecture leaves an unresolved distribution issue; publishing the mod's source does not by itself settle it. This assessment is an inference from the source and the FSF's interpretation, not a court ruling about this mod.

References: [GNU FAQ: plug-in boundaries](https://www.gnu.org/licenses/gpl-faq.en.html#GPLPlugins), [GNU FAQ: GPL plug-ins for nonfree programs](https://www.gnu.org/licenses/gpl-faq.en.html#GPLPluginsInNF), [GPLv3 sections 1, 5 and 6](https://opensource.org/license/gpl-3.0).

## Source materials

`scripts/package-source-review.ps1` prepares a local review snapshot containing the mod's source, vendored receiver, tests, documentation, build scripts, a Local.props example and a pinned FFmpeg source archive. It records hashes of the snapshot files and of the existing DLLs, and records installed MSYS2 package versions. It excludes private settings, Git metadata, game DLLs, receiver keys, logs and runtime artwork caches.

The FFmpeg checkout is clean at `894da5ca7d742e4429ffb2af534fcda0103ef593`. Its missing empty `.git/refs` directory was restored during this review so Git resolves the FFmpeg repository rather than falling back to the parent Live Radio repository; no source or commit pointer was changed.

This review snapshot is **not a complete corresponding-source release**. Exact OpenSSL/libplist package source and build recipes/patches still need collecting, and the applicable source obligations or exceptions for each statically linked runtime must be checked. A clean build against those collected dependencies and final source-access instructions are also pending. The existing managed/native automatic test results do not establish license compliance.

## Ways to resolve the publication issue

1. Obtain applicable permission from the relevant copyright holders, or a qualified licensing assessment establishing a valid distribution basis for this specific integration.
2. Replace the affected core with an implementation whose license permits the intended in-process use, then rebuild and repeat the device tests. Availability of a suitable replacement has not been established.
3. Consider a genuinely separate receiver program with an appropriate communication boundary and compliant source distribution. This changes the user's requirement for a mod-only implementation and has not been implemented or approved.

The current implementation remains mod-only. No runtime architecture has been changed. The published mod is Public, ID 158648; the distribution questions above remain unresolved.
