# Neon入力基盤の調査結果と moorestech への活かし方

調査日: 2026-07-22
調査対象: `/Users/katsumi/Desktop/個人データ/Neon`（Neon.Input / Neon.VirtualMouse 他）および `moorestech_client` の入力基盤

## 結論（TLDR）

Neonの入力基盤の本質は「**画面ごとのキーマップを宣言的に定義し、スタック最上位だけが入力を受ける**」「**アクションIDを安定値enumで一元管理し、リバインド・プリセット・バージョン管理を載せる**」の2点。moorestechは排他制御の権威（UIStateマシン）は既に持っているので、Neonの**Keymapスタックそのものは持ち込まず**、「各UIStateが自分のキーマップを宣言する」形でNeonの宣言的バインドとアクションID一元化だけを移植するのが正着。これで現在19ファイル39箇所に散らばる`HybridInput.GetKeyDown(KeyCode.B)`直書きを一掃でき、リバインド機能への道も開ける。

なお`Neon.Input`は商用タイトルの私有コード（Aktsk.Input=Akatsuki製の下位レイヤーに依存）なので、**コードのコピーではなく設計パターンの移植**が前提。下位のAktsk.Input（BasicInput等）はディレクトリに含まれておらず、そもそも流用不能。

## Neonの入力基盤アーキテクチャ（4層）

1. **InputManager（静的・デバイス抽象化層）** — PlayerLoopに独自ノードを挿し、**全MonoBehaviourのUpdateより先に**入力を確定（`InputUpdater.cs:66-68`）。スキーム自動判定（KB/M・ゲームパッド・タッチ）、ゲームパッド用**仮想マウスカーソル**、カーソルロック/ワープ、タッチUIからの**ソフトウェア入力注入**（`Request()` — 物理入力と同じアクションIDチャネルに流す）を持つ。
2. **Keymapスタック（コンテキスト排他層）** — 画面ごとに`Keymap`サブクラスを定義し、`KeymapManager`がスタック管理。**最上位のKeymapのみバインドが有効**（Activate時に全バインドをリセットして自分のバインドだけ登録、`KeymapManager.cs:130-138`）。ダイアログを積めば背後の画面の入力は物理的に届かない。`KeymapGuard.Guard()`で「演出中は全入力遮断」がusing一行で書ける。Escape/Bボタンの「戻る」は`OnBackRequest()`で統一ルーティング。
3. **宣言的バインド定義** — 各Keymapが`CreateKeyboardKeymap()`/`CreateGamepadKeymap()`をオーバーライドし、「アクションID × 割当キー × 判定種別（Begin/Press/Repeat/End/長押し時間）× 修飾キーコード × 優先度」を列挙（`AdventureKeymap.cs:61-89`が好例）。**その画面で効くキーが1メソッドに全部並ぶ**一覧性が最大の価値。
4. **AssignSettings（リバインド永続化層）** — アクションIDは`enum AssignId`で**シリアライズ値を絶対に変えない**運用（廃止はコメントアウト、`KeyboardAssignSettings.cs:32-34`）。GroupRange=10000で機能グループ分け、プリセット（Fix1/Fix2/Custom）、**データバージョン不一致で自動リセット**、リバインド不可の固定キーは別枠。UIのキーガイド表示もこのIDから引く。

## moorestechの現状と課題

- 正規経路`Client.Input.InputManager`（InputAction直結・Player/Playable/UIの3グループ）と移行層`HybridInput`（KeyCode直読み）の**2系統が並存**。`UIRoot.cs:26`に「TODO InputManagerに移動」とあり是正途上。
- **生KeyCodeが19ファイル39箇所**。「B=ビルドメニュー」等が複数Stateに重複ハードコードされ、`HybridInput.ToInputSystemKey`のホワイトリスト（18キー手書き）に無いキーは**QueueStateEvent注入が効かないサイレントフォールバック**になる — プレイテストで無言で入力が効かない既知の罠と同根。
- **リバインド機能なし**。実行時バインドオーバーライドのAPIは一切未使用。キーバインドは`moorestechInputSettings.inputactions`にビルド時固定。
- 排他制御はUIStateマシン（`UIStateEnum` 13ステート）＋`WebUiInputExclusivity`（CEFテキストフォーカス時キーボード抑止、WebSocket `op:"input_state"` 経由）＋`WebUiScreenGate`の3枚で、機能はしているが「どのStateでどのキーが生きているか」は各Stateのif文の中にしか存在しない。
- ゲームパッドはスキーム定義（KeyboardMouse/Gamepad/Xbox/PS4）だけあってバインド・カーソル・ナビゲーションは実質皆無。
- ホットバー数字キー(1-9)は独自Composite `HotBarKeyBoardComposite` で実装済み（Composite活用の前例）。
- プレイテストDSLの注入層 `Client.Playtest.Input.SemanticInput` は `InputSystem.QueueStateEvent` で注入し、InputManager経由・HybridInput経由の両方に効く。マウスは `CefInputForwarder` がCEFへも複製転送する。

## 移植提案（優先度順）

### ① アクションID一元化 + State別宣言的キーマップ（最優先・単独で価値大）

Neonの`AssignId`パターンで`GameActionId` enum（安定値・グループ範囲・廃止はコメントアウト）を`Client.Input`に導入し、各UIStateに`CreateKeymap()`相当の宣言メソッドを持たせて「このStateで効くアクション→キー」を列挙させる。判定はInputSystemの`InputAction`に寄せ、`HybridInput`のKeyCode直書き39箇所を全廃。「変更の波及を恐れない」原則どおり全呼び出し側を一括更新する。ホワイトリスト漏れによるプレイテスト無言死も構造的に消える。

### ② Keymapスタックは移植しない（裁定事項）

moorestechでは`UIStateControl`が「唯一の正」の遷移権威と明記されており、Neonの`KeymapManager`スタックを併設すると権威が二重になる。スタックの利点（最上位のみ有効・Guard・Back統一）は、①の宣言的キーマップを「現在のUIStateのものだけ有効化する」ことで同等に得られる。`KeymapGuard`相当は「空キーマップのState」（Skit/Story用に実質既存）で足りる。

### ③ リバインド永続化（①の上に載せる）

Neonのような自前バインド機構は不要で、InputSystem標準の`SaveBindingOverridesAsJson`/`LoadBindingOverridesFromJson`を使い、Neonから借りるのは**運用設計**の方:

- (a) 保存データにバージョンを持たせ不一致で全リセット（`AAssignSettings.OnAfterDeserialize`）
- (b) プリセット＋部分カスタム
- (c) リバインド禁止の固定キー枠
- (d) UIキーガイド表示をアクションIDから引く仕組み

### ④ バインド意味論の拡充（必要になった時）

現状`InputKey`はDown/Hold/Upのみ。NeonのValidateType相当のうちmoorestechに実益があるのは**修飾キーコード**（Ctrl+I等が現在ベタ書き）と**長押し判定**（TimeSensitiveBind）。InputSystemのInteraction（Hold/Tap）とComposite（`HotBarKeyBoardComposite`の前例あり）で表現可能。

### ⑤ ゲームパッド対応時のロードマップ（将来枠）

Neonの`InputControlScheme`（最終使用デバイスでスキーム自動切替）＋仮想マウスは、moorestechでは面白い符合がある: **UIがCEFなので、ゲームパッド用仮想カーソルは`CefInputForwarder`（プレイテスト用の仮想マウス→CEF転送）とほぼ同じ機構で実現できる**。スティック→仮想カーソル座標→`SendMouseMove/Click`転送、という経路の骨格が既にある。着手時はNeonの「切替フレームの座標乖離ケア」（`InputControlScheme.cs:81-94`）が参考になる。

### 移植しない方がよいもの

- Aktsk依存のAction1-4チャネルモデル（InputSystemコールバックで足りる）
- `ClearDragEventInvoker`のリフレクションハック（InputSystemUIInputModule内部フィールドをリフレクションで書き換えるドラッグ状態リセット）
- `static partial class InputManager`スタイル（本リポジトリはpartial全面禁止）
- タッチ/ジャイロ層

## 主要ファイル一覧

### Neon側

- `Neon.Input/Scripts/InputManager.cs` — 静的デバイス抽象化層（562行）
- `Neon.Input/Scripts/InputUpdater.cs` — PlayerLoop挿入
- `Neon.Input/Scripts/Keymap.cs` / `KeymapManager.cs` / `KeymapGuard.cs` — Keymapスタック
- `Neon.Input/Scripts/Mapping/KeyboardMapping.cs` / `GamepadMapping.cs` — 宣言的バインド
- `Neon.Input/Scripts/AssignSetting/AAssignSettings.cs` / `KeyboardAssignSettings.cs` — リバインド永続化
- `Neon.Input/Scripts/InputControlScheme.cs` — スキーム自動切替＋仮想マウス統合
- `Neon.Game/Scripts/Scenes/Adventure/Sequence/Bindings/AdventureKeymap.cs` — ゲームプレイKeymapの好例

### moorestech側

- `moorestech_client/Assets/Scripts/Client.Input/InputManager.cs` — 正規入力ゲートウェイ
- `moorestech_client/Assets/Scripts/Client.Input/HybridInput.cs` — KeyCode直読み移行層
- `moorestech_client/Assets/Scripts/Client.Input/WebUiInputExclusivity.cs` — CEFフォーカス抑止
- `moorestech_client/Assets/Scripts/Client.Input/HotBarKeyBoardComposite.cs` — 独自Composite前例
- `moorestech_client/Assets/Asset/Common/moorestechInputSettings.inputactions` — バインド定義本体
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/UIStateControl.cs` — 遷移権威
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebSocketMessageDispatcher.cs` — input_state受信
- `moorestech_client/Assets/Scripts/Client.Playtest/Input/SemanticInput.cs` — QueueStateEvent注入
- `moorestech_client/Assets/Scripts/Client.Playtest/WebUi/CefInputForwarder.cs` — 仮想マウス→CEF転送
