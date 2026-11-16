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
