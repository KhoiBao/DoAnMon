#pragma once
#include <Arduino.h>
#include <WiFiClient.h>
#include <PubSubClient.h>

// WiFi + MQTT connection manager with exponential backoff reconnect.
class NetworkService {
public:
  void begin();
  void loop();                       // call every iteration; keeps connections alive
  bool mqttConnected();
  bool publish(const char* topic, const char* payload);
private:
  void ensureWiFi();
  void ensureMqtt();
  WiFiClient _wifiClient;
  PubSubClient _mqtt{_wifiClient};
  unsigned long _nextMqttRetry = 0;
  unsigned long _mqttBackoff   = 0;
};
