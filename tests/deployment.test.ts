import { afterAll, expect, test } from 'bun:test';
import { mkdtemp, readFile, writeFile, rm } from 'node:fs/promises';
import { basename, dirname, join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { materializeNativeLibrary } from '../src/deployment';
const temp = await mkdtemp(join(tmpdir(), 'launchpad-native-cache-'));
afterAll(() => {
  if (dirname(resolve(temp)) !== resolve(tmpdir()) || !basename(temp).startsWith('launchpad-native-cache-')) throw new Error('Invalid native cache test cleanup path');
  return rm(temp, { recursive: true, force: true });
});
test('extracts embedded native bytes to a stable content-addressed cache', async () => {
  const input = join(temp, 'library.bin'); await writeFile(input, new Uint8Array([1, 2, 3, 4]));
  const file = await materializeNativeLibrary(input, join(temp, 'cache'));
  expect(await readFile(file)).toEqual(Buffer.from([1, 2, 3, 4]));
  expect(await materializeNativeLibrary(input, join(temp, 'cache'))).toBe(file);
});
test('refuses to load modified bytes from a native cache entry', async () => {
  const input = join(temp, 'tamper.bin'); await writeFile(input, new Uint8Array([5, 6, 7]));
  const file = await materializeNativeLibrary(input, join(temp, 'cache'));
  await writeFile(file, new Uint8Array([0]));
  await expect(materializeNativeLibrary(input, join(temp, 'cache'))).rejects.toThrow('checksum');
});
test('concurrent extraction resolves to the same verified library', async () => {
  const input = join(temp, 'concurrent.bin'); await writeFile(input, new Uint8Array([8, 9, 10]));
  const files = await Promise.all(Array.from({ length: 4 }, () => materializeNativeLibrary(input, join(temp, 'concurrent-cache'))));
  expect(new Set(files).size).toBe(1);
  expect(await readFile(files[0]!)).toEqual(Buffer.from([8, 9, 10]));
});
