import React, { useEffect, useLayoutEffect, useRef } from 'react';
import type { Pos, BtnCfg } from './types';
import { clamp } from './utils';

export const GpButton: React.FC<{
  id: string; cfg: BtnCfg; pos: Pos;
  editMode: boolean; active: boolean; isSelected: boolean;
  canvasRef: React.RefObject<HTMLDivElement | null>;
  onDrag: (x: number, y: number) => void;
  onPress: (source: string) => void; onRelease: (source: string) => void;
  scale?: number;
  onSelect: () => void;
  showHitboxes: boolean;
}> = ({ id, cfg, pos, editMode, active, isSelected, canvasRef, onDrag, onPress, onRelease, onSelect, showHitboxes, scale = 1 }) => {
  const dragRef = useRef<{ tid: number; sx: number; sy: number; ox: number; oy: number; moved: boolean } | null>(null);
  const onDragRef = useRef(onDrag);
  const onSelectRef = useRef(onSelect);
  const onReleaseRef = useRef(onRelease);
  useLayoutEffect(() => { onDragRef.current = onDrag; onSelectRef.current = onSelect; onReleaseRef.current = onRelease; });
  const mouseCleanup = useRef<(() => void) | null>(null);
  const lastTouchAt = useRef(-Infinity);
  useEffect(() => { dragRef.current = null; mouseCleanup.current?.(); mouseCleanup.current = null; }, [editMode]);

  const effectiveW = (pos.w ?? cfg.w) * scale;
  const effectiveH = (pos.h ?? cfg.h) * scale;
  const effectiveRound = pos.round ?? cfg.round;
  const color = pos.color ?? cfg.color;
  const isPad = (pos.appearance ?? cfg.appearance) === 'pad';

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
      for (const touch of Array.from(e.changedTouches)) onReleaseRef.current(`touch-${touch.identifier}`);
      const d = dragRef.current; if (!d) return;
      for (let i = 0; i < e.changedTouches.length; i++) {
        if (e.changedTouches[i].identifier === d.tid) {
          if (!d.moved && e.type !== 'touchcancel') onSelectRef.current();
          dragRef.current = null;
        }
      }
    };
    document.addEventListener('touchmove', move, { passive: false });
    document.addEventListener('touchend', end);
    document.addEventListener('touchcancel', end);
    const mouseEnd = () => onReleaseRef.current('mouse');
    document.addEventListener('mouseup', mouseEnd);
    return () => {
      document.removeEventListener('touchmove', move); document.removeEventListener('touchend', end);
      document.removeEventListener('touchcancel', end); document.removeEventListener('mouseup', mouseEnd);
      mouseCleanup.current?.();
    };
  }, [canvasRef]);

  const onTouchStart = (e: React.TouchEvent) => {
    e.stopPropagation();
    lastTouchAt.current = performance.now();
    if (editMode) {
      const t = e.changedTouches[0];
      dragRef.current = { tid: t.identifier, sx: t.clientX, sy: t.clientY, ox: pos.x, oy: pos.y, moved: false };
    } else {
      for (const touch of Array.from(e.changedTouches)) onPress(`touch-${touch.identifier}`);
      if (navigator.vibrate) navigator.vibrate(12);
    }
  };
  const onTouchEnd = (e: React.TouchEvent) => {
    for (const touch of Array.from(e.changedTouches)) onRelease(`touch-${touch.identifier}`);
  };

  const onMouseDown = (e: React.MouseEvent) => {
    e.preventDefault();
    if (e.button !== 0 || performance.now() - lastTouchAt.current < 800) return;
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
        mouseCleanup.current = null;
      };
      mouseCleanup.current?.();
      mouseCleanup.current = () => { document.removeEventListener('mousemove', mm); document.removeEventListener('mouseup', mu); };
      document.addEventListener('mousemove', mm);
      document.addEventListener('mouseup', mu);
    } else { onPress('mouse'); }
  };
  const onMouseUp = () => onRelease('mouse');
  const onMouseLeave = () => { if (!editMode) onRelease('mouse'); };

  return (
    <div
      className={`gp-ctrl ${editMode ? 'edit' : ''} ${isSelected ? 'selected' : ''}`}
      data-control={id} data-command={pos.command ?? cfg.command ?? id}
      role="button" aria-label={id === 'Start' ? 'Start' : pos.label ?? id} aria-pressed={active} tabIndex={0}
      style={{ left: `clamp(0px, ${pos.x}%, max(0px, calc(100% - ${effectiveW}px)))`, top: `clamp(0px, ${pos.y}%, max(0px, calc(100% - ${effectiveH}px)))` }}
      onTouchStart={onTouchStart} onTouchEnd={onTouchEnd} onTouchCancel={onTouchEnd}
      onMouseDown={onMouseDown} onMouseUp={onMouseUp} onMouseLeave={onMouseLeave}
      onKeyDown={e => { if (['Enter', ' '].includes(e.key)) { e.preventDefault(); if (editMode) onSelect(); else onPress('keyboard'); } }}
      onKeyUp={e => { if (['Enter', ' '].includes(e.key)) { e.preventDefault(); onRelease('keyboard'); } }}
      onBlur={() => onRelease('keyboard')}
      onContextMenu={e => e.preventDefault()}
    >
      <div
        className={`gp-btn ${isPad ? 'gp-pad' : ''} ${active ? 'active' : ''} ${effectiveRound ? 'round' : 'rect'} ${showHitboxes ? 'show-hitbox' : ''}`}
        style={{
          width: effectiveW,
          height: effectiveH,
          fontSize: isPad ? clamp(Math.min(effectiveW, effectiveH) * 0.2, 11, 22) : Math.min(effectiveW, effectiveH) * 0.9,
          color: isPad ? '#fff' : active ? '#fff' : color,
          ...(isPad ? { '--pad-color': color } as React.CSSProperties : {}),
          filter: active
            ? `drop-shadow(0 0 8px ${color}) brightness(1.3)`
            : `drop-shadow(0 2px 4px rgba(0,0,0,0.6))`,
        }}
      >
        {pos.label ?? cfg.label}
        {isPad && id !== 'Start' && <span className="gp-pad-command">{pos.command ?? cfg.command ?? id}</span>}
      </div>
      {editMode && <span className="gp-ctrl-label">{id}</span>}
    </div>
  );
};
