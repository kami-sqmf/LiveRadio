# 📡 Airplay Radio

把 iPhone／iPad 的音訊帶進《Cities: Skylines II》的原生電台面板。

## ✨ 功能

- 📱 在遊戲內直接接收 AirPlay 音訊，不需要另外啟動輔助程式。
- 🎵 顯示來源提供的曲名與歌手。
- 🖼️ 將手機傳來的媒體封面顯示為圓形電台縮圖。
- ⏯️ 來源支援時，可遙控上一首、下一首與播放／暫停。
- 🔊 提供 0～+12 dB 本機音量增益，搭配峰值限制。
- 🎛️ 使用遊戲原本的電台音量與靜音控制。
- 🌐 英文與繁體中文介面，隨遊戲語言切換。

## 🚀 開始收聽

1. 在同一播放集啟用 Airplay Radio 與 ExtendedRadio。
2. 進入城市，選取 Airplay Radio 電台。
3. 手機與電腦連接同一個可信任的區域網路。
4. 在手機的 AirPlay「音訊輸出」選單選擇「Airplay Radio - 電腦名稱」，開始播放。

來源太小聲時，可到「選項 → Airplay Radio → 音量增益（dB）」先試 +6 dB。高增益時，峰值限制可能壓縮音樂動態。

## 📦 需求

Windows x64。實機紀錄使用《Cities: Skylines II》**1.6.0f1**，其他版本尚未驗證。

同一播放集必須啟用 **ExtendedRadio（ID 75862）**：
https://mods.paradoxplaza.com/mods/75862/Windows

Live Radio 為選用。接收核心及解碼器內附於模組，不需另外安裝 FFmpeg 執行檔、Bonjour 服務或 GStreamer。

## ℹ️ 相容性與限制

這是實驗性的 AirPlay 音訊接收器，不提供螢幕鏡像、多房間同步、歌單／下一首資訊或完整 AirPlay 2 支援。Android 原生投放及所有 DRM 來源不在保證範圍內。

封面、曲目資料、遙控與播放狀態回報取決於手機播放器。未提供遙控的來源使用本機暫停；手機與遊戲音量不會互相同步。切台或停用接收器會中斷連線，切回後請在手機重新連線。

通話輸出由手機或通話 App 控制。模組無法替手機切換通話輸出，也無法可靠地過濾來源混入串流的通話音訊。

音訊需要緩衝，仍可能短暫卡頓；尚未實作完整來源時鐘排程與長時間漂移校正。目前測試不代表所有手機及網路組合皆相容。

請僅在可信任的區域網路使用：接收器尚無使用者配對 PIN，原生協定核心也未完成針對此模組的完整安全審查。請勿將接收連接埠轉發至網際網路；模組不會自動變更防火牆規則。

裝置識別與少量處理後的封面快取儲存在遊戲的本機資料目錄；封面由手機傳入，不會另外上網搜尋。

## 🤖 AI 聲明與授權

遊戲整合、接收器橋接及本地修改使用 AI 編碼代理 Codex 開發，電台圖示亦經 AI 輔助製作。內附的上游程式碼保留原作者與授權聲明。

## 📚 開源專案致謝

原生接收器包含 [UxPlay](https://github.com/FDH2/UxPlay) 的程式碼，以及其承襲自 RPiPlay、ShairPlay、AirplayServer 與 PlayFair 的貢獻。內嵌 mDNS 來自 [UxPlayEnhanced](https://github.com/Kylepossible/UxPlayEnhanced)，音訊解碼與重取樣使用靜態連結的 [FFmpeg](https://github.com/FFmpeg/FFmpeg) 函式庫。

另包含 OpenSSL、libplist、llhttp 與 MinGW／GCC 執行階段元件；遊戲介接方式沿用 Live Radio。DACP 遙控為本地實作，協定行為參考 AirPlay 文件與 Shairport Sync。確切來源版本、本地修改與元件授權列於套件內的 THIRD-PARTY-NOTICES.md 及授權檔案。

作者：kami-sqmf。Airplay Radio 採 GPL-3.0-or-later，套件附第三方元件聲明。本模組為獨立作品，未宣稱獲 Apple 認證或與其有隸屬關係。

模組原始碼、建置說明與未完成的發布審查：[Airplay Radio 原始碼](https://github.com/kami-sqmf/LiveRadio/tree/main/AirplayRadio)。

## 🆕 0.3.1 版本

新增分開的英文／繁體中文設定與播放提示，保留 0.3.0 的手機遙控、圓形媒體封面、音量增益及緩衝診斷。實機與自動測試紀錄收錄於隨附文件。
