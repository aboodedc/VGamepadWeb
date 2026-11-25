import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createServer } from 'vite';

let server, createDrumsLayout, createProDrumsLayout, getLayoutDefaults, getLayoutScale, getPresetTheme,
  restoreProDrumsLayout, loadLayout, parseLayout, readInitialTheme, resolveProfileTheme,
  getButtonIds, getStickIds, createButtonInput;
before(async () => {
  server = await createServer({ server: { middlewareMode: true, ws: false }, appType: 'custom', logLevel: 'error' });
  ({ createDrumsLayout, createProDrumsLayout, getLayoutDefaults, getLayoutScale, getPresetTheme, restoreProDrumsLayout }
    = await server.ssrLoadModule('/src/components/VirtualGamepad/presets.ts'));
  ({ loadLayout, parseLayout, readInitialTheme, resolveProfileTheme } = await server.ssrLoadModule('/src/components/VirtualGamepad/utils.ts'));
  ({ getButtonIds, getStickIds } = await server.ssrLoadModule('/src/components/VirtualGamepad/constants.ts'));
  ({ createButtonInput } = await server.ssrLoadModule('/src/components/VirtualGamepad/buttonInput.ts'));
});
after(async () => { await server?.close(); delete globalThis.localStorage; });

function storage(entries = new Map()) {
  globalThis.localStorage = { getItem: key => entries.get(key) ?? null, setItem: (key, value) => entries.set(key, value) };
  return entries;
}

test('Pro Drums has eight distinct playable Xbox commands plus independent Start', async () => {
  const pro = createProDrumsLayout();
  const ids = getButtonIds('pro-drums');
  assert.equal(ids.length, 9);
  assert.equal(ids.filter(id => id !== 'Start').length, 8);
  assert.deepEqual(getStickIds('pro-drums'), []);
  assert.equal(pro.presetId, 'clone-hero-pro-drums');
  const commands = Object.values(pro.controls).map(pos => pos.command);
  assert.equal(new Set(commands).size, 9);
  // Verify default commands against the actual emulation map, without changing the backend.
  const backend = await readFile(new URL('../../VGamepadWeb.Core/GamepadManager.cs', import.meta.url), 'utf8');
  const xboxMap = backend.match(/_buttonMapXbox[\s\S]*?\n        };/)[0];
  const supported = new Set([...xboxMap.matchAll(/\{"([^"]+)",/g)].map(match => match[1]));
  for (const command of commands) assert.ok(supported.has(command), `Unsupported Xbox button: ${command}`);
  for (const color of ['Yellow', 'Blue', 'Green']) {
    assert.notEqual(pro.controls[`Cymbal${color}`].command, pro.controls[`Drum${color}`].command);
    assert.equal(pro.controls[`Cymbal${color}`].icon, 'cymbal');
  }
});

test('the existing simple Drums preset retains its exact geometry and command assignments', () => {
  const simple = createDrumsLayout();
  const geometry = Object.fromEntries(Object.entries(simple.controls).map(([id, pos]) => [id, [pos.x, pos.y, pos.w, pos.h, pos.command]]));
  assert.deepEqual(geometry, {
    DrumRed: [5.6, 22, 136, 136, 'B'], DrumYellow: [19.2, 1.1, 302, 302, 'Y'],
    DrumBlue: [49.8, 0.2, 310, 310, 'X'], DrumGreen: [80.8, 24.3, 138, 138, 'A'],
    Kick: [9.5, 70, 812, 80, 'LB'], Start: [89.7, 0.4, 48, 34, 'Start'],
  });
  const before = structuredClone(simple);
  const pro = createProDrumsLayout();
  pro.controls.DrumYellow.w = 24;
  pro.controls.Kick.command = 'RT';
  assert.deepEqual(createDrumsLayout(), before);
  assert.equal(getButtonIds('drums').length, 6);
});

test('simple Drums, Pro Drums and console layouts persist independently across profiles', () => {
  const simple = createDrumsLayout();
  Object.assign(simple.controls.DrumRed, { x: 13, w: 154, icon: null });
  const pro = createProDrumsLayout();
  Object.assign(pro.controls.CymbalYellow, { x: 28, w: 160, h: 80, command: 'RT', hidden: true, round: false, icon: 'snare', showLabel: false });
  const consoleLayout = { version: 2, controls: { A: { x: 12, y: 34, w: 64, h: 48 } } };
  const entries = storage(new Map([
    ['gamepad_layout_v2_p_drums', JSON.stringify(simple)],
    ['gamepad_layout_v2_p_pro-drums', JSON.stringify(pro)],
    ...['xbox', 'ps', 'nintendo'].map(theme => [`gamepad_layout_v2_p_${theme}`, JSON.stringify(consoleLayout)]),
  ]));
  const before = [...entries];
  assert.deepEqual(loadLayout('drums', 'p'), simple);
  assert.deepEqual(loadLayout('pro-drums', 'p'), pro);
  assert.deepEqual([...entries], before);
  assert.deepEqual(loadLayout('pro-drums', 'fresh'), createProDrumsLayout());
  assert.deepEqual(loadLayout('drums', 'fresh'), createDrumsLayout());
  assert.equal(resolveProfileTheme('pro-drums', 'p'), 'pro-drums');
  entries.set('gamepad_theme', 'pro-drums'); entries.set('gamepad_active_profile', 'p');
  assert.equal(readInitialTheme(), 'pro-drums');
  entries.set('gamepad_theme', 'drums');
  assert.equal(readInitialTheme(), 'drums');
});

test('preset import, completion and reset restore Pro Drums without changing the simple preset', () => {
  const pro = createProDrumsLayout();
  assert.deepEqual(parseLayout(JSON.parse(JSON.stringify(pro))), pro);
  assert.equal(getPresetTheme(pro), 'pro-drums');
  assert.equal(getPresetTheme(createDrumsLayout()), 'drums');
  const edited = structuredClone(pro);
  edited.controls.Kick.command = 'RT'; edited.controls.DrumBlue.hidden = true;
  edited.controls.CymbalYellow.icon = null;
  delete edited.controls.CymbalBlue;
  const restored = restoreProDrumsLayout(edited);
  assert.deepEqual(restored.controls.CymbalBlue, pro.controls.CymbalBlue);
  assert.equal(restored.controls.Kick.command, 'RT');
  assert.equal(restored.controls.CymbalYellow.icon, null);
  assert.deepEqual(getLayoutDefaults(edited), pro);
  assert.deepEqual(getLayoutDefaults(edited, 'drums'), createDrumsLayout());
  storage(new Map([['gamepad_layout_v2_p_xbox', JSON.stringify(createDrumsLayout())]]));
  assert.deepEqual(loadLayout('pro-drums', 'p'), pro); // No legacy simple-Drums migration into Pro.
});

test('Pro Drums touch rectangles fit and never overlap across landscape and portrait canvases', () => {
  const pro = createProDrumsLayout();
  for (const [width, height] of [[1000, 428], [1073, 459], [844, 358], [667, 290], [932, 350], [740, 300], [390, 720]]) {
    const scale = getLayoutScale(pro, width, height);
    const boxes = Object.entries(pro.controls).map(([id, pos]) => ({
      id, x: pos.x / 100 * width, y: pos.y / 100 * height, w: pos.w * scale, h: pos.h * scale,
    }));
    for (const [i, box] of boxes.entries()) {
      assert.ok(box.x >= 0 && box.y >= 0 && box.x + box.w <= width && box.y + box.h <= height, `Bounds: ${box.id}`);
      for (const other of boxes.slice(i + 1)) {
        const overlaps = box.x < other.x + other.w && box.x + box.w > other.x && box.y < other.y + other.h && box.y + box.h > other.y;
        assert.equal(overlaps, false, `${box.id} overlaps ${other.id} at ${width}x${height}`);
      }
    }
  }
});

test('all eight Pro Drums notes send immediately and release independently through the existing input system', () => {
  const notes = Object.entries(createProDrumsLayout().controls).filter(([id]) => id !== 'Start');
  const wire = [];
  const input = createButtonInput((command, down) => wire.push(`B:${command}:${down ? 1 : 0}`));
  for (const [i, [id, pos]] of notes.entries()) {
    input.press(id, pos.command, `touch-${i}`);
    assert.equal(wire.at(-1), `B:${pos.command}:1`);
  }
  assert.equal(wire.length, 8);
  input.release('CymbalYellow', 'touch-4');
  assert.deepEqual(wire.slice(8), ['B:RB:0']);
  input.releaseAll(); input.releaseAll();
  assert.equal(wire.length, 16);
  for (const [, pos] of notes) assert.equal(wire.filter(msg => msg === `B:${pos.command}:0`).length, 1);
});
