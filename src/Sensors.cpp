#include "Sensors.h"
#include <DHT.h>
#include "Config.h"

static DHT dht(PIN_DHT_DATA, DHT22);

bool Sensors::begin() {
  dht.begin();
  return true;
}

uint32_t Sensors::readAdcAverage(uint8_t pin) const {
  uint32_t sum = 0;
  for (int i = 0; i < ADC_SAMPLES; i++) {
    sum += analogRead(pin);
    delay(2);
  }
  return sum / ADC_SAMPLES;
}

WeatherReading Sensors::read() {
  WeatherReading r;
  r.temperature = dht.readTemperature();
  r.humidity    = dht.readHumidity();
  r.sensorOk    = !isnan(r.temperature) && !isnan(r.humidity);

  uint32_t adcSpeed = readAdcAverage(PIN_ADC_WIND_SPEED);
  uint32_t adcDir   = readAdcAverage(PIN_ADC_WIND_DIR);
  uint32_t adcRain  = readAdcAverage(PIN_ADC_RAIN);

  r.windSpeed = (adcSpeed * WIND_SPEED_MAX_MS) / 4095.0F;
  r.windDir   = (int)((adcDir * 360UL) / 4095UL);
  if (r.windDir > 359) r.windDir = 359;
  r.rain      = (adcRain * RAIN_MAX_MMH) / 4095.0F;

  return r;
}
