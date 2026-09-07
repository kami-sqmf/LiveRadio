import React, { useEffect, useState } from 'react';
import { useValue } from 'cs2/api';
import { useText } from './l10n';
import { Browser } from './Browser';
import { loadNativeControls } from './native';
import { openRequest$, send } from './state';

interface Registry {
  get(path: string, name: string): any;
  extend(path: string, name: string, extension: (component: any) => any): void;
  append(target: string, component: React.ComponentType): void;
}
const radioPath = 'game-ui/game/components/radio/radio-panel/radio-panel.tsx';
// CS2's module loader only requests the sibling stylesheet when this export is present.
export const hasCSS = true;
let handledOpenRequest = 0;

// Clone the native panel shell and keep its header, close behavior, placement and external radio player.
// This intentionally inspects React props, never hashes or translated DOM text.
function replaceContent(node: React.ReactNode, transform: (element: React.ReactElement<any>) => React.ReactElement): React.ReactNode {
  if (!React.isValidElement<any>(node)) return node;
  if (typeof node.props.contentClassName === 'string' && typeof node.props.onClose === 'function') return transform(node);
  if (!node.props.children) return node;
  const children = node.props.children;
  return React.cloneElement(node, {}, Array.isArray(children)
    ? children.map(child => replaceContent(child, transform)) : replaceContent(children, transform));
}

class NativeFallback extends React.Component<React.PropsWithChildren<{ original: React.ComponentType<any>; nativeProps: any }>, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  componentDidCatch(error: Error) { send('uiLog', `Native panel integration failed: ${error.message}`); send('close'); }
  render() { return this.state.failed ? React.createElement(this.props.original, this.props.nativeProps) : this.props.children; }
}

export default function register(registry: Registry) {
  if (!loadNativeControls((path, name) => registry.get(path, name))) {
    send('uiLog', 'Required native input controls unavailable; keeping the original radio panel.');
    return;
  }
  const native = registry.get(radioPath, 'RadioPanel');
  if (typeof native !== 'function') {
    registry.append('Game', function FallbackBrowser() {
      const [open, setOpen] = useState(false);
      return <div className="lr-fallback"><button onClick={() => setOpen(!open)}>Live Radio</button>
        {open && <div className="lr-fallback-panel"><Browser /></div>}</div>;
    });
    send('uiLog', 'Native RadioPanel export unavailable; adjacent browser fallback registered.');
    return;
  }
  registry.extend(radioPath, 'RadioPanel', (Original: (props: any) => React.ReactNode) => {
    function Extended(props: any) {
      const t = useText();
      const [browse, setBrowse] = useState(false);
      const request = useValue(openRequest$);
      useEffect(() => { if (request > handledOpenRequest) { handledOpenRequest = request; setBrowse(true); } }, [request]);
      useEffect(() => { send('uiLog', 'Native radio panel mounted (game stations first).'); send('icons'); return () => send('close'); }, []);
      // The installed native component is a function. Invoke it unconditionally to retain all native hooks.
      const tree = Original(props);
      let found = false;
      const result = replaceContent(tree, panel => {
        found = true;
        return React.cloneElement(panel, {
          className: `${panel.props.className || ''} lr-native-panel`,
          contentClassName: `${panel.props.contentClassName || ''} lr-native-content${browse ? ' lr-browser-open' : ''}`
        }, <>
          <div className="lr-mode"><button className={!browse ? 'lr-active' : ''} onClick={() => setBrowse(false)}>{t("Game stations", "遊戲電台")}</button>
            <button className={browse ? 'lr-active' : ''} onClick={() => setBrowse(true)}>Live Radio</button></div>
          {browse ? <div className="lr-browser-host"><Browser key={request} /></div> : <div className="lr-original">{panel.props.children}</div>}
        </>);
      });
      if (!found) throw new Error('Native panel shell no longer has contentClassName/onClose.');
      return <>{result}</>;
    }
    return function RadioPanel(props: any) {
      return <NativeFallback original={Original as React.ComponentType<any>} nativeProps={props}><Extended {...props} /></NativeFallback>;
    };
  });
  send('uiLog', 'Native RadioPanel extension registered.');
}
