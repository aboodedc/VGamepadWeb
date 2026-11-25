import { useEffect, useState } from 'react';

type FullscreenDocument = Document & {
  webkitFullscreenElement?: Element | null;
  webkitFullscreenEnabled?: boolean;
  webkitExitFullscreen?: () => void | Promise<void>;
};
type FullscreenElement = HTMLElement & { webkitRequestFullscreen?: () => void | Promise<void> };

export function getPresentationState(doc: FullscreenDocument = document, win: Window = window) {
  const target = doc.documentElement as FullscreenElement;
  return {
    native: (doc.fullscreenElement ?? doc.webkitFullscreenElement) === target,
    supported: (typeof target.requestFullscreen === 'function' && doc.fullscreenEnabled !== false)
      || (typeof target.webkitRequestFullscreen === 'function' && doc.webkitFullscreenEnabled !== false),
    standalone: win.matchMedia('(display-mode: standalone)').matches
      || (win.navigator as Navigator & { standalone?: boolean }).standalone === true,
  };
}

export function useFullscreen() {
  const [state, setState] = useState(getPresentationState);
  const [pending, setPending] = useState(false);
  const [help, setHelp] = useState<'unsupported' | 'rejected' | 'standalone' | null>(null);
  const [error, setError] = useState('');
  useEffect(() => {
    const update = () => setState(getPresentationState());
    const failed = () => { update(); setHelp('rejected'); };
    const mode = window.matchMedia('(display-mode: standalone)');
    document.addEventListener('fullscreenchange', update);
    document.addEventListener('webkitfullscreenchange', update);
    document.addEventListener('fullscreenerror', failed);
    document.addEventListener('webkitfullscreenerror', failed);
    window.addEventListener('pageshow', update);
    mode.addEventListener('change', update);
    return () => {
      document.removeEventListener('fullscreenchange', update);
      document.removeEventListener('webkitfullscreenchange', update);
      document.removeEventListener('fullscreenerror', failed);
      document.removeEventListener('webkitfullscreenerror', failed);
      window.removeEventListener('pageshow', update);
      mode.removeEventListener('change', update);
    };
  }, []);

  // Called directly by a toolbar click, before any asynchronous work or timers.
  const toggle = async () => {
    if (pending) return;
    const current = getPresentationState();
    const doc = document as FullscreenDocument;
    const target = doc.documentElement as FullscreenElement;
    setError('');
    if (!current.native && current.standalone) { setHelp('standalone'); return; }
    if (!current.native && !current.supported) { setHelp('unsupported'); return; }
    setPending(true);
    try {
      if (current.native) {
        if (doc.exitFullscreen) await doc.exitFullscreen();
        else await doc.webkitExitFullscreen?.();
      } else if (target.requestFullscreen && doc.fullscreenEnabled !== false) {
        await target.requestFullscreen({ navigationUI: 'hide' });
      } else {
        await target.webkitRequestFullscreen?.();
      }
      setState(getPresentationState());
    } catch (cause) {
      setState(getPresentationState());
      setError(cause instanceof Error ? cause.message : String(cause));
      setHelp('rejected');
    } finally { setPending(false); }
  };
  return { ...state, pending, help, error, toggle, closeHelp: () => setHelp(null) };
}
