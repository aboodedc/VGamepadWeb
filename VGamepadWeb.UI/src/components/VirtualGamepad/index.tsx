import React, { useState, useEffect, useRef, useCallback } from 'react';
import type { GamepadLayout, ThemeType } from './types';
import { LAYOUT_KEY, URL_KEY, DEFAULT_LAYOUT, THEMES, BTN_IDS, STICK_IDS } from './constants';
import { loadLayout, clamp } from './utils';
import { GpButton } from './GpButton';
import { GpJoystick } from './GpJoystick';
import { TopBar } from './TopBar';
import { SettingsModal } from './SettingsModal';
import { EditBar } from './EditBar';
import { useGamepadConnection } from './hooks/useGamepadConnection';
import { useMotionSensors } from './hooks/useMotionSensors';
import { LanguageProvider, useLanguage } from './LanguageContext';

const VirtualGamepadInner: React.FC = () => {
  const { t } = useLanguage();
  const [activeProfile, setActiveProfile] = useState<string>(() => localStorage.getItem('gamepad_active_profile') || 'default');
  const [profiles, setProfiles] = useState<{id: string, name: string}[]>(() => {
    try { 
      const p = localStorage.getItem('gamepad_profiles');
      if (p) return JSON.parse(p);
    } catch {}
    return [{ id: 'default', name: 'Default' }];
  });

  const [theme, setTheme] = useState<ThemeType>(() => (localStorage.getItem('gamepad_theme') as ThemeType) || 'xbox');
  const [layout, setLayout] = useState<GamepadLayout>(() => {
    const initialTheme = (localStorage.getItem('gamepad_theme') as ThemeType) || 'xbox';
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
  const [enableGyro, setEnableGyro] = useState<boolean>(() => localStorage.getItem('gamepad_gyro') !== 'false');
  const [motionOrientation, setMotionOrientation] = useState<string>(() => localStorage.getItem('gamepad_orientation') || 'Horizontal');
  const [activeButtons, setActiveButtons] = useState<Set<string>>(new Set());
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isBarHidden, setIsBarHidden] = useState(false);
  const [selectedControl, setSelectedControl] = useState<string | null>(null);
  const [visibilityMenuOpen, setVisibilityMenuOpen] = useState(false);
  const [showHitboxes, setShowHitboxes] = useState(false);

  const canvasRef = useRef<HTMLDivElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  const actualServerUrl = useSameServer ? window.location.origin : serverUrl;
  const { connStatus, latency, controllerId, dataChannel, connect, disconnect, sendButton, sendJoystick } = useGamepadConnection({
    serverUrl: actualServerUrl, serverPassword, controllerType, enableVib, sensitivity, enableGyro, motionOrientation
  });
  const { isSupported, needsPermission, requestPermission, isActive, start, stop, dataRef } = useMotionSensor();

  const { requestPermission } = useMotionSensors(dataChannel);

  const handleConnect = async () => {
    await requestPermission();
    await connect();
  };

  // Clear selection when exiting edit mode
  useEffect(() => { 
    if (!editMode) {
      setSelectedControl(null);
      setVisibilityMenuOpen(false);
    }
  }, [editMode]);

  const rafRef = useRef<number | null>(null);

  // Start/stop motion sensor based on connection + motionEnabled
  useEffect(() => {
    if (connStatus === 'on' && motionEnabled) {
      (async () => {
        if (needsPermission) {
          const ok = await requestPermission();
          if (!ok) return;
        }
        start();
      })();
    } else {
      stop();
    }
  }, [connStatus, motionEnabled]);

  // RAF loop — send motion at max available rate
  useEffect(() => {
    if (connStatus !== 'on' || !motionEnabled || !isActive) return;

    const sendLoop = () => {
      if (connStatus !== 'on' || !motionEnabled) return;
      const d = dataRef.current;
      sendMotion(d.accel.x, d.accel.y, d.accel.z, d.gyro.x, d.gyro.y, d.gyro.z);
      rafRef.current = requestAnimationFrame(sendLoop);
    };

    rafRef.current = requestAnimationFrame(sendLoop);
    return () => {
      if (rafRef.current != null) {
        cancelAnimationFrame(rafRef.current);
        rafRef.current = null;
      }
    };
  }, [connStatus, motionEnabled, isActive, sendMotion]);

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
  useEffect(() => { localStorage.setItem('gamepad_active_profile', activeProfile); }, [activeProfile]);
  useEffect(() => { localStorage.setItem('gamepad_profiles', JSON.stringify(profiles)); }, [profiles]);
  useEffect(() => { localStorage.setItem(URL_KEY, serverUrl); }, [serverUrl]);
  useEffect(() => { localStorage.setItem('gamepad_use_same_server', useSameServer.toString()); }, [useSameServer]);
  useEffect(() => { localStorage.setItem('gamepad_ctype', controllerType.toString()); }, [controllerType]);
  useEffect(() => { localStorage.setItem('gamepad_vib', enableVib.toString()); }, [enableVib]);
  useEffect(() => { localStorage.setItem('gamepad_sens', sensitivity.toString()); }, [sensitivity]);
  useEffect(() => { localStorage.setItem('gamepad_gyro', enableGyro.toString()); }, [enableGyro]);
  useEffect(() => { localStorage.setItem('gamepad_orientation', motionOrientation); }, [motionOrientation]);
  useEffect(() => { localStorage.setItem('gamepad_pass', serverPassword); }, [serverPassword]);
  useEffect(() => { localStorage.setItem('gamepad_motion', motionEnabled.toString()); }, [motionEnabled]);
  useEffect(() => { localStorage.setItem('gamepad_dsu_port', dsuPort.toString()); }, [dsuPort]);

  // Reconnect automatically when orientation or server URL changes while connected
  const isFirstMountOrConnRef = useRef(true);
  const prevOrientation = useRef(motionOrientation);
  const prevServerUrl = useRef(actualServerUrl);

  useEffect(() => {
    if (isFirstMountOrConnRef.current) {
      isFirstMountOrConnRef.current = false;
      prevOrientation.current = motionOrientation;
      prevServerUrl.current = actualServerUrl;
      return;
    }

    const orientationChanged = prevOrientation.current !== motionOrientation;
    const serverUrlChanged = prevServerUrl.current !== actualServerUrl;

    if (orientationChanged || serverUrlChanged) {
      prevOrientation.current = motionOrientation;
      prevServerUrl.current = actualServerUrl;

      if (connStatus === 'on') {
        console.log('[Connection] Reconnecting due to settings change...');
        (async () => {
          await disconnect();
          await handleConnect();
        })();
      }
    }
  }, [motionOrientation, actualServerUrl, connStatus, disconnect, handleConnect]);

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
        isFullscreen={isFullscreen} toggleFullscreen={toggleFullscreen} editMode={editMode} setEditMode={setEditMode}
        menuOpen={menuOpen} setMenuOpen={setMenuOpen} visibilityMenuOpen={visibilityMenuOpen}
        setVisibilityMenuOpen={setVisibilityMenuOpen} setSettingsOpen={setSettingsOpen} resetLayout={resetLayout}
        exportLayout={exportLayout} fileRef={fileRef} importLayout={importLayout} layout={layout} toggleVisibility={toggleVisibility}
        showHitboxes={showHitboxes} setShowHitboxes={setShowHitboxes}
      />

      <SettingsModal 
        settingsOpen={settingsOpen} setSettingsOpen={setSettingsOpen} activeProfile={activeProfile}
        profiles={profiles} switchProfile={switchProfile} createNewProfile={createNewProfile} deleteProfile={deleteProfile}
        theme={theme} setTheme={setTheme} controllerType={controllerType} setControllerType={setControllerType}
        enableVib={enableVib} setEnableVib={setEnableVib} sensitivity={sensitivity} setSensitivity={setSensitivity}
        enableGyro={enableGyro} setEnableGyro={setEnableGyro} motionOrientation={motionOrientation} setMotionOrientation={setMotionOrientation}
        useSameServer={useSameServer} setUseSameServer={setUseSameServer}
        serverUrl={serverUrl} setServerUrl={setServerUrl} serverPassword={serverPassword} setServerPassword={setServerPassword} 
        connStatus={connStatus} connect={handleConnect} disconnect={disconnect}
      />

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
