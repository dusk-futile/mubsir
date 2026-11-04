/** A Messages API stream: usage in message_start (input, cache) and message_delta (output, cumulative). */
export const SSE = [
  'event: message_start\r\n',
  'data: {"type":"message_start","message":{"id":"msg_1","type":"message","usage":{"input_tokens":120,"cache_read_input_tokens":800,"cache_creation_input_tokens":0,"output_tokens":1}}}\r\n\r\n',
  'event: content_block_delta\n',
  'data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"فتحت الإعدادات"}}\n\n',
  'event: message_delta\n',
  'data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":42}}\n\n',
  'event: message_stop\n',
  'data: {"type":"message_stop"}',
].join('');

/** Haiku 5.5 cost of SSE above: 120 x 0.10 + 800 x 0.01 + 42 x 0.50 = 41 micro-USD. */
export const SSE_COST_MICRO = 41;
