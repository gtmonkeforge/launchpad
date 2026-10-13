# MonkeForge Launchpad
You can get releases & information about MonkeForge Launchpad at https://monkeforge.org/app.

Launchpad includes Friends, Chat, Groups, and Computer tabs. Social features use the official
MonkeForge `/api/v1` API with the existing native login session. Publishing group members sign
in with their own accounts; group publishing opens the website's publisher selector.

Install Launchpad Connector in the configured Gorilla Tag installation to enable live room
sharing, in-game social notifications, and Computer settings. Launchpad writes a local discovery
file at `BepInEx/config/launchpad-connector.json` containing its loopback socket address and a
random connection token. The connector reconnects automatically. No MonkeForge credentials
are passed to the game. Room sharing defaults to None and expires when presence stops updating.

Computer settings apply while the game is connected: name, red/green/blue values from 0–9,
turn mode and speed, and All / Push to talk / Push to mute microphone modes. Name changes use
the game's checks. The tab reports disconnection and command failures instead of saving offline
changes that appear to have applied.

Development checks: `bun check`, `bun test`, and `bun run build:verify`. The shared social panel
is `ui/social.js`; keep it synchronized with `monkeforge-site/static/social.js`.
