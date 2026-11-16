#include "Sensors.h"
#include <DHT.h>
#include "Config.h"

static DHT dht(PIN_DHT_DATA, DHT22);

bool Sensors::begin() {
  dht.begin();
  return true;
}

// Sorts the window, drops the min and max, averages the rest.
// stable is false when the raw spread is too wide (floating or disconnected input).
uint32_t Sensors::readAdcTrimMean(uint8_t pin, bool& stable) const {
  uint16_t s[ADC_SAMPLES];
  for (int i = 0; i < ADC_SAMPLES; i++) {
    s[i] = analogRead(pin);
    delay(2);
  }
  for (int i = 1; i < ADC_SAMPLES; i++) {
    const uint16_t key = s[i];
    int j = i - 1;
    while (j >= 0 && s[j] > key) {
      s[j + 1] = s[j];
      j--;
    }
    s[j + 1] = key;
  }
  stable = (s[ADC_SAMPLES - 1] - s[0]) <= ADC_STABLE_SPREAD_MAX;
  uint32_t sum = 0;
  for (int i = 1; i < ADC_SAMPLES - 1; i++) sum += s[i];
  return sum / (ADC_SAMPLES - 2);
}

WeatherReading Sensors::read() {
  WeatherReading r;
  r.temperature = dht.readTemperature();
  r.humidity    = dht.readHumidity();
  const bool dhtOk = !isnan(r.temperature) && !isnan(r.humidity);

  bool okSpeed, okDir, okRain, okLight;
  const uint32_t adcSpeed = readAdcTrimMean(PIN_ADC_WIND_SPEED, okSpeed);
  const uint32_t adcDir   = readAdcTrimMean(PIN_ADC_WIND_DIR, okDir);
  const uint32_t adcRain  = readAdcTrimMean(PIN_ADC_RAIN, okRain);
  const uint32_t adcLight = readAdcTrimMean(PIN_ADC_LIGHT, okLight);

  r.windSpeed = (adcSpeed * WIND_SPEED_MAX_MS) / 4095.0F;
  r.windDir   = (int)((adcDir * 360UL) / 4095UL);
  if (r.windDir > 359) r.windDir = 359;
  r.rain      = (adcRain * RAIN_MAX_MMH) / 4095.0F;
  r.light     = (adcLight * LIGHT_MAX_LUX) / 4095.0F;

  r.sensorOk = dhtOk && okSpeed && okDir && okRain && okLight;
  if (!r.sensorOk) {
    Serial.print(F("Cam bien loi: DHT="));
    Serial.print(dhtOk ? 1 : 0);
    Serial.print(F(" gio="));
    Serial.print(okSpeed ? 1 : 0);
    Serial.print(F(" huong="));
    Serial.print(okDir ? 1 : 0);
    Serial.print(F(" mua="));
    Serial.print(okRain ? 1 : 0);
    Serial.print(F(" sang="));
    Serial.println(okLight ? 1 : 0);
  }
  return r;
}
