# StayAwake — Prevent sleep

A small Windows desktop app that prevents idle sleep. No installation needed: just run `StayAwake.exe`.

[日本語](README.md) · [Download / Releases](https://github.com/mamesiva64/stay-awake/releases/latest) · [MIT License](LICENSE)

![StayAwake English interface](docs/stay-awake-en.png)

## Quick start

1. Download `StayAwake-1.0.0-windows.zip` from [Releases](https://github.com/mamesiva64/stay-awake/releases/latest) and extract it.
2. Run `StayAwake.exe`. Idle-sleep prevention and keeping the screen on are enabled at startup.
3. Use the main button to toggle sleep prevention. Choose Never, 30 minutes, 1, 2, 4, or 8 hours for the timer.
4. Close the window to exit and release idle-sleep prevention.

Requires Windows 10 / 11 and .NET Framework 4.x, normally included with Windows. The release executable is not code-signed.

## Options

- **Keep the screen on:** also prevents automatic display timeout. When unchecked, the screen follows Windows settings.
- **Keep running when the lid is closed:** for laptops; edits the active Windows power plan after confirmation.

### Lid-close settings

The checkbox reads the **current Windows settings**. Starting the app never changes them. Different plugged-in and battery keep-running states appear as an indeterminate checkbox; both values are shown below it. Settings refresh periodically and when the window regains focus.

- Enabling asks permission to set both plugged-in and battery lid-close actions to **Do nothing**.
- Disabling asks permission to set both actions to **Sleep**. This is not a restore operation.
- Clicking an indeterminate checkbox asks to set both actions to Do nothing.
- Canceling leaves Windows settings unchanged.
- **No previous settings or recovery files are saved.** Copying the app to another PC uses that PC's Windows settings.
- **Lid settings remain after turning off sleep prevention, timer expiry, or app exit.** Uncheck the option or use Windows Power Options to change them again.
- Failed changes may be partial. The app shows an error and re-reads Windows settings; it does not automatically restore earlier values.

Files from the earlier prototype under `%LOCALAPPDATA%\StayAwake` (`initial-*.txt` / `pending.txt`) are ignored. Exit the older app before using this version. Its backup files are never applied to another PC.

Normal idle-sleep prevention does not change your Windows power plan. Only the lid option changes OS settings, after confirmation. Permissions or organizational policy may restrict changes. Manual sleep, shutdown, and OS/hardware protection are not prevented.

## Language

Japanese Windows display language selects Japanese; all other languages select English. Window text and confirmation messages are localized. Optional overrides:

```powershell
.\StayAwake.exe --lang=ja
.\StayAwake.exe --lang=en
```

## Build and test

Run on Windows using the built-in .NET Framework C# compiler. Visual Studio and NuGet packages are not required.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
```

`StayAwake.cs` contains the UI, localization, timer and execution-state requests. `LidSettings.cs` reads and writes Windows lid settings without persisting configuration. `Tests.cs` uses fake power services to cover confirmations, cancelation, errors, timers and language fallback. Tests do not change real Windows settings.

After building tests, run `.\Tests.exe --screenshots .\docs` to refresh the Japanese and English screenshots. This reads the actual lid settings without modifying them or creating execution-state requests.

## License

[MIT](LICENSE).
