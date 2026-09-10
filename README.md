# 🎙️ DiscordVAPlugin

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![VoiceAttack V2](https://img.shields.io/badge/VoiceAttack-V2%20(V4%20API)-2b9d48)](https://voiceattack.com/)
[![Discord.Net](https://img.shields.io/badge/Discord.Net-3.15-5865F2)](https://github.com/discord-net/Discord.Net)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Windows](https://img.shields.io/badge/Platform-Windows-0078D4)](https://github.com/ARA-ARA-ENJOYER/VoiceAttack-Discord-Plugin)

> A VoiceAttack V2 plugin that connects VoiceAttack to Discord via a Discord Bot.
> Send messages, read channels, control voice channels, search users, and initiate
> calls — all via voice commands. 🗣️➡️💬

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

## ✨ Features

| | |
|---|---|
| 💬 **Messaging** | Send messages to any text channel, send DMs, read recent chat into variables |
| 🎧 **Voice** | Join/leave voice channels, toggle self-mute and self-deafen |
| 🔍 **Users** | Search by name **or by ID** (name-change proof), list channel members |
| 📞 **Calls** | Open a DM and start a voice call via keyboard automation (Windows only) |
| 🛡️ **Collision-safe** | Duplicate display names are detected — falls back to unique `@username` |

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
| `mute` | Toggle self-mute |
| `deafen` | Toggle self-deafen |
| `calluser:<name>` | Open Discord DM and initiate call (legacy flow) |
| `callbyid:<user ID>` | Look up user by ID, open DM via quick switcher, call with Ctrl+' |
| `callbyusername:<name>` | Same new-style call flow, matched by name |

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

---

## 💡 Example Voice Commands

Create these under **Other > Advanced > Execute an External Plugin Function**:

1. **"Send message to general"** — Context: `sendmessage:general:{TXT}` (dictation)
2. **"Read chat"** — Context: `readmessages:general:10`, then TTS `{TXT:Discord.LastMessages}`
3. **"Call [name]"** — Context: `callbyusername:{TXT}`
4. **"Call that person"** — Context: `callbyid:832258686764056657`
5. **"Join voice [channel]"** — Context: `joinvoice:{TXT}`
6. **"Mute"** — Context: `mute`

---

## ⚠️ Limitations

- **Calls**: keyboard automation (`Ctrl+K` → DM → `Ctrl+'`). Requires the Discord desktop app open. May break if Discord updates its UI.
- **Duplicate display names**: detected automatically — the plugin warns and navigates by unique `@username` instead. Prefer `callbyid` over `calluser` when you have the ID.
- **Voice channels**: bot needs Connect and Speak permissions.
- **Rate limits**: Discord API throttles abuse; the plugin retries but go easy.
- **Windows only**: call automation works on Windows.

---

## 🛠️ Building from Source

```bash
dotnet build -c Release
```

Output lands in `DiscordVAPlugin/bin/Release/net8.0/`. Copy that folder plus your
local `config.json` to `C:\Program Files\VoiceAttack\Apps\VA.DiscordVAPlugin\`.

> **Secrets:** `config.json` (your real bot token) is git-ignored and never committed.
> The repo ships `config.example.json` as the documented template. See [SETUP.md](SETUP.md).

---

## 📄 License

MIT — see [LICENSE](LICENSE).
