# UITK 段階1 input レーン（入力・フォーカス・IME・DSL・テストの土台） Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** UITK の画面が乗る入力の基盤（ポインタ判定の論理和・入力排他の汎用化・ESC ラッチ・UI 専用アクション資産・フォーカス方針・IME モードの一元化・共通 TextField 部品・DSL の UITK ロケータ・L1/L2 のテストの土台）を、配布画面の見た目と挙動を変えずに `feature/uitk-s1-input` へ入れる。

**Architecture:** 入力の裁きは `Client.Input` の `UiInputExclusivity`（`WebUiInputExclusivity` を改名・汎用化）と `Client.Game` の `UiPointerHitTest` の 2 か所に保つ。`Client.Game`・`Client.Input` は `Client.Ui` を参照できないので、UITK 側の値は `Client.Ui` が push する（文字入力フォーカス・ESC ラッチ）か、`Client.Input` が定義するインターフェース `IGameUiPointerProbe` を `Client.Ui` が実装して登録する（ポインタ）。UITK のキー・フォーカスの既定処理はルートの TrickleDown で裁き、IME のモードは「どちらかの入力欄にフォーカスがあるか」の 1 本の ReactiveProperty から決める。DSL は `IUiElementLocator` の UITK 版と、UITK を先に探す両系合成を足す。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（UIDocument・PanelEventHandler・TextField）、Input System 1.x（InputSystemUIInputModule・`.inputactions`）、UI Test Framework 1.0.0（`UITestFixture`・`PanelSimulator`）、UniRx、VContainer、UniTask、NUnit

## Requirements

設計の正本は `docs/uitk-migration/plan.md` §2.2「作業（基盤）」の入力・DSL・テストの土台の行、§3.8、§4.2〜4.4、§4.8〜4.9、bd `moorestech-albof.12`、`.decisions/` の UITK 裁定（特に `2026-10-26-UITKの主パネル表示中はWeb-UIを全面的にオフにする.md`）。grill は不要（段階計画とユーザー裁定で設計済み）。

- R-1 **改名**: `WebUiInputExclusivity` を `UiInputExclusivity` へ改名する（旧名は残さない）。骨組みが足した休止の口 `SetWebUiSuspended` も同じクラスへ移す。受け入れ: `grep -rn "WebUiInputExclusivity" moorestech_client/Assets/Scripts` が 0 件、compile 成功、既存の `WebUiInputExclusivitySuspensionTest`（改名後 `UiInputExclusivityWebSuspensionTest`）・`TextInputMovementLockTest`・`PlayableInputInteractInputTest`・`DeleteInputFixture` を使うテストが緑
- R-2 **入力排他の汎用化（§3.8）**: `UiInputExclusivity` が Web 側の値と GameUi 側の値を別々に持ち、論理和で答える。`IsTextInputFocused` と `TextInputFocused`（ReactiveProperty）＝「GameUi の入力欄にフォーカス」∨「Web が休止中でなく Web の入力欄にフォーカス」。`IsPointerOverWebUi` は今どおり（休止中は false）。GameUi 側の書き込み口 `SetGameUiTextInputFocused(bool)` を足す。受け入れ: L0 テストで論理和・休止中は Web 側を読まない・休止解除で Web の古い値を捨てる・GameUi の値は休止に影響されない
- R-3 **ポインタの論理和（§3.8 ポインタ）**: `UiPointerHitTest.IsPointerOverAnyUi` を「ロック中は false。そうでなければ GameUi の Pick が層コンテナ以外を返す ∨ Web の pointerOverUi（休止中でないときだけ）∨ EventSystem（uGUI）」にする。呼び出し側（23 ファイル・28 か所）は変えない。GameUi の Pick は `Client.Input` に置くインターフェース `IGameUiPointerProbe { bool IsPointerOverGameUi(Vector2 screenPosition); }` を `Client.Ui` が実装し `UiInputExclusivity.SetGameUiPointerProbe(probe)` で登録する（未登録＝GameUi 無しは false）。受け入れ: L0（probe の登録で論理和になる）、L1（層コンテナだけなら false、要素があれば true、`PickingMode.Ignore` の要素は false）、L3（実ゲームで UITK の要素の上で `IsPointerOverAnyUi` が true）
- R-4 **ESC ラッチ（§3.8 ESC・S5）**: 「このフレームの ESC は UI が使った」ラッチ `UiInputExclusivity.ConsumeEscapeThisFrame()`／`IsEscapeConsumedThisFrame` を足し、ESC に割り当てた `InputKey`（`CloseUI`・`OpenMenu`）と `HybridInput.GetKeyDown(KeyCode.Escape)` がラッチ中は押下を返さない（`Time.frameCount` で 1 フレームだけ効く）。GameUi の入力欄で ESC が来たら、ルートの TrickleDown で (a) ラッチを立て (b) TextField の既定 ESC 処理（値の巻き戻しと再フォーカス）を止め (c) 値を保ったままフォーカスだけ外す。IME の変換中（`Input.compositionString` が空でない）は (a) だけ行い、フォーカスは外さず変換の取り消しを OS に任せる。受け入れ: L0（ラッチは同フレームだけ・ESC 以外のキーは影響なし）、L1（確定後の ESC で値が残りフォーカスが外れ、再フォーカスされない）、S5 の結論を台帳に書く
- R-5 **UI 専用アクション資産（§3.8・S4）**: `moorestech_client/Assets/Input/MoorestechUiInputActions.inputactions` を作る（アクションマップ `UI` に Point・Click・RightClick・MiddleClick・ScrollWheel・TrackedDevicePosition・TrackedDeviceOrientation を `DefaultInputActions` と同じ名前・同じバインドで持つ。Navigate・Submit・Cancel は持たない）。MainGame シーンの `InputSystemUIInputModule.actionsAsset`（`Assets/Scenes/Game/MainGame.unity:8435`）を `uloop execute-dynamic-code` で差し替え、move・submit・cancel の参照が null になることを確かめる。S4: 差し替え前後で、フォーカスを持つ UITK 要素に対し WASD・矢印・Space・Enter の注入（QueueStateEvent）で `NavigationMoveEvent`・`NavigationSubmitEvent` が来るかを数える。止められなかったものはルートの TrickleDown で打ち消す。受け入れ: L3 で差し替え後に 0 件、結論と数値を台帳へ
- R-6 **フォーカス方針（§3.8 フォーカス）**: 入力欄（`TextField`）以外は `focusable=false`。ルートの TrickleDown で (a) `NavigationMoveEvent`（Tab の Next/Previous を含む全方向）と `NavigationSubmitEvent`・`NavigationCancelEvent` を打ち消す（Tab は入力モジュールを通らないためアクション資産では止まらない）、(b) TextField 以外の要素がフォーカスを得たら即座に外し、要素名付きで LogWarning を出す（部品の作り忘れに開発者が気づく経路）。受け入れ: L1 で Space・Enter でボタンが押されない、Tab でフォーカスが動かない、Button にフォーカスが残らない
- R-7 **文字入力フォーカスの同期（§3.8 文字入力）**: ルートで `FocusInEvent`・`FocusOutEvent` を受け、フォーカス中の要素が TextField（の内部要素を含む）かを `UiInputExclusivity.SetGameUiTextInputFocused` へ同期で書く。受け入れ: L1（フォーカスで true、外すと false、TextField の内部要素へのフォーカスでも true）、L3（実ゲームで UITK の入力欄にフォーカス中は移動が止まり ESC でポーズが開かない＝`.decisions/2026-10-01-文字入力欄にフォーカス中は移動を止めたままにする.md`）
- R-8 **IME モードの一元化（albof.12 の 2・4）**: `Input.imeCompositionMode` を「`UiInputExclusivity.TextInputFocused`（論理和）が true なら On、false なら Off」に 1 か所で決める。UITK の TextField がフォーカスアウトで Auto に戻す処理の後に効くよう、値の変化を購読してメインスレッドで適用する（Web の値は WS スレッドから来るので `ObserveOnMainThread`）。起動時の CEF の On 固定も上書きされる（Web の入力欄にフォーカスが来れば On になる）。受け入れ: L3 で UITK の欄のフォーカス・フォーカスアウト・Web 側 true の各場合のモード値を確かめる
- R-9 **Web 側の上書きで ESC が漏れない（albof.12 の 3）**: Web の `textInputFocused=false` が来ても GameUi の欄にフォーカスがあれば `IsTextInputFocused` は true のまま（R-2 の論理和で塞ぐ）。受け入れ: L0
- R-10 **共通 TextField 部品（§3.4 段階 1・albof.12 の 5〜7・§4.9）**: `Client.Ui/Components/TextField/GameTextField`（`TextField` を包む VisualElement。単行・複数行は ctor 引数）。(a) `selectAllOnFocus=false`・`selectAllOnMouseUp=false`、(b) 変換中の文字に下線（未確定範囲の下に 1px の線の子要素を置き、`Input.compositionString` の変化で位置と幅を更新。複数行の折り返しをまたぐ未確定文字は先頭行ぶんだけ引く割り切りでよい）、(c) 候補窓をキャレットの位置に出す（キャレットのパネル座標をスクリーンのピクセルへ換算して `Input.compositionCursorPos` へ書く。換算は純関数 `ImeCaretScreenPosition.FromPanel(panelPoint, panelSize, screenSize)` に分け L0 で固定）、(d) 値の購読は `IObservable<string>`（未確定の文字列も流れる。購読側は未確定でも壊れないこと、を XML コメントに書く）、(e) 名前 `name` は testid と同じ文字列を受ける。受け入れ: L1（クリックで全選択しない・下線要素の表示と非表示・値の購読）、L0（座標換算）。IME の実機確認（Mac の osascript 合成キー）は Task 7 で 1 日まで試し、できなければ「未完了」として台帳と完了報告へ書く（バグ報告画面での本番確認は screens レーン）
- R-11 **DSL の UITK ロケータ（§4.4）**: `Client.Playtest/UiElements/GameUi/GameUiElementLocator : IUiElementLocator`（パネルは `GameUiRoot.Instance` から）と、UITK を先に探す両系合成 `CompositeUiElementLocator`。手順は §4.4 の 1〜4（`Query(name)` で全件・2 件以上は Ambiguous・祖先の `resolvedStyle` と `worldBound` 面積で可視・`panel.Pick(中心)` が自身か子孫で Clickable・パネル座標→Unity スクリーン座標〈左下原点〉）。状態フラグは USS クラスのうち `--` を含むもの（`--selected` 等）。`SetText` は `TextField.value` を設定して ChangeEvent を流す（`SetValueWithoutNotify` は使わない）。読み取り専用・非表示・無効は `NotInteractable`、TextField 以外は `NotTextField`、設定後の値が違えば `ValueMismatch`。合成は UITK と Web の両方で Found なら Ambiguous を返し理由をログへ出す。`PlaytestUiLocatorSession.Locator` を合成へ差し替える。受け入れ: L1（UiTestHost のパネルに要素を置いた Find／SetText／Ambiguous／被覆）、L0（パネル→スクリーン座標の換算）、合成の L0（偽の 2 ロケータで優先順と重複）
- R-12 **テストの土台（§4.3・S13）**: (a) `Client.Tests/Ui/TestDoubles/` に `FakeModel<T>`（ReactiveProperty で組む偽モデルの基底）と `CommandRecorder<TCommand>`（発行の記録）を置き、使い方を XML コメントに書く（画面レーンは `FakeXxxModel : FakeModel<XxxViewModel>` と `RecordingXxxCommands : IXxxCommands` を作る）。(b) L2 規約テスト `Client.Tests/Ui/Conventions/` で `Client.Ui` 配下の USS・UXML をテキストとして読み、§3.5 の規約（`Tokens.uss`・`SkitTokens.uss`・`SlotTokens.uss` の外で色リテラル〈`#`・`rgb(`・`rgba(`〉禁止、構造擬似クラス〈`:first-child` `:last-child` `:nth-child`〉禁止、`z-index` 禁止、UXML の `text="…"` のリテラル禁止、同一 UXML 内の `name` 重複禁止、ビットマップ参照〈`.png` `.jpg` `.psd`〉禁止、`-unity-font-style: bold` 禁止）を落とす。違反を作った一時文字列で検査関数が落ちることも確かめる。(c) 最初の L1（本番テーマの要素で schedule と transition を進める）: UiTestHost 上で、USS の `transition` を持つ要素のクラスを切り替え、`simulate.FrameUpdate(ms)` で時間を進めて `resolvedStyle` が途中値→終値になること、`schedule.Execute(...).StartingIn(ms)` が時間で発火すること。(d) S13: CI（`run_test.yml`）の client shard で L1 が batchmode で動くか。`gh workflow run run_test.yml --ref feature/uitk-s1-input` で 1 回流して結果を台帳へ書く（動かなければ plan §6 の 3 に従い原因と結論を書く）
- R-13 **拡張点の明記**: `UiInputExclusivity`・`GameUiInputRules`（ルートの入力規則）・`GameTextField`・`GameUiElementLocator`・テストの雛形の冒頭に「後続レーンが足す場所」を書く
- R-14 配布画面の見た目と挙動は変えない（UITK の層は空のまま。資産の差し替えは Navigate・Submit・Cancel を外すだけで、uGUI の既存画面〈タイトル・HP バー・デバッグ UI〉はキーボードでの選択移動を使っていないことを確かめる）
- やらないこと: テーマ・フォント・UiMotion（theme レーン）、ポーズ等の画面と §3.11 の CEF 停止の記録（無応答時に Web 側の値を無視する閾値を含む。screens レーン）、`wheel-passthrough`（段階 2）、ESC 以外のキー処理の置き場、ゲームパッド、`AnchorIds`、ref-uitk-design、moores-code-review の UITK 観点、録画プレイテスト、moores-code-review・pr-create

## Global Constraints

- AGENTS.md: 1 ファイル 200 行以下・1 ディレクトリ 10 ファイル以下、partial 禁止、`Func<>` 禁止、try-catch 原則禁止、イベントは UniRx（`Action` のイベントを新設しない）、日英 2 行コメント（各 1 行）、`[SerializeField]` は `_` 無し小文字キャメル、単純 getter/setter 禁止（`{ get; private set; }` は可）、デフォルト引数禁止、初期化は `Initialize`、エディタ専用コードは `#if UNITY_EDITOR` でファイル末尾、`.meta` を手で作らない、prefab・シーン・ScriptableObject は `uloop execute-dynamic-code` 経由でだけ変える（`.inputactions` は JSON なのでテキストで書いてよい）
- 名前に技術名（Uitk）を入れない。UITK 側を指す語は `GameUi`（骨組みの `GameUiRoot`・`UiSurfaceOwner.GameUi` に合わせる）
- `Client.Game`・`Client.Input` から `Client.Ui` を参照しない（参照の向き `Client.Game` ← `Client.Ui.State` ← `Client.Ui`）。`Client.Input` は今の参照（`Unity.InputSystem`・`UniRx`）のまま
- fail-closed で拒否・無視する経路は理由をログへ出す。黙って続行する行には `silent-ok: <理由> / <誰がどこで気づくか>` を書く
- テストは `--filter-type regex` で絞る。`Client.Tests.Playtest` 系を選ぶ広い正規表現は禁止（本番送信テストを踏む）
- `.cs` を変えたら `uloop compile --project-path ./moorestech_client`
- EditModeInPlayingTest には `[Category("CiShardClientPlay1")]` を付ける（骨組みの前例）
- バックグラウンドのポーリングで待たず、コマンドは前景で実行する。区切りごとにコミットする
- Unity Editor はこの worktree の 1 本だけを使う。起動・停止しない（落ちていたら `moores-wt status` で自分のものか確かめてから `uloop launch`）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 本体の inline 検査（判断記録 D-4〜D-6）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱1・第3バケツ0 / 弱=ESC ラッチは ESC 専用で、他キーのラッチは段階 2 以降で要るまで足さない（D-8）

---

## File Structure

| ファイル | 責務 |
|---|---|
| `Client.Input/UiInputExclusivity.cs`（旧 `WebUiInputExclusivity.cs` を `git mv`） | 入力排他の唯一の窓口。Web 側と GameUi 側の値・休止・ESC ラッチ・ポインタ probe の登録 |
| `Client.Input/IGameUiPointerProbe.cs` | GameUi の Pick を `Client.Input` から引くインターフェース |
| `Client.Input/InputManager.cs`・`HybridInput.cs` | 抑止判定に ESC ラッチを足す |
| `Client.Game/InGame/Control/UiPointerHitTest.cs` | 論理和の判定 |
| `Client.Ui/Foundation/Input/GameUiInputRules.cs` | ルートの TrickleDown で、フォーカス同期・ESC・ナビゲーション打ち消し・非入力欄のフォーカス拒否 |
| `Client.Ui/Foundation/Input/GameUiPointerProbe.cs` | `IGameUiPointerProbe` の実装（スクリーン→パネル座標・`panel.Pick`） |
| `Client.Ui/Foundation/Input/ImeCompositionModeApplier.cs` | 論理和の TextInputFocused から IME モードを決める（DI の entry point） |
| `Client.Ui/Foundation/Input/GameUiInputInstaller.cs` | `GameUiRoot` のルートへ規則と probe を取り付ける（DI の entry point） |
| `Client.Ui/Components/TextField/GameTextField.cs`・`ImeCompositionUnderline.cs`・`ImeCaretScreenPosition.cs`・`GameTextField.uss` | 共通 TextField 部品 |
| `Client.Starter/Registration/MainGameUiRegistration.cs` | entry point の登録を足す |
| `Assets/Input/MoorestechUiInputActions.inputactions` | UI 専用のアクション資産 |
| `Assets/Scenes/Game/MainGame.unity` | 入力モジュールの資産参照（uloop で差し替え） |
| `Client.Playtest/UiElements/GameUi/GameUiElementLocator.cs`・`GameUiElementQuery.cs`・`GameUiScreenMapper.cs` | UITK 版ロケータ |
| `Client.Playtest/UiElements/CompositeUiElementLocator.cs`・`PlaytestUiLocatorSession.cs` | 両系合成と差し替え |
| `Client.Playtest/Client.Playtest.asmdef` | `Client.Ui` の参照を足す |
| `Client.Tests/Ui/Input/…`・`Client.Tests/Ui/Components/…`・`Client.Tests/Ui/TestDoubles/…`・`Client.Tests/Ui/Conventions/…`・`Client.Tests/Ui/Motion/…`・`Client.Tests/Ui/Dsl/…` | L0・L1・L2 |
| `Client.Tests/EditModeInPlayingTest/Ui/GameUiInputTest.cs` | L3（S4 の計数・実ゲームの入力結線） |
| `docs/uitk/TODO.md` | 「## 実測の結論」に S4・S5・S13 を足す |

※ `Client.Tests`（`Tests.asmdef`）は `Client.Playtest`・`Client.Ui` を参照済み。DSL のテストは名前空間 `Client.Tests.Ui.Dsl` に置く（`Client.Tests.Playtest` 名前空間には置かない。広い正規表現で本番送信テストを踏まないため）

---

### Task 1: `UiInputExclusivity` への改名と汎用化・ESC ラッチ・ポインタの論理和

**Files:**
- Rename: `Client.Input/WebUiInputExclusivity.cs` → `Client.Input/UiInputExclusivity.cs`（`git mv`。`.meta` も一緒に mv して GUID を保つ）
- Create: `Client.Input/IGameUiPointerProbe.cs`
- Modify: `Client.Input/InputManager.cs`、`Client.Input/HybridInput.cs`、`Client.Game/InGame/Control/UiPointerHitTest.cs`、`Client.Game/InGame/Player/PlayerSystemContainer.cs`、`Client.Playtest/Operations/Ui/PlaytestUiOps.cs`、`Client.WebUiHost/Boot/WebSocketHub.cs`、`Client.WebUiHost/Boot/WebSocketMessageDispatcher.cs`、`Client.Ui/Foundation/Routing/WebUiSuspension.cs`、テスト 5 ファイル（`grep -rln WebUiInputExclusivity`）
- Test: `Client.Tests/Ui/Routing/WebUiInputExclusivitySuspensionTest.cs` → `Client.Tests/Ui/Input/UiInputExclusivityWebSuspensionTest.cs`（`git mv`）、新規 `Client.Tests/Ui/Input/UiInputExclusivityGameUiTest.cs`

**Interfaces:**
- Produces（`namespace Client.Input`）:
  - `public interface IGameUiPointerProbe { bool IsPointerOverGameUi(Vector2 screenPosition); }`
  - `public static class UiInputExclusivity`:
    - 既存（名前そのまま）: `IsPointerOverWebUi`、`IsTextInputFocused`、`TextInputFocused`（`IReadOnlyReactiveProperty<bool>`。意味を論理和へ変える）、`SetState(bool pointerOverUi, bool textInputFocused)`（Web 側の書き込み。WS スレッド）、`SetWebUiSuspended(bool)`、`IsSuppressed(InputSuppressionScope)`、`ProbeSuppressed(InputSuppressionScope)`
    - 新規: `SetGameUiTextInputFocused(bool focused)`（メインスレッド）、`SetGameUiPointerProbe(IGameUiPointerProbe probe)`（null で解除）、`bool IsPointerOverGameUi(Vector2 screenPosition)`（probe 未登録は false）、`void ConsumeEscapeThisFrame()`、`bool IsEscapeConsumedThisFrame`
  - `InputKey` は ESC ラッチ中かつ自分のアクションのコントロールに `Keyboard.current.escapeKey` が含まれるとき、`GetKeyDown`・`GetKey`・`started`/`performed` の通知を抑止する（`GetKeyUp`・`canceled` は今どおり通す）
  - `HybridInput.GetKeyDown(KeyCode.Escape)`・`GetKey(KeyCode.Escape)` も同じ

- [ ] **Step 1: 改名** — `git mv` で本体とテストを改名し、全参照を置換する（`sed` 可）。ログの接頭辞 `[WebUiInput]`・`[WebUiInputProbe]` は `[UiInput]`・`[UiInputProbe]` にする
- [ ] **Step 2: 論理和にする** — Web 側の値（`_webTextInputFocused`）と GameUi 側の値（`_gameUiTextInputFocused`）を別に持ち、`TextInputFocusedProperty.Value = _gameUi || (!_suspended && _web)` を、どちらかの書き込みと休止切替のたびに lock の中で再計算する。`IsSuppressed` は `IsTextInputFocused` を見る（休止中でも GameUi の欄なら抑止する）。`ProbeSuppressed` のログ文言は「Web」と決め打ちせず、どちら由来かを出す
- [ ] **Step 3: ESC ラッチ** — `_escapeConsumedFrame = Time.frameCount`。`IsEscapeConsumedThisFrame => _escapeConsumedFrame == Time.frameCount`。立てたときに 1 行ログ（`[UiInput] escape consumed by game UI at frame N`）。`InputKey` は ctor で自分の `InputAction` が ESC に割り当たっているかを調べず、判定のたびに `IsBoundControl(Keyboard.current.escapeKey)` を見る（再バインドに追従するため。`Keyboard.current` が null なら抑止しない）
- [ ] **Step 4: ポインタ** — `UiPointerHitTest.IsPointerOverAnyUi()` を R-3 の形にする（`HybridInput.GetMousePosition()` を probe に渡す）
- [ ] **Step 5: テストを書く** — `UiInputExclusivityGameUiTest`: GameUi の欄で true・Web の false 上書きで true のまま（albof.12 の 3）・休止中も GameUi の欄は抑止・ESC ラッチは同フレームだけ（`Time.frameCount` が進まない EditMode では「立てる前は false・立てた後は true」と「別フレームの値を入れると false」を、ラッチのフレームを内部で書き換えず `ConsumeEscapeThisFrame` 前後で確かめる。フレームを跨ぐ確認は Task 6 の L3）・probe の登録と解除。TearDown で全部を初期値へ戻す
- [ ] **Step 6: compile とテスト** — `uloop compile --project-path ./moorestech_client`。`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UiInputExclusivity|TextInputMovementLock|PlayableInteractInput|DragDelete"`
- [ ] **Step 7: コミット** — `Task 1: 入力排他を UiInputExclusivity へ改名し GameUi 側の値・ESC ラッチ・ポインタの論理和を足す`

### Task 2: ルートの入力規則（フォーカス同期・ESC・ナビゲーション打ち消し・非入力欄のフォーカス拒否）・ポインタ probe・IME モード

**Files:**
- Create: `Client.Ui/Foundation/Input/GameUiInputRules.cs`、`GameUiPointerProbe.cs`、`ImeCompositionModeApplier.cs`、`GameUiInputInstaller.cs`
- Modify: `Client.Starter/Registration/MainGameUiRegistration.cs`
- Test: `Client.Tests/Ui/Input/GameUiInputRulesTest.cs`、`GameUiPointerProbeTest.cs`

**Interfaces:**
- Consumes: Task 1 の `UiInputExclusivity`・`IGameUiPointerProbe`、骨組みの `GameUiRoot`（`GetLayer`・`UIDocument`）、`UiLayerStack`
- Produces:
  - `public sealed class GameUiInputRules { public static void Attach(VisualElement root); }` — root に TrickleDown のコールバックを登録する（MonoBehaviour 非依存。L1 から同じものを付ける）
  - `public sealed class GameUiPointerProbe : IGameUiPointerProbe { public GameUiPointerProbe(IPanel panel); }` — スクリーン座標（左下原点 px）→ `RuntimePanelUtils.ScreenToPanel(panel, new Vector2(x, Screen.height - y))` → `panel.Pick`。Pick が null か、`PickingMode.Ignore` でない最初の要素が見つからなければ false。層コンテナは Ignore なので Pick に出ない（骨組みの L1 で固定済み）
  - `public class ImeCompositionModeApplier : IInitializable, IDisposable` — `UiInputExclusivity.TextInputFocused.ObserveOnMainThread().Subscribe(focused => Input.imeCompositionMode = focused ? IMECompositionMode.On : IMECompositionMode.Off)`。起動時にも現在値で 1 回適用する
  - `public class GameUiInputInstaller : IInitializable, IDisposable` — `GameUiRoot` の rootVisualElement に `GameUiInputRules.Attach` し、`UiInputExclusivity.SetGameUiPointerProbe(new GameUiPointerProbe(panel))`。Dispose で probe を外す

- [ ] **Step 1: 規則を書く** — `GameUiInputRules.Attach(root)` は次を `TrickleDown.TrickleDown` で登録する
  - `FocusInEvent`: 新しいフォーカス先が TextField（`evt.target` か祖先に `TextField` がある。`TextField` の内部の `TextInputBase`/`TextElement` に来るため祖先を辿る）なら `SetGameUiTextInputFocused(true)`。TextField でなければ `focusController.focusedElement.Blur()` を `root.schedule.Execute` で次の機会に行い、`Debug.LogWarning($"[GameUiInput] non text-field element took focus and was blurred: {name}")`（部品の作り忘れの検知経路）
  - `FocusOutEvent`: `evt.relatedTarget`（次のフォーカス先）が TextField でなければ `SetGameUiTextInputFocused(false)`
  - `KeyDownEvent`（`KeyCode.Escape`）: フォーカスが TextField にあるとき、`UiInputExclusivity.ConsumeEscapeThisFrame()`。`Input.compositionString` が空なら `evt.StopImmediatePropagation()` と既定処理の抑止（Unity 6 の API で、TextField の既定の ESC 処理〈値の巻き戻し・再フォーカス〉が走らない手段を選ぶ。`focusController.IgnoreEvent(evt)` が使えるならそれも）をして `Blur()`。変換中は伝播を止めない（OS の取り消しに任せる）
  - `NavigationMoveEvent`・`NavigationSubmitEvent`・`NavigationCancelEvent`: `evt.StopImmediatePropagation()` と既定処理の抑止（`focusController.IgnoreEvent` 相当）
- [ ] **Step 2: probe と IME と installer を書き、`MainGameUiRegistration` へ `builder.RegisterEntryPoint<GameUiInputInstaller>()` と `builder.RegisterEntryPoint<ImeCompositionModeApplier>()` を足す**
- [ ] **Step 3: L1 を書く** — `GameUiInputRulesTest : UiTestHost`（rootVisualElement に `GameUiInputRules.Attach`）:
  - TextField にフォーカス → `UiInputExclusivity.IsTextInputFocused` true、別の場所をクリック（`simulate.Click` で空き領域）か `Blur()` → false
  - TextField に "abc" を入れて確定済みのまま ESC（`simulate.KeyDown(KeyCode.Escape)` 等の PanelSimulator の API）→ 値は "abc" のまま、フォーカスが外れ、`IsEscapeConsumedThisFrame` true、数フレーム（`simulate.FrameUpdate()`）後も再フォーカスされない
  - 複数行（`multiline=true`）の欄で同じ（S9 で全文が消えた形）
  - `Button`（focusable を既定のまま）を置き、`Focus()` → フォーカスが残らない・LogWarning（`LogAssert.Expect`）
  - フォーカス中の要素へ `NavigationSubmitEvent`・`NavigationMoveEvent`（Next）を `rootVisualElement.panel` 経由で送る → Button の clicked が 0 回・フォーカスが動かない
  - `GameUiPointerProbeTest`: 空のパネル（層だけ）で false、Position の箱の上で true、Ignore の箱の上で false（スクリーン座標→パネル座標の換算は UiTestHost のパネルでは `RuntimePanelUtils` が使えない場合がある。その場合はパネル座標を直接受ける内部関数 `IsPickableAt(Vector2 panelPosition)` を L1 で試し、スクリーン換算は Task 6 の L3 で試す）
  - 各テストの TearDown で `UiInputExclusivity` を初期値に戻す
- [ ] **Step 4: compile とテスト** — `--filter-value "Client\.Tests\.Ui\."`
- [ ] **Step 5: コミット** — `Task 2: GameUi のルートにフォーカス同期・ESC・ナビゲーション打ち消しと Pick の probe・IME モードの一元化を足す`

### Task 3: UI 専用のアクション資産と S4・S5 の実測（L3）

**Files:**
- Create: `moorestech_client/Assets/Input/MoorestechUiInputActions.inputactions`（`Assets/Input` が無ければ既存の `.inputactions` の置き場〈`find moorestech_client/Assets -name "*.inputactions" -not -path "*/Library/*"`〉の隣）
- Modify（uloop 経由）: `Assets/Scenes/Game/MainGame.unity` の `InputSystemUIInputModule.actionsAsset`
- Test: `Client.Tests/EditModeInPlayingTest/Ui/GameUiInputTest.cs`（Task 6 と同じファイル。ここでは S4 の計数ケースを入れる）
- Modify: `docs/uitk/TODO.md`「## 実測の結論」に S4・S5 の行

- [ ] **Step 1: 資産を書く** — `DefaultInputActions`（`Library/PackageCache/com.unity.inputsystem@*/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions`）の `UI` マップから Point・Click・RightClick・MiddleClick・ScrollWheel・TrackedDevicePosition・TrackedDeviceOrientation を同じ名前・型・バインドで写す（id は新しい GUID）。Navigate・Submit・Cancel は入れない。Player マップは入れない。Unity にインポートさせ（`uloop compile` か AssetDatabase.Refresh）、`.meta` は Unity が作る
- [ ] **Step 2: 差し替え前の S4 を測る** — EditModeInPlayingTest のケース `UI資産の差し替え前後でWASDと矢印とSpaceとEnterがナビゲーションイベントを出すかを数える` を書く: MainGame を起動し、`GameUiRoot` の Screen 層に focusable な要素（`focusable=true`・`tabIndex=0`）を置いて `Focus()`、ルートに `NavigationMoveEvent`・`NavigationSubmitEvent` の計数コールバック（TrickleDown・数えるだけ）を付ける。ただし Task 2 の規則が打ち消すので、計数は規則より前で受ける（`GameUiInputRules` の打ち消しは伝播を止めるだけなので、TrickleDown でルートに先に登録した計数が先に呼ばれることを確かめる。順序が保証されなければ計数を root の親〈`panel.visualTree`〉に付ける）。`InputSystem.QueueStateEvent` で W・A・S・D・矢印 4 つ・Space・Enter を 1 つずつ押して離し、各キーの計数をログ（`[S4] key=W move=1 submit=0` の形）と Assert で残す。差し替え前の資産で測る分は、テスト中に一時的に `module.actionsAsset` を `DefaultInputActions` へ戻して測る（シーンは変えない）か、差し替え前に 1 回だけ実行してログを証拠として残す（どちらにしたか報告へ）
- [ ] **Step 3: シーンを差し替える** — `uloop execute-dynamic-code` で MainGame シーンを開き、EventSystem の `InputSystemUIInputModule.actionsAsset` に新しい資産を設定し、`point`・`leftClick`・`rightClick`・`middleClick`・`scrollWheel`・`trackedDevicePosition`・`trackedDeviceOrientation` が新しい資産の同名アクションを指し、`move`・`submit`・`cancel` が null になったことを出力で確かめ、シーンを保存する。差し替え前の参照（`m_ActionsAsset` の guid `ca9f5fa95ffab41fb9a615ab714db018`）を報告に書く
- [ ] **Step 4: 差し替え後の S4** — 同じケースで、差し替え後は全キーで move=0・submit=0 を Assert。止まらなかったキーがあれば Task 2 の規則が打ち消していることを、規則を通った後段（要素自身のコールバック）で 0 件として確かめる
- [ ] **Step 5: uGUI への影響を確かめる** — MainGame シーン内の uGUI の `Selectable`（`Button` 等）でキーボードの選択移動に依存するものが無いかを `uloop execute-dynamic-code` で列挙して報告へ（デバッグ UI など。Navigation の設定と用途）。依存があれば報告の懸念に書く
- [ ] **Step 6: S5 を調べる** — TextField 上の ESC を処理するのは `PanelEventHandler.Update`（IMGUI の Event キューから KeyDown を作る。`Library/PackageCache/com.unity.ugui@*/Runtime/UGUI/EventSystem/UIElements/PanelEventHandler.cs`）で、`UIStateControl.Update` と同じ既定の実行順（0）にある。`uloop execute-dynamic-code` で両者の実行順（`MonoImporter.GetExecutionOrder` と `DefaultExecutionOrder` 属性）を出し、PlayMode で両 Update の 1 フレーム内の先後をログ（一時的な計測用コード。コミットしない）で 1 回確かめる。注入（QueueStateEvent）は IMGUI のキューに入らないので、ESC の先後そのものはここでは測れない（OS 段の合成キーが要る。Task 7）。結論は「先後が決まっているか、決まっていなければ両順で閉じない設計（R-4：先に UITK が処理すればラッチ、先に UIStateControl が処理すれば入力欄フォーカス中の Keyboard 抑止）」の形で書く
- [ ] **Step 7: 台帳** — `docs/uitk/TODO.md`「## 実測の結論」へ S4・S5 の行（担当は「段階1 input レーン」、証拠は TestResults のテスト名とログ行）
- [ ] **Step 8: compile とテスト** — `--filter-value "GameUiInputTest"`（EditModeInPlayingTest。ドメインリロードの後に Reloading エラーが出たら 45 秒待って再試行）
- [ ] **Step 9: コミット** — `Task 3: UI 専用のアクション資産へ差し替え S4・S5 を実測する`

### Task 4: 共通 TextField 部品 `GameTextField`

**Files:**
- Create: `Client.Ui/Components/TextField/GameTextField.cs`、`ImeCompositionUnderline.cs`、`ImeCaretScreenPosition.cs`、`GameTextField.uss`
- Test: `Client.Tests/Ui/Components/GameTextFieldTest.cs`、`ImeCaretScreenPositionTest.cs`

**Interfaces:**
- Produces（`namespace Client.Ui.Components.TextField`。`UnityEngine.UIElements.TextField` と名前が衝突しないよう、using 側は完全修飾かエイリアス）:
  - `public sealed class GameTextField : VisualElement { public GameTextField(string testid, bool multiline); public string Value { get; } public IObservable<string> OnValueChanged { get; } public void SetValueWithoutNotify(string value); public UnityEngine.UIElements.TextField Field { get; } }` — `name = testid` は内側の `TextField` に付ける（DSL の `SetText` が TextField を見つけるため）。外側は `focusable=false`・`pickingMode=Ignore`
  - `public static class ImeCaretScreenPosition { public static Vector2 FromPanel(Vector2 panelPoint, Vector2 panelSize, Vector2 screenSize); }` — 返り値は `Input.compositionCursorPos` へ書く値（スクリーンのピクセル・左上原点）。パネルの拡縮（`screenSize / panelSize`。Expand のため x・y の同じ倍率 `min` ではなく、`RuntimePanelUtils` と同じ換算をする）を掛ける
  - 下線 `ImeCompositionUnderline`（GameTextField の内部。`Input.compositionString` を変化時だけ見る。変化の検知はフォーカス中だけ回す `schedule.Execute(...).Every(0)`〈パネルのスケジューラ。`Update()` は使わない〉で、フォーカスアウトで止める）

- [ ] **Step 1: 部品を書く** — (a) `selectAllOnFocus=false`・`selectAllOnMouseUp=false`、(b) 下線（未確定文字列の幅は `Field` の内部 `TextElement` の `MeasureTextSize(compositionString, …)` で測り、`textSelection.cursorPosition` を右端にして左へ伸ばす。1px・色はトークン `var(--…)`〈既存トークンから文字色に当たるものを選ぶ。無ければ `currentColor` 相当を USS で〉）、(c) 候補窓（フォーカス中・変換中のスケジュールで `Input.compositionCursorPos = ImeCaretScreenPosition.FromPanel(Field 内のキャレットのパネル座標, panel のサイズ, Screen のサイズ)`。UITK 自身が同じ値を書いた後に上書きされる順序を、Task 7 の実機確認で確かめる）、(d) `OnValueChanged` は `RegisterValueChangedCallback` を UniRx の Subject へ流す。XML コメントに「IME の変換中の未確定文字列も値として流れる。購読側（文字数の検証・即時の絞り込み）は未確定の文字列でも壊れないこと（§4.9）」
- [ ] **Step 2: L1** — `GameTextFieldTest : UiTestHost`（`GameUiInputRules.Attach` も付ける）: クリックでフォーカスしても全選択にならない（`Field.textSelection.selectIndex == cursorIndex`）、`OnValueChanged` が値の変更で流れる、下線は変換中でないとき非表示（`compositionString` は EditMode で作れないので、下線の更新関数を `internal void ApplyComposition(string compositionString)` に分けて直接呼び、表示・幅>0・非表示を確かめる）、`name` が内側の TextField に付く。`ImeCaretScreenPositionTest`: 1280×720 のパネルを 1920×1080 へ（1.5 倍・S9 の Windows の形）、2560×1080（21:9）へ、y の向き
- [ ] **Step 3: compile とテスト** — `--filter-value "Client\.Tests\.Ui\."`
- [ ] **Step 4: コミット** — `Task 4: 共通 TextField 部品に全選択の抑止・変換中の下線・候補窓の位置を入れる`

### Task 5: DSL の UITK ロケータと両系合成

**Files:**
- Create: `Client.Playtest/UiElements/GameUi/GameUiElementLocator.cs`、`GameUiElementQuery.cs`（パネルの照会の純粋な部分）、`GameUiScreenMapper.cs`（パネル↔スクリーン）、`Client.Playtest/UiElements/CompositeUiElementLocator.cs`
- Modify: `Client.Playtest/UiElements/PlaytestUiLocatorSession.cs`（`Locator` を `new CompositeUiElementLocator(new GameUiElementLocator(), new WebUiElementLocator())`）、`Client.Playtest/Client.Playtest.asmdef`（`Client.Ui` を足す）
- Test: `Client.Tests/Ui/Dsl/`（`GameUiElementQueryTest`・`CompositeUiElementLocatorTest`・`GameUiScreenMapperTest`）

**Interfaces:**
- Consumes: 既存 `IUiElementLocator`・`UiElementSnapshot`・`UiSetTextResult`・`UiSetTextStatus`・`UiElementLookupStatus`、骨組みの `GameUiRoot`
- Produces:
  - `internal static class GameUiElementQuery { UiElementSnapshot Find(VisualElement root, string testid, IGameUiScreenMapper mapper); UiSetTextResult SetText(VisualElement root, string testid, string text); }`（`IGameUiScreenMapper { Rect PanelToScreen(Rect panelRect); }`。本番は `GameUiScreenMapper`〈`Screen` と root の `worldBound` から倍率を出し、y を反転して左下原点へ〉、テストは恒等＋反転の偽）
  - `GameUiElementLocator : IUiElementLocator`（`IsAvailable` は `GameUiRoot.Instance != null`。`StartSession`・`StopSession` は何もしない〈UITK はプロセス内なので受け口が要らない〉。`Find`・`SetText` は同期結果を `UniTask.FromResult` で返す）
  - `CompositeUiElementLocator : IUiElementLocator`（ctor で `IUiElementLocator gameUi, IUiElementLocator web`。Find: 両方を照会し、両方 Found なら `Ambiguous` を返して `Debug.LogError($"[PlaytestUi] testid '{testid}' resolved in both game UI and web UI")`、片方 Found ならそれ、どちらも無ければ GameUi が使えるなら GameUi の NotFound、使えなければ Web の結果。SetText: GameUi で Found なら GameUi に入れる、そうでなければ Web。`IsAvailable` は論理和。Start/Stop は両方へ）

- [ ] **Step 1: 照会を書く** — §4.4 の 1〜4 と R-11。可視は「自身と祖先の `resolvedStyle.display != None`・`visibility == Visible`・`opacity > 0`、かつ `worldBound` の面積 > 0」。Clickable は「可視、かつ `panel.Pick(worldBound.center)` が自身か子孫」。テキストは TextField なら `value`、`TextElement` なら `text`、それ以外は子孫の `TextElement` の text を連結。状態フラグは `GetClasses()` のうち `--` を含むもの
- [ ] **Step 2: 合成と差し替え** — 差し替え後も Web だけの既存シナリオが同じ結果になること（GameUi は層が空で常に NotFound、Web が Found を返す）を合成の L0 で固定
- [ ] **Step 3: テスト** — 照会の L1（UiTestHost に `name` 付きの Label・TextField・覆う箱を置く）: Found・NotFound・Ambiguous（同名 2 件）・被覆で Clickable=false・`display:none` の祖先で Visible=false・SetText で ChangeEvent が 1 回来て値が入る・`isReadOnly` で NotInteractable・Label に SetText で NotTextField・`maxLength` で ValueMismatch。合成の L0（偽ロケータ 2 つ）。座標換算の L0
- [ ] **Step 4: compile とテスト**（`--filter-value "GameUiElement|CompositeUiElementLocator|GameUiScreenMapper"`。`Client.Tests.Playtest` 名前空間に置いた場合は、その名前空間全体を選ぶ正規表現にしないこと）
- [ ] **Step 5: コミット** — `Task 5: DSL に UITK 版ロケータと UITK を先に探す両系合成を足す`

### Task 6: テストの土台（雛形・L2 規約・最初の L1）と実ゲームの結合確認（L3）

**Files:**
- Create: `Client.Tests/Ui/TestDoubles/FakeModel.cs`、`CommandRecorder.cs`、`Client.Tests/Ui/TestDoubles/Samples/`（雛形の使い方を示す最小の例 1 組と、それを使う L1 テスト 1 本。例の画面は作らず、`FakeCounterModel`〈`FakeModel<int>`〉と `RecordingCounterCommands`〈`ICounterCommands { void Increment(); }`〉を Label と Button に結ぶ 20 行程度のテスト内ビュー）
- Create: `Client.Tests/Ui/Conventions/UiStyleConventionTest.cs`、`UiStyleConventionRules.cs`（検査関数。200 行を超えるなら UXML 用を分ける）
- Create: `Client.Tests/Ui/Motion/PanelTimeAdvanceTest.cs`（最初の L1：schedule と transition）
- Modify: `Client.Tests/EditModeInPlayingTest/Ui/GameUiInputTest.cs`（Task 3 で作ったファイルに結合ケースを足す）
- Modify: `docs/uitk/TODO.md`（S13）

- [ ] **Step 1: 雛形** — `FakeModel<T>`（`ReactiveProperty<T>` を持ち、`IReadOnlyReactiveProperty<T> Current`・`void Set(T value)`）、`CommandRecorder<TCommand>`（`IReadOnlyList<TCommand> Issued`・`void Record(TCommand command)`・`void Clear()`）。XML コメントに「Playwright の `setUiState` は FakeModel の Set、`payloadsOf` は Issued」と画面レーンの作り方を書く
- [ ] **Step 2: L2** — R-12(b) の規則を検査関数に実装し、`Client.Ui` 配下の全 `.uss`・`.uxml` に掛けるテストと、違反を含む文字列で各規則が落ちるテストを書く（今ある `Tokens.uss` 等の 3 枚は色リテラルの検査から外す。`GameTextField.uss` は対象）
- [ ] **Step 3: 最初の L1** — `PanelTimeAdvanceTest : UiTestHost`: 要素に `transition-property: opacity; transition-duration: 200ms` を当て（インライン style でよい）、opacity を 1→0 に切り替え、`simulate.FrameUpdate(100)` 相当（PanelSimulator の時間を進める API）で 0<opacity<1、さらに進めて 0。`schedule.Execute(...).StartingIn(300)` が 200ms では未発火・400ms で発火。本番テーマ（UiTestHost のテーマとトークン USS）が当たった状態で行う
- [ ] **Step 4: L3 の結合ケース** — `GameUiInputTest` に: (a) Screen 層に `GameTextField` を置いてフォーカス → `UiInputExclusivity.IsTextInputFocused` true・`Input.imeCompositionMode == On`・プレイヤーの移動が止まる（`TextInputMovementLockTest` の確かめ方に倣う）、(b) その状態で ESC を QueueStateEvent で押す → ポーズに入らない（`UIStateControl.CurrentState` が GameScreen のまま）、(c) フォーカスを外す（`Blur()`）→ false・`Off`、(d) Web 側から `SetState(false, true)` → `On`、(e) UITK の要素の上へマウスを QueueStateEvent で動かす → `UiPointerHitTest.IsPointerOverAnyUi()` true、要素を外すと false、(f) ESC ラッチを立てたフレームでは `InputManager.UI.CloseUI.GetKeyDown` が押下を返さず、次フレーム以降は返す（`SetKeyDownForTest` の前例を見て、ラッチの効き方を確かめられる形にする）
- [ ] **Step 5: compile とテスト** — `--filter-value "Client\.Tests\.Ui\."` と `--filter-value "GameUiInputTest|GameUiSkeletonTest"`
- [ ] **Step 6: コミットと push** — `Task 6: テストの雛形・L2 の USS/UXML 規約・最初の L1・実ゲームの入力結線テストを足す` の後 `git push origin feature/uitk-s1-input`
- [ ] **Step 7: S13** — `gh workflow run run_test.yml --ref feature/uitk-s1-input` を 1 回だけ実行し、`gh run list --workflow run_test.yml --branch feature/uitk-s1-input --limit 1` で run id を得て、`gh run watch <id> --exit-status`（前景で待つ。上限 60 分。超えたら打ち切って途中の状態を書く）。Client の shard で `Client.Tests.Ui.Motion.PanelTimeAdvanceTest` と `UiLayerStackTest` が通ったかを、artifact（`gh run download`）の結果 xml で確かめる。結論（動いた／動かなかった＋原因）を台帳 S13 の行へ書いてコミット

### Task 7: IME の実機確認（Mac・1 日まで）と締め

**Files:**
- Modify: `docs/uitk/TODO.md`（S9 の段階 1 の行への追記か、新しい行）

- [ ] **Step 1: Mac の Editor で IME を試す** — PlayMode の MainGame で Screen 層に `GameTextField`（複数行）を置く一時スクリプト（`uloop execute-dynamic-code` で注入。コミットしない）でフォーカスし、`osascript` の key code で入力ソースを日本語へ（104）→ ローマ字を打って変換 → 確定 → ESC、変換中の ESC、の各操作で、`uloop screenshot`（window モード。rendering モードには UITK が写らない）と、`Input.compositionString`・`Input.compositionCursorPos`・`Input.imeCompositionMode`・値・フォーカスをログで残す。確かめること: 確定後の ESC で値が残りフォーカスが外れポーズに入らない、変換中の ESC でポーズに入らない、下線が見える、候補窓がキャレットの近くに出る（スクショ）、クリックで全選択しない。手順は S9 の報告（https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-10-28-uitk-s9-ime-result/ の report-*.html）。Editor が前面に出るので、他セッションの作業を妨げない時間で 1 回だけ行う。1 日で成立しなければ打ち切り、何ができて何ができなかったかを書く
- [ ] **Step 2: 台帳** — 結果を `docs/uitk/TODO.md` の実測の結論へ足す（Windows は検証機の配布ビルドが要るので screens レーンのバグ報告画面の確認へ回す、と書く）
- [ ] **Step 3: 全体の compile とテスト** — compile、`--filter-value "Client\.Tests\.Ui\.|UiInputExclusivity|TextInputMovementLock|PlayableInteractInput|DragDelete|GameUiElement|CompositeUiElementLocator"`、`--filter-value "GameUiInputTest|GameUiSkeletonTest"`、`uloop get-logs --log-type Error`
- [ ] **Step 4: コミットと push**

---

## 判断記録（ADR）

- D-1 このレーンは moores-code-review と pr-create を行わない。段階 0 から続く裁定「各レーンは統合ブランチへ入れ、PR は段階で 1 本」（`.decisions/2026-10-26-UITK段階0のPRは1本に統合する.md` の段階 1 への適用）と、指揮セッションの依頼文による。最終レビューと PR は指揮セッションが統合後に行う。出所: ユーザー（指揮セッション経由の依頼）
- D-2 AskUserQuestion は行わない（依頼文「ユーザーへ質問しない。推奨案で進め、要裁定に書く」とユーザーの goal 指示）。出所: ユーザー
- D-3 UITK 側を指す語は `GameUi`（技術名を入れない裁定 10-29 と骨組みの命名に合わせる）
- D-4 配置: `Client.Game`・`Client.Input` は `Client.Ui` を参照できないので、UITK の Pick は `Client.Input` のインターフェース `IGameUiPointerProbe` を `Client.Ui` が実装して登録する（依存の逆転）。`Func<>` で渡す案は AGENTS.md で禁止。前例: 骨組みの `WebUiSuspension` が `Client.Ui` から `Client.Input` の static へ push している形
- D-5 配置: 文字入力フォーカスと ESC ラッチは `Client.Ui` が `UiInputExclusivity` へ同期で push する（§3.8「同じスレッド・同じフレームで書くので lock も ObserveOnMainThread も要らない」）。Web 側は WS スレッドからなので、論理和の再計算は既存の lock の中で行う
- D-6 配置: ルートの入力規則は MonoBehaviour 非依存の `GameUiInputRules.Attach(VisualElement)` にし、DI の entry point が `GameUiRoot` のルートへ付ける。L1 は同じ関数を `UiTestHost` のルートへ付けて試す（§3.1 の 3 層の「View は MonoBehaviour に依存しない」に合わせる）
- D-7 ESC ラッチは押下だけを抑止し、解放（`GetKeyUp`・`canceled`）は通す（既存の `InputKey` が「解放は抑止中も届ける」としている前例に合わせる）
- D-8 ラッチは ESC 専用。plan §3.8 のキー処理の置き場（←→・Enter）は画面の Presenter が持つので、汎用のキーラッチは要るまで作らない（Phase 2.6 弱）
- D-9 S5 は「どちらが先か」に依存しない設計にした（先に UITK が処理すればラッチ、先に `UIStateControl` が処理すれば入力欄フォーカス中の Keyboard 抑止で止まる）。実測は実行順の確認と、OS 段の合成キーでの実機確認（Task 7）で行う。InputSystem の注入は IMGUI のキューに入らないので L3 では ESC の先後を測れない
- D-10 IME モードは「フォーカスが無いときは Off」にする（albof.12 の 4「どの欄にも触れていないのにワールドで変換が始まる」を塞ぐため。Auto は OS 任せで変換が始まりうる）。CEF の初期化時の On は、Web の入力欄にフォーカスが来たときの On で置き換わる。Web の欄で日本語が打てることは CEF がある環境の実機でしか確かめられないので、screens レーンのバグ報告画面の IME 確認に含めてもらう（完了報告に書く）
- D-11 §3.11 の「最後の WS 受信から閾値を超えたら Web 側の値を無視」は CEF 停止の記録と一体で screens レーンの持ち物。このレーンでは `UiInputExclusivity` の Web 側の値を 1 か所にまとめておき、閾値の判定を足す口をコメントで示す
- D-12 DSL の `GameUiElementLocator` は Editor 限定の `Client.Playtest` に置く（既存の Web 版と同じ置き場）。照会の本体は純粋な静的関数にしてパネルを引数で受け、L1 で `UiTestHost` のパネルに対して試す
- D-13 `GameTextField` の外側の箱は `focusable=false`・`pickingMode=Ignore`、testid（`name`）は内側の `TextField` に付ける（DSL の `SetText` が TextField を `name` で引くため。§3.12「testid は name に同じ綴りで」）
- D-14 録画プレイテストは行わない（UITK の層は空で見た目は不変。入力の結線は L3 で確かめる）
- D-15 S4 の差し替えは配布画面の挙動を変えうる唯一の変更（uGUI の Selectable のキーボード移動が止まる）。Task 3 Step 5 で依存の有無を列挙し、依存があれば要裁定へ
