# Player View Mode UI Decoupling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** FPS/TPS切替をUIステートから完全に分離し、どのUIが重なっていてもVキーで視点を切り替えられ、新しいUIステート追加時に視点コードの変更を不要にする。

**Architecture:** `PlayerViewModeController`をVContainerの`IStartable`兼`ITickable`として登録し、視点モードの保持・V入力・視点副作用の適用だけを担当させる。`UIStateControl`と`UIStateEnum`への依存を削除し、カーソル表示とカメラ回転可否は各操作状態が所有する一方、FPS/TPS切替はカメラ距離・自機表示・クロスヘア・照準方式だけを変更する。

**Tech Stack:** Unity 6 / VContainer `ITickable` / Cinemachine / DOTween / UniRx / NUnit EditMode / uloop CLI

## Global Constraints

- 最初に`pwd`で`/Users/katsumi/moorestech-worktrees/tree3`であることを確認する
- 1ファイル200行以下、partial禁止、try-catch禁止、デフォルト引数の新規追加禁止
- 複雑なメソッドのローカル関数はメソッド内の`#region Internal`へ置き、`#endregion`より下にコードを書かない
- 主要処理には日本語・英語の2行セットコメントを約3〜10行ごとに置く
- イベントを追加する場合はUniRxを使い、`Action`イベントを追加しない
- `.meta`ファイルは手動作成・編集しない。Unity生成物だけをコミットする
- Prefab・Scene・ScriptableObjectをテキスト編集しない。本計画ではVContainerの既存登録を使い、Unity固有YAMLを変更しない
- `.cs`変更後は必ず`uloop compile --project-path ./moorestech_client`を実行する
- テストは`--filter-type regex`で対象を限定する。Domain Reload中なら45秒待って再実行する
- 各タスクを独立してテストし、完了時にコミットする
- Vキーは全UIステート上で有効とし、`UIStateEnum`やUIの表示有無では抑止しない

## File Structure

### 視点モード層

- `Control/ViewMode/PlayerViewModeController.cs`: FPS/TPS選択状態、初期適用、V入力、Applier呼び出しだけを所有する`IStartable`兼`ITickable`
- `Control/ViewMode/IPlayerViewApplier.cs`: 完全な視点モードを適用する単一メソッド契約
- `Control/ViewMode/PlayerViewApplier.cs`: カメラ、自機表示、クロスヘア、照準方式へモードを適用
- `Control/ViewMode/AimPointProvider.cs`: 選択中の視点に対応する照準座標を返す。UI状態は保持しない
- `Control/InGameCameraController.cs`: FPS/TPSのカメラ距離・追従オフセットと三人称ズームを所有

### UI・操作状態層

- `UI/UIState/UIStateControl.cs`: UI遷移だけを所有し、視点Controllerを参照しない
- `UI/UIState/State/GameScreenState.cs`: 通常操作時のカーソルロックとカメラ回転を所有
- `UI/UIState/State/BuildMenuState.cs`: メニュー操作用カーソルと回転停止を所有
- `UI/UIState/State/PlaceBlockState.cs`: 三人称照準操作時の右ドラッグ回転を所有
- `UI/UIState/State/DeleteObjectState.cs`: 三人称照準操作時の右ドラッグ回転を所有

### テスト層

- `Client.Tests/ViewMode/PlayerViewModeControllerTest.cs`: UI非依存の視点保持・トグルを検証
- `Client.Tests/ViewMode/PlayerViewModeInputTest.cs`: `ITickable.Tick()`のV入力を全UI非依存で検証
- `Client.Tests/ViewMode/FakePlayerViewApplier.cs`: 適用された完全なモードを記録
- `Client.Tests/UIState/UIStateControlViewModeTest.cs`: 削除。UIStateControlと視点の結合を仕様として残さない
- `Client.Tests/UIState/UIStateControlTextInputFocusTest.cs`: 削除。UIから視点入力を抑止する旧仕様を残さない
- `Client.Tests/ViewMode/PlayerViewTextInputFocusTest.cs`: 削除。UIフォーカスを視点Controllerが解釈する旧仕様を残さない

## 配置と前例

| 項目 | 配置先・機構 | 根拠 |
|---|---|---|
| `PlayerViewModeController : IStartable, ITickable` | `Client.Game/InGame/Control/ViewMode`、VContainer singleton | `BlockColliderCullingRegisterService`の`IStartable`と`PlayerStateController`の`ITickable`を組み合わせ、Scene上の`Awake`完了後に初期表示を適用する |
| 視点副作用 | `IPlayerViewApplier.SetViewMode(PlayerViewMode)` | 現行PRのFake差し替え可能なApplier境界を維持し、UI語彙を契約から除去する |
| カーソル・回転 | 各具体的な操作状態 | `DebugBlockInfoState`と`TrainHudGameScreenSubState`が自身の操作開始・終了時にカーソルと回転を設定する既存前例に合わせる |
| 通知 | 新規通知なし、ControllerからApplierへ同期適用 | 状態所有者自身が変更操作直後にプッシュするため、毎tickの同値検知やC#イベントは不要 |
| DI登録 | `MainGameStarter`の視点モード登録ブロック | 既存の`PlayerStateController`等と同じ`.AsSelf().As<ITickable>()`形式を使う |

新規パターンは導入しない。すべて`Client.*`内のローカル表示・入力状態であり、サーバー、Master、SaveLoad、Protocolへの依存追加はない。

---

### Task 1: UI非依存な視点モードControllerをテスト駆動で作る

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/ViewMode/IPlayerViewApplier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/ViewMode/PlayerViewModeController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/ViewMode/FakePlayerViewApplier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/ViewMode/PlayerViewModeControllerTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/ViewMode/PlayerViewModeInputTest.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Tests/ViewMode/PlayerViewTextInputFocusTest.cs`

**Interfaces:**
- Consumes: `PlayerViewMode`, `HybridInput.GetKeyDown(KeyCode.V)`, VContainer `ITickable`
- Produces: `IPlayerViewApplier.SetViewMode(PlayerViewMode mode)`, `PlayerViewModeController.Start()`, `PlayerViewModeController.ToggleViewMode()`, `PlayerViewModeController.GetCurrentMode()`, `PlayerViewModeController.Tick()`

- [ ] **Step 1: UI非依存性を規定する失敗テストへ書き換える**

`FakePlayerViewApplier`は個別副作用の記録をやめ、完全なモードだけを記録する。

```csharp
public class FakePlayerViewApplier : IPlayerViewApplier
{
    public readonly List<PlayerViewMode> AppliedModes = new();

    public void SetViewMode(PlayerViewMode mode)
    {
        AppliedModes.Add(mode);
    }
}
```

`PlayerViewModeControllerTest`に以下を残す。

```csharp
[Test]
public void StartAppliesThirdPersonAsInitialMode()
{
    _controller.Start();

    Assert.AreEqual(PlayerViewMode.ThirdPerson, _controller.GetCurrentMode());
    CollectionAssert.AreEqual(
        new[] { PlayerViewMode.ThirdPerson },
        _applier.AppliedModes);
}

[Test]
public void ToggleChangesModeAndAppliesCompleteMode()
{
    _controller.Start();
    _controller.ToggleViewMode();
    _controller.ToggleViewMode();

    Assert.AreEqual(PlayerViewMode.ThirdPerson, _controller.GetCurrentMode());
    CollectionAssert.AreEqual(
        new[]
        {
            PlayerViewMode.ThirdPerson,
            PlayerViewMode.FirstPerson,
            PlayerViewMode.ThirdPerson,
        },
        _applier.AppliedModes);
}
```

`PlayerViewModeInputTest`ではVキー以外を押した場合とVキーを押した場合を検証する。ControllerへUI状態を渡す処理は書かない。

```csharp
[Test]
public void TickTogglesViewModeWhenVIsPressed()
{
    Press(_keyboard.vKey);
    _controller.Tick();

    Assert.AreEqual(PlayerViewMode.FirstPerson, _controller.GetCurrentMode());
}

[Test]
public void TickDoesNotToggleForAnotherKey()
{
    Press(_keyboard.bKey);
    _controller.Tick();

    Assert.AreEqual(PlayerViewMode.ThirdPerson, _controller.GetCurrentMode());
}
```

- [ ] **Step 2: テストを実行して旧インターフェースにより失敗することを確認する**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerViewModeControllerTest|PlayerViewModeInputTest"
```

Expected: `SetViewMode`、`GetCurrentMode`または`Tick`が未定義でコンパイル失敗する。

- [ ] **Step 3: Applier契約を完全な視点モード1つへ縮小する**

`IPlayerViewApplier.cs`:

```csharp
namespace Client.Game.InGame.Control.ViewMode
{
    public interface IPlayerViewApplier
    {
        void SetViewMode(PlayerViewMode mode);
    }
}
```

- [ ] **Step 4: ControllerをUI非依存なITickableとして実装する**

`PlayerViewModeController.cs`は`ThirdPersonCameraDistance`を含めず、以下の責務だけにする。

```csharp
using Client.Input;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Control.ViewMode
{
    public class PlayerViewModeController : IStartable, ITickable
    {
        private readonly IPlayerViewApplier _applier;
        private PlayerViewMode _currentMode = PlayerViewMode.ThirdPerson;

        public PlayerViewModeController(IPlayerViewApplier applier)
        {
            _applier = applier;
        }

        public void Start()
        {
            // Scene上の表示SingletonがAwakeした後に初期視点を同期する
            // Synchronize the initial view after scene presentation singletons have awakened
            _applier.SetViewMode(_currentMode);
        }

        public void Tick()
        {
            // UI表示状態に関係なくV入力を受け付ける
            // Accept the V input regardless of the visible UI state
            if (HybridInput.GetKeyDown(KeyCode.V)) ToggleViewMode();
        }

        public void ToggleViewMode()
        {
            _currentMode = _currentMode == PlayerViewMode.ThirdPerson
                ? PlayerViewMode.FirstPerson
                : PlayerViewMode.ThirdPerson;
            _applier.SetViewMode(_currentMode);
        }

        public PlayerViewMode GetCurrentMode()
        {
            return _currentMode;
        }
    }
}
```

削除対象:

- `UIStateEnum`のusing、フィールド、`SetUIState`
- `ManualUpdate(bool)`、`RestoreAfterApplicationFocus`
- `SetTextInputFocused`、`ApplyCurrentState`
- `IsMouseAimState`、`IsViewState`
- 同ファイルにある`ThirdPersonCameraDistance`

- [ ] **Step 5: 旧テキストフォーカス仕様のテストを削除する**

`PlayerViewTextInputFocusTest.cs`を削除する。`.meta`は手で削除せず、Unity起動後にUnityが反映した削除状態を確認する。

- [ ] **Step 6: 対象テストを実行する**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerViewModeControllerTest|PlayerViewModeInputTest"
```

Expected: Controller/Inputテストが全件PASSする。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Control/ViewMode moorestech_client/Assets/Scripts/Client.Tests/ViewMode
git commit -m "refactor: 視点モード制御をUIステートから分離"
```

---

### Task 2: 視点Applierと三人称距離を単一責務へ整理する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/ViewMode/ThirdPersonCameraDistance.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/ViewMode/PlayerViewApplier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/ViewMode/AimPointProvider.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/InGameCameraController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/ViewMode/AimPointProviderTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/ViewMode/ThirdPersonCameraDistanceTest.cs`

**Interfaces:**
- Consumes: Task 1の`IPlayerViewApplier.SetViewMode(PlayerViewMode)`
- Produces: `ThirdPersonCameraDistance`、`AimPointProvider.SetViewMode(PlayerViewMode)`、視点モードに限定された`PlayerViewApplier`

- [ ] **Step 1: 照準方式がUIやカーソル状態ではなく視点モードだけで決まる失敗テストを書く**

```csharp
[Test]
public void ThirdPersonUsesMouseAim()
{
    AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
    Assert.AreEqual(AimPointMode.Mouse, AimPointProvider.GetCurrentMode());
}

[Test]
public void FirstPersonUsesScreenCenterAim()
{
    AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
    Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.GetCurrentMode());
}
```

Test teardownは`SetViewMode(PlayerViewMode.ThirdPerson)`へ変更する。

- [ ] **Step 2: テストを実行して新API未定義で失敗することを確認する**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "AimPointProviderTest|ThirdPersonCameraDistanceTest"
```

Expected: `SetViewMode`または`GetCurrentMode`未定義で失敗する。

- [ ] **Step 3: 三人称距離クラスを専用ファイルへ移動する**

Task 1でControllerから除去した`ThirdPersonCameraDistance`を`ThirdPersonCameraDistance.cs`へ移す。public APIは以下を維持する。

```csharp
public ThirdPersonCameraDistance(float initialDistance);
public void SetTransitioning(bool transitioning);
public bool TryAddZoom(float delta);
public float GetDistance();
```

距離範囲`0.6f`〜`10f`、Tween中のズーム拒否、クランプ動作は変更しない。

- [ ] **Step 4: AimPointProviderを視点モード入力へ変更する**

`SetMode(AimPointMode)`を外部公開せず、視点Applierだけが呼ぶAPIへ変更する。

```csharp
private static AimPointMode _currentMode = AimPointMode.Mouse;

public static void SetViewMode(PlayerViewMode viewMode)
{
    _currentMode = viewMode == PlayerViewMode.FirstPerson
        ? AimPointMode.ScreenCenter
        : AimPointMode.Mouse;
}

public static AimPointMode GetCurrentMode()
{
    return _currentMode;
}
```

`GetAimScreenPoint()`は`_currentMode`に従って画面中央または`HybridInput.GetMousePosition()`を返す。

- [ ] **Step 5: PlayerViewApplierからカーソルと回転制御を削除する**

```csharp
public void SetViewMode(PlayerViewMode mode)
{
    var isFirstPerson = mode == PlayerViewMode.FirstPerson;

    // 視点そのものに属する表示だけを同期する
    // Synchronize only presentation that belongs to the selected view mode
    _inGameCameraController.SetFirstPersonMode(isFirstPerson);
    PlayerSystemContainer.Instance.PlayerObjectController.SetModelVisible(!isFirstPerson);
    CrosshairView.Instance.SetVisible(isFirstPerson);
    AimPointProvider.SetViewMode(mode);
}
```

削除対象:

- `SetCursorVisible`
- `SetCameraRotatable`
- `InputManager`のusing

- [ ] **Step 6: 対象テストを実行する**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "AimPointProviderTest|ThirdPersonCameraDistanceTest|PlayerObjectModelVisibilityTest"
```

Expected: 全件PASSする。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Control moorestech_client/Assets/Scripts/Client.Game/InGame/Player moorestech_client/Assets/Scripts/Client.Tests/ViewMode moorestech_client/Assets/Scripts/Client.Tests/Player
git commit -m "refactor: 視点副作用をカメラと表示に限定"
```

---

### Task 3: UIStateControlから視点依存を除去し、操作状態へカーソル・回転を戻す

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/UIStateControl.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/TextInputFocusProvider.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateControlViewModeTest.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateControlTextInputFocusTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateControlTest.cs`（既存ファイルがなければ新規作成し、`.meta`はUnityに生成させる）

**Interfaces:**
- Consumes: `InGameCameraController.SetControllable(bool)`、`InputManager.MouseCursorVisible(bool)`
- Produces: 視点Controllerを知らない`UIStateControl`、操作状態ごとのカーソル・回転ライフサイクル

- [ ] **Step 1: UIStateControlが視点Controllerなしで遷移するテストを書く**

テスト用の2ステートを`UIStateDictionary`へ入れ、`Construct`へ渡す引数がDictionaryだけであることを規定する。

```csharp
var control = controlObject.AddComponent<UIStateControl>();
control.Construct(dictionary);
control.Initialize(UIStateEnum.GameScreen, initialContext);

InvokeUpdate(control);

Assert.AreEqual(UIStateEnum.BuildMenu, control.CurrentState);
Assert.AreEqual(1, firstState.ExitCount);
Assert.AreEqual(1, secondState.EnterCount);
```

- [ ] **Step 2: テストを実行してConstructの旧シグネチャにより失敗することを確認する**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UIStateControlTest"
```

Expected: `Construct(UIStateDictionary)`が未定義で失敗する。

- [ ] **Step 3: UIStateControlから視点依存をすべて削除する**

`UIStateControl`のDIはDictionaryだけに戻す。

```csharp
[Inject]
public void Construct(UIStateDictionary uiStateDictionary)
{
    _uiStateDictionary = uiStateDictionary;
}
```

削除対象:

- `PlayerViewModeController`フィールドとusing
- `Initialize`内の`SetUIState`
- `Update`内の`ManualUpdate(TextInputFocusProvider.IsFocused())`
- 遷移時の`SetUIState`
- `_isInitialized`
- 視点Controllerに対する`OnApplicationFocus`復元

`IApplicationFocusRestorer`による現在UIステート固有の復元は残す。

```csharp
private void OnApplicationFocus(bool hasFocus)
{
    if (!hasFocus) return;
    if (_uiStateDictionary.GetState(CurrentState) is IApplicationFocusRestorer focusRestorer)
        focusRestorer.RestoreAfterApplicationFocus();
}
```

- [ ] **Step 4: 旧結合テストとTextInputFocusProviderを削除する**

以下を削除する。

- `UIStateControlViewModeTest.cs`
- `UIStateControlTextInputFocusTest.cs`
- `TextInputFocusProvider.cs`

`PlaceBlockState`に残る`TextInputFocusProvider.IsFocused()`も削除し、UI表示中でも視点トグルを含むゲーム入力をUIStateControl側で一律抑止しない。

- [ ] **Step 5: GameScreenとBuildMenuが自身の操作状態を適用する**

`GameScreenState`へ`InGameCameraController`依存を戻し、`OnEnter`で以下を実行する。

```csharp
InputManager.MouseCursorVisible(false);
_inGameCameraController.SetControllable(true);
```

`BuildMenuState`へ`InGameCameraController`依存を追加し、`OnEnter`で以下を実行する。

```csharp
InputManager.MouseCursorVisible(true);
_inGameCameraController.SetControllable(false);
```

これらはFPS/TPSを判定しない。現在の視点を維持したまま操作方法だけを変更する。

- [ ] **Step 6: PlaceBlockとDeleteObjectへ右ドラッグ回転を戻す**

両ステートへ`InGameCameraController`を注入し、`OnEnter`でカーソル表示・回転停止を適用する。

```csharp
InputManager.MouseCursorVisible(true);
_inGameCameraController.SetControllable(false);
```

`GetNextUpdate()`で右ボタンDown/Upを処理する。

```csharp
if (HybridInput.GetMouseButtonDown(1))
{
    InputManager.MouseCursorVisible(false);
    _inGameCameraController.SetControllable(true);
}

if (HybridInput.GetMouseButtonUp(1))
{
    InputManager.MouseCursorVisible(true);
    _inGameCameraController.SetControllable(false);
}
```

`OnExit`では右ボタンUpを取り逃しても残らないよう必ず回転を停止する。

```csharp
_inGameCameraController.SetControllable(false);
```

FPS/TPSの分岐は追加しない。FPSでもUI操作状態に応じて右ドラッグ方式となるが、V切替自体は常に有効とする。

- [ ] **Step 7: UIStateControlテストを実行する**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UIStateControlTest|PlayerViewModeControllerTest|PlayerViewModeInputTest"
```

Expected: UI遷移テストと視点テストが独立して全件PASSする。CIで失敗していた`UIStateControlTextInputFocusTest`はテスト一覧に存在しない。

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState moorestech_client/Assets/Scripts/Client.Tests/UIState
git commit -m "refactor: UI遷移とプレイヤー視点を分離"
```

---

### Task 4: DI登録と全体回帰を完成させる

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`
- Modify: `.claude/skills/unity-playmode-recorded-playtest/scenarios/fps-tps-view-toggle-via-ui.cs`

**Interfaces:**
- Consumes: Task 1の`PlayerViewModeController : IStartable, ITickable`
- Produces: UIStateControlに依存せず毎フレームV入力を処理するDI構成、UI重畳中の視点切替を検証するE2E

- [ ] **Step 1: ControllerをITickableとして登録する**

`MainGameStarter.cs`の視点登録を以下へ変更する。

```csharp
// 視点モード（UIステートと独立して毎フレーム入力を処理）
// Player view mode (ticks independently from UI states)
builder.Register<IPlayerViewApplier, PlayerViewApplier>(Lifetime.Singleton);
builder.Register<PlayerViewModeController>(Lifetime.Singleton).AsSelf().As<IStartable>().As<ITickable>();
```

新しいMonoBehaviourを作らないため、Prefab・Scene変更は不要。

- [ ] **Step 2: コンパイルする**

Run:

```bash
uloop compile --project-path ./moorestech_client
```

Expected: コンパイルエラー0件。

- [ ] **Step 3: 関連EditModeテストを実行する**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerViewMode|AimPointProvider|ThirdPersonCameraDistance|PlayerObjectModelVisibility|MapObjectMiningAim|UIStateControl"
```

Expected: 関連テストが全件PASSする。

- [ ] **Step 4: E2EシナリオへUI重畳中の切替を追加する**

既存のGameScreen、PlaceBlock、DeleteBarでの切替検証に加え、少なくともBuildMenuとPlayerInventoryで以下を検証する。

```text
1. UIを開く
2. UIが表示中であることをassert
3. Vキーを送る
4. カメラ距離がFirstPersonCameraDistanceへ近づくことをassert
5. UIが閉じていないことをassert
6. もう一度Vキーを送る
7. 三人称距離へ戻ることをassert
```

UIが開いたまま切り替わることを、単なるController単体テストではなくPlayMode上で確認する。

- [ ] **Step 5: PlayMode録画テストを実行する**

Runは`unity-playmode-recorded-playtest`スキルに従い、シナリオ`fps-tps-view-toggle-via-ui`を指定する。

Expected:

- GameScreen、BuildMenu、PlaceBlock、DeleteBar、PlayerInventoryの全箇所でV切替成功
- UI表示中のV切替でUIが閉じない
- TPS距離がV連打後も初期値へ戻る
- FPS中の持ち替え後も自機Rendererが表示されない

- [ ] **Step 6: Unityエラーログを確認する**

Run:

```bash
uloop get-logs --project-path ./moorestech_client --log-type Error
```

Expected: 今回の変更に起因するErrorログ0件。

- [ ] **Step 7: 静的な分離条件を確認する**

Run:

```bash
rg -n "UIStateEnum|TextInputFocusProvider|SetUIState|IsViewState|IsMouseAimState" moorestech_client/Assets/Scripts/Client.Game/InGame/Control/ViewMode
rg -n "PlayerViewModeController" moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState
git diff --check origin/master-fable-tmp...HEAD
```

Expected:

- 最初の2コマンドは一致0件
- `git diff --check`は手書きコードの空白エラー0件。Unity生成`.meta`の既存末尾空白だけなら備考として記録する

- [ ] **Step 8: 最終コミットを作成する**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs .claude/skills/unity-playmode-recorded-playtest/scenarios/fps-tps-view-toggle-via-ui.cs
git commit -m "test: UI表示中のFPS TPS切替を検証"
```

---

## QA Bug-Hunt Checklist

最初から問題がある前提で、成功経路だけでなく以下を意図的に壊しに行く。

- VをTween完了前に10回以上連打し、TPS距離と追従オフセットが縮退しない
- BuildMenu、Inventory、ResearchTreeなどカーソル表示UIを開いたままV切替でき、UIが閉じない
- UI表示中にFPSへ切り替えてもカーソル表示状態がUI側の要求から変わらない
- PlaceBlock/DeleteBarの右ドラッグ中にV切替し、MouseUp後に回転・カーソル状態が復元する
- 右ドラッグ中にPlaceBlock/DeleteBarを退出し、MouseUpを取り逃しても回転が残らない
- FPSのままGameScreen→Inventory→GameScreenと往復し、FPS選択と三人称保存距離が維持される
- FPSのままSkit/TrainHUD/Debugへ遷移し、各カメラ・カーソル所有者との競合がない
- アプリ非アクティブ化を右ドラッグ中に行い、復帰後にカーソル・回転が固定されない
- FPS中のホットバー持ち替えで手持ちRendererだけが浮かない
- TPSで採掘・設置・削除はマウス位置、FPSでは画面中央からRayが出る

## Plan Self-Review

- 要件網羅: UIState列挙を視点層から削除し、全UI表示中のV切替をTask 1・4で保証した
- 依存方向: `Control/ViewMode`から`UI/UIState`への依存は0件になり、UIStateControlも視点Controllerを参照しない
- ポーリング: `Tick()`はVキーのフレーム入力だけを扱い、UI状態やフォーカスの毎tick同値検知をしない
- 通知規約: 新規`Action`イベントなし。変更操作直後にControllerがApplierへ同期プッシュする
- ファイル制限: 新規コードファイルは`ThirdPersonCameraDistance.cs`と必要な`UIStateControlTest.cs`だけで、各200行未満・既存ディレクトリ10コードファイル以内
- プレースホルダや内容未指定のテスト手順なし
- 型整合: `SetViewMode(PlayerViewMode)`、`GetCurrentMode()`、`Tick()`を全タスクで統一した
