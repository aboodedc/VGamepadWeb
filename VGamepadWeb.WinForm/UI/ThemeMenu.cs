using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VGamepadWeb.WinForm.UI
{
    /// <summary>
    /// ContextMenuStrip 的自绘渲染器：系统默认按浅色刷，暗色主题下会闪出一块白板。
    /// 只接管背景、边框、分隔线、悬停和勾选；颜色全读 <see cref="Theme"/>，
    /// 所以主题一换，下次弹出自然跟上，不需要重建菜单。
    /// </summary>
    public sealed class ThemeMenuRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush(Theme.Panel))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        /// <summary>
        /// 左侧图标/勾选边距。默认按浅色渐变画，暗色主题下就是菜单左边缘那条刺眼的白带。
        /// </summary>
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush(Theme.Panel))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Rectangle bounds = e.AffectedBounds;
            bounds.Width -= 1;
            bounds.Height -= 1;

            using (Pen pen = new Pen(Theme.Line))
            {
                e.Graphics.DrawRectangle(pen, bounds);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected && !e.Item.Pressed)
            {
                return;
            }

            using (SolidBrush brush = new SolidBrush(Theme.AccentBg))
            {
                e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.DisabledText;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            // 默认分隔线用 Professional 的浅灰，深底上没有对比。
            Rectangle bounds = new Rectangle(Point.Empty, e.Item.Size);
            int y = bounds.Height / 2;

            using (Pen pen = new Pen(Theme.Line))
            {
                e.Graphics.DrawLine(pen, bounds.Left + 8, y, bounds.Right - 8, y);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // 默认勾是黑色位图，深色底上等于没画：这里用强调色手画一个。
            Rectangle bounds = e.ImageRectangle;

            // 没有图片边距时系统可能给出零宽区域，退回按项高自己算一块。
            if (bounds.Width < 4 || bounds.Height < 4)
            {
                int size = Math.Max(8, e.Item.Height - 4);
                bool rtl = e.Item.RightToLeft == RightToLeft.Yes;
                bounds = new Rectangle(rtl ? e.Item.Width - size - 6 : 6, 2, size, size);
            }

            float cx = bounds.Left + bounds.Width / 2f;
            float cy = bounds.Top + bounds.Height / 2f;
            float w = Math.Max(3f, bounds.Width * 0.22f);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(Theme.Accent, Math.Max(1.6f, w * 0.7f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                e.Graphics.DrawLines(
                    pen,
                    new[]
                    {
                        new PointF(cx - w, cy),
                        new PointF(cx - w * 0.2f, cy + w * 0.8f),
                        new PointF(cx + w, cy - w * 0.8f)
                    });
            }
        }
    }
}
