import React, { useRef } from 'react';

type NativeInputProps = React.InputHTMLAttributes<HTMLInputElement>;
type CheckProps = { checked: boolean; onChange: (checked: boolean) => void; className?: string };
export let NativeTextInput: React.ComponentType<NativeInputProps>;
export let NativeCheckbox: React.ComponentType<CheckProps>;
export let useCameraActionBarrier: (disabled: boolean) => void;
export let useToolActionBarrier: (disabled: boolean) => void;

export function loadNativeControls(get: (path: string, name: string) => any) {
  const input = get('game-ui/common/input/text/text-input.tsx', 'TextInput');
  const checkbox = get('game-ui/common/input/toggle/checkbox/checkbox.tsx', 'Checkbox');
  const camera = get('game-ui/common/hooks/use-camera-action-barrier.tsx', 'useCameraActionBarrier');
  const tool = get('game-ui/common/hooks/use-tool-action-barrier.tsx', 'useToolActionBarrier');
  if (!input || !checkbox || typeof camera !== 'function' || typeof tool !== 'function') return false;
  NativeTextInput = input; NativeCheckbox = checkbox;
  useCameraActionBarrier = camera; useToolActionBarrier = tool;
  return true;
}

export function TextField({ value, label, placeholder, onValueChange, onEditing, onComposing, onSubmit, maxLength = 100 }: {
  value: string; label: string; placeholder: string;
  onValueChange: (value: string) => void; onEditing: (editing: boolean) => void;
  onComposing?: (composing: boolean) => void; onSubmit?: () => void; maxLength?: number;
}) {
  const composing = useRef(false);
  return <NativeTextInput className="lr-text-input" aria-label={label} value={value} maxLength={maxLength}
    placeholder={placeholder}
    // CS2 TextInput forwards a DOM change event, not a string.
    onChange={event => onValueChange(event.target.value)}
    onMouseDownCapture={() => onEditing(true)} onFocus={() => onEditing(true)} onBlur={() => onEditing(false)}
    onCompositionStart={() => { composing.current = true; onComposing?.(true); }}
    onCompositionEnd={() => { composing.current = false; onComposing?.(false); }}
    onKeyDownCapture={event => {
      // Stock TextInput blurs on Enter/Escape. Let the IME finish before that handler runs.
      if ((composing.current || event.nativeEvent.isComposing) && ['Enter', 'Escape'].includes(event.key)) event.stopPropagation();
    }}
    onKeyDown={event => {
      event.stopPropagation();
      if (event.key === 'Enter' && !composing.current && !event.nativeEvent.isComposing) onSubmit?.();
    }} onKeyUp={event => event.stopPropagation()} />;
}

export function CheckField({ checked, onChange, children }: React.PropsWithChildren<CheckProps>) {
  return <div className="lr-check" role="checkbox" aria-checked={checked} tabIndex={0}
    onClick={() => onChange(!checked)} onKeyDown={event => {
      if (event.key === ' ' || event.key === 'Enter') { event.preventDefault(); event.stopPropagation(); onChange(!checked); }
    }}>
    {/* Native Checkbox consumes its click; label clicks toggle through the wrapper. */}
    <NativeCheckbox className="lr-native-check" checked={checked} onChange={onChange} />
    <span>{children}</span>
  </div>;
}
