@echo off
REM Build VoiceAttack Discord Plugin (solution: plugin + tests + setup wizard)
echo Building VoiceAttack Discord Plugin...
dotnet build VoiceAttackDiscordPlugin.sln -c Release
echo.
echo Running tests...
dotnet test VoiceAttackDiscordPlugin.Tests\VoiceAttackDiscordPlugin.Tests.csproj -c Release --no-build
echo.
echo Publishing setup wizard (single file)...
dotnet publish Setup\VoiceAttackDiscordPlugin.Setup.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
echo.
echo Setup output: publish\VoiceAttack-Discord-Plugin-Setup.exe
echo.
echo To deploy, copy VoiceAttackDiscordPlugin\bin\Release\net8.0\ plus config.json to:
echo   "C:\Program Files\VoiceAttack\Apps\VoiceAttackDiscordPlugin\"
echo.
echo Prefer the one-click route? Run the Setup wizard from the Releases page instead.
pause
