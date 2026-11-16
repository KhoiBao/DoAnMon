#include <stdio.h>
#include <stdlib.h>
#include "wokwi-api.h"

// ===================== Register map (BME280 datasheet) =====================
#define REG_CHIP_ID      0xD0
#define REG_RESET        0xE0
#define REG_STATUS       0xF3
#define REG_CTRL_MEAS    0xF4
#define REG_CONFIG       0xF5
#define REG_PRES_MSB     0xF7
#define REG_PRES_LSB     0xF8
#define REG_PRES_XLSB    0xF9
#define REG_TEMP_MSB     0xFA
#define REG_TEMP_LSB     0xFB
#define REG_TEMP_XLSB    0xFC
#define REG_HUM_MSB      0xFD
#define REG_HUM_LSB      0xFE

// ===================== Calibration registers =====================
#define REG_DIG_T1_L 0x88
#define REG_DIG_T1_H 0x89
#define REG_DIG_T2_L 0x8A
#define REG_DIG_T2_H 0x8B
#define REG_DIG_T3_L 0x8C
#define REG_DIG_T3_H 0x8D
#define REG_DIG_P1_L 0x8E
#define REG_DIG_P1_H 0x8F
#define REG_DIG_P2_L 0x90
#define REG_DIG_P2_H 0x91
#define REG_DIG_P3_L 0x92
#define REG_DIG_P3_H 0x93
#define REG_DIG_P4_L 0x94
#define REG_DIG_P4_H 0x95
#define REG_DIG_P5_L 0x96
#define REG_DIG_P5_H 0x97
#define REG_DIG_P6_L 0x98
#define REG_DIG_P6_H 0x99
#define REG_DIG_P7_L 0x9A
#define REG_DIG_P7_H 0x9B
#define REG_DIG_P8_L 0x9C
#define REG_DIG_P8_H 0x9D
#define REG_DIG_P9_L 0x9E
#define REG_DIG_P9_H 0x9F
#define REG_DIG_H1   0xA1
#define REG_DIG_H2_L 0xE1
#define REG_DIG_H2_H 0xE2
#define REG_DIG_H3   0xE3
#define REG_DIG_H4   0xE4
#define REG_DIG_H5   0xE5
#define REG_DIG_H6   0xE7

// Virtual chip calibration (verified against the Adafruit compensation
// formulas by tools/verify_bme280_model.py)
#define DIG_T1 20000
#define DIG_T2 10000
#define DIG_T3 0
#define DIG_P1 36477
#define DIG_P2 (-10685)
#define DIG_P3 3024
#define DIG_P4 2855
#define DIG_P5 140
#define DIG_P6 (-7)
#define DIG_P7 15500
#define DIG_P8 (-14600)
#define DIG_P9 6000
#define DIG_H1 75
#define DIG_H2 362
#define DIG_H3 0
#define DIG_H4 325
#define DIG_H5 50
#define DIG_H6 30

#define CHIP_ID_VALUE   0x60
#define SOFT_RESET_WORD 0xB6
#define I2C_ADDR        0x76

typedef struct {
  pin_t vcc;
  uint32_t temp_attr;   // degC
  uint32_t pres_attr;   // hPa
  uint32_t hum_attr;    // %RH
  uint8_t reg;          // register pointer
  uint8_t write_phase;  // 0 = expect register pointer, 1 = expect data
  uint8_t ctrl_meas;
  uint8_t config;
  int64_t t_fine;
} chip_state_t;

// ===================== Compensation formulas (Adafruit_BME280.cpp) =====================
// All divisions keep C signed-integer truncation semantics.

static int32_t lib_temp_centi(chip_state_t *chip, uint32_t adc_t, int64_t *t_fine_out) {
  int32_t var1 = ((int32_t)(adc_t / 8) - DIG_T1 * 2) * DIG_T2 / 2048;
  int32_t d = (int32_t)(adc_t / 16) - DIG_T1;
  int32_t var2 = ((d * d) / 4096) * DIG_T3 / 16384;
  int32_t t_fine = var1 + var2;
  if (t_fine_out) *t_fine_out = t_fine;
  return (t_fine * 5 + 128) / 256;
}

static int64_t lib_pressure_var4(chip_state_t *chip, uint32_t adc_p, int64_t t_fine) {
  int64_t var1 = t_fine - 128000;
  int64_t var2 = var1 * var1 * DIG_P6;
  var2 = var2 + (var1 * DIG_P5) * 131072;
  var2 = var2 + ((int64_t)DIG_P4) * 34359738368;
  var1 = (var1 * var1 * DIG_P3) / 256 + (var1 * DIG_P2) * 4096;
  var1 = (140737488355328 + var1) * DIG_P1 / 8589934592;
  if (var1 == 0) return 0;
  int64_t var4 = 1048576 - adc_p;
  var4 = ((var4 * 2147483648) - var2) * 3125 / var1;
  int64_t var1b = ((int64_t)DIG_P9) * (var4 / 8192) * (var4 / 8192) / 33554432;
  int64_t var2b = (((int64_t)DIG_P8) * var4) / 524288;
  return (var4 + var1b + var2b) / 256 + DIG_P7 * 16;
}

static int64_t lib_humidity_var5(chip_state_t *chip, uint32_t adc_h, int64_t t_fine) {
  int64_t var1 = t_fine - 76800;
  int64_t var2 = (int64_t)adc_h * 16384;
  int64_t var3 = ((int64_t)DIG_H4) * 1048576;
  int64_t var4 = ((int64_t)DIG_H5) * var1;
  int64_t var5 = (var2 - var3 - var4 + 16384) / 32768;
  var2 = var1 * DIG_H6 / 1024;
  var3 = var1 * DIG_H3 / 2048;
  var4 = (var2 * (var3 + 32768)) / 1024 + 2097152;
  var2 = (var4 * DIG_H2 + 8192) / 16384;
  var3 = var5 * var2;
  var4 = ((var3 / 32768) * (var3 / 32768)) / 128;
  var5 = var3 - (var4 * DIG_H1) / 16;
  if (var5 < 0) var5 = 0;
  if (var5 > 419430400) var5 = 419430400;
  return var5;
}

// ===================== Forward model: bisection =====================

static uint32_t forward_temp(chip_state_t *chip, int32_t target_centi) {
  uint32_t lo = 0, hi = 0xFFFFF;
  while (lo < hi) {
    uint32_t mid = (lo + hi) / 2;
    if (lib_temp_centi(chip, mid, NULL) < target_centi) lo = mid + 1;
    else hi = mid;
  }
  lib_temp_centi(chip, lo, &chip->t_fine);
  return lo;
}

static uint32_t forward_pressure(chip_state_t *chip, int64_t target_var4) {
  uint32_t lo = 0, hi = 0xFFFFF;
  while (lo < hi) {
    uint32_t mid = (lo + hi) / 2;
    if (lib_pressure_var4(chip, mid, chip->t_fine) > target_var4) lo = mid + 1;
    else hi = mid;
  }
  return lo;
}

static uint32_t forward_humidity(chip_state_t *chip, int64_t target_var5) {
  uint32_t lo = 0, hi = 0xFFFF;
  while (lo < hi) {
    uint32_t mid = (lo + hi) / 2;
    if (lib_humidity_var5(chip, mid, chip->t_fine) < target_var5) lo = mid + 1;
    else hi = mid;
  }
  return lo;
}

static uint32_t chip_adc_temp(chip_state_t *chip) {
  double t = attr_read(chip->temp_attr);
  if (t < -38.15) t = -38.15;
  if (t > 86.84) t = 86.84;
  return forward_temp(chip, (int32_t)(t * 100));
}

static uint32_t chip_adc_pres(chip_state_t *chip) {
  double hpa = attr_read(chip->pres_attr);
  if (hpa < 300.0) hpa = 300.0;
  if (hpa > 1100.0) hpa = 1100.0;
  int64_t target_var4 = (int64_t)(hpa * 100.0 * 256.0);
  return forward_pressure(chip, target_var4);
}

static uint32_t chip_adc_hum(chip_state_t *chip) {
  double h = attr_read(chip->hum_attr);
  if (h < 0.0) h = 0.0;
  if (h > 100.0) h = 100.0;
  int64_t target_var5 = (int64_t)(h * 4096.0 * 1024.0);
  return forward_humidity(chip, target_var5);
}

// ===================== Register read =====================

static uint8_t read_register(chip_state_t *chip, uint8_t reg) {
  switch (reg) {
    case REG_CHIP_ID: return CHIP_ID_VALUE;
    case REG_STATUS: return 0x00;
    case REG_CTRL_MEAS: return chip->ctrl_meas;
    case REG_CONFIG: return chip->config;
    case REG_DIG_T1_L: return DIG_T1 & 0xFF;
    case REG_DIG_T1_H: return (DIG_T1 >> 8) & 0xFF;
    case REG_DIG_T2_L: return DIG_T2 & 0xFF;
    case REG_DIG_T2_H: return (DIG_T2 >> 8) & 0xFF;
    case REG_DIG_T3_L: return DIG_T3 & 0xFF;
    case REG_DIG_T3_H: return (DIG_T3 >> 8) & 0xFF;
    case REG_DIG_P1_L: return DIG_P1 & 0xFF;
    case REG_DIG_P1_H: return (DIG_P1 >> 8) & 0xFF;
    case REG_DIG_P2_L: return DIG_P2 & 0xFF;
    case REG_DIG_P2_H: return (DIG_P2 >> 8) & 0xFF;
    case REG_DIG_P3_L: return DIG_P3 & 0xFF;
    case REG_DIG_P3_H: return (DIG_P3 >> 8) & 0xFF;
    case REG_DIG_P4_L: return DIG_P4 & 0xFF;
    case REG_DIG_P4_H: return (DIG_P4 >> 8) & 0xFF;
    case REG_DIG_P5_L: return DIG_P5 & 0xFF;
    case REG_DIG_P5_H: return (DIG_P5 >> 8) & 0xFF;
    case REG_DIG_P6_L: return DIG_P6 & 0xFF;
    case REG_DIG_P6_H: return (DIG_P6 >> 8) & 0xFF;
    case REG_DIG_P7_L: return DIG_P7 & 0xFF;
    case REG_DIG_P7_H: return (DIG_P7 >> 8) & 0xFF;
    case REG_DIG_P8_L: return DIG_P8 & 0xFF;
    case REG_DIG_P8_H: return (DIG_P8 >> 8) & 0xFF;
    case REG_DIG_P9_L: return DIG_P9 & 0xFF;
    case REG_DIG_P9_H: return (DIG_P9 >> 8) & 0xFF;
    case REG_DIG_H1: return DIG_H1;
    case REG_DIG_H2_L: return DIG_H2 & 0xFF;
    case REG_DIG_H2_H: return (DIG_H2 >> 8) & 0xFF;
    case REG_DIG_H3: return DIG_H3;
    case REG_DIG_H4: return (DIG_H4 >> 4) & 0xFF;                          // E4: H4 bits [11:4]
    case REG_DIG_H5: return ((DIG_H5 & 0xF) << 4) | (DIG_H4 & 0xF);        // E5: low nibble = H4[3:0], high nibble = H5[3:0]
    case REG_DIG_H6: return (DIG_H5 >> 4) & 0xFF;                          // E6: H5 bits [11:4]
    case REG_PRES_MSB: { uint32_t v = chip_adc_pres(chip); return (v >> 12) & 0xFF; }
    case REG_PRES_LSB: { uint32_t v = chip_adc_pres(chip); return (v >> 4) & 0xFF; }
    case REG_PRES_XLSB: { uint32_t v = chip_adc_pres(chip); return (v & 0xF) << 4; }
    case REG_TEMP_MSB: { uint32_t v = chip_adc_temp(chip); return (v >> 12) & 0xFF; }
    case REG_TEMP_LSB: { uint32_t v = chip_adc_temp(chip); return (v >> 4) & 0xFF; }
    case REG_TEMP_XLSB: { uint32_t v = chip_adc_temp(chip); return (v & 0xF) << 4; }
    case REG_HUM_MSB: { uint32_t v = chip_adc_hum(chip); return (v >> 8) & 0xFF; }
    case REG_HUM_LSB: { uint32_t v = chip_adc_hum(chip); return v & 0xFF; }
    default: return 0x00;
  }
}

// ===================== I2C callbacks =====================

bool on_i2c_connect(void *user_data, uint32_t address, bool read) {
  (void)address; (void)read;
  chip_state_t *chip = (chip_state_t *)user_data;
  if (pin_read(chip->vcc) != HIGH) return false;
  chip->write_phase = 0;
  return true;
}

uint8_t on_i2c_read(void *user_data) {
  chip_state_t *chip = (chip_state_t *)user_data;
  uint8_t value = read_register(chip, chip->reg);
  chip->reg++;
  return value;
}

bool on_i2c_write(void *user_data, uint8_t data) {
  chip_state_t *chip = (chip_state_t *)user_data;
  if (chip->write_phase == 0) {
    chip->reg = data;
    chip->write_phase = 1;
    return true;
  }
  // Data byte: single-register write (library only writes single bytes).
  switch (chip->reg) {
    case REG_CTRL_MEAS: chip->ctrl_meas = data; break;
    case REG_CONFIG: chip->config = data; break;
    case REG_RESET:
      if (data == SOFT_RESET_WORD) {
        chip->ctrl_meas = 0;
        chip->config = 0;
      }
      break;
    default: break;
  }
  return true;
}

void on_i2c_disconnect(void *user_data) {
  (void)user_data;
}

// ===================== Init =====================

void chip_init() {
  chip_state_t *chip = malloc(sizeof(chip_state_t));
  chip->vcc = pin_init("VCC", INPUT);

  chip->ctrl_meas = 0;
  chip->config = 0;
  chip->reg = 0;
  chip->write_phase = 0;
  chip->t_fine = 0;

  chip->temp_attr = attr_init("TEMP", 25.0);   // degC
  chip->pres_attr = attr_init("PRES", 1013.25); // hPa
  chip->hum_attr = attr_init("HUM", 60.0);     // %RH

  const i2c_config_t i2c_config = {
    .address = I2C_ADDR,
    .scl = pin_init("SCL", INPUT_PULLUP),
    .sda = pin_init("SDA", INPUT_PULLUP),
    .connect = on_i2c_connect,
    .read = on_i2c_read,
    .write = on_i2c_write,
    .disconnect = on_i2c_disconnect,
    .user_data = chip,
  };
  i2c_init(&i2c_config);

  printf("BME280 chip: I2C addr 0x76, attrs TEMP/PRES/HUM\n");
}
