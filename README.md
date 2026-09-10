# DiscordVAPlugin

A VoiceAttack V2 plugin that connects VoiceAttack to Discord via a Discord Bot. Send messages, read channels, control voice channels, search users, and initiate calls -- all via voice commands.

## Features

- **Send messages** to any Discord text channel
- **Read messages** from channels into VoiceAttack variables
- **Send DMs** to specific users
- **Join/leave voice channels**
- **Mute/unmute** and **deafen/undeafen** in voice
- **Search users** by name
- **List users** in a channel
- **Initiate calls** via keyboard automation (Windows only)

## Prerequisites

- VoiceAttack V2 (with Plugin Support enabled)
- .NET 8 Runtime
- A Discord Bot (create one at https://discord.com/developers)

## Setup

### 1. Create a Discord Bot

1. Go to https://discord.com/developers/applications
2. Click **New Application** and give it a name
3. Go to the **Bot** tab and click **Reset Token** to get your bot token
4. Under **Privileged Gateway Intents**, enable:
   - **Presence Intent**
   - **Server Members Intent**
   - **Message Content Intent**
5. Go to **OAuth2 > URL Generator**
6. Select scopes: `bot`, `applications.commands`
7. Select permissions: Send Messages, Read Message History, Connect, Speak, Use Voice Activity
8. Copy the generated URL and open it in your browser to invite the bot to your server

### 2. Install the Plugin

1. Build the project or download the release
2. Copy the entire output folder to: `C:\Program Files (x86)\VoiceAttack\Apps\DiscordVAPlugin\`
3. Open VoiceAttack, go to **Options** (wrench icon) > **General**
4. Enable **Plugin Support**
5. Restart VoiceAttack

### 3. Configure

Edit `config.json` in the plugin folder:

```json
{
  "BotToken": "YOUR_BOT_TOKEN_HERE",
  "DefaultGuildId": 0,
  "DefaultChannelName": "general",
  "AutoConnect": true,
  "LogLevel": "Info"
}
```

- **BotToken**: Your Discord bot token from step 1
- **DefaultGuildId**: Your server's Guild ID (right-click server name > Copy Server ID with Developer Mode on)
- **DefaultChannelName**: Default channel to use when none specified
- **AutoConnect**: Connect to Discord automatically when VoiceAttack starts

## Usage

Use the **Execute an External Plugin Function** action in VoiceAttack with the following contexts:

| Context | Text1 | Text2 | Description |
|---------|-------|-------|-------------|
| `connect` | | | Connect to Discord |
| `disconnect` | | | Disconnect from Discord |
| `sendmessage` | channel name | message | Send a message to a channel |
| `readmessages` | channel name | count | Read messages (stored in `Discord.LastMessages`) |
| `senddm` | user name | message | Send a DM to a user |
| `searchuser` | user name | | Search for a user (sets `Discord.found.UserId`, etc.) |
| `searchuserid` | user ID | | Search for a user by ID, name-change proof (sets `Discord.found.*`) |
| `listusers` | channel name | | List users in a channel |
| `joinvoice` | channel name | | Join a voice channel |
| `leavevoice` | | | Leave current voice channel |
| `mute` | | | Toggle self-mute |
| `deafen` | | | Toggle self-deafen |
| `calluser` | user name | | Open Discord DM and initiate call (keyboard automation) |
| `callbyid` | user ID | | Look up user by ID, open DM via quick switcher, call with Ctrl+' |

### VoiceAttack Variables Set

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

## Example Voice Commands

Create these in VoiceAttack under **Other > Advanced > Execute an External Plugin Function**:

1. **"Send message to general"** - Context: `sendmessage`, Text1: `general`, Text2: `{TXT}` (dictation)
2. **"Read chat"** - Context: `readmessages`, Text1: `general`, Text2: `10`
3. **"Call [name]"** - Context: `calluser`, Text1: `{TXT}`
4. **"Join voice [channel]"** - Context: `joinvoice`, Text1: `{TXT}`
5. **"Mute"** - Context: `mute`

## Limitations

- **Call feature**: Uses keyboard shortcuts (Ctrl+U to search, Ctrl+Shift+C to call). Requires Discord desktop app to be open. May break if Discord updates its UI.
- **Voice channels**: Bot must have Connect and Speak permissions.
- **Rate limits**: Discord API has rate limits. The plugin handles retries but excessive use may be throttled.
- **Windows only**: Call automation via keyboard shortcuts only works on Windows.

## Building from Source

```bash
dotnet build
```

The output will be in `bin/Debug/net8.0/`. Copy that entire folder to your VoiceAttack Apps directory.

## License

MIT
