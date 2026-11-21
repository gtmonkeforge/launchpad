# Launchpad server

The small service behind `https://launchpad.monkeforge.org`. It does three things:

- **`/`** redirects to `https://monkeforge.org/software/launchpad` (`HOMEPAGE_URL`).
- **Sign-in.** Launchpad is a confidential OAuth2 app on MonkeForge. The app does the browser part itself (loopback redirect + PKCE) and sends the code to `POST /api/v1/oauth/token`, where this server adds the client secret and forwards it to MonkeForge. `POST /api/v1/oauth/revoke` works the same way. Only Launchpad's exact flow is accepted: a `http://127.0.0.1:<port>/callback` redirect and a PKCE verifier on every code.
- **Updates.** `GET /api/v1/updates/latest?os=windows&arch=x64` returns the newest release's manifest and its signature. Installers are served from `/download/<version>/<file>`, and `/download/latest/windows` and `/download/latest/linux` redirect to the newest ones, for download buttons.

## MonkeForge app settings

In **Settings → Developer → Applications → Launchpad** on monkeforge.org:

1. Add the redirect URI `http://127.0.0.1/callback`. Any port matches it.
2. Leave **Public client** off and create a client secret. Put it in this server's `.env` as `LAUNCHPAD_CLIENT_SECRET`.
3. Launchpad asks for the `account` scope, which only **verified** apps get. Verify the app in the admin panel.

## Deploying

```sh
dotnet publish src/Launchpad.Server -c Release -r linux-x64 --self-contained -o out
scp -r out server:/tmp/launchpad-server
ssh server sudo /tmp/launchpad-server/install.sh
```

`install.sh` installs a systemd service in `/opt/launchpad-server`, listening on `127.0.0.1:5080`, and creates `.env` from [`deploy/env.example`](deploy/env.example). Put your reverse proxy or Cloudflare Tunnel in front of it for `launchpad.monkeforge.org`. Running `install.sh` again upgrades the server and keeps `.env` and `data/`.

## Publishing a release

Releases are signed with an offline ECDSA key. Launchpad has the public half built in ([`Updater.ReleaseKey`](../Launchpad/Updates/Updater.cs)) and only installs what that key signed. If the server is compromised, it still can't push code to anyone.

```powershell
# once: create the key (kept at ~/.monkeforge/launchpad-release-key.pem; back it up)
dotnet run scripts/release.cs -- keygen

# each release: bump <Version> in Directory.Build.props, build, then sign and upload
./scripts/package.ps1
$env:LAUNCHPAD_PUBLISH_TOKEN = "<the server's PUBLISH_TOKEN>"
dotnet run scripts/release.cs -- publish notes.md
```

Installed copies check for updates when they start and every 4 hours. They download new versions in the background, then install them when the user clicks **Restart to update** or closes Launchpad. Published versions can't be replaced, so fix a bad release by publishing a newer one.

Releases are kept on disk in `data/releases/<version>/` (`release.json`, `release.json.sig` and the installers). You can also copy a signed folder there by hand.
