#pragma once
#include <Arduino.h>

struct WeatherReading {
  float temperature;   // degC, from DHT22
  float humidity;      // %RH, from DHT22
  float windSpeed;     // m/s, from ADC
  int   windDir;       // degrees 0..359, from ADC
  float rain;          // mm/h, from ADC
  bool  sensorOk;      // true when DHT22 read succeeded
};

class Sensors {
public:
  bool begin();                    // prepares the DHT22
  WeatherReading read();           // one averaged sample set
private:
  uint32_t readAdcAverage(uint8_t pin) const;
};
