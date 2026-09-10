@echo off
REM Build DiscordVAPlugin
echo Building DiscordVAPlugin...
dotnet build -c Release
echo.
echo Build output: bin\Release\net8.0\
echo.
echo To deploy, copy bin\Release\net8.0\ to:
echo   "C:\Program Files (x86)\VoiceAttack\Apps\DiscordVAPlugin\"
pause
