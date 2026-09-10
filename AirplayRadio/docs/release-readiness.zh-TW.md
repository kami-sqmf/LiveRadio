# Airplay Radio 0.3.1 發布紀錄

[English](release-readiness.md) · [主要文件](../README.zh-TW.md)

2026-09-10 已公開發布至 [Paradox Mods](https://mods.paradoxplaza.com/mods/158648/Windows)，模組 ID **158648**，版本 **0.3.1**，Windows x64，建議遊戲版本 **1.6.***，需要 **ExtendedRadio 75862**。已確認未登入時也能瀏覽公開頁面，商店截圖只有裁切後的廣播面板。

Git 保留原始碼、必要第三方來源與授權、建置及打包腳本、測試與合成 ALAC 素材、文件、電台 SVG 及最終商店圖片。完整遊戲截圖、重複 PNG、編譯產物、壓縮包、快取、本機設定與接收器識別資料均忽略。原始截圖保留在本機，裁切與雜湊紀錄見[商店圖片](../release-assets/README.md)。

建置前把儲存庫根目錄的 `Local.props.example` 複製為 `Local.props` 並填入自己的路徑。FFmpeg 依 README 的固定版本另行取得。發布準備腳本沿用 ID 158648，只產生本機設定，不會自行上傳。

此次裁圖與發布未修改執行階段 DLL，也未增加實機驗收結果。既有測試與待驗證項目見[驗證紀錄](../VALIDATION.md)。GPL 整合及完整對應原始碼的未完成事項保留在[發布與授權紀錄](publication-review.md)；Git 原始碼上傳不代表這些事項已解決。
