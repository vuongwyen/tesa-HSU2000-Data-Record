---
type: reference
created: 2026-07-16
updated: 2026-07-16
---

# API Contract: ScaleData Server & Clients

Tài liệu chuẩn giao tiếp để code Client mới bắn data lên Server.

## 1. Xác thực (Authentication)

Client phải gọi API Login để lấy Token trước khi làm việc khác.

- **URL:** `POST /api/auth/login`
- **Header:** `Content-Type: application/json`
- **Body:**
  ```json
  {
    "username": "admin",
    "password": "admin123"
  }
  ```
- **Response (200 OK):**
  ```json
  {
    "token": "eyJhbGciOiJIUzI1...",
    "expiresInSeconds": 3600
  }
  ```

> [!IMPORTANT]
> Lưu Token lại. Tất cả các request sau này phải kẹp header: `Authorization: Bearer <token>`

---

## 2. Đồng bộ Dữ liệu (Sync Data)

Dùng để đẩy dữ liệu test lên Server. Hỗ trợ gửi 1 cục (Batch) nhiều bản ghi cùng lúc.

- **URL:** `POST /api/scale/sync`
- **Headers:** 
  - `Content-Type: application/json`
  - `Authorization: Bearer <token>`
  - `Idempotency-Key: <UUID>` *(Bắt buộc: Mã duy nhất cho mỗi lần gọi API. Nếu rớt mạng gọi lại cùng Key, Server không lưu trùng dữ liệu).*
- **Body:**
  ```json
  {
    "deviceId": "MLT-204", // Tên/Mã thiết bị client (Tự động đăng ký trên server nếu chưa có)
    "data": [
      {
        "recordId": "123e4567-e89b-12d3-a456-426614174000", // UUID duy nhất của bản ghi
        "appId": "APP01", // Mã app nội bộ (nếu có)
        "appType": "Scale", // Phân loại App (VD: "Scale", "QualityControl", "TapeAdhesion")
        "testedAt": "2026-07-16T15:00:00Z", // Thời gian test (ISO 8601 UTC)
        "payload": { 
          // Cục JSON tự do. Client muốn nhét trường gì vào đây cũng được.
          // VD với Scale:
          "WeightValue": 105.5,
          "Unit": "g",
          "BatchCode": "B01"
        }
      }
      // ... có thể gửi n bản ghi trong mảng này
    ]
  }
  ```
- **Response (200 OK hoặc 409 Conflict):**
  - **200 OK**: Lưu thành công.
  - **409 Conflict**: Trùng `Idempotency-Key` (đã lưu trước đó rồi, Client coi như thành công).
  - **403 Forbidden**: Thiết bị (`deviceId`) đã bị Admin Block trên web. Client nên ngừng đồng bộ.
  - **429 Too Many Requests**: Bắn API quá nhanh. Đợi 1-2s rồi thử lại.

---

## 3. Các luật cứng (Rules)

1. **Idempotency-Key**: Phải gen GUID mới mỗi lần *chủ động* ấn Sync. Khi bị lỗi Timeout/Rớt mạng, gọi lại API giữ nguyên GUID cũ.
2. **RecordId**: Client phải tự sinh GUID cho từng bản ghi ngay lúc cân/đo xong lưu vào SQLite. Cấm dùng ID tự tăng kiểu `1, 2, 3`.
3. **Payload**: Được phép linh hoạt. Web UI hiện tại đang map cứng vài trường:
   - Nếu `appType = "Scale"`: UI tìm `payload.WeightValue`, `payload.Unit`, `payload.BatchCode`.
   - Nếu `appType = "QualityControl"`: UI tìm `payload.Status`, `payload.DefectType`.
   - Nếu `appType = "TapeAdhesion"`: UI tìm `payload.PeelForce`, `payload.Unit`.
4. **Rate Limit**: Tối đa 5 requests / giây. Vượt quá sẽ ăn lỗi 429. Mặc định nên gom 10-50 bản ghi vào 1 request (Batch) để chạy cho mượt.
