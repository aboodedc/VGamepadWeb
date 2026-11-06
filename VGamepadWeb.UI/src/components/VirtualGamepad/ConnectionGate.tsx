import React from 'react';
import { useLanguage } from './LanguageContext';
import type { Language } from './i18n';
import type { ThemeType } from './types';

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
  serverPassword: string;
  setServerPassword: (pass: string) => void;
  /** 手柄类型 = 服务端创建的设备 + 按键外观。0 = Xbox 360 设备，1 = DualShock 4 设备。 */
  theme: ThemeType;
  setTheme: (theme: ThemeType) => void;
  connect: () => void;
  toggleFullscreen: () => void;
  isFullscreen: boolean;
}

/**
 * 未连接时盖住整个页面的「门」。
 * 连接相关的一切（密码、连接按钮、全屏、语言）都收在这里：
 * 连上之前用户没法绕过它去操作手柄，连上之后覆盖层消失、顶栏接管。
 *
 * 不再提供服务器地址输入：这个页面本身就由那台 PC 的服务器提供，
 * 还能连到哪去。地址恒为当前页面的 origin。
 */
export const ConnectionGate: React.FC<ConnectionGateProps> = ({
  connected, connecting, notice,
  serverPassword, setServerPassword,
  theme, setTheme,
  connect, toggleFullscreen, isFullscreen
}) => {
  const { t, language, setLanguage } = useLanguage();

  if (connected) return null;

  const languageLabel = (lang: Language) =>
    lang === 'ar' ? t.arabic : lang === 'zh' ? t.chinese : t.english;

  return (
    <div className="gp-gate">
      <div className="gp-gate-panel">
        <h2 className="gp-gate-title">{t.gateTitle}</h2>

        {/* 手柄页本来就是横向铺开更好用；能不能真的转过去由浏览器决定，这里先把话说明白。 */}
        <p className="gp-gate-hint">{t.gateLandscape}</p>

        {notice && <div className="gp-gate-notice">{notice}</div>}

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

        {/* 手柄类型本身就是「设备 + 按键外观」的组合：括号里写清楚按键画的是谁。
            服务端只认 Xbox 360 / DualShock 4 两种设备，Nintendo Switch 只是 Xbox 360 上的按键画法。
            连上后覆盖层消失，就没有改的入口了。 */}
        <label className="gp-gate-label" htmlFor="gp-gate-type">{t.controllerType}</label>
        <select
          id="gp-gate-type"
          className="gp-gate-type"
          value={theme}
          onChange={e => setTheme(e.target.value as ThemeType)}
          disabled={connecting}
        >
          <option value="xbox">{t.typeXbox360}（Xbox）</option>
          <option value="nintendo">{t.typeXbox360}（Nintendo Switch）</option>
          <option value="ps">{t.typeDualShock4}（PlayStation）</option>
        </select>

        <button className="gp-mbtn green gp-gate-connect" onClick={connect} disabled={connecting}>
          {connecting ? t.connecting : t.connect}
        </button>

        <div className="gp-gate-row">
          <button className="gp-gate-small" onClick={toggleFullscreen}>
            {isFullscreen ? t.minimize : t.fullscreen}
          </button>

          {/* 语言用下拉框：语言一多，按钮排不下。 */}
          <select
            className="gp-input gp-gate-lang"
            value={language}
            onChange={e => setLanguage(e.target.value as Language)}
            aria-label={t.language}
          >
            {LANGUAGE_OPTIONS.map(opt => (
              <option key={opt.value} value={opt.value}>
                {opt.flag} {languageLabel(opt.value)}
              </option>
            ))}
          </select>
        </div>
      </div>
    </div>
  );
};
