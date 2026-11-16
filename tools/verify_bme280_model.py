#!/usr/bin/env python3
"""Verify the BME280 virtual-chip forward model against the Adafruit
library's inverse (compensation) formulas.

The chip must produce raw ADC values such that the Adafruit_BME280
compensation formulas return the slider values (temperature, pressure,
humidity). This script implements BOTH sides with exact C-compatible
integer arithmetic and checks the round trip.
"""

import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def cdiv(a, b):
    """C signed integer division: truncates toward zero."""
    q = abs(a) // abs(b)
    if (a >= 0) != (b >= 0):
        q = -q
    return q


def cshift(a, n):
    """Arithmetic shift like C (shift of negative values floors)."""
    if n >= 0:
        return a >> n
    return a << (-n)


# ------------------------------------------------------------------
# Virtual chip calibration constants (written into bme280.chip.c)
# ------------------------------------------------------------------
DIG_T1, DIG_T2, DIG_T3 = 20000, 10000, 0
DIG_P1, DIG_P2, DIG_P3 = 36477, -10685, 3024
DIG_P4, DIG_P5, DIG_P6 = 2855, 140, -7
DIG_P7, DIG_P8, DIG_P9 = 15500, -14600, 6000
DIG_H1, DIG_H2, DIG_H3 = 75, 362, 0
DIG_H4, DIG_H5, DIG_H6 = 325, 50, 30

ADC_T_MAX = (1 << 20) - 1
ADC_P_MAX = (1 << 20) - 1
ADC_H_MAX = (1 << 16) - 1


# ------------------------------------------------------------------
# Adafruit_BME280 inverse formulas (from Adafruit_BME280.cpp)
# ------------------------------------------------------------------
def lib_temp(adc_t):
    """Return (temperature_degC_float, t_fine)."""
    var1 = cdiv((adc_t // 8) - DIG_T1 * 2, 1)
    var1 = cdiv(var1 * DIG_T2, 2048)
    d = (adc_t // 16) - DIG_T1
    var2 = cdiv(cdiv(cdiv(d, 1) * d, 4096) * DIG_T3, 16384)
    t_fine = var1 + var2
    t = cdiv(t_fine * 5 + 128, 256)
    return t / 100.0, t_fine


def lib_pressure(adc_p, t_fine):
    """Return pressure in Pa (float)."""
    var1 = t_fine - 128000
    var2 = var1 * var1 * DIG_P6
    var2 = var2 + (var1 * DIG_P5) * 131072
    var2 = var2 + DIG_P4 * 34359738368
    var1 = cdiv(var1 * var1 * DIG_P3, 256) + (var1 * DIG_P2) * 4096
    var1 = cdiv((140737488355328 + var1) * DIG_P1, 8589934592)
    if var1 == 0:
        return 0.0
    var4 = 1048576 - adc_p
    var4 = cdiv((var4 * 2147483648 - var2) * 3125, var1)
    var1b = cdiv(DIG_P9 * cdiv(var4, 8192) * cdiv(var4, 8192), 33554432)
    var2b = cdiv(DIG_P8 * var4, 524288)
    var4 = cdiv(var4 + var1b + var2b, 256) + DIG_P7 * 16
    return var4 / 256.0


def lib_humidity(adc_h, t_fine):
    """Return relative humidity in %RH (float)."""
    var1 = t_fine - 76800
    var2 = adc_h * 16384
    var3 = DIG_H4 * 1048576
    var4 = DIG_H5 * var1
    var5 = cdiv(var2 - var3 - var4 + 16384, 32768)
    var2 = cdiv(var1 * DIG_H6, 1024)
    var3 = cdiv(var1 * DIG_H3, 2048)
    var4 = cdiv(var2 * (var3 + 32768), 1024) + 2097152
    var2 = cdiv(var4 * DIG_H2 + 8192, 16384)
    var3 = var5 * var2
    var4 = cdiv(cdiv(var3, 32768) * cdiv(var3, 32768), 128)
    var5 = var3 - cdiv(var4 * DIG_H1, 16)
    var5 = max(0, min(var5, 419430400))
    return cdiv(var5, 4096) / 1024.0


# ------------------------------------------------------------------
# Chip forward model: bisection for smallest adc where value >= target
# ------------------------------------------------------------------
BISECT_ITERATIONS = 24


def forward_temp(target_centi):
    """Smallest adc_t with lib_temp(adc_t) >= target (in centi-degrees)."""
    lo, hi = 0, ADC_T_MAX
    while lo < hi:
        mid = (lo + hi) // 2
        if lib_temp(mid)[0] * 100 < target_centi:
            lo = mid + 1
        else:
            hi = mid
    return lo


def forward_pressure(target_var4, t_fine):
    """Smallest adc_p with lib_pressure(adc_p, t_fine) <= target.

    lib_pressure is decreasing in adc_p. Compare on the raw var4
    (int64, Pa * 256) to avoid float rounding in the comparison.
    """
    def raw_var4(adc_p):
        var1 = t_fine - 128000
        var2 = var1 * var1 * DIG_P6
        var2 = var2 + (var1 * DIG_P5) * 131072
        var2 = var2 + DIG_P4 * 34359738368
        var1b = cdiv(var1 * var1 * DIG_P3, 256) + (var1 * DIG_P2) * 4096
        var1b = cdiv((140737488355328 + var1b) * DIG_P1, 8589934592)
        if var1b == 0:
            return 0
        var4 = 1048576 - adc_p
        var4 = cdiv((var4 * 2147483648 - var2) * 3125, var1b)
        vb1 = cdiv(DIG_P9 * cdiv(var4, 8192) * cdiv(var4, 8192), 33554432)
        vb2 = cdiv(DIG_P8 * var4, 524288)
        return cdiv(var4 + vb1 + vb2, 256) + DIG_P7 * 16

    lo, hi = 0, ADC_P_MAX
    while lo < hi:
        mid = (lo + hi) // 2
        if raw_var4(mid) > target_var4:
            lo = mid + 1
        else:
            hi = mid
    return lo


def forward_humidity(target_var5, t_fine):
    """Smallest adc_h with clamped var5 >= target (increasing in adc_h)."""

    def raw_var5(adc_h):
        var1 = t_fine - 76800
        var2 = adc_h * 16384
        var3 = DIG_H4 * 1048576
        var4 = DIG_H5 * var1
        var5 = cdiv(var2 - var3 - var4 + 16384, 32768)
        var2 = cdiv(var1 * DIG_H6, 1024)
        var3 = cdiv(var1 * DIG_H3, 2048)
        var4 = cdiv(var2 * (var3 + 32768), 1024) + 2097152
        var2 = cdiv(var4 * DIG_H2 + 8192, 16384)
        var3 = var5 * var2
        var4 = cdiv(cdiv(var3, 32768) * cdiv(var3, 32768), 128)
        var5 = var3 - cdiv(var4 * DIG_H1, 16)
        return max(0, min(var5, 419430400))

    lo, hi = 0, ADC_H_MAX
    while lo < hi:
        mid = (lo + hi) // 2
        if raw_var5(mid) < target_var5:
            lo = mid + 1
        else:
            hi = mid
    return lo


def chip_pack(temperature, pressure_pa, humidity):
    """Full chip behaviour: returns (adc_t, t_fine, adc_p, adc_h)."""
    adc_t = forward_temp(round(temperature * 100))
    _, t_fine = lib_temp(adc_t)
    adc_p = forward_pressure(round(pressure_pa * 256), t_fine)
    adc_h = forward_humidity(round(humidity * 4096 * 1024), t_fine)
    return adc_t, t_fine, adc_p, adc_h


def main():
    print("BME280 virtual chip model verification")
    print("=" * 60)

    temp_cases = [-20.0, 0.0, 5.0, 15.5, 25.0, 30.0, 40.0, 60.0]
    pres_cases = [30000.0, 70000.0, 90000.0, 101325.0, 105000.0, 110000.0]
    hum_cases = [0.0, 5.0, 30.0, 60.0, 85.5, 100.0]

    max_err_t = max_err_p = max_err_h = 0.0
    failures = 0

    for t in temp_cases:
        adc_t, t_fine, _, _ = chip_pack(t, 101325.0, 60.0)
        got, _ = lib_temp(adc_t)
        err = abs(got - t)
        max_err_t = max(max_err_t, err)
        ok = err < 0.01
        failures += 0 if ok else 1
        print(f"temp {t:8.2f} -> adc 0x{adc_t:05X} -> lib {got:9.2f} "
              f"err {err:.4f}  t_fine {t_fine}  {'OK' if ok else 'FAIL'}")

    print("-" * 60)

    for p in pres_cases:
        _, t_fine, adc_p, _ = chip_pack(25.0, p, 60.0)
        got = lib_pressure(adc_p, t_fine)
        err = abs(got - p)
        max_err_p = max(max_err_p, err)
        ok = err < 0.5
        failures += 0 if ok else 1
        print(f"pres {p:10.1f} -> adc 0x{adc_p:05X} -> lib {got:10.1f} "
              f"err {err:.4f}  {'OK' if ok else 'FAIL'}")

    print("-" * 60)

    for h in hum_cases:
        adc_t, t_fine, _, adc_h = chip_pack(25.0, 101325.0, h)
        got = lib_humidity(adc_h, t_fine)
        err = abs(got - h)
        max_err_h = max(max_err_h, err)
        ok = err < 0.01
        failures += 0 if ok else 1
        print(f"hum  {h:8.1f} -> adc 0x{adc_h:04X} -> lib {got:8.2f} "
              f"err {err:.4f}  {'OK' if ok else 'FAIL'}")

    print("=" * 60)
    print(f"max err: temp {max_err_t:.4f} degC | pres {max_err_p:.4f} Pa "
          f"| hum {max_err_h:.4f} %RH")
    print(f"failures: {failures}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
