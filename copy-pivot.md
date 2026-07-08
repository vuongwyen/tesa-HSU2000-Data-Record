# Project Plan: Copy Pivot — Nút Copy Nhanh theo Vị trí

## Mục tiêu (Goal)
Thêm nút **"Copy Pivot"** vào thanh công cụ tab Data Sheet. Khi user chọn nhiều dòng cùng 1 lô (Batch) trên lưới và bấm nút này, ứng dụng sẽ tự động sắp xếp và copy dữ liệu theo định dạng ngang (pivot) vào Clipboard:

```
D<Tab>G<Tab>C
18.243<Tab>39.277<Tab>38.261
```

User chỉ cần `Ctrl+V` thẳng vào file Excel master để hoàn tất.

---

## Logic Pivot

Thứ tự cột cố định: **D → G → C** (Đầu → Giữa → Cuối).
- Ánh xạ: `Location == "D"` → cột D, `"G"` → cột G, `"C"` → cột C.
- Nếu 1 vị trí bị thiếu: điền `---`.
- Nếu 1 vị trí bị trùng (đo 2 lần): lấy dòng có `Id` lớn hơn (đo sau).
- Header (Hàng đầu tiên): `D<Tab>G<Tab>C`
- Giá trị: `AvgValue` với định dạng `F3`.

---

## Phạm vi thay đổi (Scope)

**File duy nhất:** `MainForm.cs`

### 1. Thêm trường (Field)
- Thêm `private TesaRoundedButton _btnCopyPivot;` vào danh sách field.

### 2. Thêm Control vào UI (`InitializeComponent`)
- Thêm button `_btnCopyPivot` ("Copy Pivot") vào `pnlDataTop`.
- Vị trí: Ngay bên phải nút `Đồng bộ Server` (`_btnBulkSync`), trên hàng thứ 2 (y=65).
- Màu nền: `Color.DarkGoldenrod` (hoặc màu tương tự để phân biệt với các nút khác).

### 3. Thêm Event Handler
Viết hàm `BtnCopyPivot_Click`:
1. Kiểm tra xem có dòng nào được chọn không. Nếu không → `MessageBox.Show` cảnh báo.
2. Lấy danh sách các dòng được chọn, lấy `Location` và `AvgValue` từ `DataRowView`.
3. Pivot: điền giá trị vào dictionary `{ "D": ..., "G": ..., "C": ... }`.
4. Xây dựng chuỗi text:
   ```
   "D\tG\tC\r\n{valD}\t{valG}\t{valC}"
   ```
5. Dùng `Clipboard.SetText(pivotText)` để đưa vào Clipboard.
6. Hiển thị `MessageBox.Show` xác nhận thành công.

---

## Chi tiết Công việc (Task Breakdown)

- [ ] Thêm field `_btnCopyPivot` vào danh sách field của `MainForm`.
- [ ] Khởi tạo và thêm `_btnCopyPivot` vào `pnlDataTop` trong `InitializeComponent`.
- [ ] Viết hàm `BtnCopyPivot_Click` với đầy đủ logic pivot, validation và Clipboard.
- [ ] Build và kiểm tra không có lỗi.

---

## Kế hoạch Kiểm tra (Verification Checklist)

- [ ] **Happy path**: Chọn 3 dòng có Location = D, G, C → bấm Copy Pivot → dán vào Notepad → kiểm tra định dạng đúng.
- [ ] **Thiếu vị trí**: Chọn 2 dòng (thiếu G) → Copy Pivot → cột G phải hiển thị `---`.
- [ ] **Trùng vị trí**: Chọn 2 dòng cùng Location D → Copy Pivot → phải lấy dòng Id lớn hơn.
- [ ] **Không chọn gì**: Bấm Copy Pivot khi không có dòng nào được chọn → phải hiện thông báo cảnh báo.
