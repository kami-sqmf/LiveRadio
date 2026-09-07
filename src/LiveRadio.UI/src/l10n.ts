import { useValue } from 'cs2/api';
import { locale$ } from './state';

export const isChinese = (locale: string) => ['zh-hant', 'zh-tw', 'zh-hk'].includes(locale.toLowerCase());
export function useText() {
  const chinese = isChinese(useValue(locale$));
  return (english: string, traditional: string, ...args: (string | number)[]) =>
    (chinese ? traditional : english).replace(/\{(\d+)\}/g, (_, i) => String(args[Number(i)] ?? ''));
}
