import { env } from 'cloudflare:workers';
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { utcMonth } from '../src/util';
import { SSE, SSE_COST_MICRO } from './fixtures';
import { call, jsonResponse, monthSpend, newDevice, usageOf, withEnv } from './helpers';

const CHAT = { model: 'claude-haiku-5-5', max_tokens: 1024, messages: [{ role: 'user', content: 'افتح الإعدادات' }] };
const ADMIN = { authorization: 'Bearer admin-test-token', 'content-type': 'application/json' };

/** Every outbound request the Worker makes lands here as a Request. */
let upstream: Mock<(request: Request) => Promise<Response>>;
const answer = (input = 1_000, output = 200) =>
  jsonResponse({ id: 'msg_1', type: 'message', content: [{ type: 'text', text: 'ok' }], usage: { input_tokens: input, output_tokens: output } });

beforeEach(() => {
  upstream = vi.fn(async (request: Request) => (request.url.startsWith('https://hooks.test') ? new Response('ok') : answer()));
  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => upstream(new Request(input as RequestInfo, init)));
});
afterEach(() => vi.restoreAllMocks());

const ask = (token: string, body: unknown = CHAT, headers: Record<string, string> = {}, e = env) =>
  call(
    '/v1/messages',
    { method: 'POST', headers: { 'content-type': 'application/json', 'x-api-key': token, 'anthropic-version': '2026-10-21', ...headers }, body: JSON.stringify(body) },
    e,
  );
const anthropicCalls = () => upstream.mock.calls.filter(([r]) => r.url === 'https://api.anthropic.com/v1/messages');
const alertCalls = () => upstream.mock.calls.filter(([r]) => r.url.startsWith('https://hooks.test'));

describe('registration', () => {
  it('gives a new install its own token and stores only its hash', async () => {
    const { res } = await call('/v1/register', {
      method: 'POST',
      headers: { 'content-type': 'application/json', 'cf-connecting-ip': '1.1.1.1' },
      body: JSON.stringify({ app_version: '2.0.0' }),
    });
    expect(res.status).toBe(201);
    const body = await res.json<{ token: string; device_id: string }>();
    expect(body.token).toMatch(/^mbs_[A-Za-z0-9_-]{43}$/);
    expect(body.device_id).toMatch(/^[0-9a-f]{32}$/);
    const row = await env.DB.prepare('SELECT token_hash, status, app_version FROM devices WHERE id = ?')
      .bind(body.device_id)
      .first<{ token_hash: string; status: string; app_version: string }>();
    expect(row).toMatchObject({ status: 'active', app_version: '2.0.0' });
    expect(row?.token_hash).toMatch(/^[0-9a-f]{64}$/);
    expect(row?.token_hash).not.toContain(body.token.slice(4));
  });

  it('limits new installs per network per day', async () => {
    const register = () => call('/v1/register', { method: 'POST', headers: { 'cf-connecting-ip': '2.2.2.2' }, body: '{}' });
    for (let i = 0; i < 5; i += 1) expect((await register()).res.status).toBe(201);
    const { res } = await register();
    expect(res.status).toBe(429);
    expect(res.headers.get('retry-after')).toBe('3600');
  });

  it('asks for an invite code when invite codes are set', async () => {
    const e = withEnv({ INVITE_CODES: 'cairo-pilot, giza-pilot' });
    const register = (invite?: string) =>
      call('/v1/register', { method: 'POST', headers: { 'cf-connecting-ip': '3.3.3.3' }, body: JSON.stringify({ invite }) }, e);
    expect((await register()).res.status).toBe(403);
    expect((await register('wrong')).res.status).toBe(403);
    expect((await register('giza-pilot')).res.status).toBe(201);
  });
});

describe('/v1/messages', () => {
  it('rejects missing and unknown tokens', async () => {
    expect((await call('/v1/messages', { method: 'POST', body: '{}' })).res.status).toBe(401);
    expect((await ask('mbs_not-a-real-token')).res.status).toBe(401);
    expect(anthropicCalls()).toHaveLength(0);
  });

  it('forwards the request unchanged with the server key, and records its cost', async () => {
    const device = await newDevice();
    const body = JSON.stringify(CHAT);
    const { res, done } = await call('/v1/messages', {
      method: 'POST',
      headers: { 'content-type': 'application/json', 'x-api-key': device.token },
      body,
    });
    expect(res.status).toBe(200);
    expect(await res.json()).toMatchObject({ id: 'msg_1' });
    await done();

    const [sent] = anthropicCalls()[0]!;
    expect(sent.headers.get('x-api-key')).toBe('sk-ant-server-test');
    expect(sent.headers.get('anthropic-version')).toBe('2026-10-21');
    expect(await sent.text()).toBe(body);

    expect(await usageOf(device.id)).toMatchObject({ requests: 1, input_tokens: 1_000, output_tokens: 200, cost_micro: 200 });
    expect(await monthSpend()).toBe(200);
  });

  it('streams the answer through byte for byte and bills it from the stream', async () => {
    upstream.mockImplementation(async () => new Response(SSE, { headers: { 'content-type': 'text/event-stream', 'request-id': 'req_1' } }));
    const device = await newDevice();
    const { res, done } = await ask(device.token, { ...CHAT, stream: true });
    expect(res.headers.get('content-type')).toBe('text/event-stream');
    expect(res.headers.get('request-id')).toBe('req_1');
    expect(await res.text()).toBe(SSE);
    await done();
    expect(await usageOf(device.id)).toMatchObject({ input_tokens: 120, output_tokens: 42, cache_read_tokens: 800, cost_micro: SSE_COST_MICRO });
  });

  it('refuses models, betas, fast mode and output sizes the relay has not enabled', async () => {
    const device = await newDevice();
    const refused = [
      await ask(device.token, { ...CHAT, model: 'claude-opus-5-5' }),
      await ask(device.token, { ...CHAT, max_tokens: 64_000 }),
      await ask(device.token, { ...CHAT, speed: 'fast' }),
      await ask(device.token, CHAT, { 'anthropic-beta': 'fast-mode-2026-02-01' }),
      await call('/v1/messages', { method: 'POST', headers: { 'x-api-key': device.token }, body: 'not json' }),
    ];
    for (const { res } of refused) {
      expect(res.status).toBe(400);
      expect(await res.json()).toMatchObject({ type: 'error', error: { type: 'invalid_request_error' } });
    }
    expect(anthropicCalls()).toHaveLength(0);
  });

  it("refuses tools Anthropic runs and bills apart, but passes the app's own tools", async () => {
    const device = await newDevice();
    const own = { name: 'open_app', description: 'Open an app', input_schema: { type: 'object', properties: {} } };
    const refused = [
      await ask(device.token, { ...CHAT, tools: [own, { type: 'web_search_20260209', name: 'web_search' }] }),
      await ask(device.token, { ...CHAT, tools: [{ type: 'code_execution_20260521', name: 'code_execution' }] }),
      await ask(device.token, { ...CHAT, mcp_servers: [{ type: 'url', url: 'https://mcp.example', name: 'x' }] }),
    ];
    for (const { res } of refused) expect(res.status).toBe(400);
    expect(anthropicCalls()).toHaveLength(0);

    expect((await ask(device.token, { ...CHAT, tools: [own, { ...own, name: 'scroll', type: 'custom' }] })).res.status).toBe(200);
    const e = withEnv({ ALLOWED_TOOL_TYPES: 'computer_toolset_20260801' });
    expect((await ask(device.token, { ...CHAT, tools: [{ type: 'computer_toolset_20260801', name: 'computer' }] }, {}, e)).res.status).toBe(200);
  });

  it('allows a beta once the owner enables it', async () => {
    const device = await newDevice();
    const e = withEnv({ ALLOWED_BETAS: 'mid-conversation-output-config-2026-07-01' });
    const { res } = await ask(device.token, CHAT, { 'anthropic-beta': 'mid-conversation-output-config-2026-07-01' }, e);
    expect(res.status).toBe(200);
    expect(anthropicCalls()[0]![0].headers.get('anthropic-beta')).toBe('mid-conversation-output-config-2026-07-01');
  });

  it('passes Anthropic errors back unchanged and bills nothing', async () => {
    upstream.mockImplementation(async () =>
      jsonResponse({ type: 'error', error: { type: 'overloaded_error', message: 'Overloaded' } }, { status: 529 }),
    );
    const device = await newDevice();
    const { res, done } = await ask(device.token);
    expect(res.status).toBe(529);
    expect(await res.json()).toMatchObject({ error: { type: 'overloaded_error' } });
    await done();
    expect(await usageOf(device.id)).toMatchObject({ requests: 1, cost_micro: 0 });
  });

  it("stops a device at its daily limit without calling Anthropic", async () => {
    const e = withEnv({ DEVICE_DAILY_REQUESTS: '2' });
    const device = await newDevice();
    expect((await ask(device.token, CHAT, {}, e)).res.status).toBe(200);
    expect((await ask(device.token, CHAT, {}, e)).res.status).toBe(200);
    const { res } = await ask(device.token, CHAT, {}, e);
    expect(res.status).toBe(402);
    expect(await res.json()).toMatchObject({ error: { type: 'quota_exceeded' } });
    expect(anthropicCalls()).toHaveLength(2);
  });

  it('stops paid requests once the monthly budget is used up', async () => {
    await env.DB.prepare('INSERT INTO spend_monthly (month, cost_micro) VALUES (?, ?)').bind(utcMonth(), 200_000_000).run();
    const device = await newDevice();
    const { res } = await ask(device.token);
    expect(res.status).toBe(402);
    expect(await res.json()).toMatchObject({ error: { type: 'budget_exhausted' } });
    expect(anthropicCalls()).toHaveLength(0);
  });

  it('sends one budget alert per threshold', async () => {
    // Budget $0.001 = 1,000 micro-USD; each answer costs 6,000 x $0.10/M = 600 micro-USD.
    upstream.mockImplementation(async (r) => (r.url.startsWith('https://hooks.test') ? new Response('ok') : answer(6_000, 0)));
    const e = withEnv({ MONTHLY_BUDGET_USD: '0.001', ALERT_WEBHOOK_URL: 'https://hooks.test/alert' });
    const device = await newDevice(e);

    await (await ask(device.token, CHAT, {}, e)).done();
    expect(alertCalls()).toHaveLength(1);
    expect(await alertCalls()[0]![0].json()).toMatchObject({ text: expect.stringContaining('50%') });

    await (await ask(device.token, CHAT, {}, e)).done();
    expect(alertCalls()).toHaveLength(2);
    expect(await alertCalls()[1]![0].json()).toMatchObject({ text: expect.stringContaining('100%') });

    const { res } = await ask(device.token, CHAT, {}, e);
    expect(res.status).toBe(402);
    expect(alertCalls()).toHaveLength(2);
  });
});

describe('owner switches', () => {
  it('needs the admin token', async () => {
    expect((await call('/admin/usage')).res.status).toBe(401);
    expect((await call('/admin/usage', { headers: { authorization: 'Bearer wrong' } })).res.status).toBe(401);
  });

  it('pauses and restarts the relay for everyone', async () => {
    const device = await newDevice();
    expect((await call('/admin/pause', { method: 'POST', headers: ADMIN, body: '{"paused":true}' })).res.status).toBe(200);
    const paused = await ask(device.token);
    expect(paused.res.status).toBe(503);
    expect(await paused.res.json()).toMatchObject({ error: { type: 'service_paused' } });
    expect((await call('/v1/register', { method: 'POST', body: '{}' })).res.status).toBe(503);

    await call('/admin/pause', { method: 'POST', headers: ADMIN, body: '{"paused":false}' });
    expect((await ask(device.token)).res.status).toBe(200);
  });

  it('switches one device off and on', async () => {
    const device = await newDevice();
    expect((await call(`/admin/devices/${device.id}/block`, { method: 'POST', headers: ADMIN })).res.status).toBe(200);
    expect((await ask(device.token)).res.status).toBe(403);
    await call(`/admin/devices/${device.id}/unblock`, { method: 'POST', headers: ADMIN });
    expect((await ask(device.token)).res.status).toBe(200);
    expect((await call(`/admin/devices/${'0'.repeat(32)}/block`, { method: 'POST', headers: ADMIN })).res.status).toBe(404);
  });

  it("reports this month's spend and today's totals", async () => {
    const device = await newDevice();
    await (await ask(device.token)).done();
    const { res } = await call('/admin/usage', { headers: ADMIN });
    expect(await res.json()).toMatchObject({
      month: utcMonth(),
      spend_usd: 0.0002,
      budget_usd: 200,
      paused: false,
      today: { requests: 1, cost_usd: 0.0002, active_devices: 1 },
      devices: { total: 1, blocked: 0 },
    });
  });
});

describe('other endpoints', () => {
  it('serves the update feed from R2 without a token', async () => {
    await env.UPDATES.put('releases.win.json', '{"Assets":[]}');
    const { res } = await call('/update/releases.win.json');
    expect(res.status).toBe(200);
    expect(res.headers.get('content-type')).toBe('application/json');
    expect(await res.text()).toBe('{"Assets":[]}');
    expect((await call('/update/missing.json')).res.status).toBe(404);
    expect((await call('/update/..%2Fsecret')).res.status).toBe(404);
  });

  it('says plainly that speech-to-text is not set up yet', async () => {
    const device = await newDevice();
    const { res } = await call('/v1/stt-token', { method: 'POST', headers: { 'x-api-key': device.token } });
    expect(res.status).toBe(501);
    expect(await res.json()).toMatchObject({ error: { type: 'not_configured' } });
  });

  it('answers 404 and 405 for unknown paths and methods', async () => {
    expect((await call('/v1/unknown', { method: 'POST' })).res.status).toBe(404);
    expect((await call('/v1/messages')).res.status).toBe(405);
    expect((await call('/v1/register')).res.status).toBe(405);
    expect((await call('/health')).res.status).toBe(200);
  });
});
