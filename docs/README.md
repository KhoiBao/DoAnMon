# Mini Weather Station — Documentation

This project is a simulated mini weather station for a farm. It runs on
Wokwi. It sends telemetry over MQTT. A .NET dashboard shows the data and
compares it with the Open-Meteo weather API.

Air temperature and humidity come from a DHT22. Three potentiometers
stand in for wind speed, wind direction and rain. All are built-in
Wokwi parts, so no custom chip is needed.

## Contents

| Document | What it covers |
|----------|----------------|
| [firmware.md](firmware.md) | ESP32 firmware: sensors, scaling, MQTT payload |
| [dashboard.md](dashboard.md) | The Blazor Server dashboard |
| [verification.md](verification.md) | Test procedures and results |
| [security.md](security.md) | Token and secret handling |

## System overview

```
[Wokwi simulator]
  DHT22 (temp + humidity)  --GPIO-->  ESP32 firmware
  3 potentiometers         --ADC-->     |
  (wind, wind dir, rain)                |
                                  MQTT JSON every 5 s
                                      v
                     broker.hivemq.com (public demo broker)
                                      v
                     [Blazor dashboard]  <-->  Open-Meteo API
```

The firmware reads air temperature and humidity from the DHT22. Three
potentiometers stand in for wind speed, wind direction and rain. The
firmware publishes one JSON message every 5 seconds. The dashboard subscribes to the MQTT topic. It also asks the
Open-Meteo API for current weather at a chosen location. The page shows
both values and the difference.

## Quick start

1. Install PlatformIO and the Wokwi VS Code extension (and the .NET 8 SDK for the dashboard).
2. Build the firmware: `pio run`.
3. Start the simulator with the Wokwi extension.
4. Start the dashboard: `dotnet run --project dashboard/TramThoiTiet.Dashboard`.
5. Open `http://localhost:5000` (or the URL that the console shows).
