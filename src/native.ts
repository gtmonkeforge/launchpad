/** The deployment build replaces this module with a statically bundled import. */
export async function loadNativeWebview(): Promise<any> {
  const moduleName = 'webview-bun';
  return import(moduleName);
}
