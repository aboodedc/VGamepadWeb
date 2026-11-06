import { useEffect } from 'react';

/**
 * 手柄界面的「锁定」：挡住那些会打断手柄操作的浏览器行为。
 *
 * 为什么需要：手柄页全屏运行在手机/平板上，用户的手指会到处点。
 * 一次误触右键就弹出一大坨系统菜单，一次误触刷新就断连丢槽位。
 *
 * 拦哪些：
 * - 右键菜单（含长按弹出的选择菜单）
 * - 开发者工具快捷键：F12 / Ctrl+Shift+I / Ctrl+Shift+J / Ctrl+Shift+C / Ctrl+U
 * - 复制 / 剪切 / 粘贴 / 全选（密码框允许粘贴，用户要粘密码）
 * - 刷新：F5 / Ctrl+R —— 连上后刷新会断连，槽位被服务端回收
 * - 保存网页 / 打印
 * - 拖拽、选中文字
 * - 长按：系统上下文菜单、文本选择气泡、原生 tooltip、拖拽幽灵影像
 *
 * 拦不住的（浏览器层面的硬限制，只能靠引导）：
 * - 浏览器菜单里的「检查」仍然能打开开发者工具，页面无法阻止
 * - 桌面浏览器地址栏的各种快捷键（Ctrl+L、Alt+← 等）页面管不到
 * - 独立 App（如 WebView 壳）不在本次范围内
 */

const isPasswordField = (target: EventTarget | null): boolean => {
  const el = target as HTMLInputElement | null;
  return !!el && el.tagName === 'INPUT' && el.type === 'password';
};

/** 长按手势的判定阈值（毫秒 / 像素），跟系统默认量级一致。 */
const LONG_PRESS_MS = 500;
const LONG_PRESS_MOVE_TOLERANCE = 10;

export const usePageLockdown = () => {
  useEffect(() => {
    const onContextMenu = (e: Event) => {
      e.preventDefault();
    };

    const onDragStart = (e: Event) => {
      e.preventDefault();
    };

    const onSelectStart = (e: Event) => {
      // 密码框留个例外：用户要能选中复制不了、清空重输。
      if (isPasswordField(e.target)) return;
      e.preventDefault();
    };

    // ── 长按 ──
    // 手指按住不动一段时间，系统会认为你想「选文字 / 看菜单 / 拖拽」。
    // 这里按下即计时，超过阈值就标记为长按；随后的抬起不再当作普通点击，
    // 免得长按松手时误触发一次按钮。
    let longPressTimer: ReturnType<typeof setTimeout> | null = null;
    let longPressFired = false;
    let suppressNextClick = false;
    let pressStartX = 0;
    let pressStartY = 0;

    const clearLongPress = () => {
      if (longPressTimer !== null) {
        clearTimeout(longPressTimer);
        longPressTimer = null;
      }
    };

    const onTouchStart = (e: Event) => {
      const touch = (e as TouchEvent).touches[0];
      if (!touch) return;

      // 输入框不参与：用户要在密码框里长按选词、粘贴。
      if (isPasswordField(e.target)) return;

      longPressFired = false;
      pressStartX = touch.clientX;
      pressStartY = touch.clientY;

      clearLongPress();
      longPressTimer = setTimeout(() => {
        longPressFired = true;
        // 已经弹出来的东西收不回去，但可以挡住随后由长按合成的那些事件。
      }, LONG_PRESS_MS);
    };

    const onTouchMove = (e: Event) => {
      if (longPressTimer === null) return;
      const touch = (e as TouchEvent).touches[0];
      if (!touch) return;
      // 手指动了就不是长按，是拖动 —— 计时器作废，但别拦事件（摇杆拖拽要靠它）。
      if (Math.abs(touch.clientX - pressStartX) > LONG_PRESS_MOVE_TOLERANCE
        || Math.abs(touch.clientY - pressStartY) > LONG_PRESS_MOVE_TOLERANCE) {
        clearLongPress();
      }
    };

    const onTouchEnd = () => {
      clearLongPress();

      if (!longPressFired) return;

      // 长按松手：这一下不能再当成点击。
      // flag 放在 document 的捕获阶段拦 click —— React 的 onClick 是委托到
      // root 上的，挂在具体元素上拦不住。
      suppressNextClick = true;
      setTimeout(() => { suppressNextClick = false; }, 400);
    };

    const onClickCapture = (e: Event) => {
      if (!suppressNextClick) return;
      suppressNextClick = false;
      e.preventDefault();
      e.stopPropagation();
    };

    const onKeyDown = (e: KeyboardEvent) => {
      const key = e.key.toLowerCase();
      const ctrl = e.ctrlKey || e.metaKey;
      const shift = e.shiftKey;
      const inPassword = isPasswordField(e.target);

      // ── 开发者工具 ──
      if (key === 'f12') { e.preventDefault(); return; }
      if (ctrl && shift && (key === 'i' || key === 'j' || key === 'c')) { e.preventDefault(); return; }
      if (ctrl && key === 'u') { e.preventDefault(); return; }

      // ── 复制 / 剪切 / 粘贴 / 全选 ──
      // 密码框允许粘贴（要粘密码），但不给复制、剪切。
      if (ctrl && (key === 'c' || key === 'x')) { e.preventDefault(); return; }
      if (ctrl && key === 'v') { if (!inPassword) e.preventDefault(); return; }
      if (ctrl && key === 'a') { if (!inPassword) e.preventDefault(); return; }

      // ── 保存 / 打印 ──
      if (ctrl && key === 's') { e.preventDefault(); return; }
      if (ctrl && key === 'p') { e.preventDefault(); return; }

      // ── 刷新：连上后刷新会断连，槽位被服务端回收 ──
      if (key === 'f5' || (ctrl && key === 'r')) { e.preventDefault(); return; }
    };

    // 用捕获阶段：在输入框自己的处理之前先拦下快捷键。
    document.addEventListener('contextmenu', onContextMenu, true);
    document.addEventListener('dragstart', onDragStart, true);
    document.addEventListener('selectstart', onSelectStart, true);
    document.addEventListener('keydown', onKeyDown, true);

    document.addEventListener('touchstart', onTouchStart, { passive: true });
    document.addEventListener('touchmove', onTouchMove, { passive: true });
    document.addEventListener('touchend', onTouchEnd, { passive: true });
    document.addEventListener('click', onClickCapture, true);

    return () => {
      document.removeEventListener('contextmenu', onContextMenu, true);
      document.removeEventListener('dragstart', onDragStart, true);
      document.removeEventListener('selectstart', onSelectStart, true);
      document.removeEventListener('keydown', onKeyDown, true);

      document.removeEventListener('touchstart', onTouchStart);
      document.removeEventListener('touchmove', onTouchMove);
      document.removeEventListener('touchend', onTouchEnd);
      document.removeEventListener('click', onClickCapture, true);
      clearLongPress();
    };
  }, []);
};
