import React, { useState } from 'react';
import type { GamepadLayout, ThemeType } from './types';
import { BTN_IDS, STICK_IDS } from './constants';
import { useLanguage } from './LanguageContext';
import { AboutModal } from './AboutModal';

interface TopBarProps {
  connStatus: 'off' | 'ing' | 'on';
  latency: number | null;
  controllerId: number | null;
  isBarHidden: boolean;
  setIsBarHidden: (hidden: boolean) => void;
  isFullscreen: boolean;
  toggleFullscreen: () => void;
  editMode: boolean;
  setEditMode: (mode: boolean) => void;
  menuOpen: boolean;
  setMenuOpen: (open: boolean) => void;
  visibilityMenuOpen: boolean;
  setVisibilityMenuOpen: (open: boolean) => void;
  resetLayout: () => void;
  exportLayout: () => void;
  fileRef: React.RefObject<HTMLInputElement | null>;
  importLayout: (e: React.ChangeEvent<HTMLInputElement>) => void;
  layout: GamepadLayout;
  toggleVisibility: (id: string) => void;
  showHitboxes: boolean;
  setShowHitboxes: (show: boolean) => void;
  // 从设置弹窗搬出来的手柄参数：直接摊在这一行里，不再有弹窗。
  activeProfile: string;
  profiles: { id: string; name: string }[];
  switchProfile: (id: string) => void;
  createNewProfile: () => void;
  deleteProfile: (id: string) => void;
  theme: ThemeType;
  setTheme: (theme: ThemeType) => void;
  enableVib: boolean;
  setEnableVib: (enable: boolean) => void;
  sensitivity: number;
  setSensitivity: (sens: number) => void;
}

/**
 * 顶栏是手柄界面的唯一一行：状态 + 所有可调项。
 * 配置方案、按键风格、启动振动、全屏都直接摆在这一行上，
 * 原来的设置弹窗已经拆掉，不再有第二处入口。
 */
export const TopBar: React.FC<TopBarProps> = ({
  connStatus, latency, controllerId, isBarHidden, setIsBarHidden, isFullscreen, toggleFullscreen,
  editMode, setEditMode, menuOpen, setMenuOpen, visibilityMenuOpen, setVisibilityMenuOpen,
  resetLayout, exportLayout, fileRef, importLayout, layout, toggleVisibility,
  showHitboxes, setShowHitboxes,
  activeProfile, profiles, switchProfile, createNewProfile, deleteProfile,
  theme, setTheme, enableVib, setEnableVib, sensitivity, setSensitivity
}) => {
  const { t } = useLanguage();
  const [aboutOpen, setAboutOpen] = useState(false);

  return (
    <>
      <div className={`gp-bar ${isBarHidden ? 'hidden' : ''}`}>
        <span className={`gp-dot ${connStatus}`} />
        <span className="gp-bar-status">
          {connStatus === 'on' ? t.connected : connStatus === 'ing' ? t.connecting : t.disconnected}
        </span>
        {connStatus === 'on' && latency !== null && (
          <span className={`gp-latency ${latency < 20 ? 'good' : latency < 50 ? 'ok' : 'bad'}`}>
            {latency}ms
          </span>
        )}
        {connStatus === 'on' && controllerId !== null && (
          <span className="gp-bar-status" style={{ color: '#3b82f6', background: 'rgba(59, 130, 246, 0.1)', padding: '2px 6px', borderRadius: '4px' }}>
            P{controllerId + 1} ({controllerId})
          </span>
        )}
        <div className="gp-spacer" />

        {/* 手柄参数：配置方案 / 按键风格 / 启动振动 / 灵敏度 —— 全在这一行。
            手柄类型不在这里：它决定服务端建哪种设备，只能在连接门里选。 */}
        <select
          className="gp-bar-select"
          value={activeProfile}
          onChange={e => switchProfile(e.target.value)}
          aria-label={t.profile}
        >
          {profiles.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
        </select>

        <select
          className="gp-bar-select"
          value={theme}
          onChange={e => setTheme(e.target.value as ThemeType)}
          aria-label={t.buttonStyle}
        >
          <option value="xbox">{t.xboxDefault}</option>
          <option value="ps">PlayStation</option>
          <option value="nintendo">Nintendo Switch</option>
        </select>

        <button
          className={`gp-bar-btn gp-bar-toggle ${enableVib ? 'on' : 'off'}`}
          onClick={() => setEnableVib(!enableVib)}
          aria-label={t.enableVibration}
        >
          {t.enableVibration}
          <span className="gp-bar-toggle-state">{enableVib ? t.on : t.off}</span>
        </button>

        <label className="gp-bar-slider" aria-label={`${t.sensitivity} (${sensitivity}%)`}>
          <span className="gp-bar-slider-label">{t.sensitivity}</span>
          <input
            type="range"
            min="10" max="200"
            value={sensitivity}
            onChange={e => setSensitivity(parseInt(e.target.value, 10))}
          />
        </label>

        <button className="gp-bar-btn" onClick={toggleFullscreen}>
          {isFullscreen ? t.minimize : t.fullscreen}
        </button>

        {isFullscreen && (
          <button className="gp-bar-btn" onClick={() => setIsBarHidden(true)}>
            {t.hide}
          </button>
        )}
        <button
          className={`gp-bar-btn ${editMode ? 'done' : ''}`}
          onClick={() => setEditMode(!editMode)}
        >
          {editMode ? t.done : t.edit}
        </button>
        {editMode && (
          <button
            className={`gp-bar-btn ${showHitboxes ? 'done' : ''}`}
            onClick={() => setShowHitboxes(!showHitboxes)}
          >
            {t.touchArea}
          </button>
        )}
        {editMode && (
          <div className="gp-menu-wrap">
            <button className="gp-bar-btn" onClick={() => { setVisibilityMenuOpen(!visibilityMenuOpen); setMenuOpen(false); }}>
              {t.buttons}
            </button>
            {visibilityMenuOpen && (
              <div className="gp-dropdown" style={{ maxHeight: '60vh', overflowY: 'auto', minWidth: '150px' }}>
                {[...BTN_IDS, ...STICK_IDS].map(id => {
                  const isHidden = layout.controls[id]?.hidden === true;
                  return (
                    <button key={id} onClick={() => toggleVisibility(id)} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span>{id === 'LeftStick' ? t.leftStick : id === 'RightStick' ? t.rightStick : id}</span>
                      <span className="gp-vis-state">{isHidden ? t.hideControl : t.show}</span>
                    </button>
                  );
                })}
              </div>
            )}
          </div>
        )}

        <div className="gp-menu-wrap">
          <button className="gp-bar-btn" onClick={() => { setMenuOpen(!menuOpen); setVisibilityMenuOpen(false); }}>{t.menu}</button>
          {menuOpen && (
            <div className="gp-dropdown promptfont">
              <button onClick={resetLayout}>{t.reset}</button>
              <button onClick={exportLayout}>{t.export}</button>
              <button onClick={() => { fileRef.current?.click(); }}>{t.import}</button>
              <button onClick={() => { createNewProfile(); setMenuOpen(false); }}>{t.newProfile}</button>
              <button
                onClick={() => { deleteProfile(activeProfile); setMenuOpen(false); }}
                disabled={profiles.length <= 1}
              >
                {t.deleteProfile}
              </button>
              <button onClick={() => { setAboutOpen(true); setMenuOpen(false); }}>{t.about}</button>
            </div>
          )}
        </div>
        <input ref={fileRef} type="file" accept=".json" style={{ display: 'none' }} onChange={importLayout} />
      </div>

      {isBarHidden && (
        <button
          className="gp-show-bar-btn"
          onClick={() => setIsBarHidden(false)}
        >
          {t.showBar}
        </button>
      )}
      <AboutModal open={aboutOpen} onClose={() => setAboutOpen(false)} />
    </>
  );
};
