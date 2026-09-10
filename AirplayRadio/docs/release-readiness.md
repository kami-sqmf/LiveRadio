# Airplay Radio 0.3.1 release record

[Main documentation](../README.md) · [繁體中文](release-readiness.zh-TW.md)

Published on 2026-09-10: [Airplay Radio on Paradox Mods](https://mods.paradoxplaza.com/mods/158648/Windows), ID **158648**, Public, version **0.3.1**, Windows x64, suggested game version **1.6.***, required dependency **ExtendedRadio 75862**. The public page and cropped screenshot were verified after the official publisher reported success.

The English listing uses the approved thumbnail and only `release-assets/radio-panel.en.png` (1520 × 1156). [Store images](../release-assets/README.md) records provenance and hashes. The full original captures remain local and are ignored by Git.

Run `scripts/package-release.ps1` from an existing build to prepare the runtime ZIP. Only the managed DLL, native DLL, station SVG, documentation and licenses are included. Run `scripts/prepare-publish.ps1` to prepare local publisher configurations for the existing listing; it does not upload. Generated configurations, ZIPs, debug symbols and caches are ignored by Git.

The repository retains source, vendored components and licenses, build/package scripts, tests with the synthetic ALAC fixture, documentation, the game SVG, and final store assets. The duplicate raster icon is ignored. Copy the repository's `Local.props.example` to `Local.props` and fill in your own game/dependency paths before building; the local file is ignored. FFmpeg source is fetched separately at the pinned revision documented in the README.

Existing native and managed validation is recorded in [VALIDATION.md](../VALIDATION.md). The crop/publication task did not rebuild runtime binaries or establish additional device acceptance. Language switching, broader sender/network coverage and the other documented manual checks remain pending.

The [publication and license review](publication-review.md) records remaining GPL integration and complete corresponding-source issues. Publishing repository source does not by itself complete those outstanding checks.
