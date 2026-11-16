#include "NetworkService.h"
#include <WiFi.h>
#include <time.h>
#include "Config.h"

#if __has_include("secrets.h")
  #include "secrets.h"
#else
  #define MQTT_USER "demo"
  #define MQTT_PASS ""
#endif

#ifndef WIFI_SSID
  #define WIFI_SSID "Wokwi-GUEST"
  #define WIFI_PASS ""
#endif

void NetworkService::begin() {
  _mqtt.setServer(MQTT_DEFAULT_SERVER, MQTT_DEFAULT_PORT);
  _mqtt.setKeepAlive(30);
  _mqtt.setSocketTimeout(10);
}

void NetworkService::ensureWiFi() {
  if (WiFi.status() == WL_CONNECTED) return;
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  for (int i = 0; i < 30 && WiFi.status() != WL_CONNECTED; i++) {
    delay(200);
  }
  configTime(TZ_OFFSET_SEC, 0, NTP_SERVER);
}

void NetworkService::ensureMqtt() {
  if (_mqtt.connected()) return;
  unsigned long now = millis();
  if (now < _nextMqttRetry) return;

  if (_mqtt.connect(MQTT_CLIENT_ID, MQTT_USER, MQTT_PASS)) {
    _mqttBackoff = MQTT_RECONNECT_MIN_MS;
  } else {
    _mqttBackoff = min(_mqttBackoff * 2, MQTT_RECONNECT_MAX_MS);
    if (_mqttBackoff < MQTT_RECONNECT_MIN_MS) _mqttBackoff = MQTT_RECONNECT_MIN_MS;
    _nextMqttRetry = now + _mqttBackoff;
  }
}

void NetworkService::loop() {
  ensureWiFi();
  ensureMqtt();
  _mqtt.loop();
  if (_mqtt.connected() && !_buffer.empty()) {
    drainBuffer();
  }
}

bool NetworkService::mqttConnected() {
  return _mqtt.connected();
}

bool NetworkService::publish(const char* topic, const char* payload) {
  if (!_mqtt.connected()) {
    _buffer.push(payload);
    return false;
  }
  if (_mqtt.publish(topic, payload)) return true;
  _buffer.push(payload);
  return false;
}

void NetworkService::drainBuffer() {
  char payload[128];
  uint32_t synced = 0;
  while (!_buffer.empty()) {
    if (!_buffer.pop(payload, sizeof(payload))) break;
    if (!_mqtt.publish(MQTT_TOPIC_TELEMETRY, payload)) {
      _buffer.push(payload);
      break;
    }
    synced++;
    delay(20);
  }
  const uint32_t lost = _buffer.dropped();
  _buffer.resetDropped();
  if (synced == 0) return;
  Serial.print(F("Dong bo lai "));
  Serial.print(synced);
  Serial.print(F(" mau tu buffer"));
  if (lost > 0) {
    Serial.print(F(", mat "));
    Serial.print(lost);
    Serial.print(F(" mau do buffer day"));
  }
  Serial.println();
}
