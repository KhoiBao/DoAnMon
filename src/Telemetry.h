#pragma once
#include <Arduino.h>
#include "Sensors.h"

// Serializes one WeatherReading into the ThingsBoard telemetry JSON payload.
// Returns the number of bytes written (excluding the terminating NUL).
size_t buildTelemetryJson(const WeatherReading& r, char* buf, size_t bufSize);
