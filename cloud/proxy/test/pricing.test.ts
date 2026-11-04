import { describe, expect, it } from 'vitest';
import { claudeCostMicro, emptyUsage, speechCostMicro, type Usage } from '../src/pricing';

const usage = (u: Partial<Usage>): Usage => ({ ...emptyUsage(), ...u });

describe('claudeCostMicro', () => {
  it('prices Haiku 5.5 on the short-prompt card ($0.10 / $0.50)', () => {
    expect(claudeCostMicro('claude-haiku-5-5', usage({ input_tokens: 1_000, output_tokens: 200 }))).toBe(100 + 100);
  });

  it('keeps Haiku 5.5 on the short card at exactly 100K prompt tokens', () => {
    expect(claudeCostMicro('claude-haiku-5-5', usage({ input_tokens: 100_000 }))).toBe(10_000);
  });

  it('moves Haiku 5.5 to the long card above 100K, counting cache reads in the prompt', () => {
    const u = usage({ input_tokens: 60_000, cache_read_input_tokens: 50_000, output_tokens: 1_000 });
    expect(claudeCostMicro('claude-haiku-5-5', u)).toBe(60_000 * 0.5 + 50_000 * 0.05 + 1_000 * 2.5);
  });

  it('bills cache writes at 1.25x for 5 minutes and 2x for 1 hour', () => {
    const u = usage({ cache_creation_input_tokens: 5_000, cache_write_1h_tokens: 1_000 });
    expect(claudeCostMicro('claude-sonnet-5-5', u)).toBe(4_000 * 2 * 1.25 + 1_000 * 2 * 2);
  });

  it('bills Opus 5.5 cache reads at 0.05x and Sonnet 5.5 at 0.1x, without float drift', () => {
    expect(claudeCostMicro('claude-opus-5-5', usage({ cache_read_input_tokens: 10_000 }))).toBe(2_000);
    expect(claudeCostMicro('claude-sonnet-5-5', usage({ cache_read_input_tokens: 10_000 }))).toBe(2_000);
  });

  it('rounds a fraction of a micro-dollar up', () => {
    expect(claudeCostMicro('claude-haiku-5-5', usage({ input_tokens: 3 }))).toBe(1);
  });

  it('refuses a model it has no price for', () => {
    expect(() => claudeCostMicro('claude-unknown', emptyUsage())).toThrow('unpriced_model');
  });
});

describe('speechCostMicro', () => {
  it('bills Fish Audio per UTF-8 byte', () => {
    expect(speechCostMicro(1_000, 15)).toBe(15_000);
    expect(speechCostMicro(3, 15.3)).toBe(46);
  });
});
