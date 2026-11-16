#include "Sensors.h"
#include <Wire.h>
#include <Adafruit_Sensor.h>
#include <Adafruit_BME280.h>
#include "Config.h"

static Adafruit_BME280 bme;

bool Sensors::begin() {
  Wire.begin(PIN_I2C_SDA, PIN_I2C_SCL);
  if (!bme.begin(BME280_I2C_ADDR)) {
    return false;
  }
  bme.setSampling(Adafruit_BME280::MODE_NORMAL,
                  Adafruit_BME280::SAMPLING_X2,
                  Adafruit_BME280::SAMPLING_X16,
                  Adafruit_BME280::SAMPLING_X16,
                  Adafruit_BME280::FILTER_X16,
                  Adafruit_BME280::STANDBY_MS_500);
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
  r.temperature = bme.readTemperature();
  r.humidity    = bme.readHumidity();
  r.pressure    = bme.readPressure() / 100.0F;
  r.sensorOk    = !isnan(r.temperature) && !isnan(r.humidity) && !isnan(r.pressure);

  uint32_t adcSpeed = readAdcAverage(PIN_ADC_WIND_SPEED);
  uint32_t adcDir   = readAdcAverage(PIN_ADC_WIND_DIR);
  uint32_t adcRain  = readAdcAverage(PIN_ADC_RAIN);

  r.windSpeed = (adcSpeed * WIND_SPEED_MAX_MS) / 4095.0F;
  r.windDir   = (int)((adcDir * 360UL) / 4095UL);
  if (r.windDir > 359) r.windDir = 359;
  r.rain = (adcRain * RAIN_MAX_MMH) / 4095.0F;

  return r;
}
