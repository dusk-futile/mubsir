import { emptyUsage, type Usage } from './pricing';

const FIELDS = ['input_tokens', 'output_tokens', 'cache_creation_input_tokens', 'cache_read_input_tokens'] as const;

/** Copies the numbers present in an Anthropic `usage` object; later events win (output is cumulative). */
export function mergeUsage(into: Usage, usage: unknown): void {
  if (!usage || typeof usage !== 'object') return;
  const u = usage as Record<string, unknown>;
  for (const field of FIELDS) {
    const value = u[field];
    if (typeof value === 'number') into[field] = value;
  }
  const creation = u['cache_creation'] as Record<string, unknown> | undefined;
  const oneHour = creation?.['ephemeral_1h_input_tokens'];
  if (typeof oneHour === 'number') into.cache_write_1h_tokens = oneHour;
}

/** Reads usage out of one SSE `data:` payload (message_start and message_delta carry it). */
export function scanEvent(data: string, usage: Usage): void {
  let event: { type?: string; message?: { usage?: unknown }; usage?: unknown };
  try {
    event = JSON.parse(data);
  } catch {
    return;
  }
  if (event?.type === 'message_start') mergeUsage(usage, event.message?.usage);
  else if (event?.type === 'message_delta') mergeUsage(usage, event.usage);
}

/**
 * Passes an SSE stream through byte for byte while reading the usage numbers out of it.
 * `onDone` runs exactly once: when the stream ends, fails, or the client goes away.
 */
export function meteredStream(body: ReadableStream<Uint8Array>, onDone: (usage: Usage) => void): ReadableStream<Uint8Array> {
  const reader = body.getReader();
  const decoder = new TextDecoder();
  const usage = emptyUsage();
  let buffer = '';
  let finished = false;

  const scanLines = (text: string) => {
    buffer += text;
    let newline: number;
    while ((newline = buffer.indexOf('\n')) >= 0) {
      const line = buffer.slice(0, newline).replace(/\r$/, '');
      buffer = buffer.slice(newline + 1);
      if (line.startsWith('data:')) scanEvent(line.slice(5).trim(), usage);
    }
  };
  const finish = () => {
    if (finished) return;
    finished = true;
    scanLines(decoder.decode());
    if (buffer.startsWith('data:')) scanEvent(buffer.slice(5).trim(), usage);
    onDone(usage);
  };

  return new ReadableStream<Uint8Array>({
    async pull(controller) {
      try {
        const { value, done } = await reader.read();
        if (done) {
          finish();
          controller.close();
          return;
        }
        scanLines(decoder.decode(value, { stream: true }));
        controller.enqueue(value);
      } catch (err) {
        finish();
        controller.error(err);
      }
    },
    async cancel(reason) {
      finish();
      await reader.cancel(reason);
    },
  });
}
