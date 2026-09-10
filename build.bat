@echo off
REM Build VoiceAttack Discord Plugin (solution: plugin + tests + setup wizard)
echo Building VoiceAttack Discord Plugin...
dotnet build VoiceAttackDiscordPlugin.sln -c Release
echo.
echo Running tests...
dotnet test VoiceAttackDiscordPlugin.Tests\VoiceAttackDiscordPlugin.Tests.csproj -c Release --no-build
echo.
echo Build output: VoiceAttackDiscordPlugin\bin\Release\net8.0\
echo.
echo To deploy, copy VoiceAttackDiscordPlugin\bin\Release\net8.0\ plus config.json to:
echo   "C:\Program Files\VoiceAttack\Apps\VA.VoiceAttackDiscordPlugin\"
echo.
echo Prefer the one-click route? Run the Setup wizard from the Releases page instead.
pause
