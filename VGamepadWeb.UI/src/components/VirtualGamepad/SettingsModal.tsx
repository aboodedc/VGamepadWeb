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
  enableVib: boolean;
  setEnableVib: (enable: boolean) => void;
  sensitivity: number;
  setSensitivity: (sens: number) => void;
  connStatus: 'off' | 'ing' | 'on';
  disconnect: () => void;
}

/**
 * 连上之后的设置面板：只管外观与手柄参数。
 * 地址、密码、连接都搬去了连接覆盖层（ConnectionGate），这里不再重复；
 * 只留一个「断开连接」作为主动退出门的出口。
 */
export const SettingsModal: React.FC<SettingsModalProps> = ({
  settingsOpen, setSettingsOpen, activeProfile, profiles, switchProfile, createNewProfile, deleteProfile,
  theme, setTheme, enableVib, setEnableVib, sensitivity, setSensitivity, connStatus, disconnect
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

        <div className="gp-modal-btns">
          {connStatus === 'on' && (
            <button className="gp-mbtn red" onClick={disconnect}>{t.disconnect}</button>
          )}
          <button className="gp-mbtn" onClick={() => setSettingsOpen(false)}>{t.close}</button>
        </div>
      </div>
    </div>
  );
};
