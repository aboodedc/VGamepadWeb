import type { ThemeType, GamepadLayout } from './types';
import { 
  LAYOUT_KEY, 
  DEFAULT_LAYOUT, 
  DEFAULT_PORTRAIT_LAYOUT, 
  JOY_LEFT_LAYOUT, 
  JOY_LEFT_PORTRAIT_LAYOUT, 
  JOY_RIGHT_LAYOUT, 
  JOY_RIGHT_PORTRAIT_LAYOUT 
} from './constants';

export function loadLayout(theme: ThemeType, profileId: string, isPortrait: boolean): GamepadLayout {
  const suffix = isPortrait ? '_portrait' : '_landscape';
  try {
    let raw = localStorage.getItem(`${LAYOUT_KEY}_${profileId}_${theme}${suffix}`);
    // Migration from old unprofiled layout (which was landscape)
    if (!raw && profileId === 'default' && !isPortrait) {
      raw = localStorage.getItem(`${LAYOUT_KEY}_${theme}`);
      if (raw) {
        localStorage.setItem(`${LAYOUT_KEY}_${profileId}_${theme}${suffix}`, raw);
      }
    }
    if (raw) { const p = JSON.parse(raw); if (p.controls) return p; }
  } catch { /* ignore */ }
  
  if (isPortrait) {
    if (profileId === 'joy_left') return JOY_LEFT_PORTRAIT_LAYOUT;
    if (profileId === 'joy_right') return JOY_RIGHT_PORTRAIT_LAYOUT;
    return DEFAULT_PORTRAIT_LAYOUT;
  } else {
    if (profileId === 'joy_left') return JOY_LEFT_LAYOUT;
    if (profileId === 'joy_right') return JOY_RIGHT_LAYOUT;
    return DEFAULT_LAYOUT;
  }
}

export function clamp(v: number, min: number, max: number) { 
  return Math.max(min, Math.min(max, v)); 
}
