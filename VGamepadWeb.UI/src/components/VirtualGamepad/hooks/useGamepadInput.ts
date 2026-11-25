import { useEffect, useMemo, useState } from 'react';
import { createButtonInput } from '../buttonInput';

export function useGamepadInput(send: (command: string, pressed: boolean) => void) {
  const [activeButtons, setActiveButtons] = useState<Set<string>>(() => new Set());
  const input = useMemo(() => createButtonInput(send, setActiveButtons), [send]);
  useEffect(() => {
    const release = () => input.releaseAll();
    const visibility = () => { if (document.hidden) release(); };
    window.addEventListener('blur', release);
    window.addEventListener('pagehide', release);
    document.addEventListener('visibilitychange', visibility);
    return () => {
      window.removeEventListener('blur', release);
      window.removeEventListener('pagehide', release);
      document.removeEventListener('visibilitychange', visibility);
      release();
    };
  }, [input]);
  return { activeButtons, input };
}
