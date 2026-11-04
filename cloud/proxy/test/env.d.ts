import type { D1Migration } from '@cloudflare/vitest-plugin';
import type { Env as RelayEnv } from '../src/env';

declare global {
  namespace Cloudflare {
    interface Env extends RelayEnv {
      TEST_MIGRATIONS: D1Migration[];
    }
  }
}

export {};
