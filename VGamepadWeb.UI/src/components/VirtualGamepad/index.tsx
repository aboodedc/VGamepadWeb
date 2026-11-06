import React, { useState, useEffect, useRef, useCallback } from 'react';
import type { GamepadLayout, ThemeType } from './types';
import { LAYOUT_KEY, DEFAULT_LAYOUT, THEMES, BTN_IDS, STICK_IDS } from './constants';
import { loadLayout, clamp } from './utils';
import { GpButton } from './GpButton';
import { GpJoystick } from './GpJoystick';
import { TopBar } from './TopBar';
import { ConnectionGate } from './ConnectionGate';
import { EditBar } from './EditBar';
import { useGamepadConnection } from './hooks/useGamepadConnection';
import { usePageLockdown } from './hooks/usePageLockdown';
import { LanguageProvider, useLanguage } from './LanguageContext';

const VirtualGamepadInner: React.FC = () => {
  const { t } = useLanguage();

  // 挡住右键菜单 / 开发者工具 / 复制 / 刷新等会打断手柄操作的浏览器行为。
  usePageLockdown();
  const [activeProfile, setActiveProfile] = useState<string>(() => localStorage.getItem('gamepad_active_profile') || 'default');
  // 默认方案的名字跟着语言走。老用户 localStorage 里存的还是旧名（「默认」/「Default」），
  // 读取时就换掉 —— 不用 effect 改状态，初始化阶段一次搞定。
  const [profiles, setProfiles] = useState<{id: string, name: string}[]>(() => {
    try {
      const p = localStorage.getItem('gamepad_profiles');
      if (p) {
        const saved = JSON.parse(p) as {id: string, name: string}[];
        if (Array.isArray(saved)) {
          return saved.map(item =>
            item.id === 'default' ? { ...item, name: t.defaultProfile } : item
          );
        }
      }
    } catch {}
    return [{ id: 'default', name: t.defaultProfile }];
  });

  const [theme, setTheme] = useState<ThemeType>(() => (localStorage.getItem('gamepad_theme') as ThemeType) || 'xbox');
  // 手柄类型：0 = Xbox 360，1 = DualShock 4。服务端按它建虚拟设备，连上后不能再改。
  const [controllerType, setControllerType] = useState<number>(
    () => parseInt(localStorage.getItem('gamepad_ctype') || '0', 10) === 1 ? 1 : 0
  );
  const [layout, setLayout] = useState<GamepadLayout>(() => {
    const initialTheme = (localStorage.getItem('gamepad_theme') as ThemeType) || 'xbox';
    const initialProfile = localStorage.getItem('gamepad_active_profile') || 'default';
    return loadLayout(initialTheme, initialProfile);
  });
  
  const [editMode, setEditMode] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [serverPassword, setServerPassword] = useState(() => localStorage.getItem('gamepad_pass') || '');
  // 振动默认开启：只记「显式关掉」的那次，没存过就是开。
  const [enableVib, setEnableVib] = useState<boolean>(() => localStorage.getItem('gamepad_vib') !== 'false');
  const [sensitivity, setSensitivity] = useState<number>(() => parseInt(localStorage.getItem('gamepad_sens') || '100', 10));
  const [activeButtons, setActiveButtons] = useState<Set<string>>(new Set());
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isBarHidden, setIsBarHidden] = useState(false);
  const [selectedControl, setSelectedControl] = useState<string | null>(null);
  const [visibilityMenuOpen, setVisibilityMenuOpen] = useState(false);
  const [showHitboxes, setShowHitboxes] = useState(false);

  const canvasRef = useRef<HTMLDivElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  // 服务器地址恒为当前页面的 origin：这个页面本就是那台 PC 提供的，
  // 再让用户填地址是自己给自己找麻烦。
  const actualServerUrl = window.location.origin;
  const {
    connStatus, latency, controllerId, notice,
    connect, dismissNotice, sendButton, sendJoystick
  } = useGamepadConnection({
    serverUrl: actualServerUrl, serverPassword, enableVib, sensitivity, controllerType, t
  });

  // Clear selection when exiting edit mode
  useEffect(() => { 
    if (!editMode) {
      setSelectedControl(null);
      setVisibilityMenuOpen(false);
    }
  }, [editMode]);

  // Fullscreen support
  useEffect(() => {
    const handleFullscreenChange = () => {
      const isFull = !!document.fullscreenElement;
      setIsFullscreen(isFull);
      if (!isFull) setIsBarHidden(false);
    };
    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () => {
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
    };
  }, []);

  const toggleFullscreen = async () => {
    try {
      if (!document.fullscreenElement) {
        await document.documentElement.requestFullscreen();
      } else {
        if (document.exitFullscreen) {
          await document.exitFullscreen();
        }
      }
    } catch (err) {
      console.error('Fullscreen error:', err);
    }
  };

  // Persist
  useEffect(() => { localStorage.setItem(`${LAYOUT_KEY}_${activeProfile}_${theme}`, JSON.stringify(layout)); }, [layout, theme, activeProfile]);
  useEffect(() => { localStorage.setItem('gamepad_theme', theme); }, [theme]);
  useEffect(() => { localStorage.setItem('gamepad_ctype', controllerType.toString()); }, [controllerType]);
  useEffect(() => { localStorage.setItem('gamepad_active_profile', activeProfile); }, [activeProfile]);
  useEffect(() => { localStorage.setItem('gamepad_profiles', JSON.stringify(profiles)); }, [profiles]);
  useEffect(() => { localStorage.setItem('gamepad_vib', enableVib.toString()); }, [enableVib]);
  useEffect(() => { localStorage.setItem('gamepad_sens', sensitivity.toString()); }, [sensitivity]);
  useEffect(() => { localStorage.setItem('gamepad_pass', serverPassword); }, [serverPassword]);

  const switchProfile = useCallback((profileId: string) => {
    setActiveProfile(profileId);
    setLayout(loadLayout(theme, profileId));
  }, [theme]);

  const createNewProfile = () => {
    const name = prompt(t.newProfilePrompt);
    if (name && name.trim()) {
      const newId = 'p_' + Date.now();
      setProfiles(prev => [...prev, { id: newId, name: name.trim() }]);
      setActiveProfile(newId);
      setLayout(loadLayout(theme, newId));
    }
  };

  const deleteProfile = (profileId: string) => {
    if (profiles.length <= 1) return alert(t.cannotDeleteOnly);
    if (window.confirm(t.confirmDeleteProfile)) {
      const newProfiles = profiles.filter(p => p.id !== profileId);
      setProfiles(newProfiles);
      if (activeProfile === profileId) {
        const fallback = newProfiles[0].id;
        setActiveProfile(fallback);
        setLayout(loadLayout(theme, fallback));
      }
      Object.keys(THEMES).forEach(t => localStorage.removeItem(`${LAYOUT_KEY}_${profileId}_${t}`));
    }
  };

  // Layout ops
  const updatePos = useCallback((id: string, x: number, y: number) => {
    setLayout(prev => ({ ...prev, controls: { ...prev.controls, [id]: { ...prev.controls[id], x, y } } }));
  }, []);

  const updateSize = useCallback((id: string, delta: number) => {
    setLayout(prev => {
      const ctrl = prev.controls[id] || { x: 50, y: 50 };
      const isStick = (STICK_IDS as readonly string[]).includes(id);
      const defaultW = isStick ? 120 : (THEMES[theme][id]?.w ?? 56);
      const defaultH = isStick ? 120 : (THEMES[theme][id]?.h ?? 56);
      const curW = ctrl.w ?? defaultW;
      const curH = ctrl.h ?? defaultH;
      return { ...prev, controls: { ...prev.controls, [id]: { ...ctrl, w: clamp(curW + delta, 24, 200), h: clamp(curH + delta, 24, 200) } } };
    });
  }, [theme]);

  const setSize = useCallback((id: string, size: number) => {
    setLayout(prev => {
      const ctrl = prev.controls[id] || { x: 50, y: 50 };
      return { ...prev, controls: { ...prev.controls, [id]: { ...ctrl, w: clamp(size, 24, 200), h: clamp(size, 24, 200) } } };
    });
  }, []);

  const setShape = useCallback((id: string, round: boolean) => {
    setLayout(prev => {
      const ctrl = prev.controls[id] || { x: 50, y: 50 };
      return { ...prev, controls: { ...prev.controls, [id]: { ...ctrl, round } } };
    });
  }, []);

  const nudgePos = useCallback((id: string, dx: number, dy: number) => {
    setLayout(prev => {
      const ctrl = prev.controls[id] || { x: 50, y: 50 };
      return {
        ...prev,
        controls: {
          ...prev.controls,
          [id]: {
            ...ctrl,
            x: clamp(ctrl.x + dx, 0, 95),
            y: clamp(ctrl.y + dy, 0, 95)
          }
        }
      };
    });
  }, []);

  const resetControl = useCallback((id: string) => {
    setLayout(prev => {
      const newControls = { ...prev.controls };
      const defaultPos = DEFAULT_LAYOUT.controls[id];
      if (defaultPos) {
        newControls[id] = { ...defaultPos };
      } else {
        delete newControls[id];
      }
      return { ...prev, controls: newControls };
    });
  }, []);

  const toggleVisibility = useCallback((id: string) => {
    setLayout(prev => {
      const defaultPos = DEFAULT_LAYOUT.controls[id];
      const ctrl = prev.controls[id] || (defaultPos ? { ...defaultPos } : { x: 50, y: 50 });
      return { ...prev, controls: { ...prev.controls, [id]: { ...ctrl, hidden: !ctrl.hidden } } };
    });
    setSelectedControl(prev => prev === id ? null : prev);
  }, []);

  const resetLayout = () => { setLayout(DEFAULT_LAYOUT); setMenuOpen(false); setSelectedControl(null); setVisibilityMenuOpen(false); };

  const exportLayout = () => {
    const blob = new Blob([JSON.stringify(layout, null, 2)], { type: 'application/json' });
    const a = document.createElement('a'); a.href = URL.createObjectURL(blob);
    a.download = 'gamepad-layout.json'; a.click(); URL.revokeObjectURL(a.href);
    setMenuOpen(false);
  };

  const importLayout = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]; if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      try {
        const p = JSON.parse(reader.result as string);
        if (p.controls) { setLayout(p); } else { alert(t.invalidFile); }
      } catch { alert(t.readFileFailed); }
    };
    reader.readAsText(file);
    e.target.value = '';
    setMenuOpen(false);
  };

  return (
    <div className="gp-root">
      <TopBar
        connStatus={connStatus} latency={latency} controllerId={controllerId} isBarHidden={isBarHidden} setIsBarHidden={setIsBarHidden}
        isFullscreen={isFullscreen} toggleFullscreen={toggleFullscreen}
        editMode={editMode} setEditMode={setEditMode}
        menuOpen={menuOpen} setMenuOpen={setMenuOpen} visibilityMenuOpen={visibilityMenuOpen}
        setVisibilityMenuOpen={setVisibilityMenuOpen} resetLayout={resetLayout}
        exportLayout={exportLayout} fileRef={fileRef} importLayout={importLayout} layout={layout} toggleVisibility={toggleVisibility}
        showHitboxes={showHitboxes} setShowHitboxes={setShowHitboxes}
        activeProfile={activeProfile} profiles={profiles} switchProfile={switchProfile}
        createNewProfile={createNewProfile} deleteProfile={deleteProfile}
        theme={theme} setTheme={setTheme}
        controllerType={controllerType} setControllerType={setControllerType}
        enableVib={enableVib} setEnableVib={setEnableVib}
        sensitivity={sensitivity} setSensitivity={setSensitivity}
      />

      {/* 未连接时盖住整页：密码 / 连接 / 全屏 / 语言都在这里，连上才能用手柄。 */}
      <ConnectionGate
        connected={connStatus === 'on'}
        connecting={connStatus === 'ing'}
        notice={notice}
        serverPassword={serverPassword} setServerPassword={setServerPassword}
        connect={connect}
        toggleFullscreen={toggleFullscreen}
        isFullscreen={isFullscreen}
      />

      {notice && connStatus === 'on' && (
        <div className="gp-notice" onClick={dismissNotice}>{notice}</div>
      )}

      {/* ─── Canvas ─── */}
      <div
        ref={canvasRef}
        className={`gp-canvas ${editMode ? 'editing' : ''} ${connStatus === 'off' ? 'dimmed' : ''}`}
        onClick={() => {
          if (menuOpen) setMenuOpen(false);
          if (visibilityMenuOpen) setVisibilityMenuOpen(false);
        }}
      >
        {BTN_IDS.map(id => {
          const pos = layout.controls[id] || DEFAULT_LAYOUT.controls[id] || { x: 50, y: 50 };
          if (pos.hidden) return null;
          return (
            <GpButton
              key={id} id={id} cfg={THEMES[theme][id]}
              pos={pos}
              editMode={editMode}
              active={activeButtons.has(id)}
              isSelected={selectedControl === id}
              showHitboxes={showHitboxes}
              canvasRef={canvasRef}
              onDrag={(x, y) => updatePos(id, x, y)}
              onPress={() => {
                if (editMode) return;
                setActiveButtons(p => new Set(p).add(id));
                sendButton(id, true);
              }}
              onRelease={() => {
                if (editMode) return;
                setActiveButtons(p => { const s = new Set(p); s.delete(id); return s; });
                sendButton(id, false);
              }}
              onSelect={() => setSelectedControl(id)}
            />
          );
        })}
        {STICK_IDS.map(sid => {
          const pos = layout.controls[sid] || DEFAULT_LAYOUT.controls[sid] || { x: 50, y: 50 };
          if (pos.hidden) return null;
          return (
            <GpJoystick
              key={sid} id={sid}
              pos={pos}
              editMode={editMode}
              isSelected={selectedControl === sid}
              showHitboxes={showHitboxes}
              canvasRef={canvasRef}
              onDrag={(x, y) => updatePos(sid, x, y)}
              onMove={(x, y) => { if (!editMode) sendJoystick(sid.replace('Stick', ''), x, y); }}
              onSelect={() => setSelectedControl(sid)}
            />
          );
        })}

        <EditBar 
          editMode={editMode} selectedControl={selectedControl} layout={layout} theme={theme}
          updateSize={updateSize} setSize={setSize} setShape={setShape} nudgePos={nudgePos}
          resetControl={resetControl} setSelectedControl={setSelectedControl}
        />
      </div>
    </div>
  );
};

export const VirtualGamepad: React.FC = () => (
  <LanguageProvider>
    <VirtualGamepadInner />
  </LanguageProvider>
);
