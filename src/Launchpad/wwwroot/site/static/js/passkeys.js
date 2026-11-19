import { api } from './core.js';

export const passkeysSupported = () => typeof window.PublicKeyCredential === 'function' && !!navigator.credentials?.create;

const toB64 = buffer => {
    const bytes = new Uint8Array(buffer);
    let binary = '';

    for (const byte of bytes)
        binary += String.fromCharCode(byte);

    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
};

const fromB64 = value => {
    const padded = value.replace(/-/g, '+').replace(/_/g, '/') + '='.repeat((4 - value.length % 4) % 4);
    return Uint8Array.from(atob(padded), c => c.charCodeAt(0));
};

// Browsers reject a cancelled or timed-out prompt with NotAllowedError; that isn't worth an error toast.
export const cancelled = error => error?.name === 'NotAllowedError' || error?.name === 'AbortError';

function explain(error) {
    if (error?.name === 'InvalidStateError')
        return new Error('This passkey is already on your account.');

    if (error?.name === 'SecurityError')
        return new Error('Passkeys only work on this site’s main address. Open it there and try again.');

    return error;
}

export async function registerPasskey(name) {
    const options = await api('/api/v1/passkeys/options', { method: 'POST' });
    let credential;

    try {
        credential = await navigator.credentials.create({
            publicKey: {
                challenge: fromB64(options.challenge),
                rp: options.rp,
                user: { id: fromB64(options.user.id), name: options.user.name, displayName: options.user.display_name },
                pubKeyCredParams: options.algorithms.map(alg => ({ type: 'public-key', alg })),
                timeout: options.timeout,
                attestation: 'none',
                authenticatorSelection: { residentKey: 'required', requireResidentKey: true, userVerification: 'preferred' },
                excludeCredentials: options.exclude.map(id => ({ type: 'public-key', id: fromB64(id) })),
            },
        });
    } catch (error) {
        throw explain(error);
    }

    const response = credential.response;
    const publicKey = response.getPublicKey?.();

    if (!publicKey || !response.getAuthenticatorData)
        throw new Error('This browser can’t finish adding a passkey. Update it, or try another browser.');

    return api('/api/v1/passkeys', {
        method: 'POST',
        json: {
            name,
            id: toB64(credential.rawId),
            client_data_json: toB64(response.clientDataJSON),
            authenticator_data: toB64(response.getAuthenticatorData()),
            public_key: toB64(publicKey),
            algorithm: response.getPublicKeyAlgorithm(),
            transports: response.getTransports?.() || [],
        },
    });
}

// The challenge is base64url bytes; clientDataJSON echoes it back base64url-encoded, which is the string the server stored.
export async function signInWithPasskey(extra = {}) {
    const options = await api('/api/v1/auth/passkey/options', { method: 'POST' });
    const credential = await navigator.credentials.get({
        publicKey: {
            challenge: fromB64(options.challenge),
            rpId: options.rp_id,
            timeout: options.timeout,
            userVerification: options.user_verification,
            allowCredentials: [],
        },
    }).catch(error => { throw explain(error); });

    const response = credential.response;
    return api('/api/v1/auth/passkey', {
        method: 'POST',
        json: {
            ...extra,
            id: toB64(credential.rawId),
            client_data_json: toB64(response.clientDataJSON),
            authenticator_data: toB64(response.authenticatorData),
            signature: toB64(response.signature),
            user_handle: response.userHandle ? toB64(response.userHandle) : '',
        },
    });
}
