namespace VGamepadWeb.WinForm
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            console = new UI.ConsoleView();
            SuspendLayout();
            // 
            // console
            // 
            console.Dock = DockStyle.Fill;
            console.Location = new Point(0, 0);
            console.Name = "console";
            console.Size = new Size(980, 580);
            console.TabIndex = 0;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(980, 580);
            Controls.Add(console);
            FormBorderStyle = FormBorderStyle.Sizable;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(980, 580);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "VGamepadWeb";
            ResumeLayout(false);
        }

        #endregion

        private UI.ConsoleView console;
    }
}
