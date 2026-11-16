# Mini Weather Station — Documentation

This project is a simulated mini weather station for a farm. It runs on
Wokwi. It sends telemetry over MQTT. A .NET dashboard shows the data and
compares it with the Open-Meteo weather API.

## Contents

| Document | What it covers |
|----------|----------------|
| [firmware.md](firmware.md) | ESP32 firmware: sensors, scaling, MQTT payload |
| [simulator-bme280.md](simulator-bme280.md) | The virtual BME280 chip and its math |
| [dashboard.md](dashboard.md) | The Blazor Server dashboard |
| [verification.md](verification.md) | Test procedures and results |
| [security.md](security.md) | Token and secret handling |

## System overview

```
[Wokwi simulator]
  BME280 chip (custom)  --I2C-->  ESP32 firmware
  3 potentiometers      --ADC-->      |
                                  MQTT JSON every 5 s
                                      v
                     broker.hivemq.com (public demo broker)
                                      v
                     [Blazor dashboard]  <-->  Open-Meteo API
```

The firmware reads air temperature, air humidity and pressure from the
BME280. Three potentiometers stand in for wind speed, wind direction and
rain. The firmware publishes one JSON message every 5 seconds. The
dashboard subscribes to the MQTT topic. It also asks the Open-Meteo API
for current weather at a chosen location. The page shows both values and
the difference.

## Quick start

1. Install PlatformIO, the Wokwi VS Code extension and the .NET 8 SDK.
2. Build the firmware: `pio run`.
3. Compile the chip: `wokwi-cli chip compile bme280.chip.c -o bme280.chip.wasm`.
4. Start the simulator with the Wokwi extension.
5. Start the dashboard: `dotnet run --project dashboard/TramThoiTiet.Dashboard`.
6. Open `http://localhost:5000` (or the URL that the console shows).
