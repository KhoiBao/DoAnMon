# Security Notes

## Access tokens

The MQTT username is a ThingsBoard device Access Token. A public demo
broker (broker.hivemq.com) accepts any username, so the firmware works
without a real token. For ThingsBoard, put the real token in
`include/secrets.h`.

Rules:

1. Do not hard-code tokens in source files that you share.
2. Do not paste tokens into reports or screenshots.
3. `include/secrets.h` is for local use only. Never commit it.

## What is committed

| File | Content |
|------|---------|
| `include/secrets.example.h` | Placeholder values only |
| `include/Config.h` | Broker host, port, topic, pins — no tokens |
| `dashboard/appsettings.json` | Broker host, port, topic, location — no tokens |

The compile-time guard `__has_include("secrets.h")` makes the firmware
build even when `secrets.h` does not exist. The build then uses the
default value `demo`.

## Public broker risk

The demo topic on a public broker is readable by anyone. Use only
simulated data on the public broker. For a real deployment:

1. Use a private broker with authentication.
2. Use MQTT over TLS (port 8883) when the hardware supports it.
3. Change the topic to a private prefix.

## Report integrity

Do not write "tested" or "measured" for a test you did not run. Do not
fill real-result columns in test tables before the test exists. The
verification document records only tests that were executed on this
machine.
