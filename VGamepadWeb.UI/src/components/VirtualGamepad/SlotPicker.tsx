import React from 'react';
import { useLanguage } from './LanguageContext';
import type { SlotInfo } from './hooks/useGamepadConnection';

interface SlotPickerProps {
  open: boolean;
  slots: SlotInfo[] | null;
  mySlot: number | null;
  recommended: number | null;
  onSelect: (slot: number) => void;
  onCancel: () => void;
}

export const SlotPicker: React.FC<SlotPickerProps> = ({ open, slots, mySlot, recommended, onSelect, onCancel }) => {
  const { t } = useLanguage();
  if (!open) return null;

  const label = (slot: number, state: string) =>
    t.slotLabel.replace('{num}', String(slot + 1)).replace('{state}', state);

  const canTake = (slot: number) => {
    const info = (slots ?? []).find(s => s.slot === slot);
    return !!info && !info.occupied && !info.locked;
  };

  const reconnectSlot = recommended !== null && canTake(recommended) ? recommended : null;

  return (
    <div className="gp-overlay" onClick={onCancel}>
      <div className="gp-modal" onClick={e => e.stopPropagation()}>
        <h3>{t.slotTitle}</h3>
        {reconnectSlot !== null && (
          <button className="gp-mbtn green gp-slot-reconnect" onClick={() => onSelect(reconnectSlot)}>
            {t.slotReconnect.replace('{num}', String(reconnectSlot + 1))}
          </button>
        )}
        <div className="gp-slot-grid">
          {(slots ?? []).map(s => {
            const isYou = mySlot !== null && mySlot === s.slot;
            const selectable = !isYou && !s.locked && !s.occupied;
            const state = isYou ? t.slotYou : s.locked ? t.slotLocked : s.occupied ? t.slotOccupied : t.slotFree;
            return (
              <button
                key={s.slot}
                className={`gp-slot ${isYou ? 'you' : ''} ${s.slot === recommended ? 'last' : ''}`}
                disabled={!selectable}
                onClick={() => onSelect(s.slot)}
              >
                {label(s.slot, state)}
                {s.slot === recommended && <span className="gp-slot-badge">{t.slotLast}</span>}
              </button>
            );
          })}
        </div>
        <p className="gp-slot-hint">{slots && slots.length > 0 ? t.slotHint : t.slotNoData}</p>
        <div className="gp-modal-btns">
          <button className="gp-mbtn" onClick={onCancel}>{t.close}</button>
        </div>
      </div>
    </div>
  );
};
