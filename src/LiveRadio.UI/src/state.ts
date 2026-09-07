import { bindValue, trigger } from 'cs2/api';
export interface Station {
  id: string; name: string; country: string; codec: string; bitrate: number;
  tags: string; language: string; url: string; supported: boolean; needsDecoder: boolean; reason: string; custom?: boolean; icon?: string;
}
export interface Data {
  favorites: Station[]; recent: Station[]; results: Station[];
  query: string; country: string; aliases: boolean;
  cacheQuery: string; cacheCountry: string; updatedAt: string;
}
export interface Playback {
  ready: boolean; id: string; station: string; state: string; status: string;
  paused: boolean; muted: boolean; enabled: boolean; emergency: boolean; decoder: boolean; volume: number;
  loading: boolean; more: boolean; undo: boolean; searchError: string; notice: string; bootstrap: string;
}
export const data$ = bindValue<Data>('LiveRadio', 'data', {
  favorites: [], recent: [], results: [], query: '', country: 'TW', aliases: true,
  cacheQuery: '', cacheCountry: '', updatedAt: ''
});
export const playback$ = bindValue<Playback>('LiveRadio', 'playback', {
  ready: false, id: '', station: '', state: 'native', status: '', paused: true, muted: false,
  enabled: true, emergency: false, decoder: false, volume: 1, loading: false, more: false, undo: false,
  searchError: '', notice: '', bootstrap: ''
});
export const locale$ = bindValue<string>('LiveRadio', 'locale', 'en-US');
export const manualResult$ = bindValue<string>('LiveRadio', 'manualResult', '');
export const openRequest$ = bindValue<number>('LiveRadio', 'openRequest', 0);
export const send = (name: string, ...args: unknown[]) => trigger('LiveRadio', name, ...args);
export const sameStation = (a: Station, b: Station) => a.id === b.id || a.url === b.url;
