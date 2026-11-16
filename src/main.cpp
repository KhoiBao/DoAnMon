#include <Arduino.h>
#include <WiFi.h>
#include "Config.h"
#include "Sensors.h"
#include "Telemetry.h"
#include "NetworkService.h"

static Sensors sensors;
static NetworkService network;

void setup() {
  Serial.begin(115200);
  pinMode(PIN_LED_STATUS, OUTPUT);
  pinMode(PIN_LED_MQTT, OUTPUT);
  digitalWrite(PIN_LED_STATUS, LOW);
  digitalWrite(PIN_LED_MQTT, LOW);

  Serial.println(F("Tram thoi tiet mini - khoi dong"));
  if (!sensors.begin()) {
    Serial.println(F("LOI: Khong tim thay BME280 tai dia chi 0x76"));
    // Keep running so the dashboard can see sensor_ok=false telemetry.
  }

  network.begin();
}

void loop() {
  network.loop();

  WeatherReading r = sensors.read();
  digitalWrite(PIN_LED_STATUS, r.sensorOk ? HIGH : LOW);
  digitalWrite(PIN_LED_MQTT, network.mqttConnected() ? HIGH : LOW);

  char payload[384];
  size_t len = buildTelemetryJson(r, payload, sizeof(payload));

  if (network.publish(MQTT_TOPIC_TELEMETRY, payload)) {
    Serial.print(F("Gui: "));
  } else {
    Serial.print(F("Gui that bai: "));
  }
  Serial.println(payload);

  delay(SAMPLE_INTERVAL_MS);
}
