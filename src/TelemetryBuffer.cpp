#include "TelemetryBuffer.h"

void TelemetryBuffer::push(const char* payload) {
  strncpy(_entries[_head], payload, PAYLOAD_MAX - 1);
  _entries[_head][PAYLOAD_MAX - 1] = '\0';
  _head = (_head + 1) % CAPACITY;
  if (_count < CAPACITY) {
    _count++;
  } else {
    _dropped++;
  }
}

bool TelemetryBuffer::pop(char* out, size_t outSize) {
  if (_count == 0) return false;
  const size_t oldest = (_head + CAPACITY - _count) % CAPACITY;
  strncpy(out, _entries[oldest], outSize - 1);
  out[outSize - 1] = '\0';
  _count--;
  return true;
}
