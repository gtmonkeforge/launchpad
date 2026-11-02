export type ModLoader = 'bepinex5' | 'melonloader';
export interface Download { filename: string; size: number; sha256: string; kind: 'dll' | 'zip'; url: string; loader?: ModLoader | 'bepinex6' }
export interface Release { version: string; notes: string; released_at: string; downloads: Download[]; downloads_by_loader?: Partial<Record<ModLoader, Download[]>> }
export interface Mod {
  can_transfer?: boolean; can_manage?: boolean; guid: string; id: string; name: string; description: string; description_html: string;
  category: string; current_version: string; downloads: number; logo: string | null; banner?: string | null; badges?: string[];
  screenshots: string[]; source_url: string | null; links: { label: string; url: string }[];
  author: { id: string; name: string; discord_username: string; avatar: string | null; is_verified: boolean; is_admin?: boolean };
  capabilities: Record<string, boolean>; releases: Release[]; channels: { name: string }[]; channel?: { name?: string; notes?: string };
}
export interface Installed { guid: string; name: string; version: string; files: string[]; folders: string[]; loader?: ModLoader }
