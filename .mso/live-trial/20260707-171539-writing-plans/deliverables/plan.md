# FPS建設モード Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建設系モード（設置・削除・ビルドメニュー）に一人称視点（FPS）を追加し、Vキーで俯瞰⇔FPSを即時切替できるようにする。

**Architecture:** DIシングルトン `BuildViewModeController` が建設系視点モードの唯一の所有者となり、各建設系ステート（PlaceBlock / DeleteBar / BuildMenu）から**明示駆動**される（OnEnter/GetNextUpdate/遷移直前の3フック）。レイキャストの照準点は静的クラス `AimPointProvider` に一元化し、FPS中のみ画面中央を返す。純粋なセッション判定ロジックは `BuildViewSession` に分離してユニットテスト可能にする。

**Tech Stack:** Unity 6 / C# / VContainer（DI）/ Cinemachine（FramingTransposer）/ DOTween（カメラTween）/ NUnit（EditModeテスト）/ uloop CLI（コンパイル・テスト・シーン編集）

## Global Constraints

- 作業ディレクトリ: `/Users/katsumi/moorestech`（git worktree 頻用のため各タスク開始時に `pwd` で確認）
- コンパイル: `uloop compile --project-path ./moorestech_client`（.cs変更後は必ず実行）
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`
- 「Unity is reloading (Domain Reload in progress)」エラーが出たら45秒待機してリトライ
- 1ファイル200行以下。`partial` は如何なる条件でも絶対禁止
- try-catch 基本禁止。デフォルト引数禁止（呼び出し側を変更する）
- 単純な setter プロパティ禁止（Set は `public void SetHoge` メソッド。get-only プロパティは既存イディオムどおり可）
- コメントは日本語・英語の2行セット（各1行、約3〜10行ごと）。自明なコメントは書かない
- `.meta` ファイルは手動作成禁止（Unityが生成した .meta のコミットは可。各コミット前に `git status` で新規 .meta を確認して含める）
- シーン（MainGame.unity）の編集は `uloop execute-dynamic-code` 経由のみ（Write/Editツールでの直接編集禁止）
- 新キー入力（V）は既存前例準拠: `UnityEngine.Input.GetKeyDown(KeyCode.V)` 直接読み＋`//TODO InputSystemのリファクタ対象` コメント。`.inputactions` は変更しない
- 設置リーチ・レイヤーマスク（`Without_Player_MapObject_Block_LayerMask`）は現行のまま変更しない

## 配置と前例（spec-architecture-review 済み）

| 配置決定 | 前例（ファイルパス） |
|---|---|
| `BuildViewModeController` はステートから明示駆動（OnEnter/ManualUpdate/遷移直前フック）。`UIStateControl.OnStateChanged` 購読は使わない | 駆動前例: `PlaceBlockState` → `PlaceSystemStateController.ManualUpdate()/Disable()`（`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs`）。置換対象 `ScreenClickableCameraController` 自身もステート駆動 |
| `BuildViewModeController` の DI 登録は `builder.Register<T>(Lifetime.Singleton)` | `MainGameStarter.cs:190` の `PlaceSystemStateController` 登録 |
| `CrosshairView` は Object シングルトン（Awake で Instance 設定、シーン事前配置） | `Client.Game/InGame/UI/KeyControl/KeyControlDescription.cs` |
| 自機モデル操作は `IPlayerObjectController` へのメソッド追加＋`PlayerSystemContainer.Instance` 経由アクセス | `PlayerSystemContainer.cs`（`Instance.PlayerObjectController`）、既存 `SetActive`/`SetControllable` |
| 新規イベント/通知は作らない（全て明示呼び出しで完結、UniRx 追加不要） | — |

**ユーザーレビュー注目点（specからの意図的変更・新規パターン）:**

1. **セッション判定を購読→ステート駆動に反転**: 旧spec記載の `UIStateControl.OnStateChanged` 購読は「制御参加コンポーネントはステート駆動」の規約（`.claude/skills/writing-plans/references/moorestech-layer-map.md` 機構規約表）に違反するため、各ステートからの明示呼び出しに変更。遷移先を知る必要がある「セッション終了判定」は、遷移を返す唯一の場所である `GetNextUpdate` の return 直前フック `OnLeaveBuildState(next)` で行う。
2. **`ScreenClickableCameraController` は削除せず存続**: specは「廃止」としていたが、建設系でない `DebugBlockInfoState`（F3デバッグ）も同クラスを使用しており、`TweenCameraInfo` 型も同ファイルに定義されている。建設系3ステートからの利用のみ剥がし、クラス自体は Debug 用に残す。
3. **`AimPointProvider` の API は `SetMode(BuildViewMode)` ではなく `SetScreenCenterAim(bool)`**: enum を渡すと PlaceSystem 層から Control 層の型への依存が生まれる。また「FPSをモード記憶したまま建設セッション外（GameScreen）に居る」間は中央照準であってはならない（`BlockClickDetectUtil` は GameScreen のブロッククリックにも使われる）ため、意味論を「モード」ではなく「セッション中のFPS照準が有効か」の bool にする。セッション終了時に必ず false へ戻す。
4. **`BlockClickDetectUtil` も照準一元化の対象**: 削除モードのホバー判定（`DeleteObjectService`）と電線モードのブロック選択は `BlockClickDetectUtil.TryGetCursorOnComponent` 経由のため、ここも `AimPointProvider` を通す。副次効果として `PlaceSystemUtil` のレイ取得が legacy `Input.mousePosition` から InputSystem 座標（`Mouse.current`）優先に統一され、プレイテストDSLの入力注入（QueueStateEvent）と同一経路になる。

## File Structure

**新規作成:**

| ファイル | 責務 |
|---|---|
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/BuildViewMode.cs` | TopDown / FirstPerson の2値 enum |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/BuildViewSession.cs` | セッション開始/継続/終了判定・モード記憶・カメラdirtyフラグ（純粋ロジック、Unity API非依存） |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/BuildViewModeController.cs` | 効果適用のオーケストレーション（カメラTween・カーソル・クロスヘア・自機表示・Vトグル） |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/AimPointProvider.cs` | 照準スクリーン座標の一元提供（静的クラス） |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Crosshair/CrosshairView.cs` | 中央ドットクロスヘアの表示切替（Objectシングルトン） |
| `moorestech_client/Assets/Scripts/Client.Tests/UIState/BuildViewSessionTest.cs` | セッション判定・モード記憶のユニットテスト |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/AimPointProviderTest.cs` | 照準座標のユニットテスト |

**変更:**

| ファイル | 変更内容 |
|---|---|
| `Client.Game/InGame/Control/InGameCameraController.cs` | FPSビューフラグ（F1/F2ズーム・距離クランプ無効化）、追従オフセットのTween対応、デフォルトオフセット保持 |
| `Client.Game/InGame/Player/PlayerObjectController.cs` | `IPlayerObjectController` と実装に `SetModelVisible(bool)` 追加 |
| `Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs` | Shift+B分岐削除、`ScreenClickableCameraController` → `BuildViewModeController` 駆動、V説明追記 |
| `Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs` | 同上 |
| `Client.Game/InGame/UI/UIState/State/BuildMenuState.cs` | `BuildViewModeController` 駆動追加（カーソル制御をコントローラーへ移譲）、V説明追記 |
| `Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs` | `Input.mousePosition` 3箇所 → `AimPointProvider.GetAimScreenPoint()` |
| `Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/ElectricWireEditMode.cs` | 同上 1箇所 |
| `Client.Game/InGame/Control/BlockClickDetectUtil.cs` | マウス座標取得 → `AimPointProvider.GetAimScreenPoint()` |
| `Client.Starter/MainGameStarter.cs` | `BuildViewModeController` の DI 登録 |
| シーン `moorestech_client/Assets/Scenes/Game/MainGame.unity` | CrosshairView オブジェクト配置（uloop execute-dynamic-code 経由） |

**変更しないもの（明示）:** `UICursorFollowControl.cs`・`GameObjectToolTipTargetController.cs`（UIカーソル追従でありレイ照準ではない）、`MapObjectMiningController.cs`（既に画面中央固定）、`DebugBlockInfoState.cs`（建設系外、`ScreenClickableCameraController` 継続使用）、`.inputactions`、`PlaceableMaxDistance`。

---

### Task 1: BuildViewMode enum と BuildViewSession（純粋ロジック）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/BuildViewMode.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/BuildViewSession.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/BuildViewSessionTest.cs`

**Interfaces:**
- Consumes: `UIStateEnum`（`Client.Game.InGame.UI.UIState`、値: `BuildMenu` / `PlaceBlock` / `DeleteBar` / `GameScreen` 等）
- Produces:
  - `enum BuildViewMode { TopDown, FirstPerson }`
  - `class BuildViewSession` — `BuildViewMode CurrentMode`（初期値 TopDown）/ `bool IsSessionActive` / `UIStateEnum CurrentBuildState` / `bool IsCameraDirty` / `static bool IsBuildState(UIStateEnum)` / `bool EnterBuildState(UIStateEnum)`（セッション新規開始なら true）/ `bool LeaveToState(UIStateEnum)`（セッション終了なら true）/ `void ToggleMode()` / `void MarkCameraDirty()` / `void ClearCameraDirty()`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/UIState/BuildViewSessionTest.cs` を作成:

```csharp
using Client.Game.InGame.Control.BuildView;
using Client.Game.InGame.UI.UIState;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     建設系視点モードのセッション判定・モード記憶を検証するテスト
    ///     Tests verifying build view session transitions and mode memory
    /// </summary>
    public class BuildViewSessionTest
    {
        [Test]
        public void InitialModeIsTopDownAndSessionInactive()
        {
            var session = new BuildViewSession();

            Assert.AreEqual(BuildViewMode.TopDown, session.CurrentMode);
            Assert.IsFalse(session.IsSessionActive);
        }

        [Test]
        public void EnterFromNonBuildStateStartsSession()
        {
            var session = new BuildViewSession();

            Assert.IsTrue(session.EnterBuildState(UIStateEnum.PlaceBlock));
            Assert.IsTrue(session.IsSessionActive);
            Assert.AreEqual(UIStateEnum.PlaceBlock, session.CurrentBuildState);
        }

        [Test]
        public void TransitionBetweenBuildStatesKeepsSession()
        {
            // G↔B往復（DeleteBar→BuildMenu→PlaceBlock）でセッションが継続する
            // The session survives DeleteBar→BuildMenu→PlaceBlock transitions
            var session = new BuildViewSession();

            Assert.IsTrue(session.EnterBuildState(UIStateEnum.DeleteBar));
            Assert.IsFalse(session.LeaveToState(UIStateEnum.BuildMenu));
            Assert.IsFalse(session.EnterBuildState(UIStateEnum.BuildMenu));
            Assert.IsFalse(session.LeaveToState(UIStateEnum.PlaceBlock));
            Assert.IsFalse(session.EnterBuildState(UIStateEnum.PlaceBlock));
            Assert.IsTrue(session.IsSessionActive);
        }

        [Test]
        public void LeaveToNonBuildStateEndsSession()
        {
            var session = new BuildViewSession();
            session.EnterBuildState(UIStateEnum.PlaceBlock);

            Assert.IsTrue(session.LeaveToState(UIStateEnum.GameScreen));
            Assert.IsFalse(session.IsSessionActive);
        }

        [Test]
        public void ModeMemorySurvivesSessionEnd()
        {
            // V切替の記憶がセッションをまたいで保持される
            // The toggled mode is remembered across sessions
            var session = new BuildViewSession();
            session.EnterBuildState(UIStateEnum.PlaceBlock);
            session.ToggleMode();
            session.LeaveToState(UIStateEnum.GameScreen);

            Assert.AreEqual(BuildViewMode.FirstPerson, session.CurrentMode);

            session.EnterBuildState(UIStateEnum.DeleteBar);
            Assert.AreEqual(BuildViewMode.FirstPerson, session.CurrentMode);
        }

        [Test]
        public void ToggleModeFlipsBothWays()
        {
            var session = new BuildViewSession();

            session.ToggleMode();
            Assert.AreEqual(BuildViewMode.FirstPerson, session.CurrentMode);
            session.ToggleMode();
            Assert.AreEqual(BuildViewMode.TopDown, session.CurrentMode);
        }

        [Test]
        public void CameraDirtyResetsOnNewSession()
        {
            var session = new BuildViewSession();
            session.EnterBuildState(UIStateEnum.PlaceBlock);
            session.MarkCameraDirty();
            session.LeaveToState(UIStateEnum.GameScreen);

            session.EnterBuildState(UIStateEnum.DeleteBar);
            Assert.IsFalse(session.IsCameraDirty);
        }

        [Test]
        public void IsBuildStateCoversExactlyThreeStates()
        {
            Assert.IsTrue(BuildViewSession.IsBuildState(UIStateEnum.BuildMenu));
            Assert.IsTrue(BuildViewSession.IsBuildState(UIStateEnum.PlaceBlock));
            Assert.IsTrue(BuildViewSession.IsBuildState(UIStateEnum.DeleteBar));
            Assert.IsFalse(BuildViewSession.IsBuildState(UIStateEnum.GameScreen));
            Assert.IsFalse(BuildViewSession.IsBuildState(UIStateEnum.PlayerInventory));
            Assert.IsFalse(BuildViewSession.IsBuildState(UIStateEnum.Debug));
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: FAIL — `CS0246: The type or namespace name 'BuildViewSession' could not be found`（`BuildViewMode` も同様）

- [ ] **Step 3: 最小実装を書く**

`moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/BuildViewMode.cs` を作成:

```csharp
namespace Client.Game.InGame.Control.BuildView
{
    /// <summary>
    ///     建設系モードの視点種別（俯瞰 / 一人称）
    ///     View mode used by the build states (top-down / first person)
    /// </summary>
    public enum BuildViewMode
    {
        TopDown,
        FirstPerson,
    }
}
```

`moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/BuildViewSession.cs` を作成:

```csharp
using Client.Game.InGame.UI.UIState;

namespace Client.Game.InGame.Control.BuildView
{
    /// <summary>
    ///     建設系視点モードのセッション状態とモード記憶を持つ純粋ロジック
    ///     Pure logic holding the build view session state and the remembered mode
    /// </summary>
    public class BuildViewSession
    {
        public BuildViewMode CurrentMode { get; private set; } = BuildViewMode.TopDown;
        public bool IsSessionActive { get; private set; }
        public UIStateEnum CurrentBuildState { get; private set; }
        public bool IsCameraDirty { get; private set; }

        public static bool IsBuildState(UIStateEnum state)
        {
            return state is UIStateEnum.BuildMenu or UIStateEnum.PlaceBlock or UIStateEnum.DeleteBar;
        }

        // 建設系外→建設系の進入のみセッションを開始しtrueを返す
        // Starts the session (returns true) only when entering from a non-build state
        public bool EnterBuildState(UIStateEnum state)
        {
            CurrentBuildState = state;
            if (IsSessionActive) return false;

            IsSessionActive = true;
            IsCameraDirty = false;
            return true;
        }

        // 遷移先が建設系外の時のみセッションを終了しtrueを返す
        // Ends the session (returns true) only when the destination is outside the build set
        public bool LeaveToState(UIStateEnum nextState)
        {
            if (!IsSessionActive || IsBuildState(nextState)) return false;

            IsSessionActive = false;
            return true;
        }

        public void ToggleMode()
        {
            CurrentMode = CurrentMode == BuildViewMode.TopDown ? BuildViewMode.FirstPerson : BuildViewMode.TopDown;
        }

        // セッション中にカメラを動かした事実を記録する（終了時の復帰Tween要否の判定に使う）
        // Records that this session moved the camera (decides whether to restore on exit)
        public void MarkCameraDirty()
        {
            IsCameraDirty = true;
        }

        public void ClearCameraDirty()
        {
            IsCameraDirty = false;
        }
    }
}
```

- [ ] **Step 4: コンパイルとテストがパスすることを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: PASS（エラー0件）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BuildViewSession"`
Expected: 8 tests PASS

- [ ] **Step 5: コミット**

```bash
git status   # Unity生成の.metaを確認して含める
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView moorestech_client/Assets/Scripts/Client.Tests/UIState/BuildViewSessionTest.cs*
git commit -m "feat: 建設視点モードのセッション純ロジックを追加"
```

---

### Task 2: AimPointProvider と全レイ取得箇所の置換

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/AimPointProvider.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs:35,56,73`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/ElectricWireEditMode.cs:65`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BlockClickDetectUtil.cs:48-49`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/AimPointProviderTest.cs`

**Interfaces:**
- Consumes: なし（Unity API のみ）
- Produces: `static class AimPointProvider` — `static void SetScreenCenterAim(bool)` / `static Vector3 GetAimScreenPoint()`（中央照準OFF時は `Mouse.current` 優先・`Input.mousePosition` フォールバック）

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/AimPointProviderTest.cs` を作成:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.PlaceSystem
{
    /// <summary>
    ///     照準点プロバイダのモード別返却座標を検証するテスト
    ///     Tests verifying the aim point per mode
    /// </summary>
    public class AimPointProviderTest
    {
        [SetUp]
        [TearDown]
        public void ResetAimMode()
        {
            // 静的状態を他テストへ漏らさない
            // Keep the static state from leaking into other tests
            AimPointProvider.SetScreenCenterAim(false);
        }

        [Test]
        public void ScreenCenterAimReturnsScreenCenter()
        {
            AimPointProvider.SetScreenCenterAim(true);

            var expected = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
            Assert.AreEqual(expected, AimPointProvider.GetAimScreenPoint());
        }

        [Test]
        public void DefaultAimFollowsMousePosition()
        {
            var expected = Mouse.current != null
                ? (Vector3)Mouse.current.position.ReadValue()
                : UnityEngine.Input.mousePosition;

            Assert.AreEqual(expected, AimPointProvider.GetAimScreenPoint());
        }

        [Test]
        public void DisablingScreenCenterAimRestoresMousePosition()
        {
            AimPointProvider.SetScreenCenterAim(true);
            AimPointProvider.SetScreenCenterAim(false);

            var expected = Mouse.current != null
                ? (Vector3)Mouse.current.position.ReadValue()
                : UnityEngine.Input.mousePosition;

            Assert.AreEqual(expected, AimPointProvider.GetAimScreenPoint());
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: FAIL — `CS0246: The type or namespace name 'AimPointProvider' could not be found`

- [ ] **Step 3: AimPointProvider を実装**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/AimPointProvider.cs` を作成:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    ///     設置・削除系レイキャストの照準スクリーン座標を一元提供する
    ///     Single source of the aim screen point used by placement and deletion raycasts
    /// </summary>
    public static class AimPointProvider
    {
        private static bool _isScreenCenterAim;

        // FPS建設セッション中のみtrueにする。Control層の型へ依存しないためbool set方式
        // Set true only during an FPS build session; a bool setter avoids depending on Control-layer types
        public static void SetScreenCenterAim(bool isScreenCenterAim)
        {
            _isScreenCenterAim = isScreenCenterAim;
        }

        public static Vector3 GetAimScreenPoint()
        {
            if (_isScreenCenterAim) return new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);

            // 実マウスと入力注入を同一経路で扱うためInputSystem座標を優先する
            // Prefer the Input System position so real and injected mouse input share one path
            return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : UnityEngine.Input.mousePosition;
        }
    }
}
```

- [ ] **Step 4: 呼び出し箇所を置換**

`PlaceSystemUtil.cs` の3箇所（35行・56行・73行）:

```csharp
// 変更前（3箇所とも同一）
var ray = mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
// 変更後
var ray = mainCamera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
```

`ElectricWireEditMode.cs` 65行:

```csharp
// 変更前
var ray = _context.MainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
// 変更後
var ray = _context.MainCamera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
```

（ファイル先頭に `using Client.Game.InGame.BlockSystem.PlaceSystem.Util;` を追加）

`BlockClickDetectUtil.cs` 45〜49行:

```csharp
// 変更前
// TODO InputSystemのリファクタ対象
// InputSystemのマウス座標を使う（実機と入力注入の双方を同一経路で扱う）
// Use the Input System mouse position so real and injected input share one path
var mousePosition = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : UnityEngine.Input.mousePosition;
var ray = camera.ScreenPointToRay(mousePosition);
// 変更後
// 照準座標はAimPointProvider一元管理（FPS建設モード中は画面中央になる）
// The aim point is centralized in AimPointProvider (screen center during FPS build mode)
var ray = camera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
```

（`using Client.Game.InGame.BlockSystem.PlaceSystem.Util;` を追加。`using UnityEngine.InputSystem;` は不要になれば削除）

- [ ] **Step 5: 置換漏れを grep で検出**

Run: `grep -rn "ScreenPointToRay" moorestech_client/Assets/Scripts --include="*.cs" | grep -v "AimPointProvider.GetAimScreenPoint"`
Expected: 残るのは `UICursorFollowControl.cs`（UIカーソル追従・対象外）、`GameObjectToolTipTargetController.cs`（ツールチップ・対象外）、`MapObjectMiningController.cs`（既に画面中央・対象外）のみ。設置・削除系（PlaceSystem / DragDelete / ElectricWire / BlockClickDetectUtil）に `Input.mousePosition` 直書きが残っていないこと。

- [ ] **Step 6: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: PASS

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "AimPointProvider"`
Expected: 3 tests PASS

- [ ] **Step 7: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/AimPointProvider.cs* moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/ElectricWireEditMode.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BlockClickDetectUtil.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/AimPointProviderTest.cs*
git commit -m "feat: 照準点プロバイダを新設しレイ取得を一元化"
```

---

### Task 3: InGameCameraController の FPS 対応拡張

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/InGameCameraController.cs`

**Interfaces:**
- Consumes: 既存の `CinemachineFramingTransposer _cinemachineFraming` / `DOTween Sequence _currentSequence`
- Produces（Task 6 が利用）:
  - `public const float FirstPersonHeadHeight = 1.6f`
  - `Vector3 DefaultTrackedObjectOffset`（get-onlyプロパティ）
  - `Vector3 FirstPersonTrackedObjectOffset`（get-onlyプロパティ、デフォルトのxzにy=頭部高さ）
  - `void SetFirstPersonView(bool enabled)` — F1/F2ズームと距離クランプ(0.6〜10)を無効化するフラグ
  - `void StartTweenCamera(Vector3 targetRotation, float targetDistance, Vector3 targetTrackedOffset, float duration)` — 追従オフセットも同時にTweenする4引数オーバーロード

- [ ] **Step 1: フィールドと定数を追加**

`InGameCameraController.cs` のフィールド宣言部（29行 `private bool _isControllable;` の後）に追加:

```csharp
        // FPS建設ビュー中はズーム・距離クランプを止めて距離0を維持する
        // While in FPS build view, zoom and distance clamp are suspended to hold distance 0
        private bool _isFirstPersonView;
        private Vector3 _defaultTrackedObjectOffset;

        public const float FirstPersonHeadHeight = 1.6f;

        public Vector3 DefaultTrackedObjectOffset => _defaultTrackedObjectOffset;
        public Vector3 FirstPersonTrackedObjectOffset => new(_defaultTrackedObjectOffset.x, FirstPersonHeadHeight, _defaultTrackedObjectOffset.z);
```

`Awake()`（36〜40行）にオフセット保存を追加:

```csharp
        private void Awake()
        {
            _cinemachineFraming = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
            _targetRotation = transform.rotation; // Initialize target rotation to current rotation
            _defaultTrackedObjectOffset = _cinemachineFraming.m_TrackedObjectOffset;
        }
```

- [ ] **Step 2: Update() の F1/F2 ズームとクランプを FPS 中は無効化**

`Update()` 冒頭（44〜47行）を変更:

```csharp
// 変更前
var distance = _cinemachineFraming.m_CameraDistance;
if (UnityEngine.Input.GetKey(KeyCode.F1)) distance -= Time.deltaTime * 3f; // TODO InputManagerに移動
if (UnityEngine.Input.GetKey(KeyCode.F2)) distance += Time.deltaTime * 3f; // TODO InputManagerに移動
_cinemachineFraming.m_CameraDistance = Mathf.Clamp(distance, 0.6f, 10);
// 変更後
// FPS建設ビュー中は距離0固定のためズーム入力とクランプを適用しない
// Skip zoom input and clamping in FPS build view to keep the camera distance at 0
if (!_isFirstPersonView)
{
    var distance = _cinemachineFraming.m_CameraDistance;
    if (UnityEngine.Input.GetKey(KeyCode.F1)) distance -= Time.deltaTime * 3f; // TODO InputManagerに移動
    if (UnityEngine.Input.GetKey(KeyCode.F2)) distance += Time.deltaTime * 3f; // TODO InputManagerに移動
    _cinemachineFraming.m_CameraDistance = Mathf.Clamp(distance, 0.6f, 10);
}
```

- [ ] **Step 3: セッターと4引数Tweenオーバーロードを追加**

クラス末尾（既存 `StartTweenCamera(TweenCameraInfo target)` の後）に追加:

```csharp
        public void SetFirstPersonView(bool enabled)
        {
            _isFirstPersonView = enabled;
        }

        public void StartTweenCamera(Vector3 targetRotation, float targetDistance, Vector3 targetTrackedOffset, float duration)
        {
            // 回転・距離に加えて追従オフセット（頭部高さ⇔既定）も同時にTweenする
            // Tween the follow offset (head height vs default) together with rotation and distance
            _currentSequence?.Kill();
            _currentSequence = DOTween.Sequence()
                .Append(DOTween.To(() => _targetRotation, x => _targetRotation = x, targetRotation, duration).SetEase(Ease.InOutQuad))
                .Join(DOTween.To(() => _cinemachineFraming.m_CameraDistance, x => _cinemachineFraming.m_CameraDistance = x, targetDistance, duration).SetEase(Ease.InOutQuad))
                .Join(DOTween.To(() => _cinemachineFraming.m_TrackedObjectOffset, x => _cinemachineFraming.m_TrackedObjectOffset = x, targetTrackedOffset, duration).SetEase(Ease.InOutQuad));
        }
```

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: PASS（挙動変更なし: `_isFirstPersonView` は常に false のため既存動作は不変）

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Control/InGameCameraController.cs
git commit -m "feat: カメラにFPSビューフラグと追従オフセットTweenを追加"
```

---

### Task 4: CrosshairView（クラス＋シーン配置）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Crosshair/CrosshairView.cs`
- Modify（uloop経由のみ）: `moorestech_client/Assets/Scenes/Game/MainGame.unity`

**Interfaces:**
- Consumes: なし
- Produces（Task 6 が利用）: `CrosshairView.Instance`（Objectシングルトン）/ `void SetVisible(bool visible)`。初期状態は非表示

- [ ] **Step 1: CrosshairView クラスを作成**

`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Crosshair/CrosshairView.cs` を作成:

```csharp
using UnityEngine;

namespace Client.Game.InGame.UI.Crosshair
{
    /// <summary>
    ///     FPS建設モード用の画面中央クロスヘア（中央ドットのみの最小UI）
    ///     Minimal center-dot crosshair shown during FPS build mode
    /// </summary>
    public class CrosshairView : MonoBehaviour
    {
        public static CrosshairView Instance { get; private set; }

        [SerializeField] private GameObject crosshairRoot;

        private void Awake()
        {
            Instance = this;
            crosshairRoot.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            crosshairRoot.SetActive(visible);
        }
    }
}
```

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: PASS

- [ ] **Step 3: シーンに配置（uloop execute-dynamic-code）**

MainGame.unity を開いて Canvas 配下に CrosshairView を生成・配線・保存する。以下の C# を `uloop execute-dynamic-code --project-path ./moorestech_client` で実行:

```csharp
// MainGameシーンを開く
// Open the MainGame scene
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Game/MainGame.unity");

// KeyControlDescriptionと同じルートCanvasを取得する
// Locate the same root canvas that hosts KeyControlDescription
var keyControl = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.UI.KeyControl.KeyControlDescription>(UnityEngine.FindObjectsInactive.Include);
var canvas = keyControl.GetComponentInParent<UnityEngine.Canvas>(true).rootCanvas;

// 中央アンカーのルートと中央ドットImageを作る
// Build a center-anchored root and the center dot image
var root = new UnityEngine.GameObject("CrosshairView", typeof(UnityEngine.RectTransform));
root.transform.SetParent(canvas.transform, false);
var rootRect = (UnityEngine.RectTransform)root.transform;
rootRect.anchorMin = new UnityEngine.Vector2(0.5f, 0.5f);
rootRect.anchorMax = new UnityEngine.Vector2(0.5f, 0.5f);
rootRect.anchoredPosition = UnityEngine.Vector2.zero;
rootRect.sizeDelta = UnityEngine.Vector2.zero;

var dot = new UnityEngine.GameObject("Dot", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image));
dot.transform.SetParent(root.transform, false);
var dotRect = (UnityEngine.RectTransform)dot.transform;
dotRect.anchoredPosition = UnityEngine.Vector2.zero;
dotRect.sizeDelta = new UnityEngine.Vector2(6f, 6f);
var image = dot.GetComponent<UnityEngine.UI.Image>();
image.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<UnityEngine.Sprite>("UI/Skin/Knob.psd");
image.color = UnityEngine.Color.white;
image.raycastTarget = false;

// コンポーネントを付けてSerializeFieldを配線する
// Attach the component and wire the serialized field
var view = root.AddComponent<Client.Game.InGame.UI.Crosshair.CrosshairView>();
var so = new UnityEditor.SerializedObject(view);
so.FindProperty("crosshairRoot").objectReferenceValue = dot;
so.ApplyModifiedProperties();

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "CrosshairView placed under: " + canvas.name;
```

- [ ] **Step 4: 配置を検証**

Run: `uloop find-game-objects --project-path ./moorestech_client --name "CrosshairView"`（uloop-find-game-objects スキル参照）
Expected: MainGame シーンの Canvas 配下に 1 件ヒット、`CrosshairView` コンポーネント付き

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 新規エラーなし

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Crosshair moorestech_client/Assets/Scenes/Game/MainGame.unity
git commit -m "feat: FPS建設モード用の中央ドットクロスヘアを追加"
```

---

### Task 5: PlayerObjectController に SetModelVisible を追加

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Player/PlayerObjectController.cs`

**Interfaces:**
- Consumes: なし
- Produces（Task 6 が利用）: `IPlayerObjectController.SetModelVisible(bool visible)`（`PlayerSystemContainer.Instance.PlayerObjectController` 経由で呼ぶ）

- [ ] **Step 1: インターフェースにメソッドを追加**

`PlayerObjectController.cs` の `IPlayerObjectController`（12〜20行）に追加:

```csharp
    public interface IPlayerObjectController
    {
        public Vector3 Position { get; }
        public void SetPlayerPosition(Vector3 playerPos);
        public void SetActive(bool active);

        public void SetAnimationState(string state);
        public void SetControllable(bool enable);
        public void SetModelVisible(bool visible);
    }
```

- [ ] **Step 2: 実装を追加**

`PlayerObjectController` クラスの `SetControllable`（104〜107行）の直後に追加:

```csharp
        public void SetModelVisible(bool visible)
        {
            // 移動・当たり判定は生かしたまま見た目のレンダラーだけ切り替える
            // Toggle only the renderers; movement and collision stay alive
            foreach (var childRenderer in GetComponentsInChildren<Renderer>(true))
            {
                childRenderer.enabled = visible;
            }
        }
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: PASS（`IPlayerObjectController` の実装は `PlayerObjectController` のみのため追加実装漏れは起きない。他実装があればここでコンパイルエラーとして検出される）

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Player/PlayerObjectController.cs
git commit -m "feat: 自機モデルの表示切替APIを追加"
```

---

### Task 6: BuildViewModeController（中核）と DI 登録

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/BuildViewModeController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（197行付近のUIステート登録群）

**Interfaces:**
- Consumes: Task 1 `BuildViewSession` / Task 2 `AimPointProvider.SetScreenCenterAim(bool)` / Task 3 `InGameCameraController.SetFirstPersonView(bool)`・`StartTweenCamera(Vector3, float, Vector3, float)`・`DefaultTrackedObjectOffset`・`FirstPersonTrackedObjectOffset` / Task 4 `CrosshairView.Instance.SetVisible(bool)` / Task 5 `PlayerSystemContainer.Instance.PlayerObjectController.SetModelVisible(bool)` / 既存 `TweenCameraInfo`・`ScreenClickableCameraController.DefaultTweenDuration`・`CreateTopDownTweenCameraInfo()`・`CreateCurrentCameraTweenCameraInfo()`・`InputManager.MouseCursorVisible(bool)`
- Produces（Task 7 が利用）:
  - `void OnEnterBuildState(UIStateEnum state)` — 各建設系ステートの `OnEnter` から呼ぶ
  - `void ManualUpdate()` — 各建設系ステートの `GetNextUpdate` から毎フレーム呼ぶ（Vトグル＋俯瞰時の右クリック回転）
  - `void OnLeaveBuildState(UIStateEnum nextState)` — 遷移を返す直前に呼ぶ（建設系外への遷移のみセッション終了）
  - `void ToggleViewMode()` — Vキーと等価の公開API（E2Eテストからの直接呼び出し用）
  - `BuildViewMode CurrentMode`

- [ ] **Step 1: BuildViewModeController を作成**

`moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/BuildViewModeController.cs` を作成:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Crosshair;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.Input;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.Control.BuildView
{
    /// <summary>
    ///     建設系視点モード（俯瞰/FPS）の唯一の所有者。建設系ステートから明示駆動される
    ///     Sole owner of the build view mode (top-down / FPS), explicitly driven by the build states
    /// </summary>
    public class BuildViewModeController
    {
        private readonly InGameCameraController _inGameCameraController;
        private readonly BuildViewSession _session = new();

        private TweenCameraInfo _sessionStartCameraInfo;

        public BuildViewMode CurrentMode => _session.CurrentMode;

        public BuildViewModeController(InGameCameraController inGameCameraController)
        {
            _inGameCameraController = inGameCameraController;
        }

        // 建設系ステートのOnEnterから呼ぶ。建設系外からの進入でセッションを開始する
        // Called from each build state's OnEnter; starts a session when entering from a non-build state
        public void OnEnterBuildState(UIStateEnum state)
        {
            var sessionStarted = _session.EnterBuildState(state);
            if (sessionStarted)
            {
                _sessionStartCameraInfo = _inGameCameraController.CreateCurrentCameraTweenCameraInfo();
                if (_session.CurrentMode == BuildViewMode.FirstPerson) ApplyFirstPersonCamera();
            }

            // TopDownの俯瞰TweenはPlaceBlock進入時のみ（BuildMenu・DeleteBarではカメラを動かさない）
            // In TopDown the overhead tween runs only on PlaceBlock entry (BuildMenu/DeleteBar leave the camera alone)
            if (_session.CurrentMode == BuildViewMode.TopDown && state == UIStateEnum.PlaceBlock)
            {
                _inGameCameraController.StartTweenCamera(_inGameCameraController.CreateTopDownTweenCameraInfo());
                _session.MarkCameraDirty();
            }

            ApplyCursorAndCrosshair();
        }

        // 建設系ステートのGetNextUpdateから毎フレーム呼ぶ
        // Called every frame from the build states' GetNextUpdate
        public void ManualUpdate()
        {
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetKeyDown(KeyCode.V)) ToggleViewMode();

            UpdateTopDownRightClickRotation();
        }

        // モードを反転しカメラ・カーソル・クロスヘア・自機表示を一括更新する（E2Eからも直接呼べる公開API）
        // Flip the mode and update camera, cursor, crosshair, and player model at once (public for E2E use)
        public void ToggleViewMode()
        {
            if (!_session.IsSessionActive) return;

            _session.ToggleMode();
            if (_session.CurrentMode == BuildViewMode.FirstPerson)
            {
                ApplyFirstPersonCamera();
            }
            else
            {
                ApplyTopDownCameraOnToggle();
            }

            ApplyCursorAndCrosshair();
        }

        // 遷移を返す直前に呼ぶ。建設系外へ抜ける時のみセッションを終了し通常状態へ戻す
        // Called right before returning a transition; ends the session only when leaving the build set
        public void OnLeaveBuildState(UIStateEnum nextState)
        {
            if (!_session.LeaveToState(nextState)) return;

            // セッション中にカメラを動かした場合のみ保存カメラへ復帰する（TopDown削除のみのセッションでは動かさない）
            // Restore the saved camera only when this session moved it (a TopDown delete-only session leaves it alone)
            _inGameCameraController.SetFirstPersonView(false);
            if (_session.IsCameraDirty)
            {
                _inGameCameraController.StartTweenCamera(_sessionStartCameraInfo.Rotation, _sessionStartCameraInfo.Distance, _inGameCameraController.DefaultTrackedObjectOffset, ScreenClickableCameraController.DefaultTweenDuration);
            }

            AimPointProvider.SetScreenCenterAim(false);
            CrosshairView.Instance.SetVisible(false);
            PlayerSystemContainer.Instance.PlayerObjectController.SetModelVisible(true);
            InputManager.MouseCursorVisible(false);
            _inGameCameraController.SetControllable(false);
        }

        private void ApplyFirstPersonCamera()
        {
            // 距離0＋頭部オフセットへTweenし、自機を消して画面中央照準に切り替える
            // Tween to distance 0 with the head offset, hide the player model, and switch to center aim
            _inGameCameraController.SetFirstPersonView(true);
            _inGameCameraController.StartTweenCamera(_inGameCameraController.CameraEulerAngle, 0f, _inGameCameraController.FirstPersonTrackedObjectOffset, ScreenClickableCameraController.DefaultTweenDuration);
            _session.MarkCameraDirty();
            AimPointProvider.SetScreenCenterAim(true);
            PlayerSystemContainer.Instance.PlayerObjectController.SetModelVisible(false);
        }

        private void ApplyTopDownCameraOnToggle()
        {
            // PlaceBlock中は俯瞰へ、それ以外はセッション開始時のカメラへ戻す
            // Tween to overhead while in PlaceBlock, otherwise back to the session-start camera
            _inGameCameraController.SetFirstPersonView(false);
            AimPointProvider.SetScreenCenterAim(false);
            PlayerSystemContainer.Instance.PlayerObjectController.SetModelVisible(true);

            if (_session.CurrentBuildState == UIStateEnum.PlaceBlock)
            {
                var topDown = _inGameCameraController.CreateTopDownTweenCameraInfo();
                _inGameCameraController.StartTweenCamera(topDown.Rotation, topDown.Distance, _inGameCameraController.DefaultTrackedObjectOffset, ScreenClickableCameraController.DefaultTweenDuration);
                _session.MarkCameraDirty();
            }
            else
            {
                _inGameCameraController.StartTweenCamera(_sessionStartCameraInfo.Rotation, _sessionStartCameraInfo.Distance, _inGameCameraController.DefaultTrackedObjectOffset, ScreenClickableCameraController.DefaultTweenDuration);
                _session.ClearCameraDirty();
            }
        }

        private void ApplyCursorAndCrosshair()
        {
            // メニュー中はモードに関わらずカーソル解放・クロスヘア非表示・視点回転なし
            // While the menu is open: free cursor, no crosshair, no look control, regardless of mode
            if (_session.CurrentBuildState == UIStateEnum.BuildMenu)
            {
                InputManager.MouseCursorVisible(true);
                _inGameCameraController.SetControllable(false);
                CrosshairView.Instance.SetVisible(false);
                return;
            }

            if (_session.CurrentMode == BuildViewMode.FirstPerson)
            {
                // FPSはカーソルロック＋常時視点操作＋クロスヘア表示
                // FPS locks the cursor, keeps look control always on, and shows the crosshair
                InputManager.MouseCursorVisible(false);
                _inGameCameraController.SetControllable(true);
                CrosshairView.Instance.SetVisible(true);
            }
            else
            {
                InputManager.MouseCursorVisible(true);
                _inGameCameraController.SetControllable(false);
                CrosshairView.Instance.SetVisible(false);
            }
        }

        private void UpdateTopDownRightClickRotation()
        {
            if (_session.CurrentMode != BuildViewMode.TopDown) return;
            if (_session.CurrentBuildState == UIStateEnum.BuildMenu) return;

            //TODO InputSystemのリファクタ対象
            // 右クリック押下中のみカメラ回転（旧ScreenClickableCameraControllerの挙動を踏襲）
            // Rotate only while right-click is held (carried over from ScreenClickableCameraController)
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                InputManager.MouseCursorVisible(false);
                _inGameCameraController.SetControllable(true);
            }

            if (UnityEngine.Input.GetMouseButtonUp(1))
            {
                InputManager.MouseCursorVisible(true);
                _inGameCameraController.SetControllable(false);
            }
        }
    }
}
```

- [ ] **Step 2: DI 登録を追加**

`MainGameStarter.cs` の UIステート登録群（197行 `builder.Register<UIStateDictionary>(Lifetime.Singleton);` の直前）に追加:

```csharp
            builder.Register<BuildViewModeController>(Lifetime.Singleton);
```

（`using Client.Game.InGame.Control.BuildView;` をファイル先頭に追加）

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: PASS（まだどのステートも呼ばないため挙動変更なし）

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/BuildViewModeController.cs* moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs
git commit -m "feat: 建設視点モードコントローラーを追加しDI登録"
```

---

### Task 7: 建設系3ステートの配線（ScreenClickableCameraController 撤去・Shift+B 廃止・V説明）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs`

**Interfaces:**
- Consumes: Task 6 の `BuildViewModeController.OnEnterBuildState / ManualUpdate / OnLeaveBuildState`
- Produces: なし（最終配線）

- [ ] **Step 1: PlaceBlockState を書き換え**

`PlaceBlockState.cs` 全体を以下に置換:

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control.BuildView;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.Skit;
using Client.Input;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class PlaceBlockState : IUIState
    {
        private readonly BuildViewModeController _buildViewModeController;
        private readonly SkitManager _skitManager;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly List<IDisposable> _blockPlacedDisposable = new();
        private readonly PlaceSystemStateController _placeSystemStateController;

        public PlaceBlockState(SkitManager skitManager, BuildViewModeController buildViewModeController, BlockGameObjectDataStore blockGameObjectDataStore, PlaceSystemStateController placeSystemStateController)
        {
            _skitManager = skitManager;
            _buildViewModeController = buildViewModeController;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _placeSystemStateController = placeSystemStateController;
        }

        public void OnEnter(UITransitContext context)
        {
            // 視点モード（カメラ・カーソル・クロスヘア）はコントローラーが一括適用する
            // The view mode controller applies camera, cursor, and crosshair in one place
            _buildViewModeController.OnEnterBuildState(UIStateEnum.PlaceBlock);

            // ここが重くなったら近いブロックだけプレビューをオンにするなどする
            foreach (var blockGameObject in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                blockGameObject.EnablePreviewOnlyObjects(true, true);
            }
            _blockPlacedDisposable.Add(_blockGameObjectDataStore.OnBlockPlaced.Subscribe(OnPlaceBlock));

            KeyControlDescription.Instance.SetText("Tab: ブロック選択\nQ: 設置高さ上げる\nE: ブロック高さ下げる\nB: 配置モード終了\n左クリック: ブロック配置\nG:ブロック削除\nV: 視点切替");
        }

        public UITransitContext GetNextUpdate()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return Transit(UIStateEnum.PlayerInventory);
            if (InputManager.UI.BlockDelete.GetKeyDown) return Transit(UIStateEnum.DeleteBar);
            if (_skitManager.IsPlayingSkit) return Transit(UIStateEnum.Story);
            // Tabでビルドメニューを開き直す
            // Reopen the build menu with Tab
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) return Transit(UIStateEnum.BuildMenu);
            //TODO InputSystemのリファクタ対象
            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.B)) return Transit(UIStateEnum.GameScreen);

            _buildViewModeController.ManualUpdate();
            _placeSystemStateController.ManualUpdate();

            return null;

            #region Internal

            UITransitContext Transit(UIStateEnum next)
            {
                // 建設系外への遷移でのみセッションが終了する（判定はコントローラー側）
                // The controller ends the session only when the destination leaves the build set
                _buildViewModeController.OnLeaveBuildState(next);
                return new UITransitContext(next);
            }

            #endregion
        }

        private void OnPlaceBlock(BlockGameObject blockGameObject)
        {
            blockGameObject.EnablePreviewOnlyObjects(true, false);

            _blockPlacedDisposable.Add(blockGameObject.OnFinishedPlaceAnimation.Subscribe(_ =>
            {
                blockGameObject.EnablePreviewOnlyObjects(true, true);
            }));
        }

        public void OnExit()
        {
            _placeSystemStateController.Disable();

            foreach (var blockGameObject in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                blockGameObject.EnablePreviewOnlyObjects(false, false);
            }

            _blockPlacedDisposable.ForEach(d => d.Dispose());
            _blockPlacedDisposable.Clear();
        }
    }
}
```

削除されるもの: `_screenClickableCameraController` フィールドと全呼び出し、`_isChangeCameraAngle`（Shift+B分岐）、`InGameCameraController` 依存、俯瞰Tweenの直接実行（コントローラーへ移動）。

- [ ] **Step 2: DeleteObjectState を書き換え**

`DeleteObjectState.cs` 全体を以下に置換:

```csharp
using Client.Game.InGame.Control.BuildView;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class DeleteObjectState : IUIState
    {
        private readonly DeleteBarObject _deleteBarObject;

        private readonly BuildViewModeController _buildViewModeController;
        private readonly DeleteObjectService _deleteObjectService = new();

        public DeleteObjectState(DeleteBarObject deleteBarObject, BuildViewModeController buildViewModeController, RailGraphClientCache cache)
        {
            _buildViewModeController = buildViewModeController;
            _deleteBarObject = deleteBarObject;
            deleteBarObject.gameObject.SetActive(false);
        }

        public void OnEnter(UITransitContext context)
        {
            // 視点モードの適用はコントローラーに委譲（TopDownでは従来どおりカメラを動かさない）
            // Delegate view-mode handling to the controller (TopDown keeps the camera untouched as before)
            _buildViewModeController.OnEnterBuildState(UIStateEnum.DeleteBar);
            _deleteBarObject.gameObject.SetActive(true);
            KeyControlDescription.Instance.SetText("ドラッグ: まとめて選択\n離す: まとめて削除\nESC: 選択キャンセル\nG: 破壊モード終了\nB: 設置モード\nTab: インベントリ\nV: 視点切替");
        }

        public UITransitContext GetNextUpdate()
        {
            // モード遷移を判定する（ESCはモードを抜けず削除サービス側で選択キャンセルに使う）
            // Handle mode transitions (ESC stays in the mode and is used as selection cancel by the delete service)
            var transit = HandleTransition();
            if (transit != null) return transit;

            // 削除インタラクションはサービスに委譲する
            // Delegate the delete interaction to the service
            _deleteObjectService.Update();

            _buildViewModeController.ManualUpdate();
            return null;

            #region Internal

            UITransitContext HandleTransition()
            {
                // OpenMenu(ポーズ)もESCにbindされ、ここで拾うとESCの選択キャンセル/モード終了が死ぬため破壊モードでは扱わない
                // OpenMenu(pause) is also bound to ESC; handling it here would shadow ESC's cancel/exit, so skip it in destroy mode
                if (InputManager.UI.BlockDelete.GetKeyDown) return Transit(UIStateEnum.GameScreen);
                if (UnityEngine.Input.GetKeyDown(KeyCode.B)) return Transit(UIStateEnum.BuildMenu);
                if (InputManager.UI.OpenInventory.GetKeyDown) return Transit(UIStateEnum.PlayerInventory);

                // ESCはまず削除選択のキャンセルに使い、キャンセルする選択が無ければ破壊モードを抜ける
                // ESC is used first to cancel the delete selection; with nothing to cancel it leaves destroy mode
                if (InputManager.UI.CloseUI.GetKeyDown && !_deleteObjectService.TryCancelSelection())
                {
                    return Transit(UIStateEnum.GameScreen);
                }
                return null;
            }

            UITransitContext Transit(UIStateEnum next)
            {
                _buildViewModeController.OnLeaveBuildState(next);
                return new UITransitContext(next);
            }

            #endregion
        }

        public void OnExit()
        {
            _deleteObjectService.CancelSelection();
            _deleteBarObject.gameObject.SetActive(false);
        }
    }
}
```

注意: コンストラクタの `InGameCameraController` 依存は `BuildViewModeController` に置き換わる（未使用だった `RailGraphClientCache cache` は現状維持）。

- [ ] **Step 3: BuildMenuState を書き換え**

`BuildMenuState.cs` 全体を以下に置換:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control.BuildView;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.KeyControl;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class BuildMenuState : IUIState
    {
        private readonly BuildMenuView _buildMenuView;
        private readonly PlacementSelection _placementSelection;
        private readonly BuildViewModeController _buildViewModeController;

        public BuildMenuState(BuildMenuView buildMenuView, PlacementSelection placementSelection, BuildViewModeController buildViewModeController)
        {
            _buildMenuView = buildMenuView;
            _placementSelection = placementSelection;
            _buildViewModeController = buildViewModeController;
        }

        public void OnEnter(UITransitContext context)
        {
            // カーソル解放・クロスヘア非表示はコントローラーが適用する（FPS中もメニュー操作を可能にする）
            // The controller frees the cursor and hides the crosshair (keeps the menu usable during FPS)
            _buildViewModeController.OnEnterBuildState(UIStateEnum.BuildMenu);
            _buildMenuView.SetActive(true);
            KeyControlDescription.Instance.SetText("クリック: 設置ブロック選択  B: 閉じる  V: 視点切替");
        }

        public UITransitContext GetNextUpdate()
        {
            // 選択が確定したら種別に応じて選択状態を設定し設置モードへ遷移する
            // On selection, set the placement selection by entry type and transition to placement mode
            if (_buildMenuView.TryConsumeSelectedEntry(out var entry))
            {
                switch (entry.EntryType)
                {
                    case PlacementSelectionType.Block:
                        _placementSelection.SetSelectedBlock(entry.BlockId);
                        break;
                    case PlacementSelectionType.TrainCar:
                        _placementSelection.SetSelectedTrainCar(entry.TrainCarGuid);
                        break;
                    case PlacementSelectionType.ConnectTool:
                        _placementSelection.SetSelectedConnectTool(entry.ConnectPlaceMode);
                        break;
                }
                return Transit(UIStateEnum.PlaceBlock);
            }

            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.B)) return Transit(UIStateEnum.GameScreen);
            if (InputManager.UI.OpenInventory.GetKeyDown) return Transit(UIStateEnum.PlayerInventory);

            _buildViewModeController.ManualUpdate();
            return null;

            #region Internal

            UITransitContext Transit(UIStateEnum next)
            {
                _buildViewModeController.OnLeaveBuildState(next);
                return new UITransitContext(next);
            }

            #endregion
        }

        public void OnExit()
        {
            _buildMenuView.SetActive(false);
        }
    }
}
```

削除されるもの: `OnEnter` の `InputManager.MouseCursorVisible(true)` と `OnExit` の `InputManager.MouseCursorVisible(false)`（カーソル制御の所有者をコントローラーに一本化。遷移先が建設系なら次の `OnEnterBuildState` が、建設系外なら `OnLeaveBuildState` と遷移先ステートが設定する）。

- [ ] **Step 4: ScreenClickableCameraController の残存利用を確認**

Run: `grep -rn "ScreenClickableCameraController" moorestech_client/Assets/Scripts --include="*.cs" | grep -v "Input/ScreenClickableCameraController.cs"`
Expected: `DebugBlockInfoState.cs` の2箇所と `BuildViewModeController.cs`（`DefaultTweenDuration` 参照）のみ。建設系3ステートには残っていないこと。

- [ ] **Step 5: コンパイルと全関連テスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: PASS

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BuildView|PlaceSystem|UIState"`
Expected: 全テストPASS（BuildViewSessionTest 8件・AimPointProviderTest 3件・既存 DragDelete系・PlaceSystem系の回帰）

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 新規エラーなし

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs
git commit -m "feat: 建設系ステートを視点モードコントローラー駆動に切替（Shift+B廃止）"
```

---

### Task 8: E2E プレイテストと手動確認

**Files:**
- なし（検証のみ。プレイテストシナリオを追加する場合は unity-playmode-recorded-playtest スキルの出力先規約に従う）

**Interfaces:**
- Consumes: Task 6 `BuildViewModeController.ToggleViewMode()`（Vキー相当の直接呼び出し）、UIStateControl のリフレクションチェーン
- Produces: 検証結果（回帰なしの確認）

- [ ] **Step 1: プレイテストDSLで俯瞰モードの設置回帰を確認**

unity-playmode-recorded-playtest スキル（`tools/playtest/run-scenario.sh`）を起動し、既存の「UI経路でのブロック設置」シナリオを実行する。
Expected: 俯瞰モード（従来挙動）でブロック設置がサーバーに反映される（result.json が成功）。
注意: サーバーポート11564固定のため他worktreeのPlayModeと同時実行不可。マスタデータはブランチ互換コミットへピン留めしたworktreeを使う。

- [ ] **Step 2: FPSモードでの設置E2E**

PlayMode起動中に `uloop execute-dynamic-code` で建設ステートに入った後（DSLでBキー→メニュー選択→PlaceBlock）、Vキー相当を直接呼ぶ:

```csharp
// Vキーはlegacy Input直読みのためQueueStateEventで注入できない。コントローラーAPIを直接呼ぶ
// The V key is read via legacy Input and cannot be injected; call the controller API directly
var uiStateControl = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.UI.UIState.UIStateControl>();
var dictField = typeof(Client.Game.InGame.UI.UIState.UIStateControl).GetField("_uiStateDictionary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var dict = (Client.Game.InGame.UI.UIState.UIStateDictionary)dictField.GetValue(uiStateControl);
var placeBlockState = dict.GetState(Client.Game.InGame.UI.UIState.UIStateEnum.PlaceBlock);
var controllerField = placeBlockState.GetType().GetField("_buildViewModeController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var controller = (Client.Game.InGame.Control.BuildView.BuildViewModeController)controllerField.GetValue(placeBlockState);
controller.ToggleViewMode();
return "mode=" + controller.CurrentMode;
```

Expected: 戻り値 `mode=FirstPerson`。その後DSLでマウス移動（視点回転）→左クリック設置し、画面中央照準の位置にブロックがサーバー反映されること。カメラ距離が0（一人称）、クロスヘア表示、自機非表示であることを `uloop screenshot` で確認。

- [ ] **Step 3: セッション継続・終了の実機確認**

同一PlayMode内で以下を順に実行し、それぞれ `uloop screenshot` とログで確認:
1. FPS中にTab → BuildMenu: カメラFPSのまま・カーソル解放・クロスヘア非表示
2. メニューで選択 → PlaceBlock復帰: カーソル再ロック・クロスヘア再表示
3. B → GameScreen: 三人称カメラへ復帰Tween・自機再表示・クロスヘア非表示
4. 再度 B→（メニュー経由）PlaceBlock: FPSで開始（モード記憶）

Expected: 各ステップでスクリーンショットが期待見た目と一致し、`uloop get-logs --log-type Error` に新規エラーなし。

- [ ] **Step 4: 手動確認項目の申し送り**

以下は人間の目でのみ判断できるため、ユーザーへの完了報告に手動確認依頼として明記する:
- カメラTweenの見た目（俯瞰⇔FPSの0.25秒Tweenの気持ちよさ、頭部高さ1.6fの妥当性 — `InGameCameraController.FirstPersonHeadHeight` 定数で調整可能）
- 壁際でのニアクリップのめり込み（問題があれば既存カメラ設定のニアクリップを調整）
- カーソルロック切替の操作感（V連打時の挙動を含む。Tween多重は `_currentSequence?.Kill()` で既に防止済み）
- FPS削除モードでの `MouseCursorTooltip`（削除不可理由）の表示位置（カーソルロック中はマウス座標が固定されるため、視認性に問題があれば別途改善）

- [ ] **Step 5: 最終コミット確認**

```bash
git status
```
Expected: 未コミットの作業が残っていないこと（worktree運用のため作業消失防止として必須）。プレイテストシナリオ等の追加ファイルがあればコミットする。

---

## Self-Review 結果（作成時実施済み）

- **Spec coverage**: 決定事項1〜5（共存トグル/V即時切替＋記憶/リーチ共通=変更なし/Place+Delete+Menu適用/Shift+B廃止）→ Task 1・6・7。照準一元化 → Task 2。カメラ（距離0・頭部オフセット・F1/F2無効）→ Task 3。クロスヘア → Task 4。自機非表示 → Task 5。エッジケース（メニュー中カーソル解放/セッション終了条件/削除モードのカメラ非移動/Tween多重防止）→ Task 6 のロジックと Task 8 の検証に反映。テスト計画4種 → Task 1・2・8。
- **Placeholder scan**: 全コードステップに完全なコードを記載。TBD/「適切に」系の指示なし。
- **Type consistency**: `BuildViewSession.EnterBuildState/LeaveToState/ToggleMode/MarkCameraDirty/ClearCameraDirty`、`InGameCameraController.SetFirstPersonView/StartTweenCamera(4引数)/DefaultTrackedObjectOffset/FirstPersonTrackedObjectOffset/FirstPersonHeadHeight`、`AimPointProvider.SetScreenCenterAim/GetAimScreenPoint`、`CrosshairView.Instance.SetVisible`、`IPlayerObjectController.SetModelVisible`、`BuildViewModeController.OnEnterBuildState/ManualUpdate/OnLeaveBuildState/ToggleViewMode` — 定義タスクと利用タスクで署名一致を確認済み。
- **構造検査（spec-architecture-review）**: 配置決定8件をインベントリ化し層責務・前例・イディオムと突合。違反0件（購読方式は事前にステート駆動へ修正済み）。新規パターン・spec逸脱4件は「配置と前例」セクションの注目点として明記。
