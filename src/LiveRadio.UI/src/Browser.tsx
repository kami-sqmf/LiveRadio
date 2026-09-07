import React, { useEffect, useRef, useState } from 'react';
import { useValue } from 'cs2/api';
import { Scrollable } from 'cs2/ui';
import { AutoNavigationScope, InputActionBarrier } from 'cs2/input';
import { getCountries, countryName as nameOfCountry } from './countries';
import { useText } from './l10n';
import { ManualStation } from './ManualStation';
import { StationIcon } from './StationIcon';
import { data$, playback$, locale$, send, sameStation } from './state';
import { TextField, CheckField, useCameraActionBarrier, useToolActionBarrier } from './native';
type Tab = 'favorites' | 'explore' | 'recent';


export function Browser() {
  const t = useText(), locale = useValue(locale$);
  const countries = getCountries(locale), countryName = (code: string) => nameOfCountry(code, locale);
  const tabNames: Record<Tab, string> = { favorites: t('Favorites', '收藏'), explore: t('Explore', '探索'), recent: t('Recent', '最近') };
  const [manual, setManual] = useState(false), [decoderHelp, setDecoderHelp] = useState(false);
  const data = useValue(data$), playback = useValue(playback$);
  const [tab, setTab] = useState<Tab>('favorites');
  const [query, setQuery] = useState(data.query), [country, setCountry] = useState(data.country);
  const [aliases, setAliases] = useState(data.aliases), [showUnavailable, setShowUnavailable] = useState(false);
  const [countryOpen, setCountryOpen] = useState(false), [regionSearch, setRegionSearch] = useState('');
  const [details, setDetails] = useState(''), [editing, setEditing] = useState(false);
  const [composing, setComposing] = useState(false), [windowSize, setWindowSize] = useState(50);
  const [showNotice, setShowNotice] = useState(false);
  useCameraActionBarrier(!editing);
  useToolActionBarrier(!editing);
  const timer = useRef<ReturnType<typeof setTimeout>>();
  const edited = useRef(false), sent = useRef('');
  const cancelTimer = () => { if (timer.current) clearTimeout(timer.current); timer.current = undefined; };
  const key = (q: string, c: string, a: boolean) => `${q.trim()}\n${c}\n${a}`;
  const search = (q = query, c = country, a = aliases) => {
    cancelTimer(); sent.current = key(q, c, a); send('search', q, c, a);
  };
  useEffect(() => {
    if (!edited.current) { setQuery(data.query); setCountry(data.country); setAliases(data.aliases); }
  }, [data.query, data.country, data.aliases]);
  useEffect(() => {
    setShowNotice(!!playback.notice);
    const timeout = setTimeout(() => setShowNotice(false), playback.undo ? 10000 : 3500);
    return () => clearTimeout(timeout);
  }, [playback.notice, playback.undo]);
  useEffect(() => { if (editing) send('uiLog', 'Text input focused; native camera/tool barriers active.'); }, [editing]);
  useEffect(() => { if (playback.ready) send('tab', manual ? 'favorites' : tab); return () => { cancelTimer(); send('close'); }; }, [tab, playback.ready, manual]);
  useEffect(() => {
    cancelTimer();
    if (!manual && tab === 'explore' && !composing && edited.current && sent.current !== key(query, country, aliases))
      timer.current = setTimeout(() => search(), 400);
    return cancelTimer;
  }, [query, country, aliases, composing, tab, manual]);
  const chooseTab = (next: Tab) => { setManual(false); cancelTimer(); setEditing(false); setComposing(false); setCountryOpen(false); setDetails(''); setWindowSize(50); setTab(next); };
  const source = tab === 'favorites' ? data.favorites : tab === 'recent' ? data.recent : data.results;
  const unavailable = source.filter(s => !s.supported).length;
  const visible = (tab === 'explore' && !showUnavailable ? source.filter(s => s.supported) : source)
    .slice().sort((a, b) => tab === 'explore' ? Number(b.name === query.trim()) - Number(a.name === query.trim()) : 0);
  const regionOptions = countries.filter(([code, name]) => `${code} ${name}`.toLowerCase().includes(regionSearch.toLowerCase()));

  return <InputActionBarrier disabled={!editing} excludes={[]}>
    {/* A barrier only accepts one focus child; the form contains several native controls. */}
    <AutoNavigationScope debugName="LiveRadioBrowser">
    <div className="lr-browser" onWheel={e => e.stopPropagation()}
      onKeyDown={e => {
        e.stopPropagation();
        if (e.key === 'Escape' && !composing && !e.nativeEvent.isComposing) {
          if (countryOpen) setCountryOpen(false); else (document.activeElement as HTMLElement)?.blur();
        }
      }}>
      <div className="lr-tabs" role="tablist" aria-label={t("Station lists", "電台清單")}>
        {(Object.keys(tabNames) as Tab[]).map(t => <button key={t} role="tab" aria-selected={tab === t}
          className={tab === t ? 'lr-active' : ''} aria-label={t === 'favorites' ? `${tabNames.favorites} ${data.favorites.length}` : tabNames[t]}
          onClick={() => chooseTab(t)}><span>{tabNames[t]}</span>{t === 'favorites' && data.favorites.length > 0 && <span className="lr-tab-count">{data.favorites.length}</span>}</button>)}
        <button className="lr-add" disabled={!playback.ready} onClick={() => { cancelTimer(); setEditing(false); setManual(true); setCountryOpen(false); }}>{t('Add station', '新增電台')}</button>
      </div>
      {!playback.ready && <div className="lr-message" role="status">{playback.bootstrap || t("Waiting for Live Radio…", "等待 Live Radio 載入…")}</div>}
      {playback.ready && !playback.enabled && <div className="lr-message">{t("Radio is disabled", "電台已關閉")} <button onClick={() => send('enableRadio')}>{t("Enable radio", "開啟電台")}</button></div>}
      {playback.ready && !playback.decoder && <div className="lr-message lr-decoder">
        <span>{t('MP3 is ready. Other formats need FFmpeg.', 'MP3 可直接播放，其他格式需要 FFmpeg。')}</span>
        <button onClick={() => setDecoderHelp(!decoderHelp)}>{t('FFmpeg setup', '設定 FFmpeg')}</button>
        <button onClick={() => send('recheckDecoder')}>{t('Check again', '重新偵測')}</button>
        {decoderHelp && <div className="lr-decoder-help">
          <p>{t('Download a Windows build from the FFmpeg website and extract it. In Options > Live Radio > Audio decoder, enter the full path to bin/ffmpeg.exe. Then check again here. Nothing is installed automatically.', '從 FFmpeg 網站下載 Windows 版本並解壓縮。在選項 → Live Radio → 音訊解碼器填入 bin/ffmpeg.exe 的完整路徑，再回來按「重新偵測」。模組不會自動安裝。')}</p>
          <button onClick={() => send('decoderHelp')}>{t('Open FFmpeg download page', '開啟 FFmpeg 下載頁')}</button>
        </div>}
      </div>}
      {playback.emergency && <div className="lr-message">{t("An emergency broadcast is active. Station switching is paused.", "緊急廣播中，暫時無法切台")}</div>}
      {!manual && tab === 'explore' && <div className="lr-filters">
        <div className="lr-search-line"><label className="lr-search-label">{t("Station name", "電台名稱")}
          <TextField label={t("Station name", "電台名稱")} value={query} placeholder={t("Search stations, e.g. Jazz, BCC", "搜尋電台，例如中廣、BCC")}
            onValueChange={value => { edited.current = true; setQuery(value); setWindowSize(50); }}
            onEditing={setEditing} onComposing={value => { cancelTimer(); setComposing(value); }} onSubmit={() => search()} />
        </label>
        <button className="lr-small-button" disabled={!query} onClick={() => { edited.current = true; setQuery(''); search('', country); }}>{t("Clear", "清除")}</button>
        <button className="lr-small-button" disabled={playback.loading} onClick={() => { search(); }}>{t("Search", "搜尋")}</button></div>
        <div className="lr-region-line"><span>{t("Country / region", "國家／地區")}</span><button className="lr-region-button" aria-label={`${t("Country / region", "國家／地區")}: ${countryName(country)}`} aria-expanded={countryOpen} onClick={() => { setCountryOpen(!countryOpen); setRegionSearch(''); }}><span>{countryName(country)}</span><span className="lr-chevron" aria-hidden="true" /></button>
          <button disabled={playback.loading} onClick={() => send('refresh')}>{t("Refresh", "更新")}</button>
          <CheckField checked={showUnavailable} onChange={setShowUnavailable}>{t("Show unsupported", "顯示未支援項目")}</CheckField>
        </div>
        {countryOpen && <div className="lr-region-popup">
          <label>{t("Find a region", "搜尋地區")}<TextField label={t("Find a region", "搜尋地區")} value={regionSearch} onValueChange={setRegionSearch} onEditing={setEditing} placeholder={t("Taiwan, Japan, All regions", "台灣、日本、全部地區")} /></label>
          <Scrollable vertical className="lr-region-options">{regionOptions.map(([code, name]) => <button key={code} className={country === code ? 'lr-active' : ''}
            onClick={() => { edited.current = true; setCountry(code); setCountryOpen(false); setWindowSize(50); search(query, code); }}>{name}</button>)}</Scrollable>
        </div>}
        {query.trim().toUpperCase() === 'BCC' && <CheckField checked={aliases}
          onChange={checked => { edited.current = true; setAliases(checked); search(query, country, checked); }}>{t("Also search 中廣 (keep BCC results)", "也搜尋：中廣（保留 BCC 的結果）")}</CheckField>}
      </div>}
      {!manual && tab === 'explore' && <div className="lr-summary" aria-live="polite">{playback.loading ? t("Loading stations…", "正在取得電台清單…") : t("{0} stations · {1}", "已載入 {0} 台 · {1}", source.length, countryName(data.cacheCountry))}
        {unavailable > 0 && t(" · {0} unsupported", " · {0} 台尚未支援", unavailable)}
      </div>}
      {!manual && tab === 'explore' && playback.searchError && <div className="lr-message lr-error" role="alert">{playback.searchError}<button onClick={() => send('refresh')}>{t("Retry", "重試")}</button></div>}
      {!manual && tab === 'explore' && (playback.loading || playback.searchError) && source.length > 0 && <div className="lr-caption">{t("Previous results: ", "顯示先前清單：")}{countryName(data.cacheCountry)}{data.cacheQuery && ` · ${data.cacheQuery}`}{data.updatedAt && ` · ${new Date(data.updatedAt).toLocaleString()}`}</div>}
      {manual ? <ManualStation onClose={() => setManual(false)} onSaved={() => chooseTab('favorites')} onEditing={setEditing} /> : <Scrollable vertical className="lr-list" trackVisibility="scrollable">
        {visible.slice(0, windowSize).map(station => {
          const favorite = data.favorites.some(s => sameStation(s, station));
          const selected = playback.id === station.id;
          const unavailable = !station.supported || (station.needsDecoder && !playback.decoder);
          const busy = selected && ['playing', 'connecting', 'buffering', 'reconnecting'].includes(playback.state);
          const label = selected ? ({ playing: t("Playing", "播放中"), paused: t("Play", "播放"), connecting: t("Connecting", "連線中"), buffering: t("Buffering", "緩衝中"), reconnecting: t("Reconnecting", "重連中"), failed: t("Retry", "重試") } as Record<string, string>)[playback.state] || t("Play", "播放") : t("Play", "播放");
          return <div key={station.id} className={`lr-row ${selected ? 'lr-selected' : ''}`}>
            <div className="lr-row-main"><StationIcon name={station.name} src={station.icon} />
              <button className="lr-station" aria-expanded={details === station.id} onClick={() => setDetails(details === station.id ? '' : station.id)}>
                <span className="lr-name">{station.name}</span><span className="lr-meta">{station.custom ? t("Custom station", "自訂電台") : countryName(station.country)}{station.tags && ` · ${station.tags.split(',').slice(0, 2).join(', ')}`}</span>
              </button>
              <button disabled={unavailable || busy || !playback.ready || !playback.enabled || playback.emergency}
                aria-label={`${label} ${station.name}`} onClick={() => send('play', station.id)}>{label}</button>
              <button disabled={!favorite && !station.supported} aria-label={`${favorite ? t("Remove favorite", "移除收藏") : t("Favorites", "收藏")} ${station.name}`}
                className={favorite ? 'lr-remove' : ''} onClick={() => send('favorite', station.id)}>{favorite ? t("Remove", "移除") : t("Favorites", "收藏")}</button>
            </div>
            {selected && playback.state === 'failed' && <div className="lr-row-status" role="status">{playback.status}</div>}
            {unavailable && <div className="lr-row-status">{!station.supported ? station.reason : t("Needs FFmpeg. Open Options > Live Radio > Audio decoder to set it up.", "此電台需要解碼器，請到選項 → LiveRadio → 音訊解碼器設定 FFmpeg。")}</div>}
            {details === station.id && <div className="lr-detail">{station.codec || t("Unknown format", "未知格式")}{station.bitrate > 0 && ` · ${station.bitrate} kbps`}{station.language && ` · ${station.language}`}<div>{station.url}</div></div>}
          </div>;
        })}
        {visible.length === 0 && !playback.loading && <div className="lr-empty">
          <strong>{tab === 'favorites' ? t("No favorite stations yet", "還沒有收藏電台") : tab === 'recent' ? t("No recently played stations", "還沒有收聽紀錄") : t("No matching stations", "目前沒有符合條件的電台")}</strong>
          <p>{tab === 'favorites' ? t("Save stations to favorites to play them directly next time.", "點電台旁的「收藏」，下次就能直接播放。") : tab === 'recent' ? t("Stations appear here after playback starts successfully.", "成功開始播放的電台會出現在這裡。") : unavailable > 0 ? t("Turn on Show unsupported to see why.", "可開啟「顯示未支援項目」查看原因。") : t("Try another name or a broader region.", "試試其他台名，或擴大搜尋地區。")}</p>
          {tab !== 'explore' ? <button onClick={() => chooseTab('explore')}>{t("Explore stations", "探索電台")}</button> : country && <button onClick={() => { edited.current = true; setCountry(''); search(query, ''); }}>{t("Search all regions", "搜尋全部地區")}</button>}
        </div>}
        {visible.length > windowSize ? <button className="lr-more" onClick={() => setWindowSize(windowSize + 50)}>{t("Show more loaded stations", "顯示更多已載入電台")}</button> : tab === 'explore' && playback.more && <button className="lr-more" onClick={() => { setWindowSize(windowSize + 50); send('more'); }}>{t("Load more", "載入更多")}</button>}
        {!manual && tab === 'explore' && source.length >= 1000 && <p className="lr-caption">{t("The browsing limit has been reached. Refine your search.", "已達本次瀏覽上限，請縮小搜尋條件。")}</p>}
      </Scrollable>}
      {showNotice && playback.notice && <div className="lr-notice" role="status">{playback.notice}{playback.undo && <button onClick={() => send('undo')}>{t("Undo", "復原")}</button>}</div>}
    </div>
    </AutoNavigationScope>
  </InputActionBarrier>;
}
