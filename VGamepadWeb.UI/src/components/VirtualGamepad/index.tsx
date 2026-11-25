import React, { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import type { GamepadLayout, Pos, Profile, ThemeType } from './types';
import { LAYOUT_KEY, URL_KEY, DEFAULT_LAYOUT, THEMES, getButtonIds, getStickIds, STICK_IDS } from './constants';
import { loadLayout, clamp, parseLayout, readInitialTheme, resolveProfileTheme } from './utils';
import { createDrumsLayout, getLayoutDefaults, getLayoutScale, migrateDrumsLayout } from './presets';
import { GpButton } from './GpButton';
import { GpJoystick } from './GpJoystick';
import { TopBar } from './TopBar';
import { SettingsModal } from './SettingsModal';
import { EditBar } from './EditBar';
import { useGamepadConnection } from './hooks/useGamepadConnection';
import { useGamepadInput } from './hooks/useGamepadInput';
import { useFullscreen } from './hooks/useFullscreen';
import { FullscreenHelpModal } from './FullscreenHelpModal';
import { LanguageProvider, useLanguage } from './LanguageContext';

const VirtualGamepadInner: React.FC = () => {
  const { t } = useLanguage();
  const [activeProfile, setActiveProfile] = useState<string>(() => localStorage.getItem('gamepad_active_profile') || 'default');
  const [profiles, setProfiles] = useState<Profile[]>(() => {
    try { 
      const p = localStorage.getItem('gamepad_profiles');
      if (p) {
        const saved = JSON.parse(p);
        if (Array.isArray(saved) && saved.length && saved.every(profile => typeof profile?.id === 'string' && typeof profile?.name === 'string')) return saved;
      }
    } catch { /* Keep the default profile if saved metadata is invalid. */ }
    return [{ id: 'default', name: 'Default' }];
  });

  const [theme, setTheme] = useState<ThemeType>(readInitialTheme);
  const [layout, setLayout] = useState<GamepadLayout>(() => {
    const initialTheme = readInitialTheme();
    const initialProfile = localStorage.getItem('gamepad_active_profile') || 'default';
    return loadLayout(initialTheme, initialProfile);
  });
  
  const [editMode, setEditMode] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [useSameServer, setUseSameServer] = useState<boolean>(() => localStorage.getItem('gamepad_use_same_server') === 'true' || localStorage.getItem('gamepad_use_same_server') === null);
  const [serverUrl, setServerUrl] = useState(() => localStorage.getItem(URL_KEY) || `http://${window.location.hostname}:5000`);
  const [serverPassword, setServerPassword] = useState(() => localStorage.getItem('gamepad_pass') || '');
  const [controllerType, setControllerType] = useState<number>(() => parseInt(localStorage.getItem('gamepad_ctype') || '0', 10));
  const [enableVib, setEnableVib] = useState<boolean>(() => localStorage.getItem('gamepad_vib') !== 'false');
  const [sensitivity, setSensitivity] = useState<number>(() => parseInt(localStorage.getItem('gamepad_sens') || '100', 10));
  const fullscreen = useFullscreen();
  const [isBarHidden, setIsBarHidden] = useState(false);
  const [selectedControl, setSelectedControl] = useState<string | null>(null);
  const [visibilityMenuOpen, setVisibilityMenuOpen] = useState(false);
  const [showHitboxes, setShowHitboxes] = useState(false);

  const canvasRef = useRef<HTMLDivElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const [canvasSize, setCanvasSize] = useState({ width: 800, height: 320 });
  const layoutScale = getLayoutScale(layout, canvasSize.width, canvasSize.height);
  const defaults = useMemo(() => theme === 'drums' ? createDrumsLayout() : structuredClone(DEFAULT_LAYOUT), [theme]);
  const buttonIds = getButtonIds(theme);
  const stickIds = getStickIds(theme);

  const actualServerUrl = useSameServer ? window.location.origin : serverUrl;
  const { connStatus, latency, controllerId, connect, disconnect, sendButton, sendJoystick } = useGamepadConnection({
    serverUrl: actualServerUrl, serverPassword, controllerType, enableVib, sensitivity
  });
  const { activeButtons, input } = useGamepadInput(sendButton);
  const setEditing = (value: boolean) => {
    input.releaseAll(); setEditMode(value);
    if (!value) { setSelectedControl(null); setVisibilityMenuOpen(false); }
  };
  const openSettings = (value: boolean) => { input.releaseAll(); setSettingsOpen(value); };
  const toggleFullscreen = () => { input.releaseAll(); void fullscreen.toggle(); };

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const observer = new ResizeObserver(entries => {
      const { width, height } = entries[0].contentRect;
      setCanvasSize({ width, height });
    });
    observer.observe(canvas);
    return () => observer.disconnect();
  }, []);

  useEffect(() => { if (connStatus !== 'on') input.releaseAll(); }, [connStatus, input]);

  // Persist
  useEffect(() => { localStorage.setItem(`${LAYOUT_KEY}_${activeProfile}_${theme}`, JSON.stringify(layout)); }, [layout, theme, activeProfile]);
  useEffect(() => { localStorage.setItem('gamepad_theme', theme); }, [theme]);
  useEffect(() => { localStorage.setItem('gamepad_active_profile', activeProfile); }, [activeProfile]);
  useEffect(() => { localStorage.setItem('gamepad_profiles', JSON.stringify(profiles)); }, [profiles]);
  useEffect(() => { localStorage.setItem(URL_KEY, serverUrl); }, [serverUrl]);
  useEffect(() => { localStorage.setItem('gamepad_use_same_server', useSameServer.toString()); }, [useSameServer]);
  useEffect(() => { localStorage.setItem('gamepad_ctype', controllerType.toString()); }, [controllerType]);
  useEffect(() => { localStorage.setItem('gamepad_vib', enableVib.toString()); }, [enableVib]);
  useEffect(() => { localStorage.setItem('gamepad_sens', sensitivity.toString()); }, [sensitivity]);
  useEffect(() => { localStorage.setItem('gamepad_pass', serverPassword); }, [serverPassword]);

  const switchProfile = useCallback((profileId: string) => {
    input.releaseAll();
    const nextTheme = resolveProfileTheme(theme, profileId);
    setActiveProfile(profileId);
    setTheme(nextTheme);
    setLayout(loadLayout(nextTheme, profileId));
    setSelectedControl(null);
  }, [theme, input]);

  const switchTheme = (nextTheme: ThemeType) => {
    input.releaseAll();
    localStorage.setItem(`${LAYOUT_KEY}_${activeProfile}_${theme}`, JSON.stringify(layout));
    setLayout(loadLayout(nextTheme, activeProfile));
    setTheme(nextTheme);
    setSelectedControl(null);
  };

  const createNewProfile = () => {
    const name = prompt(t.newProfilePrompt);
    if (name && name.trim()) {
      input.releaseAll();
      const newId = 'p_' + Date.now();
      setProfiles(prev => [...prev, { id: newId, name: name.trim() }]);
      setActiveProfile(newId);
      setLayout(loadLayout(theme, newId));
    }
  };

  const deleteProfile = (profileId: string) => {
    if (profiles.length <= 1) return alert(t.cannotDeleteOnly);
    if (window.confirm(t.confirmDeleteProfile)) {
      input.releaseAll();
      const newProfiles = profiles.filter(p => p.id !== profileId);
      setProfiles(newProfiles);
      if (activeProfile === profileId) {
        const fallback = newProfiles[0].id;
        const nextTheme = resolveProfileTheme(theme, fallback);
        setActiveProfile(fallback);
        setTheme(nextTheme);
        setLayout(loadLayout(nextTheme, fallback));
      }
      Object.keys(THEMES).forEach(t => localStorage.removeItem(`${LAYOUT_KEY}_${profileId}_${t}`));
    }
  };

  // Layout ops
  const updatePos = useCallback((id: string, x: number, y: number) => {
    setLayout(prev => ({ ...prev, controls: { ...prev.controls, [id]: { ...prev.controls[id], x, y } } }));
  }, []);

  const patchControl = useCallback((id: string, patch: Partial<Pos>) => {
    input.releaseControl(id);
    setLayout(prev => {
      const ctrl = prev.controls[id] || { x: 50, y: 50 };
      return { ...prev, controls: { ...prev.controls, [id]: { ...ctrl, ...patch } } };
    });
  }, [input]);

  const setDimension = (id: string, axis: 'w' | 'h', size: number) => {
    if (!Number.isFinite(size)) return;
    size = Math.max(24, size);
    setLayout(prev => {
      const ctrl = prev.controls[id] || { x: 50, y: 50 };
      const stick = (STICK_IDS as readonly string[]).includes(id);
      const w = ctrl.w ?? (stick ? 120 : THEMES[theme][id].w);
      const h = ctrl.h ?? (stick ? 120 : THEMES[theme][id].h);
      const limitW = Math.max(24, canvasSize.width / layoutScale);
      const limitH = Math.max(24, canvasSize.height / layoutScale);
      const locked = stick || (ctrl.lockAspect ?? w === h);
      let nextW = axis === 'w' ? size : locked ? w * size / h : w;
      let nextH = axis === 'h' ? size : locked ? h * size / w : h;
      if (locked) {
        const factor = Math.min(1, limitW / nextW, limitH / nextH);
        nextW *= factor; nextH *= factor;
      }
      nextW = Math.round(clamp(nextW, 24, limitW));
      nextH = Math.round(clamp(nextH, 24, limitH));
      return { ...prev, controls: { ...prev.controls, [id]: { ...ctrl, w: nextW, h: nextH } } };
    });
  };

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
    input.releaseControl(id);
    setLayout(prev => {
      const newControls = { ...prev.controls };
      const defaultPos = getLayoutDefaults(prev, theme).controls[id];
      if (defaultPos) {
        newControls[id] = { ...defaultPos };
      } else {
        delete newControls[id];
      }
      return { ...prev, controls: newControls };
    });
  }, [input, theme]);

  const toggleVisibility = useCallback((id: string) => {
    input.releaseControl(id);
    setLayout(prev => {
      const defaultPos = defaults.controls[id];
      const ctrl = prev.controls[id] || (defaultPos ? { ...defaultPos } : { x: 50, y: 50 });
      return { ...prev, controls: { ...prev.controls, [id]: { ...ctrl, hidden: !ctrl.hidden } } };
    });
    setSelectedControl(prev => prev === id ? null : prev);
  }, [input, defaults]);

  const resetLayout = () => { input.releaseAll(); setLayout(getLayoutDefaults(layout, theme)); setMenuOpen(false); setSelectedControl(null); setVisibilityMenuOpen(false); };

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
        const imported = parseLayout(JSON.parse(reader.result as string));
        input.releaseAll();
        localStorage.setItem(`${LAYOUT_KEY}_${activeProfile}_${theme}_backup`, JSON.stringify(layout));
        const nextTheme = imported.presetId === 'clone-hero-drums' ? 'drums' : theme === 'drums' ? 'xbox' : theme;
        setTheme(nextTheme);
        setLayout(nextTheme === 'drums' ? migrateDrumsLayout(imported) : imported);
        setSelectedControl(null);
      } catch { alert(t.readFileFailed); }
    };
    reader.readAsText(file);
    e.target.value = '';
    setMenuOpen(false);
  };

  return (
    <div className={`gp-root ${isBarHidden ? 'gp-game-mode' : ''}`} data-presentation={fullscreen.native ? 'fullscreen' : fullscreen.standalone ? 'standalone' : 'browser'}>
      <TopBar 
        connStatus={connStatus} latency={latency} controllerId={controllerId} isBarHidden={isBarHidden}
        setIsBarHidden={hidden => { input.releaseAll(); if (hidden) setEditing(false); setIsBarHidden(hidden); }}
        isFullscreen={fullscreen.native} standalone={fullscreen.standalone} fullscreenSupported={fullscreen.supported}
        fullscreenPending={fullscreen.pending} toggleFullscreen={toggleFullscreen} editMode={editMode} setEditMode={setEditing}
        menuOpen={menuOpen} setMenuOpen={setMenuOpen} visibilityMenuOpen={visibilityMenuOpen}
        setVisibilityMenuOpen={setVisibilityMenuOpen} setSettingsOpen={openSettings} resetLayout={resetLayout}
        exportLayout={exportLayout} fileRef={fileRef} importLayout={importLayout} layout={layout} toggleVisibility={toggleVisibility}
        controlIds={[...buttonIds, ...stickIds]}
        showHitboxes={showHitboxes} setShowHitboxes={setShowHitboxes}
      />

      <SettingsModal 
        settingsOpen={settingsOpen} setSettingsOpen={openSettings} activeProfile={activeProfile}
        profiles={profiles} switchProfile={switchProfile} createNewProfile={createNewProfile} deleteProfile={deleteProfile}
        theme={theme} setTheme={switchTheme} controllerType={controllerType} setControllerType={setControllerType}
        enableVib={enableVib} setEnableVib={setEnableVib} sensitivity={sensitivity} setSensitivity={setSensitivity}
        useSameServer={useSameServer} setUseSameServer={setUseSameServer}
        serverUrl={serverUrl} setServerUrl={setServerUrl} serverPassword={serverPassword} setServerPassword={setServerPassword} 
        connStatus={connStatus} connect={connect} disconnect={disconnect}
      />

      <FullscreenHelpModal reason={fullscreen.help} error={fullscreen.error} close={fullscreen.closeHelp}
        enterGameMode={() => { input.releaseAll(); setEditing(false); setIsBarHidden(true); }} />

      {/* ─── Canvas ─── */}
      <div
        ref={canvasRef}
        className={`gp-canvas ${editMode ? 'editing' : ''} ${connStatus === 'off' ? 'dimmed' : ''}`}
        onClick={() => {
          if (menuOpen) setMenuOpen(false);
          if (visibilityMenuOpen) setVisibilityMenuOpen(false);
        }}
      >
        {theme === 'drums' && <div className="gp-landscape-hint">{t.rotateForDrums}</div>}
        {buttonIds.map(id => {
          const pos = layout.controls[id] || defaults.controls[id] || { x: 50, y: 50 };
          if (pos.hidden) return null;
          return (
            <GpButton
              key={`${activeProfile}-${theme}-${id}`} id={id} cfg={THEMES[theme][id]}
              pos={pos}
              scale={layoutScale}
              editMode={editMode}
              active={activeButtons.has(id)}
              isSelected={selectedControl === id}
              showHitboxes={showHitboxes}
              canvasRef={canvasRef}
              onDrag={(x, y) => updatePos(id, x, y)}
              onPress={source => {
                if (editMode) return;
                input.press(id, pos.command ?? THEMES[theme][id].command ?? id, source);
              }}
              onRelease={source => input.release(id, source)}
              onSelect={() => setSelectedControl(id)}
            />
          );
        })}
        {stickIds.map(sid => {
          const pos = layout.controls[sid] || defaults.controls[sid] || { x: 50, y: 50 };
          if (pos.hidden) return null;
          return (
            <GpJoystick
              key={`${activeProfile}-${theme}-${sid}`} id={sid}
              pos={{ ...pos, w: (pos.w ?? 120) * layoutScale }}
              editMode={editMode}
              isSelected={selectedControl === sid}
              showHitboxes={showHitboxes}
              canvasRef={canvasRef}
              onDrag={(x, y) => updatePos(sid, x, y)}
              onMove={(x, y) => { if (!editMode || (x === 0 && y === 0)) sendJoystick(sid.replace('Stick', ''), x, y); }}
              onSelect={() => setSelectedControl(sid)}
            />
          );
        })}

        <EditBar 
          editMode={editMode} selectedControl={selectedControl} layout={layout} theme={theme}
          maxWidth={Math.max(24, Math.floor(canvasSize.width / layoutScale))}
          maxHeight={Math.max(24, Math.floor(canvasSize.height / layoutScale))}
          setDimension={setDimension} patchControl={patchControl} nudgePos={nudgePos}
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
