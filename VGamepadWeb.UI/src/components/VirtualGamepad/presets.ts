import { DEFAULT_LAYOUT, THEMES } from './constants';
import type { GamepadLayout, Pos, ThemeType } from './types';

export function createDrumsLayout(): GamepadLayout {
  const controls: Record<string, Pos> = {};
  const positions: Record<string, { x: number; y: number }> = {
    DrumRed: { x: 5.6, y: 22 }, DrumYellow: { x: 19.2, y: 1.1 },
    DrumBlue: { x: 49.8, y: 0.2 }, DrumGreen: { x: 80.8, y: 24.3 },
    Kick: { x: 9.5, y: 70 }, Start: { x: 89.7, y: 0.4 },
  };
  for (const [id, cfg] of Object.entries(THEMES.drums)) {
    controls[id] = { ...positions[id], ...cfg, lockAspect: cfg.round };
  }
  return { version: 2, presetId: 'clone-hero-drums', referenceCanvas: { width: 1000, height: 428 }, controls };
}

export function getLayoutDefaults(layout: GamepadLayout, theme?: ThemeType): GamepadLayout {
  return (theme ? theme === 'drums' : layout.presetId === 'clone-hero-drums') ? createDrumsLayout() : structuredClone(DEFAULT_LAYOUT);
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
    }
  }
  return { ...layout, ...defaults, referenceCanvas: layout.referenceCanvas ?? defaults.referenceCanvas };
}

export function getLayoutScale(layout: GamepadLayout, width: number, height: number): number {
  if (!layout.referenceCanvas || width <= 0 || height <= 0) return 1;
  return Math.min(width / layout.referenceCanvas.width, height / layout.referenceCanvas.height);
}
