# Changelog

All notable changes to the VoiceAttack Discord Plugin are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
When cutting a release, add a new section at the top — the CI release workflow
extracts the matching `## [x.y.z]` block into the GitHub release notes automatically.

## [Unreleased]

## [1.4.0] - 2026-09-12

### Added
- Startup update check: on every load the plugin queries the GitHub Releases
  API once and logs `You're up to date (vX.Y.Z).` or
  `Update available: vX.Y.Z (you have v…). Download: <releases/latest>` in
  green. Offline/API failure surfaces as a soft yellow line
  (`Couldn't check for updates (offline?)…`) and never interrupts anything.
  Nothing is uploaded — the request is anonymous (no token or user data leaves
  your PC).

### Changed
- Plugin assembly and install folder renamed from `VA.VoiceAttackDiscordPlugin`
  to `VoiceAttackDiscordPlugin`: the legacy `VA.` prefix was never a
  VoiceAttack requirement (any `Apps` subfolder whose DLL exports the `VA_*`
  plugin methods loads), so it is gone from files, folders, and docs. The
  `VA_*` method names in code stay — that part is the real VoiceAttack plugin
  API.
- Setup wizard no longer hardcodes the install folder: it derives the name
  from the plugin payload's own assembly name at runtime, and migrates
  `config.json` from both legacy folders (`VA.DiscordVAPlugin` and
  `VA.VoiceAttackDiscordPlugin`) with optional cleanup.

### Upgrading (1.3.x → 1.4.0)
- One-click: run the 1.4.0 setup wizard — it moves your token/config across
  and removes the old folder. Manual: install into
  `Apps\VoiceAttackDiscordPlugin\`, copy `config.json` yourself, and delete
  the old folder so VoiceAttack doesn't load two copies of the plugin.

## [1.3.2] - 2026-09-11

### Changed
- First-launch docs (README, SETUP.md, release notes): the Windows
  SmartScreen prompt on first run is now explained, with checksum
  verification pointers.
- Documentation and example cleanups.

## [1.3.1] - 2026-09-11

### Fixed
- `sendmessage`/`senddm` no longer silently drop message text after a colon
  (URLs, times) — the parsed tail is rejoined before sending.
- Default-channel fallback documented correctly: use the double-colon form
  (`sendmessage::<message>`); a single colon puts the text in the channel slot.
- "Connected" is now logged only after the Discord gateway READY handshake
  (15s timeout) in both manual and auto-connect paths.
- Setup wizard re-runs the install if the VoiceAttack path changed since the
  last install step, instead of writing config.json to the stale folder.
- Setup build ordering: a `ProjectReference` guarantees the plugin builds
  before payload embedding, plus a fail-fast error on an empty payload.
- `callbyusername` now matches the raw username only (a shared display name
  can no longer win over the real username).
- Wizard invite link drops the unused Move Members permission and adds the
  documented View Channel + Use Voice Activity permissions.

### Changed
- Wizard save preserves existing `AutoConnect`/`LogLevel` instead of
  resetting them, and restores the encrypted token blob if the token box is
  cleared after a stray keystroke.
- `build.bat` now also publishes the single-file setup wizard.

## [1.3.0] - 2026-09-11

### Added
- Setup wizard Discord dark mode by default, with a Light/Dark toggle in the
  header (all steps, step indicator, and validation colors re-theme live).
- `CHANGELOG.md` (Keep a Changelog format); the CI release workflow now injects
  the matching version section into every GitHub release under "What's new".

### Changed
- Setup wizard visual refresh: Discord blurple primary buttons, painted 5-node
  step indicator, header accent rule, footer button band, AA-safe status colors
  with text prefixes, per-step keyboard focus, Enter-to-advance.
- Back button is hidden on the first and last wizard steps (it did nothing there).
- Per-monitor DPI awareness so wizard text stays crisp on scaled displays.
- README links to the changelog.

## [1.2.1] - 2026-09-11

### Changed
- Documentation sync: README and SETUP now describe the user/bot mute split
  (`mute`/`deafen` vs `botmute`/`botdeafen`), the call-navigation actions
  (`callbyid`, `callbyusername`, `callbyname`), and rename troubleshooting.

## [1.2.0] - 2026-09-11

### Added
- User mute/deafen toggles (`mute`, `deafen`) via `Ctrl+Shift+M` / `Ctrl+Shift+D`
  keyboard automation (Windows).
- Server-side bot mute/deafen (`botmute`, `botdeafen`).
- `callbyid:<user ID>` — look up a user by ID, open the DM via `@username` in the
  quick switcher, start the call.
- `callbyusername:<name>` — same call flow, matched by raw username (no `@`).
- `callbyname:<name>` — same call flow, matched by display name, with `@username`
  fallback when the display name is duplicated.
- Shared `KeyboardAutomation` Win32 keyboard helper class.
- Setup wizard guided Configure step: step-by-step instructions, "Open Discord
  Developer Portal" and "Invite bot to this server" buttons, default channel field,
  and `LoadExistingConfig` prefill on relaunch.

### Changed
- `callbyusername` and `callbyname` warn when a user may have renamed and advise
  `callbyid` (IDs never change).

### Fixed
- `searchuserid` no longer depends on exact-match search; works even when the
  name-based search path fluctuates.

## [1.1.0] - 2026-09-10

### Added
- DPAPI-encrypted bot token storage (token re-saved encrypted on first load,
  tied to the Windows user account).
- Five-step GUI setup wizard (single-file, self-contained, embedded payload,
  token validation against the Discord API).
- `AutoConnect` option and `config.example.json` template.
- `build.bat` for manual builds.
- Test suite (41 tests) covering token validation, config handling, and commands.
- GitHub Actions auto-release on tags with `SHA256SUMS.txt` checksums.

### Changed
- Unified naming: assembly/folder `VA.VoiceAttackDiscordPlugin`, namespace
  `VoiceAttackDiscordPlugin`, log prefix, and display name.

### Fixed
- Init crash when resolving the config path (init proxy has no `PluginDir`).
- Discord.Net 3.15 API usage (`Mute`/`Deaf` props, cached channel users).
- Legacy `config.json` is no longer tracked in git (holds local-only bot secrets).

## [1.0.0] - 2026-09-10

### Added
- Initial release: Discord connect/disconnect, sending messages and DMs, reading
  recent channel messages into variables, joining/leaving voice channels, user
  search by name and by ID, listing channel members, and DM call initiation via
  keyboard automation.
- V4 plugin interface: all actions through the single `Context` field as
  `action:arg1:arg2`.
- Collision-safe calls: duplicate display names warn and fall back to `@username`.
- README and SETUP guides with voice-command examples.
