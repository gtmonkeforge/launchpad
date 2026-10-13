import { readFile } from 'node:fs/promises';
import { join } from 'node:path';

export function assemblyVersion(bytes: Uint8Array): string | null {
  try {
    const v = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    const u16 = (p: number) => v.getUint16(p, true), u32 = (p: number) => v.getUint32(p, true);
    if (u16(0) !== 0x5a4d) return null;
    const pe = u32(0x3c);
    if (u32(pe) !== 0x4550) return null;
    const sections = u16(pe + 6), optional = pe + 24, magic = u16(optional);
    if (magic !== 0x10b && magic !== 0x20b) return null;
    const sectionTable = optional + u16(pe + 20);
    const rva = (address: number) => {
      for (let i = 0; i < sections; i++) {
        const p = sectionTable + i * 40, start = u32(p + 12), size = u32(p + 16);
        if (address >= start && address < start + size) return u32(p + 20) + address - start;
      }
      throw new Error('RVA is outside the PE sections');
    };
    const clr = rva(u32(optional + (magic === 0x10b ? 96 : 112) + 14 * 8));
    const metadata = rva(u32(clr + 8));
    if (u32(metadata) !== 0x424a5342) return null;
    const align = (n: number) => (n + 3) & ~3;
    let p = align(metadata + 16 + u32(metadata + 12));
    const streams = u16(p + 2); p += 4;
    let table = 0;
    for (let i = 0; i < streams; i++) {
      const offset = u32(p); p += 8;
      const start = p;
      while (v.getUint8(p)) p++;
      const name = new TextDecoder().decode(bytes.subarray(start, p)); p = align(p + 1);
      if (name === '#~' || name === '#-') table = metadata + offset;
    }
    if (!table) return null;
    const heaps = v.getUint8(table + 6), valid = v.getBigUint64(table + 8, true), rows = Array<number>(64).fill(0);
    p = table + 24;
    for (let i = 0; i < 64; i++) if (valid & (1n << BigInt(i))) { rows[i] = u32(p); p += 4; }
    if (!rows[32]) return null;
    const str = heaps & 1 ? 4 : 2, guid = heaps & 2 ? 4 : 2, blob = heaps & 4 ? 4 : 2;
    const idx = (n: number) => rows[n]! >= 65536 ? 4 : 2;
    const coded = (bits: number, tables: number[]) => tables.some(n => rows[n]! >= 2 ** (16 - bits)) ? 4 : 2;
    const type = coded(2, [2, 1, 27]), method = coded(1, [6, 10]);
    const sizes = [
      2 + str + guid * 3, coded(2, [0, 26, 35, 1]) + str * 2,
      4 + str * 2 + type + idx(4) + idx(6), idx(4), 2 + str + blob, idx(6),
      8 + str + blob + idx(8), idx(8), 4 + str, idx(2) + type,
      coded(3, [2, 1, 26, 6, 27]) + str + blob, 2 + coded(2, [4, 8, 23]) + blob,
      coded(5, [6, 4, 1, 2, 8, 9, 10, 0, 14, 23, 20, 17, 26, 27, 32, 35, 38, 39, 40, 42, 44, 43]) + coded(3, [6, 10]) + blob,
      coded(1, [4, 8]) + blob, 2 + coded(2, [2, 6, 32]) + blob,
      6 + idx(2), 4 + idx(4), blob, idx(2) + idx(20), idx(20), 2 + str + type,
      idx(2) + idx(23), idx(23), 2 + str + blob, 2 + idx(6) + coded(1, [20, 23]),
      idx(2) + method * 2, str, blob, 2 + coded(1, [4, 6]) + str + idx(26), 4 + idx(4), 8, 4,
    ];
    for (let i = 0; i < 32; i++) p += rows[i]! * sizes[i]!;
    return [u16(p + 4), u16(p + 6), u16(p + 8), u16(p + 10)].join('.');
  } catch { return null; }
}
export async function installedLoaderVersion(game: string, recorded?: string) {
  if (!game) return null;
  const bytes = await readFile(join(game, 'BepInEx/core/BepInEx.dll')).catch(() => null);
  if (!bytes) return null;
  return recorded?.replace(/^v/, '') || assemblyVersion(bytes);
}
