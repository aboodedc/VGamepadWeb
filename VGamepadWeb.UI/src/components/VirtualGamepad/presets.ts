import { DEFAULT_LAYOUT, THEMES } from './constants';
import type { GamepadLayout, Pos, ThemeType } from './types';

export function createDrumsLayout(): GamepadLayout {
  return createDrumsPreset('drums', {
    DrumRed: { x: 5.6, y: 22 }, DrumYellow: { x: 19.2, y: 1.1 },
    DrumBlue: { x: 49.8, y: 0.2 }, DrumGreen: { x: 80.8, y: 24.3 },
    Kick: { x: 9.5, y: 70 }, Start: { x: 89.7, y: 0.4 },
  });
}

export function createProDrumsLayout(): GamepadLayout {
  return createDrumsPreset('pro-drums', {
    DrumRed: { x: 4, y: 42 }, DrumYellow: { x: 20, y: 24 },
    DrumBlue: { x: 49, y: 24 }, DrumGreen: { x: 79, y: 42 },
    CymbalYellow: { x: 26, y: 2 }, CymbalBlue: { x: 55, y: 2 }, CymbalGreen: { x: 79, y: 18 },
    Kick: { x: 6, y: 82 }, Start: { x: 92, y: 1 },
  });
}

function createDrumsPreset(theme: 'drums' | 'pro-drums', positions: Record<string, { x: number; y: number }>): GamepadLayout {
  const controls: Record<string, Pos> = {};
  for (const [id, cfg] of Object.entries(THEMES[theme])) {
    controls[id] = { ...positions[id], ...cfg, lockAspect: cfg.round };
  }
  return { version: 2, presetId: theme === 'drums' ? 'clone-hero-drums' : 'clone-hero-pro-drums', referenceCanvas: { width: 1000, height: 428 }, controls };
}

export function getPresetTheme(layout: GamepadLayout): 'drums' | 'pro-drums' | null {
  if (layout.presetId === 'clone-hero-drums') return 'drums';
  if (layout.presetId === 'clone-hero-pro-drums') return 'pro-drums';
  return null;
}

export function getLayoutDefaults(layout: GamepadLayout, theme?: ThemeType): GamepadLayout {
  switch (theme ?? getPresetTheme(layout)) {
    case 'drums': return createDrumsLayout();
    case 'pro-drums': return createProDrumsLayout();
    default: return structuredClone(DEFAULT_LAYOUT);
  }
}

export function restoreProDrumsLayout(layout: GamepadLayout): GamepadLayout {
  const defaults = createProDrumsLayout();
  for (const id of Object.keys(defaults.controls)) {
    defaults.controls[id] = { ...defaults.controls[id], ...layout.controls[id] };
  }
  return { ...layout, ...defaults, referenceCanvas: layout.referenceCanvas ?? defaults.referenceCanvas };
}

// Old battery profiles are read without changing their original Xbox/PS/Nintendo records.
export function migrateDrumsLayout(layout: GamepadLayout): GamepadLayout {
  const defaults = createDrumsLayout();
  const legacyIds: Record<string, string> = { DrumRed: 'B', DrumYellow: 'Y', DrumBlue: 'X', DrumGreen: 'A', Kick: 'LB' };
  const dedicated = ['DrumRed', 'DrumYellow', 'DrumBlue', 'DrumGreen', 'Kick'].some(id => id in layout.controls);
  for (const id of Object.keys(defaults.controls)) {
    const source = layout.controls[dedicated ? id : legacyIds[id] ?? id];
    if (source && (id !== 'Start' || dedicated || source.hidden === false)) {
      defaults.controls[id] = { ...defaults.controls[id], ...source };
      // New preset artwork must not change an existing text-only layout.
      if (source.icon === undefined) delete defaults.controls[id].icon;
    }
  }
  return { ...layout, ...defaults, referenceCanvas: layout.referenceCanvas ?? defaults.referenceCanvas };
}

export function getLayoutScale(layout: GamepadLayout, width: number, height: number): number {
  if (!layout.referenceCanvas || width <= 0 || height <= 0) return 1;
  return Math.min(width / layout.referenceCanvas.width, height / layout.referenceCanvas.height);
}
