# 📱 Airplay Radio

[English](README.md) · [繁體中文](README.zh-TW.md) · [上架準備](docs/release-readiness.zh-TW.md)

**0.3.1 已公開發布至 [Paradox Mods](https://mods.paradoxplaza.com/mods/158648/Windows)，模組 ID 158648。** 已核對商店頁面，發布與未完成事項見[發布紀錄](docs/release-readiness.zh-TW.md)。

文件以英文版為主，本頁為繁體中文版本。

[![版本](https://img.shields.io/badge/版本-0.3.1%20原型-1f6feb?style=flat-square)](VALIDATION.md)
[![授權](https://img.shields.io/badge/授權-GPL--3.0--or--later-2da44e?style=flat-square)](LICENSE)
[![遊戲](https://img.shields.io/badge/Cities%3A%20Skylines%20II-1.6.0f1-bf5af2?style=flat-square)](https://www.paradoxinteractive.com/games/cities-skylines-ii)
[![相依](https://img.shields.io/badge/需要-ExtendedRadio%2075862-fb8500?style=flat-square)](https://mods.paradoxplaza.com/mods/75862/Windows)
[![平台](https://img.shields.io/badge/平台-Windows%20x64-6e7781?style=flat-square)](#-建置)

**把 iPhone／iPad 正在播的音樂，直接投進《Cities: Skylines II》的原生電台面板。**

打開手機的 AirPlay 音訊輸出，選 `Airplay Radio - 你的電腦名稱`，按下播放——你的城市就有了一台專屬電台。曲名、歌手與封面跟著顯示，遊戲的音量、靜音、上一首／下一首照常使用。

> [!IMPORTANT]
> 這是**獨立原型**，不是 Live Radio 的一部分，也不會修改 Live Radio 的播放核心。請只在**可信任的家用區域網路**使用，**不要**把接收連接埠轉發到網際網路——原型尚未加入配對 PIN，接收核心也未完成安全審查。

---

## 🚀 快速開始

1. **確認環境**：Windows x64 版《Cities: Skylines II》。目前實機紀錄來自 **1.6.0f1**，其他版本未驗證。
2. **啟用相依**：在同一個播放集啟用 [ExtendedRadio](https://mods.paradoxplaza.com/mods/75862/Windows)（ID **75862**）與 Airplay Radio。
3. **選台**：進入城市，打開原生電台面板，找到 **Airplay Radio** 網路底下的 **Airplay Radio** 電台並選取它。
4. **投放**：手機與電腦連同一個區域網路，在手機的 AirPlay **音訊輸出**清單選擇 `Airplay Radio - 電腦名稱`，然後開始播放。
5. **微調**：太小聲就到 **選項 → Airplay Radio → 音量增益（dB）** 加 +6 dB 試試，調整立即生效。

第一次連線若跳出 Windows 防火牆提示，**只勾選你正在用的私人網路**。模組不會自動建立防火牆規則。

完整的安裝、操作與疑難排解請看 🎧 **[操作指南](USAGE.zh-TW.md)**。

---

## ✨ 做得到

| | 說明 |
| --- | --- |
| 🎵 **原生電台體驗** | 以獨立的 network 與 patch ID 註冊電台，設定與資料目錄都和 Live Radio 分開。 |
| 🔇 **遊戲控制音量** | 電台音量、靜音、緊急廣播靜音都照遊戲原本的方式運作。 |
| 📱 **手機媒體遙控** | 手機提供 DACP 服務時，原生的上一首／下一首／播放／暫停會直接控制手機。 |
| 🖼️ **媒體封面** | 手機送來 JPEG／PNG 封面時，電台列與播放列顯示圓形縮圖；沒有就沿用廣播圖示。 |
| 🔊 **音量增益** | 0～+12 dB 增益搭配雙聲道連動峰值限制，救回太小聲的來源。 |
| 🧩 **零外掛程序** | 接收核心、mDNS 探索、ALAC／AAC／PCM 解碼全在遊戲程序內完成。**不需要** FFmpeg、Bonjour、GStreamer 或任何輔助 EXE。 |

## ⛔ 做不到

- ❌ 不是完整的 AirPlay 2：沒有同步多房間、影片鏡像，也不保證所有 DRM 來源相容。
- ❌ **Android 原生投放不在保證範圍內**，這是 AirPlay 原型。
- ❌ 沒有歌單功能；封面由手機播放器決定是否提供，模組不會另外上網找圖。
- ❌ 手機音量與遊戲音量是兩段獨立增益，**不會互相同步**。
- ❌ 尚未實作依 NTP 時鐘排程與長時間時鐘漂移校正，啟播與重新緩衝至少需要 500 ms 的預緩衝。
- ❌ 無法替手機切換通話輸出或可靠地分離串流中的通話聲，詳見[通話音訊說明](USAGE.zh-TW.md#通話音訊留在手機)。

---

## 🎛️ 選項頁一覽

**選項 → Airplay Radio**

| 設定 | 預設 | 說明 |
| --- | --- | --- |
| 🔌 啟用接收器 | 開 | 關閉後停止接收；重新開啟需要從手機重新連線。 |
| 🔄 重新啟動接收器 | — | 網路換了或狀態卡住時按一下，之後在手機重新選擇輸出。 |
| 🎚️ 手機媒體遙控 | 開 | 關閉後上一首／下一首不送出，暫停改為本機暫停。 |
| 🔊 音量增益（dB） | 0 | 0～+12 dB，立即生效。+6 dB 約 2 倍振幅，+12 dB 約 4 倍。 |
| 🖼️ 顯示媒體封面 | 開 | 關閉後一律使用廣播圖示。 |
| 📡 接收狀態 | — | 目前曲目、裝置名稱與遙控連線狀態。 |
| 📈 音訊診斷 | — | 播放時每 15 秒更新的緩衝／重送／解碼統計。 |

> [!NOTE]
> 找不到遙控服務時，**暫停是本機暫停**：手機繼續播，模組丟棄收到的音訊。已連上遙控但指令被拒或逾時，畫面會顯示失敗，不會假裝手機已暫停。

---

## 📚 文件導覽

| 文件 | 內容 |
| --- | --- |
| 🎧 [操作指南](USAGE.zh-TW.md) | 安裝、第一次連線、選項詳解、疑難排解、診斷數值怎麼看 |
| 📝 [版本紀錄](CHANGELOG.zh-TW.md) | 各版本更新內容 |
| 🧪 [驗證紀錄](VALIDATION.md) | 各版本測試證據、適用範圍與仍待實機驗收的項目 |
| 🛍️ [英文商店草稿](docs/paradox-description.en.md)／[繁中商店草稿](docs/paradox-description.zh-TW.md) | 商店介紹、相依與限制；另見[發布前檢查](docs/release-readiness.zh-TW.md) |
| 📄 [第三方聲明](THIRD-PARTY-NOTICES.md) | 接收核心與解碼器的 GPL／LGPL／MIT 元件 |
| 🎨 [圖示製作紀錄](assets/GENERATION.md) | 電台圖示的產生方式與歷次修訂 |

---

## 🧱 運作方式

- **音訊路徑**：接收 → 解碼 → 固定 44.1 kHz 雙聲道浮點 → Unity Radio 混音群組。緩衝上限 2 秒、至少預緩衝 500 ms（遊戲一次要求更多時以該批需求為準）；滿了丟最舊的音訊，音訊回呼不等待解碼鎖。
- **中繼資料**：來源有提供才顯示曲名、歌手、裝置名稱與封面。封面在背景解碼、裁切成 192 px 圓形縮圖，原始資料上限 2 MiB、邊長上限 4096 px，只保留最近 8 張於 `ModsData/AirplayRadio/Artwork`。
- **遙控**：從同一個 RTSP 連線取得 DACP 識別與憑證，於區域網路探索對應服務，背景送出有逾時的 HTTP 指令，不自動重送切歌。切台、關閉接收或換連線都會取消舊遙控。
- **生命週期**：切台、關閉接收與卸載都先以背景工作停止網路，再清理原生資源。緊急廣播期間靜音接收輸出。
- **本機資料**：`ModsData/AirplayRadio`（`device-id.txt`、`receiver.key`、`Artwork/`）與 `Logs/AirplayRadio.log`。

---

## 🛠️ 建置

沿用儲存庫根目錄的 `Local.props`（`GameManagedPath`、`ExtendedRadioPath`）與 .NET SDK。
原生工具鏈使用 MSYS2 UCRT64：GCC、CMake、Ninja、pkgconf、OpenSSL、libplist，加上 MSYS 的 make／diffutils。

```powershell
# 1. 取得固定版本的 FFmpeg 原始碼（commit 894da5ca7d742e4429ffb2af534fcda0103ef593）
git clone --depth 1 --branch n8.0.1 https://github.com/FFmpeg/FFmpeg.git .work/airplay-ffmpeg

# 2. 建置與測試
./AirplayRadio/scripts/build.ps1
./AirplayRadio/scripts/test.ps1

# 3. 關閉遊戲後安裝到本機 Mods 目錄
./AirplayRadio/scripts/install-local.ps1
```

腳本只建置 ALAC、AAC、PCM 解碼與重取樣的靜態函式庫，不產生 FFmpeg 執行檔；其餘原生相依同樣靜態連結，打包腳本會拒絕 Windows 系統 DLL 以外的動態相依。產物在根目錄 `artifacts/AirplayRadio`：`AirplayRadio.dll` 與 `native/AirplayRadioNative.dll`，另附偵錯符號與文件。`install-local.ps1` 只安裝本機原型，**不會**發布到 Paradox Mods。

---

## 🔐 安全與隱私

- 音訊、中繼資料與封面只在區域網路內傳輸，模組不會把任何內容上傳到外部服務。
- 封面不另行連線搜尋，只處理手機送來的資料。
- 請勿將接收連接埠對外轉發；原型沒有使用者配對 PIN。
- 分享日誌前請確認內容——`Logs/AirplayRadio.log` 會記錄裝置名稱與曲目資訊。

---

## 🤖 AI 使用聲明

> [!NOTE]
> 遊戲整合、接收器橋接及本地修改使用 AI 編碼代理開發，電台圖示亦經 AI 輔助製作。內附的上游程式碼保留原作者與授權聲明。已驗證與未驗證的範圍記錄於 [驗證紀錄](VALIDATION.md)。

`AirplayRadioNative.dll` 包含 [UxPlay](https://github.com/FDH2/UxPlay) 的 AirPlay／RAOP 核心、[UxPlayEnhanced](https://github.com/Kylepossible/UxPlayEnhanced) 的內嵌 mDNS，以及靜態連結的 [FFmpeg](https://github.com/FFmpeg/FFmpeg) 解碼器與重取樣器；另使用 OpenSSL、libplist、llhttp 和 MinGW／GCC 執行階段元件。UxPlay 保留了 RPiPlay、ShairPlay、AirplayServer 及 PlayFair 的歷史貢獻。

`AirplayRadio.dll` 為遊戲端整合，部分介接方式沿用 Live Radio。DACP 遙控是本地實作，協定行為參考 AirPlay 文件與 Shairport Sync。各來源版本、授權及修改內容列於 [第三方聲明](THIRD-PARTY-NOTICES.md)。

---

## ⚖️ 授權

本原型採 **GPL-3.0-or-later**；接收核心包含不同來源的 GPL／LGPL／MIT 元件，詳見 [第三方聲明](THIRD-PARTY-NOTICES.md)。Live Radio 原專案的 MIT 授權不因此改變。

本地測試套件不是已完成發布審查的正式版本。**發布原生二進位時，必須一併提供完整對應原始碼、建置腳本與所有第三方授權文件。**
