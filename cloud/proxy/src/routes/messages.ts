import { list, num, type Device, type Env } from '../env';
import { budgetExhausted, countRequest, recordClaude } from '../limits';
import { meteredStream, mergeUsage } from '../meter';
import { emptyUsage, PRICED_MODELS, type Usage } from '../pricing';
import { apiError } from '../util';

const ANTHROPIC_URL = 'https://api.anthropic.com/v1/messages';
const PASS_HEADERS = ['content-type', 'request-id', 'retry-after'];

/**
 * POST /v1/messages: the same path and body as Anthropic's Messages API, so the app's Anthropic SDK
 * only changes its base URL and sends the device token as its API key. The relay checks the request,
 * swaps in the server's key, forwards the body unchanged and streams the answer back unchanged,
 * reading the usage on the way to bill the device.
 */
export async function messages(request: Request, env: Env, ctx: ExecutionContext, device: Device): Promise<Response> {
  const maxBytes = num(env.MAX_BODY_BYTES, 10_000_000);
  if (num(request.headers.get('content-length') ?? undefined, 0) > maxBytes) {
    return apiError(413, 'request_too_large', `Requests are limited to ${maxBytes} bytes.`);
  }
  const text = await request.text();
  if (text.length > maxBytes) return apiError(413, 'request_too_large', `Requests are limited to ${maxBytes} bytes.`);

  let body: {
    model?: unknown;
    max_tokens?: unknown;
    stream?: unknown;
    speed?: unknown;
    tools?: unknown;
    mcp_servers?: unknown;
    container?: unknown;
  };
  try {
    body = JSON.parse(text);
  } catch {
    return apiError(400, 'invalid_request_error', 'The request body must be JSON.');
  }

  const model = body?.model;
  if (typeof model !== 'string' || !list(env.ALLOWED_MODELS).includes(model)) {
    return apiError(400, 'invalid_request_error', `This model is not enabled on the relay: ${String(model)}.`);
  }
  if (!PRICED_MODELS.includes(model)) return apiError(500, 'api_error', 'The relay has no price for this model.');
  const maxOut = num(env.MAX_OUTPUT_TOKENS, 16_000);
  if (typeof body.max_tokens !== 'number' || body.max_tokens < 0 || body.max_tokens > maxOut) {
    return apiError(400, 'invalid_request_error', `max_tokens must be between 0 and ${maxOut}.`);
  }
  if (body.speed !== undefined) return apiError(400, 'invalid_request_error', 'Fast mode is not enabled on the relay.');
  if (body.mcp_servers !== undefined || body.container !== undefined) {
    return apiError(400, 'invalid_request_error', 'MCP servers and containers are not enabled on the relay.');
  }
  // Tools Anthropic runs itself (web search, code execution, ...) are billed apart from tokens, so
  // only the types the owner lists are let through. The app's own tools have no type (or "custom").
  const toolTypes = (Array.isArray(body.tools) ? body.tools : [])
    .map((tool: unknown) => (tool && typeof tool === 'object' ? (tool as { type?: unknown }).type : undefined))
    .filter((type): type is string => typeof type === 'string' && type !== 'custom');
  const allowedTools = list(env.ALLOWED_TOOL_TYPES);
  const blockedTool = toolTypes.find((type) => !allowedTools.includes(type));
  if (blockedTool) return apiError(400, 'invalid_request_error', `This tool is not enabled on the relay: ${blockedTool}.`);

  const betas = list(request.headers.get('anthropic-beta') ?? undefined);
  const allowedBetas = list(env.ALLOWED_BETAS);
  const blocked = betas.find((b) => !allowedBetas.includes(b));
  if (blocked) return apiError(400, 'invalid_request_error', `This beta is not enabled on the relay: ${blocked}.`);

  if (await budgetExhausted(env)) return apiError(402, 'budget_exhausted', "This month's budget is used up.");
  if ((await countRequest(env, device.id)) > num(env.DEVICE_DAILY_REQUESTS, 500)) {
    return apiError(402, 'quota_exceeded', "This device has reached today's limit.");
  }

  const headers = new Headers({
    'content-type': 'application/json',
    'x-api-key': env.ANTHROPIC_API_KEY,
    'anthropic-version': request.headers.get('anthropic-version') || env.ANTHROPIC_VERSION || '2026-10-21',
  });
  if (betas.length > 0) headers.set('anthropic-beta', betas.join(','));

  const upstream = await fetch(ANTHROPIC_URL, { method: 'POST', headers, body: text, signal: request.signal });

  const out = new Headers();
  for (const name of PASS_HEADERS) {
    const value = upstream.headers.get(name);
    if (value) out.set(name, value);
  }

  // Anthropic errors (and their usage-free bodies) go back unchanged.
  if (!upstream.ok || !upstream.body) return new Response(upstream.body, { status: upstream.status, headers: out });

  const record = (usage: Usage) => ctx.waitUntil(recordClaude(env, device.id, model, usage).catch(() => undefined));

  if (body.stream === true) {
    return new Response(meteredStream(upstream.body, record), { status: upstream.status, headers: out });
  }

  const answer = await upstream.text();
  try {
    const usage = emptyUsage();
    mergeUsage(usage, (JSON.parse(answer) as { usage?: unknown }).usage);
    record(usage);
  } catch {
    // An answer we cannot parse is still passed on; it has no usage to record.
  }
  return new Response(answer, { status: upstream.status, headers: out });
}
