using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace tesa_HSU2000_Data_Record;

public class FormChartViewer : Form
{
    private Models.HsuTestResult _result;
    private List<decimal> _data;
    
    public FormChartViewer(Models.HsuTestResult result)
    {
        _result = result;
        _data = result.RawForceData ?? new List<decimal>();
        this.Text = $"Biểu đồ lực - {_result.SampleName}";
        this.Size = new Size(800, 500);
        this.StartPosition = FormStartPosition.CenterParent;
        
        var lblInfo = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.DimGray,
            BackColor = Color.WhiteSmoke
        };

        if (_data.Any())
        {
            lblInfo.Text = $"Nart: {_result.Nart} | Lô: {_result.BatchCode} | Mẫu: {_result.SampleName} | Vị trí: {_result.Location} | Min: {_data.Min():F2} N | Max: {_data.Max():F2} N | TB: {_result.AvgValue:F2} N";
        }
        this.Controls.Add(lblInfo);

        var panel = new DoubleBufferedPanel { Dock = DockStyle.Fill, BackColor = Color.White };
        panel.Paint += Panel_Paint;
        panel.Resize += (s, e) => panel.Invalidate();
        this.Controls.Add(panel);
        
        panel.BringToFront();
    }

    private void Panel_Paint(object? sender, PaintEventArgs e)
    {
        if (_data.Count < 2)
        {
            e.Graphics.DrawString("Không đủ dữ liệu để vẽ biểu đồ.", new Font("Segoe UI", 12), Brushes.Gray, 50, 50);
            return;
        }
        
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        var rect = ((Control)sender!).ClientRectangle;
        int paddingX = 60;
        int paddingY = 40;
        
        float maxVal = (float)(_data.Max() == 0 ? 1 : _data.Max());
        // Lấy 110% max để đồ thị không đụng nóc
        maxVal = maxVal * 1.1f;
        
        float xScale = (float)(rect.Width - paddingX * 2) / (_data.Count - 1);
        float yScale = (float)(rect.Height - paddingY * 2) / maxVal;
        
        var points = new PointF[_data.Count];
        for (int i = 0; i < _data.Count; i++)
        {
            float x = paddingX + i * xScale;
            float y = rect.Height - paddingY - ((float)_data[i] * yScale);
            points[i] = new PointF(x, y);
        }
        
        // Trục hoành và tung
        using var penAxis = new Pen(Color.Black, 2);
        g.DrawLine(penAxis, paddingX, rect.Height - paddingY, rect.Width - paddingX, rect.Height - paddingY); // X
        g.DrawLine(penAxis, paddingX, paddingY, paddingX, rect.Height - paddingY); // Y
        
        // Grid ngang
        using var penGrid = new Pen(Color.LightGray, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        int gridLines = 5;
        for (int i = 0; i <= gridLines; i++)
        {
            float y = rect.Height - paddingY - (i * (rect.Height - paddingY * 2) / gridLines);
            g.DrawLine(penGrid, paddingX, y, rect.Width - paddingX, y);
            
            float val = (maxVal / gridLines) * i;
            g.DrawString(val.ToString("0.0"), new Font("Segoe UI", 9), Brushes.DimGray, 10, y - 8);
        }
        
        // Đường biểu diễn
        using var penCurve = new Pen(Color.FromArgb(0, 163, 224), 2f); // Tesa Blue
        g.DrawLines(penCurve, points);
    }
}

// Giảm giật lag khi resize
public class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        this.UpdateStyles();
    }
}
