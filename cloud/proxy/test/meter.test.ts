import { describe, expect, it } from 'vitest';
import { meteredStream } from '../src/meter';
import type { Usage } from '../src/pricing';
import { SSE } from './fixtures';

function chunked(text: string, size: number): ReadableStream<Uint8Array> {
  const bytes = new TextEncoder().encode(text);
  return new ReadableStream<Uint8Array>({
    start(controller) {
      for (let i = 0; i < bytes.length; i += size) controller.enqueue(bytes.slice(i, i + size));
      controller.close();
    },
  });
}

describe('meteredStream', () => {
  it('passes every byte through unchanged and reads usage, whatever the chunk boundaries', async () => {
    for (const size of [1, 3, 7, 64, 100_000]) {
      const reports: Usage[] = [];
      const out = await new Response(meteredStream(chunked(SSE, size), (u) => reports.push({ ...u }))).text();
      expect(out).toBe(SSE);
      expect(reports).toHaveLength(1);
      expect(reports[0]).toMatchObject({ input_tokens: 120, cache_read_input_tokens: 800, output_tokens: 42 });
    }
  });

  it('reports once when the client goes away mid-stream', async () => {
    const reports: Usage[] = [];
    const reader = meteredStream(chunked(SSE, 16), (u) => reports.push({ ...u })).getReader();
    await reader.read();
    await reader.cancel('client left');
    expect(reports).toHaveLength(1);
  });

  it('ignores lines that are not JSON', async () => {
    const reports: Usage[] = [];
    const text = 'event: ping\ndata: not json\n\n' + SSE;
    expect(await new Response(meteredStream(chunked(text, 5), (u) => reports.push({ ...u }))).text()).toBe(text);
    expect(reports[0]?.output_tokens).toBe(42);
  });
});
