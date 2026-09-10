#!/bin/bash
# Open DiscordVAPlugin in Windows Explorer from WSL
explorer.exe "$(wslpath -w /home/username123/projects/DiscordVAPlugin)" 2>/dev/null
