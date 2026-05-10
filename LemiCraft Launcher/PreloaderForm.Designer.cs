namespace LemiCraft_Launcher
{
    partial class PreloaderForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PreloaderForm));
            LogoPicture = new SmoothPictureBox();
            ProgressBar = new ProgressBar();
            ((System.ComponentModel.ISupportInitialize)LogoPicture).BeginInit();
            SuspendLayout();
            //
            // LogoPicture
            //
            LogoPicture.BackColor = Color.Transparent;
            LogoPicture.Image = Properties.Resources.logo_rounded;
            LogoPicture.Location = new Point(12, 16);
            LogoPicture.Name = "LogoPicture";
            LogoPicture.Size = new Size(44, 44);
            LogoPicture.SizeMode = PictureBoxSizeMode.Zoom;
            LogoPicture.TabIndex = 0;
            LogoPicture.TabStop = false;
            //
            // ProgressBar
            //
            ProgressBar.Location = new Point(68, 23);
            ProgressBar.Name = "ProgressBar";
            ProgressBar.Size = new Size(336, 30);
            ProgressBar.Style = ProgressBarStyle.Continuous;
            ProgressBar.TabIndex = 1;
            //
            // PreloaderForm
            //
            BackColor = Color.FromArgb(30, 41, 59);
            ClientSize = new Size(416, 76);
            Controls.Add(LogoPicture);
            Controls.Add(ProgressBar);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            Name = "PreloaderForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "LemiCraft Launcher";
            ((System.ComponentModel.ISupportInitialize)LogoPicture).EndInit();
            ResumeLayout(false);
        }

        private SmoothPictureBox LogoPicture;
        private System.Windows.Forms.ProgressBar ProgressBar;
    }
}
