# Project Plan: Thêm Sheet Pivot khi Xuất Báo Cáo

## Mục tiêu (Goal)
Cập nhật tính năng **Xuất báo cáo (.xlsx / .csv)**. Khi người dùng click xuất file Excel, phần mềm sẽ tạo ra một file `.xlsx` bao gồm **2 sheet**:
1. **Sheet 1 (`Data`)**: Giữ nguyên định dạng gốc, chứa toàn bộ chi tiết từng lần đo trên Data Grid.
2. **Sheet 2 (`Pivot`)**: Tự động gom nhóm dữ liệu hiển thị trên Data Grid theo `Mã Nart` và `Mã Lô`, xếp ngang các giá trị Trung bình lực theo 3 vị trí `D`, `G`, `C`.

---

## Chi tiết Triển khai (Proposed Changes)

**Thay đổi duy nhất trong file `MainForm.cs`, tại hàm `BtnExport_Click`**.

Hiện tại, hàm `BtnExport_Click` đang tạo ra 1 WorkSheet tên `"Data"`. Chúng ta sẽ thêm logic để tạo WorkSheet thứ 2 tên `"Pivot"`.

### Logic xử lý Sheet "Pivot"
1. Lấy toàn bộ dữ liệu đang hiển thị trên lưới `_dgvResults` (chỉ lấy các dòng hiển thị / đã lọc).
2. Gom nhóm (Group by) các dòng theo `Nart` và `BatchCode`.
3. Ghi Header cho Sheet Pivot:
   - Cột A: `Mã Nart`
   - Cột B: `Mã Lô`
   - Cột C: `D`
   - Cột D: `G`
   - Cột E: `C`
   - Format Header: Nền xanh (`TesaBlue`), chữ trắng in đậm (giống Sheet 1).
4. Duyệt qua từng nhóm, gán giá trị tương ứng vào cột D, G, C (giữ định dạng F3).
   - Nếu đo trùng lặp, dùng logic lấy Id cao nhất (mới nhất).
   - Nếu thiếu vị trí, điền `---`.
5. Tự động căn lề (`AdjustToContents`) cho Sheet Pivot.

---

## Chi tiết Công việc (Task Breakdown)

- [ ] Cập nhật hàm `BtnExport_Click` trong `MainForm.cs`.
- [ ] Trích xuất dữ liệu từ `_dgvResults.Rows` thành danh sách các dòng (tương tự logic Copy Pivot).
- [ ] Thêm `var wsPivot = wb.Worksheets.Add("Pivot");`.
- [ ] Ghi Header và đổ dữ liệu đã group vào `wsPivot`.
- [ ] Build dự án và test không có lỗi biên dịch.

---

## Kế hoạch Kiểm tra (Verification Checklist)

- [ ] **Happy Path**: Nhấn "Xuất báo cáo", mở file `.xlsx` xem có 2 sheet (Data và Pivot) hay không. Sheet Pivot phải chứa định dạng D-G-C chuẩn xác.
- [ ] **Data Filtering**: Lọc dữ liệu trên lưới (chỉ để lại 1 Batch), nhấn xuất báo cáo -> Sheet Pivot chỉ được chứa 1 Batch đó.
- [ ] **Trùng/Thiếu vị trí**: Cố tình tạo mock data bị trùng vị trí D hoặc thiếu vị trí G -> Xuất báo cáo -> Sheet Pivot phải hiển thị `---` hoặc lấy giá trị mới nhất, ứng dụng không được crash.
