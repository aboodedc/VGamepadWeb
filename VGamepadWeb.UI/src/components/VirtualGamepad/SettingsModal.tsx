import React from 'react';
import type { ThemeType, Profile } from './types';
import { useLanguage } from './LanguageContext';

interface SettingsModalProps {
  settingsOpen: boolean;
  setSettingsOpen: (open: boolean) => void;
  activeProfile: string;
  profiles: Profile[];
  switchProfile: (id: string) => void;
  createNewProfile: () => void;
  deleteProfile: (id: string) => void;
  theme: ThemeType;
  setTheme: (theme: ThemeType) => void;
  controllerType: number;
  setControllerType: (type: number) => void;
  enableVib: boolean;
  setEnableVib: (enable: boolean) => void;
  sensitivity: number;
  setSensitivity: (sens: number) => void;
  enableGyro: boolean;
  setEnableGyro: (enable: boolean) => void;
  motionOrientation: string;
  setMotionOrientation: (orient: string) => void;
  useSameServer?: boolean;
  setUseSameServer?: (val: boolean) => void;
  serverUrl: string;
  setServerUrl: (url: string) => void;
  serverPassword?: string;
  setServerPassword?: (pass: string) => void;
  connStatus: 'off' | 'ing' | 'on';
  connect: () => void;
  disconnect: () => void;
  motionEnabled: boolean;
  setMotionEnabled: (val: boolean) => void;
  dsuPort: number;
  setDsuPort: (val: number) => void;
}

export const SettingsModal: React.FC<SettingsModalProps> = ({
  settingsOpen, setSettingsOpen, activeProfile, profiles, switchProfile, createNewProfile, deleteProfile,
  theme, setTheme, controllerType, setControllerType, enableVib, setEnableVib, sensitivity, setSensitivity,
  enableGyro, setEnableGyro, motionOrientation, setMotionOrientation,
  useSameServer, setUseSameServer, serverUrl, setServerUrl, serverPassword, setServerPassword, connStatus, connect, disconnect
}) => {
  const { t } = useLanguage();

  if (!settingsOpen) return null;

  return (
    <div className="gp-overlay" onClick={() => setSettingsOpen(false)}>
      <div className="gp-modal" onClick={e => e.stopPropagation()}>
        <h3>{t.settings}</h3>
        
        <label>{t.profile}</label>
        <div style={{ display: 'flex', gap: '8px', marginBottom: '16px' }}>
          <select 
            className="gp-input" 
            value={activeProfile}
            onChange={e => switchProfile(e.target.value)}
            style={{ flex: 1, marginBottom: 0 }}
          >
            {profiles.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
          </select>
          <button className="gp-mbtn green" style={{ padding: '0 12px', flex: 'none' }} onClick={createNewProfile}>+</button>
          <button className="gp-mbtn red" style={{ padding: '0 12px', flex: 'none' }} onClick={() => deleteProfile(activeProfile)} disabled={profiles.length <= 1}>-</button>
        </div>

        <label>{t.buttonStyle}</label>
        <select 
          className="gp-input" 
          value={theme}
          onChange={e => setTheme(e.target.value as ThemeType)}
          style={{ marginBottom: '16px' }}
        >
          <option value="xbox">{t.xboxDefault}</option>
          <option value="ps">PlayStation</option>
          <option value="nintendo">Nintendo Switch</option>
        </select>

        <label>{t.controllerTypeLabel}</label>
        <select 
          className="gp-input" 
          value={controllerType}
          onChange={e => setControllerType(parseInt(e.target.value, 10))}
          style={{ marginBottom: '16px' }}
          disabled={connStatus === 'on' || connStatus === 'ing'}
        >
          <option value={0}>Xbox 360</option>
          <option value={1}>DualShock 4 (PS4)</option>
        </select>

        <label style={{ display: 'flex', alignItems: 'center', marginBottom: '16px', gap: '8px', cursor: 'pointer' }}>
          <input 
            type="checkbox" 
            checked={enableVib}
            onChange={e => setEnableVib(e.target.checked)}
            style={{ width: '20px', height: '20px' }}
          />
          {t.enableVibration}
        </label>

        <label>{t.sensitivity} ({sensitivity}%)</label>
        <input 
          type="range" 
          className="gp-input" 
          min="10" max="200" 
          value={sensitivity}
          onChange={e => setSensitivity(parseInt(e.target.value, 10))}
          style={{ marginBottom: '16px' }}
        />

        <label style={{ display: 'flex', alignItems: 'center', marginBottom: '16px', gap: '8px', cursor: 'pointer' }}>
          <input 
            type="checkbox" 
            checked={enableGyro}
            onChange={e => setEnableGyro(e.target.checked)}
            style={{ width: '20px', height: '20px' }}
          />
          {t.enableGyro}
        </label>

        {enableGyro && (
          <>
            <label>{t.motionOrientationLabel}</label>
            <select 
              className="gp-input" 
              value={motionOrientation}
              onChange={e => setMotionOrientation(e.target.value)}
              style={{ marginBottom: '16px' }}
            >
              <option value="Horizontal">{t.motionOrientationHorizontal}</option>
              <option value="Vertical">{t.motionOrientationVertical}</option>
            </select>
          </>
        )}

        <label style={{ display: 'flex', alignItems: 'center', marginBottom: '16px', gap: '8px', cursor: 'pointer' }}>
          <input 
            type="checkbox" 
            checked={useSameServer}
            onChange={e => setUseSameServer && setUseSameServer(e.target.checked)}
            style={{ width: '20px', height: '20px' }}
          />
          {t.useSameServer}
        </label>

        {!useSameServer && (
          <>
            <label>{t.customServer}</label>
            <input
              className="gp-input"
              value={serverUrl}
              onChange={e => setServerUrl(e.target.value)}
              placeholder="http://192.168.1.x:5000"
              style={{ marginBottom: '16px' }}
            />
          </>
        )}

        {setServerPassword !== undefined && (
          <>
            <label>{t.password}</label>
            <input
              type="password"
              className="gp-input"
              value={serverPassword}
              onChange={e => setServerPassword(e.target.value)}
              placeholder={t.passwordPlaceholder}
              style={{ marginBottom: '16px' }}
            />
          </>
        )}

        <label style={{ display: 'flex', alignItems: 'center', marginBottom: '16px', gap: '8px', cursor: 'pointer' }}>
          <input
            type="checkbox"
            checked={motionEnabled}
            onChange={e => setMotionEnabled(e.target.checked)}
            style={{ width: '20px', height: '20px' }}
          />
          {t.enableMotionControls}
        </label>

        {motionEnabled && (
          <>
            <label>{t.dsuPort}</label>
            <input
              className="gp-input"
              type="number"
              min={1024}
              max={65535}
              value={dsuPort}
              onChange={e => setDsuPort(parseInt(e.target.value, 10) || 26760)}
              style={{ marginBottom: '16px' }}
            />
          </>
        )}

        <div className="gp-modal-btns">
          {connStatus === 'on' ? (
            <button className="gp-mbtn red" onClick={disconnect}>{t.disconnect}</button>
          ) : (
            <button className="gp-mbtn green" onClick={connect} disabled={connStatus === 'ing'}>
              {connStatus === 'ing' ? '...' : t.connect}
            </button>
          )}
          <button className="gp-mbtn" onClick={() => setSettingsOpen(false)}>{t.close}</button>
        </div>
      </div>
    </div>
  );
};
