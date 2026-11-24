# Blazor Server Dashboard

## Stack

* .NET 8, Blazor Server (interactive server render mode)
* MQTTnet 4.3 for MQTT subscribe
* EF Core 8 + Microsoft.Data.Sqlite for telemetry storage
* Chart.js 4 (CDN) for history charts
* Open-Meteo API for reference weather

The dashboard project is at `dashboard/TramThoiTiet.Dashboard/`.

## Files

| File | Role |
|------|------|
| `Program.cs` | DI setup, hosted service, HTTP pipeline, DB creation |
| `appsettings.json` | Broker, topic, default location, API cache |
| `Services/WeatherStore.cs` | Latest sample, 200-sample ring buffer, change event |
| `Services/MqttReceiverService.cs` | Background MQTT client with reconnect loop; writes each valid sample to SQLite |
| `Services/HistoryService.cs` | Reads SQLite and buckets rows for each time range |
| `Services/WeatherAdvice.cs` | Weighted temperature + light verdict |
| `Services/AiAdviceService.cs` | On-demand AI commentary via the OpenRouter API |
| `Services/OpenMeteoService.cs` | API client with a 10-minute cache |
| `Data/WeatherRecord.cs` | EF entity; maps NaN to NULL (SQLite REAL cannot store NaN) |
| `Data/WeatherDbContext.cs` | DbContext with the `WeatherRecords` set |
| `Components/Pages/Index.razor` | The comparison page |
| `wwwroot/js/app.js` | Geolocation interop and the history chart |

## Data flow

1. The ESP32 publishes JSON to `v1/devices/me/telemetry` once per
   cycle (5 s demo mode, 60 s battery mode).
2. `MqttReceiverService` parses each message, adds a `WeatherSample`
   to `WeatherStore`, and writes a `WeatherRecord` to SQLite. If the
   device clock was not synced (`ts` missing or implausible), the
   arrival time is stored instead.
3. `Index.razor` listens to the `WeatherStore.Changed` event, updates
   the page and pushes the history to the Chart.js chart.
4. The Open-Meteo client fetches current weather for the chosen
   coordinates. Results are cached for 10 minutes per location.

## Storage and history

The database is `tramthoitiet.db` next to the project files, created by
`EnsureCreated()` at startup. The `WeatherRecord` table stores one row
per accepted telemetry message: timestamp, device, temperature,
humidity, wind speed, wind direction, rain, light and the sensor flag.
Numeric columns are nullable because SQLite REAL cannot store NaN; a
missing sensor value is stored as NULL and read back as NaN.

`HistoryService` serves three ranges:

| Range | Source | Bucketing | Label format |
|-------|--------|-----------|--------------|
| Direct (Trực tiếp) | 200-sample in-memory ring | none | `HH:mm:ss` |
| 1 hour | SQLite rows, raw | none | `HH:mm:ss` |
| 24 hours | SQLite rows | mean per 1 minute | `HH:mm` |
| 7 days | SQLite rows | mean per 10 minutes | `dd/MM HH:mm` |

Bucket means skip NULL/NaN values; the direction mean is rounded, and
a bucket keeps the sensor flag only when all its rows are OK. In the
chart, missing values become gaps (NaN is mapped to null before the
JS call, because Blazor interop cannot serialize NaN).

The history chart has five series: temperature and humidity on the
left axis, wind speed, rain and light (lx) on the right axes.

## Weighted advice

`WeatherAdvice.Assess(temperature, light)` scores the current
conditions:

* Temperature score: 100 inside 24–30 °C, minus 10 per °C outside the
  band, clamped to 0..100.
* Light score: 100 for 200–600 lx; 70 for ≥ 600 lx (glare) or < 50 lx
  (dark, neutral); 60 for 50–200 lx (overcast).
* Total: 60 % temperature + 40 % light, rounded.

| Score | Level | Badge |
|-------|-------|-------|
| ≥ 75 | Thuận lợi | green |
| 50–74 | Bình thường | yellow |
| < 50 | Cần lưu ý | orange |

The comment names the temperature band (lạnh, mát, dễ chịu, nóng,
rất nóng), then adds one recommendation. The light value itself stays
in the tile, table and chart; the comment does not repeat it. If
either value is missing, the advice shows
"Không đủ dữ liệu" and no badge.

## AI commentary (on demand)

Below the advice strip sits a "Nhận xét AI" card with a "Nhận xét"
button. The AI sentence is generated only when the user clicks the
button — nothing calls the API on its own.

`AiAdviceService` sends the current readings (temperature, humidity,
wind, direction, rain, light, sensor flag) plus the weighted score and
level to the OpenRouter chat completions API (`Ai:Model`, default
`liquid/lfm-3-1.7b:free`) with a Bearer key and asks for one 1–2
sentence Vietnamese comment: a temperature band plus one actionable
recommendation. The prompt restricts the model to the provided
measurements only — no outside information, no invented values. The
API key comes from `Ai:ApiKey` in `appsettings.json` and is never
hard-coded. The Liquid model is a reasoning model: reasoning is
mandatory on this endpoint, so the request sets `max_tokens` 2500 and
the HTTP timeout is 15 s — a real answer uses ~1300 completion tokens
and lands in about 8 s.

| Result | Meaning | Panel shows |
|--------|---------|-------------|
| Ok | API answered | the AI sentence |
| NotConfigured | `Ai:ApiKey` empty | "Chưa cấu hình API key…" |
| Failed | network/API/parse error or missing readings | "Không tạo được nhận xét, thử lại sau." |

While the model is generating, the button is disabled and the panel
shows a spinner; the rest of the page keeps updating over MQTT. The
weighted badge and comment above the card stay untouched — the AI
layer only phrases the sentence. A repeated click with the same
condition band (temperature band + light band + level) reuses the
cached sentence for 5 minutes; a failed attempt retries after 30
seconds. Without a key the dashboard behaves exactly as before.

## Comparison page

The page shows:

* Connection badges for MQTT and sensor health.
* The user's location as a place name (browser geolocation plus reverse
  geocoding via OpenStreetMap Nominatim). If the user denies the location
  request, the configured default location is used.
* A comparison table: local value, API value and the difference for
  temperature, humidity, wind speed, wind direction, rain and light.
* Wind direction as compass labels (B, ĐB, Đ, ĐN, N, TN, T, TB).
* Rain and light (local only — the API column shows a dash).
* The advice badge with its comment.
* A history chart with range buttons: Trực tiếp, 1 giờ, 24 giờ,
  7 ngày.
* Today's forecast and a 5-day forecast (min/max temperature, weather
  state, rain amount and probability) from Open-Meteo.

Weather states come from the WMO weather code, translated to Vietnamese
("Trời quang", "Mưa phùn nhẹ", "Mưa rào vừa", "Dông"...).

## Location

On page load the dashboard asks the browser for the current position.
The coordinates go to Open-Meteo and to the reverse geocoder, which
returns a place name at street level (for example "Đường Tây Thạnh,
Khu phố 9, Phường Tây Thạnh, Thành phố Hồ Chí Minh"). The button
"Lấy vị trí hiện tại" repeats the request.

## Units

| Value | Local | Open-Meteo | After conversion |
|-------|-------|------------|------------------|
| Temperature | °C | °C | as-is |
| Humidity | % | % | as-is |
| Wind speed | m/s | km/h | divided by 3.6 |
| Wind direction | degrees | degrees | as-is |

Pressure is not part of the system.

## Run

```
dotnet run --project dashboard/TramThoiTiet.Dashboard
```

The console shows the listen URL. Open it in a browser.
