#include "Telemetry.h"
#include <ArduinoJson.h>
#include "Config.h"

size_t buildTelemetryJson(const WeatherReading& r, char* buf, size_t bufSize) {
  JsonDocument doc;
  doc["device"]       = DEVICE_NAME;
  doc["nhiet_do"]     = round(r.temperature * 100) / 100.0;
  doc["do_am"]        = round(r.humidity * 100) / 100.0;
  doc["toc_do_gio"]   = round(r.windSpeed * 100) / 100.0;
  doc["huong_gio"]    = r.windDir;
  doc["mua"]          = round(r.rain * 100) / 100.0;
  doc["sensor_ok"]    = r.sensorOk;
  doc["ts"]           = millis();
  return serializeJson(doc, buf, bufSize);
}
