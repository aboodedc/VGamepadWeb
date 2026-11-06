using VGamepadWeb.Core;
using VGamepadWeb.WinForm.UI;

namespace VGamepadWeb.WinForm
{
    /// <summary>
    /// 槽位设置弹窗：类型 / 名称 / 锁定，槽位被占用时额外提供「踢出连接」。
    /// 普通 WinForms 控件布局，不参与 ConsoleView 的 GDI 自绘；文案全部走 Strings.T。
    /// 只负责收集用户选择，写操作（SetSlotType / Kick 等）由 Form1 在对话框返回后执行。
    /// </summary>
    public sealed class SlotSettingsDialog : Form
    {
        private const int FieldWidth = 260;

        private readonly ComboBox _typeBox;
        private readonly TextBox _nameBox;
        private readonly CheckBox _lockBox;

        public SlotSettingsDialog(
            string language,
            int slotNumber,
            TypeController type,
            string name,
            bool locked,
            bool occupied,
            string connectionId,
            Func<string, bool> kick)
        {
            string lang = string.IsNullOrEmpty(language) ? Languages.Chinese : language;

            Text = Strings.T(lang, "slot.dialog.title", slotNumber);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var grid = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(16)
            };

            // 类型：被占用时禁改（改类型要重建设备），旁边给一行说明。
            _typeBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = FieldWidth,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 3, 0, 3)
            };
            _typeBox.Items.Add(Strings.T(lang, "type.xbox"));
            _typeBox.Items.Add(Strings.T(lang, "type.ds4"));
            _typeBox.SelectedIndex = type == TypeController.DualShock4 ? 1 : 0;
            _typeBox.Enabled = !occupied;

            grid.Controls.Add(CreateLabel(lang, "slot.dialog.type"));
            grid.Controls.Add(_typeBox);

            if (occupied)
            {
                grid.Controls.Add(CreateHint(lang, "slot.dialog.typeLocked"));
            }

            _nameBox = new TextBox
            {
                Text = name ?? string.Empty,
                Width = FieldWidth,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 3, 0, 3)
            };

            grid.Controls.Add(CreateLabel(lang, "slot.dialog.name"));
            grid.Controls.Add(_nameBox);

            _lockBox = new CheckBox
            {
                Text = Strings.T(lang, "slot.dialog.lock"),
                Checked = locked,
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 0)
            };

            grid.Controls.Add(_lockBox);
            grid.Controls.Add(CreateHint(lang, "slot.dialog.lockHint"));

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 14, 0, 0)
            };

            var cancelButton = new Button
            {
                Text = Strings.T(lang, "slot.dialog.cancel"),
                AutoSize = true,
                DialogResult = DialogResult.Cancel
            };

            var okButton = new Button
            {
                Text = Strings.T(lang, "slot.dialog.ok"),
                AutoSize = true,
                DialogResult = DialogResult.OK
            };

            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(okButton);

            if (occupied)
            {
                var kickButton = new Button
                {
                    Text = Strings.T(lang, "slot.dialog.kick"),
                    AutoSize = true
                };

                kickButton.Click += (_, _) =>
                {
                    KickRequested = true;
                    KickSucceeded = kick != null && kick(connectionId);
                    DialogResult = DialogResult.OK;
                    Close();
                };

                buttons.Controls.Add(kickButton);
            }

            grid.Controls.Add(buttons);

            Controls.Add(grid);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        /// <summary>确定后的类型；槽位被占用时下拉是禁用的，这里返回的就是原类型。</summary>
        public TypeController SelectedType =>
            _typeBox.SelectedIndex == 1 ? TypeController.DualShock4 : TypeController.Xbox360;

        public string SlotName => _nameBox.Text.Trim();

        public bool SlotLocked => _lockBox.Checked;

        /// <summary>用户点过「踢出连接」。踢出本身在对话框关闭前已执行，结果看 KickSucceeded。</summary>
        public bool KickRequested { get; private set; }

        public bool KickSucceeded { get; private set; }

        private static Label CreateLabel(string lang, string key)
        {
            return new Label
            {
                Text = Strings.T(lang, key),
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 3)
            };
        }

        private static Label CreateHint(string lang, string key)
        {
            return new Label
            {
                Text = Strings.T(lang, key),
                AutoSize = true,
                MaximumSize = new Size(FieldWidth, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 0, 0, 6)
            };
        }
    }
}
