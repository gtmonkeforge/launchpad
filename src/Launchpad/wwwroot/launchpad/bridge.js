const boot = JSON.parse(document.querySelector('#boot')?.textContent || '{}');
export const launchpad = boot.launchpad || {};

const pending = new Map();
const listeners = new Map();
let sequence = 0;

function post(message) {
    window.external.sendMessage(JSON.stringify({ token: launchpad.token, ...message }));
}

export function call(method, args = {}) {
    return new Promise((resolve, reject) => {
        const id = ++sequence;
        pending.set(id, { resolve, reject });

        try {
            post({ id, method, args });
        } catch {
            pending.delete(id);
            reject(new Error('Launchpad’s window lost its connection. Restart Launchpad.'));
        }
    });
}

export function send(method, args = {}) {
    try {
        post({ method, args });
    } catch {
    }
}

export function on(name, listener) {
    if (!listeners.has(name))
        listeners.set(name, new Set());

    listeners.get(name).add(listener);
}

window.external?.receiveMessage?.(text => {
    let message;

    try {
        message = JSON.parse(text);
    } catch {
        return;
    }

    if (message.event) {
        listeners.get(message.event)?.forEach(listener => {
            try {
                listener(message.data);
            } catch (error) {
                console.error(error);
            }
        });
        return;
    }

    const waiting = pending.get(message.id);

    if (!waiting)
        return;

    pending.delete(message.id);

    if (message.ok)
        waiting.resolve(message.result);
    else
        waiting.reject(Object.assign(new Error(message.error || 'Something went wrong.'), { status: message.status || 0 }));
});

let stateRequest;
let installedRequest;

export function appState(fresh = false) {
    if (fresh || !stateRequest)
        stateRequest = call('app.state');

    return stateRequest;
}

export function installedMods(fresh = false) {
    if (fresh || !installedRequest)
        installedRequest = call('mods.installed').catch(() => []);

    return installedRequest;
}

on('mods', () => {
    stateRequest = null;
    installedRequest = null;
});

// Pages switch without a reload, so listeners a page adds should pass this signal to be removed when it's left.
let pageController = new AbortController();
window.addEventListener('launchpad:navigate', () => {
    pageController.abort();
    pageController = new AbortController();
});

export const pageSignal = () => pageController.signal;

export const maxCustomBytes = 64 * 1024 * 1024;

function base64(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result).slice(String(reader.result).indexOf(',') + 1));
        reader.onerror = () => reject(new Error(`Launchpad couldn’t read ${file.name}.`));
        reader.readAsDataURL(file);
    });
}

let installingFiles = false;

export async function installDllFiles(files, toast) {
    files = [...files];

    if (!files.length)
        return;

    if (installingFiles) {
        toast('Launchpad is already installing a DLL. Wait for it to finish.', 'error');
        return;
    }

    installingFiles = true;

    try {
        for (const file of files) {
            if (!/\.dll$/i.test(file.name)) {
                toast(`${file.name} isn’t a .dll file.`, 'error');
                continue;
            }

            if (file.size > maxCustomBytes) {
                toast(`${file.name} is too large to install.`, 'error');
                continue;
            }

            try {
                const result = await call('mods.installFile', { name: file.name, data: await base64(file) });
                toast(`${result.name} is installed.`);
            } catch (error) {
                toast(error.message, 'error');
            }
        }
    } finally {
        installingFiles = false;
    }
}

export const loaderNames ={ bepinex5: 'BepInEx 5', melonloader: 'MelonLoader' };
