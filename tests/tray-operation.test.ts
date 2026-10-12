import { expect, test } from 'bun:test';
import { attemptTrayOperation } from '../src/tray-operation';

test('tray creation exceptions and registration failures restore access without propagating errors', () => {
  let restored = 0;
  const restore = () => { restored++; };
  expect(attemptTrayOperation(() => { throw new Error('Could not create the Launchpad tray icon'); }, restore)).toBe(false);
  expect(attemptTrayOperation(() => false, restore)).toBe(false);
  expect(restored).toBe(2);
  expect(attemptTrayOperation(() => true, restore)).toBe(true);
  expect(restored).toBe(2);
});
