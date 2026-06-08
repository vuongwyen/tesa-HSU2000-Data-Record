using System;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Zuby.ADGV;
using ClosedXML.Excel;
using tesa_HSU2000_Data_Record.Data;
using tesa_HSU2000_Data_Record.Models;
using tesa_HSU2000_Data_Record.Services;

namespace tesa_HSU2000_Data_Record;

public class MainForm : Form
{
    // tesa Brand Colors
    private readonly Color TesaRed = Color.FromArgb(218, 41, 28);
    private readonly Color TesaBlue = Color.FromArgb(0, 163, 224);
    private readonly Color TesaWhite = Color.White;
    private readonly Color BackgroundLight = Color.FromArgb(245, 245, 245);

    // Services
    private HsuWatcherManager? _watcherManager;
    private NetworkSyncService? _syncService;
    private AppDbContext _dbContext;

    // Data Binding
    private DataTable _dataTableResults;
    private BindingSource _bindingSource;

    // UI Controls
    private Label _lblWatcherStatus;
    private Label _lblSyncStatus;
    private AdvancedDataGridView _dgvResults;
    private Button _btnToggleWatcher;
    private Button _btnBrowseFolder;
    private TextBox _txtFolderPath;
    private Button _btnGenerateDemo;
    
    // Bulk Action & Report Controls
    private Button _btnBulkSync;
    private Button _btnBulkDelete;
    private Button _btnExport;
    private Button _btnBackup;
    private TextBox _txtSearch;

    // Inputs
    private TextBox _txtNart;
    private TextBox _txtBatchCode;
    private TextBox _txtLocation;
    private TextBox _txtSampleName;
    private TextBox _txtTester;

    // Chart & Interactivity
    private Panel _pnlChart;
    private HsuTestResult? _latestResultForChart;
    private float _zoomFactor = 1.0f;
    private float _panX = 0f;
    private bool _isPanning = false;
    private Point _lastMousePos;
    private Point _currentMousePos = new Point(-1, -1);

    public MainForm()
    {
        InitializeComponent();
        InitializeServices();
    }

    private void InitializeComponent()
    {
        this.Text = "Máy trạm Đồng bộ Dữ liệu tesa HSU-2000";
        this.Size = new Size(1350, 850);
        this.BackColor = BackgroundLight;
        this.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        this.StartPosition = FormStartPosition.CenterScreen;

        // --- HEADER ---
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = TesaWhite };
        var pnlStripe = new Panel { Dock = DockStyle.Bottom, Height = 4, BackColor = TesaRed };
        pnlHeader.Controls.Add(pnlStripe);

        var lblTitle = new Label
        {
            Text = "Thu thập Dữ liệu HSU-2000",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = TesaBlue,
            AutoSize = true,
            Location = new Point(20, 25)
        };
        pnlHeader.Controls.Add(lblTitle);

        // --- LEFT PANEL (INPUTS) ---
        var pnlLeft = new Panel
        {
            Dock = DockStyle.Left,
            Width = 350,
            Padding = new Padding(20),
            BackColor = BackgroundLight
        };

        int startY = 20;
        int spacing = 65;

        pnlLeft.Controls.Add(new Label { Text = "Mã Nart:", Location = new Point(20, startY), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) });
        _txtNart = new TextBox { Location = new Point(20, startY + 25), Width = 300 };
        pnlLeft.Controls.Add(_txtNart);

        pnlLeft.Controls.Add(new Label { Text = "Mã Lô (Batch):", Location = new Point(20, startY + spacing), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) });
        _txtBatchCode = new TextBox { Location = new Point(20, startY + spacing + 25), Width = 300 };
        pnlLeft.Controls.Add(_txtBatchCode);

        pnlLeft.Controls.Add(new Label { Text = "Vị trí:", Location = new Point(20, startY + spacing * 2), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) });
        _txtLocation = new TextBox { Location = new Point(20, startY + spacing * 2 + 25), Width = 300 };
        pnlLeft.Controls.Add(_txtLocation);

        pnlLeft.Controls.Add(new Label { Text = "Tên Mẫu:", Location = new Point(20, startY + spacing * 3), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) });
        _txtSampleName = new TextBox { Location = new Point(20, startY + spacing * 3 + 25), Width = 300 };
        pnlLeft.Controls.Add(_txtSampleName);

        pnlLeft.Controls.Add(new Label { Text = "Người thử:", Location = new Point(20, startY + spacing * 4), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) });
        _txtTester = new TextBox { Location = new Point(20, startY + spacing * 4 + 25), Width = 300 };
        pnlLeft.Controls.Add(_txtTester);

        pnlLeft.Controls.Add(new Label { Text = "Thư mục theo dõi:", Location = new Point(20, startY + spacing * 5), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) });
        _txtFolderPath = new TextBox { Location = new Point(20, startY + spacing * 5 + 25), Width = 220, ReadOnly = true };
        _btnBrowseFolder = new Button { Text = "Duyệt...", Location = new Point(245, startY + spacing * 5 + 24), Width = 75, BackColor = Color.LightGray };
        _btnBrowseFolder.Click += BtnBrowseFolder_Click;
        pnlLeft.Controls.Add(_txtFolderPath);
        pnlLeft.Controls.Add(_btnBrowseFolder);

        _btnToggleWatcher = new Button
        {
            Text = "Bắt đầu theo dõi",
            BackColor = TesaBlue,
            ForeColor = TesaWhite,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Size = new Size(300, 40),
            Location = new Point(20, startY + spacing * 6 + 10),
            Cursor = Cursors.Hand
        };
        _btnToggleWatcher.FlatAppearance.BorderSize = 0;
        _btnToggleWatcher.Click += BtnToggleWatcher_Click;
        pnlLeft.Controls.Add(_btnToggleWatcher);

        _lblWatcherStatus = new Label { Text = "Trạng thái: ĐANG DỪNG", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.DimGray, Location = new Point(20, startY + spacing * 6 + 60), AutoSize = true };
        _lblSyncStatus = new Label { Text = "Đồng bộ: Nhàn rỗi", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.DimGray, Location = new Point(20, startY + spacing * 6 + 80), AutoSize = true };
        pnlLeft.Controls.Add(_lblWatcherStatus);
        pnlLeft.Controls.Add(_lblSyncStatus);

        _btnGenerateDemo = new Button
        {
            Text = "Tạo File Demo (Test)",
            BackColor = Color.Orange,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Size = new Size(300, 35),
            Location = new Point(20, startY + spacing * 6 + 120),
            Cursor = Cursors.Hand
        };
        _btnGenerateDemo.FlatAppearance.BorderSize = 0;
        _btnGenerateDemo.Click += BtnGenerateDemo_Click;
        pnlLeft.Controls.Add(_btnGenerateDemo);

        // --- RIGHT PANEL ---
        var pnlRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
        
        var lblChartTitle = new Label { Text = "Biên độ dao động (Lực) - Kéo để cuộn, Lăn chuột để Zoom", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = TesaBlue, Dock = DockStyle.Top, Height = 30 };
        
        _pnlChart = new Panel { Dock = DockStyle.Top, Height = 250, BackColor = TesaWhite, BorderStyle = BorderStyle.FixedSingle };
        
        typeof(Panel).InvokeMember("DoubleBuffered", 
            System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, 
            null, _pnlChart, new object[] { true });

        _pnlChart.Paint += PnlChart_Paint;
        _pnlChart.MouseDown += PnlChart_MouseDown;
        _pnlChart.MouseUp += PnlChart_MouseUp;
        _pnlChart.MouseMove += PnlChart_MouseMove;
        _pnlChart.MouseLeave += PnlChart_MouseLeave;

        var pnlSpacer = new Panel { Dock = DockStyle.Top, Height = 20 };

        var pnlGridTitle = new Panel { Dock = DockStyle.Top, Height = 40 };
        var lblGridTitle = new Label { Text = "Lịch sử kiểm tra (Top 100)", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = TesaBlue, AutoSize = true, Location = new Point(0, 5) };
        pnlGridTitle.Controls.Add(lblGridTitle);

        var lblSearch = new Label { Text = "🔍 Tìm:", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.DimGray, AutoSize = true, Location = new Point(230, 8) };
        _txtSearch = new TextBox { Width = 200, Location = new Point(290, 6) };
        _txtSearch.TextChanged += TxtSearch_TextChanged;
        pnlGridTitle.Controls.Add(lblSearch);
        pnlGridTitle.Controls.Add(_txtSearch);

        _btnBulkSync = new Button { Text = "Đồng bộ lại", BackColor = TesaBlue, ForeColor = TesaWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Size = new Size(110, 30), Location = new Point(500, 2), Cursor = Cursors.Hand };
        _btnBulkSync.FlatAppearance.BorderSize = 0;
        _btnBulkSync.Click += BtnBulkSync_Click;

        _btnBulkDelete = new Button { Text = "Xoá", BackColor = TesaRed, ForeColor = TesaWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Size = new Size(80, 30), Location = new Point(620, 2), Cursor = Cursors.Hand };
        _btnBulkDelete.FlatAppearance.BorderSize = 0;
        _btnBulkDelete.Click += BtnBulkDelete_Click;

        _btnExport = new Button { Text = "Xuất Excel", BackColor = Color.SeaGreen, ForeColor = TesaWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Size = new Size(100, 30), Location = new Point(710, 2), Cursor = Cursors.Hand };
        _btnExport.FlatAppearance.BorderSize = 0;
        _btnExport.Click += BtnExport_Click;

        _btnBackup = new Button { Text = "Sao lưu DB", BackColor = Color.DarkGoldenrod, ForeColor = TesaWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Size = new Size(100, 30), Location = new Point(820, 2), Cursor = Cursors.Hand };
        _btnBackup.FlatAppearance.BorderSize = 0;
        _btnBackup.Click += BtnBackup_Click;

        pnlGridTitle.Controls.Add(_btnBulkSync);
        pnlGridTitle.Controls.Add(_btnBulkDelete);
        pnlGridTitle.Controls.Add(_btnExport);
        pnlGridTitle.Controls.Add(_btnBackup);
        
        _dgvResults = new AdvancedDataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = TesaWhite,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = true,
            EnableHeadersVisualStyles = false,
            GridColor = Color.LightGray
        };

        _dgvResults.ColumnHeadersDefaultCellStyle.BackColor = TesaBlue;
        _dgvResults.ColumnHeadersDefaultCellStyle.ForeColor = TesaWhite;
        _dgvResults.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _dgvResults.ColumnHeadersHeight = 40;
        _dgvResults.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(240, 248, 255);
        _dgvResults.DefaultCellStyle.SelectionBackColor = Color.LightBlue;
        _dgvResults.DefaultCellStyle.SelectionForeColor = Color.Black;
        
        _dgvResults.FilterStringChanged += DgvResults_FilterStringChanged;
        _dgvResults.SortStringChanged += DgvResults_SortStringChanged;
        _dgvResults.SelectionChanged += DgvResults_SelectionChanged;
        _dgvResults.CellEndEdit += DgvResults_CellEndEdit;

        pnlRight.Controls.Add(_dgvResults);
        pnlRight.Controls.Add(pnlGridTitle);
        pnlRight.Controls.Add(pnlSpacer);
        pnlRight.Controls.Add(_pnlChart);
        pnlRight.Controls.Add(lblChartTitle);

        this.Controls.Add(pnlRight);
        this.Controls.Add(pnlLeft);
        this.Controls.Add(pnlHeader);
    }

    private void InitializeServices()
    {
        // Khởi tạo Database SQLite
        _dbContext = new AppDbContext();
        _dbContext.Database.EnsureCreated();

        // Thiết lập thư mục dữ liệu mặc định an toàn (My Documents)
        string docFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string defaultPath = Path.Combine(docFolder, "tesa_HSU2000_Data");
        
        var setting = _dbContext.Settings.FirstOrDefault();
        if (setting == null)
        {
            setting = new AppSetting { WatchFolderPath = defaultPath };
            _dbContext.Settings.Add(setting);
            _dbContext.SaveChanges();
        }
        
        if (!Directory.Exists(setting.WatchFolderPath)) Directory.CreateDirectory(setting.WatchFolderPath);
        _txtFolderPath.Text = setting.WatchFolderPath;

        // Khởi tạo DataTable cho ADGV
        _dataTableResults = new DataTable();
        _dataTableResults.Columns.Add("Id", typeof(int));
        _dataTableResults.Columns.Add("Nart", typeof(string));
        _dataTableResults.Columns.Add("BatchCode", typeof(string));
        _dataTableResults.Columns.Add("Location", typeof(string));
        _dataTableResults.Columns.Add("SampleName", typeof(string));
        _dataTableResults.Columns.Add("Tester", typeof(string));
        _dataTableResults.Columns.Add("Timestamp", typeof(DateTime));
        _dataTableResults.Columns.Add("AvgValue", typeof(decimal));
        _dataTableResults.Columns.Add("Unit", typeof(string));

        // Load 100 kết quả gần nhất từ DB
        var recentData = _dbContext.TestResults.OrderByDescending(r => r.Id).Take(100).ToList();
        foreach (var r in recentData)
        {
            _dataTableResults.Rows.Add(r.Id, r.Nart, r.BatchCode, r.Location, r.SampleName, r.Tester, r.Timestamp, r.AvgValue, r.Unit);
        }

        _bindingSource = new BindingSource { DataSource = _dataTableResults };
        _dgvResults.DataSource = _bindingSource;

        // Dịch cột hiển thị
        if (_dgvResults.Columns["Id"] != null) _dgvResults.Columns["Id"].Visible = false;
        if (_dgvResults.Columns["Nart"] != null) _dgvResults.Columns["Nart"].HeaderText = "Mã Nart";
        if (_dgvResults.Columns["BatchCode"] != null) _dgvResults.Columns["BatchCode"].HeaderText = "Mã Lô";
        if (_dgvResults.Columns["Location"] != null) _dgvResults.Columns["Location"].HeaderText = "Vị trí";
        if (_dgvResults.Columns["SampleName"] != null) _dgvResults.Columns["SampleName"].HeaderText = "Tên Mẫu";
        if (_dgvResults.Columns["Tester"] != null) _dgvResults.Columns["Tester"].HeaderText = "Người thử";
        if (_dgvResults.Columns["Timestamp"] != null) _dgvResults.Columns["Timestamp"].HeaderText = "Thời gian";
        if (_dgvResults.Columns["AvgValue"] != null) 
        {
            _dgvResults.Columns["AvgValue"].HeaderText = "Trung bình";
            _dgvResults.Columns["AvgValue"].ReadOnly = true;
        }
        if (_dgvResults.Columns["Unit"] != null) _dgvResults.Columns["Unit"].HeaderText = "Đơn vị";

        // Hiển thị biểu đồ cho dòng đầu tiên nếu có dữ liệu
        if (recentData.Count > 0)
        {
            _latestResultForChart = recentData[0];
            _pnlChart.Invalidate();
        }

        var httpClient = new HttpClient();
        _syncService = new NetworkSyncService(httpClient, "https://api.example.com", "YOUR_JWT_TOKEN");
        _syncService.SyncStatusChanged += SyncService_SyncStatusChanged;
        _syncService.StartSyncing();
    }

    private void DgvResults_FilterStringChanged(object? sender, EventArgs e)
    {
        ApplyCombinedFilter();
    }

    private void TxtSearch_TextChanged(object? sender, EventArgs e)
    {
        ApplyCombinedFilter();
    }

    private void ApplyCombinedFilter()
    {
        string adgvFilter = _dgvResults.FilterString;
        string quickSearch = _txtSearch.Text.Trim().Replace("'", "''");
        
        string combined = "";
        
        if (!string.IsNullOrEmpty(adgvFilter))
        {
            combined = $"({adgvFilter})";
        }
        
        if (!string.IsNullOrEmpty(quickSearch))
        {
            string searchFilter = $"Nart LIKE '%{quickSearch}%' OR BatchCode LIKE '%{quickSearch}%' OR Location LIKE '%{quickSearch}%' OR SampleName LIKE '%{quickSearch}%' OR Tester LIKE '%{quickSearch}%'";
            if (string.IsNullOrEmpty(combined))
                combined = searchFilter;
            else
                combined += $" AND ({searchFilter})";
        }

        _bindingSource.Filter = combined;
    }

    private void DgvResults_SortStringChanged(object? sender, EventArgs e)
    {
        _bindingSource.Sort = _dgvResults.SortString;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        Point pt = _pnlChart.PointToClient(MousePosition);
        if (_pnlChart.ClientRectangle.Contains(pt))
        {
            HandleChartMouseWheel(e.Delta, pt.X);
        }
        else
        {
            base.OnMouseWheel(e);
        }
    }

    private void HandleChartMouseWheel(int delta, int mouseX)
    {
        if (_latestResultForChart == null) return;
        
        float oldZoom = _zoomFactor;
        
        if (delta > 0)
            _zoomFactor *= 1.25f;
        else
            _zoomFactor /= 1.25f;

        if (_zoomFactor < 1.0f) 
        {
            _zoomFactor = 1.0f;
            _panX = 0f;
        }
        else if (_zoomFactor > 100.0f)
        {
            _zoomFactor = 100.0f;
        }

        if (_zoomFactor > 1.0f)
        {
            float mouseRatio = (mouseX - _panX) / (_pnlChart.Width * oldZoom);
            _panX = mouseX - (mouseRatio * _pnlChart.Width * _zoomFactor);
        }

        ConstrainPan();
        _pnlChart.Invalidate();
    }

    private void ConstrainPan()
    {
        if (_panX > 0) _panX = 0;
        float maxPan = _pnlChart.Width - (_pnlChart.Width * _zoomFactor);
        if (_panX < maxPan) _panX = maxPan;
    }

    private void PnlChart_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isPanning = true;
            _lastMousePos = e.Location;
            _pnlChart.Cursor = Cursors.Hand;
        }
    }

    private void PnlChart_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isPanning = false;
            _pnlChart.Cursor = Cursors.Default;
        }
    }

    private void PnlChart_MouseMove(object? sender, MouseEventArgs e)
    {
        _currentMousePos = e.Location;

        if (_isPanning)
        {
            _panX += (e.X - _lastMousePos.X);
            _lastMousePos = e.Location;
            ConstrainPan();
        }

        _pnlChart.Invalidate();
    }

    private void PnlChart_MouseLeave(object? sender, EventArgs e)
    {
        _currentMousePos = new Point(-1, -1);
        _pnlChart.Invalidate();
    }

    private void BtnBrowseFolder_Click(object? sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog();
        fbd.SelectedPath = _txtFolderPath.Text;
        if (fbd.ShowDialog() == DialogResult.OK)
        {
            _txtFolderPath.Text = fbd.SelectedPath;

            var setting = _dbContext.Settings.FirstOrDefault();
            if (setting != null)
            {
                setting.WatchFolderPath = fbd.SelectedPath;
                _dbContext.SaveChanges();
            }

            if (_watcherManager != null && _lblWatcherStatus.Text.Contains("ĐANG CHẠY"))
            {
                BtnToggleWatcher_Click(null, EventArgs.Empty);
                BtnToggleWatcher_Click(null, EventArgs.Empty);
            }
        }
    }

    private async void BtnGenerateDemo_Click(object? sender, EventArgs e)
    {
        var warnResult = MessageBox.Show(
            "TÍNH NĂNG NÀY CHỈ ĐƯỢC DÙNG ĐỂ KIỂM THỬ (TEST) HỆ THỐNG!\n\n" +
            "Dữ liệu sinh ra là dữ liệu giả lập có độ hỗn loạn rất cao (mô phỏng cuộn keo lỗi/nhiễu), " +
            "không phải dữ liệu thực tế từ máy HSU-2000.\n\n" +
            "Bạn có chắc chắn muốn tiếp tục tạo file giả lập không?",
            "CẢNH BÁO KIỂM THỬ",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (warnResult != DialogResult.Yes) return;

        try
        {
            _btnGenerateDemo.Enabled = false;
            _btnGenerateDemo.Text = "Đang tạo...";

            string folderPath = _txtFolderPath.Text;
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string fileName = $"Demo_PeelTest_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string fullPath = Path.Combine(folderPath, fileName);

            await Task.Run(() =>
            {
                double totalDistance = 22.672;
                double step = 0.00995;
                int totalPoints = (int)(totalDistance / step);

                var sb = new StringBuilder();
                sb.AppendLine("X;Force");
                sb.AppendLine(" M;Newtons");

                Random rand = new Random();

                for (int i = 0; i <= totalPoints; i++)
                {
                    double x = i * step;
                    if (x > totalDistance) x = totalDistance;

                    double force = 0.0;

                    if (x < 0.07)
                    {
                        force = 2.22 + rand.NextDouble() * 0.5;
                    }
                    else if (x > 22.41)
                    {
                        double ProgressToEnd = (x - 22.41) / (totalDistance - 22.41);
                        double baseForce = 21.0 * (1.0 - ProgressToEnd); 
                        force = baseForce + (rand.NextDouble() - 0.5) * 2.0; 
                        if (force < 0.093) force = 0.093;
                    }
                    else
                    {
                        double baseForce = 16.5 + Math.Sin(x * 2.0) * 3.0; 
                        double stickSlipOscillation = (rand.NextDouble() * 6.0) * Math.Sin(x * 30.0) + 
                                                      (rand.NextDouble() * 5.0) * Math.Cos(x * 75.0);
                        double chaoticNoise = (rand.NextDouble() - 0.5) * 14.0;
                        double peakAnomaly = 0.0;
                        if (rand.NextDouble() < 0.04) 
                        {
                            peakAnomaly = (rand.NextDouble() > 0.5 ? 1 : -1) * (15.0 + rand.NextDouble() * 20.0); 
                        }

                        force = baseForce + stickSlipOscillation + chaoticNoise + peakAnomaly;

                        if (force > 55.0) force = 50.0 + rand.NextDouble() * 5.0; 
                        if (force < 1.0) force = 1.0 + rand.NextDouble() * 2.0; 
                    }

                    string xStr = x.ToString("G7", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
                    string forceStr = force.ToString("F3", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

                    sb.AppendLine($"{xStr};{forceStr}");
                }

                File.WriteAllText(fullPath, sb.ToString());
            });

            if (_lblWatcherStatus.Text.Contains("ĐANG DỪNG"))
            {
                MessageBox.Show($"Đã tạo file tại thư mục cấu hình.\nHãy bấm 'Bắt đầu theo dõi' để ứng dụng tự động đọc file vừa tạo!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi tạo file demo: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnGenerateDemo.Enabled = true;
            _btnGenerateDemo.Text = "Tạo File Demo (Test)";
        }
    }

    private void BtnBulkSync_Click(object? sender, EventArgs e)
    {
        if (_dgvResults.SelectedRows.Count == 0) return;

        int count = 0;
        foreach (DataGridViewRow row in _dgvResults.SelectedRows)
        {
            if (row.DataBoundItem is DataRowView rowView)
            {
                int id = (int)rowView["Id"];
                var result = _dbContext.TestResults.FirstOrDefault(x => x.Id == id);
                if (result != null)
                {
                    _syncService?.EnqueueForSync(result);
                    count++;
                }
            }
        }
        MessageBox.Show($"Đã đưa {count} bản ghi vào hàng đợi đồng bộ lại.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnBulkDelete_Click(object? sender, EventArgs e)
    {
        if (_dgvResults.SelectedRows.Count == 0) return;

        var confirmResult = MessageBox.Show($"Bạn có chắc muốn xoá {_dgvResults.SelectedRows.Count} bản ghi đã chọn khỏi cơ sở dữ liệu?", "Xác nhận xoá", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirmResult == DialogResult.Yes)
        {
            var rowsToDelete = _dgvResults.SelectedRows.Cast<DataGridViewRow>().ToList();
            foreach (var row in rowsToDelete)
            {
                if (row.DataBoundItem is DataRowView rowView)
                {
                    int id = (int)rowView["Id"];
                    var result = _dbContext.TestResults.FirstOrDefault(x => x.Id == id);
                    if (result != null)
                    {
                        _dbContext.TestResults.Remove(result);
                        if (result.Id == _latestResultForChart?.Id)
                        {
                            _latestResultForChart = null;
                            _pnlChart.Invalidate();
                        }
                    }
                    rowView.Row.Delete(); // Xóa khỏi DataTable
                }
            }
            _dbContext.SaveChanges();
        }
    }

    private void BtnExport_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", FileName = $"BaoCao_HSU2000_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx" };
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Data");
                
                // Ghi Header
                int colIndex = 1;
                for (int i = 0; i < _dgvResults.Columns.Count; i++)
                {
                    if (_dgvResults.Columns[i].Visible)
                    {
                        var cell = ws.Cell(1, colIndex);
                        cell.Value = _dgvResults.Columns[i].HeaderText;
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromColor(TesaBlue);
                        cell.Style.Font.FontColor = XLColor.White;
                        colIndex++;
                    }
                }

                // Ghi Dữ liệu (chỉ lấy các hàng đang hiển thị / đã lọc)
                for (int i = 0; i < _dgvResults.Rows.Count; i++)
                {
                    colIndex = 1;
                    for (int j = 0; j < _dgvResults.Columns.Count; j++)
                    {
                        if (_dgvResults.Columns[j].Visible)
                        {
                            var val = _dgvResults.Rows[i].Cells[j].Value;
                            ws.Cell(i + 2, colIndex).Value = val?.ToString() ?? "";
                            colIndex++;
                        }
                    }
                }
                
                ws.Columns().AdjustToContents(); // Tự căn lề
                wb.SaveAs(sfd.FileName);
                MessageBox.Show("Xuất báo cáo thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void BtnBackup_Click(object? sender, EventArgs e)
    {
        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dbPath = Path.Combine(appDataFolder, "tesa_HSU2000", "tesa_hsu2000.db");
        
        using var sfd = new SaveFileDialog { Filter = "SQLite Database (*.db)|*.db", FileName = $"Backup_tesa_hsu2000_{DateTime.Now:yyyyMMdd_HHmmss}.db" };
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                File.Copy(dbPath, sfd.FileName, overwrite: true);
                MessageBox.Show("Sao lưu cơ sở dữ liệu thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi sao lưu DB: {ex.Message}\nĐảm bảo bạn có quyền ghi vào thư mục được chọn.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void DgvResults_SelectionChanged(object? sender, EventArgs e)
    {
        if (_dgvResults.SelectedRows.Count > 0)
        {
            if (_dgvResults.SelectedRows[0].DataBoundItem is DataRowView rowView)
            {
                int id = (int)rowView["Id"];
                var result = _dbContext.TestResults.FirstOrDefault(x => x.Id == id);
                if (result != null && result != _latestResultForChart)
                {
                    _latestResultForChart = result;
                    _zoomFactor = 1.0f;
                    _panX = 0f;
                    _pnlChart.Invalidate();
                }
            }
        }
    }

    private void DgvResults_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        try
        {
            if (_dgvResults.Rows[e.RowIndex].DataBoundItem is DataRowView rowView)
            {
                int id = (int)rowView["Id"];
                var result = _dbContext.TestResults.FirstOrDefault(x => x.Id == id);
                if (result != null)
                {
                    result.Nart = rowView["Nart"]?.ToString() ?? "";
                    result.BatchCode = rowView["BatchCode"]?.ToString() ?? "";
                    result.Location = rowView["Location"]?.ToString() ?? "";
                    result.SampleName = rowView["SampleName"]?.ToString() ?? "";
                    result.Tester = rowView["Tester"]?.ToString() ?? "";
                    _dbContext.SaveChanges();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể lưu thay đổi vào CSDL: {ex.Message}", "Lỗi DB", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PnlChart_Paint(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int width = _pnlChart.Width;
        int height = _pnlChart.Height;

        g.Clear(TesaWhite);

        if (_latestResultForChart == null || _latestResultForChart.RawForceData == null || _latestResultForChart.RawForceData.Count < 2)
        {
            string msg = "Đang chờ dữ liệu...";
            SizeF size = g.MeasureString(msg, this.Font);
            g.DrawString(msg, this.Font, Brushes.Gray, (width - size.Width) / 2, (height - size.Height) / 2);
            return;
        }

        var data = _latestResultForChart.RawForceData;
        float maxVal = (float)data.Max();
        float minVal = (float)data.Min();
        float range = maxVal - minVal;
        if (range == 0) range = 1;

        using (var pen = new Pen(Color.LightGray, 1) { DashStyle = DashStyle.Dash })
        {
            for (int i = 1; i < 5; i++)
            {
                float y = height * (i / 5f);
                g.DrawLine(pen, 0, y, width, y);
            }
        }

        float effectiveWidth = width * _zoomFactor;
        PointF[] points = new PointF[data.Count];
        for (int i = 0; i < data.Count; i++)
        {
            float x = _panX + effectiveWidth * ((float)i / (data.Count - 1));
            float y = height - (height * (((float)data[i] - minVal) / range));
            y = 10 + (y * 0.9f); // Thêm padding
            points[i] = new PointF(x, y);
        }

        using (var pen = new Pen(TesaRed, 1.5f))
        {
            g.DrawLines(pen, points);
        }

        g.DrawString($"Max: {maxVal:F2} N", new Font("Segoe UI", 8F, FontStyle.Bold), Brushes.DimGray, 5, 5);
        g.DrawString($"Min: {minVal:F2} N", new Font("Segoe UI", 8F, FontStyle.Bold), Brushes.DimGray, 5, height - 20);

        if (_currentMousePos.X >= 0 && _currentMousePos.X <= width && _currentMousePos.Y >= 0 && _currentMousePos.Y <= height)
        {
            float ratio = (_currentMousePos.X - _panX) / effectiveWidth;
            int closestIndex = (int)Math.Round(ratio * (data.Count - 1));

            if (closestIndex >= 0 && closestIndex < data.Count)
            {
                float px = points[closestIndex].X;
                float py = points[closestIndex].Y;

                if (px >= 0 && px <= width)
                {
                    using (var crossPen = new Pen(Color.DarkBlue, 1f) { DashStyle = DashStyle.Dot })
                    {
                        g.DrawLine(crossPen, px, 0, px, height);
                        g.DrawLine(crossPen, 0, py, width, py);
                    }

                    g.FillEllipse(Brushes.DarkBlue, px - 4, py - 4, 8, 8);

                    string tooltip = $"Index: {closestIndex}\nLực: {data[closestIndex]:F2} N";
                    var font = new Font("Segoe UI", 9F, FontStyle.Bold);
                    SizeF textSize = g.MeasureString(tooltip, font);
                    
                    float textX = px + 10;
                    float textY = py - 10 - textSize.Height;

                    if (textX + textSize.Width > width) textX = px - 10 - textSize.Width;
                    if (textY < 0) textY = py + 10;

                    RectangleF bgRect = new RectangleF(textX - 2, textY - 2, textSize.Width + 4, textSize.Height + 4);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(220, 255, 255, 255)), bgRect);
                    g.DrawRectangle(Pens.Gray, bgRect.X, bgRect.Y, bgRect.Width, bgRect.Height);

                    g.DrawString(tooltip, font, Brushes.Black, textX, textY);
                }
            }
        }
    }

    private void BtnToggleWatcher_Click(object? sender, EventArgs e)
    {
        if (_lblWatcherStatus.Text.Contains("ĐANG DỪNG"))
        {
            string path = _txtFolderPath.Text;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            if (_watcherManager != null) _watcherManager.Dispose();
            
            var parserService = new HsuParserService();
            _watcherManager = new HsuWatcherManager(path, parserService);
            _watcherManager.FileProcessed += WatcherManager_FileProcessed;
            _watcherManager.ErrorOccurred += WatcherManager_ErrorOccurred;
            
            _watcherManager.Start();
            
            _lblWatcherStatus.Text = "Thư mục: ĐANG CHẠY";
            _lblWatcherStatus.ForeColor = Color.ForestGreen;
            
            _btnToggleWatcher.Text = "Dừng theo dõi";
            _btnToggleWatcher.BackColor = TesaRed;
        }
        else
        {
            _watcherManager?.Stop();
            _lblWatcherStatus.Text = "Thư mục: ĐANG DỪNG";
            _lblWatcherStatus.ForeColor = Color.DimGray;
            
            _btnToggleWatcher.Text = "Bắt đầu theo dõi";
            _btnToggleWatcher.BackColor = TesaBlue;
        }
    }

    private void WatcherManager_FileProcessed(object? sender, HsuTestResult result)
    {
        if (this.InvokeRequired)
        {
            this.BeginInvoke(new Action(() => WatcherManager_FileProcessed(sender, result)));
            return;
        }

        result.Nart = _txtNart.Text.Trim();
        result.BatchCode = _txtBatchCode.Text.Trim();
        result.Location = _txtLocation.Text.Trim();
        result.SampleName = _txtSampleName.Text.Trim();
        result.Tester = _txtTester.Text.Trim();
        result.Timestamp = DateTime.Now;
        
        try
        {
            // --- THÊM VÀO DATABASE ---
            _dbContext.TestResults.Add(result);
            _dbContext.SaveChanges(); // Lấy được ID tự tăng từ SQLite
        }
        catch (Exception ex)
        {
            _dbContext.Entry(result).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            MessageBox.Show($"Lỗi lưu Database: {ex.Message}", "Lỗi DB", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        _latestResultForChart = result;
        _zoomFactor = 1.0f; 
        _panX = 0f;
        _pnlChart.Invalidate();

        // Thêm dòng mới vào đầu bảng DataView
        var newRow = _dataTableResults.NewRow();
        newRow["Id"] = result.Id;
        newRow["Nart"] = result.Nart;
        newRow["BatchCode"] = result.BatchCode;
        newRow["Location"] = result.Location;
        newRow["SampleName"] = result.SampleName;
        newRow["Tester"] = result.Tester;
        newRow["Timestamp"] = result.Timestamp;
        newRow["AvgValue"] = result.AvgValue;
        newRow["Unit"] = result.Unit;
        _dataTableResults.Rows.InsertAt(newRow, 0);

        _syncService?.EnqueueForSync(result);
    }

    private void WatcherManager_ErrorOccurred(object? sender, string message)
    {
        if (this.InvokeRequired)
        {
            this.BeginInvoke(new Action(() => WatcherManager_ErrorOccurred(sender, message)));
            return;
        }

        MessageBox.Show(message, "Lỗi Thư mục", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void SyncService_SyncStatusChanged(object? sender, string status)
    {
        if (this.InvokeRequired)
        {
            this.BeginInvoke(new Action(() => SyncService_SyncStatusChanged(sender, status)));
            return;
        }

        _lblSyncStatus.Text = $"Đồng bộ: {status}";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _watcherManager?.Dispose();
        _syncService?.Dispose();
        _dbContext?.Dispose(); 
        base.OnFormClosing(e);
    }
}
