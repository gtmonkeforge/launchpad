import type { Download, Installed, ModLoader, Release } from './types';
export const loaderNames: Record<ModLoader, string> = { bepinex5: 'BepInEx 5', melonloader: 'MelonLoader' };
export const loaderRecord = (loader: ModLoader) => loader === 'melonloader' ? 'melonloader' : 'bepinex';
export const loaderProxy = (loader: ModLoader) => loader === 'melonloader' ? 'version' as const : 'winhttp' as const;
export const pluginFolders = (loader: ModLoader) => loader === 'melonloader' ? ['Mods', 'Plugins'] : ['BepInEx/plugins'];
export function installedModLoader(mod: Installed): ModLoader {
  return mod.loader || ([...mod.files, ...mod.folders].some(path => /(?:^|[\\/])(Mods|Plugins)[\\/]/i.test(path) && !/(?:^|[\\/])BepInEx[\\/]/i.test(path)) ? 'melonloader' : 'bepinex5');
}
export function selectDownload(release: Release, loader: ModLoader): Download {
  const files = release.downloads_by_loader?.[loader] || release.downloads.filter(file => file.loader === loader || (loader === 'bepinex5' && !file.loader));
  const file = files.find(file => file.kind === 'dll' || file.kind === 'zip');
  if (!file) throw new Error(`This release has no ${loaderNames[loader]} download. Choose another loader in Settings.`);
  return file;
}
/** Keep rooted mod archives inside mod directories, never inside either loader's runtime. */
export function modEntries(archive: Record<string, Uint8Array>, loader: ModLoader, guid: string): Record<string, Uint8Array> {
  const normalized = Object.entries(archive).map(([name, data]) => [name.replace(/\\/g, '/'), data] as const);
  const rooted = normalized.some(([name]) => /^(BepInEx|Mods|Plugins|UserData)\//i.test(name));
  const entries: Record<string, Uint8Array> = {};
  for (const [name, data] of normalized) {
    if (rooted) {
      if (!(loader === 'melonloader' ? /^(Mods|Plugins|UserData)\//i : /^BepInEx\/(plugins|patchers|config)\//i).test(name)) throw new Error('Mod archive contains files outside the selected loader’s mod folders');
      const canonical = name.replace(/^mods\//i, 'Mods/').replace(/^plugins\//i, 'Plugins/').replace(/^userdata\//i, 'UserData/').replace(/^bepinex\/(plugins|patchers|config)\//i, (_, folder: string) => 'BepInEx/' + folder.toLowerCase() + '/');
      entries[canonical] = data;
    } else entries[`${loader === 'melonloader' ? 'Mods' : 'BepInEx/plugins/' + guid}/${name}`] = data;
  }
  return entries;
}
