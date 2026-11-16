# ESP32 Firmware

## Files

| File | Role |
|------|------|
| `include/Config.h` | Pin numbers, scaling limits, MQTT settings |
| `include/secrets.example.h` | Template for `secrets.h` (tokens) |
| `src/main.cpp` | Setup and 5-second loop |
| `src/Sensors.cpp/.h` | BME280 reads and ADC averaging |
| `src/Telemetry.cpp/.h` | JSON payload builder |
| `src/NetworkService.cpp/.h` | WiFi and MQTT with reconnect backoff |

## Hardware wiring

| Device | Pin | ESP32 pin |
|--------|-----|-----------|
| BME280 SDA | SDA | GPIO 21 |
| BME280 SCL | SCL | GPIO 22 |
| BME280 SDO | — | GND (sets I2C address 0x76) |
| Potentiometer 1 (wind speed) | SIG | GPIO 34 |
| Potentiometer 2 (wind direction) | SIG | GPIO 35 |
| Potentiometer 3 (rain) | SIG | GPIO 32 |
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

Each ADC channel is sampled 10 times and averaged. This reduces noise.

## MQTT payload

The firmware publishes to topic `v1/devices/me/telemetry`. This topic is
the ThingsBoard telemetry endpoint, so the device also works with
ThingsBoard. The Access Token is the MQTT username.

```json
{
  "device": "tram-mini-01",
  "nhiet_do": 25.0,
  "do_am": 60.0,
  "ap_suat": 1013.25,
  "toc_do_gio": 12.5,
  "huong_gio": 180,
  "mua": 0,
  "sensor_ok": true,
  "ts": 12345
}
```

Pressure is in hPa. Wind speed is in m/s. `ts` is the millis() counter,
because the simulator has no real-time clock.

## Reconnect logic

`NetworkService` reconnects MQTT with exponential backoff. The delay
starts at 2 seconds and doubles to a maximum of 30 seconds. The MQTT LED
shows connection state. The status LED shows sensor health.

## Secrets

Copy `include/secrets.example.h` to `include/secrets.h`. Put your token
in `MQTT_USER`. The file is not committed. See [security.md](security.md).
