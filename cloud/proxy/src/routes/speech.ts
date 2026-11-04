import { num, type Device, type Env } from '../env';
import { addSpeechBytes, budgetExhausted, recordSpeech } from '../limits';
import { apiError, sha256Hex, utf8Length } from '../util';

const FISH_URL = 'https://api.fish.audio/v1/tts';
const MAX_CHARS = 4000;

type Voice = 'conversation' | 'reading';

/**
 * POST /v1/tts: { "text": "...", "voice": "conversation" | "reading", "format"?: "mp3" | "opus",
 * "speed"?: 0.5-2 }. The app never names a provider or a voice id; the relay picks them from its
 * settings. Every result is cached, so the same sentence in the same voice is paid for once.
 */
export async function speech(request: Request, env: Env, ctx: ExecutionContext, device: Device): Promise<Response> {
  let body: { text?: unknown; voice?: unknown; format?: unknown; speed?: unknown };
  try {
    body = await request.json();
  } catch {
    return apiError(400, 'invalid_request_error', 'The request body must be JSON.');
  }

  const text = typeof body?.text === 'string' ? body.text : '';
  if (text.trim().length === 0 || text.length > MAX_CHARS) {
    return apiError(400, 'invalid_request_error', `text must be 1 to ${MAX_CHARS} characters.`);
  }
  const voice: Voice | null = body.voice === 'reading' || body.voice === 'conversation' ? body.voice : null;
  if (!voice) return apiError(400, 'invalid_request_error', 'voice must be "conversation" or "reading".');
  const format = body.format === 'opus' ? 'opus' : 'mp3';
  const speed = body.speed === undefined ? 1 : Number(body.speed);
  if (!(speed >= 0.5 && speed <= 2)) return apiError(400, 'invalid_request_error', 'speed must be between 0.5 and 2.');

  const voiceId = voice === 'reading' ? env.VOICE_READING : env.VOICE_CONVERSATION;
  if (!voiceId) return apiError(503, 'not_configured', `No ${voice} voice is set on the relay yet.`);
  const model = env.FISH_MODEL || 's2.1-pro';
  const contentType = format === 'mp3' ? 'audio/mpeg' : 'audio/ogg';
  const key = `tts/${await sha256Hex(JSON.stringify(['fish', model, voiceId, format, speed, text]))}.${format}`;

  const cached = await env.AUDIO.get(key);
  if (cached) return new Response(cached.body, { headers: { 'content-type': contentType, 'x-mubsir-cache': 'hit' } });

  if (await budgetExhausted(env)) return apiError(402, 'budget_exhausted', "This month's budget is used up.");
  const bytes = utf8Length(text);
  if ((await addSpeechBytes(env, device.id, bytes)) > num(env.DEVICE_DAILY_TTS_BYTES, 900_000)) {
    await addSpeechBytes(env, device.id, -bytes);
    return apiError(402, 'quota_exceeded', "This device has reached today's speech limit.");
  }

  const upstream = await fetch(FISH_URL, {
    method: 'POST',
    headers: { authorization: `Bearer ${env.FISH_API_KEY}`, 'content-type': 'application/json', model },
    body: JSON.stringify({
      text,
      reference_id: voiceId,
      format,
      ...(format === 'mp3' ? { mp3_bitrate: 64 } : {}),
      latency: voice === 'conversation' ? 'balanced' : 'normal',
      normalize: true,
      prosody: { speed },
    }),
    signal: request.signal,
  });
  if (!upstream.ok || !upstream.body) {
    await addSpeechBytes(env, device.id, -bytes);
    return apiError(502, 'upstream_error', `The voice provider answered ${upstream.status}.`);
  }

  ctx.waitUntil(recordSpeech(env, device.id, bytes).catch(() => undefined));
  // One copy streams to the user right away; the other is saved once complete.
  const [toUser, toCache] = upstream.body.tee();
  ctx.waitUntil(
    new Response(toCache)
      .arrayBuffer()
      .then((audio) => env.AUDIO.put(key, audio, { httpMetadata: { contentType } }))
      .catch(() => undefined),
  );
  return new Response(toUser, { headers: { 'content-type': contentType, 'x-mubsir-cache': 'miss' } });
}
