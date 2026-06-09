using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace tesa_HSU2000_Data_Record.Controls
{
    public class TesaRoundedPanel : Panel
    {
        private int _borderRadius = 15;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int BorderRadius
        {
            get => _borderRadius;
            set { _borderRadius = value; this.Invalidate(); }
        }

        public TesaRoundedPanel()
        {
            this.Resize += (sender, e) => this.Invalidate();
        }

        private GraphicsPath GetFigurePath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            float curveSize = radius * 2F;
            path.StartFigure();
            path.AddArc(rect.X, rect.Y, curveSize, curveSize, 180, 90);
            path.AddArc(rect.Right - curveSize, rect.Y, curveSize, curveSize, 270, 90);
            path.AddArc(rect.Right - curveSize, rect.Bottom - curveSize, curveSize, curveSize, 0, 90);
            path.AddArc(rect.X, rect.Bottom - curveSize, curveSize, curveSize, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle rectSurface = this.ClientRectangle;
            if (_borderRadius > 2)
            {
                using (GraphicsPath pathSurface = GetFigurePath(rectSurface, _borderRadius))
                using (Pen penSurface = new Pen(this.Parent != null ? this.Parent.BackColor : this.BackColor, 2))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    this.Region = new Region(pathSurface);
                    e.Graphics.DrawPath(penSurface, pathSurface); // Anti-alias edge
                }
            }
            else
            {
                e.Graphics.SmoothingMode = SmoothingMode.None;
                this.Region = new Region(rectSurface);
            }
        }
    }
}
