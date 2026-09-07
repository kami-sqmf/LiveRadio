# 開發指南 / Development

[主文檔](../README.md) · [驗證紀錄](validation.md) · [發布準備](release-readiness.zh-TW.md)

## 建置與安裝

需要 Windows、.NET SDK、Node.js/npm、已安裝遊戲與 ExtendedRadio。目標框架為 .NET Framework 4.8；UI 使用 TypeScript／React。遊戲及 ExtendedRadio 組件只作本機參照，不能放入儲存庫。

在專案根目錄執行：

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/test-media.ps1
# Install Playwright locally if it is not already available:
npm install --prefix .tools/ui-tests --no-save playwright
$env:PLAYWRIGHT_MODULE = (Resolve-Path .tools/ui-tests/node_modules/playwright).Path
node tests/ui/check.cjs
./scripts/package-release.ps1
# Close the game before installing:
./scripts/install-local.ps1
```

UI 檢查預設使用已安裝的 Microsoft Edge。建置自動從 Player.log 尋找遊戲 Managed 目錄，從模組快取尋找 ExtendedRadio。若無法偵測，明確傳入路徑：

```powershell
./scripts/build.ps1 -GameManagedPath 'D:/Games/Cities Skylines II/Cities2_Data/Managed' -ExtendedRadioPath 'D:/Mods/ExtendedRadio'
```

建置會產生機器專用的 Local.props（Git 已排除）。一般產物位於 artifacts/LiveRadio；`-Diagnostics` 另產生 artifacts/LiveRadio-diagnostic。安裝診斷版須使用 `./scripts/install-local.ps1 -Diagnostics`。安裝前備份既有模組產物，不覆寫收藏及存檔。

核心檢查不需開啟遊戲；媒體檢查使用 FFmpeg 與 loopback 合成來源。`./scripts/test.ps1 -Live` 另會連線到真實目錄及電台。UI 替身測試不能取代 Gameface／Unity 驗收。

## 架構與已採用的 UI 決策

| 目錄 | 職責 |
| --- | --- |
| src/LiveRadio.Core | 目錄、收藏、串流、NLayer／FFmpeg 解碼、有界 PCM 佇列 |
| src/LiveRadio.Mod | 遊戲生命週期、ExtendedRadio、RadioPanel bindings、圖標快取 |
| src/LiveRadio.UI | 遊戲 React 執行期上的瀏覽介面、語系與原生輸入元件 |
| tests | 核心、媒體、UI 替身與獨立原生診斷 |
| scripts | 建置、驗證、本機安裝與封裝 |

原生面板首頁保留遊戲電台，進入 Live Radio 預設收藏。收藏、探索、最近及自訂電台共用播放核心，UI 不自行建立播放器。原生暫停、音量、靜音維持單一狀態來源；Live Radio AudioSource 接入 Radio 混音群組，原生等化器目前不分析直播。

探索按需查詢，每頁 50 台、每次搜尋上限 1,000 台；過期回應不覆蓋較新條件。BCC 別名擴展保留原查詢。收藏不觸發清單搜尋，但可補抓圖標資料，這項實作已取代舊計畫「收藏完全不下載圖標」的描述。UI 只顯示本機快取圖片，失敗使用首字。

文字欄處理原生 change 事件；多欄位使用 AutoNavigationScope，配合鏡頭／工具輸入阻擋。Gameface 不支援的 CSS 行為必須以實機確認。新增分類、收藏排序、自訂別名及匯入／匯出仍是後續方向，不屬於目前功能。

MP3 與 FFmpeg 使用單一寫入者／讀取者 PCM 佇列。滿緩衝時只讓解碼工作等待，音訊回呼不取得 producer lock。MP3 每框核對取樣率與聲道，格式改變會重建輸出及預緩衝，並非無縫格式切換。已知未解決的配置速率問題見 [風險說明](security.md)。

ExtendedRadio 的新增介面沒有完整移除／刷新功能，因此配接器只清理自己的頻道並使遊戲快取失效；涉及的私有欄位需要在遊戲更新後重驗。

## 原生解碼調查與後續驗證

目前 **MP3 使用內附 NLayer，其餘支援格式使用外部 FFmpeg**。不需 FFmpeg 不等於遊戲原生解碼；原生 Vorbis／Opus 直播尚未驗證或設為預設。

先前對照本機 CS2 1.6.0 與 ExtendedRadio 75862_21，確認其電台資產流程掃描本機音訊檔；AudioAsset 的檔案路徑使用 file://。這無法證明所有 Unity HTTP 音訊 API 都不支援直播，也無法證明能穩定播放無結尾串流。Vorbis 與 Opus 即使同為 OGG 容器仍需分開驗證。

`tests/native/NativeDecoderProbe.cs` 僅在診斷版編入；執行時暫停一般 session，不呼叫 NLayer／FFmpeg，設有啟動逾時、下載量及觀察上限。原生優先的目標順序是：已驗證可處理來源的遊戲／Unity 解碼 → 內附解碼器 → FFmpeg。變更前必須確認：

1. 完整 Vorbis 控制檔、未知長度 HTTP、連續 Vorbis 與 Opus，逐一確認實際編碼。既有探測中 Listen.moe 為 Opus，不能依 OGG 標籤當成 Vorbis。
2. 首次輸出、AudioClip 狀態、跨歌曲／logical stream 銜接、長時間記憶體上限。
3. 暫停／換台釋放，失敗時先停止原生音訊再回退，不能同時播放兩個解碼器。
4. Windows 遊戲執行期的原生 MP3 能力；泛用 Unity API 文件不是本機實測。

此調查合併原 ui-plan 與 native-decoder-review，移除已過時工時及安裝狀態。測試結果統一記在 [validation](validation.md)，功能變更記在 [Changelog](../CHANGELOG.md)，發布阻擋項只維護於 [發布準備](release-readiness.zh-TW.md)。
