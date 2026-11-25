export type GamepadCommand = 'A' | 'B' | 'X' | 'Y' | 'LB' | 'RB' | 'LT' | 'RT' | 'Up' | 'Down' | 'Left' | 'Right' | 'Start' | 'Back' | 'Xbox' | 'LS' | 'RS';
export type ButtonIconName = 'snare' | 'drum-pad' | 'kick' | 'cymbal';
export interface Pos {
  x: number; y: number; w?: number; h?: number; round?: boolean; hidden?: boolean;
  command?: GamepadCommand;
  appearance?: 'icon' | 'pad';
  label?: string;
  color?: string;
  lockAspect?: boolean;
  // null explicitly removes a preset icon; missing fields keep legacy controls text-only.
  icon?: ButtonIconName | null;
  showLabel?: boolean;
}
export interface GamepadLayout {
  version: number;
  controls: Record<string, Pos>;
  presetId?: 'clone-hero-drums' | 'clone-hero-pro-drums';
  referenceCanvas?: { width: number; height: number };
}
export type ThemeType = 'xbox' | 'ps' | 'nintendo' | 'drums' | 'pro-drums';
export interface BtnCfg { label: string; color: string; w: number; h: number; round: boolean; command?: GamepadCommand; appearance?: 'icon' | 'pad'; icon?: ButtonIconName }
export interface Profile { id: string; name: string }
