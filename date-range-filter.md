# Project Plan: Date Range Filter cho Data Grid và Export

## Mục tiêu (Goal)
Khắc phục lỗi "Mất dữ liệu cũ" bằng cách cho phép người dùng lọc (filter) kết quả đo lường theo khoảng thời gian (Từ ngày - Đến ngày) ngay trên giao diện `Data Sheet`, đồng thời xuất báo cáo tương ứng với khoảng thời gian này thay vì chỉ lấy cứng 100 bản ghi mới nhất.

## Phạm vi thay đổi (Scope)
- **File**: `MainForm.cs`
- **Thành phần UI**: Thêm `DateTimePicker` cho "Từ ngày" (dtpFromDate), "Đến ngày" (dtpToDate) và một nút "Lọc" (btnFilterDate) tại `pnlDataTop`.
- **Logic Data**:
  - Gỡ bỏ `.Take(100)` cứng ở hàm `InitializeServices`.
  - Thay bằng hàm `LoadDataFromDateRange(DateTime from, DateTime to)` để truy vấn dữ liệu từ DB, clear lưới và gán lại dữ liệu.
  - Sửa lại tính năng Cập nhật (khi có file mới) hoặc Export để đảm bảo nó vẫn tuân thủ bộ lọc ngày (nếu cần thiết, hoặc Export chỉ xuất dữ liệu đang hiển thị trên Grid).

---

## Phân công (Agent Assignments)
- **`frontend-specialist`**: Cập nhật lại UI trong hàm `InitializeServices` của `MainForm.cs` (thêm các thẻ Label, DateTimePicker, Button cho bộ lọc ngày mà vẫn đảm bảo giao diện không bị lệch).
- **`backend-specialist`**: Cập nhật logic `LoadDataFromDateRange` và sửa các truy vấn LINQ thay thế cho lệnh `.Take(100)` mặc định. Đảm bảo luồng xử lý không bị đơ giao diện nếu truy vấn số lượng lớn.

---

## Chi tiết Công việc (Task Breakdown)

### 1. Thay đổi Giao diện (UI) `MainForm.cs`
- [ ] Tại `InitializeServices()`, trong vùng code khởi tạo `pnlDataTop` (sau phần `lblSearch` / `_txtSearch`).
- [ ] Thêm label `Từ ngày:` và `DateTimePicker _dtpFrom`.
- [ ] Thêm label `Đến ngày:` và `DateTimePicker _dtpTo`.
- [ ] Thêm nút `btnFilter` ("Lọc").
- [ ] Chỉnh sửa thuộc tính `Location` (hoặc cấu trúc Panel) của các nút hiện tại (Backup, Restore, Export) để chừa chỗ cho control lọc ngày.

### 2. Cập nhật Logic tải Dữ liệu (Data Fetching)
- [ ] Viết hàm `LoadDataByDateRange(DateTime from, DateTime to)` trong `MainForm.cs`:
  - `var data = _dbContext.TestResults.Where(x => x.Timestamp >= from && x.Timestamp <= to).OrderByDescending(x => x.Id).ToList();`
  - Đẩy `data` vào `_dataTableResults`.
- [ ] Trong lần load đầu tiên (hàm `InitializeServices`), thay vì lấy cứng `.Take(100)`, gọi `LoadDataByDateRange` với giá trị mặc định (Ví dụ: `_dtpFrom.Value = DateTime.Today.AddDays(-7);` và `_dtpTo.Value = DateTime.Now;` để load 7 ngày gần nhất).
- [ ] Đăng ký event `Click` cho `btnFilter` để gọi hàm `LoadDataByDateRange(_dtpFrom.Value, _dtpTo.Value)`.

### 3. Cập nhật Luồng đồng bộ & Xoá (nếu cần)
- [ ] Tính năng Xuất báo cáo (Export) đang dùng `_dgvResults` (chỉ xuất row hiển thị) -> khi lưới đã được lọc theo ngày, tính năng này **TỰ ĐỘNG** đúng, chỉ xuất ra các bản ghi theo khoảng ngày đó. Do đó không cần sửa lại Export.
- [ ] Tính năng xoá dòng (`BtnBulkDelete_Click`) chỉ tác động lên bản ghi đang chọn -> vẫn hoạt động bình thường.

---

## Kế hoạch Kiểm tra (Verification Checklist)
- [ ] Giao diện (UI): Các nút DatePicker không bị đè lên TextBox tìm kiếm hoặc nút Export.
- [ ] Tải dữ liệu mặc định: Lần đầu mở app chỉ load dữ liệu 7 ngày gần nhất, không query chậm (không gây giật lag).
- [ ] Logic lọc: 
  - Đổi khoảng ngày (chọn khoảng thời gian 1 tháng trước) và bấm Lọc -> lưới hiển thị đúng số lượng cũ.
- [ ] Tính năng Export: 
  - Bấm Xuất báo cáo -> File Excel xuất ra CHỈ chứa các dòng dữ liệu nằm trong khoảng thời gian đã lọc.
- [ ] Dữ liệu mới: Khi có file đo đạc sinh ra (watcher đọc file), nếu bản ghi đó lọt vào ngày lọc (VD: ngày hôm nay), bản ghi phải tự động xuất hiện trên grid.

---
*Lưu ý: Mọi thay đổi đều được thực hiện trên repo local.*
