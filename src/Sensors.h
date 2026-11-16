#pragma once
#include <Arduino.h>

struct WeatherReading {
  float temperature;   // degC, from DHT22
  float humidity;      // %RH, from DHT22
  float windSpeed;     // m/s, from ADC
  int   windDir;       // degrees 0..359, from ADC
  float rain;          // mm/h, from ADC
  float light;         // lux 0..LIGHT_MAX_LUX, approximated from LDR module
  bool  sensorOk;      // true when DHT22 read succeeded and ADC channels are stable
};

class Sensors {
public:
  bool begin();                    // prepares the DHT22
  WeatherReading read();           // one trimmed-mean sample set
private:
  uint32_t readAdcTrimMean(uint8_t pin, bool& stable) const;
};
