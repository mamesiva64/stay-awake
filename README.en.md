# StayAwake — Prevent sleep

A small Windows desktop app that prevents idle sleep. No installation needed: just run `StayAwake.exe`.

[日本語](README.md) · [Download / Releases](https://github.com/mamesiva64/stay-awake/releases/latest) · [Release notes](RELEASE_NOTES.md) · [MIT License](LICENSE)

![StayAwake English interface](docs/stay-awake-en.png)

## Quick start

1. Download `StayAwake-1.1.0-windows.zip` from [Releases](https://github.com/mamesiva64/stay-awake/releases/latest) and extract it.
2. Run `StayAwake.exe`. Sleep prevention starts right away; the screen still turns off per Windows settings.
3. Use the top button to turn sleep prevention on or off. **Turn off after** offers Never, 30 minutes, 1, 2, 4, or 8 hours.
4. Click **Turn off screen in 5 s** to turn the screen off after five seconds. Moving the mouse or pressing a key wakes it. Click again during the countdown to cancel.
5. Closing the window asks for confirmation; click OK to exit. Exiting stops sleep prevention; the lid-close setting stays as it is.

Requires Windows 10 / 11 and .NET Framework 4.x, normally included with Windows. The release executable is not code-signed.

## Options

- **Keep the screen on:** off by default. When checked, the screen does not turn off automatically either. When unchecked, the screen turns off per Windows settings.
- **When I close the lid:** shows and changes the Windows lid-close setting, separately for plugged-in and on-battery power.

### Lid-close setting

Like Windows Power Options, the **Plugged in** and **On battery** lists offer Do nothing, Sleep, Hibernate, and Shut down. They mirror the **current Windows settings** directly; starting the app never changes them. Hibernate is listed only when this PC supports it (or when Windows is already set to it). Settings are re-read periodically and when the window regains focus.

To keep the PC running with the lid closed, choose **Do nothing**.

- Choosing a different value asks for confirmation; Yes changes only that power source. The other one is never changed.
- No leaves Windows unchanged and puts the list back to the current Windows value.
- **No previous settings or recovery files are saved.** Copying the app to another PC uses that PC's Windows settings.
- **Changed settings remain after sleep prevention stops, the timer ends, or the app exits.** Choose again here or in Windows Power Options to change them.
- If a change fails, the app shows an error and re-reads Windows settings; it does not restore earlier values automatically.

Files from the earlier prototype under `%LOCALAPPDATA%\StayAwake` (`initial-*.txt` / `pending.txt`) are ignored. Exit the older app before using this version. Its backup files are never applied to another PC.

Sleep prevention itself does not change your Windows power plan. Only **When I close the lid** changes Windows settings, and always after confirmation. Permissions or organization policy may block changes. Manual sleep, shutdown, and Windows or hardware protection are not prevented.

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

`StayAwake.cs` contains the UI, localization, timer, screen-off button and execution-state requests. `LidSettings.cs` reads and writes the Windows "When I close the lid" setting without persisting configuration. `Tests.cs` uses fake power services to cover confirmations, cancelation, errors, timers, the screen-off countdown, the exit confirmation and language fallback. Tests do not change real Windows settings.

After building tests, run `.\Tests.exe --screenshots .\docs` to refresh the Japanese and English screenshots. This reads the actual lid settings without modifying them or creating execution-state requests.

## License

[MIT](LICENSE).
