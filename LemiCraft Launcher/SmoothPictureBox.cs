using System.Drawing.Drawing2D;

namespace LemiCraft_Launcher
{
    internal class SmoothPictureBox : PictureBox
    {
        protected override void OnPaint(PaintEventArgs pe)
        {
            if (Image == null) { base.OnPaint(pe); return; }
            pe.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            pe.Graphics.PixelOffsetMode   = PixelOffsetMode.HighQuality;
            pe.Graphics.DrawImage(Image, new Rectangle(0, 0, Width, Height));
        }
    }
}
