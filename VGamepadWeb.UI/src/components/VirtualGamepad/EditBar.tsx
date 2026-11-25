import React from 'react';
import type { GamepadLayout, Pos, ThemeType } from './types';
import { BTN_IDS, STICK_IDS, THEMES } from './constants';
import { useLanguage } from './LanguageContext';

interface EditBarProps {
  editMode: boolean;
  selectedControl: string | null;
  layout: GamepadLayout;
  theme: ThemeType;
  maxWidth: number;
  maxHeight: number;
  setDimension: (id: string, axis: 'w' | 'h', value: number) => void;
  patchControl: (id: string, patch: Partial<Pos>) => void;
  nudgePos: (id: string, dx: number, dy: number) => void;
  resetControl: (id: string) => void;
  setSelectedControl: (id: string | null) => void;
}

export const EditBar: React.FC<EditBarProps> = ({ editMode, selectedControl, layout, theme,
  maxWidth, maxHeight, setDimension, patchControl, nudgePos, resetControl, setSelectedControl }) => {
  const { t } = useLanguage();
  if (!editMode) return null;
  if (!selectedControl) return <div className="gp-edit-bar"><div className="gp-edit-hint">{t.editHint}</div></div>;
  const id = selectedControl;
  const pos = layout.controls[id] ?? { x: 50, y: 50 };
  const isStick = (STICK_IDS as readonly string[]).includes(id);
  const cfg = THEMES[theme][id];
  const w = pos.w ?? (isStick ? 120 : cfg.w);
  const h = pos.h ?? (isStick ? 120 : cfg.h);
  const round = pos.round ?? cfg?.round ?? true;
  const locked = pos.lockAspect ?? w === h;
  const dimension = (axis: 'w' | 'h', label: string, value: number, limit: number) => (
    <div className="gp-edit-section">
      <label className="gp-edit-label" htmlFor={`dimension-${axis}`}>{label}</label>
      <div className="gp-edit-slider-wrap">
        <button className="gp-edit-btn-val" aria-label={`${label} −`} onClick={() => setDimension(id, axis, value - 4)}>−</button>
        <input aria-label={`${label} slider`} type="range" min="24" max={Math.max(limit, value)} value={value}
          onChange={e => setDimension(id, axis, Number(e.target.value))} className="gp-edit-slider" />
        <button className="gp-edit-btn-val" aria-label={`${label} +`} onClick={() => setDimension(id, axis, value + 4)}>+</button>
        <input id={`dimension-${axis}`} aria-label={label} className="gp-dimension-number" type="number" min="24"
          max={Math.max(limit, value)} value={Math.round(value)} onChange={e => {
            if (e.target.value !== '') setDimension(id, axis, Number(e.target.value));
          }} />
      </div>
    </div>
  );
  return (
    <div className="gp-edit-bar">
      <div className="gp-edit-panel">
        <div className="gp-edit-header">
          <span className="gp-edit-title">{isStick ? (id === 'LeftStick' ? t.leftStickFull : t.rightStickFull) : `${t.buttonPrefix} ${id}`}</span>
          <button className="gp-edit-action close" onClick={() => setSelectedControl(null)}>{t.closePanel}</button>
        </div>
        <div className="gp-edit-body">
          <div className="gp-edit-dimensions">
            {dimension('w', isStick ? t.diameter : t.width, w, isStick ? Math.min(maxWidth, maxHeight) : maxWidth)}
            {!isStick && dimension('h', t.height, h, maxHeight)}
            {!isStick && <label className="gp-checkbox"><input type="checkbox" checked={locked}
              onChange={e => patchControl(id, { lockAspect: e.target.checked })} />{t.keepProportions}</label>}
          </div>
          {!isStick && <div className="gp-edit-section gp-button-options">
            <label>{t.shape}<select aria-label={t.shape} className="gp-input" value={round ? 'round' : 'rect'}
              onChange={e => {
                const circular = e.target.value === 'round';
                patchControl(id, circular ? { round: true, h: w, lockAspect: true } : { round: false, lockAspect: false });
              }}><option value="round">{t.circular}</option><option value="rect">{t.rectangle}</option></select></label>
            <label>{t.command}<select aria-label={t.command} className="gp-input" value={pos.command ?? cfg.command ?? id}
              onChange={e => patchControl(id, { command: e.target.value as Pos['command'] })}>
              {BTN_IDS.map(command => <option key={command} value={command}>{command}</option>)}
            </select></label>
            <label>{t.appearance}<select aria-label={t.appearance} className="gp-input" value={pos.appearance ?? cfg.appearance ?? 'icon'}
              onChange={e => patchControl(id, { appearance: e.target.value as Pos['appearance'] })}>
              <option value="icon">{t.iconStyle}</option><option value="pad">{t.padStyle}</option>
            </select></label>
            <label>{t.buttonIcon}<select aria-label={t.buttonIcon} className="gp-input" value={pos.icon ?? ''}
              onChange={e => patchControl(id, e.target.value
                ? { icon: e.target.value as Pos['icon'] }
                : { icon: null, showLabel: true })}>
              <option value="">{t.noIcon}</option>
              <option value="snare">{t.snareIcon}</option>
              <option value="drum-pad">{t.drumPadIcon}</option>
              <option value="kick">{t.kickIcon}</option>
              <option value="cymbal">{t.cymbalIcon}</option>
            </select></label>
            {pos.icon && <label className="gp-checkbox"><input type="checkbox" checked={pos.showLabel !== false}
              onChange={e => patchControl(id, { showLabel: e.target.checked })} />{t.showButtonText}</label>}
            {(pos.appearance ?? cfg.appearance) === 'pad' && <div className="gp-pad-options">
              <label>{t.padLabel}<input className="gp-input" aria-label={t.padLabel} maxLength={40} value={pos.label ?? cfg.label}
                onChange={e => patchControl(id, { label: e.target.value })} /></label>
              <label>{t.padColor}<input aria-label={t.padColor} type="color" value={pos.color ?? cfg.color}
                onChange={e => patchControl(id, { color: e.target.value })} /></label>
            </div>}
          </div>}
          <div className="gp-edit-section">
            <span className="gp-edit-label">{t.position}</span>
            <div className="gp-edit-nudge">
              <button className="gp-nudge-btn up" aria-label="Move up" onClick={() => nudgePos(id, 0, -1)}>▲</button>
              <div className="gp-nudge-mid">
                <button className="gp-nudge-btn left" aria-label="Move left" onClick={() => nudgePos(id, -1, 0)}>◀</button>
                <span className="gp-nudge-title">{t.align}</span>
                <button className="gp-nudge-btn right" aria-label="Move right" onClick={() => nudgePos(id, 1, 0)}>▶</button>
              </div>
              <button className="gp-nudge-btn down" aria-label="Move down" onClick={() => nudgePos(id, 0, 1)}>▼</button>
            </div>
            <button className="gp-edit-action reset" onClick={() => resetControl(id)}>{t.resetButton}</button>
          </div>
        </div>
      </div>
    </div>
  );
};
