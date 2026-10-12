export interface Download { filename: string; size: number; sha256: string; kind: 'dll' | 'zip'; url: string }
export interface Release { version: string; notes: string; released_at: string; downloads: Download[] }
export interface Mod {
  guid: string; id: string; name: string; description: string; description_html: string;
  category: string; current_version: string; downloads: number; logo: string | null;
  screenshots: string[]; source_url: string | null; links: { label: string; url: string }[];
  author: { id: string; name: string; discord_username: string; avatar: string | null; is_verified: boolean };
  capabilities: Record<string, boolean>; releases: Release[]; channels: { name: string }[]; channel?: { notes?: string };
}
export interface Installed { guid: string; name: string; version: string; files: string[]; folders: string[] }
