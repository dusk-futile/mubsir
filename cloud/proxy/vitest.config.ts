import path from 'node:path';
import { cloudflareTest, readD1Migrations } from '@cloudflare/vitest-plugin';
import { defineConfig } from 'vitest/config';

export default defineConfig(async () => {
  const migrations = await readD1Migrations(path.join(import.meta.dirname, 'migrations'));
  return {
    plugins: [
      cloudflareTest({
        wrangler: { configPath: './wrangler.toml' },
        miniflare: {
          // Test-only values; real secrets are set with `wrangler secret put`.
          bindings: {
            TEST_MIGRATIONS: migrations,
            ANTHROPIC_API_KEY: 'sk-ant-server-test',
            FISH_API_KEY: 'fish-server-test',
            ADMIN_TOKEN: 'admin-test-token',
            IP_SALT: 'test-salt',
            VOICE_CONVERSATION: 'voice-conversation-test',
            VOICE_READING: 'voice-reading-test',
          },
        },
      }),
    ],
    test: { setupFiles: ['./test/setup.ts'] },
  };
});
