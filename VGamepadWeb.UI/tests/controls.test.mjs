import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'vite';

let server, createButtonInput, createDrumsLayout, getLayoutScale, loadLayout, parseLayout, getPresentationState, getButtonIds, getStickIds, THEMES, resolveProfileTheme, readInitialTheme;
before(async () => {
  server = await createServer({ server: { middlewareMode: true }, appType: 'custom', logLevel: 'error' });
  ({ createButtonInput } = await server.ssrLoadModule('/src/components/VirtualGamepad/buttonInput.ts'));
  ({ createDrumsLayout, getLayoutScale } = await server.ssrLoadModule('/src/components/VirtualGamepad/presets.ts'));
  ({ loadLayout, parseLayout, resolveProfileTheme, readInitialTheme } = await server.ssrLoadModule('/src/components/VirtualGamepad/utils.ts'));
  ({ getButtonIds, getStickIds, THEMES } = await server.ssrLoadModule('/src/components/VirtualGamepad/constants.ts'));
  ({ getPresentationState } = await server.ssrLoadModule('/src/components/VirtualGamepad/hooks/useFullscreen.ts'));
});
after(async () => { await server?.close(); delete globalThis.localStorage; });

test('two fingers on the same pad release only after the last finger', () => {
  const wire = [];
  const input = createButtonInput((command, down) => wire.push(`B:${command}:${down ? 1 : 0}`));
  input.press('kick', 'LB', 'touch-1');
  assert.deepEqual(wire, ['B:LB:1']); // Input is sent before press returns.
  input.press('kick', 'LB', 'touch-2');
  input.release('kick', 'touch-1');
  assert.deepEqual(wire, ['B:LB:1']);
  input.release('kick', 'touch-2');
  assert.deepEqual(wire, ['B:LB:1', 'B:LB:0']);
});

test('simultaneous pads and kick remain independent', () => {
  const wire = [];
  const input = createButtonInput((...args) => wire.push(args));
  input.press('red', 'B', 'touch-1'); input.press('blue', 'X', 'touch-2'); input.press('kick', 'LB', 'touch-3');
  input.release('red', 'touch-1');
  assert.deepEqual(wire, [['B', true], ['X', true], ['LB', true], ['B', false]]);
  input.releaseAll(); input.releaseAll();
  assert.deepEqual(wire.slice(4), [['X', false], ['LB', false]]);
});

test('shared commands, repeated starts, cancellation, and remapping never release the wrong command', () => {
  const wire = [];
  const input = createButtonInput((...args) => wire.push(args));
  input.press('red', 'A', 'touch-1'); input.press('red', 'B', 'touch-1');
  input.press('green', 'A', 'touch-2'); input.releaseControl('red');
  assert.deepEqual(wire, [['A', true]]);
  input.release('green', 'touch-2');
  assert.deepEqual(wire, [['A', true], ['A', false]]);
  input.press('red', 'B', 'touch-3'); input.releaseControl('red');
  assert.deepEqual(wire.slice(2), [['B', true], ['B', false]]);
});

test('legacy layouts, profile keys, and legacy unprofiled migration keep their values', () => {
  const entries = new Map();
  globalThis.localStorage = { getItem: key => entries.get(key) ?? null, setItem: (key, value) => entries.set(key, value) };
  const old = { version: 2, controls: { A: { x: 17, y: 42, w: 80, h: 48, hidden: false } } };
  const raw = JSON.stringify(old);
  entries.set('gamepad_layout_v2_xbox', raw);
  assert.deepEqual(loadLayout('xbox', 'default'), old);
  assert.equal(entries.get('gamepad_layout_v2_default_xbox'), raw);
  assert.equal(entries.get('gamepad_layout_v2_xbox'), raw);
  assert.equal(loadLayout('xbox', 'another').controls.A.command, undefined);
  const fallback = loadLayout('xbox', 'another'); fallback.controls.A.x = 0;
  assert.notEqual(loadLayout('xbox', 'another').controls.A.x, 0);
});

test('invalid saved data is backed up and invalid imports are rejected', () => {
  const entries = new Map([['gamepad_layout_v2_p_xbox', '{invalid']]);
  globalThis.localStorage = { getItem: key => entries.get(key) ?? null, setItem: (key, value) => entries.set(key, value) };
  loadLayout('xbox', 'p');
  assert.equal(entries.get('gamepad_layout_v2_p_xbox_backup'), '{invalid');
  for (const controls of [[], { A: { x: 0, y: 0, command: 'DROP' } }, { A: { x: 0, y: 0, w: Infinity } }, { A: { x: 0, y: 0, hidden: 'yes' } }]) {
    assert.throws(() => parseLayout({ version: 2, controls }));
  }
});

test('drums have a dedicated control catalog, five colored pads and a small Xbox Start', () => {
  const drums = createDrumsLayout();
  const visible = Object.entries(drums.controls).filter(([, pos]) => !pos.hidden);
  assert.equal(visible.length, 6);
  assert.deepEqual(getButtonIds('drums'), ['DrumRed', 'DrumYellow', 'DrumBlue', 'DrumGreen', 'Kick', 'Start']);
  assert.deepEqual(getStickIds('drums'), []);
  assert.ok(getButtonIds('xbox').includes('LB'));
  assert.equal(getStickIds('xbox').length, 2);
  assert.deepEqual(visible.filter(([id]) => id !== 'Start').map(([, pos]) => pos.color).sort(), ['#ef4444', '#facc15', '#3b82f6', '#22c55e', '#f97316'].sort());
  assert.ok(drums.controls.Kick.w > 4 * drums.controls.Kick.h);
  assert.equal(drums.controls.Kick.round, false);
  assert.equal(drums.controls.Start.command, 'Start');
  assert.equal(drums.controls.Start.label, '☰');
  assert.ok(drums.controls.Start.w < drums.controls.DrumRed.w);
  const wire = [];
  const input = createButtonInput((command, pressed) => wire.push(`B:${command}:${pressed ? 1 : 0}`));
  input.press('Start', THEMES.drums.Start.command, 'touch-1');
  input.release('Start', 'touch-1');
  assert.deepEqual(wire, ['B:Start:1', 'B:Start:0']);
  assert.deepEqual(parseLayout(JSON.parse(JSON.stringify(drums))), drums);
  for (const [width, height] of [[667, 290], [844, 320], [932, 350], [390, 720]]) {
    const scale = getLayoutScale(drums, width, height);
    for (const [, pos] of visible) {
      assert.ok(pos.x / 100 * width + pos.w * scale <= width);
      assert.ok(pos.y / 100 * height + pos.h * scale <= height);
    }
  }
});

test('legacy battery profiles migrate with their edits, while Xbox stays a separate controller', () => {
  const legacy = { version: 2, presetId: 'clone-hero-drums', referenceCanvas: { width: 800, height: 320 }, controls: {
    B: { x: 12, y: 18, w: 170, h: 100, command: 'RT', color: '#ef4444', appearance: 'pad' },
    Y: { x: 27, y: 15 }, X: { x: 55, y: 15 }, A: { x: 75, y: 15 },
    LB: { x: 20, y: 65, w: 520, h: 64, command: 'LT' }, Start: { x: 55, y: 8, hidden: true },
    LeftStick: { x: 12, y: 68, hidden: true },
  } };
  const raw = JSON.stringify(legacy);
  const entries = new Map([['gamepad_layout_v2_p_xbox', raw], ['gamepad_theme', 'xbox'], ['gamepad_active_profile', 'p']]);
  globalThis.localStorage = { getItem: key => entries.get(key) ?? null, setItem: (key, value) => entries.set(key, value) };
  assert.equal(resolveProfileTheme('xbox', 'p'), 'drums');
  assert.equal(readInitialTheme(), 'drums');
  const migrated = loadLayout('drums', 'p');
  assert.equal(migrated.controls.DrumRed.command, 'RT');
  assert.equal(migrated.controls.DrumRed.x, 12);
  assert.equal(migrated.controls.Kick.w, 520);
  assert.equal(migrated.controls.Kick.command, 'LT');
  assert.equal(migrated.controls.Start.hidden, undefined);
  assert.equal(migrated.controls.Start.command, 'Start');
  assert.deepEqual(Object.keys(migrated.controls), getButtonIds('drums'));
  assert.equal(entries.get('gamepad_layout_v2_p_xbox'), raw);
  const xbox = loadLayout('xbox', 'p');
  assert.equal(xbox.presetId, undefined);
  assert.ok(xbox.controls.LeftStick);
  assert.equal(entries.get('gamepad_layout_v2_p_xbox_legacy_drums'), raw);
  entries.set('gamepad_layout_v2_p_xbox', JSON.stringify(xbox));
  assert.equal(loadLayout('drums', 'p').controls.Kick.w, 520);
  entries.set('gamepad_layout_v2_p_drums', JSON.stringify(migrated));
  assert.deepEqual(loadLayout('drums', 'p'), migrated);
  assert.equal(readInitialTheme(), 'xbox');
});

test('presentation distinguishes unsupported, permitted, native, prefixed, and standalone', () => {
  const root = {};
  const doc = { documentElement: root, fullscreenElement: null, fullscreenEnabled: false };
  const win = { navigator: {}, matchMedia: () => ({ matches: false }) };
  assert.deepEqual(getPresentationState(doc, win), { native: false, supported: false, standalone: false });
  root.requestFullscreen = () => {};
  assert.equal(getPresentationState(doc, win).supported, false);
  doc.fullscreenEnabled = true;
  assert.equal(getPresentationState(doc, win).supported, true);
  assert.equal(getPresentationState(doc, win).native, false);
  doc.fullscreenElement = root;
  assert.equal(getPresentationState(doc, win).native, true);
  doc.fullscreenElement = null; doc.fullscreenEnabled = false;
  root.webkitRequestFullscreen = () => {}; doc.webkitFullscreenEnabled = true; doc.webkitFullscreenElement = root;
  assert.equal(getPresentationState(doc, win).native, true);
  assert.equal(getPresentationState(doc, win).supported, true);
  win.navigator.standalone = true;
  assert.equal(getPresentationState(doc, win).standalone, true);
  delete win.navigator.standalone;
  win.matchMedia = query => ({ matches: query === '(display-mode: fullscreen)' });
  assert.equal(getPresentationState(doc, win).standalone, false);
  win.matchMedia = query => ({ matches: query === '(display-mode: standalone)' });
  assert.equal(getPresentationState(doc, win).standalone, true);
});
