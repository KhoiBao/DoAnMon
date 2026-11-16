# Virtual BME280 Chip

## Why a custom chip

Wokwi has no BME280 part. This project implements one as a Wokwi custom
chip (`bme280.chip.c` compiled to `bme280.chip.wasm`). The chip behaves
like a real BME280 on the I2C bus:

* Chip ID register 0xD0 returns 0x60.
* Calibration registers 0x88..0x9F and 0xA1, 0xE1..0xE7 return the
  virtual calibration values.
* Data registers 0xF7..0xFE return raw 20-bit pressure, 20-bit
  temperature and 16-bit humidity samples.
* The Adafruit BME280 library reads these registers without changes.
* Write access works for CTRL_MEAS (0xF4), CONFIG (0xF5) and the soft
  reset (0xB6 to 0xE0).

## The math problem

The Adafruit library applies the Bosch compensation formulas to the raw
ADC values. The chip must invert these formulas. It receives slider
values (temperature, pressure, humidity) and must produce ADC values
that give back those values after compensation.

The forward model uses bisection. For each measurement, the chip searches
for the smallest ADC value where the compensation formula reaches the
target. The compensation formulas are monotone in the ADC domain, so
bisection is correct and fast (20 iterations over 20-bit values).

## Virtual calibration

The calibration constants were chosen so that the temperature curve is
smooth and monotone:

| Register | Value | Register | Value |
|----------|-------|----------|-------|
| dig_T1 | 20000 | dig_H1 | 75 |
| dig_T2 | 10000 | dig_H2 | 362 |
| dig_T3 | 0 | dig_H3 | 0 |
| dig_P1 | 36477 | dig_H4 | 325 |
| dig_P2 | −10685 | dig_H5 | 50 |
| dig_P3 | 3024 | dig_H6 | 30 |
| dig_P4 | 2855 | | |
| dig_P5 | 140 | | |
| dig_P6 | −7 | | |
| dig_P7 | 15500 | | |
| dig_P8 | −14600 | | |
| dig_P9 | 6000 | | |

The 20-bit temperature ADC covers −38.15..86.84 °C with this
calibration. The humidity ADC reaches 100 %RH below full scale.

## H4/H5 register packing

The BME280 stores two 12-bit signed values in three bytes:

```
dig_H4 = (E4 << 4) | (E5 & 0x0F)
dig_H5 = (E5 >> 4) | (E6 << 4)
```

The chip writes:
* E4 = `dig_H4 >> 4`
* E5 = `(H5 low nibble << 4) | (H4 low nibble)`
* E6 = `dig_H5 >> 4`

## Verification

`tools/verify_bme280_model.py` implements both sides — the chip forward
model and the library compensation formulas — with the same integer
arithmetic. It checks the round trip:

| Quantity | Max error | Limit from ADC step |
|----------|-----------|---------------------|
| Temperature | 0.0000 °C | 0.0002 °C |
| Pressure | 0.164 Pa | 0.18 Pa (1 ADC LSB ≈ 0.17 Pa) |
| Humidity | 0.0098 %RH | 0.01 %RH |

The pressure error is set by the 20-bit ADC resolution of a real
BME280 (about 0.17 Pa per step). The model reaches this limit.

## Chip interface

| Pin | Role |
|-----|------|
| VCC | Power. The chip answers only when VCC is high. |
| GND | Ground |
| SCL | I2C clock |
| SDA | I2C data |

The I2C address is 0x76. The sliders on the chip control TEMP (°C),
HUM (%RH) and PRES (hPa).
