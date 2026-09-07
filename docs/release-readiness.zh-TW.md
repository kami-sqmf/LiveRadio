# 🚀 Live Radio 0.4.4 發布準備

[🏠 主文檔](../README.md) · [🧪 驗證紀錄](validation.md) · [🛍️ 商店介紹](paradox-description.en.md) · [⚠️ 已知風險](security.md)

更新：2026-09-07。作者已授權發布 0.4.4，目前正在準備提交 Paradox Mods，尚未取得發布成功結果。此授權不代表下列未完成驗收已通過；已知風險與限制保留在商店說明。

---

## 🏷️ 發布資料

| 欄位 | 內容 |
| --- | --- |
| 名稱／版本 | Live Radio / 0.4.4 |
| 作者 | kami-sqmf |
| 🤖 AI 使用聲明 | 本專案所有程式碼皆使用 AI 編碼代理 Codex 開發；第三方元件保留自身聲明 |
| 縮圖 | release-assets/thumbnail.png，1024 × 1024；以 scripts/make-thumbnail.ps1 產生 |
| 原始碼 | https://github.com/kami-sqmf/LiveRadio |
| 問題紀錄 | https://github.com/kami-sqmf/LiveRadio/issues；不保證回覆 |
| 授權 | [MIT](../LICENSE)，保留 [第三方聲明](../THIRD-PARTY-NOTICES.md) |
| 維護 | 大概率不會繼續更新，歡迎 Fork 自行接續開發 |
| 平台／類別 | Windows / Code Mod；於實際發布工具確認可選值 |
| 必要相依 | ExtendedRadio，模組 ID **75862**；本機測試版本 75862_21 |
| 遊戲版本 | 既有實機紀錄為 **1.6.0f1**，其他版本未驗證 |
| 說明 | [英文商店文案](paradox-description.en.md) |
| 版本說明 | [CHANGELOG](../CHANGELOG.md)，0.4.4 段落 |
| 套件 | artifacts/LiveRadio-0.4.4.zip，內含 SHA256SUMS.txt |

---

## 📦 封裝與 GitHub

`./scripts/build.ps1` 建置一般版，`./scripts/test.ps1` 驗證核心，`./scripts/package-release.ps1` 產生 ZIP。封裝使用執行產物白名單，加上主文檔、子文檔、MIT 與第三方授權，並為每個檔案產生 SHA-256。文件從工作區取得，避免拿到舊建置中的過時副本。

GitHub 只上傳原始碼、建置／測試指令碼、文件、授權與自製發布素材。排除 .tools、.work、artifacts、Local.props、node_modules、bin／obj、遊戲 DLL、ExtendedRadio／Harmony DLL、FFmpeg、私人收藏、快取圖標、日誌及存檔。ZIP 另排除 PDB 及診斷探測程式。

---

## 🚢 Paradox Mods 上傳流程

1. 從遊戲安裝目前 Modding Toolchain，取得當前官方專案／Publish 範本；先核對工具提供的欄位及相依檔案收集方式。
2. 以一般版 artifacts/LiveRadio 的執行檔與必要文件準備內容，對照 ZIP 白名單。不要直接發布 bin 或整個工作區。
3. 填入上表資料、必要相依、商店文案、版本說明、縮圖與遊戲截圖。核對發布工具對遊戲版本及圖片尺寸的實際要求。
4. 確認外部 FFmpeg 執行方式符合當前平台規則；不內附或自動下載 FFmpeg。
5. 若工具提供非公開測試方式，先驗證訂閱、升級及回退；避免本機 Mods 與訂閱版本重複載入。
6. 完成下列待辦後，另行提交發布；記錄平台模組 ID，再更新 README 的安裝連結及本頁狀態。

官方指出可使用 Visual Studio／Rider Publish，平台提供說明、截圖、相依及支援版本等中繼資料。2026-04-29 更新亦要求發布縮圖。此次官方 Wiki 仍回傳 401，未取得完整 PublishConfiguration schema，因此不提供假定可直接上傳的 XML。

來源：[Code Modding](https://www.paradoxinteractive.com/games/cities-skylines-ii/modding/dev-diary-3-code-modding)、[Paradox Mods](https://www.paradoxinteractive.com/games/cities-skylines-ii/modding/dev-diary-1-paradox-mods)、[1.5.7f1 更新](https://www.paradoxinteractive.com/games/cities-skylines-ii/news/patch-notes-spring-cleaning)。

---

## ☑️ 尚待完成

- [ ] 現版一般包完整實機驗收；此前 0.4.4 圖標實測來自診斷版。
- [ ] 較長 MP3／AAC／Vorbis／Opus／HLS 試聽與分段銜接；追蹤政大讀取錯誤、Big B 連線失敗與輔大偶發格式問題。
- [ ] 英文介面、完整 IME／WASD、手動新增／移除／復原、退出重啟後收藏、緊急廣播與音訊釋放。
- [ ] 乾淨播放集：僅本模組＋ExtendedRadio，並測試無 FFmpeg、無網路、失效電台、缺少相依。
- [ ] 原生解碼優先調查；目前維持 NLayer／FFmpeg，不聲稱原生直播已通過。
- [ ] 處理或明確接受 [已知風險](security.md)；本輪沒有實作安全修正。
- [x] 依作者要求補拍英文遊戲截圖，裁切至廣播面板，包含原生 Channel Program；設定頁截圖已移除。
- [ ] 核對縮圖與截圖是否符合當前上傳端要求。
- [ ] 取得目前官方發布設定，核對外部程序規則、相依、遊戲版本及訂閱／升級流程。
- [ ] 核對發布工具是否有 AI 生成內容的標示欄位，並據實填寫。

不承諾未來更新，並不代表上述未完成項目已通過。MIT 授權與維護聲明已依作者指示同步到主文檔與商店介紹。
