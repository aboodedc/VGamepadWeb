import React from 'react';
import { useLanguage } from './LanguageContext';
import type { Language } from './i18n';

const LANGUAGE_OPTIONS: { value: Language; flag: string }[] = [
  { value: 'ar', flag: '🇸🇦' },
  { value: 'en', flag: '🇬🇧' },
  { value: 'zh', flag: '🇨🇳' },
];

interface ConnectionGateProps {
  /** true = 已连上，覆盖层收起，用户可以开始用手柄。 */
  connected: boolean;
  connecting: boolean;
  notice: string | null;
  serverUrl: string;
  setServerUrl: (url: string) => void;
  useSameServer: boolean;
  setUseSameServer: (val: boolean) => void;
  serverPassword: string;
  setServerPassword: (pass: string) => void;
  connect: () => void;
  toggleFullscreen: () => void;
  isFullscreen: boolean;
}

/**
 * 未连接时盖住整个页面的「门」。
 * 连接相关的一切（地址、密码、连接按钮、全屏、语言）都收在这里：
 * 连上之前用户没法绕过它去操作手柄，连上之后覆盖层消失、顶栏接管。
 */
export const ConnectionGate: React.FC<ConnectionGateProps> = ({
  connected, connecting, notice,
  serverUrl, setServerUrl, useSameServer, setUseSameServer,
  serverPassword, setServerPassword, connect, toggleFullscreen, isFullscreen
}) => {
  const { t, language, setLanguage } = useLanguage();

  if (connected) return null;

  const languageLabel = (lang: Language) =>
    lang === 'ar' ? t.arabic : lang === 'zh' ? t.chinese : t.english;

  return (
    <div className="gp-gate">
      <div className="gp-gate-panel">
        <h2 className="gp-gate-title">{t.gateTitle}</h2>
        <p className="gp-gate-sub">{t.gateSubtitle}</p>

        {notice && <div className="gp-gate-notice">{notice}</div>}

        <label className="gp-gate-label">{t.useSameServer}</label>
        <label className="gp-gate-check">
          <input
            type="checkbox"
            checked={useSameServer}
            onChange={e => setUseSameServer(e.target.checked)}
          />
          {t.useSameServerHint}
        </label>

        {!useSameServer && (
          <>
            <label className="gp-gate-label">{t.customServer}</label>
            <input
              className="gp-input"
              value={serverUrl}
              onChange={e => setServerUrl(e.target.value)}
              placeholder="http://192.168.1.x:5000"
              autoCapitalize="off"
              autoCorrect="off"
            />
          </>
        )}

        <label className="gp-gate-label">{t.password}</label>
        <input
          type="password"
          className="gp-input"
          value={serverPassword}
          onChange={e => setServerPassword(e.target.value)}
          placeholder={t.passwordPlaceholder}
          autoCapitalize="off"
          autoCorrect="off"
        />

        <button className="gp-mbtn green gp-gate-connect" onClick={connect} disabled={connecting}>
          {connecting ? t.connecting : t.connect}
        </button>

        <div className="gp-gate-row">
          <button className="gp-gate-small" onClick={toggleFullscreen}>
            {isFullscreen ? t.minimize : t.fullscreen}
          </button>

          <div className="gp-gate-langs">
            {LANGUAGE_OPTIONS.map(opt => (
              <button
                key={opt.value}
                className={`gp-gate-small ${language === opt.value ? 'active' : ''}`}
                onClick={() => setLanguage(opt.value)}
              >
                <span>{opt.flag}</span>
                <span>{languageLabel(opt.value)}</span>
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};
