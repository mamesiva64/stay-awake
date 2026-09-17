# StayAwake 1.0.0

Windowsの無操作によるスリープを防止するデスクトップアプリです。

- ボタン、解除タイマー、オプションの順に画面を整理
- 画面をつけたままにするオプション
- OSの現在値を表示する、ノートPCのふた設定オプション
- 有効化・無効化の両方でOS設定変更を確認
- 設定の保存・バックアップ・自動復元を廃止
- 日本語UIと、それ以外の環境向けの英語UI

ZIPを展開して `StayAwake.exe` を起動してください。Windows 10 / 11向けです。
ふたの設定は終了後も残り、OFFにすると「スリープ」になります。

---

A portable Windows app that prevents idle sleep, with a timer, an optional screen-on request, and a confirmed lid-close setting editor. Japanese display language selects Japanese; other languages select English.

Extract the ZIP and run `StayAwake.exe`. Requires Windows 10 / 11 with .NET Framework 4.x. The executable is unsigned. No settings backups are stored. Lid-setting changes persist after exit; disabling the lid option explicitly sets Sleep.

Tested with fake power services and read-only access to real Windows lid settings. Physical lid-close behavior has not been tested on hardware.
