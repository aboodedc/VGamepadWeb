import React, { useState, useEffect, useRef, useCallback } from 'react';
import type { Pos } from './types';
import { clamp } from './utils';

export const GpJoystick: React.FC<{
  id: string; pos: Pos; editMode: boolean; isSelected: boolean;
  canvasRef: React.RefObject<HTMLDivElement | null>;
  onDrag: (x: number, y: number) => void;
  onMove: (x: number, y: number) => void;
  onSelect: () => void;
  showHitboxes: boolean;
}> = ({ id, pos, editMode, isSelected, canvasRef, onDrag, onMove, onSelect, showHitboxes }) => {
  const elRef = useRef<HTMLDivElement>(null);
  const [knob, setKnob] = useState({ x: 0, y: 0 });
  const stickTouch = useRef<number | null>(null);
  const dragTouch = useRef<{ tid: number; sx: number; sy: number; ox: number; oy: number; moved: boolean } | null>(null);

  const onMoveRef = useRef(onMove); onMoveRef.current = onMove;
  const onDragRef = useRef(onDrag); onDragRef.current = onDrag;
  const editRef = useRef(editMode); editRef.current = editMode;
  const onSelectRef = useRef(onSelect); onSelectRef.current = onSelect;

  const joySize = pos.w ?? 120;
  const knobSize = Math.round(joySize * 0.4);

  const calcStick = useCallback((cx: number, cy: number) => {
    const el = elRef.current; if (!el) return;
    const r = el.getBoundingClientRect();
    const midX = r.left + r.width / 2, midY = r.top + r.height / 2;
    const maxR = r.width * 0.3;
    let dx = cx - midX, dy = cy - midY;
    const dist = Math.sqrt(dx * dx + dy * dy);
    if (dist > maxR) { dx = (dx / dist) * maxR; dy = (dy / dist) * maxR; }
    setKnob({ x: dx, y: dy });
    const nx = dx / maxR, ny = -dy / maxR; // invert Y
    const sx = Math.round(clamp(nx * 32767, -32768, 32767));
    const sy = Math.round(clamp(ny * 32767, -32768, 32767));
    onMoveRef.current(sx, sy);
  }, []);

  useEffect(() => {
    const move = (e: TouchEvent) => {
      // Edit drag
      const d = dragTouch.current;
      if (d) {
        for (let i = 0; i < e.changedTouches.length; i++) {
          const t = e.changedTouches[i];
          if (t.identifier !== d.tid) continue;
          e.preventDefault();
          if (Math.abs(t.clientX - d.sx) > 5 || Math.abs(t.clientY - d.sy) > 5) d.moved = true;
          const c = canvasRef.current; if (!c) return;
          const cr = c.getBoundingClientRect();
          onDragRef.current(
            clamp(d.ox + ((t.clientX - d.sx) / cr.width) * 100, 0, 85),
            clamp(d.oy + ((t.clientY - d.sy) / cr.height) * 100, 0, 70)
          );
        }
        return;
      }
      // Play stick
      if (stickTouch.current !== null) {
        for (let i = 0; i < e.changedTouches.length; i++) {
          const t = e.changedTouches[i];
          if (t.identifier === stickTouch.current) { e.preventDefault(); calcStick(t.clientX, t.clientY); }
        }
      }
    };
    const end = (e: TouchEvent) => {
      const dt = dragTouch.current;
      if (dt) {
        for (let i = 0; i < e.changedTouches.length; i++) {
          if (e.changedTouches[i].identifier === dt.tid) {
            if (!dt.moved) onSelectRef.current();
            dragTouch.current = null;
          }
        }
        return;
      }
      if (stickTouch.current !== null) {
        for (let i = 0; i < e.changedTouches.length; i++) {
          if (e.changedTouches[i].identifier === stickTouch.current) {
            stickTouch.current = null;
            setKnob({ x: 0, y: 0 });
            onMoveRef.current(0, 0);
          }
        }
      }
    };
    document.addEventListener('touchmove', move, { passive: false });
    document.addEventListener('touchend', end);
    // 锁屏 / 来电 / 切应用时系统只发 touchcancel：不接住它，摇杆会停在最后的位置。
    document.addEventListener('touchcancel', end);
    return () => {
      document.removeEventListener('touchmove', move);
      document.removeEventListener('touchend', end);
      document.removeEventListener('touchcancel', end);
    };
  }, [canvasRef, calcStick]);

  const onTouchStart = (e: React.TouchEvent) => {
    e.preventDefault(); e.stopPropagation();
    const t = e.changedTouches[0];
    if (editRef.current) {
      dragTouch.current = { tid: t.identifier, sx: t.clientX, sy: t.clientY, ox: pos.x, oy: pos.y, moved: false };
    } else {
      stickTouch.current = t.identifier;
      calcStick(t.clientX, t.clientY);
    }
  };

  // Mouse support for desktop
  const onMouseDown = (e: React.MouseEvent) => {
    e.preventDefault();
    if (editMode) {
      const sx = e.clientX, sy = e.clientY, ox = pos.x, oy = pos.y;
      let moved = false;
      const mm = (me: MouseEvent) => {
        if (Math.abs(me.clientX - sx) > 5 || Math.abs(me.clientY - sy) > 5) moved = true;
        const c = canvasRef.current; if (!c) return;
        const cr = c.getBoundingClientRect();
        onDragRef.current(
          clamp(ox + ((me.clientX - sx) / cr.width) * 100, 0, 85),
          clamp(oy + ((me.clientY - sy) / cr.height) * 100, 0, 70)
        );
      };
      const mu = () => {
        if (!moved) onSelectRef.current();
        document.removeEventListener('mousemove', mm); document.removeEventListener('mouseup', mu);
      };
      document.addEventListener('mousemove', mm); document.addEventListener('mouseup', mu);
    } else {
      calcStick(e.clientX, e.clientY);
      const mm = (me: MouseEvent) => calcStick(me.clientX, me.clientY);
      const mu = () => {
        setKnob({ x: 0, y: 0 }); onMoveRef.current(0, 0);
        document.removeEventListener('mousemove', mm); document.removeEventListener('mouseup', mu);
      };
      document.addEventListener('mousemove', mm); document.addEventListener('mouseup', mu);
    }
  };

  return (
    <div
      className={`gp-ctrl ${editMode ? 'edit' : ''} ${isSelected ? 'selected' : ''}`}
      style={{ left: `${pos.x}%`, top: `${pos.y}%` }}
    >
      <div
        ref={elRef}
        className={`gp-joy ${showHitboxes ? 'show-hitbox' : ''}`}
        style={{ width: joySize, height: joySize }}
        onTouchStart={onTouchStart}
        onMouseDown={onMouseDown}
        onContextMenu={e => e.preventDefault()}
      >
        <div className="gp-joy-knob" style={{
          width: knobSize, height: knobSize,
          transform: `translate(${knob.x}px, ${knob.y}px)`
        }} />
        <div className="gp-joy-cross-h" /><div className="gp-joy-cross-v" />
      </div>
      {editMode && <span className="gp-ctrl-label">{id}</span>}
    </div>
  );
};
