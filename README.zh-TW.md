# 📻 Live Radio

[![版本](https://img.shields.io/badge/版本-0.4.4-1f6feb?style=flat-square)](CHANGELOG.md)
[![授權](https://img.shields.io/badge/授權-MIT-2da44e?style=flat-square)](LICENSE)
[![遊戲](https://img.shields.io/badge/Cities%3A%20Skylines%20II-1.6.0f1-bf5af2?style=flat-square)](https://www.paradoxinteractive.com/games/cities-skylines-ii)
[![相依](https://img.shields.io/badge/需要-ExtendedRadio%2075862-fb8500?style=flat-square)](https://mods.paradoxplaza.com/mods/75862/Windows)
[![Docs](https://img.shields.io/badge/docs-English-0a7ea4?style=flat-square)](README.md)

在《Cities: Skylines II》原生電台面板收聽網路廣播。**0.4.4 已發布至 [Paradox Mods](https://mods.paradoxplaza.com/mods/158298/Windows)**（ID 158298）。 [English](README.md)

此儲存庫也包含獨立的手機音訊接收模組 [Airplay Radio](AirplayRadio/README.zh-TW.md)，**0.3.1 已發布至 [Paradox Mods](https://mods.paradoxplaza.com/mods/158648/Windows)**。原始碼位於 `AirplayRadio/`，使用自己的 GPL-3.0-or-later 授權及第三方聲明；Live Radio 維持 MIT。

依台名與國家／地區搜尋 Radio Browser，最多收藏 50 台、保留 20 台最近紀錄，也可新增 HTTP(S) 串流直連網址。播放、暫停、靜音與音量沿用遊戲控制。介面跟隨遊戲語言，提供英文與繁體中文，其餘語言使用英文。

---

## 🚀 需求與快速開始

1. 使用 Windows 版 Cities: Skylines II。目前實機紀錄來自 **1.6.0f1**，其他版本未驗證。
2. 在同一播放集啟用 [ExtendedRadio](https://mods.paradoxplaza.com/mods/75862/Windows)（ID **75862**）。
3. 啟用前閱讀 [已知風險](docs/security.md)：來源可使電腦請求本機／內網服務，頻繁 MP3 格式切換可能耗盡資源，外部台名也可能插入誤導日誌。這些問題尚未修正。
4. 本機版本請依 [開發指南](docs/development.md) 建置，在遊戲關閉時安裝。重啟後開啟原生電台面板 → **Live Radio** →「收藏」或「探索」。
5. **MP3** 使用內附 NLayer，不需要 FFmpeg。**AAC／HE-AAC、OGG／Vorbis／Opus、HLS 及未知格式的自動偵測**需要另行安裝 [FFmpeg](https://ffmpeg.org/download.html#build-windows)。在「選項 → Live Radio → 音訊解碼器」填入真正的執行檔路徑，例如 `C:/Tools/ffmpeg/bin/ffmpeg.exe`，再按「重新偵測」。模組不會自行下載或更新 FFmpeg。

收藏不會自動播放；關閉面板後繼續收聽。暫停會中斷連線，恢復時接回當下直播。新增電台須填音訊或 .m3u8 直連，不能填電台網頁；無副檔名的 MP3 可手動選 MP3。移除收藏後有 10 秒可復原。

啟動與開啟收藏不查詢電台清單，但開啟面板可能補抓圖標。自訂電台不提交到 Radio Browser，收藏網址以明文儲存；分享診斷時請移除私人網址及權杖。HLS／自動模式預先緩衝 6 秒、容量上限 20 秒，直播延遲可能增加。不支援 DRM、登入、Cookie 或自訂驗證標頭。

---

## 📚 文件導覽

| 文件 | 內容 |
| --- | --- |
| 🎧 [完整使用指南（英文）](docs/usage.md) | 搜尋、自訂網址、格式、FFmpeg 與本機資料 |
| ⚠️ [已知風險（中英對照）](docs/security.md) | 尚未修正的網路、解碼與日誌問題 |
| 🛠️ [開發指南](docs/development.md) | 建置、安裝、架構、UI 決策與原生解碼調查 |
| 🧪 [驗證紀錄](docs/validation.md) | 測試證據、適用範圍與待驗收項目 |
| 🚀 [發布準備](docs/release-readiness.zh-TW.md) | GitHub 與 Paradox Mods 發布清單 |
| 🛍️ [Paradox 商店草稿](docs/paradox-description.en.md) | 英文介紹、相依與限制 |
| 📝 [版本變更](CHANGELOG.md) | 各版重點 |
| 📄 [第三方聲明](THIRD-PARTY-NOTICES.md) | 相依授權與 [Unicode 授權](licenses/UNICODE-LICENSE.txt) |

---

## 🤖 AI 使用聲明

> [!NOTE]
> **本專案所有程式碼皆使用 AI 編碼代理 Codex 開發。** 第三方元件保留各自的作者與授權聲明。已驗證與未驗證的範圍記錄於[驗證紀錄](docs/validation.md)與[已知風險](docs/security.md)。

---

## ⚖️ 維護狀態與授權

**大概率不會繼續更新，歡迎 Fork 自行接續開發。** 不承諾後續修正、支援回覆或新遊戲版本相容性。

原始碼：[kami-sqmf/LiveRadio](https://github.com/kami-sqmf/LiveRadio)。可在 [GitHub Issues](https://github.com/kami-sqmf/LiveRadio/issues) 留下紀錄，但不保證回覆。採用 [MIT License](LICENSE)，再散布時請保留授權及 [第三方聲明](THIRD-PARTY-NOTICES.md)。
