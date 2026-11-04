/** Token usage of one Messages API call, as reported by Anthropic. */
export interface Usage {
  input_tokens: number;
  output_tokens: number;
  cache_creation_input_tokens: number;
  cache_read_input_tokens: number;
  /** Part of cache_creation_input_tokens written with the 1-hour TTL (billed 2x instead of 1.25x). */
  cache_write_1h_tokens: number;
}

export const emptyUsage = (): Usage => ({
  input_tokens: 0,
  output_tokens: 0,
  cache_creation_input_tokens: 0,
  cache_read_input_tokens: 0,
  cache_write_1h_tokens: 0,
});

/**
 * Prices in thousandths of a USD per million tokens, so all arithmetic stays in whole numbers
 * (0.2 has no exact binary form; 200 does). Every input price is divisible by 4, so the 1.25x
 * cache-write price is whole too.
 */
interface Rate {
  input: number;
  output: number;
  cacheRead: number;
}

/**
 * Anthropic first-party prices (checked 2026-11-04). Haiku 5.5 has two rate cards chosen by the
 * prompt's length (input + cache writes + cache reads): $0.10 / $0.50 up to 100K tokens, $0.50 /
 * $2.50 above. Cache writes cost 1.25x input (5-minute TTL) or 2x (1-hour TTL); cache reads 0.1x
 * input, except 0.05x on Opus 5.5.
 */
const RATES: Record<string, (promptTokens: number) => Rate> = {
  'claude-haiku-5-5': (prompt) =>
    prompt > 100_000 ? { input: 500, output: 2_500, cacheRead: 50 } : { input: 100, output: 500, cacheRead: 10 },
  'claude-sonnet-5-5': () => ({ input: 2_000, output: 10_000, cacheRead: 200 }),
  'claude-opus-5-5': () => ({ input: 4_000, output: 20_000, cacheRead: 200 }),
};

export const PRICED_MODELS: readonly string[] = Object.keys(RATES);

/** Cost of one call in micro-dollars (millionths of a USD), rounded up. */
export function claudeCostMicro(model: string, u: Usage): number {
  const rateFor = RATES[model];
  if (!rateFor) throw new Error('unpriced_model');
  const prompt = u.input_tokens + u.cache_creation_input_tokens + u.cache_read_input_tokens;
  const r = rateFor(prompt);
  const write1h = Math.min(u.cache_write_1h_tokens, u.cache_creation_input_tokens);
  const write5m = u.cache_creation_input_tokens - write1h;
  // tokens x (thousandths of a USD per million tokens) = nano-USD; / 1000 = micro-USD
  const nano =
    u.input_tokens * r.input +
    u.output_tokens * r.output +
    u.cache_read_input_tokens * r.cacheRead +
    write5m * ((r.input * 5) / 4) +
    write1h * r.input * 2;
  return Math.ceil(nano / 1000);
}

/** Fish Audio bills per UTF-8 byte of text (Arabic letters are 2 bytes). */
export function speechCostMicro(bytes: number, usdPerMillionBytes: number): number {
  // Rounded to 3 decimals first so a price like 15.3 can't add a phantom micro-dollar.
  return Math.ceil(Math.round(bytes * usdPerMillionBytes * 1000) / 1000);
}
