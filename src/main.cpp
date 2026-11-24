#include <Arduino.h>
#include <WiFi.h>
#include "Config.h"
#include "Sensors.h"
#include "Telemetry.h"
#include "NetworkService.h"
#if DEEP_SLEEP_ENABLE
  #include "esp_sleep.h"
#endif

static Sensors sensors;
static NetworkService network;

void setup() {
  Serial.begin(115200);
  pinMode(PIN_LED_STATUS, OUTPUT);
  pinMode(PIN_LED_MQTT, OUTPUT);
  digitalWrite(PIN_LED_STATUS, LOW);
  digitalWrite(PIN_LED_MQTT, LOW);

  Serial.println(F("Tram thoi tiet mini - khoi dong"));
  Serial.println(F("Num xoay: GPIO34 = gio (m/s), GPIO35 = huong gio (do),"));
  Serial.println(F("          GPIO32 = mua (mm/h)"));
  Serial.println(F("DHT22 (GPIO5) = nhiet do + do am khong khi"));
  Serial.println(F("LDR (GPIO33) = cuong do anh sang (lux)"));
  sensors.begin();

  network.begin();
  // The Wokwi gateway can need a few seconds. Wait for MQTT here so the
  // first sample is published instead of being buffered/lost in a deep-sleep
  // build (the buffer does not survive deep sleep).
  const unsigned long startedMs = millis();
  while (!network.mqttConnected() && millis() - startedMs < 15000UL) {
    network.loop();
    delay(200);
  }
}

void loop() {
  network.loop();

  WeatherReading r = sensors.read();
  digitalWrite(PIN_LED_STATUS, r.sensorOk ? HIGH : LOW);
  digitalWrite(PIN_LED_MQTT, network.mqttConnected() ? HIGH : LOW);

  char payload[128];
  buildTelemetryJson(r, payload, sizeof(payload));

  if (network.publish(MQTT_TOPIC_TELEMETRY, payload)) {
    Serial.print(F("Gui: "));
  } else {
    Serial.print(F("Luu buffer: "));
  }
  Serial.println(payload);

#if DEEP_SLEEP_ENABLE
  digitalWrite(PIN_LED_STATUS, LOW);
  digitalWrite(PIN_LED_MQTT, LOW);
  Serial.println(F("Che do pin: ngu sau..."));
  Serial.flush();
  esp_sleep_enable_timer_wakeup((uint64_t)SLEEP_INTERVAL_S * 1000000ULL);
  esp_deep_sleep_start();
#else
  delay(SAMPLE_INTERVAL_MS);
#endif
}
