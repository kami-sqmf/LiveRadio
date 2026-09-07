import React, { useState } from 'react';
export function StationIcon({ name, src }: { name: string; src?: string }) {
  const [failed, setFailed] = useState('');
  // A remote image can throw in Unity before the DOM receives an error event.
  const local = src && /^coui:\/\/liveradio-icons\/[a-f0-9]{32}(?:-initial)?\.(?:png|svg)$/.test(src);
  return <span className="lr-icon" aria-hidden="true">{local && src !== failed ? <img src={src} onError={() => setFailed(src)} /> : Array.from(name.trim())[0]}</span>;
}
