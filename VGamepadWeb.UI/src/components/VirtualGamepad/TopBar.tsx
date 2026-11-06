import React, { useState } from 'react';
import type { GamepadLayout } from './types';
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
  editMode: boolean;
  setEditMode: (mode: boolean) => void;
  menuOpen: boolean;
  setMenuOpen: (open: boolean) => void;
  visibilityMenuOpen: boolean;
  setVisibilityMenuOpen: (open: boolean) => void;
  setSettingsOpen: (open: boolean) => void;
  resetLayout: () => void;
  exportLayout: () => void;
  fileRef: React.RefObject<HTMLInputElement | null>;
  importLayout: (e: React.ChangeEvent<HTMLInputElement>) => void;
  layout: GamepadLayout;
  toggleVisibility: (id: string) => void;
  showHitboxes: boolean;
  setShowHitboxes: (show: boolean) => void;
}

/**
 * 顶栏只负责「状态 + 布局类操作」。
 * 全屏、语言、连接/断开都搬到了连接覆盖层（ConnectionGate），这里不再重复入口。
 */
export const TopBar: React.FC<TopBarProps> = ({
  connStatus, latency, controllerId, isBarHidden, setIsBarHidden, isFullscreen,
  editMode, setEditMode, menuOpen, setMenuOpen, visibilityMenuOpen, setVisibilityMenuOpen,
  setSettingsOpen, resetLayout, exportLayout, fileRef, importLayout, layout, toggleVisibility,
  showHitboxes, setShowHitboxes
}) => {
  const { t } = useLanguage();
  const [aboutOpen, setAboutOpen] = useState(false);

  return (
    <>
      <div className={`gp-bar ${isBarHidden ? 'hidden' : ''}`}>
        <div className={`gp-dot ${connStatus}`} onClick={() => setSettingsOpen(true)} />
        <span className="gp-bar-status" onClick={() => setSettingsOpen(true)}>
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
                      <span className='promptfont'>{isHidden ? '\u{1F315}' : '\u{1F311}'}</span>
                    </button>
                  );
                })}
              </div>
            )}
          </div>
        )}

        <div className="gp-menu-wrap">
          <button className="gp-bar-btn" onClick={() => { setMenuOpen(!menuOpen); setVisibilityMenuOpen(false); }}>☰</button>
          {menuOpen && (
            <div className="gp-dropdown promptfont">
              <button onClick={resetLayout}>{t.reset}</button>
              <button onClick={exportLayout}>{t.export}</button>
              <button onClick={() => { fileRef.current?.click(); }}>{t.import}</button>
              <button onClick={() => { setSettingsOpen(true); setMenuOpen(false); }}>{t.settings}</button>
              <button onClick={() => { setAboutOpen(true); setMenuOpen(false); }}>{t.about}</button>
            </div>
          )}
        </div>
        <input ref={fileRef} type="file" accept=".json" style={{ display: 'none' }} onChange={importLayout} />
      </div>

      {isBarHidden && (
        <button 
          className="promptfont gp-show-bar-btn"
          onClick={() => setIsBarHidden(false)}
        >
          {"\u{1F441}"}
        </button>
      )}
      <AboutModal open={aboutOpen} onClose={() => setAboutOpen(false)} />
    </>
  );
};
