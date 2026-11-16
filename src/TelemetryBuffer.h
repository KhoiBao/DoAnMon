#pragma once
#include <Arduino.h>

// Fixed-size ring buffer for telemetry payloads published while MQTT is offline.
// Overwrites the oldest entry when full and counts it as dropped.
class TelemetryBuffer {
public:
  static constexpr size_t CAPACITY = 128;
  static constexpr size_t PAYLOAD_MAX = 128;

  void push(const char* payload);
  bool pop(char* out, size_t outSize);
  bool empty() const { return _count == 0; }
  uint32_t dropped() const { return _dropped; }
  void resetDropped() { _dropped = 0; }

private:
  char _entries[CAPACITY][PAYLOAD_MAX];
  size_t _head = 0;
  size_t _count = 0;
  uint32_t _dropped = 0;
};
