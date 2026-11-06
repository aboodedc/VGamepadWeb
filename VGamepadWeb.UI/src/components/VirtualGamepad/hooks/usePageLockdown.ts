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
 *
 * 长按由 CSS 管：`-webkit-touch-callout: none` + `user-select: none` 已经压住了
 * 系统菜单和选择气泡。之前这里还挂了一层 JS 长按计时器，但它挂在 document 上，
 * 而手柄控件自己 stopPropagation 了触摸事件 —— 该保护的没保护上，反倒会吞掉
 * 顶栏按钮长按后的那一次点击，所以删了。
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

    return () => {
      document.removeEventListener('contextmenu', onContextMenu, true);
      document.removeEventListener('dragstart', onDragStart, true);
      document.removeEventListener('selectstart', onSelectStart, true);
      document.removeEventListener('keydown', onKeyDown, true);
    };
  }, []);
};
