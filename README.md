# DashTray

Windows tray utility for typographic replacements, smart quotes and short text snippets.
It uses a global keyboard hook and simulates replacement keystrokes in the focused app.
The public source ships only neutral example snippets (`;mail`, `;company`); add your own in settings.

## Build

Windows x64 and the .NET 8 SDK:

```powershell
dotnet build DashTray.csproj -c Release --nologo
```

`publish.ps1` creates a self-contained executable in `publish/` and reports native build failures.
`install.ps1` and `uninstall.ps1` modify the local installation/startup and may stop a running instance;
read them before use. They were not executed for this publication.

Startup is **off by default** in this source release; enable it from settings if desired.
Personal settings live under `%LOCALAPPDATA%/DashTray/` and must not be committed.
Use the tray controls to pause replacements and configure exclusions.

## Verification

See VALIDATION.md for actual build results. Global keyboard behavior, application compatibility,
installer and startup behavior require separate interactive checks. No automated test suite exists.
The undocumented custom icon is omitted; Windows/system icon fallback is used.
