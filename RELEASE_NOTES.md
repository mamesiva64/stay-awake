# StayAwake 1.1.0

画面をコンパクトにし、ノートPCを閉じたときの動作をWindowsの電源オプションと同じように選べるようにしました。

- 画面全体をコンパクトにリデザイン（オプションは枠にまとめて表示）
- ノートPCを閉じたときの動作を、電源接続時・バッテリー駆動時それぞれのコンボボックスで「何もしない／スリープ／休止状態／シャットダウン」から選択。表示はWindowsの現在の設定をそのまま反映し、変更時は確認を表示（「いいえ」なら表示も元に戻ります）
- 「5秒後に画面を消す」ボタンを追加（マウスかキーボードの操作で戻ります）
- 「画面もつけたままにする」の初期状態をオフに変更
- ウィンドウを閉じるときに確認を表示（OKで終了）
- 用語をWindowsの表記に統一（「スリープ防止」「カバーを閉じたときの動作」「バッテリー駆動時」など）

ZIPを展開して `StayAwake.exe` を起動してください。Windows 10 / 11向けです。
ノートPCを閉じたときの動作の変更は、アプリを終了しても元に戻りません。

---

A more compact layout, and the lid-close setting now works like Windows Power Options.

- Redesigned, compact window with options grouped in a frame
- "When I close the lid" lists for Plugged in and On battery offer Do nothing, Sleep, Hibernate, and Shut down. They mirror the current Windows settings; changes ask for confirmation, and No puts the list back
- New "Turn off screen in 5 s" button (mouse or keyboard wakes the screen)
- "Keep the screen on" is now off by default
- Closing the window asks for confirmation (OK to exit)
- Wording aligned with Windows terms

Extract the ZIP and run `StayAwake.exe`. Requires Windows 10 / 11 with .NET Framework 4.x. The executable is unsigned. No settings backups are stored. Lid-setting changes persist after exit.

Automated tests use fake power services and read-only access to real Windows settings. Physical lid-close behavior has not been tested on hardware.
