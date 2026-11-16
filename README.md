# DoAnMon — Trạm thời tiết mini (mini weather station)

Simulated mini weather station for a farm. The firmware runs on an ESP32
inside the Wokwi simulator. It reads a DHT22 (temperature + humidity)
and three potentiometers (wind speed, wind direction, rain) and
publishes JSON telemetry over MQTT. A Blazor Server dashboard shows the
telemetry, the user's location, today's weather and a 5-day forecast
from the Open-Meteo weather API.

Full documentation: [docs/README.md](docs/README.md).

## Quick start

```bash
pio run                                                    # build firmware
# start the simulator from VS Code (Wokwi extension)
dotnet run --project dashboard/TramThoiTiet.Dashboard      # dashboard
```

## Repository layout

| Path | Content |
|------|---------|
| `src/`, `include/` | ESP32 firmware (PlatformIO) |
| `diagram.json`, `wokwi.toml` | Wokwi simulator assets |
| `tools/` | Serial capture script |
| `docs/` | Documentation (ASD-STE100 style) |
| `dashboard/` | Blazor Server dashboard (.NET 8) |

## Security

MQTT tokens go in `include/secrets.h` (local only, never committed).
See `include/secrets.example.h` and `docs/security.md`.
