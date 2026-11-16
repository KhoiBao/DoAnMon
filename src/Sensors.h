#pragma once
#include <Arduino.h>

struct WeatherReading {
  float temperature;   // degC, from BME280
  float humidity;      // %RH, from BME280
  float pressure;      // hPa, from BME280
  float windSpeed;     // m/s, from ADC
  int   windDir;       // degrees 0..359, from ADC
  float rain;          // mm/h, from ADC
  bool  sensorOk;      // true when BME280 read succeeded
};

class Sensors {
public:
  bool begin();                    // returns false if BME280 not found
  WeatherReading read();           // one averaged sample set
private:
  uint32_t readAdcAverage(uint8_t pin) const;
};
