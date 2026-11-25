export type GamepadCommand = 'A' | 'B' | 'X' | 'Y' | 'LB' | 'RB' | 'LT' | 'RT' | 'Up' | 'Down' | 'Left' | 'Right' | 'Start' | 'Back' | 'Xbox' | 'LS' | 'RS';
export interface Pos {
  x: number; y: number; w?: number; h?: number; round?: boolean; hidden?: boolean;
  command?: GamepadCommand;
  appearance?: 'icon' | 'pad';
  label?: string;
  color?: string;
  lockAspect?: boolean;
}
export interface GamepadLayout {
  version: number;
  controls: Record<string, Pos>;
  presetId?: 'clone-hero-drums';
  referenceCanvas?: { width: number; height: number };
}
export type ThemeType = 'xbox' | 'ps' | 'nintendo' | 'drums';
export interface BtnCfg { label: string; color: string; w: number; h: number; round: boolean; command?: GamepadCommand; appearance?: 'icon' | 'pad' }
export interface Profile { id: string; name: string }
