import { expect, test } from 'bun:test';
import { trayPixels } from '../src/linux-tray';
test('Linux tray pixmaps preserve the logo alpha and accent in network-order ARGB32', () => {
  const alpha = new Uint8Array(1024); alpha[0] = 255; alpha[1] = 128;
  const pixels = trayPixels(alpha, '#dc5c3b');
  expect(pixels).toHaveLength(4096);
  expect(pixels.slice(0, 12)).toEqual([255, 220, 92, 59, 128, 220, 92, 59, 0, 220, 92, 59]);
  expect(() => trayPixels(new Uint8Array(1), '#dc5c3b')).toThrow();
  expect(() => trayPixels(alpha, 'invalid')).toThrow();
});
