import type { ThemeType, BtnCfg, GamepadLayout } from './types';

export const LAYOUT_KEY = 'gamepad_layout_v2';
export const URL_KEY = 'gamepad_server_url';

export const THEMES: Record<ThemeType, Record<string, BtnCfg>> = {
  xbox: {
    // Face buttons: xbox-a=U+21D3, xbox-b=U+21D2, xbox-x=U+21D0, xbox-y=U+21D1
    A:     { label: '\u21D3', color: '#22c55e', w: 56, h: 56, round: true  },
    B:     { label: '\u21D2', color: '#ef4444', w: 56, h: 56, round: true  },
    X:     { label: '\u21D0', color: '#3b82f6', w: 56, h: 56, round: true  },
    Y:     { label: '\u21D1', color: '#eab308', w: 56, h: 56, round: true  },
    // Shoulders: xbox-left-shoulder=U+2198, xbox-right-shoulder=U+2199
    LB:    { label: '\u2198', color: '#64748b', w: 80, h: 36, round: false },
    RB:    { label: '\u2199', color: '#64748b', w: 80, h: 36, round: false },
    // Triggers: xbox-left-trigger=U+2196, xbox-right-trigger=U+2197
    LT:    { label: '\u2196', color: '#64748b', w: 80, h: 36, round: false },
    RT:    { label: '\u2197', color: '#64748b', w: 80, h: 36, round: false },
    // Dpad: xbox-dpad-up=U+227B, xbox-dpad-down=U+227D, xbox-dpad-left=U+227A, xbox-dpad-right=U+227C
    Up:    { label: '\u2200', color: '#475569', w: 44, h: 44, round: false },
    Down:  { label: '\u2202', color: '#475569', w: 44, h: 44, round: false },
    Left:  { label: '\u21FF', color: '#475569', w: 44, h: 44, round: false },
    Right: { label: '\u2201', color: '#475569', w: 44, h: 44, round: false },
    // Menu buttons: xbox-menu=U+21FB (burger), xbox-view=U+21FA (share/view)
    Start: { label: '\u21FB', color: '#475569', w: 40, h: 28, round: false },
    Back:  { label: '\u21FA', color: '#475569', w: 40, h: 28, round: false },
    // Home: gamepad-home=U+E001
    Xbox:  { label: '\uE001', color: '#16a34a', w: 38, h: 38, round: true  },
    // Stick clicks: analog-l-click=U+21BA, analog-r-click=U+21BB
    LS:    { label: '\u21BA', color: '#334155', w: 34, h: 34, round: true  },
    RS:    { label: '\u21BB', color: '#334155', w: 34, h: 34, round: true  },
  },
  ps: {
    // Face buttons: sony-a=U+21E3 (cross), sony-b=U+21E2 (circle), sony-x=U+21E0 (square), sony-y=U+21E1 (triangle)
    A:     { label: '\u21E3', color: '#3b82f6', w: 56, h: 56, round: true  },
    B:     { label: '\u21E2', color: '#ef4444', w: 56, h: 56, round: true  },
    X:     { label: '\u21E0', color: '#d946ef', w: 56, h: 56, round: true  },
    Y:     { label: '\u21E1', color: '#22c55e', w: 56, h: 56, round: true  },
    // Shoulders: sony-left-shoulder=U+21B0, sony-right-shoulder=U+21B1
    LB:    { label: '\u21B0', color: '#64748b', w: 80, h: 36, round: false },
    RB:    { label: '\u21B1', color: '#64748b', w: 80, h: 36, round: false },
    // Triggers: sony-left-trigger=U+21B2, sony-right-trigger=U+21B3
    LT:    { label: '\u21B2', color: '#64748b', w: 80, h: 36, round: false },
    RT:    { label: '\u21B3', color: '#64748b', w: 80, h: 36, round: false },
    // Dpad: generic dpad arrows (U+219E/219F/21A0/21A1 = left/up/right/down)
    Up:    { label: '\u2200', color: '#475569', w: 44, h: 44, round: false },
    Down:  { label: '\u2202', color: '#475569', w: 44, h: 44, round: false },
    Left:  { label: '\u21FF', color: '#475569', w: 44, h: 44, round: false },
    Right: { label: '\u2201', color: '#475569', w: 44, h: 44, round: false },
    // Options/Share: sony-options=U+21E8, sony-share=U+21E6
    Start: { label: '\u21E8', color: '#475569', w: 40, h: 28, round: false },
    Back:  { label: '\u21E6', color: '#475569', w: 40, h: 28, round: false },
    // Touchpad: sony-touchpad=U+21E7
    Xbox:  { label: '\u21E7', color: '#3b82f6', w: 38, h: 38, round: true  },
    // Stick clicks: sony-left-stick=U+21EF (L3), sony-right-stick=U+21F0 (R3)
    LS:    { label: '\u21EF', color: '#334155', w: 34, h: 34, round: true  },
    RS:    { label: '\u21F0', color: '#334155', w: 34, h: 34, round: true  },
  },
  nintendo: {
    // Face buttons: Nintendo layout (A right, B down, X up, Y left)
    A:     { label: '\u21D2', color: '#ef4444', w: 56, h: 56, round: true  }, // xbox-b shape for A (right)
    B:     { label: '\u21D3', color: '#eab308', w: 56, h: 56, round: true  }, // xbox-a shape for B (down)
    X:     { label: '\u21D1', color: '#22c55e', w: 56, h: 56, round: true  }, // xbox-y shape for X (up)
    Y:     { label: '\u21D0', color: '#3b82f6', w: 56, h: 56, round: true  }, // xbox-x shape for Y (left)
    // Shoulders: nintendo-left-shoulder=U+219C, nintendo-right-shoulder=U+219D
    LB:    { label: '\u219C', color: '#64748b', w: 80, h: 36, round: false },
    RB:    { label: '\u219D', color: '#64748b', w: 80, h: 36, round: false },
    // Triggers: nintendo-left-trigger=U+219A (ZL), nintendo-right-trigger=U+219B (ZR)
    LT:    { label: '\u219A', color: '#64748b', w: 80, h: 36, round: false },
    RT:    { label: '\u219B', color: '#64748b', w: 80, h: 36, round: false },
    // Dpad: Joycon dpad - U+21FF/2200/2201/2202 = left/up/right/down
    Up:    { label: '\u2200', color: '#475569', w: 44, h: 44, round: false },
    Down:  { label: '\u2202', color: '#475569', w: 44, h: 44, round: false },
    Left:  { label: '\u21FF', color: '#475569', w: 44, h: 44, round: false },
    Right: { label: '\u2201', color: '#475569', w: 44, h: 44, round: false },
    // Plus/Minus: nintendo-plus=U+21FE, nintendo-minus=U+21FD
    Start: { label: '\u21FE', color: '#475569', w: 32, h: 32, round: true  },
    Back:  { label: '\u21FD', color: '#475569', w: 32, h: 32, round: true  },
    // Home: gamepad-home=U+21F9
    Xbox:  { label: '\u221C', color: '#ef4444', w: 38, h: 38, round: true  },
    // Stick clicks: analog-l-click=U+21BA, analog-r-click=U+21BB
    LS:    { label: '\u21BA', color: '#334155', w: 34, h: 34, round: true  },
    RS:    { label: '\u21BB', color: '#334155', w: 34, h: 34, round: true  },
  }
};

export const BTN_IDS = Object.keys(THEMES.xbox);
export const STICK_IDS = ['LeftStick', 'RightStick'] as const;

export const DEFAULT_LAYOUT: GamepadLayout = {
  version: 2,
  controls: {
    LT: { x: 3, y: 3 }, LB: { x: 16, y: 3 },
    RB: { x: 74, y: 3 }, RT: { x: 87, y: 3 },
    Back: { x: 40, y: 8 }, Xbox: { x: 48, y: 3 }, Start: { x: 55, y: 8 },
    Up: { x: 20, y: 22 }, Down: { x: 20, y: 50 },
    Left: { x: 11, y: 36 }, Right: { x: 29, y: 36 },
    Y: { x: 81, y: 18 }, X: { x: 72, y: 34 },
    B: { x: 90, y: 34 }, A: { x: 81, y: 50 },
    LeftStick: { x: 12, y: 68 }, RightStick: { x: 76, y: 68 },
    LS: { x: 4, y: 90 }, RS: { x: 91, y: 90 },
  }
};

export const DEFAULT_PORTRAIT_LAYOUT: GamepadLayout = {
  version: 2,
  controls: {
    LT: { x: 10, y: 35 }, LB: { x: 28, y: 35 },
    RB: { x: 58, y: 35 }, RT: { x: 76, y: 35 },
    Back: { x: 32, y: 41 }, Xbox: { x: 48, y: 35 }, Start: { x: 60, y: 41 },
    Up: { x: 18, y: 48 }, Down: { x: 18, y: 68 },
    Left: { x: 7, y: 58 }, Right: { x: 29, y: 58 },
    Y: { x: 78, y: 48 }, X: { x: 67, y: 58 },
    B: { x: 89, y: 58 }, A: { x: 78, y: 68 },
    LeftStick: { x: 12, y: 78 }, RightStick: { x: 70, y: 78 },
    LS: { x: 4, y: 93 }, RS: { x: 88, y: 93 },
  }
};

export const JOY_LEFT_LAYOUT: GamepadLayout = {
  version: 2,
  controls: {
    LT: { x: 3, y: 3 }, LB: { x: 16, y: 3 },
    RB: { x: 74, y: 3, hidden: true }, RT: { x: 87, y: 3, hidden: true },
    Back: { x: 40, y: 8 }, Xbox: { x: 48, y: 3 }, Start: { x: 55, y: 8, hidden: true },
    Up: { x: 20, y: 22 }, Down: { x: 20, y: 50 },
    Left: { x: 11, y: 36 }, Right: { x: 29, y: 36 },
    Y: { x: 81, y: 18, hidden: true }, X: { x: 72, y: 34, hidden: true },
    B: { x: 90, y: 34, hidden: true }, A: { x: 81, y: 50, hidden: true },
    LeftStick: { x: 12, y: 68 }, RightStick: { x: 76, y: 68, hidden: true },
    LS: { x: 4, y: 90 }, RS: { x: 91, y: 90, hidden: true },
  }
};

export const JOY_LEFT_PORTRAIT_LAYOUT: GamepadLayout = {
  version: 2,
  controls: {
    LT: { x: 55, y: 4 }, LB: { x: 5, y: 4 },
    RB: { x: 74, y: 4, hidden: true }, RT: { x: 87, y: 4, hidden: true },
    Back: { x: 25, y: 14 }, Xbox: { x: 55, y: 14 }, Start: { x: 55, y: 8, hidden: true },
    Up: { x: 42, y: 48 }, Down: { x: 42, y: 68 },
    Left: { x: 20, y: 58 }, Right: { x: 64, y: 58 },
    Y: { x: 81, y: 18, hidden: true }, X: { x: 72, y: 34, hidden: true },
    B: { x: 90, y: 34, hidden: true }, A: { x: 81, y: 50, hidden: true },
    LeftStick: { x: 28, y: 26 }, RightStick: { x: 76, y: 68, hidden: true },
    LS: { x: 42, y: 82 }, RS: { x: 91, y: 90, hidden: true },
  }
};

export const JOY_RIGHT_LAYOUT: GamepadLayout = {
  version: 2,
  controls: {
    LT: { x: 3, y: 3, hidden: true }, LB: { x: 16, y: 3, hidden: true },
    RB: { x: 74, y: 3 }, RT: { x: 87, y: 3 },
    Back: { x: 40, y: 8, hidden: true }, Xbox: { x: 48, y: 3 }, Start: { x: 55, y: 8 },
    Up: { x: 20, y: 22, hidden: true }, Down: { x: 20, y: 50, hidden: true },
    Left: { x: 11, y: 36, hidden: true }, Right: { x: 29, y: 36, hidden: true },
    Y: { x: 81, y: 18 }, X: { x: 72, y: 34 },
    B: { x: 90, y: 34 }, A: { x: 81, y: 50 },
    LeftStick: { x: 12, y: 68, hidden: true }, RightStick: { x: 76, y: 68 },
    LS: { x: 4, y: 90, hidden: true }, RS: { x: 91, y: 90 },
  }
};

export const JOY_RIGHT_PORTRAIT_LAYOUT: GamepadLayout = {
  version: 2,
  controls: {
    LT: { x: 3, y: 3, hidden: true }, LB: { x: 16, y: 3, hidden: true },
    RB: { x: 5, y: 4 }, RT: { x: 55, y: 4 },
    Back: { x: 40, y: 8, hidden: true }, Xbox: { x: 25, y: 14 }, Start: { x: 55, y: 14 },
    Up: { x: 20, y: 22, hidden: true }, Down: { x: 20, y: 50, hidden: true },
    Left: { x: 11, y: 36, hidden: true }, Right: { x: 29, y: 36, hidden: true },
    Y: { x: 42, y: 20 }, X: { x: 20, y: 30 },
    B: { x: 64, y: 30 }, A: { x: 42, y: 40 },
    LeftStick: { x: 12, y: 68, hidden: true }, RightStick: { x: 28, y: 52 },
    LS: { x: 4, y: 90, hidden: true }, RS: { x: 42, y: 82 },
  }
};
