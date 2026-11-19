export const passkeysSupported = () => false;

export const cancelled = error => error?.name === 'NotAllowedError' || error?.name === 'AbortError';

export async function registerPasskey() {
    throw new Error('Add passkeys on monkeforge.org.');
}

export async function signInWithPasskey() {
    throw new Error('Sign in with a passkey from your browser.');
}
