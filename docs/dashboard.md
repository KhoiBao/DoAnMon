# Blazor Server Dashboard

## Stack

* .NET 8, Blazor Server (interactive server render mode)
* MQTTnet 4.3 for MQTT subscribe
* Chart.js 4 (CDN) for history charts
* Open-Meteo API for reference weather

The dashboard project is at `dashboard/TramThoiTiet.Dashboard/`.

## Files

| File | Role |
|------|------|
| `Program.cs` | DI setup, hosted service, HTTP pipeline |
| `appsettings.json` | Broker, topic, default location, API cache |
| `Services/WeatherStore.cs` | Latest sample, 200-sample ring buffer, change event |
| `Services/MqttReceiverService.cs` | Background MQTT client with reconnect loop |
| `Services/OpenMeteoService.cs` | API client with a 10-minute cache |
| `Components/Pages/Index.razor` | The comparison page |
| `wwwroot/js/app.js` | Geolocation interop and the history chart |

## Data flow

1. The ESP32 publishes JSON to `v1/devices/me/telemetry` every 5 s.
2. `MqttReceiverService` parses each message and adds a `WeatherSample`
   to `WeatherStore`.
3. `Index.razor` listens to the `WeatherStore.Changed` event, updates the
   page and pushes the history to the Chart.js chart.
4. The Open-Meteo client fetches current weather for the chosen
   coordinates. Results are cached for 10 minutes per location.

## Comparison page

The page shows:

* Connection badges for MQTT and sensor health.
* The user's location as a place name (browser geolocation plus reverse
  geocoding via OpenStreetMap Nominatim). If the user denies the location
  request, the configured default location is used.
* A comparison table: local value, API value and the difference for
  temperature, humidity, wind speed and wind direction.
* Wind direction as compass labels (B, ĐB, Đ, ĐN, N, TN, T, TB).
* Rain (local only — the API column shows a dash).
* A history chart with temperature, humidity, wind and rain.
* Today's forecast and a 5-day forecast (min/max temperature, weather
  state, rain amount and probability) from Open-Meteo.

Weather states come from the WMO weather code, translated to Vietnamese
("Trời quang", "Mưa phùn nhẹ", "Mưa rào vừa", "Dông"...).

## Location

On page load the dashboard asks the browser for the current position.
The coordinates go to Open-Meteo and to the reverse geocoder, which
returns a place name at street level (for example "Đường Tây Thạnh,
Khu phố 9, Phường Tây Thạnh, Thành phố Hồ Chí Minh"). The button
"Lấy vị trí hiện tại" repeats the request.

## Units

| Value | Local | Open-Meteo | After conversion |
|-------|-------|------------|------------------|
| Temperature | °C | °C | as-is |
| Humidity | % | % | as-is |
| Wind speed | m/s | km/h | divided by 3.6 |
| Wind direction | degrees | degrees | as-is |

Pressure is not part of the system.

## Run

```
dotnet run --project dashboard/TramThoiTiet.Dashboard
```

The console shows the listen URL. Open it in a browser.
