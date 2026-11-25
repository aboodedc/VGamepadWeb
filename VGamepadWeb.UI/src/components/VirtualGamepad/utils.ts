import type { ThemeType, GamepadLayout } from './types';
import { LAYOUT_KEY, THEMES, BTN_IDS, STICK_IDS, DEFAULT_LAYOUT, isDrumsTheme } from './constants';
import { getLayoutDefaults, migrateDrumsLayout, restoreProDrumsLayout } from './presets';

export function parseLayout(value: unknown): GamepadLayout {
  if (!value || typeof value !== 'object' || !('controls' in value)) throw new Error('Invalid layout');
  const layout = value as GamepadLayout;
  if (!layout.controls || typeof layout.controls !== 'object' || Array.isArray(layout.controls)) throw new Error('Invalid controls');
  for (const [id, pos] of Object.entries(layout.controls)) {
    if (![...Object.values(THEMES).flatMap(controls => Object.keys(controls)), ...STICK_IDS].includes(id)) throw new Error('Unknown control');
    if (!pos || !Number.isFinite(pos.x) || !Number.isFinite(pos.y)) throw new Error('Invalid position');
    if ([pos.w, pos.h].some(size => size !== undefined && (!Number.isFinite(size) || size < 24 || size > 4096))) throw new Error('Invalid size');
    if (pos.command !== undefined && !BTN_IDS.includes(pos.command)) throw new Error('Invalid command');
    if (pos.appearance !== undefined && !['icon', 'pad'].includes(pos.appearance)) throw new Error('Invalid appearance');
    if (pos.label !== undefined && (typeof pos.label !== 'string' || pos.label.length > 40)) throw new Error('Invalid label');
    if (pos.color !== undefined && (typeof pos.color !== 'string' || !/^#[\da-f]{6}$/i.test(pos.color))) throw new Error('Invalid color');
    if (pos.icon !== undefined && pos.icon !== null && !['snare', 'drum-pad', 'kick', 'cymbal'].includes(pos.icon)) throw new Error('Invalid icon');
    for (const field of ['round', 'hidden', 'lockAspect', 'showLabel'] as const) {
      if (pos[field] !== undefined && typeof pos[field] !== 'boolean') throw new Error('Invalid flag');
    }
  }
  if (layout.presetId !== undefined && !['clone-hero-drums', 'clone-hero-pro-drums'].includes(layout.presetId)) throw new Error('Invalid preset');
  if (layout.referenceCanvas && (!Number.isFinite(layout.referenceCanvas.width) || !Number.isFinite(layout.referenceCanvas.height)
    || layout.referenceCanvas.width <= 0 || layout.referenceCanvas.height <= 0)) throw new Error('Invalid canvas');
  return { ...layout, version: layout.version ?? 2 };
}

export function loadLayout(theme: ThemeType, profileId: string): GamepadLayout {
  const key = `${LAYOUT_KEY}_${profileId}_${theme}`;
  let raw: string | null = null;
  try {
    raw = localStorage.getItem(key);
    // Migration from old unprofiled layout
    if (!raw && profileId === 'default') {
      raw = localStorage.getItem(`${LAYOUT_KEY}_${theme}`);
      if (raw) {
        localStorage.setItem(key, raw);
      }
    }
    if (raw) {
      const layout = parseLayout(JSON.parse(raw));
      if (theme === 'pro-drums') {
        if (layout.presetId !== 'clone-hero-pro-drums') throw new Error('Invalid Pro Drums layout');
        return restoreProDrumsLayout(layout);
      }
      if (theme === 'drums') return migrateDrumsLayout(layout);
      if (layout.presetId === 'clone-hero-drums') {
        localStorage.setItem(`${key}_legacy_drums`, raw);
        return getLayoutDefaults(layout, theme);
      }
      return layout;
    }
    if (theme === 'drums') {
      const preferred = localStorage.getItem('gamepad_theme');
      const variants = [...new Set([preferred, 'xbox', 'ps', 'nintendo'])].filter(variant => variant && variant !== 'drums' && variant !== 'pro-drums');
      for (const variant of variants) {
        const legacyKey = `${LAYOUT_KEY}_${profileId}_${variant}`;
        const candidates = [localStorage.getItem(legacyKey), localStorage.getItem(`${legacyKey}_legacy_drums`),
          profileId === 'default' ? localStorage.getItem(`${LAYOUT_KEY}_${variant}`) : null];
        for (const legacy of candidates) {
          if (!legacy) continue;
          try {
            const layout = parseLayout(JSON.parse(legacy));
            if (layout.presetId === 'clone-hero-drums') return migrateDrumsLayout(layout);
          } catch { /* Another theme can still contain a valid legacy battery layout. */ }
        }
      }
    }
  } catch {
    // Retain the original before the UI can persist a fallback over it.
    if (raw) {
      try { localStorage.setItem(`${key}_backup`, raw); } catch { /* Storage may be full. */ }
    }
  }
  return getLayoutDefaults(DEFAULT_LAYOUT, theme);
}

export function resolveProfileTheme(theme: ThemeType, profileId: string): ThemeType {
  if (isDrumsTheme(theme)) return theme;
  try {
    const raw = localStorage.getItem(`${LAYOUT_KEY}_${profileId}_${theme}`)
      ?? (profileId === 'default' ? localStorage.getItem(`${LAYOUT_KEY}_${theme}`) : null);
    if (raw && JSON.parse(raw).presetId === 'clone-hero-drums') return 'drums';
  } catch { /* The loader handles malformed saved data and its backup. */ }
  return theme;
}

export function readInitialTheme(): ThemeType {
  const saved = localStorage.getItem('gamepad_theme');
  const theme = saved && Object.hasOwn(THEMES, saved) ? saved as ThemeType : 'xbox';
  return resolveProfileTheme(theme, localStorage.getItem('gamepad_active_profile') || 'default');
}

export function clamp(v: number, min: number, max: number) { 
  return Math.max(min, Math.min(max, v)); 
}
