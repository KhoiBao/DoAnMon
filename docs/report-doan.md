# Mục báo cáo: Lưu trữ dữ liệu, biểu đồ lịch sử và nhận xét theo trọng số

> Đoạn dưới đây viết theo văn phong báo cáo môn học, có thể dán trực
> tiếp vào file Word. Bảng số liệu lấy từ `verification.md` (đã chạy
> thật trên máy ngày 16/11/2026).

---

## 4.x Lưu trữ dữ liệu đo vào cơ sở dữ liệu

### 4.x.1 Lựa chọn cơ sở dữ liệu

Nhóm chọn **SQLite** kết hợp **Entity Framework Core 8** để lưu dữ
liệu telemetry. Lý do lựa chọn:

- SQLite là cơ sở dữ liệu nhúng, không cần cài đặt server, toàn bộ dữ
  liệu nằm trong một file duy nhất (`tramthoitiet.db`) nên dễ sao lưu
  và dễ trình bày khi demo.
- EF Core cho phép định nghĩa bảng bằng lớp C# (`WeatherRecord`),
  tránh viết SQL thủ công; thao tác tạo bảng tự động khi khởi động
  (`EnsureCreated`).
- Mỗi bản ghi chỉ vài chục byte, tốc độ ghi đáp ứng được tần suất gửi
  1 mẫu/5 giây (chế độ demo) hoặc 1 mẫu/60 giây (chế độ pin).

Cột số được khai báo kiểu `double?` (cho phép NULL) vì kiểu REAL của
SQLite không thể lưu giá trị NaN; khi cảm biến lỗi hoặc bản tin thiếu
trường, hệ thống lưu NULL thay vì giá trị sai.

### 4.x.2 Cấu trúc bảng

Bảng `WeatherRecords` (một dòng cho mỗi bản tin hợp lệ):

| Cột | Kiểu | Ý nghĩa |
|-----|------|---------|
| Id | INTEGER (khoá chính, tự tăng) | Số thứ tự bản ghi |
| ReceivedAt | TEXT (DateTime) | Thời điểm đo — lấy từ trường `ts` (Unix epoch, NTP GMT+7); nếu thiết bị chưa đồng bộ giờ thì lấy thời điểm nhận |
| Device | TEXT | Tên trạm (`tram-mini-01`) |
| NhietDo | REAL (nullable) | Nhiệt độ °C |
| DoAm | REAL (nullable) | Độ ẩm % |
| TocDoGio | REAL (nullable) | Tốc độ gió m/s |
| HuongGio | INTEGER | Hướng gió độ |
| Mua | REAL (nullable) | Mưa mm/h |
| AnhSang | REAL (nullable) | Cường độ ánh sáng lux |
| SensorOk | INTEGER (bool) | Trạng thái cảm biến |

Việc ghi diễn ra trong `MqttReceiverService`: sau khi phân tích bản
tin JSON hợp lệ, dịch vụ lưu mẫu vào bộ đệm trực tiếp (ring buffer
200 mẫu) và ghi một dòng vào SQLite.

## 4.y Thu thập cường độ ánh sáng (LDR)

Hệ thống bổ sung một mô-đun LDR nối chân AO vào GPIO 33 của ESP32.
Giá trị ADC (0–4095) được chuyển tuyến tính sang lux theo công thức:

```
lux = adc / 4095 × 1000
```

Cùng lúc với các kênh ADC khác, kênh ánh sáng được lấy mẫu 10 lần,
loại bỏ giá trị cao nhất và thấp nhất rồi lấy trung bình (trim mean);
nếu biên độ dao động trong cửa sổ lấy mẫu vượt 3000, kênh được đánh
dấu lỗi và trường `ok` trong bản tin chuyển thành `false`. Giá trị lux
được đóng gói vào trường `lux` của bản tin MQTT, hiển thị ở thẻ "Ánh
sáng (lx)", dòng "Ánh sáng (lx)" trong bảng so sánh, và thành chuỗi
dữ liệu thứ năm trên biểu đồ lịch sử (trục riêng bên phải).

## 4.z Nhận xét hiện tại theo trọng số (WeatherAdvice)

Trang dashboard tính một nhận xét định lượng từ hai yếu tố ảnh hưởng
trực tiếp đến người dùng ngoài trời: nhiệt độ và ánh sáng.

**Điểm nhiệt độ (T):** cho điểm 100 trong khoảng dễ chịu 24–30 °C;
mỗi °C lệch khỏi khoảng bị trừ 10 điểm; kết quả giới hạn trong
[0, 100].

```
T = clamp(100 − 10 × dist(T_c, [24; 30]), 0, 100)
```

**Điểm ánh sáng (L):**

| Khoảng lux | Ý nghĩa | Điểm |
|-----------|---------|------|
| 200–600 | nắng vừa | 100 |
| ≥ 600 | nắng gắt | 70 |
| < 50 | tối (không phản ánh ban ngày) | 70 |
| 50–200 | âm u, nhiều mây | 60 |

**Điểm tổng hợp:** lấy trọng số 60 % nhiệt độ và 40 % ánh sáng:

```
S = 0,6 × T + 0,4 × L   (làm tròn đến nguyên)
```

Phân loại:

| S | Mức | Nhãn hiển thị |
|---|-----|----------------|
| ≥ 75 | Thuận lợi | nhãn xanh |
| 50–74 | Bình thường | nhãn vàng |
| < 50 | Cần lưu ý | nhãn cam |

Nhận xét hiển thị kèm dải nhiệt độ (lạnh, mát, dễ chịu, nóng, rất
nóng) và một khuyến nghị ưu tiên theo thứ tự: nhiệt ≥ 32 °C → hạn chế
ra ngoài giữa trưa; nhiệt < 20 °C → giữ ấm; lux < 50 → trời tối;
lux ≥ 600 → che chắn; lux < 200 → trời âm u; còn lại → điều kiện dễ
chịu. Giá trị ánh sáng chỉ hiển thị ở thẻ số liệu, bảng và biểu đồ,
không lặp lại trong câu nhận xét. Thiếu một trong hai giá trị thì
hiển thị "Không đủ dữ liệu", không hiện nhãn.

## 4.t Biểu đồ lịch sử

Biểu đồ Chart.js có 5 chuỗi dữ liệu: nhiệt độ và độ ẩm (trục trái),
tốc độ gió, mưa và ánh sáng (các trục phải). Bốn chế độ truy vấn:

| Chế độ | Nguồn dữ liệu | Cách gộp | Định dạng trục thời gian |
|--------|--------------|----------|--------------------------|
| Trực tiếp | bộ đệm RAM (200 mẫu) | không gộp | HH:mm:ss |
| 1 giờ | SQLite, từng dòng | không gộp | HH:mm:ss |
| 24 giờ | SQLite | trung bình mỗi phút | HH:mm |
| 7 ngày | SQLite | trung bình mỗi 10 phút | dd/MM HH:mm |

Trung bình của mỗi nhóm chỉ tính trên các giá trị hợp lệ (bỏ qua
NULL); giá trị thiếu được ánh xạ thành null trước khi gọi JavaScript
vì Blazor không chuyển được NaN, và Chart.js vẽ thành khoảng đứt
trên đường đồ thị.

## 4.u Kết quả kiểm thử

### Kiểm thử đơn vị (xUnit, 13 bài)

| Nhóm | Số bài | Phạm vi | Kết quả |
|------|--------|---------|---------|
| WeatherAdvice | 8 | biên dải nhiệt/ánh sáng, trọng số, thiếu dữ liệu | PASS 8/8 |
| HistoryService | 5 | truy vấn từng dòng, gộp 1 phút, gộp 10 phút, bỏ NULL, trung bình hướng gió | PASS 5/5 |

Bài kiểm thử HistoryService chạy trên SQLite in-memory, tức là kiểm
thử cả câu truy vấn EF Core thật chứ không dùng mock.

### Kiểm thử end-to-end (16/11/2026)

Phát bản tin thử bằng client MQTT Python (trường `t, h, ws, wd, r,
lux, ok, ts`), quan sát trang trong trình duyệt và dò trực tiếp file
SQLite bằng Python `sqlite3`:

| Hạng mục | Kết quả |
|----------|---------|
| Ghi DB mỗi bản tin hợp lệ | PASS — lệnh INSERT ghi log mỗi mẫu |
| Lưu đúng giá trị lux (420/380/450) | PASS — đọc lại từ bảng đúng giá trị |
| Bản tin thiếu trường → NULL, trang hiển thị "–" | PASS |
| Nhãn nhận xét (29,1 °C + 450 lx → "Thuận lợi") | PASS |
| 4 chế độ biểu đồ (Trực tiếp/1 giờ/24 giờ/7 ngày) | PASS |
| Giá trị thiếu vẽ thành khoảng đứt trên đồ thị | PASS |
| Console trình duyệt không lỗi | PASS |

Trạng thái cuối: 12 dòng trong `WeatherRecords`, 9 dòng có giá trị
ánh sáng, 3 dòng NULL (bản tin của thiết bị lạ trên broker công cộng
không có các trường dự kiến).

### Hạn chế đã ghi nhận

- Broker dùng để demo là broker công cộng, nên dashboard cũng nhận
  được bản tin của thiết bị lạ; các bản tin này được lưu với NULL và
  đánh dấu `SensorOk = false`.
- SQLite phù hợp quy mô một trạm; nếu mở rộng nhiều trạm cần chuyển
  sang PostgreSQL/SQL Server và thêm chỉ mục theo thời gian.

## 4.v Nhận xét AI theo yêu cầu (tùy chọn, bấm để sinh)

Bên cạnh mô hình trọng số, hệ thống có một khối "Nhận xét AI" hiển thị
riêng trên trang: câu nhận xét chỉ được sinh khi người dùng bấm nút
"Nhận xét", tránh gọi API liên tục.

Kiến trúc lai:

- Điểm số và nhãn vẫn đến từ mô hình trọng số (phần 4.z) — phần này
  tất định, kiểm thử đơn vị được.
- Dịch vụ `AiAdviceService` gửi số liệu hiện tại (nhiệt độ, độ ẩm,
  gió, hướng gió, mưa, ánh sáng, trạng thái cảm biến) cùng điểm số và
  nhãn tới API OpenRouter (định dạng OpenAI chat completions, xác thực
  Bearer; model cấu hình qua `Ai:Model`, mặc định model miễn phí
  `liquid/lfm-3-1.7b:free`), yêu cầu viết 1–2 câu tiếng Việt: một câu
  mô tả dải nhiệt độ và một khuyến nghị hành động. Prompt ràng buộc
  model CHỈ đánh giá dựa trên các số liệu đã nêu — không tự bịa thêm
  số liệu, không tra cứu thông tin bên ngoài.
- Model Liquid là model reasoning (bắt buộc có phần suy luận): token
  suy luận tính vào budget trả lời, vì vậy request đặt `max_tokens`
  2500 và timeout HTTP 15 giây; một câu trả lời thật dùng ~1300 token
  và hoàn tất trong khoảng 8 giây (đo thực tế 2026-11-16).
- API key đặt trong `appsettings.json` (`Ai:ApiKey`), không nhúng vào
  mã nguồn (bám sát mục an toàn SEC-4).

Ba trạng thái hiển thị: thành công (câu của AI), chưa cấu hình key
("Chưa cấu hình API key…"), và lỗi mạng/API ("Không tạo được nhận xét,
thử lại sau."). Khi đang sinh câu, nút bị khóa và khối hiển thị biểu
tượng quay; phần còn lại của trang vẫn cập nhật dữ liệu trực tiếp.

Để không tốn hạn mức API: câu trả lời được cache theo "dải điều kiện"
(dải nhiệt độ + dải ánh sáng + nhãn) trong 5 phút — bấm lại cùng điều
kiện lấy câu cũ; lần gọi lỗi được giữ 30 giây trước khi thử lại. Không
có key thì hệ thống hoạt động như cũ, không phát sinh lỗi.

---

*Nguồn số liệu: `docs/verification.md` mục 7–8, chạy ngày 16/11/2026.*
