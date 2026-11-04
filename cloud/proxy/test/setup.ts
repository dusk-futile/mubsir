import { applyD1Migrations } from 'cloudflare:test';
import { env } from 'cloudflare:workers';
import { beforeEach } from 'vitest';

await applyD1Migrations(env.DB, env.TEST_MIGRATIONS);

// Storage is shared between tests here, so every test starts from empty tables and buckets.
beforeEach(async () => {
  await env.DB.batch(
    ['devices', 'usage_daily', 'spend_monthly', 'registrations', 'settings'].map((table) => env.DB.prepare(`DELETE FROM ${table}`)),
  );
  for (const bucket of [env.AUDIO, env.UPDATES]) {
    const listed = await bucket.list();
    if (listed.objects.length > 0) await bucket.delete(listed.objects.map((o) => o.key));
  }
});
