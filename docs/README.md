# Mini Weather Station — Documentation

This project is a simulated mini weather station for a farm. It runs on
Wokwi. It sends telemetry over MQTT. A .NET dashboard shows the data and
compares it with the Open-Meteo weather API.

Air temperature and humidity come from a DHT22. Three potentiometers
stand in for wind speed, wind direction and rain. An LDR module on
GPIO 33 gives the light intensity. All are built-in Wokwi parts, so no
custom chip is needed.

The dashboard stores every sample in SQLite and shows history charts
for four time ranges, plus a weighted advice line based on temperature
and light. An optional AI comment (OpenRouter API with a free Liquid
model, generated only when the
user clicks "Nhận xét") phrases the same readings in natural language;
without an API key the dashboard works as before.

## Contents

| Document | What it covers |
|----------|----------------|
| [tong-hop-du-an.md](tong-hop-du-an.md) | All-in-one project synthesis for slides and the Word report (Vietnamese) |
| [firmware.md](firmware.md) | ESP32 firmware: sensors, scaling, MQTT payload |
| [dashboard.md](dashboard.md) | The Blazor Server dashboard: storage, history, advice |
| [verification.md](verification.md) | Test procedures and results |
| [security.md](security.md) | Token and secret handling |
| [report-doan.md](report-doan.md) | Paste-ready Vietnamese section for the course report |

## System overview

```
[Wokwi simulator]
  DHT22 (temp + humidity)  --GPIO-->  ESP32 firmware
  3 potentiometers         --ADC-->     |
  LDR (light)              --ADC-->     |
                                  MQTT JSON per cycle
                                      v
                     broker.hivemq.com (public demo broker)
                                      v
                     [Blazor dashboard]  <-->  Open-Meteo API
                                |            (reference weather)
                          SQLite (tramthoitiet.db)
```

The firmware reads air temperature and humidity from the DHT22. Three
potentiometers stand in for wind speed, wind direction and rain, and an
LDR module gives the light intensity. The firmware publishes one JSON
message per cycle (5 s in demo mode, 60 s in battery mode). The
dashboard subscribes to the MQTT topic, stores each sample in SQLite,
and shows live and historical charts. It also asks the
Open-Meteo API for current weather at a chosen location. The page shows
both values and the difference.

## Quick start

1. Install PlatformIO and the Wokwi VS Code extension (and the .NET 8 SDK for the dashboard).
2. Build the firmware: `pio run`.
3. Start the simulator with the Wokwi extension.
4. Start the dashboard: `dotnet run --project dashboard/TramThoiTiet.Dashboard`.
5. Open `http://localhost:5000` (or the URL that the console shows).

## If the dashboard shows only the forecast

The green MQTT badge shows the **dashboard's** connection to the broker. It
does not prove that the ESP32 is online. The station cards, advice, AI card,
table and chart are rendered only after the first telemetry message arrives.

1. Start the Wokwi simulator and open the serial monitor (115200 baud).
2. Look for `WiFi: da ket noi` and then `Gui: {...}` every 5 seconds.
3. `WiFi: KHONG ket noi duoc` means the simulator has no network. Restart it
   and check that the `Wokwi-GUEST` network is available.
4. `Luu buffer: {...}` means WiFi is up but the MQTT broker connection failed.
   The dashboard and the ESP32 must use the same broker and topic.
5. With `DEEP_SLEEP_ENABLE 0` (demo default) the firmware publishes every
   5 seconds. With `1` it publishes once and sleeps for 60 seconds.
