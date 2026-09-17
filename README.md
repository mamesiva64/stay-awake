# StayAwake — スリープを防止

Windowsの無操作によるスリープを防ぐ、小さなデスクトップアプリです。
インストール不要。`StayAwake.exe` を起動するだけで使えます。

[English](README.en.md) · [ダウンロード / Releases](https://github.com/mamesiva64/stay-awake/releases/latest) · [MIT License](LICENSE)

![StayAwakeの日本語画面](docs/stay-awake-ja.png)

## ダウンロードと使い方

1. [Releases](https://github.com/mamesiva64/stay-awake/releases/latest) から `StayAwake-1.0.0-windows.zip` をダウンロードし、展開します。
2. `StayAwake.exe` を起動します。最初からスリープ抑止が有効になり、画面もつけたままになります。
3. 大きなボタンで抑止をON/OFFにします。タイマーは無期限・30分・1時間・2時間・4時間・8時間から選べます。
4. ウィンドウを閉じるとアプリが終了し、無操作によるスリープの抑止を解除します。

対応環境：Windows 10 / 11、.NET Framework 4.x。通常、Windows付属の環境で動作し、追加インストールは不要です。
配布EXEはコード署名されていません。

## オプション

| 項目 | 動作 |
| --- | --- |
| 画面もつけたままにする | ONでは画面の自動消灯も防止します。OFFでは画面はOSの設定に従います。 |
| ふたを閉じても動作を続ける | ノートPC向け。現在の電源プランのふたの動作を、確認後に変更します。 |

### ふたの設定について

チェック状態と補足表示は、**OSの現在の設定**から取得します。起動時に設定を書き換えません。
電源接続時とバッテリー時で「何もしない」の設定が異なる場合は、中間状態のチェックと両方の値を表示します。
設定は定期的に再取得するため、Windows側で電源プランや設定を変更すると画面も更新されます。

- **ONにする場合**：確認ダイアログで許可すると、電源接続時・バッテリー時とも「何もしない」にします。
- **OFFにする場合**：確認ダイアログで許可すると、両方とも「スリープ」にします。以前の設定への復元ではありません。
- 中間状態をクリックした場合は、両方を「何もしない」にする確認を表示します。
- キャンセルした場合は変更しません。
- **変更前の設定や復元用ファイルは保存しません。** 別のPCへアプリをコピーしても、そのPCのOS設定を読みます。
- **ふたの設定は、スリープ抑止のOFF・タイマー解除・アプリ終了後も残ります。** 戻す場合はチェックをOFFにするか、Windowsの電源オプションで設定してください。
- 変更が途中で失敗した場合はエラーを表示し、OSの値を読み直します。自動復元は行いません。

旧試作版の `%LOCALAPPDATA%\StayAwake` にある `initial-*.txt` / `pending.txt` は、このバージョンでは読み書きしません。旧版を終了してから使用してください。旧ファイルの内容が別PCへ適用されることはありません。

通常のスリープ抑止はWindowsの電源設定を書き換えません。ふたの設定だけが、確認後にOS設定を直接変更します。
権限や組織のポリシーによって操作できない場合があります。手動スリープ・シャットダウンや、OS・機器側の保護動作は抑止しません。

## 日本語 / English

Windowsの表示言語が日本語なら日本語、それ以外なら英語を自動選択します。ボタン、状態表示、確認ダイアログも切り替わります。
次の起動引数で明示的に選択することもできます。

```powershell
.\StayAwake.exe --lang=ja
.\StayAwake.exe --lang=en
```

![StayAwake English UI](docs/stay-awake-en.png)

## ソースからビルド

Windows PowerShellで実行します。Windows付属の .NET Framework C#コンパイラーを使用し、Visual StudioやNuGetパッケージは不要です。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
```

- `StayAwake.cs`：画面、日英表示、スリープ抑止、タイマー
- `LidSettings.cs`：Windowsのふた設定の読み書き（設定ファイルなし）
- `Tests.cs`：疑似電源設定での確認・キャンセル・失敗・タイマー・言語切替のテスト
- `build.ps1`：アイコン生成と実行ファイルのビルド
- `test.ps1`：テストのビルドと実行。実機の電源設定は変更しません。

スクリーンショットを再生成する場合は、テストのビルド後に `.\Tests.exe --screenshots .\docs` を実行します。画面には実機の設定を読み取り専用で表示し、電源設定は変更しません。

## ライセンス

[MIT](LICENSE)。改変・再配布・商用利用が可能です。
