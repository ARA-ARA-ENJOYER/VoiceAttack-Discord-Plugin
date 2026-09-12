# 🎙️ VoiceAttack Discord Plugin

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![VoiceAttack V2](https://img.shields.io/badge/VoiceAttack-V2%20(V4%20API)-2b9d48)](https://voiceattack.com/)
[![Discord.Net](https://img.shields.io/badge/Discord.Net-3.20-5865F2)](https://github.com/discord-net/Discord.Net)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Windows](https://img.shields.io/badge/Platform-Windows-0078D4)](https://github.com/ARA-ARA-ENJOYER/VoiceAttack-Discord-Plugin)

> A VoiceAttack V2 plugin that connects VoiceAttack to Discord via a Discord Bot.
> Send messages, read channels, control voice channels, search users, and initiate
> calls — all via voice commands. 🗣️➡️💬

> **Non-technical? Start here:** download **`VoiceAttack-Discord-Plugin-Setup.exe`**
> from the [Releases page](https://github.com/ARA-ARA-ENJOYER/VoiceAttack-Discord-Plugin/releases),
> double-click it, and follow the wizard — no building, no manual file copying. 🪄
>
> If Windows shows *"Windows protected your PC"*, that's normal for unsigned
> open-source apps: click **More info → Run anyway**. Cautious? Compare the
> checksum with **`SHA256SUMS.txt`** on the release page before running it.
>
> **What changed?** See **[CHANGELOG.md](CHANGELOG.md)** for every release —
> features, fixes, and breaking changes. 📝
>
> **Upgrading from 1.3.x?** See [⬆️ Upgrading](#️-upgrading) below — run the new
> wizard and it migrates your token automatically; the folder lost its `VA.` prefix.

---

## 🚀 Quick Start

New here? Follow the full walkthrough: **[📖 SETUP.md](SETUP.md)** — bot creation,
deployment, your first voice command, and troubleshooting.

```text
1. Create a Discord bot + invite it        → SETUP.md Part 1
2. Copy plugin files to VoiceAttack\Apps   → SETUP.md Part 2
3. Fill in config.json (token stays local) → SETUP.md Part 2
4. Enable Plugin Support + restart         → SETUP.md Part 3
5. Say "connect to discord"                → SETUP.md Part 4
```

---

## ⬆️ Upgrading

Coming from **1.3.x**? The one-click path needs nothing from you:

1. Download **`VoiceAttack-Discord-Plugin-Setup.exe`** (1.4.0) and run the wizard.
2. It finds your old `VA.VoiceAttackDiscordPlugin` folder, copies your
   `config.json` (token preserved) into the new `VoiceAttackDiscordPlugin`
   folder, and offers to delete the old folder.
3. Restart VoiceAttack. The log confirms: `You're up to date (v1.4.0).`

Manual upgraders: install the release files into
`C:\Program Files\VoiceAttack\Apps\VoiceAttackDiscordPlugin\` (**new name —
the `VA.` prefix is gone**) and copy your `config.json` across yourself. Your
existing encrypted token keeps working (encryption is tied to your Windows
user, not the folder). Delete the old `VA.VoiceAttackDiscordPlugin` folder so
VoiceAttack doesn't load two copies of the plugin.

New in 1.4.0: on every load the plugin checks GitHub Releases and logs either
`You're up to date (v1.4.0).` or `Update available: v1.x.x …` (a soft yellow
line if GitHub is unreachable — nothing is ever uploaded).

---

## ✨ Features

| | |
|---|---|
| 💬 **Messaging** | Send messages to any text channel, send DMs, read recent chat into variables |
| 🎧 **Voice** | Join/leave voice channels, toggle your mute/deafen or the bot's (`botmute`/`botdeafen`) |
| 🔍 **Users** | Search by name **or by ID** (name-change proof), list channel members |
| 📞 **Calls** | Open a DM and start a voice call via keyboard automation (Windows only) |
| 🛡️ **Collision-safe** | Duplicate display names are detected — falls back to unique `@username` |
| 🔔 **Update check** | On load, checks GitHub Releases and logs when a newer version is out |

---

## 🧩 Action Reference

Actions run through **Execute an External Plugin Function**. Under the V4 plugin
interface, everything goes in the single **Context** field as `action:arg1:arg2`
(colon-delimited). The Context field parses `{TXT:...}` tokens, so dictation and
variables work inline. Ignore the legacy variable input boxes — they do nothing.

| Context | Description |
|---------|-------------|
| `connect` | Connect to Discord |
| `disconnect` | Disconnect from Discord |
| `sendmessage:<channel>:<message>` | Send a message to a channel |
| `readmessages:<channel>:<count>` | Read messages (stored in `Discord.LastMessages`) |
| `senddm:<user>:<message>` | Send a DM to a user |
| `searchuser:<name>` | Search for a user (sets `Discord.found.UserId`, etc.) |
| `searchuserid:<user ID>` | Search for a user by ID, name-change proof (sets `Discord.found.*`) |
| `listusers:<channel>` | List users in a channel |
| `joinvoice:<channel>` | Join a voice channel |
| `leavevoice` | Leave current voice channel |
| `mute` | Toggle YOUR microphone mute (`Ctrl+Shift+M`, Windows) |
| `deafen` | Toggle YOUR deafen (`Ctrl+Shift+D`, Windows) |
| `botmute` | Toggle the bot's server-side mute |
| `botdeafen` | Toggle the bot's server-side deafen |
| `calluser:<name>` | Open Discord DM and initiate call (legacy flow) |
| `callbyid:<user ID>` | Look up user by ID, open DM via `@username` in quick switcher, call with Ctrl+' |
| `callbyusername:<name>` | Same new-style call flow, matched by raw username (no `@`) |
| `callbyname:<name>` | Same new-style call flow, matched by display name |

### 📥 VoiceAttack Variables Set

| Variable | Description |
|----------|-------------|
| `Discord.LastMessages` | Messages read from a channel |
| `Discord.LastMessageCount` | Number of messages read |
| `Discord.VoiceChannel` | Current voice channel name |
| `Discord.Users` | Comma-separated list of users |
| `Discord.UserCount` | Number of users found |
| `Discord.found.UserId` | Found user's ID |
| `Discord.found.DisplayName` | Found user's display name |
| `Discord.found.Username` | Found user's username |

Read them back with `{TXT:...}`, e.g. TTS: `Last messages: {TXT:Discord.LastMessages}`.

💡 `sendmessage`, `readmessages`, `listusers`, and `joinvoice` fall back to
`DefaultChannelName` from `config.json` when the channel part is empty —
leave it blank with a double colon (e.g. Context `sendmessage::{TXT}` sends
your dictated text to the default channel). A single-colon form like
`sendmessage:hello` treats `hello` as the channel name, not the message.

---

## 💡 Example Voice Commands

Create these under **Other > Advanced > Execute an External Plugin Function**:

1. **"Send message to general"** — Context: `sendmessage:general:{TXT}` (dictation)
2. **"Read chat"** — Context: `readmessages:general:10`, then TTS `{TXT:Discord.LastMessages}`
3. **"Call [name]"** — Context: `callbyusername:{TXT}`
4. **"Call that person"** — Context: `callbyid:123456789012345678`
5. **"Join voice [channel]"** — Context: `joinvoice:{TXT}`
6. **"Mute"** — Context: `mute`

---

## ⚠️ Limitations

- **Calls**: keyboard automation (`Ctrl+K` → DM → `Ctrl+'`). Requires the Discord desktop app open. May break if Discord updates its UI.
- **Duplicate display names**: detected automatically for `callbyname` — the plugin warns and navigates by unique `@username` instead. Prefer `callbyid` over the name-based calls when you have the ID.
- **Changed names**: if `callbyusername`/`callbyname` can't find someone, they warn you they may have renamed — use `callbyid:<ID>` (IDs never change).
- **Voice channels**: bot needs Connect and Speak permissions.
- **Rate limits**: Discord API throttles abuse; the plugin retries but go easy.
- **Windows only**: call automation and your `mute`/`deafen` toggles work on Windows.

---

## 🔒 Security & Privacy

- **Your bot token is encrypted on this PC.** The first time VoiceAttack loads the
  plugin, a plaintext token in `config.json` is re-saved encrypted (Windows DPAPI,
  tied to your Windows user account). Copying `config.json` to another PC or user
  won't work there — just re-enter the token via the setup wizard.
- **Give the bot as few permissions as possible.** It only needs *View Channel, Send Messages,
  Read Message History, Connect, Speak, Use Voice Activity* plus the *Server Members*
  and *Message Content* intents. (`botmute`/`botdeafen` additionally need *Mute Members*
  and *Deafen Members* — the wizard's invite link includes exactly this set, nothing more.) If the bot is only for your server, don't invite it
  anywhere else.
- **If a token ever leaks:** Discord Developer Portal → your app → Bot → **Reset Token**,
  then paste the new one via the setup wizard (or into `config.json` — it re-encrypts
  on next load).
- **Voice commands act immediately** — saying the phrase really sends the message or
  places the call. Only install voice profiles you trust, and pause VoiceAttack's
  listening when you're not using it.
- **Chat content stays local** — messages the plugin reads only land in VoiceAttack
  variables on your PC. The live log prints channel/DM activity (single-line,
  truncated); nothing is sent anywhere except to Discord itself.
- **Verify downloads:** every release ships `SHA256SUMS.txt`. In PowerShell run
  `Get-FileHash <file> -Algorithm SHA256` and compare.

---

## 🛠️ Building from Source

```bash
dotnet build -c Release
```

Output lands in `VoiceAttackDiscordPlugin/bin/Release/net8.0/`. Copy that folder plus your
local `config.json` to `C:\Program Files\VoiceAttack\Apps\VoiceAttackDiscordPlugin\`.
Prefer the wizard? See the one-click note at the top.

> **Secrets:** `config.json` (your real bot token) is git-ignored and never committed.
> The repo ships `config.example.json` as the documented template. See [SETUP.md](SETUP.md).

---

## 📄 License

MIT — see [LICENSE](LICENSE).
