using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tesa_HSU2000_Data_Record.Data;
using tesa_HSU2000_Data_Record.Models;

namespace tesa_HSU2000_Data_Record;

public class FormServerSettings : Form
{
    private TextBox _txtApiUrl = null!;
    private TextBox _txtApiKey = null!;
    private TextBox _txtDeviceId = null!;
    private AppSetting? _currentSetting;

    public FormServerSettings()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        this.Text = "Cài đặt Server API";
        this.Size = new Size(400, 380);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        var fontLabel = new Font("Segoe UI", 10F, FontStyle.Regular);
        var fontInput = new Font("Segoe UI", 11F, FontStyle.Regular);

        int startY = 20;
        int spacing = 60;

        // URL
        this.Controls.Add(new Label { Text = "API Base URL:", Font = fontLabel, Location = new Point(20, startY), AutoSize = true });
        _txtApiUrl = new TextBox { Font = fontInput, Location = new Point(20, startY + 25), Width = 340 };
        this.Controls.Add(_txtApiUrl);

        // API Key
        this.Controls.Add(new Label { Text = "API Key:", Font = fontLabel, Location = new Point(20, startY + spacing), AutoSize = true });
        _txtApiKey = new TextBox { Font = fontInput, Location = new Point(20, startY + spacing + 25), Width = 340, UseSystemPasswordChar = true };
        this.Controls.Add(_txtApiKey);

        // DeviceId
        this.Controls.Add(new Label { Text = "Mã thiết bị (DeviceId):", Font = fontLabel, Location = new Point(20, startY + spacing * 3), AutoSize = true });
        _txtDeviceId = new TextBox { Font = fontInput, Location = new Point(20, startY + spacing * 3 + 25), Width = 340 };
        this.Controls.Add(_txtDeviceId);

        // Buttons
        var btnSave = new Button 
        { 
            Text = "Lưu & Đóng", 
            Location = new Point(160, startY + spacing * 4 + 10), 
            Size = new Size(100, 35),
            BackColor = Color.FromArgb(0, 81, 158), // Tesa Blue
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnSave.Click += BtnSave_Click;
        this.Controls.Add(btnSave);

        var btnCancel = new Button 
        { 
            Text = "Hủy", 
            Location = new Point(270, startY + spacing * 4 + 10), 
            Size = new Size(90, 35),
            BackColor = Color.DimGray,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnCancel.Click += (s, e) => this.Close();
        this.Controls.Add(btnCancel);
    }

    private void LoadSettings()
    {
        using (var db = new AppDbContext())
        {
            _currentSetting = db.Settings.FirstOrDefault();
            if (_currentSetting != null)
            {
                _txtApiUrl.Text = _currentSetting.ApiBaseUrl;
                _txtApiKey.Text = _currentSetting.ApiPassword; // Reusing ApiPassword field for ApiKey backward compatibility
                _txtDeviceId.Text = string.IsNullOrEmpty(_currentSetting.DeviceId) ? Environment.MachineName : _currentSetting.DeviceId;
            }
            else
            {
                _txtDeviceId.Text = Environment.MachineName;
            }
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        using (var db = new AppDbContext())
        {
            var setting = db.Settings.FirstOrDefault();
            if (setting == null)
            {
                setting = new AppSetting();
                db.Settings.Add(setting);
            }

            setting.ApiBaseUrl = _txtApiUrl.Text.Trim();
            setting.ApiPassword = _txtApiKey.Text.Trim(); // Saving API Key in ApiPassword field
            setting.DeviceId = string.IsNullOrWhiteSpace(_txtDeviceId.Text) ? Environment.MachineName : _txtDeviceId.Text.Trim();

            db.SaveChanges();
        }

        MessageBox.Show("Đã lưu cấu hình Server.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}
