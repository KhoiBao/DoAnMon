#pragma once
#include <Arduino.h>

// ======================= Hardware pins =======================
// DHT22 for air temperature + humidity (single-wire digital)
#define PIN_DHT_DATA       5

// Analog inputs (potentiometers on Wokwi, real sensors later)
#define PIN_ADC_WIND_SPEED 34   // 0..30 m/s
#define PIN_ADC_WIND_DIR   35   // 0..359 degrees
#define PIN_ADC_RAIN       32   // 0..50 mm/h

// Status LEDs
#define PIN_LED_STATUS     2    // ON when all sensors read OK
#define PIN_LED_MQTT       4    // ON when connected to broker

// ======================= Sampling =======================
#define SAMPLE_INTERVAL_MS 5000UL
#define ADC_SAMPLES        10   // averaged per reading

// ======================= Weather scaling =======================
#define WIND_SPEED_MAX_MS  30.0f
#define RAIN_MAX_MMH       50.0f

// ======================= MQTT =======================
// ThingsBoard-compatible: Access Token as MQTT username, empty password.
// Topic is the ThingsBoard telemetry endpoint.
#define MQTT_DEFAULT_SERVER   "broker.hivemq.com"
#define MQTT_DEFAULT_PORT     1883
#define MQTT_TOPIC_TELEMETRY  "v1/devices/me/telemetry"
#define MQTT_RECONNECT_MIN_MS 2000UL
#define MQTT_RECONNECT_MAX_MS 30000UL

// Client identifier (kept short; unique per workshop not required for public demo broker)
#define MQTT_CLIENT_ID        "tram-thoi-tiet-mini"
#define DEVICE_NAME           "tram-mini-01"
