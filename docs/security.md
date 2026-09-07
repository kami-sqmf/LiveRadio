# ⚠️ Known risks / 已知風險

[🏠 Home](../README.md) · [🏠 繁體中文首頁](../README.zh-TW.md)

---

## 🌐 English

> [!WARNING]
> The following issues remain unresolved in the 0.4.4 release. This disclosure documents the current behavior; it does not add protections or change playback. Decide whether these risks are acceptable for your computer and network before using the mod.

- 🌐 **Requests to local or private-network services.** Station and artwork URLs accept HTTP(S), but their resolved IP addresses and redirect destinations are not restricted to public networks. FFmpeg can also follow URLs inside HLS playlists, including segments and encryption keys. A malicious or compromised directory entry or stream could therefore make your computer send requests to services on localhost or your private network. The consequences depend on the services reachable from your computer. Opening the radio panel or browsing results can fetch missing artwork before you play a station; displaying a locally cached icon does not make the original download safe. There is currently no per-station private-network permission prompt or enforcing media proxy.
- 💥 **MP3 resource exhaustion.** The decoder supports changes in sample rate and channel count, but does not limit how frequently a stream can trigger replacement audio buffers. A stream that repeatedly switches formats could cause excessive allocations, memory pressure, audio stuttering or an unresponsive game. A bounded playback buffer does not bound the rate of these allocations.
- 📜 **Misleading diagnostic logs.** Directory-supplied station names can contain line breaks or control characters that reach logging without consistent sanitization. A crafted name could insert misleading lines into diagnostic output, making troubleshooting and shared reports less reliable.

Prefer known broadcaster stream URLs and avoid unfamiliar custom URLs. This reduces exposure but is not a guarantee: directory listings are not a security endorsement, and trusted-looking URLs can redirect or change their contents. Review diagnostics before sharing them; stream URLs and query tokens may contain private information, and saved URLs are stored in plain text.

Existing protocol restrictions, download limits and timeouts do not resolve these issues. If you cannot accept unsolicited requests to reachable local services or the resource-exhaustion risk, leave this version disabled. Destination enforcement, per-station network grants, MP3 format-transition limits and consistent log sanitization remain proposals, not features of this version.


---

## 🌏 繁體中文

> [!WARNING]
> 0.4.4 的以下問題尚未修正；本文件只揭露目前行為，不代表已加入防護。

- 🌐 **對本機及私人網路發送請求**：電台、圖標網址及重新導向未限制為公開 IP。FFmpeg 也會讀取 HLS 清單中的分段及金鑰網址。惡意來源可能讓電腦連到 localhost 或內網服務。開啟面板就可能補抓圖標，無須先播放；本機圖標快取不代表原始下載安全。目前沒有逐台內網授權或強制媒體代理。
- 💥 **MP3 資源耗盡**：已支援取樣率／聲道切換，但未限制切換頻率。惡意串流可能反覆重建緩衝，造成大量配置、記憶體壓力、卡頓或遊戲無回應；有界緩衝不等於配置速率有上限。
- 📜 **誤導診斷紀錄**：外部台名中的換行及控制字元尚未一致清理，可能插入看似真實的日誌行，影響判讀。

優先使用熟悉的廣播業者直連網址可以降低暴露，但無法保證安全；目錄及看似可信的網址可能被更改或重新導向。現有協定限制、逾時與下載上限未解決上述問題。無法接受這些風險時，請停用此版本。

收藏網址及查詢參數以明文儲存。分享診斷前請移除私人網址、權杖及收藏資料。目的地限制、逐台網路授權、MP3 格式切換頻率限制與日誌清理仍是待辦。
