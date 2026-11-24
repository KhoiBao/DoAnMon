# ESP32 Firmware

## Files

| File | Role |
|------|------|
| `include/Config.h` | Pin numbers, scaling limits, MQTT settings |
| `include/secrets.example.h` | Template for `secrets.h` (tokens) |
| `src/main.cpp` | Setup and main loop (demo cycle or deep-sleep cycle) |
| `src/Sensors.cpp/.h` | DHT22 read and ADC averaging |
| `src/Telemetry.cpp/.h` | JSON payload builder |
| `src/NetworkService.cpp/.h` | WiFi and MQTT with reconnect backoff |

## Hardware wiring

| Device | Pin | ESP32 pin |
|--------|-----|-----------|
| DHT22 | SD (data) | GPIO 5 |
| DHT22 | VCC / GND | 3V3 / GND |
| Potentiometer 1 (wind speed) | SIG | GPIO 34 |
| Potentiometer 2 (wind direction) | SIG | GPIO 35 |
| Potentiometer 3 (rain) | SIG | GPIO 32 |
| LDR module (light) | AO | GPIO 33 |
| LED status | A | GPIO 2 (through 220 ohm) |
| LED MQTT | A | GPIO 4 (through 220 ohm) |

GPIO 34 and 35 are input-only pins. This is why the wind-speed and
wind-direction potentiometers use them.

## Sensor scaling

The firmware converts each ADC value (0..4095) to a physical unit:

| Channel | Range | Formula |
|---------|-------|---------|
| Wind speed | 0..30 m/s | `adc / 4095 * 30` |
| Wind direction | 0..359° | `adc / 4095 * 360` |
| Rain | 0..50 mm/h | `adc / 4095 * 50` |
| Light (LDR) | 0..1000 lux | `adc / 4095 * 1000` |

Each ADC channel is sampled 10 times. The firmware drops the highest
and lowest readings and averages the rest (trim mean). If the spread
(max - min) exceeds `ADC_STABLE_SPREAD_MAX` (3000), the input counts
as floating or disconnected and the channel is marked failed.

## Timing and power

`DEEP_SLEEP_ENABLE` selects the operating mode:

* `0` — demo mode: read and publish every 5 s.
* `1` — battery mode: one read/publish cycle, then deep sleep for
  `SLEEP_INTERVAL_S` (60 s). Waking from deep sleep reboots the ESP32,
  so the offline telemetry buffer does not survive sleep.

The repository default is `0` so the Wokwi/dashboard demo updates every
5 seconds. Set it to `1` for the battery-mode demonstration.

## MQTT payload

The firmware publishes to topic `v1/devices/me/telemetry`. This topic is
the ThingsBoard telemetry endpoint, so the device also works with
ThingsBoard. The Access Token is the MQTT username.

```json
{
  "t": 25.34,
  "h": 60.0,
  "ws": 12.5,
  "wd": 180,
  "r": 0,
  "lux": 420.5,
  "ok": true,
  "ts": 1794846520
}
```

Short keys keep the payload small for the offline ring buffer. `t` is
temperature in °C, `h` is humidity in %, `ws` is wind speed in m/s,
`wd` is wind direction in degrees, `r` is rain in mm/h, and `lux` is
light intensity from the LDR module (approximate, linear map).
`ok` is true when all sensors read OK. `ts` is the Unix epoch in
seconds from NTP (GMT+7); a value near 0 means the clock was not
synced yet and the dashboard falls back to arrival time.

## Reconnect logic

`NetworkService` reconnects MQTT with exponential backoff. The delay
starts at 2 seconds and doubles to a maximum of 30 seconds. The MQTT LED
shows connection state. The status LED shows sensor health.

## Secrets

Copy `include/secrets.example.h` to `include/secrets.h`. Put your token
in `MQTT_USER`. The file is not committed. See [security.md](security.md).
