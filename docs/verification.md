# Verification

ASD-STE100 note: this document records executed tests only. The result
columns stay empty until a test runs on this machine.

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

Command: `wokwi-cli chip compile bme280.chip.c -o bme280.chip.wasm`

| Step | Result |
|------|--------|
| Compile | (pending — run before the report) |

## 4. Simulator end-to-end

Procedure:

1. Start the simulator with the Wokwi extension.
2. Capture the serial output (RFC2217 on port 4000 or the serial monitor).
3. Check the first JSON message: temperature 25 °C, humidity 60 %,
   pressure 1013.25 hPa at default slider values.
4. Change each slider and check that the published value follows.

| Check | Expected | Result |
|-------|----------|--------|
| Chip ID detection | no "LOI: Khong tim thay BME280" | (pending) |
| Default values | 25.0 / 60.0 / 1013.25 | (pending) |
| Slider tracking | published value follows sliders | (pending) |
| MQTT LED | on when connected | (pending) |

## 5. Dashboard end-to-end

Procedure:

1. Run `dotnet run --project dashboard/TramThoiTiet.Dashboard`.
2. Open the page. The MQTT badge must turn green within 10 s.
3. Watch the comparison table fill with local and Open-Meteo values.
4. Press "Vị trí hiện tại" and confirm the coordinates change.

| Check | Expected | Result |
|-------|----------|--------|
| MQTT badge | green after connect | (pending) |
| Telemetry table | values from the simulator | (pending) |
| Open-Meteo fetch | values near the chosen location | (pending) |
| History chart | one point per 5 s | (pending) |
| Geolocation | coordinates set from browser | (pending) |
