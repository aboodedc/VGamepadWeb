import React from 'react';
import { useLanguage } from './LanguageContext';
import type { SlotInfo } from './hooks/useGamepadConnection';

interface SlotPickerProps {
  open: boolean;
  slots: SlotInfo[] | null;
  mySlot: number | null;
  onSelect: (slot: number) => void;
  onCancel: () => void;
}

export const SlotPicker: React.FC<SlotPickerProps> = ({ open, slots, mySlot, onSelect, onCancel }) => {
  const { t } = useLanguage();
  if (!open) return null;

  const label = (slot: number, state: string) =>
    t.slotLabel.replace('{num}', String(slot + 1)).replace('{state}', state);

  return (
    <div className="gp-overlay" onClick={onCancel}>
      <div className="gp-modal" onClick={e => e.stopPropagation()}>
        <h3>{t.slotTitle}</h3>
        <div className="gp-slot-grid">
          {(slots ?? []).map(s => {
            const isYou = mySlot !== null && mySlot === s.slot;
            const selectable = !isYou && !s.locked && !s.occupied;
            const state = isYou ? t.slotYou : s.locked ? t.slotLocked : s.occupied ? t.slotOccupied : t.slotFree;
            return (
              <button
                key={s.slot}
                className={`gp-slot ${isYou ? 'you' : ''}`}
                disabled={!selectable}
                onClick={() => onSelect(s.slot)}
              >
                {label(s.slot, state)}
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
