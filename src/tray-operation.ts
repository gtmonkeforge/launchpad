/** Tray availability is optional; failure must leave the window accessible. */
export function attemptTrayOperation(operation: () => boolean, restoreWindow: () => void): boolean {
  try { if (operation()) return true; } catch { /* Native icon creation can fail too. */ }
  restoreWindow();
  return false;
}
