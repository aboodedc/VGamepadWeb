import React from 'react';
import { useLanguage } from './LanguageContext';

interface AboutModalProps {
  open: boolean;
  onClose: () => void;
}

const GITHUB_URL   = 'https://github.com/aboodedc/VGamepadWeb';
const LINKEDIN_URL = 'https://www.linkedin.com/in/abdullrahman-alhatab';

export const AboutModal: React.FC<AboutModalProps> = ({ open, onClose }) => {
  const { t } = useLanguage();
  if (!open) return null;

  return (
    <div className="gp-overlay" onClick={onClose}>
      <div className="gp-about-modal" onClick={e => e.stopPropagation()}>

        {/* Header glow */}
        <div className="gp-about-glow" />

        {/* Logo / Title */}
        <div className="gp-about-header">
          <div className="gp-about-logo">🎮</div>
          <h2 className="gp-about-title">{t.aboutTitle}</h2>
          <p className="gp-about-version">{t.aboutTagline}</p>
        </div>

        {/* Author card */}
        <div className="gp-about-card">
          <p className="gp-about-made">{t.aboutMadeWith}</p>
          <p className="gp-about-name">{t.aboutName}</p>
          <a
            className="gp-about-email"
            href={`mailto:${t.aboutEmail}`}
            onClick={e => e.stopPropagation()}
          >
            ✉️ {t.aboutEmail}
          </a>
        </div>

        {/* Social / action buttons */}
        <div className="gp-about-actions">
          {/* GitHub star */}
          <a
            href={GITHUB_URL}
            target="_blank"
            rel="noopener noreferrer"
            className="gp-about-btn star"
            onClick={e => e.stopPropagation()}
          >
            {t.aboutGithubStar}
          </a>

          {/* LinkedIn */}
          <a
            href={LINKEDIN_URL}
            target="_blank"
            rel="noopener noreferrer"
            className="gp-about-btn linkedin"
            onClick={e => e.stopPropagation()}
          >
            {t.aboutLinkedIn}
          </a>
        </div>

        {/* MIT license */}
        <p className="gp-about-mit">⚖️ {t.aboutMIT}</p>

        {/* Close */}
        <button className="gp-about-close" onClick={onClose}>✕</button>
      </div>
    </div>
  );
};
