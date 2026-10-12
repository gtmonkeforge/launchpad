import { expect, test } from 'bun:test';
import { assemblyVersion } from '../src/loader';
function managedPe(version: number[], pe32Plus = false) {
  const bytes = new Uint8Array(2048), v = new DataView(bytes.buffer);
  const w16 = (p: number, n: number) => v.setUint16(p, n, true), w32 = (p: number, n: number) => v.setUint32(p, n, true);
  w16(0, 0x5a4d); w32(0x3c, 128); w32(128, 0x4550); w16(134, 1);
  const optional = 152, optionalSize = pe32Plus ? 240 : 224;
  w16(148, optionalSize); w16(optional, pe32Plus ? 0x20b : 0x10b);
  const sections = optional + optionalSize;
  w32(sections + 12, 0x2000); w32(sections + 16, 1536); w32(sections + 20, 512);
  w32(optional + (pe32Plus ? 112 : 96) + 14 * 8, 0x2000);
  w32(520, 0x2040);
  const metadata = 576;
  w32(metadata, 0x424a5342); w32(metadata + 12, 4);
  bytes.set(new TextEncoder().encode('v4\0\0'), metadata + 16);
  w16(metadata + 22, 1); w32(metadata + 24, 64);
  bytes.set(new TextEncoder().encode('#~\0'), metadata + 32);
  const table = metadata + 64;
  v.setBigUint64(table + 8, (1n << 0n) | (1n << 32n), true);
  w32(table + 24, 1); w32(table + 28, 1);
  // Include a Module row before the Assembly row to exercise table skipping.
  const assembly = table + 32 + 10;
  version.forEach((n, i) => w16(assembly + 4 + i * 2, n));
  return bytes;
}
test('reads installed managed assembly versions from PE32 and PE32+', () => {
  expect(assemblyVersion(managedPe([5, 4, 23, 3]))).toBe('5.4.23.3');
  expect(assemblyVersion(managedPe([6, 0, 0, 1], true))).toBe('6.0.0.1');
});
test('does not invent a version for malformed or truncated binaries', () => {
  expect(assemblyVersion(new Uint8Array([0, 1]))).toBeNull();
  expect(assemblyVersion(managedPe([5, 4, 0, 0]).subarray(0, 256))).toBeNull();
});
