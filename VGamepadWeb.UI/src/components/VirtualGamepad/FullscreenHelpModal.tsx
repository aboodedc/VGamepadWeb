import { useLanguage } from './LanguageContext';

export function FullscreenHelpModal({ reason, error, close, enterGameMode }: {
  reason: 'unsupported' | 'rejected' | 'standalone' | null;
  error: string;
  close: () => void;
  enterGameMode: () => void;
}) {
  const { t } = useLanguage();
  if (!reason) return null;
  return <div className="gp-overlay" onClick={close}>
    <section className="gp-modal gp-fullscreen-help" role="dialog" aria-modal="true" aria-labelledby="presentation-title" onClick={e => e.stopPropagation()}>
      <h3 id="presentation-title">{t.screenOptions}</h3>
      <p role="status">{reason === 'standalone' ? t.standaloneActive : reason === 'unsupported' ? t.fullscreenUnavailable : t.fullscreenRejected}</p>
      {error && <details><summary>{t.details}</summary><p>{error}</p></details>}
      <p>{t.gameModeDescription}</p>
      {reason !== 'standalone' && <>
        <h4>{t.homeScreenTitle}</h4>
        <p>{t.homeScreenInstructions}</p>
        <p className="gp-muted">{t.homeScreenStorage}</p>
      </>}
      <div className="gp-modal-btns">
        <button className="gp-mbtn green" onClick={() => { enterGameMode(); close(); }}>{t.gameMode}</button>
        <button className="gp-mbtn" onClick={close}>{t.close}</button>
      </div>
    </section>
  </div>;
}
