-- Mubsir relay storage. Only tokens' hashes, counts and costs; never what users say or read.

CREATE TABLE devices (
  id TEXT PRIMARY KEY,                     -- 32 hex characters
  token_hash TEXT NOT NULL UNIQUE,         -- SHA-256 of the device token
  status TEXT NOT NULL DEFAULT 'active',   -- 'active' or 'blocked'
  created_at INTEGER NOT NULL,             -- Unix ms
  app_version TEXT
);

CREATE TABLE usage_daily (
  device_id TEXT NOT NULL,
  day TEXT NOT NULL,                       -- YYYY-MM-DD, UTC
  requests INTEGER NOT NULL DEFAULT 0,
  input_tokens INTEGER NOT NULL DEFAULT 0,
  output_tokens INTEGER NOT NULL DEFAULT 0,
  cache_read_tokens INTEGER NOT NULL DEFAULT 0,
  cache_write_tokens INTEGER NOT NULL DEFAULT 0,
  tts_bytes INTEGER NOT NULL DEFAULT 0,
  cost_micro INTEGER NOT NULL DEFAULT 0,   -- millionths of a USD
  PRIMARY KEY (device_id, day)
);

CREATE TABLE spend_monthly (
  month TEXT PRIMARY KEY,                  -- YYYY-MM, UTC
  cost_micro INTEGER NOT NULL DEFAULT 0,
  alerted INTEGER NOT NULL DEFAULT 0       -- highest alert sent: 0, 50, 80 or 100 (%)
);

CREATE TABLE registrations (
  ip_hash TEXT NOT NULL,                   -- salted SHA-256 of the IP address
  day TEXT NOT NULL,
  count INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY (ip_hash, day)
);

CREATE TABLE settings (
  key TEXT PRIMARY KEY,                    -- 'paused' = '1' stops the relay for everyone
  value TEXT NOT NULL
);
