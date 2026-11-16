# DoAnMon — Trạm thời tiết mini (mini weather station)

Simulated mini weather station for a farm. The firmware runs on an ESP32
inside the Wokwi simulator. It reads a virtual BME280 sensor plus three
potentiometers (wind speed, wind direction, rain) and publishes JSON
telemetry over MQTT. A Blazor Server dashboard shows the telemetry and
compares it with the Open-Meteo weather API for a chosen location.

Full documentation: [docs/README.md](docs/README.md).

## Quick start

```bash
pio run                                                    # build firmware
wokwi-cli chip compile bme280.chip.c -o bme280.chip.wasm   # compile chip
# start the simulator from VS Code (Wokwi extension)
dotnet run --project dashboard/TramThoiTiet.Dashboard      # dashboard
```

## Repository layout

| Path | Content |
|------|---------|
| `src/`, `include/` | ESP32 firmware (PlatformIO) |
| `bme280.chip.c/.json` | Wokwi custom BME280 chip |
| `diagram.json`, `wokwi.toml` | Wokwi simulator assets |
| `tools/` | Model verification and serial capture scripts |
| `docs/` | Documentation (ASD-STE100 style) |
| `dashboard/` | Blazor Server dashboard (.NET 8) |

## Security

MQTT tokens go in `include/secrets.h` (local only, never committed).
See `include/secrets.example.h` and `docs/security.md`.
