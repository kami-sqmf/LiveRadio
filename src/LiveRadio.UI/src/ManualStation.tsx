import React, { useEffect, useRef, useState } from 'react';
import { useValue } from 'cs2/api';
import { Scrollable } from 'cs2/ui';
import { manualResult$, send } from './state';
import { useText } from './l10n';
import { TextField } from './native';

export function ManualStation({ onClose, onSaved, onEditing }: {
  onClose: () => void; onSaved: () => void; onEditing: (editing: boolean) => void;
}) {
  const t = useText(), result = useValue(manualResult$);
  const [name, setName] = useState(''), [url, setUrl] = useState(''), [format, setFormat] = useState('AUTO');
  const [pending, setPending] = useState(false), [error, setError] = useState('');
  const before = useRef(result);
  useEffect(() => {
    if (!pending || result === before.current) return;
    setPending(false);
    if (result.startsWith('saved:')) onSaved(); else setError(result.replace(/^error:[^:]*:/, ''));
  }, [result, pending]);
  const save = () => {
    if (pending || !name.trim() || !url.trim()) return;
    before.current = result; setError(''); setPending(true);
    send('addCustom', name.trim(), url.trim(), format);
  };
  return <Scrollable vertical className="lr-list lr-manual">
    <strong>{t('Add your own station', '新增自訂電台')}</strong>
    <p className="lr-caption">{t('Save a station name and a direct stream URL to your local favorites.', '輸入電台名稱與串流直連網址，儲存至本機收藏。')}</p>
    <label>{t('Station name', '電台名稱')}<TextField label={t('Station name', '電台名稱')} value={name} onValueChange={setName} onEditing={onEditing} placeholder={t('My radio station', '我的電台')} /></label>
    <label>{t('Stream URL', '串流網址')}<TextField label={t('Stream URL', '串流網址')} value={url} onValueChange={setUrl} onEditing={onEditing} maxLength={4096} placeholder="https://example.com/live.m3u8" /></label>
    <div className="lr-caption">{t('Use the audio or .m3u8 URL, not the station website. HTTP and HTTPS are supported.', '請填音訊或 .m3u8 網址，不是電台網頁。支援 HTTP 與 HTTPS。')}</div>
    <div className="lr-format" role="group" aria-label={t('Stream format', '串流格式')}>
      {['AUTO', 'MP3', 'AAC', 'OGG', 'HLS'].map(value => <button key={value} aria-pressed={format === value} className={format === value ? 'lr-active' : ''} onClick={() => setFormat(value)}>{value === 'AUTO' ? t('Auto', '自動') : value}</button>)}
    </div>
    <p className="lr-caption">{t('Auto recognizes file extensions; other URLs use FFmpeg. Choose MP3 for a direct MP3 stream when FFmpeg is not installed. AAC, OGG and HLS need FFmpeg.', '自動模式先辨識副檔名，其餘網址使用 FFmpeg。未安裝 FFmpeg 時，MP3 直連請選 MP3；AAC、OGG 與 HLS 需要 FFmpeg。')}</p>
    {error && <div className="lr-message lr-error" role="alert">{error}</div>}
    <div className="lr-form-actions"><button disabled={pending || !name.trim() || !url.trim()} onClick={save}>{pending ? t('Saving…', '儲存中…') : t('Save to favorites', '儲存至收藏')}</button><button disabled={pending} onClick={onClose}>{t('Cancel', '取消')}</button></div>
  </Scrollable>;
}
