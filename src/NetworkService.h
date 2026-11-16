#pragma once
#include <Arduino.h>
#include <WiFiClient.h>
#include <PubSubClient.h>
#include "TelemetryBuffer.h"

// WiFi + MQTT connection manager with exponential backoff reconnect.
// Publishes that fail while offline are kept in the ring buffer and
// replayed oldest-first once the connection is back.
class NetworkService {
public:
  void begin();
  void loop();                       // call every iteration; keeps connections alive
  bool mqttConnected();
  bool publish(const char* topic, const char* payload);  // false = buffered for replay
private:
  void ensureWiFi();
  void ensureMqtt();
  void drainBuffer();
  WiFiClient _wifiClient;
  PubSubClient _mqtt{_wifiClient};
  TelemetryBuffer _buffer;
  unsigned long _nextMqttRetry = 0;
  unsigned long _mqttBackoff   = 0;
};
