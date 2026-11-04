import { env } from 'cloudflare:workers';
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { call, newDevice, usageOf, withEnv } from './helpers';

const AUDIO = new Uint8Array([0x49, 0x44, 0x33, 4, 0, 0, 7, 7]);

let upstream: Mock<(request: Request) => Promise<Response>>;
beforeEach(() => {
  upstream = vi.fn(async () => new Response(AUDIO, { headers: { 'content-type': 'audio/mpeg' } }));
  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => upstream(new Request(input as RequestInfo, init)));
});
afterEach(() => vi.restoreAllMocks());

const speak = (token: string, body: unknown, e = env) =>
  call('/v1/tts', { method: 'POST', headers: { 'content-type': 'application/json', 'x-api-key': token }, body: JSON.stringify(body) }, e);

describe('/v1/tts', () => {
  it('makes speech once, then serves it from the cache for free', async () => {
    const device = await newDevice();
    const first = await speak(device.token, { text: 'مرحبا', voice: 'reading' });
    expect(first.res.status).toBe(200);
    expect(first.res.headers.get('content-type')).toBe('audio/mpeg');
    expect(first.res.headers.get('x-mubsir-cache')).toBe('miss');
    expect(new Uint8Array(await first.res.arrayBuffer())).toEqual(AUDIO);
    await first.done();

    const [sent] = upstream.mock.calls[0]!;
    expect(sent.url).toBe('https://api.fish.audio/v1/tts');
    expect(sent.headers.get('authorization')).toBe('Bearer fish-server-test');
    expect(sent.headers.get('model')).toBe('s2.1-pro');
    expect(await sent.json()).toMatchObject({
      text: 'مرحبا',
      reference_id: 'voice-reading-test',
      format: 'mp3',
      latency: 'normal',
      normalize: true,
      prosody: { speed: 1 },
    });

    const second = await speak(device.token, { text: 'مرحبا', voice: 'reading' });
    expect(second.res.headers.get('x-mubsir-cache')).toBe('hit');
    expect(new Uint8Array(await second.res.arrayBuffer())).toEqual(AUDIO);
    await second.done();
    expect(upstream).toHaveBeenCalledTimes(1);

    // "مرحبا" is 5 Arabic letters = 10 UTF-8 bytes; $15 per million bytes = 150 micro-USD, billed once.
    expect(await usageOf(device.id)).toMatchObject({ tts_bytes: 10, cost_micro: 150 });
  });

  it('uses the conversation voice with low latency, and keys the cache by voice, speed and format', async () => {
    const device = await newDevice();
    await (await speak(device.token, { text: 'Done.', voice: 'conversation' })).done();
    await (await speak(device.token, { text: 'Done.', voice: 'conversation', speed: 1.5 })).done();
    await (await speak(device.token, { text: 'Done.', voice: 'conversation', format: 'opus' })).done();
    expect(upstream).toHaveBeenCalledTimes(3);
    expect(await upstream.mock.calls[0]![0].json()).toMatchObject({ reference_id: 'voice-conversation-test', latency: 'balanced', mp3_bitrate: 64 });
    expect(await upstream.mock.calls[1]![0].json()).toMatchObject({ prosody: { speed: 1.5 } });
    expect(await upstream.mock.calls[2]![0].json()).toMatchObject({ format: 'opus' });
  });

  it("counts new speech against the device's daily limit", async () => {
    const e = withEnv({ DEVICE_DAILY_TTS_BYTES: '12' });
    const device = await newDevice();
    expect((await speak(device.token, { text: 'مرحبا', voice: 'reading' }, e)).res.status).toBe(200);
    const { res } = await speak(device.token, { text: 'hello', voice: 'reading' }, e);
    expect(res.status).toBe(402);
    expect(await res.json()).toMatchObject({ error: { type: 'quota_exceeded' } });
    expect(await usageOf(device.id)).toMatchObject({ tts_bytes: 10 });
  });

  it('gives the bytes back when the voice provider fails', async () => {
    upstream.mockImplementation(async () => new Response('{"status":503}', { status: 503 }));
    const device = await newDevice();
    const { res } = await speak(device.token, { text: 'hello', voice: 'reading' });
    expect(res.status).toBe(502);
    expect(await usageOf(device.id)).toMatchObject({ tts_bytes: 0, cost_micro: 0 });
  });

  it('checks the request before spending anything', async () => {
    const device = await newDevice();
    for (const body of [{ text: '', voice: 'reading' }, { text: 'hi', voice: 'singing' }, { text: 'hi', voice: 'reading', speed: 3 }, { text: 'x'.repeat(4001), voice: 'reading' }]) {
      expect((await speak(device.token, body)).res.status).toBe(400);
    }
    expect(upstream).not.toHaveBeenCalled();
  });

  it('says when no voice is set for a job yet', async () => {
    const device = await newDevice();
    const { res } = await speak(device.token, { text: 'hi', voice: 'reading' }, withEnv({ VOICE_READING: '' }));
    expect(res.status).toBe(503);
    expect(await res.json()).toMatchObject({ error: { type: 'not_configured' } });
  });
});
