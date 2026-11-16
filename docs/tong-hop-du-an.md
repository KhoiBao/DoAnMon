# Tổng hợp dự án — Trạm thời tiết mini IoT

Tài liệu gộp lại mọi thông tin cần thiết để thành viên làm **slide** và **Word báo cáo**.
Mọi con số trong đây đã được đối chiếu trực tiếp với source code (2026-11-16).
Chi tiết đầy đủ nằm ở: `docs/firmware.md`, `docs/dashboard.md`, `docs/verification.md`, `docs/security.md`, `docs/report-doan.md`.

---

## 1. Thông tin nhanh (bảng dùng cho slide mở đầu)

| Mục | Giá trị |
|---|---|
| Tên dự án | Trạm thời tiết mini (IoT) |
| Vi điều khiển | ESP32 DevKit-C v4 (board `esp32dev`) |
| Framework firmware | Arduino, PlatformIO (`platformio.ini`, env `esp32dev`) |
| Cảm biến/khối đo | DHT22 (nhiệt độ + độ ẩm), 3 biến trở mô phỏng gió/hướng gió/mưa, module LDR (ánh sáng) |
| Giao thức truyền | MQTT (PubSubClient 2.8), broker công cộng `broker.hivemq.com:1883` |
| Topic telemetry | `v1/devices/me/telemetry` (tương thích ThingsBoard) |
| Đồng bộ thời gian | NTP `pool.ntp.org`, múi giờ GMT+7 |
| Dashboard | Blazor Server, .NET 8, MQTTnet 4.3.7.1207 |
| CSDL | SQLite qua EF Core 8 (`Microsoft.EntityFrameworkCore.Sqlite 8.0.11`), file `tramthoitiet.db` |
| Dự báo thời tiết | Open-Meteo API (hiện tại + 5 ngày), cache 10 phút |
| Nhận xét AI | Gemini API (`gemini-2.5-flash`), chỉ chạy khi bấm nút, có fallback template |
| Unit test | xUnit — 23/23 PASS (9 WeatherAdvice + 5 HistoryService + 9 AiAdviceService) |
| Kiểm chứng E2E | Dashboard 10 PASS (§5), lưu trữ/lịch sử 14 PASS (§7), AI 6 PASS (§8) |

## 2. Mục tiêu và phạm vi

- Xây dựng **trạm thời tiết mô phỏng trên Wokwi**: đo nhiệt độ, độ ẩm, gió (tốc độ + hướng), mưa, ánh sáng.
- Gửi dữ liệu qua **MQTT** lên broker công cộng; **tồn dữ liệu SQLite** để xem lại lịch sử.
- Dashboard web hiển thị: số liệu trực tiếp, bảng so sánh với Open-Meteo, dự báo 5 ngày, **biểu đồ lịch sử 4 dải**, **nhận xét trọng số** và **nhận xét AI**.
- Giới hạn: phần cứng mô phỏng (biến trở thay cảm biến thật), broker công cộng dùng cho demo.

## 3. Kiến trúc tổng thể

```
[Wokwi simulator: ESP32 + DHT22 + 3 pot + LDR + 2 LED]
        |  WiFi (Wokwi-GUEST) + NTP GMT+7
        |  JSON 8 trường (t, h, ws, wd, r, lux, ok, ts)
        v  MQTT publish @ broker.hivemq.com:1883
[Dashboard Blazor Server (.NET 8)]
   MqttReceiverService (BackgroundService) --subscribe--> WeatherStore (ring 200, in-memory)
        |                                     |
        v                                     v
   SQLite tramthoitiet.db                Index.razor (Blazor Server render)
        |                                     ^  ^
        v                                     |  |
   HistoryService (raw / bucket mean) --------+  +-- AiAdviceService --> Gemini API
                                                 +-- OpenMeteoService --> Open-Meteo + Nominatim
```

### Luồng hoạt động (mô tả từng bước cho báo cáo)

1. **ESP32 khởi động**: bật Serial 115200, cấu hình LED GPIO 2/4, khởi DHT22, cấu hình MQTT server.
2. **Kết nối WiFi**: `Wokwi-GUEST`, thử tối đa 30 lần × 200 ms; sau khi nối xong gọi `configTime()` để đồng hồ NTP (GMT+7).
3. **Kết nối MQTT**: nếu mất kết nối, thử lại theo **backoff nhân đôi 2 s → 30 s**; nối được thì đặt lại 2 s.
4. **Đo dữ liệu mỗi 5 s** (chế độ demo) hoặc 1 chu kỳ rồi ngủ sâu 60 s (chế độ pin, xem §5.4):
   - DHT22 đọc nhiệt độ + độ ẩm.
   - Mỗi kênh ADC lấy **10 mẫu cách 2 ms**, sắp xếp chèn, **bỏ min/max, lấy trung bình 8 mẫu** (trim mean); nếu độ lệch max−min > 3000 → kênh đó không ổn định (đầu nổi/không nối).
   - Quy đổi: ADC 12-bit (0–4095) → thang đo tương ứng (bảng §4).
5. **Đóng gói JSON** (`buildTelemetryJson`, ArduinoJson 7, làm tròn 2 chữ số): `t`, `h`, `ws`, `wd`, `r`, `lux`, `ok`, `ts` (Unix epoch giây từ NTP).
6. **Publish MQTT**: nối được → gửi ngay; mất kết nối → **push vào ring buffer 128 × 128 byte** (đè bản ghi cũ nhất khi đầy, đếm `dropped`). Khi MQTT nối lại, buffer được phát lại **cũ nhất trước**, cách nhau 20 ms, rồi in tổng số đã đồng bộ + số mẫu mất.
7. **Dashboard nhận** (MqttReceiverService, QoS 0): parse JSON → `WeatherSample` (trường thiếu = NaN; `ts` hợp lệ (> 1,6 × 10⁹) → thời gian đo từ thiết bị, không hợp lệ → giờ nhận).
8. **Lưu hai nơi**: thêm vào `WeatherStore` (ring 200, kích sự kiện UI render lại) và **INSERT vào SQLite** (`WeatherRecords`).
9. **Hiển thị**: tile số liệu, bảng so sánh đo thực tế vs Open-Meteo (lệch delta), dự báo 5 ngày, biểu đồ lịch sử theo dải đã chọn, badge mức "Thuận lợi / Bình thường / Cần lưu ý".
10. **Nhận xét AI**: chỉ khi người dùng bấm nút **"Nhận xét"** — dashboard gửi prompt (số liệu hiện tại + điểm trọng số) tới Gemini, hiện spinner khi đang sinh, thay kết quả vào card; lỗi hoặc thiếu key thì UI giữ nhận xét template.

## 4. Phần cứng và đấu nối (từ `diagram.json` + `include/Config.h`)

| Thành phần | Chân ESP32 | Thang đo | Ghi chú |
|---|---|---|---|
| DHT22 (nhiệt độ + độ ẩm) | GPIO 5 (SDA 1 dây) | −40…80 °C, 0–100 % | thư viện Adafruit DHT |
| Biến trở 1 — tốc độ gió | GPIO 34 (ADC1) | 0–30 m/s | `WIND_SPEED_MAX_MS` |
| Biến trở 2 — hướng gió | GPIO 35 (ADC1) | 0–359° | giá trị 360 bị kẹp về 359 |
| Biến trở 3 — lượng mưa | GPIO 32 (ADC1) | 0–50 mm/h | `RAIN_MAX_MMH` |
| Module LDR — ánh sáng | GPIO 33 (ADC1, chân **AO**) | 0–1000 lux (xấp xỉ tuyến tính) | `LIGHT_MAX_LUX` |
| LED xanh lá — trạng thái cảm biến | GPIO 2 (trở 220 Ω) | — | sáng khi **tất cả** kênh đo OK |
| LED xanh dương — MQTT | GPIO 4 (trở 220 Ω) | — | sáng khi đã nối broker |

Wokwi parts: `board-esp32-devkit-c-v4`, `wokwi-dht22`, 3 × `wokwi-potentiometer`, `wokwi-photoresistor-sensor`, 2 × `wokwi-led`, 2 × `wokwi-resistor` 220 Ω.

## 5. Firmware — thông số kỹ thuật

### 5.1 Sampling và tín hiệu ADC

| Tham số | Giá trị | Hằng số (`Config.h`) |
|---|---|---|
| Chu kỳ lấy mẫu (demo) | 5000 ms | `SAMPLE_INTERVAL_MS` |
| Số mẫu ADC mỗi lần đọc | 10 mẫu, cách 2 ms | `ADC_SAMPLES` |
| Lọc nhiễu | trim mean: bỏ 1 min + 1 max, trung bình 8 mẫu còn lại | — |
| Ngưỡng ổn định | max−min ≤ 3000 (trên 12-bit) | `ADC_STABLE_SPREAD_MAX` |
| Quy đổi giá trị | `adc / 4095 × giá trị tối đa` của thang đo | — |

`ok` trong payload = DHT22 hợp lệ **và** cả 4 kênh ADC ổn định; khi lỗi, serial in trạng thái từng kênh (DHT/gió/hướng/mưa/sáng).

### 5.2 MQTT

| Tham số | Giá trị |
|---|---|
| Broker / port | `broker.hivemq.com` / 1883 (TCP, không TLS) |
| Topic | `v1/devices/me/telemetry` |
| Client ID | `tram-thoi-tiet-mini`; Device name `tram-mini-01` |
| Username/password | ThingsBoard access token nếu có (`secrets.h`); không có `secrets.h` thì dùng `demo` / rỗng (guard `__has_include`) |
| KeepAlive / socket timeout | 30 s / 10 s |
| Backoff reconnect | 2 s → nhân đôi mỗi lần fail → tối đa 30 s; thành công thì reset 2 s |
| WiFi retry | tối đa 30 × 200 ms |

### 5.3 Payload JSON (đã làm tròn 2 chữ số, buffer tối đa 128 byte)

```json
{"t":29.5,"h":62,"ws":3.4,"wd":120,"r":0,"lux":450,"ok":true,"ts":1763200000}
```

| Key | Ý nghĩa | Đơn vị |
|---|---|---|
| `t` / `h` | nhiệt độ / độ ẩm | °C / % |
| `ws` / `wd` | tốc độ / hướng gió | m/s / độ |
| `r` | lượng mưa | mm/h |
| `lux` | ánh sáng | lx |
| `ok` | toàn bộ cảm biến OK | bool |
| `ts` | thời điểm đo (Unix epoch, NTP GMT+7) | giây |

### 5.4 Hai chế độ hoạt động (`DEEP_SLEEP_ENABLE`)

| Giá trị | Chế độ | Hành vi |
|---|---|---|
| `0` | Demo liên tục | đo + publish mỗi 5 s, chạy mãi — **dùng cho demo dashboard** |
| `1` (giá trị hiện tại trong `Config.h`) | Tiết kiệm pin | đo + publish **1 chu kỳ** rồi ngủ sâu `SLEEP_INTERVAL_S` = 60 s (thức dậy = khởi động lại). **RAM bị xóa** → ring buffer không sống qua giấc ngủ |

Khi demo với dashboard cần dữ liệu liên tục, đặt `DEEP_SLEEP_ENABLE 0` rồi build lại.

### 5.5 Thư viện firmware (`platformio.ini`)

DHT sensor library + Adafruit Unified Sensor, PubSubClient ^2.8, ArduinoJson ^7. Env `native` build riêng `TelemetryBuffer.cpp` với stub để test Unity (`test/test_telemetry_buffer`).

## 6. Dashboard — kiến trúc service

| Thành phần | Loại | Vai trò |
|---|---|---|
| `WeatherStore` | Singleton | Ring buffer 200 mẫu mới nhất, `Latest`, cờ `MqttConnected`, event `Changed` để UI render lại |
| `MqttReceiverService` | BackgroundService | Kết nối + subscribe MQTT (QoS 0), parse payload, push store, **INSERT SQLite** mỗi mẫu; mất nối thì thử lại mỗi 5 s; lỗi JSON chỉ log cảnh báo, app chạy tiếp |
| `HistoryService` | Singleton | Truy vấn SQLite theo dải; gộp bucket trung bình (bỏ NULL/NaN) |
| `WeatherAdvice` | Static | Mô hình trọng số (§7) |
| `AiAdviceService` | Typed HttpClient, timeout 8 s | Nhận xét AI theo yêu cầu (§8) |
| `OpenMeteoService` | Typed HttpClient | Open-Meteo hiện tại + 5 ngày (cache 10 phút/vị trí, đổi km/h → m/s chia 3,6) + Nominatim reverse geocode (zoom 18, tiếng Việt, header UserAgent riêng) |
| `WeatherDbContext` + `WeatherRecord` | EF Core | Bảng `WeatherRecords`, `EnsureCreated` khi khởi động |

Dự án kiểm thử: `TramThoiTiet.Dashboard.Tests` — xUnit 2.9.2, SQLite in-memory cho HistoryService, `HttpMessageHandler` giả đếm số lần gọi cho AiAdviceService. Tổng **23 test**.

### 6.1 Schema SQLite (bảng `WeatherRecords`)

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | INTEGER PK | tự tăng |
| `ReceivedAt` | TEXT (DateTime) | thời điểm đo (từ `ts` thiết bị) hoặc giờ nhận |
| `Device` | TEXT | `tram-mini-01` |
| `NhietDo` / `DoAm` / `TocDoGio` / `Mua` / `AnhSang` | REAL **nullable** | trường thiếu → NULL (SQLite REAL không chứa NaN) |
| `HuongGio` | INTEGER | 0–359 |
| `SensorOk` | INTEGER (bool) | |

Thiết kế nullable được chọn vì topic trên broker công cộng — thiết bị lạ thiếu key vẫn được ghi dòng nhưng các cột đo là NULL (behavior đã chứng minh: probe 12 dòng, 9 có lux, 3 NULL).

### 6.2 Biểu đồ lịch sử (4 dải)

| Dải | Nguồn dữ liệu | Độ phân giải |
|---|---|---|
| Trực tiếp | ring 200 mẫu in-memory | từng mẫu (5 s), nhãn `HH:mm:ss` |
| 1 giờ | SQLite, dòng thô | từng mẫu; NULL vẽ thành khoảng trống đường |
| 24 giờ | SQLite, gộp bucket | trung bình mỗi **1 phút** |
| 7 ngày | SQLite, gộp bucket | trung bình mỗi **10 phút** |

Trung bình bucket **bỏ qua NULL/NaN**; hướng gió lấy trung bình làm tròn; `SensorOk` = AND của tất cả dòng trong bucket. Chart.js vẽ qua `wwwroot/js/app.js` (`updateHistoryChart`); Blazor interop không serialize được `double.NaN` nên UI map NaN → null trước khi gọi JS (null = khoảng trống đường).

## 7. Mô hình chấm điểm trọng số (`WeatherAdvice.cs`)

Điểm tổng **S = round(0,6 × T + 0,4 × L)** (làm tròn AwayFromZero), T/L là điểm nhiệt độ / ánh sáng 0–100.

| Thành phần | Công thức |
|---|---|
| Điểm nhiệt độ T | 100 nếu 24–30 °C; ngoài vùng: `100 − 10 × khoảng cách (°C)`, kẹp 0–100 (mỗi °C lệch trừ 10 điểm) |
| Điểm ánh sáng L | 200–600 lx → 100; ≥600 → 70; 50–200 → 60; <50 → 70 |

| Điểm | Mức |
|---|---|
| ≥ 75 | Thuận lợi |
| 50–74 | Bình thường |
| < 50 | Cần lưu ý |

Dải phân loại dùng cho comment và cache AI:

| Dải nhiệt độ | Dải ánh sáng |
|---|---|
| < 20 °C lạnh · < 25 mát · < 32 dễ chịu · < 35 nóng · ≥ 35 rất nóng | < 50 lx tối · < 200 âm u · < 600 nắng vừa · ≥ 600 nắng gắt |

Nhận xét template = dải nhiệt độ kèm giá trị + **một** khuyến nghị ưu tiên theo thứ tự: t ≥ 32 (hạn chế trưa) → t < 20 (giữ ấm) → lux < 50 (trời tối) → lux ≥ 600 (che chắn) → lux < 200 (âm u) → còn lại (dễ chịu). **Không** nêu chi tiết số lux trong comment (yêu cầu của nhóm); ánh sáng vẫn hiển thị ở tile/bảng/biểu đồ. Dữ liệu thiếu (NaN) → điểm null, mức "Không đủ dữ liệu".

## 8. Nhận xét AI (Gemini, kiến trúc hybrid)

Nguyên tắc: **mô hình trọng số vẫn là xương sống xác định** (chấm điểm, mức, badge); LLM chỉ viết câu nhận xét; mọi lỗi → fallback template, dashboard không bao giờ chết vì AI.

| Khía cạnh | Chi tiết |
|---|---|
| Cách kích hoạt | **Chỉ khi bấm nút "Nhận xét"** trên card — không tự gọi theo mỗi mẫu |
| Endpoint | `POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`, header `x-goog-api-key` |
| Model mặc định | `gemini-2.5-flash`, cấu hình được qua `Ai:Model` |
| Tham số sinh | temperature 0,7; maxOutputTokens 200 |
| Prompt | số liệu hiện tại (t, h, ws, wd, r, lux, trạng thái cảm biến) + điểm trọng số/mức; yêu cầu 1–2 câu tiếng Việt, câu mô tả kèm dải nhiệt độ, một khuyến nghị, không markdown/ngoặc kép, không nhắc giá trị ánh sáng |
| Cache thành công | key `dải nhiệt độ | dải ánh sáng | mức`, TTL **5 phút** (không tốn quota khi điều kiện không đổi) |
| Throttle lỗi | sau 1 lần lỗi, không gọi lại trong **30 s** với cùng điều kiện |
| Timeout HTTP | 8 s |

Trạng thái trả về và hiển thị trên UI:

| Trạng thái | Điều kiện | Hiển thị |
|---|---|---|
| `Ok` | API trả text hợp lệ | câu AI trong card |
| `NotConfigured` | `Ai:ApiKey` rỗng | "Chưa cấu hình API key — dán key vào `Ai:ApiKey` trong appsettings.json." |
| `Failed` | key sai, HTTP lỗi, parse rỗng, timeout, hoặc số liệu NaN | "Không tạo được nhận xét, thử lại sau." + UI vẫn giữ nhận xét template phía trên |

**Vị trí dán API key** (của thành viên, không hard-code vào source): `dashboard/TramThoiTiet.Dashboard/appsettings.json`:

```json
"Ai": {
  "ApiKey": "PASTE_KEY_VAO_DAY",
  "Model": "gemini-2.5-flash"
}
```

Lấy key miễn phí tại Google AI Studio (aistudio.google.com/apikey). `appsettings.json` hot-reload — đổi key **không cần khởi động lại**, bấm lại "Nhận xét" là có hiệu lực.

## 9. Open-Meteo và định vị (phần so sánh của dashboard)

- Vị trí mặc định trong `appsettings.json`: `Location: 10.82, 106.63` (khu vực TP. Hồ Chí Minh); dashboard cũng dùng geolocation trình duyệt (cần user cho phép).
- Current: `temperature_2m`, `relative_humidity_2m`, `wind_speed_10m` (API trả km/h, chia 3,6 → m/s), `wind_direction_10m`.
- Daily 5 ngày: tmax/tmin, `precipitation_sum`, `precipitation_probability_max`, `weathercode` (bảng WMO dịch sang tiếng Việt trong `DescribeWeatherCode`).
- Cache 10 phút theo cặp (lat, lon) — trong 10 phút gọi lại trả cache, không tốn API.
- Tên địa điểm: Nominatim OpenStreetMap reverse geocode, ưu tiên road/quận/thành phố…, ghép tối đa 4 phần, tiếng Việt.

## 10. Hướng dẫn chạy (cho slide "Demo")

### 10.1 Yêu cầu môi trường

- .NET SDK 8.0 (dashboard + test)
- PlatformIO Core 6.x (VS Code extension hoặc `pio` CLI) + Wokwi extension/simulator
- Python 3 (công cụ phụ: `tools/serial_capture.py` bắt serial; client `paho-mqtt` để phát telemetry kiểm thử)

### 10.2 Firmware (Wokwi simulator)

1. Mở thư mục `DoAn/DoAnMon` trong VS Code (PlatformIO + Wokwi extension).
2. Build: `pio run` (đã kiểm chứng SUCCESS, firmware.bin 302 KB).
3. Chạy simulator từ `diagram.json` (Wokwi dùng `wokwi.toml` trỏ tới `.pio/build/esp32dev/firmware.bin` + `.elf`).
4. Serial monitor 115200: banner liệt kê ánh xạ núm xoay; mỗi chu kỳ in JSON đã gửi hoặc "Luu buffer: …" khi mất MQTT.
5. Nếu dùng ThingsBoard thật: copy `include/secrets.example.h` → `include/secrets.h` và dán access token (file này không commit).
6. Demo dashboard liên tục: đặt `DEEP_SLEEP_ENABLE 0` trong `include/Config.h`, build lại.

### 10.3 Dashboard

1. `cd dashboard/TramThoiTiet.Dashboard`
2. `dotnet run` → mở `http://localhost:5000` (Kestrel mặc định, không có launchSettings).
3. Lần chạy đầu tự tạo `tramthoitiet.db` (EnsureCreated).
4. Badge MQTT xanh khi đã subscribe topic; dữ liệu mới xuất hiện ≤ 1 s.
5. Chuyển dải lịch sử bằng các nút "Trực tiếp / 1 giờ / 24 giờ / 7 ngày".
6. AI: dán key vào `appsettings.json` (§8) → bấm **"Nhận xét"**; không key vẫn chạy được toàn bộ phần còn lại.

### 10.4 Unit test

```bash
cd dashboard/TramThoiTiet.Dashboard.Tests
dotnet test
```

Kết quả hiện tại: **23/23 PASS**. Lưu ý: **dừng `dotnet run` trước** khi `dotnet test` — server đang chạy khóa file `.exe`, build test sẽ lỗi MSB3026.

### 10.5 Phát telemetry kiểm thử thủ công (tùy chọn, dùng lại khi kiểm chứng)

Client Python `paho-mqtt` publish JSON (§5.3) lên `broker.hivemq.com`, topic `v1/devices/me/telemetry` — đây chính là phương pháp của các lần kiểm chứng §5/§7/§8 trong `docs/verification.md`.

## 11. Tổng hợp kết quả kiểm thử (đã thực thi trên máy, 2026-11-16)

Nguồn chi tiết: `docs/verification.md`. Chỉ ghi số test đã chạy — khớp với quy ước báo cáo của nhóm (không viết "đã kiểm tra" cho thứ chưa chạy).

| Nhóm | Số lượng | Kết quả |
|---|---|---|
| Firmware build (`pio run`) | compile + link + bin | SUCCESS (302 KB) |
| Unit test dashboard (xUnit) | 23 test | 23/23 PASS |
| Dashboard E2E (§5) | 11 mục | 10 PASS, 1 pending (geolocation cần trình duyệt user) |
| Lưu trữ / lịch sử / advice (§7) | 14 mục | 14/14 PASS; probe DB 12 dòng (9 lux, 3 NULL do thiết bị lạ) |
| Nhận xét AI (§8) | 7 mục | 6 PASS, 1 pending (chờ API key thật của thành viên) |
| Bảo mật (§6) | 7 mục | SEC-2/3/4 PASS; SEC-1/5 FAIL (giới hạn đã ghi nhận); SEC-6/7 không thực hiện (giới hạn) |
| Simulator Wokwi (§4) | 3 mục | pending — chạy trên máy thành viên có Wokwi extension |

Ghi chú: §1 trong verification.md là hồ sơ cũ của chip BME280 tự viết — chip đã bị loại bỏ khỏi thiết kế, **không** đưa vào báo cáo hiện tại.

### Giới hạn đã ghi nhận (nêu thẳng trong báo cáo — không phải thiếu sót ẩn)

1. Broker công cộng: ai cũng publish được cùng topic → thiết bị lạ tạo dòng NULL / có thể giả mạo (SEC-1, SEC-5 — đang là FAIL có chủ đích, khắc phục đề xuất: kiểm tra dải giá trị ở receiver, broker riêng có TLS).
2. Dashboard không có đăng nhập, chạy loopback HTTP (SEC-6/7).
3. Biến trở mô phỏng cảm biến thật; lux xấp xỉ tuyến tính 0–1000.
4. Deep sleep xóa RAM → buffer không sống qua giấc ngủ (chế độ pin).
5. Đường dẫn thành công AI thật (key hợp lệ) chưa có bằng chứng — chờ key của thành viên.
6. Wokwi simulator E2E (§4) chưa chạy trên máy này.

## 12. Gợi ý cấu trúc slide (map sang nguồn tài liệu)

| Slide | Nội dung | Nguồn |
|---|---|---|
| 1–2 | Tên + mục tiêu + phạm vi | §1, §2 |
| 3 | Kiến trúc tổng thể (sơ đồ ASCII vẽ lại thành hình) | §3 |
| 4 | Phần cứng + sơ đồ đấu nối (chụp Wokwi) | §4, `diagram.json` |
| 5 | Firmware: luồng đo → JSON → MQTT (nhấn trim mean + buffer) | §3, §5 |
| 6 | Dashboard: service + SQLite | §6, §6.1 |
| 7 | Biểu đồ lịch sử 4 dải | §6.2 |
| 8 | Mô hình trọng số (công thức + bảng mức) | §7 |
| 9 | Nhận xét AI (hybrid, nút bấm, fallback) | §8 |
| 10 | So sánh Open-Meteo + dự báo 5 ngày | §9 |
| 11 | Kết quả kiểm thử (bảng §11) | `verification.md` |
| 12 | Giới hạn + hướng phát triển | §11, `security.md` |
| 13 | Demo live | §10 |

Mục paste-ready cho Word (tiếng Việt, đã viết sẵn): `docs/report-doan.md` — lý do chọn SQLite, bảng schema, LDR + trim mean, công thức trọng số, bảng test, phần AI, giới hạn.
