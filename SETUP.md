# 🎙️ DiscordVAPlugin — VoiceAttack Setup Guide

> Control Discord with your voice: send messages, read chat, manage voice channels,
> and call people — all through VoiceAttack commands powered by a Discord bot.

---

## 📋 Prerequisites

| Requirement | Notes |
|---|---|
| VoiceAttack V2 | With **Plugin Support** enabled |
| Discord desktop app | Must be open for call automation |
| .NET 8 Runtime | For running the plugin |
| A Discord Bot | Create one below (free, ~5 min) |

---

## Part 1 — 🤖 Create & Invite Your Discord Bot

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications) → **New Application** → give it a name.
2. Open the **Bot** tab → **Reset Token** → copy the token (keep it secret!).
3. Under **Privileged Gateway Intents**, enable:
   - ✅ **Presence Intent**
   - ✅ **Server Members Intent**
   - ✅ **Message Content Intent**
4. Go to **OAuth2 → URL Generator**:
   - Scopes: `bot`, `applications.commands`
   - Permissions: *Send Messages, Read Message History, Connect, Speak, Use Voice Activity*
5. Open the generated URL in your browser to invite the bot to your server.
6. Copy your IDs (Discord **Settings → Advanced → Developer Mode** must be ON):
   - Right-click your server → **Copy Server ID** → this is your `DefaultGuildId`.
   - Right-click any user → **Copy User ID** (you'll need these for `callbyid` / `searchuserid`).

---

## Part 2 — 📦 Deploy the Plugin

1. Build the project (`dotnet build -c Release`) or grab the latest release.
2. Copy the **entire output folder** to:
   ```
   C:\Program Files\VoiceAttack\Apps\VA.DiscordVAPlugin\
   ```
   ⚠️ The folder **must** be named `VA.DiscordVAPlugin` and contain `VA.DiscordVAPlugin.dll` —
   VoiceAttack only detects DLLs with the `VA.` prefix.
3. Configure the bot — copy the template and fill in your secrets:
   ```
   copy config.example.json config.json
   ```
   ```json
   {
     "BotToken": "PASTE_YOUR_BOT_TOKEN_HERE",
     "DefaultGuildId": 123456789012345678,
     "DefaultChannelName": "general",
     "AutoConnect": true,
     "LogLevel": "Info"
   }
   ```
   🔒 `config.json` is **git-ignored** — your token never leaves your machine.

---

## Part 3 — 🔌 Enable the Plugin in VoiceAttack

1. Open VoiceAttack → click the **wrench icon (Options)** → **General**.
2. Check ✅ **Plugin Support** → click OK.
3. **Restart VoiceAttack.**
4. Open the log (wrench → Log) and confirm:
   ```
   DiscordVAPlugin initialized. Bot token configured: True
   DiscordVAPlugin connected to Discord automatically.
   ```

---

## Part 4 — 🗣️ Create Your First Command

1. VoiceAttack main window → **New Command** → **When I say:** `connect to discord`.
2. Click **+** in Actions → **Other → Advanced → Execute an External Plugin Function**.
3. Pick **DiscordVAPlugin** in the dropdown.
4. In **Plugin Context** type: `connect`
   - ℹ️ Under the V4 plugin interface, **everything goes in this one Context field**
     as `action:arg1:arg2`. Ignore the legacy SmallInt/Text/Integer boxes — they do nothing.
   - The Context field parses `{TXT:...}` tokens, so dictation and variables work inline.
5. Click **OK → Save**, then say the phrase and watch the log.

---

## Part 5 — ⭐ Example Commands

| Say | Context | What it does |
|---|---|---|
| *"Send message to general saying hello"* | `sendmessage:general:{TXT}` | Sends your dictated text to `#general` |
| *"Read chat"* | `readmessages:general:5` + TTS `{TXT:Discord.LastMessages}` | Reads back the last 5 messages |
| *"Find user 832..."* | `searchuserid:832258686764056657` | Looks up by ID, fills `Discord.found.*` |
| *"Call that person"* | `callbyid:832258686764056657` | Opens their DM, starts a call (`Ctrl+'`) |
| *"Mute"* | `mute` | Toggles self-mute |
| *"Join voice lobby"* | `joinvoice:lobby` | Joins the `lobby` voice channel |

**Reading results back:** after `readmessages`, add a **Text To Speech** action:
`Last messages: {TXT:Discord.LastMessages}`. Other variables: `{TXT:Discord.Users}`,
`{TXT:Discord.found.DisplayName}`, `{TXT:Discord.VoiceChannel}`.

Full action reference lives in the [README](README.md#action-reference).

---

## Part 6 — 📞 Calling Someone By ID (Deep Dive)

`callbyid:<user ID>` runs this sequence:

1. **API lookup** — resolves the user ID to their current display name (IDs never change, names do).
2. **Uniqueness check** — counts how many people share that display name.
   - Unique → types the display name into the Quick Switcher.
   - Shared → logs a ⚠️ warning and types the unique `@username` instead.
3. **Keyboard automation** — `Ctrl+K` → types the name → `Enter` (opens the DM) → waits 1s → `Ctrl+'` (starts the call).

> **Same display name?** Usernames are unique on Discord, display names are not.
> The plugin detects collisions automatically and falls back to `@username`, so the
> Quick Switcher lands on the right DM. Prefer `callbyid` over `calluser` whenever
> you have the ID; use `callbyusername:<name>` for the same new-style flow by name.

Requirements: Discord desktop app open and logged in, and the Quick Switcher must be
able to find the person (you share a server or have an existing DM).

---

## 🛠️ Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Plugin not listed at all | Folder/DLL missing the `VA.` prefix | Folder must be `VA.DiscordVAPlugin`, DLL `VA.DiscordVAPlugin.dll` |
| `...does not contain a definition for 'Text1'` | Old build using the legacy interface | Update to the latest build; put everything in the Context field |
| `...does not contain a definition for 'PluginDir'` | Old build | Update to the latest build |
| `Not connected. Use 'connect' first` | Bot offline | Run a `connect` command, or set `AutoConnect: true` |
| `User ID ... not found` | Wrong ID, or bot shares no server with that user | Verify the ID; invite the bot to a shared server |
| Call doesn't start | Discord not focused / shortcut changed | Keep Discord open; retry with it focused |
| Wrong person called | Duplicate display name, old build | Update — new builds warn and fall back to `@username` |

---

*Back to the [README](README.md) for the action reference and API details.*
