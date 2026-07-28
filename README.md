# Tesa HSU2000 Data Record

Ứng dụng WinForms tự động hóa thu thập, xử lý, lưu trữ và đồng bộ dữ liệu lực dính từ máy test tesa HSU 2000.

---

## 1. Tổng quan dự án (Overview)
- **Mục tiêu**: Bỏ thao tác nhập tay dữ liệu cân đo của nhân viên QA/QC. Tự động đọc file `.txt` từ máy HSU 2000, tính trung bình lực dính, lưu SQLite và đồng bộ lên Server.
- **Phục vụ**: Bộ phận Quality Control (QC) nhà máy tesa.
- **Trạng thái**: Production / Active Dev (nhánh `dev` cho tính năng Sync API & Pivot Excel).
- **Liên kết**: Repository nội bộ tesa, tài liệu API ScaleData.

---

## 2. Kiến trúc hệ thống (Architecture)

```
[Máy HSU 2000] -> (File .txt) -> [HsuWatcherManager] -> [HsuParserService]
                                                             |
   [Server API] <-- (POST /api/scale/sync) -- [NetworkSyncWorker] <-- [SQLite AppDbContext]
```

- **Tech Stack**:
  - **Framework**: .NET 10.0 (Windows Forms, C# 13)
  - **Database ORM**: Entity Framework Core SQLite (`Microsoft.EntityFrameworkCore.Sqlite` v10.0)
  - **Excel Export**: `ClosedXML` v0.104.2
  - **UI Grid**: `DG.AdvancedDataGridView` v1.2.29301
- **Luồng dữ liệu**:
  1. HSU 2000 xuất file `.txt` vào thư mục theo dõi (`WatchFolderPath`).
  2. `HsuWatcherManager` phát hiện file -> `HsuParserService` parse số liệu lực dính -> Tính `AvgValue`.
  3. Ghi bản ghi vào SQLite (`TestResults`) với cờ `IsSynced = false`. Chuyển file gốc vào thư mục `Processed`.
  4. `NetworkSyncWorker` (chạy ngầm 5s/lần) gom batch 50 bản ghi -> Gọi API Login lấy JWT -> Bắn `POST /api/scale/sync` -> Cập nhật `IsSynced = true`.

---

## 3. Cấu trúc thư mục (Project structure)

```
tesa HSU2000 Data Record/
├── Controls/         # Custom UI (TesaRoundedButton, TesaRoundedPanel)
├── Data/             # EF Core DB Context (AppDbContext.cs)
├── Models/           # Thực thể DB (HsuTestResult.cs, AppSetting.cs)
├── Services/         # Logic chính (Watcher, Parser, NetworkSyncWorker)
├── MainForm.cs       # Màn hình chính (Dashboard, Data Sheet, Pivot Export)
└── FormServerSettings.cs # Hộp thoại cấu hình Server API
```
- **Quy ước**: `Hsu*` cho dịch vụ/model máy HSU 2000. Biến private UI prefix `_` (`_dgvResults`).

---

## 4. Hướng dẫn cài đặt môi trường (Setup)
- **Yêu cầu hệ thống**: Windows 10/11 x64, .NET 10.0 SDK hoặc Runtime.
- **Cài đặt step-by-step**:
  ```powershell
  git clone https://github.com/vuongwyen/tesa-HSU2000-Data-Record.git
  cd "tesa-HSU2000-Data-Record/tesa HSU2000 Data Record"
  dotnet restore
  dotnet build
  ```
- **Biến môi trường / Config**: Cấu hình lưu trực tiếp trong bảng `Settings` SQLite (URL Server, Username, Password, DeviceId). Không dùng `.env`.
- **Database Path**: Tự sinh file SQLite tại `%LOCALAPPDATA%\tesa_HSU2000\tesa_hsu2000.db`.

---

## 5. Cách chạy và build
- **Chạy Dev**:
  ```powershell
  cd "tesa HSU2000 Data Record"
  dotnet run
  ```
- **Build Production (EXE độc lập cho nhà máy)**:
  ```powershell
  dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
  ```

---

## 6. Database & Data model
- **Bảng `TestResults`**:
  - `Id` (PK, Int), `RecordId` (UUID string - Idempotency Key cho API).
  - `Nart`, `BatchCode`, `Location` (D/G/C), `SampleName`, `Tester`.
  - `AvgValue` (Decimal), `Unit` (Newton), `Timestamp`, `TestedAt`, `IsSynced` (Bool).
- **Bảng `Settings`**:
  - `WatchFolderPath`, `ApiBaseUrl`, `ApiUsername`, `ApiPassword`, `DeviceId`.
- **Migration Schema**: Sử dụng kỹ thuật `EnsureCreated()` kèm `ExecuteSqlRaw` ALTER TABLE chèn cột mặc định lúc khởi động app (không làm vỡ DB cũ).

---

## 7. Các module/tính năng quan trọng
- **`HsuWatcherManager`**: Giám sát folder, debounce 100ms chống đọc trùng/lock file, xoay vòng tự động vị trí mẫu (`D -> G -> C -> D`).
- **`HsuParserService`**: Xử lý định dạng dấu phẩy/chấm thập phân đa ngôn ngữ, lọc dữ liệu lực dính thô từ máy HSU 2000.
- **`NetworkSyncWorker`**: 
  - Tự động lấy/làm mới JWT Bearer Token trước 5 phút hết hạn.
  - Gom mảng 50 records/request, kèm header `Idempotency-Key: GUID`.
  - Xử lý backoff khi gặp lỗi `429 Too Many Requests` hoặc `403 Forbidden`.
- **Pivot Excel Export**: Xuất dữ liệu ra Excel, tự tạo sheet Pivot dàn ngang các mẫu theo vị trí Đầu (D) - Giữa (G) - Cuối (C) cho từng Batch Code.
- **Barcode Auto-start**: Bấm Enter ở ô quét mã vạch -> Tự động kích hoạt bắt đầu theo dõi mà không cần click chuột.

---

## 8. Authentication/Authorization & bảo mật
- **Admin App cục bộ**: Khóa/mở khóa nút cấu hình, backup DB (mật khẩu quản trị cục bộ).
- **Server API Auth**: Xác thực qua `POST /api/auth/login` (Username/Password), nhận `token` JWT Bearer và `expiresInSeconds`.
- **Bảo mật**: Mật khẩu API lưu trong SQLite local appdata.

---

## 9. Tích hợp bên thứ ba (Third-party integrations)
- **ScaleData Server API**:
  - Endpoint: `POST /api/scale/sync`
  - Giới hạn: Rate limit 5 requests/sec. Batch 10-50 records/request.

---

## 10. Testing
- **Cách test**: Chạy ứng dụng, ném file `.txt` mẫu vào thư mục `WatchFolderPath`.
- **Kiểm tra luồng**: Quan sát trên Data Grid View thấy dòng mới xuất hiện, cột `IsSynced` chuyển trạng thái, file gốc tự chuyển sang subfolder `Processed`.
- **Lệnh test**:
  ```powershell
  dotnet test
  ```

---

## 11. Deployment & CI/CD
- **Deploy thủ công**: Copy folder sau khi `dotnet publish` sang máy tính IPC/PC kết nối với máy cân HSU 2000 tại xưởng.
- **Rollback**: Giữ bản build cũ hoặc quay lại git tag release trước đó.

---

## 12. Monitoring & Logging
- **UI Status**: Label góc dưới cùng hiển thị trực tiếp trạng thái Sync (`_lblSyncStatus`: "Đang chờ...", "Đã đồng bộ X bản ghi", "Lỗi 403...").
- **Log lỗi**: Hiển thị qua MessageBox khi gặp lỗi IO/thư mục.

---

## 13. Known issues / Technical debt
- **EF1002 Warning**: Lệnh `ExecuteSqlRaw` ALTER TABLE thô có cảnh báo bảo mật từ EF Core linter (đã kiểm soát do chuỗi SQL tĩnh nội bộ).
- **Hardcode Admin Password**: Mật khẩu unlock quyền Admin cục bộ hiện đang fix cứng trong code `MainForm.cs`.

---

## 14. Liên hệ & tài nguyên khác
- **Người duy trì**: `treepoo2023@gmail.com`
- **Tài liệu tham khảo**:
  - Hướng dẫn vận hành thiết bị test lực dính tesa HSU 2000.
