#include "Telemetry.h"
#include <ArduinoJson.h>
#include <time.h>
#include "Config.h"

size_t buildTelemetryJson(const WeatherReading& r, char* buf, size_t bufSize) {
  JsonDocument doc;
  doc["t"]  = round(r.temperature * 100) / 100.0;
  doc["h"]  = round(r.humidity * 100) / 100.0;
  doc["ws"] = round(r.windSpeed * 100) / 100.0;
  doc["wd"] = r.windDir;
  doc["r"]  = round(r.rain * 100) / 100.0;
  doc["lux"] = round(r.light * 100) / 100.0;
  doc["ok"] = r.sensorOk;
  doc["ts"] = (uint32_t)time(nullptr);
  return serializeJson(doc, buf, bufSize);
}
