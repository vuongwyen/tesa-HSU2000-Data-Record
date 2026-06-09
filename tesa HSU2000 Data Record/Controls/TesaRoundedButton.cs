using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace tesa_HSU2000_Data_Record.Controls
{
    public class TesaRoundedButton : Button
    {
        private int _borderRadius = 10;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int BorderRadius
        {
            get => _borderRadius;
            set { _borderRadius = value; this.Invalidate(); }
        }

        public TesaRoundedButton()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
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

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            Rectangle rectSurface = this.ClientRectangle;
            Rectangle rectBorder = Rectangle.Inflate(rectSurface, -1, -1);
            int smoothSize = 2;
            if (_borderRadius > 2)
            {
                using (GraphicsPath pathSurface = GetFigurePath(rectSurface, _borderRadius))
                using (GraphicsPath pathBorder = GetFigurePath(rectBorder, _borderRadius - 1))
                using (Pen penSurface = new Pen(this.Parent != null ? this.Parent.BackColor : this.BackColor, smoothSize))
                using (Pen penBorder = new Pen(this.FlatAppearance.BorderColor, this.FlatAppearance.BorderSize))
                {
                    pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    this.Region = new Region(pathSurface);
                    pevent.Graphics.DrawPath(penSurface, pathSurface); // Draw smoothing edge
                    if (this.FlatAppearance.BorderSize >= 1)
                        pevent.Graphics.DrawPath(penBorder, pathBorder);
                }
            }
            else
            {
                pevent.Graphics.SmoothingMode = SmoothingMode.None;
                this.Region = new Region(rectSurface);
                if (this.FlatAppearance.BorderSize >= 1)
                {
                    using (Pen penBorder = new Pen(this.FlatAppearance.BorderColor, this.FlatAppearance.BorderSize))
                    {
                        pevent.Graphics.DrawRectangle(penBorder, 0, 0, this.Width - 1, this.Height - 1);
                    }
                }
            }
        }
    }
}
