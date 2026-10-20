# PadBridge

キーボードとマウスを **仮想 Xbox 360 コントローラー** に変換する、オフライン向けのローカル入力コンバーターです。ゲームには XInput のパッドが接続されているように見えます。

## 全体構造

```
PadBridge.sln
├── src/PadBridge.Core    変換・フック・仮想パッド
└── src/PadBridge.App     WPF 操作画面
```

| 部品 | 役割 |
| --- | --- |
| `VigemXbox360Gamepad` | ViGEmBus 経由で Xbox 360 相当の仮想パッドを接続し、レポートを送る |
| `LowLevelInputCapture` | 低レベルフックで KBM を取得。変換中は割り当て済み入力を消費する |
| `MouseToStickConverter` | マウス速度 → 右スティック（感度・カーブ・アンチデッドゾーン・スムージング） |
| `WasdToStickConverter` | WASD の 8 方向 → 左スティック。ランプで瞬時 0/1 を避ける |
| `ConverterEngine` | 既定 1000Hz の入力ループ。まとめて 1 レポートを Submit する |

試作で「入力が通らない / 不自然」になりやすい原因は次です。

1. `SendInput` では XInput を読むゲームに届かない → **ViGEm の仮想パッドが必要**
2. マウスとパッドが同時に届く → ゲームが KBM 側を優先または拒否 → **フックで消費**
3. 画面端でマウス Δ が止まる → **カーソルを中央へ戻す**
4. レポート間隔が粗い・スティックが 0 に落ちる → **1000Hz で継続送信**
5. 小さいスティック値がゲームのデッドゾーンに飲まれる → **アンチデッドゾーン**

## 必要なもの

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [ViGEmBus](https://github.com/nefarius/ViGEmBus/releases)（仮想ゲームパッドドライバ）

ViGEmBus は管理者としてインストーラーを実行します。インストール後、必要なら再起動してください。アプリ本体の管理者権限は不要です。

## ビルドと起動

```powershell
cd C:\Users\yuu\source\PadBridge
dotnet build
dotnet run --project src/PadBridge.App/PadBridge.App.csproj
```

1. 「仮想パッドを接続」→ Windows の「コントローラーの設定」に Xbox 360 コントローラーが出ること
2. 「変換開始」または **F8**
3. ゲームにフォーカスを移して WASD / マウスを使う
4. 停止も **F8**

設定は `%AppData%\PadBridge\profile.json` に保存されます。キー割り当ては JSON の `KeyboardBindings`（仮想キーコード）と `MouseBindings` を編集してください。

## ライブラリ

- `Nefarius.ViGEm.Client` … 仮想 Xbox コントローラー
- Windows 低レベルフック / `timeBeginPeriod(1)` … 入力取得と 1ms タイマー精度

DualShock 4 仮想デバイスは ViGEm の `CreateDualShock4Controller` で同じエンジンに載せられます。まずは互換性の広い Xbox 360 / XInput から使ってください。
