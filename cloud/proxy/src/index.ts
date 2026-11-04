import { authenticate } from './auth';
import type { Device, Env } from './env';
import { messages } from './routes/messages';
import { admin, sttToken, update } from './routes/misc';
import { register } from './routes/register';
import { speech } from './routes/speech';
import { apiError } from './util';

type DeviceRoute = (request: Request, env: Env, ctx: ExecutionContext, device: Device) => Promise<Response>;

const DEVICE_ROUTES: Record<string, DeviceRoute> = {
  '/v1/messages': messages,
  '/v1/tts': speech,
  '/v1/stt-token': sttToken,
};

/**
 * Mubsir relay: holds the API keys, checks each device's token and limits, and forwards requests.
 * It never stores or logs what a user says, reads or types; only counts and costs per device.
 */
export default {
  async fetch(request, env, ctx): Promise<Response> {
    const url = new URL(request.url);
    try {
      if (url.pathname === '/health') return Response.json({ ok: true });
      if (url.pathname.startsWith('/update/') && request.method === 'GET') return await update(url, env);
      if (url.pathname.startsWith('/admin/')) return await admin(request, url, env);
      if (url.pathname === '/v1/register') {
        return request.method === 'POST' ? await register(request, env) : apiError(405, 'invalid_request_error', 'Use POST.');
      }

      const route = DEVICE_ROUTES[url.pathname];
      if (!route) return apiError(404, 'not_found_error', 'No such endpoint.');
      if (request.method !== 'POST') return apiError(405, 'invalid_request_error', 'Use POST.');
      const device = await authenticate(request, env);
      if (device instanceof Response) return device;
      return await route(request, env, ctx, device);
    } catch (err) {
      // Error class only: request contents must never reach the logs.
      console.error('relay_error', err instanceof Error ? err.name : 'unknown');
      return apiError(500, 'api_error', 'The relay hit an error.');
    }
  },
} satisfies ExportedHandler<Env>;
