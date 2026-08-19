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
using Microsoft.EntityFrameworkCore;
using Zuby.ADGV;
using ClosedXML.Excel;
using tesa_HSU2000_Data_Record.Controls;
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

    private AppDbContext _dbContext;

    // Data Binding
    private DataTable _dataTableResults;
    private BindingSource _bindingSource;

    // UI Controls
    private Label _lblWatcherStatus;
    private Label _lblSyncStatus;
    private AdvancedDataGridView _dgvResults;
    private TesaRoundedButton _btnToggleWatcher;
    private TesaRoundedButton _btnBrowseFolder;
    private TextBox _txtFolderPath;
    private TesaRoundedButton _btnGenerateDemo;
    
    // Bulk Action & Report Controls
    private TesaRoundedButton _btnBulkSync;
    private TesaRoundedButton _btnBulkDelete;
    private TesaRoundedButton _btnExport;
    private TesaRoundedButton _btnBackup;
    private TesaRoundedButton _btnRestore;
    private TextBox _txtSearch;
    private TesaRoundedButton _btnCopyPivot;
    private DateTimePicker _dtpFrom;
    private DateTimePicker _dtpTo;
    private TesaRoundedButton _btnFilterDate;

    // Inputs
    private TextBox _txtNart;
    private TextBox _txtBatchCode;
    private TextBox _txtLocation;
    private TextBox _txtSampleName;
    private TextBox _txtTester;

    // Chart & Interactivity
    private TesaRoundedPanel _pnlChart;
    private HsuTestResult? _latestResultForChart;
    private float _zoomFactor = 1.0f;
    private float _panX = 0f;
    private bool _isPanning = false;
    private Point _lastMousePos;
    private Point _currentMousePos = new Point(-1, -1);
    
    // New UXUI Custom Navigation
    private Panel _pnlNavIndicator;
    private TesaRoundedButton _btnTabDashboard;
    private TesaRoundedButton _btnTabDataSheet;
    private TabControl _mainTabControl;
    private Button _btnLogin;

    // New UXUI Analytics
    private Label _lblTotalSamplesToday;
    private Label _lblTotalBatchToday;
    private Label _lblMinForce;
    private Label _lblMaxForce;
    
    // New UXUI Live Value
    private Label _lblLiveWeight;

    // Auth
    private const string ADMIN_PASSWORD = "admin123";
    private bool _isAdminAuthenticated = false;

    public MainForm()
    {
        InitializeComponent();
        InitializeServices();
    }

    private void InitializeComponent()
    {
        this.Text = "tesa HSU-2000 Data Record";
        this.Size = new Size(1350, 850);
        this.BackColor = BackgroundLight;
        this.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        this.StartPosition = FormStartPosition.CenterScreen;

        // --- HEADER & NAVIGATION ---
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = TesaWhite };
        var pnlStripe = new Panel { Dock = DockStyle.Bottom, Height = 4, BackColor = TesaRed };
        pnlHeader.Controls.Add(pnlStripe);
        this.Controls.Add(pnlHeader);

        string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TESA-Logo.svg.png");
        if (System.IO.File.Exists(logoPath))
        {
            var picLogo = new PictureBox
            {
                Image = Image.FromFile(logoPath),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(80, 25),
                Location = new Point(10, 15)
            };
            pnlHeader.Controls.Add(picLogo);
        }

        var lblTitle = new Label
        {
            Text = "tesa HSU-2000 Data Record",
            Font = new Font("Segoe UI", 12F, FontStyle.Regular),
            ForeColor = Color.Black,
            AutoSize = true,
            Location = new Point(100, 18)
        };
        pnlHeader.Controls.Add(lblTitle);

        // Custom Tabs
        _btnTabDashboard = CreateTabButton("Dashboard", 430);
        _btnTabDataSheet = CreateTabButton("Data Sheet", 560);

        _pnlNavIndicator = new Panel { Height = 3, BackColor = TesaRed, Top = 57 };
        pnlHeader.Controls.Add(_pnlNavIndicator);

        _btnLogin = new Button
        {
            Text = "Đăng nhập",
            BackColor = TesaBlue,
            ForeColor = TesaWhite,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Size = new Size(100, 30),
            Location = new Point(this.Width - 140, 15),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        _btnLogin.FlatAppearance.BorderSize = 0;
        _btnLogin.Click += BtnLogin_Click;
        pnlHeader.Controls.Add(_btnLogin);

        pnlHeader.Controls.Add(_btnTabDashboard);
        pnlHeader.Controls.Add(_btnTabDataSheet);

        // --- HIDDEN TAB CONTROL ---
        _mainTabControl = new TabControl 
        { 
            Dock = DockStyle.Fill, 
            ItemSize = new Size(0, 1), 
            SizeMode = TabSizeMode.Fixed, 
            Appearance = TabAppearance.FlatButtons 
        };
        
        var tabDashboard = new TabPage("Dashboard") { BackColor = BackgroundLight };
        var tabDataSheet = new TabPage("Data Sheet") { BackColor = BackgroundLight };
        
        _mainTabControl.TabPages.Add(tabDashboard);
        _mainTabControl.TabPages.Add(tabDataSheet);
        this.Controls.Add(_mainTabControl);

        // ----------------------------------------------------
        // TAB 1: DASHBOARD
        // ----------------------------------------------------
        var pnlDashLeft = new Panel { Dock = DockStyle.Left, Width = 350, Padding = new Padding(10) };
        var pnlDashRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        tabDashboard.Controls.Add(pnlDashRight);
        tabDashboard.Controls.Add(pnlDashLeft);

        // --- Dash Left: Config & Ref ---
        var gbNetwork = new GroupBox { Text = "1. Cấu hình Hệ thống", Dock = DockStyle.Top, Height = 180, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = TesaRed };
        var pnlNetInner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), Font = new Font("Segoe UI", 10F, FontStyle.Regular), ForeColor = Color.Black };
        gbNetwork.Controls.Add(pnlNetInner);
        
        pnlNetInner.Controls.Add(new Label { Text = "Thư mục theo dõi:", Location = new Point(10, 10), AutoSize = true, ForeColor = Color.DimGray });
        _txtFolderPath = new TextBox { Location = new Point(10, 35), Width = 280, ReadOnly = true, BorderStyle = BorderStyle.FixedSingle };
        pnlNetInner.Controls.Add(_txtFolderPath);

        _btnBrowseFolder = new TesaRoundedButton { Text = "Duyệt...", Width = 80, Height = 28, Location = new Point(210, 70), BackColor = Color.LightGray, ForeColor = Color.Black, BorderRadius = 4 };
        _btnBrowseFolder.Click += BtnBrowseFolder_Click;
        pnlNetInner.Controls.Add(_btnBrowseFolder);

        _lblWatcherStatus = new Label { Text = "DISCONNECTED", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.DimGray, BackColor = Color.FromArgb(230, 230, 230), Location = new Point(10, 110), Size = new Size(280, 30), TextAlign = ContentAlignment.MiddleCenter };
        pnlNetInner.Controls.Add(_lblWatcherStatus);

        var pnlDashSpacer1 = new Panel { Dock = DockStyle.Top, Height = 10 };

        var gbRef = new GroupBox { Text = "2. Thông tin tham chiếu", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = TesaRed };
        var pnlRefInner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), Font = new Font("Segoe UI", 10F, FontStyle.Regular), ForeColor = Color.Black };
        gbRef.Controls.Add(pnlRefInner);

        pnlRefInner.Controls.Add(new Label { Text = "Mã NART", Location = new Point(10, 20), AutoSize = true, ForeColor = Color.DimGray });
        _txtNart = new TextBox { Location = new Point(10, 45), Width = 310, BorderStyle = BorderStyle.FixedSingle };
        pnlRefInner.Controls.Add(_txtNart);

        pnlRefInner.Controls.Add(new Label { Text = "Lô hàng (Batch)", Location = new Point(10, 100), AutoSize = true, ForeColor = Color.DimGray });
        _txtBatchCode = new TextBox { Location = new Point(10, 125), Width = 150, BorderStyle = BorderStyle.FixedSingle };
        _txtBatchCode.KeyDown += TxtBatchCode_KeyDown;
        pnlRefInner.Controls.Add(_txtBatchCode);

        pnlRefInner.Controls.Add(new Label { Text = "Tên mẫu (Sample Name)", Location = new Point(170, 100), AutoSize = true, ForeColor = Color.DimGray });
        _txtSampleName = new TextBox { Location = new Point(170, 125), Width = 150, BorderStyle = BorderStyle.FixedSingle };
        pnlRefInner.Controls.Add(_txtSampleName);

        pnlRefInner.Controls.Add(new Label { Text = "Vị trí (Location)", Location = new Point(10, 180), AutoSize = true, ForeColor = Color.DimGray });
        _txtLocation = new TextBox { Location = new Point(10, 205), Width = 150, BorderStyle = BorderStyle.FixedSingle };
        pnlRefInner.Controls.Add(_txtLocation);

        pnlRefInner.Controls.Add(new Label { Text = "Người test (Tester)", Location = new Point(170, 180), AutoSize = true, ForeColor = Color.DimGray });
        _txtTester = new TextBox { Location = new Point(170, 205), Width = 150, BorderStyle = BorderStyle.FixedSingle };
        pnlRefInner.Controls.Add(_txtTester);

        _lblSyncStatus = new Label { Text = "Server: Chưa kết nối", Font = new Font("Segoe UI", 9F, FontStyle.Regular), ForeColor = Color.White, BackColor = TesaBlue, Dock = DockStyle.Bottom, Height = 25, TextAlign = ContentAlignment.MiddleLeft };
        
        pnlDashLeft.Controls.Add(gbRef);
        pnlDashLeft.Controls.Add(pnlDashSpacer1);
        pnlDashLeft.Controls.Add(gbNetwork);

        // --- Dash Right: Live Value ---
        var gbLive = new GroupBox { Text = "3. Giá trị đo mới nhất", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = TesaRed };
        var pnlLiveInner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20), Font = new Font("Segoe UI", 10F, FontStyle.Regular), ForeColor = Color.Black };
        gbLive.Controls.Add(pnlLiveInner);

        _lblLiveWeight = new Label 
        { 
            Text = "0.000 N", 
            Font = new Font("Segoe UI", 48F, FontStyle.Bold), 
            ForeColor = Color.FromArgb(150, 160, 175), 
            Dock = DockStyle.Bottom,
            Height = 100,
            TextAlign = ContentAlignment.MiddleCenter
        };
        pnlLiveInner.Controls.Add(_lblLiveWeight);

        var pnlLiveBottom = new Panel { Dock = DockStyle.Bottom, Height = 100 };
        _btnToggleWatcher = new TesaRoundedButton
        {
            Text = "↓ Bắt đầu theo dõi Thư mục",
            BackColor = TesaRed,
            ForeColor = TesaWhite,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 24F, FontStyle.Bold),
            Dock = DockStyle.Bottom,
            Height = 80,
            Cursor = Cursors.Hand,
            BorderRadius = 10
        };
        _btnToggleWatcher.FlatAppearance.BorderSize = 0;
        _btnToggleWatcher.Click += BtnToggleWatcher_Click;

        _btnGenerateDemo = new TesaRoundedButton
        {
            Text = "Tạo File Demo",
            BackColor = Color.Orange,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Size = new Size(120, 30),
            Location = new Point(0, 0),
            Cursor = Cursors.Hand,
            BorderRadius = 4,
            Visible = false // Ẩn mặc định
        };
        _btnGenerateDemo.FlatAppearance.BorderSize = 0;
        _btnGenerateDemo.Click += BtnGenerateDemo_Click;
        pnlLiveBottom.Controls.Add(_btnGenerateDemo);

        pnlLiveBottom.Controls.Add(_btnToggleWatcher);
        pnlLiveInner.Controls.Add(pnlLiveBottom);

        pnlDashRight.Controls.Add(gbLive);

        // ----------------------------------------------------
        // TAB 2: ANALYTICS
        // ----------------------------------------------------
        var pnlStatTop = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = TesaWhite };
        var pnlStatLine = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.LightGray };
        pnlStatTop.Controls.Add(pnlStatLine);

        _lblTotalSamplesToday = CreateStatBox(pnlStatTop, "MẪU HÔM NAY", "0", 20, TesaRed);
        _lblTotalBatchToday = CreateStatBox(pnlStatTop, "TỔNG KHỐI LƯỢNG", "0.000", 250, TesaBlue);
        _lblMinForce = CreateStatBox(pnlStatTop, "MIN (HÔM NAY)", "---", 550, Color.Chocolate);
        _lblMaxForce = CreateStatBox(pnlStatTop, "MAX (HÔM NAY)", "---", 800, TesaRed);

        _pnlChart = new TesaRoundedPanel { Dock = DockStyle.Fill, BackColor = TesaWhite, BorderRadius = 0 };
        typeof(Panel).InvokeMember("DoubleBuffered", 
            System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, 
            null, _pnlChart, new object[] { true });

        _pnlChart.Paint += PnlChart_Paint;
        _pnlChart.MouseDown += PnlChart_MouseDown;
        _pnlChart.MouseUp += PnlChart_MouseUp;
        _pnlChart.MouseMove += PnlChart_MouseMove;
        _pnlChart.MouseLeave += PnlChart_MouseLeave;

        pnlLiveInner.Controls.Add(_pnlChart);
        pnlDashRight.Controls.Add(pnlStatTop);
        pnlStatTop.SendToBack();

        // ----------------------------------------------------
        // TAB 3: DATA SHEET
        // ----------------------------------------------------
        var pnlDataTop = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = BackgroundLight };
        
        var lblDataTitle = new Label { Text = "Dữ liệu Đã lưu (SQLite Database)", Font = new Font("Segoe UI", 14F, FontStyle.Bold), AutoSize = true, Location = new Point(20, 20) };
        pnlDataTop.Controls.Add(lblDataTitle);

        var lblSearch = new Label { Text = "Tìm kiếm nhanh:", Font = new Font("Segoe UI", 10F, FontStyle.Regular), ForeColor = Color.DimGray, AutoSize = true, Location = new Point(350, 25) };
        _txtSearch = new TextBox { Width = 200, Location = new Point(470, 23), BorderStyle = BorderStyle.FixedSingle };
        _txtSearch.TextChanged += TxtSearch_TextChanged;
        pnlDataTop.Controls.Add(lblSearch);
        pnlDataTop.Controls.Add(_txtSearch);

        _btnBackup = new TesaRoundedButton { Text = "⬇ Sao lưu DB", BackColor = Color.LightGray, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Size = new Size(150, 40), Location = new Point(700, 15), Cursor = Cursors.Hand, BorderRadius = 4, Visible = false };
        _btnBackup.FlatAppearance.BorderSize = 0;
        _btnBackup.Click += BtnBackup_Click;
        pnlDataTop.Controls.Add(_btnBackup);

        _btnRestore = new TesaRoundedButton { Text = "⬆ Phục hồi DB", BackColor = Color.LightGray, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Size = new Size(150, 40), Location = new Point(860, 15), Cursor = Cursors.Hand, BorderRadius = 4, Visible = false };
        _btnRestore.FlatAppearance.BorderSize = 0;
        _btnRestore.Click += BtnRestore_Click;
        pnlDataTop.Controls.Add(_btnRestore);

        _btnExport = new TesaRoundedButton { Text = "⬇ Xuất báo cáo (.xlsx / .csv)", BackColor = TesaBlue, ForeColor = TesaWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Size = new Size(250, 40), Location = new Point(1020, 15), Cursor = Cursors.Hand, BorderRadius = 4 };
        _btnExport.FlatAppearance.BorderSize = 0;
        _btnExport.Click += BtnExport_Click;
        pnlDataTop.Controls.Add(_btnExport);

        _btnBulkDelete = new TesaRoundedButton { Text = "🗑 Xóa mục đã chọn", BackColor = TesaWhite, ForeColor = TesaRed, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Size = new Size(180, 35), Location = new Point(20, 65), Cursor = Cursors.Hand, BorderRadius = 4 };
        _btnBulkDelete.FlatAppearance.BorderColor = TesaRed;
        _btnBulkDelete.FlatAppearance.BorderSize = 1;
        _btnBulkDelete.Click += BtnBulkDelete_Click;
        pnlDataTop.Controls.Add(_btnBulkDelete);

        _btnBulkSync = new TesaRoundedButton { Text = "Đồng bộ Server", BackColor = Color.MediumPurple, ForeColor = TesaWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Size = new Size(150, 35), Location = new Point(220, 65), Cursor = Cursors.Hand, BorderRadius = 4 };
        _btnBulkSync.FlatAppearance.BorderSize = 0;
        _btnBulkSync.Click += BtnBulkSync_Click;
        pnlDataTop.Controls.Add(_btnBulkSync);

        var lblFromDate = new Label { Text = "Từ ngày:", Font = new Font("Segoe UI", 10F, FontStyle.Regular), ForeColor = Color.DimGray, AutoSize = true, Location = new Point(390, 72) };
        _dtpFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Location = new Point(460, 70), Width = 120, Font = new Font("Segoe UI", 10F) };
        _dtpFrom.Value = DateTime.Today.AddDays(-7);
        pnlDataTop.Controls.Add(lblFromDate);
        pnlDataTop.Controls.Add(_dtpFrom);

        var lblToDate = new Label { Text = "Đến ngày:", Font = new Font("Segoe UI", 10F, FontStyle.Regular), ForeColor = Color.DimGray, AutoSize = true, Location = new Point(600, 72) };
        _dtpTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Location = new Point(670, 70), Width = 120, Font = new Font("Segoe UI", 10F) };
        _dtpTo.Value = DateTime.Now;
        pnlDataTop.Controls.Add(lblToDate);
        pnlDataTop.Controls.Add(_dtpTo);

        _btnFilterDate = new TesaRoundedButton { Text = "Lọc", BackColor = TesaRed, ForeColor = TesaWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Size = new Size(80, 28), Location = new Point(810, 68), Cursor = Cursors.Hand, BorderRadius = 4 };
        _btnFilterDate.FlatAppearance.BorderSize = 0;
        _btnFilterDate.Click += BtnFilterDate_Click;
        pnlDataTop.Controls.Add(_btnFilterDate);

        _btnCopyPivot = new TesaRoundedButton { Text = "Copy Pivot", BackColor = Color.DarkGoldenrod, ForeColor = TesaWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Size = new Size(110, 35), Location = new Point(900, 65), Cursor = Cursors.Hand, BorderRadius = 4 };
        _btnCopyPivot.FlatAppearance.BorderSize = 0;
        _btnCopyPivot.Click += BtnCopyPivot_Click;
        pnlDataTop.Controls.Add(_btnCopyPivot);

        var pnlGridContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 20) };
        _dgvResults = new AdvancedDataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = TesaWhite,
            BorderStyle = BorderStyle.FixedSingle,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            EnableHeadersVisualStyles = false,
            GridColor = Color.LightGray
        };

        _dgvResults.ColumnHeadersDefaultCellStyle.BackColor = TesaBlue;
        _dgvResults.ColumnHeadersDefaultCellStyle.ForeColor = TesaWhite;
        _dgvResults.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _dgvResults.ColumnHeadersHeight = 35;
        _dgvResults.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
        _dgvResults.DefaultCellStyle.SelectionBackColor = Color.LightBlue;
        _dgvResults.DefaultCellStyle.SelectionForeColor = Color.Black;
        
        _dgvResults.FilterStringChanged += DgvResults_FilterStringChanged;
        _dgvResults.SortStringChanged += DgvResults_SortStringChanged;
        _dgvResults.SelectionChanged += DgvResults_SelectionChanged;
        _dgvResults.CellEndEdit += DgvResults_CellEndEdit;

        pnlGridContainer.Controls.Add(_dgvResults);

        tabDataSheet.Controls.Add(pnlGridContainer);
        tabDataSheet.Controls.Add(pnlDataTop);

        this.Controls.Add(_lblSyncStatus);

        _mainTabControl.BringToFront();

        SelectTab(0);
    }

    private TesaRoundedButton CreateTabButton(string text, int x)
    {
        var btn = new TesaRoundedButton
        {
            Text = text,
            Location = new Point(x, 15),
            Size = new Size(120, 35),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.DimGray,
            BackColor = TesaWhite,
            Cursor = Cursors.Hand,
            BorderRadius = 0
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (s, e) => 
        {
            if (text == "Dashboard") SelectTab(0);
            else if (text == "Data Sheet") SelectTab(1);
        };
        return btn;
    }

    private Label CreateStatBox(Panel parent, string title, string defaultValue, int x, Color valueColor)
    {
        var lblTitle = new Label { Text = title, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.DimGray, AutoSize = true, Location = new Point(x, 10) };
        var lblValue = new Label { Text = defaultValue, Font = new Font("Segoe UI", 28F, FontStyle.Bold), ForeColor = valueColor, AutoSize = true, Location = new Point(x - 5, 30) };
        parent.Controls.Add(lblTitle);
        parent.Controls.Add(lblValue);
        return lblValue;
    }

    private void SelectTab(int index)
    {
        _mainTabControl.SelectedIndex = index;
        
        _btnTabDashboard.Font = new Font("Segoe UI", 10F, index == 0 ? FontStyle.Bold : FontStyle.Regular);
        _btnTabDashboard.ForeColor = index == 0 ? TesaRed : Color.DimGray;
        
        _btnTabDataSheet.Font = new Font("Segoe UI", 10F, index == 1 ? FontStyle.Bold : FontStyle.Regular);
        _btnTabDataSheet.ForeColor = index == 1 ? TesaRed : Color.DimGray;

        _pnlNavIndicator.Width = 120;
        if (index == 0) _pnlNavIndicator.Left = 430;
        else if (index == 1) _pnlNavIndicator.Left = 560;
    }

    private void BtnLogin_Click(object? sender, EventArgs e)
    {
        if (_isAdminAuthenticated)
        {
            // Đăng xuất
            _isAdminAuthenticated = false;
            _btnLogin.Text = "Đăng nhập";
            _btnLogin.BackColor = TesaBlue;
            _btnGenerateDemo.Visible = false;
            _btnBackup.Visible = false;
            _btnRestore.Visible = false;
            MessageBox.Show("Đã đăng xuất tài khoản Admin.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var formPrompt = new Form()
        {
            Width = 350,
            Height = 200,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Xác thực Admin",
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var lblPrompt = new Label() { Left = 30, Top = 20, Text = "Nhập mật khẩu quản trị:", AutoSize = true, Font = new Font("Segoe UI", 10F) };
        var txtPrompt = new TextBox() { Left = 30, Top = 50, Width = 270, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 12F) };
        var btnOk = new Button() { Text = "Xác nhận", Left = 120, Width = 80, Top = 90, DialogResult = DialogResult.OK, BackColor = TesaBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        
        formPrompt.Controls.Add(lblPrompt);
        formPrompt.Controls.Add(txtPrompt);
        formPrompt.Controls.Add(btnOk);
        formPrompt.AcceptButton = btnOk;

        if (formPrompt.ShowDialog(this) == DialogResult.OK)
        {
            if (txtPrompt.Text == ADMIN_PASSWORD)
            {
                _isAdminAuthenticated = true;
                _btnLogin.Text = "Đăng xuất";
                _btnLogin.BackColor = Color.DimGray;
                _btnGenerateDemo.Visible = true;
                _btnBackup.Visible = true;
                _btnRestore.Visible = true;
                MessageBox.Show("Đăng nhập Admin thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Sai mật khẩu!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public void UpdateAnalytics()
    {
        if (_dbContext == null) return;

        DateTime startOfDay = DateTime.Today;
        var todayData = _dbContext.TestResults.Where(r => r.Timestamp >= startOfDay).ToList();

        if (todayData.Count == 0)
        {
            _lblTotalSamplesToday.Text = "0";
            _lblTotalBatchToday.Text = "0.00";
            _lblMinForce.Text = "---";
            _lblMaxForce.Text = "---";
        }
        else
        {
            _lblTotalSamplesToday.Text = todayData.Count.ToString();
            _lblTotalBatchToday.Text = todayData.Sum(r => r.AvgValue).ToString("F3");
            _lblMinForce.Text = todayData.Min(r => r.AvgValue).ToString("F3") + " N";
            _lblMaxForce.Text = todayData.Max(r => r.AvgValue).ToString("F3") + " N";
        }
    }

    private void InitializeServices()
    {
        // Khởi tạo Database SQLite
        _dbContext = new AppDbContext();
        _dbContext.Database.EnsureCreated();
        _dbContext.MigrateSchema();

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
        _dataTableResults.Columns.Add("MaxLength", typeof(decimal));
        _dataTableResults.Columns.Add("Unit", typeof(string));

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
            _dgvResults.Columns["AvgValue"].DefaultCellStyle.Format = "F3";
        }
        if (_dgvResults.Columns["MaxLength"] != null) 
        {
            _dgvResults.Columns["MaxLength"].HeaderText = "Độ dài (m)";
            _dgvResults.Columns["MaxLength"].ReadOnly = true;
            _dgvResults.Columns["MaxLength"].DefaultCellStyle.Format = "F3";
        }
        if (_dgvResults.Columns["Unit"] != null) _dgvResults.Columns["Unit"].HeaderText = "Đơn vị";

        // Thêm cột Xem biểu đồ (Mắt)
        if (_dgvResults.Columns["ViewChart"] == null)
        {
            var btnViewCol = new DataGridViewButtonColumn();
            btnViewCol.Name = "ViewChart";
            btnViewCol.HeaderText = "";
            btnViewCol.Text = "👁";
            btnViewCol.UseColumnTextForButtonValue = true;
            btnViewCol.Width = 35;
            btnViewCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgvResults.Columns.Add(btnViewCol);
            _dgvResults.CellContentClick += DgvResults_CellContentClick;
        }

        // Load dữ liệu theo khoảng thời gian mặc định (7 ngày gần nhất)
        LoadDataByDateRange(_dtpFrom.Value, _dtpTo.Value);


    }

    private void BtnFilterDate_Click(object? sender, EventArgs e)
    {
        LoadDataByDateRange(_dtpFrom.Value, _dtpTo.Value);
    }

    private void BtnCopyPivot_Click(object? sender, EventArgs e)
    {
        if (_dgvResults.SelectedRows.Count == 0)
        {
            MessageBox.Show("Vui lòng chọn ít nhất một dòng để copy.", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedRows = _dgvResults.SelectedRows.Cast<DataGridViewRow>()
            .Where(r => r.Cells["Id"].Value != null)
            .OrderBy(r => Convert.ToInt32(r.Cells["Id"].Value))
            .ToList();

        var grouped = selectedRows.GroupBy(r => new {
            Nart = r.Cells["Nart"].Value?.ToString(),
            Batch = r.Cells["BatchCode"].Value?.ToString()
        });

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("D\tG\tC");

        foreach (var group in grouped)
        {
            string valD = "---", valG = "---", valC = "---";

            foreach (var row in group)
            {
                string loc = row.Cells["Location"].Value?.ToString()?.Trim().ToUpper() ?? "";
                string val = Convert.ToDecimal(row.Cells["AvgValue"].Value).ToString("F3");
                
                if (loc == "D") valD = val;
                else if (loc == "G") valG = val;
                else if (loc == "C") valC = val;
            }

            sb.AppendLine($"{valD}\t{valG}\t{valC}");
        }

        try
        {
            Clipboard.SetText(sb.ToString().TrimEnd());
            MessageBox.Show("Đã copy dữ liệu theo định dạng Pivot vào Clipboard!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể truy cập Clipboard: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadDataByDateRange(DateTime from, DateTime to)
    {
        if (_dbContext == null || _dataTableResults == null) return;
        
        // Điều chỉnh toDate thành cuối ngày
        var endOfDay = to.Date.AddDays(1).AddTicks(-1);

        _dataTableResults.Rows.Clear();
        
        var data = _dbContext.TestResults
            .Where(r => r.Timestamp >= from.Date && r.Timestamp <= endOfDay)
            .OrderByDescending(r => r.Id)
            .ToList();
            
        foreach (var r in data)
        {
            _dataTableResults.Rows.Add(r.Id, r.Nart, r.BatchCode, r.Location, r.SampleName, r.Tester, r.Timestamp, r.AvgValue, r.MaxLength, r.Unit);
        }

        if (data.Count > 0)
        {
            _latestResultForChart = data[0];
            _lblLiveWeight.Text = $"{_latestResultForChart.AvgValue:F3} {_latestResultForChart.Unit}";
            _pnlChart.Invalidate();
        }

        UpdateAnalytics();
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
            UpdateAnalytics();
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

                // --- TẠO SHEET PIVOT ---
                var wsPivot = wb.Worksheets.Add("Pivot");
                
                // Header cho Sheet Pivot
                var headers = new string[] { "Mã Nart", "Mã Lô", "D", "G", "C" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = wsPivot.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromColor(TesaBlue);
                    cell.Style.Font.FontColor = XLColor.White;
                }

                // Gom nhóm dữ liệu
                var allRows = _dgvResults.Rows.Cast<DataGridViewRow>()
                    .Where(r => r.Cells["Id"].Value != null)
                    .OrderBy(r => Convert.ToInt32(r.Cells["Id"].Value))
                    .ToList();

                var grouped = allRows.GroupBy(r => new {
                    Nart = r.Cells["Nart"].Value?.ToString(),
                    Batch = r.Cells["BatchCode"].Value?.ToString()
                });

                int rowIndex = 2;
                foreach (var group in grouped)
                {
                    string valD = "---", valG = "---", valC = "---";

                    foreach (var row in group)
                    {
                        string loc = row.Cells["Location"].Value?.ToString()?.Trim().ToUpper() ?? "";
                        string val = Convert.ToDecimal(row.Cells["AvgValue"].Value).ToString("F3");
                        
                        if (loc == "D") valD = val;
                        else if (loc == "G") valG = val;
                        else if (loc == "C") valC = val;
                    }

                    wsPivot.Cell(rowIndex, 1).Value = group.Key.Nart;
                    wsPivot.Cell(rowIndex, 2).Value = group.Key.Batch;
                    wsPivot.Cell(rowIndex, 3).Value = valD;
                    wsPivot.Cell(rowIndex, 4).Value = valG;
                    wsPivot.Cell(rowIndex, 5).Value = valC;
                    
                    rowIndex++;
                }
                
                wsPivot.Columns().AdjustToContents();

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
                // Ép SQLite ghi toàn bộ dữ liệu từ bộ đệm (WAL) xuống file gốc trước khi copy
                _dbContext.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(FULL);");

                File.Copy(dbPath, sfd.FileName, overwrite: true);
                MessageBox.Show("Sao lưu cơ sở dữ liệu thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi sao lưu DB: {ex.Message}\nĐảm bảo bạn có quyền ghi vào thư mục được chọn.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void BtnRestore_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "SQLite Database (*.db)|*.db", Title = "Chọn file Backup để phục hồi" };
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            var confirmResult = MessageBox.Show("CẢNH BÁO: Phục hồi Database sẽ GHI ĐÈ và XÓA TOÀN BỘ dữ liệu hiện tại (không thể hoàn tác)! Bạn có chắc chắn muốn tiếp tục?", "Xác nhận phục hồi", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirmResult == DialogResult.Yes)
            {
                try
                {
                    string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string dbPath = Path.Combine(appDataFolder, "tesa_HSU2000", "tesa_hsu2000.db");
                    string walPath = dbPath + "-wal";
                    string shmPath = dbPath + "-shm";

                    // Hủy kết nối DB hiện tại để nhả file lock
                    _dbContext.Dispose();
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    // Xóa file phụ trợ của SQLite
                    if (File.Exists(walPath)) File.Delete(walPath);
                    if (File.Exists(shmPath)) File.Delete(shmPath);

                    // Copy đè file backup
                    File.Copy(ofd.FileName, dbPath, overwrite: true);

                    MessageBox.Show("Phục hồi cơ sở dữ liệu thành công! Ứng dụng sẽ tự động khởi động lại để áp dụng dữ liệu mới.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    Application.Restart();
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi phục hồi DB: {ex.Message}\nHãy thử tắt ứng dụng và copy thủ công.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Restart();
                    Environment.Exit(0);
                }
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

    private void DgvResults_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _dgvResults.Columns[e.ColumnIndex].Name == "ViewChart")
        {
            if (_dgvResults.Rows[e.RowIndex].DataBoundItem is System.Data.DataRowView rowView)
            {
                int id = (int)rowView["Id"];
                var result = _dbContext.TestResults.FirstOrDefault(x => x.Id == id);
                if (result != null && result.RawForceData != null && result.RawForceData.Count > 0)
                {
                    try
                    {
                        using var frm = new FormChartViewer(result);
                        frm.ShowDialog(this);
                    }
                    catch
                    {
                        MessageBox.Show("Không thể đọc dữ liệu thô của biểu đồ này.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Bản ghi này không có dữ liệu lực thô.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
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

        g.DrawString($"Max: {maxVal:F3} N", new Font("Segoe UI", 8F, FontStyle.Bold), Brushes.DimGray, 5, 5);
        g.DrawString($"Min: {minVal:F3} N", new Font("Segoe UI", 8F, FontStyle.Bold), Brushes.DimGray, 5, height - 20);

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

                    string tooltip = $"Index: {closestIndex}\nLực: {data[closestIndex]:F3} N";
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

    private void TxtBatchCode_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (_lblWatcherStatus.Text.Contains("ĐANG DỪNG"))
            {
                BtnToggleWatcher_Click(null, EventArgs.Empty);
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
        _lblLiveWeight.Text = $"{result.AvgValue:F3} {result.Unit}";
        _zoomFactor = 1.0f; 
        _panX = 0f;
        _pnlChart.Invalidate();
        UpdateAnalytics();

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
        newRow["MaxLength"] = result.MaxLength;
        newRow["Unit"] = result.Unit;
        _dataTableResults.Rows.InsertAt(newRow, 0);

        // Auto-rotate Location (D -> G -> C -> D) for lazy users
        var currentLoc = result.Location.ToUpper();
        if (currentLoc == "D") _txtLocation.Text = "G";
        else if (currentLoc == "G") _txtLocation.Text = "C";
        else if (currentLoc == "C") 
        {
            _txtLocation.Text = "D";
            // Auto-increment SampleName
            string sample = _txtSampleName.Text.Trim();
            var match = System.Text.RegularExpressions.Regex.Match(sample, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int num))
            {
                _txtSampleName.Text = sample.Substring(0, match.Index) + (num + 1).ToString() + sample.Substring(match.Index + match.Length);
            }
            else if (string.IsNullOrEmpty(sample))
            {
                _txtSampleName.Text = "2"; // Default fallback if empty
            }
        }
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



    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _watcherManager?.Dispose();

        _dbContext?.Dispose(); 
        base.OnFormClosing(e);
    }
}
