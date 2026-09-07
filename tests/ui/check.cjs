const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(__dirname, '../..');
const ui = path.join(root, 'src/LiveRadio.UI');
// Gameface treats a var() fallback as part of the name, unlike Chromium.
const css = fs.readFileSync(path.join(ui, 'dist/LiveRadio.css'), 'utf8');
assert.equal(/var\(--[^)]+,/.test(css), false, 'Gameface CSS variables cannot use fallback arguments');

(async () => {
  const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || 'msedge' });
  try {
    const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    await page.setContent('<html><body><div id="root"></div></body></html>');
    await page.addScriptTag({ path: path.join(ui, 'node_modules/react/umd/react.development.js') });
    await page.addScriptTag({ path: path.join(ui, 'node_modules/react-dom/umd/react-dom.development.js') });
    await page.addStyleTag({ path: path.join(ui, 'dist/LiveRadio.css') });
    await page.addStyleTag({ content: `html{font-size:1px;--fontSizeS:16rem;--fontSizeXS:14rem;--fontSizeM:20rem;--panelColorDark:#192936;--panelColorNormal:#243644;--accentColorNormal:#78e0ed;--textColor:#f3f7fa;--textColorDim:#bed0dc;--dividerColor:#456070;}body{font:16rem Arial;background:#101a24;margin:20rem;color:white;}*{box-sizing:border-box;}button,input{font:inherit;}button{cursor:pointer;}.native-panel{width:760rem;height:546rem;display:flex;flex-direction:column;background:#243644;}.native-content{flex:1;min-height:0;display:flex;flex-direction:row;}.native-header{padding:12rem;}.test-scroll{overflow:auto;}.native-player{width:760rem;padding:15rem;background:#192936;}` });
    await page.evaluate(() => {
      const R = window.React, bindings = {}, listeners = {};
      window.calls = []; window.cameraBlocks = 0; window.toolBlocks = 0;
      window.publish = (name, update) => {
        const binding = bindings[name]; binding.value = typeof update === 'function' ? update(binding.value) : update;
        (listeners[name] || []).forEach(fn => fn());
      };
      window['cs2/api'] = {
        bindValue(group, name, fallback) { return bindings[name] || (bindings[name] = { value: fallback, name }); },
        useValue(binding) {
          const [, change] = R.useState(0);
          R.useEffect(() => { const fn = () => change(v => v + 1); (listeners[binding.name] ||= []).push(fn); return () => { listeners[binding.name] = listeners[binding.name].filter(x => x !== fn); }; }, [binding]);
          return binding.value;
        },
        trigger(group, name, ...args) {
          window.calls.push([name, ...args]);
          if (name === 'favorite') {
            const data = bindings.data.value, station = [...data.results, ...data.favorites].find(s => s.id === args[0]);
            window.publish('data', { ...data, favorites: data.favorites.some(s => s.id === args[0]) ? data.favorites.filter(s => s.id !== args[0]) : [...data.favorites, station] });
          }
        }
      };
      window['cs2/ui'] = { Scrollable: props => R.createElement('div', { className: props.className + ' test-scroll' }, props.children) };
      // Native pass-through focus controllers allow only one registered child,
      // even when the input barrier is disabled. Model that contract explicitly.
      const FocusContext = R.createContext(null);
      window.focusErrors = [];
      function useFocusChild(kind) {
        const parent = R.useContext(FocusContext), id = R.useId();
        R.useLayoutEffect(() => {
          if (!parent) return;
          if (!parent.multiple && parent.children.size) {
            const error = `Cannot register second focus key ${kind}; use a navigation scope`;
            window.focusErrors.push(error); throw new Error(error);
          }
          parent.children.add(id); return () => parent.children.delete(id);
        }, [parent, id]);
      }
      function FocusContainer({ children, multiple, kind }) {
        useFocusChild(kind);
        const controller = R.useMemo(() => ({ children: new Set(), multiple }), [multiple]);
        return R.createElement(FocusContext.Provider, { value: controller }, children);
      }
      window['cs2/input'] = {
        InputActionBarrier: props => R.createElement(FocusContainer, { multiple: false, kind: 'InputActionBarrier' }, props.children),
        AutoNavigationScope: props => R.createElement(FocusContainer, { multiple: true, kind: 'AutoNavigationScope' }, props.children)
      };
      // Match the installed game: TextInput forwards the event, and blurs on Enter/Escape.
      window.nativeInput = props => { useFocusChild('TextInput'); return R.createElement('input', { ...props, onKeyDown: e => {
        if (e.key === 'Enter' || e.key === 'Escape') e.currentTarget.blur();
        e.stopPropagation(); props.onKeyDown?.(e);
      } }); };
      window.nativeCheckbox = props => { useFocusChild('Toggle'); return R.createElement('div', { className: props.className, 'data-native-control': 'Checkbox',
        style: { border: '1rem solid #9dc7d9', background: props.checked ? '#62b7ce' : '#19252e' },
        onClick: e => { props.onChange(!props.checked); e.stopPropagation(); e.preventDefault(); }
      }); };
      window.cameraBarrier = disabled => R.useEffect(() => { if (!disabled) { window.cameraBlocks++; return () => window.cameraBlocks--; } }, [disabled]);
      window.toolBarrier = disabled => R.useEffect(() => { if (!disabled) { window.toolBlocks++; return () => window.toolBlocks--; } }, [disabled]);
      function Panel(props) { return R.createElement('section', { className: props.className }, R.createElement('header', { className: 'native-header' }, '電台 ', R.createElement('button', { onClick: props.onClose }, '關閉')), R.createElement('div', { className: props.contentClassName }, props.children)); }
      function TutorialGuideTarget(props) { if (!R.isValidElement(props.children)) throw new Error('TutorialGuideTarget requires a single child react element!'); return props.children; }
      window.NativeRadio = props => { R.useEffect(() => {}, []); return R.createElement(TutorialGuideTarget, null, R.createElement(Panel, { className: 'native-panel', contentClassName: 'native-content', onClose: props.onClose }, R.createElement('div', null, '原生電台清單'))); };
      window.registry = { get(p, n) { return ({ RadioPanel: window.NativeRadio, TextInput: window.nativeInput, Checkbox: window.nativeCheckbox,
        useCameraActionBarrier: window.cameraBarrier, useToolActionBarrier: window.toolBarrier })[n]; }, extend(p, n, fn) { window.ExtendedRadio = fn(window.NativeRadio); } };
      window.appRoot = window.ReactDOM.createRoot(document.getElementById('root'));
      window.mount = () => window.appRoot.render(R.createElement('div', null, R.createElement(window.ExtendedRadio, { onClose: () => window.appRoot.render(R.createElement('button', { onClick: window.mount }, '重新開啟')) }), R.createElement('div', { className: 'native-player' }, '原生播放列')));
    });
    const source = fs.readFileSync(path.join(ui, 'dist/LiveRadio.mjs'), 'utf8');
    await page.evaluate(async source => { const mod = await import(URL.createObjectURL(new Blob([source], { type: 'text/javascript' }))); if (mod.hasCSS !== true) throw new Error('CS2 requires hasCSS to load the stylesheet'); mod.default(window.registry); window.mount(); }, source);
    assert.equal(await page.getByText('原生電台清單', { exact: true }).isVisible(), true);
    assert.equal(await page.locator('.lr-browser').count(), 0);
    assert.equal(await page.getByRole('button', { name: 'Game stations', exact: true }).isVisible(), true, 'English is the default');
    await page.getByRole('button', { name: 'Live Radio', exact: true }).click();
    assert.equal(await page.getByRole('tab', { name: 'Favorites 0', exact: true }).isVisible(), true);
    await page.evaluate(() => window.publish('locale', 'de-DE'));
    assert.equal(await page.getByRole('tab', { name: 'Favorites 0', exact: true }).isVisible(), true, 'Untranslated languages use English');
    await page.evaluate(() => window.publish('locale', 'zh-HANT'));

    await page.getByRole('button', { name: 'Live Radio', exact: true }).click();
    assert.equal(await page.getByRole('tab', { name: '收藏 0', exact: true }).getAttribute('aria-selected'), 'true');
    assert.equal(await page.locator('.lr-filters').count(), 0);
    assert.equal(await page.evaluate(() => window.calls.filter(c => ['search', 'refresh', 'more'].includes(c[0]) || c[0] === 'tab' && c[1] === 'explore').length), 0);
    await page.getByRole('button', { name: '關閉', exact: true }).click();
    await page.getByRole('button', { name: '重新開啟' }).click();
    assert.equal(await page.getByText('原生電台清單', { exact: true }).isVisible(), true);
    await page.getByRole('button', { name: 'Live Radio', exact: true }).click();
    assert.equal(await page.getByRole('tab', { name: '收藏 0', exact: true }).getAttribute('aria-selected'), 'true');
    await page.evaluate(() => {
      const station = { id: 'bcc', name: '中廣音樂網', country: 'TW', codec: 'AAC', bitrate: 128, tags: '音樂', language: '中文', url: 'https://example.com/music', supported: true, needsDecoder: true, reason: '' };
      window.publish('data', data => ({ ...data, results: [station, { ...station, id: 'ogg', name: 'OGG 測試電台', codec: 'OGG', url: 'https://example.com/ogg' },
        { ...station, id: 'unsupported', name: '未支援測試电台', supported: false, reason: 'Unsupported audio format', url: 'https://example.com/live.m3u8' }], cacheCountry: 'TW' }));
      window.publish('playback', p => ({ ...p, ready: true, decoder: true }));
    });
    await page.getByRole('button', { name: '探索電台', exact: true }).click();
    const unsupported = page.getByRole('checkbox', { name: '顯示未支援項目', exact: true });
    assert.equal(await page.getByText('未支援測試电台', { exact: true }).count(), 0);
    await unsupported.locator('[data-native-control="Checkbox"]').click();
    assert.equal(await unsupported.getAttribute('aria-checked'), 'true');
    assert.equal(await page.getByText('未支援測試电台', { exact: true }).isVisible(), true);
    await unsupported.getByText('顯示未支援項目').click();
    assert.equal(await unsupported.getAttribute('aria-checked'), 'false');
    assert.equal(await page.locator('input[type="checkbox"]').count(), 0);
    await page.getByRole('button', { name: '收藏 中廣音樂網', exact: true }).click();
    assert.equal(await page.evaluate(() => window.calls.filter(c => c[0] === 'play').length), 0);
    await page.getByRole('button', { name: '播放 中廣音樂網', exact: true }).click();
    assert.equal(await page.getByRole('button', { name: '播放 中廣音樂網', exact: true }).isEnabled(), true, 'A click must not claim actual playback before a backend update');
    await page.evaluate(() => window.publish('playback', p => ({ ...p, id: 'bcc', station: '中廣音樂網 · LiveRadio', state: 'playing', status: '直播中', paused: false })));
    assert.equal(await page.getByRole('button', { name: '播放中 中廣音樂網', exact: true }).isDisabled(), true);
    await page.evaluate(() => window.publish('playback', p => ({ ...p, state: 'paused', paused: true, status: '已暫停' })));
    assert.equal(await page.getByRole('button', { name: '播放 中廣音樂網', exact: true }).isEnabled(), true);
    const input = page.getByRole('textbox', { name: '電台名稱', exact: true });
    await input.fill('BCC');
    await page.waitForTimeout(480);
    assert(await page.evaluate(() => window.calls.some(c => c[0] === 'search' && c[1] === 'BCC' && c[2] === 'TW')));
    assert.equal(await page.getByText('也搜尋：中廣（保留 BCC 的結果）').isVisible(), true);
    assert.equal(await page.evaluate(() => window.cameraBlocks), 1);
    assert.equal(await page.evaluate(() => window.toolBlocks), 1);
    await input.pressSequentially('wasd');
    assert.equal(await input.inputValue(), 'BCCwasd');
    assert.equal(await page.locator('.lr-browser').count(), 1, 'Typing must not crash/unmount the browser');
    await input.fill('BCC');
    await input.press('Enter');
    assert.equal(await page.evaluate(() => window.cameraBlocks), 0);
    assert.equal(await page.evaluate(() => window.toolBlocks), 0);
    await page.evaluate(() => { window.calls.length = 0; });
    await input.dispatchEvent('compositionstart'); await input.fill('中廣'); await page.waitForTimeout(500);
    assert.equal(await page.evaluate(() => window.calls.filter(c => c[0] === 'search').length), 0);
    await input.dispatchEvent('keydown', { key: 'Enter', keyCode: 13, isComposing: true });
    assert.equal(await input.evaluate(e => e === document.activeElement), true, 'IME Enter must not reach the stock input blur handler');
    await input.dispatchEvent('compositionend'); await page.waitForTimeout(480);
    assert.equal(await page.evaluate(() => window.calls.filter(c => c[0] === 'search').length), 1);
    await page.getByRole('button', { name: '國家／地區: 台灣', exact: true }).click();
    await page.getByRole('textbox', { name: '搜尋地區', exact: true }).fill('全部');
    await page.getByRole('button', { name: '全部地區', exact: true }).click();
    assert(await page.evaluate(() => window.calls.some(c => c[0] === 'search' && c[2] === '')));
    await page.getByRole('button', { name: '遊戲電台', exact: true }).click();
    assert.equal(await page.getByText('原生電台清單', { exact: true }).isVisible(), true);
    assert.equal(await page.getByText('原生播放列', { exact: true }).isVisible(), true);
    await page.getByRole('button', { name: 'Live Radio', exact: true }).click();
    assert.equal(await page.getByRole('tab', { name: '收藏 1', exact: true }).getAttribute('aria-selected'), 'true');
    assert.equal(await page.locator('.lr-summary,.lr-playback').count(), 0);
    assert(await page.locator('.lr-tab-count').evaluate(e => parseFloat(getComputedStyle(e).marginLeft) >= 6));
    for (const [width, height, scale] of [[1920, 1080, 1], [2560, 1440, 1.33], [1920, 1080, 1.25]]) {
      await page.setViewportSize({ width, height });
      await page.evaluate(scale => document.documentElement.style.fontSize = `${scale}px`, scale);
      assert(await page.evaluate(() => { const r = document.querySelector('.lr-native-panel').getBoundingClientRect(); return r.right < innerWidth && r.bottom < innerHeight; }));
      const listLayout = await page.evaluate(() => {
        const list = document.querySelector('.lr-list').getBoundingClientRect(), host = document.querySelector('.native-content').getBoundingClientRect();
        return { gap: host.bottom - list.bottom, listHeight: list.height, panelHeight: host.height };
      });
      assert(listLayout.gap < 14 && listLayout.listHeight > listLayout.panelHeight * 0.72,
        `Favorites must use the remaining height without a bottom gap: ${JSON.stringify(listLayout)}`);
    }
    await page.evaluate(() => document.documentElement.style.fontSize = '1px');
    await page.screenshot({ path: path.join(root, '.work/ui-0.4.0-favorites.png') });
    await page.getByRole('tab', { name: '探索', exact: true }).click();
    await page.screenshot({ path: path.join(root, '.work/ui-0.4.0-explore.png') });
    const nameField = page.getByRole('textbox', { name: '電台名稱', exact: true });
    assert(await nameField.evaluate(e => e.getBoundingClientRect().width > 400));
    await nameField.focus();
    await page.getByRole('button', { name: '遊戲電台', exact: true }).click();
    assert.equal(await page.evaluate(() => window.cameraBlocks + window.toolBlocks), 0, 'Unmounting releases both native barriers');
    await page.evaluate(() => window.publish('openRequest', 1));
    assert.equal(await page.getByRole('tab', { name: '收藏 1', exact: true }).getAttribute('aria-selected'), 'true');
    await page.getByRole('button', { name: '關閉', exact: true }).click();
    await page.getByRole('button', { name: '重新開啟' }).click();
    assert.equal(await page.getByText('原生電台清單', { exact: true }).isVisible(), true, 'A consumed settings request must not override the home page next time');

    await page.getByRole('button', { name: 'Live Radio', exact: true }).click();
    await page.evaluate(() => window.publish('locale', 'en-US'));
    await page.getByRole('tab', { name: 'Explore', exact: true }).click();
    assert.equal(await page.getByRole('button', { name: 'Country / region: Taiwan', exact: true }).isVisible(), true);
    await page.evaluate(() => {
      window.publish('playback', p => ({ ...p, decoder: false }));
      window.publish('data', d => ({ ...d, results: [...d.results, { ...d.results[0], id: 'real-hls', name: 'HLS Audio', codec: 'HLS', url: 'https://example.com/new.m3u8', icon: 'https://invalid.example/icon.png' }] }));
      window.calls.length = 0;
    });
    assert.equal(await page.getByText('HLS Audio', { exact: true }).isVisible(), true, 'Missing FFmpeg must not hide HLS stations');
    assert.equal(await page.getByRole('button', { name: 'Play HLS Audio', exact: true }).isDisabled(), true);
    assert.equal(await page.getByRole('button', { name: 'Favorites HLS Audio', exact: true }).isEnabled(), true);
    const hlsIcon = page.locator('.lr-row').filter({ hasText: 'HLS Audio' }).locator('.lr-icon');
    for (const remote of ['http://invalid.example/icon.png', 'https://invalid.example/icon.png']) {
      await page.evaluate(icon => window.publish('data', d => ({ ...d, results: d.results.map(s => s.id === 'real-hls' ? { ...s, icon } : s) })), remote);
      assert.equal(await hlsIcon.locator('img').count(), 0, 'Remote artwork never reaches the Unity resource handler, even over HTTPS');
      assert.equal(await hlsIcon.innerText(), 'H');
    }
    const cachedIcon = 'coui://liveradio-icons/0123456789abcdef0123456789abcdef.png';
    // Chromium cannot load coui://; inspect and dispatch in the same task before its network error.
    await page.evaluate(icon => {
      window.ReactDOM.flushSync(() => window.publish('data', d => ({ ...d, results: d.results.map(s => s.id === 'real-hls' ? { ...s, icon } : s) })));
      const image = [...document.querySelectorAll('.lr-row')].find(row => row.textContent.includes('HLS Audio')).querySelector('img');
      window.cachedIconSrc = image?.getAttribute('src');
      image?.dispatchEvent(new Event('error'));
    }, cachedIcon);
    assert.equal(await page.evaluate(() => window.cachedIconSrc), cachedIcon, 'Validated local artwork is rendered');
    assert.equal(await hlsIcon.innerText(), 'H', 'Broken artwork falls back to the first character');
    await page.getByRole('button', { name: 'FFmpeg setup', exact: true }).click();
    assert.equal(await page.getByText('Nothing is installed automatically.', { exact: false }).isVisible(), true);
    await page.getByRole('button', { name: 'Check again', exact: true }).click();
    assert(await page.evaluate(() => window.calls.some(c => c[0] === 'recheckDecoder')));
    await page.evaluate(() => window.publish('playback', p => ({ ...p, decoder: true })));
    assert.equal(await page.getByRole('button', { name: 'Play HLS Audio', exact: true }).isEnabled(), true);
    await page.getByRole('button', { name: 'Add station', exact: true }).click();
    assert.equal(await page.locator('.lr-filters').count(), 0);
    const customName = page.getByRole('textbox', { name: 'Station name', exact: true });
    const customUrl = page.getByRole('textbox', { name: 'Stream URL', exact: true });
    assert.equal(await customUrl.getAttribute('maxlength'), '4096');
    await customName.fill('My HLS'); await customUrl.fill('https://example.com/live.m3u8?token=abc');
    assert.equal(await page.evaluate(() => window.cameraBlocks + window.toolBlocks), 2);
    await page.getByRole('button', { name: 'Save to favorites', exact: true }).click();
    assert(await page.evaluate(() => window.calls.some(c => c[0] === 'addCustom' && c[1] === 'My HLS' && c[2].endsWith('?token=abc') && c[3] === 'AUTO')));
    for (let retry = 1; retry <= 2; retry++) {
      await page.evaluate(retry => window.publish('manualResult', 'error:' + retry + ':This URL is already in your favorites.'), retry);
      assert.equal(await page.getByRole('alert').innerText(), 'This URL is already in your favorites.');
      assert.equal(await customName.inputValue(), 'My HLS');
      await page.getByRole('button', { name: 'Save to favorites', exact: true }).click();
    }
    await page.evaluate(() => window.publish('manualResult', 'saved:1'));
    assert.equal(await page.getByRole('tab', { name: 'Favorites 1', exact: true }).getAttribute('aria-selected'), 'true');
    assert.equal(await page.evaluate(() => window.calls.filter(c => c[0] === 'play').length), 0, 'Adding a favorite never starts audio');
    assert.equal(await page.evaluate(() => window.cameraBlocks + window.toolBlocks), 0, 'Leaving manual entry releases keyboard barriers');
    await page.getByRole('button', { name: 'Add station', exact: true }).click();
    await page.screenshot({ path: path.join(root, '.work/ui-0.4.0-manual-en.png') });
    await page.getByRole('button', { name: 'Cancel', exact: true }).click();
    await page.screenshot({ path: path.join(root, '.work/ui-0.4.0-favorites-en.png') });
    assert.deepEqual(await page.evaluate(() => window.focusErrors), [], 'Native controls register without competing for a single focus child');
    assert.deepEqual(errors, []);
    console.log('PASS: native home/reopen, favorites, native focus registration, input events and barriers, checkbox/IME/regions, playback, layout/scales, locale switching, decoder guidance, artwork fallback, manual station submission/retries; no JavaScript errors.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
