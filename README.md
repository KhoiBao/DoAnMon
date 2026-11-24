# DoAnMon — Trạm thời tiết mini (mini weather station)

Simulated mini weather station for a farm. The firmware runs on an ESP32
inside the Wokwi simulator. It reads a DHT22 (temperature + humidity)
and three potentiometers (wind speed, wind direction, rain) and
publishes JSON telemetry over MQTT. A Blazor Server dashboard shows the telemetry, the user's location, today's weather and a 5-day forecast from the Open-Meteo weather API, applied lfm-3-1.7b - an LLM model made by LiqidAI, to read input data and advicing user.

Board Image:
<img width="1383" height="655" alt="image" src="https://github.com/user-attachments/assets/b50358f1-9e39-4ac7-964c-2464a08f90e4" />

Components used:
-  DHT22
-  ESP32devkit-V4
-  LDR module
-  3 potentiometers 

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
