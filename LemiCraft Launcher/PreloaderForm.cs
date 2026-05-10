using System.Runtime.InteropServices;

namespace LemiCraft_Launcher
{
    public partial class PreloaderForm : Form
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private const uint PBM_SETBARCOLOR = 0x409;
        private const uint PBM_SETBKCOLOR  = 0x2001;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_CAPTION_COLOR = 35;

        public PreloaderForm()
        {
            InitializeComponent();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            int dark = 1;
            if (DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int)) != 0)
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref dark, sizeof(int));

            int captionColor = 0x003B291E;
            DwmSetWindowAttribute(Handle, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

            SendMessage(ProgressBar.Handle, PBM_SETBARCOLOR, IntPtr.Zero, (IntPtr)0x005EC522);
            SendMessage(ProgressBar.Handle, PBM_SETBKCOLOR,  IntPtr.Zero, (IntPtr)0x002A170F);
        }

        public void SetProgress(int percent, string status)
        {
            if (InvokeRequired) { Invoke(() => SetProgress(percent, status)); return; }
            ProgressBar.Value = Math.Clamp(percent, 0, 100);
            Text = $"LemiCraft :: {percent}% :: {status}";
        }
    }
}
