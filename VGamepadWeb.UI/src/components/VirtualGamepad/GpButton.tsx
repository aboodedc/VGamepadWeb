import React, { useEffect, useRef } from 'react';
import type { Pos, BtnCfg } from './types';
import { clamp } from './utils';

export const GpButton: React.FC<{
  id: string; cfg: BtnCfg; pos: Pos;
  editMode: boolean; active: boolean; isSelected: boolean;
  canvasRef: React.RefObject<HTMLDivElement | null>;
  onDrag: (x: number, y: number) => void;
  onPress: () => void; onRelease: () => void;
  onSelect: () => void;
  showHitboxes: boolean;
}> = ({ id, cfg, pos, editMode, active, isSelected, canvasRef, onDrag, onPress, onRelease, onSelect, showHitboxes }) => {
  const dragRef = useRef<{ tid: number; sx: number; sy: number; ox: number; oy: number; moved: boolean } | null>(null);
  const onDragRef = useRef(onDrag); onDragRef.current = onDrag;
  const onSelectRef = useRef(onSelect); onSelectRef.current = onSelect;

  const effectiveW = pos.w ?? cfg.w;
  const effectiveH = pos.h ?? cfg.h;
  const effectiveRound = pos.round ?? cfg.round;

  useEffect(() => {
    const move = (e: TouchEvent) => {
      const d = dragRef.current; if (!d) return;
      for (let i = 0; i < e.changedTouches.length; i++) {
        const t = e.changedTouches[i];
        if (t.identifier !== d.tid) continue;
        e.preventDefault();
        if (Math.abs(t.clientX - d.sx) > 5 || Math.abs(t.clientY - d.sy) > 5) d.moved = true;
        const c = canvasRef.current; if (!c) return;
        const r = c.getBoundingClientRect();
        onDragRef.current(
          clamp(d.ox + ((t.clientX - d.sx) / r.width) * 100, 0, 95),
          clamp(d.oy + ((t.clientY - d.sy) / r.height) * 100, 0, 95)
        );
      }
    };
    const end = (e: TouchEvent) => {
      const d = dragRef.current; if (!d) return;
      for (let i = 0; i < e.changedTouches.length; i++) {
        if (e.changedTouches[i].identifier === d.tid) {
          if (!d.moved) onSelectRef.current();
          dragRef.current = null;
        }
      }
    };
    document.addEventListener('touchmove', move, { passive: false });
    document.addEventListener('touchend', end);
    return () => { document.removeEventListener('touchmove', move); document.removeEventListener('touchend', end); };
  }, [canvasRef]);

  const onTouchStart = (e: React.TouchEvent) => {
    e.preventDefault(); e.stopPropagation();
    if (editMode) {
      const t = e.changedTouches[0];
      dragRef.current = { tid: t.identifier, sx: t.clientX, sy: t.clientY, ox: pos.x, oy: pos.y, moved: false };
    } else {
      onPress();
      if (navigator.vibrate) navigator.vibrate(12);
    }
  };
  const onTouchEnd = (e: React.TouchEvent) => {
    e.preventDefault();
    if (!editMode) onRelease();
  };

  const onMouseDown = (e: React.MouseEvent) => {
    e.preventDefault();
    if (editMode) {
      const sx = e.clientX, sy = e.clientY, ox = pos.x, oy = pos.y;
      let moved = false;
      const mm = (me: MouseEvent) => {
        if (Math.abs(me.clientX - sx) > 5 || Math.abs(me.clientY - sy) > 5) moved = true;
        const c = canvasRef.current; if (!c) return;
        const r = c.getBoundingClientRect();
        onDragRef.current(
          clamp(ox + ((me.clientX - sx) / r.width) * 100, 0, 95),
          clamp(oy + ((me.clientY - sy) / r.height) * 100, 0, 95)
        );
      };
      const mu = () => {
        if (!moved) onSelectRef.current();
        document.removeEventListener('mousemove', mm);
        document.removeEventListener('mouseup', mu);
      };
      document.addEventListener('mousemove', mm);
      document.addEventListener('mouseup', mu);
    } else { onPress(); }
  };
  const onMouseUp = () => { if (!editMode) onRelease(); };
  const onMouseLeave = () => { if (!editMode && active) onRelease(); };

  return (
    <div
      className={`gp-ctrl ${editMode ? 'edit' : ''} ${isSelected ? 'selected' : ''}`}
      style={{ left: `${pos.x}%`, top: `${pos.y}%` }}
      onTouchStart={onTouchStart} onTouchEnd={onTouchEnd} onTouchCancel={onTouchEnd}
      onMouseDown={onMouseDown} onMouseUp={onMouseUp} onMouseLeave={onMouseLeave}
      onContextMenu={e => e.preventDefault()}
    >
      <div
        className={`gp-btn ${active ? 'active' : ''} ${effectiveRound ? 'round' : 'rect'} ${showHitboxes ? 'show-hitbox' : ''}`}
        style={{
          width: effectiveW,
          height: effectiveH,
          fontSize: Math.min(effectiveW, effectiveH) * 0.9,
          color: active ? '#fff' : cfg.color,
          filter: active
            ? `drop-shadow(0 0 8px ${cfg.color}) brightness(1.3)`
            : `drop-shadow(0 2px 4px rgba(0,0,0,0.6))`,
        }}
      >
        {cfg.label}
      </div>
      {editMode && <span className="gp-ctrl-label">{id}</span>}
    </div>
  );
};
