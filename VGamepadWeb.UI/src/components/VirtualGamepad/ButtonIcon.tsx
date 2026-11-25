import { Drum } from 'lucide-react';
import type { ButtonIconName } from './types';

export function ButtonIcon({ name, size }: { name: ButtonIconName; size: number }) {
  const props = {
    className: 'gp-button-icon', width: size, height: size,
    'aria-hidden': true, focusable: false, strokeWidth: 1.4,
    style: { pointerEvents: 'none' as const },
  } as const;
  if (name === 'drum-pad') return <Drum {...props} />;
  return (
    <svg {...props} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round">
      {name === 'snare' ? <>
        <ellipse cx="12" cy="8" rx="9" ry="3" />
        <path d="M3 8v7c0 1.7 4 3 9 3s9-1.3 9-3V8M3 11c0 1.7 4 3 9 3s9-1.3 9-3M7 13.5v3.8M12 14v4M17 13.5v3.8" />
      </> : name === 'cymbal' ? <>
        <ellipse cx="12" cy="9" rx="10" ry="2" />
        <path d="M9 7.2c.5-2.9 5.5-2.9 6 0M12 3v3M12 11v9M9 21h6M12 20v1" />
      </> : <>
        <circle cx="12" cy="12" r="8.5" />
        <circle cx="12" cy="12" r="7" />
        <path d="M10 2V1h4v1M5 19l-2 3m16-3 2 3M3.5 10H2v4h1.5M20.5 10H22v4h-1.5M10 20.5V22h4v-1.5" />
      </>}
    </svg>
  );
}
