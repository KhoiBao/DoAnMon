#include <stdio.h>
#include <unity.h>
#include "TelemetryBuffer.h"

static TelemetryBuffer buf;

void setUp(void) { buf = TelemetryBuffer(); }
void tearDown(void) {}

void test_pop_empty_returns_false(void) {
  char out[TelemetryBuffer::PAYLOAD_MAX];
  TEST_ASSERT_FALSE(buf.pop(out, sizeof(out)));
}

void test_fifo_order(void) {
  char out[TelemetryBuffer::PAYLOAD_MAX];
  buf.push("a");
  buf.push("b");
  buf.push("c");
  TEST_ASSERT_TRUE(buf.pop(out, sizeof(out)));
  TEST_ASSERT_EQUAL_STRING("a", out);
  TEST_ASSERT_TRUE(buf.pop(out, sizeof(out)));
  TEST_ASSERT_EQUAL_STRING("b", out);
  TEST_ASSERT_TRUE(buf.pop(out, sizeof(out)));
  TEST_ASSERT_EQUAL_STRING("c", out);
  TEST_ASSERT_FALSE(buf.pop(out, sizeof(out)));
}

void test_overwrite_oldest_and_dropped_count(void) {
  char out[TelemetryBuffer::PAYLOAD_MAX];
  char p[16];
  const int total = (int)TelemetryBuffer::CAPACITY + 2;
  for (int i = 0; i < total; i++) {
    snprintf(p, sizeof(p), "p%d", i);
    buf.push(p);
  }
  TEST_ASSERT_EQUAL_UINT32(2, buf.dropped());
  TEST_ASSERT_TRUE(buf.pop(out, sizeof(out)));
  TEST_ASSERT_EQUAL_STRING("p2", out);
}

void test_long_payload_truncated(void) {
  char big[200];
  memset(big, 'x', sizeof(big) - 1);
  big[sizeof(big) - 1] = '\0';
  buf.push(big);
  char out[TelemetryBuffer::PAYLOAD_MAX];
  TEST_ASSERT_TRUE(buf.pop(out, sizeof(out)));
  TEST_ASSERT_EQUAL_UINT32(127, strlen(out));
}

int main(void) {
  UNITY_BEGIN();
  RUN_TEST(test_pop_empty_returns_false);
  RUN_TEST(test_fifo_order);
  RUN_TEST(test_overwrite_oldest_and_dropped_count);
  RUN_TEST(test_long_payload_truncated);
  return UNITY_END();
}
