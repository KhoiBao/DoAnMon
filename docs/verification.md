# Verification

ASD-STE100 note: this document records executed tests only. The result
columns stay empty until a test runs on this machine.

> 2026-11-16: the custom BME280 chip was retired and its files removed
> (`bme280.chip.c`, `bme280.chip.json`, `tools/verify_bme280_model.py`).
> The simulation now uses the built-in DHT22 part plus potentiometers;
> pressure also comes from a potentiometer (GPIO 33). Section 1 stays as
> the historical record of the executed chip-model test.

## Test environment

| Item | Value |
|------|-------|
| OS | Windows 10/11, x64 |
| PlatformIO Core | 6.2.0 |
| .NET SDK | 8.0 |
| wokwi-cli | 0.29.1 |
| Simulator | Wokwi VS Code extension (local engine) |

## 1. Virtual chip math (executed 2026-11-16)

Command: `python tools/verify_bme280_model.py`

The script implements the chip forward model and the Adafruit
compensation formulas with the same integer arithmetic, then checks the
round trip for both sides.

| Quantity | Cases | Max error | Result |
|----------|-------|-----------|--------|
| Temperature | 8 | 0.0000 °C | PASS |
| Pressure | 6 | 0.164 Pa | PASS |
| Humidity | 6 | 0.0098 %RH | PASS |

The pressure error equals the 20-bit ADC step of a real BME280
(≈0.17 Pa). All 20 cases pass.

## 2. Firmware build (executed 2026-11-16)

Command: `pio run`

| Step | Result |
|------|--------|
| Compile all sources | SUCCESS |
| Link firmware.elf | SUCCESS |
| Create firmware.bin | SUCCESS (302 KB) |

## 3. Chip WASM compile

No longer applies. The built-in Wokwi parts need no chip binary.

## 4. Simulator end-to-end

Procedure:

1. Start the simulator with the Wokwi extension.
2. Capture the serial output (RFC2217 on port 4000 or the serial monitor).
3. Check the first JSON message: temperature 25 °C, humidity 60 %,
   pressure 1013.25 hPa at default slider values.
4. Change each slider and check that the published value follows.

| Check | Expected | Result |
|-------|----------|--------|
| Sensor start | no "LOI" line, boot banner lists the knob mapping | (pending) |
| Slider tracking | published value follows sliders and the DHT22 panel | (pending) |
| MQTT LED | on when connected | (pending) |

## 5. Dashboard end-to-end (executed 2026-11-16)

Method: `dotnet run --no-build` on this machine; test telemetry published
to the demo topic with a Python MQTT client; page inspected in a browser.

| Check | Expected | Result |
|-------|----------|--------|
| Page routing | `http://localhost:5000/` returns the page, not 404 | PASS |
| MQTT badge | green after connect | PASS |
| Telemetry panel | new published values appear within 1 s | PASS |
| Bad data | missing or non-numeric sensor values show "–", badge red | PASS |
| Comparison table | delta column shows signed difference | PASS |
| Open-Meteo fetch | values for the resolved location | PASS |
| Today forecast | min/max, weather state, rain | PASS |
| 5-day forecast | four more days with state names | PASS |
| Reverse geocoding | street-level place name shown | PASS (default location) |
| Geolocation allowed | coordinates set from browser | (pending — needs a user browser) |
| History chart | one point per 5 s | PASS (canvas renders, console clean) |

## 6. Security tests (executed 2026-11-16)

Method: payloads published to the demo MQTT topic from a separate
client; dashboard behavior observed in the browser and in server logs.

| ID | Scenario | Expected | Result |
|----|----------|----------|--------|
| SEC-1 | Third party publishes fake telemetry on the public topic | System accepts or rejects with a warning | FAIL: dashboard accepted a spoofed device ("tram-gia-mao") with out-of-range values (-55 °C, 999 %) without any warning |
| SEC-2 | Malformed JSON payload | Ignore, log, keep running | PASS: logged "Bad telemetry payload", page kept running |
| SEC-3 | XSS string in the device field | Render as text, no script execution | PASS: Blazor encoded the string, `<script>` shown as text |
| SEC-4 | Hard-coded tokens | No tokens in source | PASS: `secrets.example.h` has placeholders only, `secrets.h` never committed |
| SEC-5 | Out-of-range values (999, 400°) | Clamp or flag invalid data | FAIL: dashboard shows the raw values; firmware clamps only its own readings |
| SEC-6 | Dashboard access control | Unknown | Not implemented: the dashboard has no login; anyone on the host can open it |
| SEC-7 | Transport encryption | TLS | Not implemented: MQTT runs plain on port 1883; dashboard is HTTP on loopback |

FAIL and "not implemented" rows are documented limits for the report,
not silent omissions. Remediation candidates: value range checks in the
receiver, a private broker with credentials + TLS, and an authenticated
reverse proxy if the dashboard ever leaves localhost.

## 7. Storage, history chart and advice (executed 2026-11-16)

Method: unit tests with xUnit against SQLite in-memory; end-to-end test
with `dotnet run`, test telemetry published with a Python MQTT client
(payload keys `t`, `h`, `ws`, `wd`, `r`, `lux`, `ok`, `ts`), page
inspected in a browser, database probed with Python `sqlite3`.

| Check | Expected | Result |
|-------|----------|--------|
| Unit tests, advice rules | 8/8 pass (band boundaries, weights, missing data) | PASS |
| Unit tests, history query | 5/5 pass (raw rows, 1-minute and 10-minute bucket means, NULL skip, direction mean) | PASS |
| DB creation | `tramthoitiet.db` created at startup | PASS |
| DB write per message | INSERT logged for each valid sample | PASS |
| Light stored | `lux` 420/380/450 read back from `WeatherRecords.AnhSang` | PASS |
| Missing values | rows without the keys stored with NULL, page shows "–" | PASS |
| Advice badge | 29.1 °C + 450 lx → "Thuận lợi", comment "dễ chịu · nắng vừa" | PASS |
| Light tile and table row | tile and table show the latest lux | PASS |
| Range "Trực tiếp" | live ring data, labels `HH:mm:ss` | PASS |
| Range "1 giờ" | raw rows from SQLite, NULL rows render as line gaps | PASS |
| Range "24 giờ" | one bucket per minute, mean of valid rows only | PASS |
| Range "7 ngày" | one bucket per 10 minutes, mean of valid rows only | PASS |
| NaN to interop | NaN mapped to null before the JS call (Blazor interop cannot serialize NaN) | PASS |
| Console | no browser console errors during all range switches | PASS |

Final probe: 12 rows in `WeatherRecords`, 9 with a light value, 3 with
NULL (third-party payloads on the public demo topic without the
expected keys).

## 8. AI commentary (executed 2026-11-16)

Method: unit tests with xUnit (prompt builder, response parser, cache
and throttling against a fake HTTP handler); end-to-end test in the
browser with the running dashboard, using the team's OpenRouter key
and the `liquid/lfm-3-1.7b:free` model. Note: the Liquid free endpoint
mandates reasoning, so the request sets `max_tokens` 2500 (a real
answer uses ~1300 completion tokens in about 8 s; with the original
200 the reasoning consumed the whole budget and the content came back
empty — found and fixed during this verification).

| Check | Expected | Result |
|-------|----------|--------|
| Unit tests, AI service | 11/11 pass (prompt contents + grounding limit, parser, request targets OpenRouter with Bearer key and max_tokens 2500, no-key, NaN, error throttle, cache hit, condition change) | PASS |
| Panel renders | "Nhận xét AI" card with the "Nhận xét" button below the advice strip | PASS |
| Click without key | "Chưa cấu hình API key" shown, no HTTP call | PASS (unit-covered; earlier E2E run) |
| Real API answer | AI sentence replaces the placeholder after the spinner | PASS — answer grounded in the given readings, no light value, no invented data |
| Rate-limited call (429) | Failed state shows "Không tạo được nhận xét, thử lại sau.", page unaffected | PASS (hit the free-tier per-minute limit during testing) |
| Cache | three clicks with the same condition band produce only two HTTP POSTs (the third served from cache) | PASS (server log count) |
| Browser console | no errors during the clicks | PASS |
| Weighted advice untouched | badge and comment above the card unchanged | PASS |
