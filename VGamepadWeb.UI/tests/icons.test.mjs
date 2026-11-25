import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { createServer } from 'vite';

let server, createDrumsLayout, loadLayout, parseLayout, GpButton, ButtonIcon;
before(async () => {
  server = await createServer({ server: { middlewareMode: true, ws: false }, appType: 'custom', logLevel: 'error' });
  ({ createDrumsLayout } = await server.ssrLoadModule('/src/components/VirtualGamepad/presets.ts'));
  ({ loadLayout, parseLayout } = await server.ssrLoadModule('/src/components/VirtualGamepad/utils.ts'));
  ({ GpButton } = await server.ssrLoadModule('/src/components/VirtualGamepad/GpButton.tsx'));
  ({ ButtonIcon } = await server.ssrLoadModule('/src/components/VirtualGamepad/ButtonIcon.tsx'));
});
after(async () => { await server?.close(); delete globalThis.localStorage; });

function storage(entries = new Map()) {
  globalThis.localStorage = { getItem: key => entries.get(key) ?? null, setItem: (key, value) => entries.set(key, value) };
  return entries;
}

test('icons, icon-only mode and explicit removal survive storage and JSON round trips', () => {
  const entries = storage();
  const layout = createDrumsLayout();
  layout.controls.DrumRed.icon = null;
  layout.controls.DrumYellow.showLabel = false;
  layout.controls.DrumBlue.icon = 'cymbal';
  layout.controls.Kick.command = 'RT';
  entries.set('gamepad_layout_v2_custom_drums', JSON.stringify(layout));
  assert.deepEqual(loadLayout('drums', 'custom'), layout);
  assert.deepEqual(parseLayout(JSON.parse(JSON.stringify(layout))), layout);
  assert.equal(loadLayout('drums', 'fresh').controls.DrumRed.icon, 'snare');
  assert.equal(loadLayout('drums', 'fresh').controls.Kick.icon, 'kick');
});

test('four distinct icon drawings remain decorative and scalable', () => {
  const drawings = ['snare', 'drum-pad', 'kick', 'cymbal'].map(name => {
    const markup = renderToStaticMarkup(createElement(ButtonIcon, { name, size: 48 }));
    assert.match(markup, /<svg[^>]+width="48"[^>]+height="48"/);
    assert.match(markup, /aria-hidden="true"/);
    assert.match(markup, /focusable="false"/);
    assert.match(markup, /pointer-events:none/);
    return markup;
  });
  assert.equal(new Set(drawings).size, 4);
});

test('existing drums and console configurations retain their text-only appearance and geometry', () => {
  const layout = createDrumsLayout();
  for (const pos of Object.values(layout.controls)) delete pos.icon;
  Object.assign(layout.controls.DrumBlue, { x: 38, y: 12, w: 177, h: 83, command: 'LT', round: false, label: 'Custom' });
  const consoleLayout = { version: 2, controls: { A: { x: 12, y: 34, w: 56, h: 56 } } };
  storage(new Map([
    ['gamepad_layout_v2_custom_drums', JSON.stringify(layout)],
    ['gamepad_layout_v2_custom_xbox', JSON.stringify(consoleLayout)],
  ]));
  assert.deepEqual(loadLayout('drums', 'custom'), layout);
  assert.deepEqual(loadLayout('xbox', 'custom'), consoleLayout);
});

test('imports accept the supported icons and reject invalid icon or text settings', () => {
  for (const icon of ['snare', 'drum-pad', 'kick', 'cymbal', null, undefined]) {
    assert.doesNotThrow(() => parseLayout({ version: 2, controls: { A: { x: 0, y: 0, icon } } }));
  }
  for (const patch of [{ icon: 'unknown-icon' }, { icon: '<svg onload="alert(1)" />' }, { icon: {} }, { showLabel: 'false' }]) {
    assert.throws(() => parseLayout({ version: 2, controls: { A: { x: 0, y: 0, ...patch } } }));
  }
});

test('circular and rectangular buttons render decorative scalable SVGs without changing control bounds', () => {
  const drums = createDrumsLayout();
  const render = (id, patch = {}, scale = 1) => {
    const cfg = drums.controls[id];
    return renderToStaticMarkup(createElement(GpButton, {
      id, cfg, pos: { ...cfg, ...patch }, scale,
      editMode: false, active: false, isSelected: false, showHitboxes: false,
      canvasRef: { current: null }, onDrag() {}, onPress() {}, onRelease() {}, onSelect() {},
    }));
  };
  const circular = render('DrumRed');
  assert.match(circular, /gp-with-icon.*round/);
  assert.match(circular, /<svg[^>]+aria-hidden="true"[^>]+focusable="false"/);
  assert.match(circular, /pointer-events:none/);
  assert.match(circular, /<span>RED<\/span>/);
  assert.match(circular, /width:136px;height:136px/);
  const rectangular = render('Kick', {}, 0.5);
  assert.match(rectangular, /gp-icon-horizontal.*rect/);
  assert.match(rectangular, /width:406px;height:40px/);
  assert.match(rectangular, /<svg[^>]+width="24\.8"/);
  assert.doesNotMatch(render('DrumRed', { showLabel: false }), /<span>RED<\/span>/);
  assert.doesNotMatch(render('DrumRed', { icon: null }), /<svg/);
  assert.match(render('DrumRed', { icon: null, showLabel: false }), /RED/);
});
