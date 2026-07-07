# FPS建設モード Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建設・削除モードに一人称視点（FPS）を追加し、Vキーで俯瞰⇔FPSを即時切替できるようにする。

**Architecture:** `BuildViewModeController`（DIシングルトン・純C#）が視点モードの記憶・トグル・建設系ステート間のカメラセッションを所有し、**各建設系ステートから`OnEnterBuildState`/`OnLeaveBuildState`/`ManualUpdate`で明示的に駆動される**（`UIStateControl`への参照・購読なし。依存方向はUIState層→Control層の一方向）。副作用は`IBuildViewApplier`越しに適用（テストはFakeで駆動）。レイキャスト起点は`AimPointProvider`（静的）で「マウス座標 or 画面中央」を一元切替し、全設置システムが無改修でFPS対応する。

**Tech Stack:** Unity 6 / VContainer / Cinemachine (FramingTransposer) / DOTween / NUnit (EditMode) / uloop CLI

**Spec:** `docs/superpowers/specs/2026-07-07-fps-build-mode-design.md`

## Global Constraints

- 1ファイル200行以下。partial禁止。try-catch禁止。デフォルト引数の新規追加禁止
- 主要処理に日本語・英語2行セットコメント（各1行、約3〜10行ごと）
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client` を実行
- .metaファイルは手動作成禁止（Unityが自動生成したものはコミット可）
- シーン等Unity固有ファイルの直接編集禁止。変更は `uloop execute-dynamic-code` 経由のみ
- 各タスク末尾で必ずgit commit（作業ディレクトリは `/Users/katsumi/moorestech`。最初に`pwd`確認）
- テスト実行: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。ドメインリロードエラー時は45秒待ってリトライ
- 新規イベントはUniRx（本計画ではイベント新設・購読なし。コントローラはステートから直接駆動される）

## 配置と前例

- `Control/BuildView/` 新設（Control直下は現在5ファイル、10ファイル制限内に収めるためサブディレクトリ化）
- 画面中央レイの前例: `MapObjectMiningController.cs:45`（採掘は既に画面中央レイ）
- ステートがコントローラを明示駆動する前例: `PlaceBlockState`→`PlaceSystemStateController.ManualUpdate()/Disable()`（`BuildViewModeController`も同じ駆動パターンに乗る。逆向きの`OnStateChanged`購読はしない）
- Fakeによるテストの前例: `Client.Tests/UIState/FakeDeleteTarget.cs`
- Vキーは`UnityEngine.Input.GetKeyDown`直接読み＋`//TODO InputSystemのリファクタ対象`（`PlaceBlockState.cs:40,67`等の既存前例に準拠。`.inputactions`は触らない）
- DI登録は`MainGameStarter.cs`の「設置システム」ブロック直後（`builder.Register<PlacementSelection>`の下）

---

### Task 1: BuildViewMode enum + AimPointProvider + レイ起点の一元化

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/BuildViewMode.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/AimPointProvider.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BuildView/AimPointProviderTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs:35,56,73`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/ElectricWireEditMode.cs:65`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BlockClickDetectUtil.cs:48-49`

**Interfaces:**
- Consumes: なし（最初のタスク）
- Produces: `enum BuildViewMode { TopDown, FirstPerson }`（namespace `Client.Game.InGame.Control.BuildView`）、`static class AimPointProvider { static BuildViewMode CurrentMode; static void SetMode(BuildViewMode); static Vector3 GetAimScreenPoint(); }`

- [ ] **Step 1: enumとAimPointProviderを作成**

`BuildViewMode.cs`:

```csharp
namespace Client.Game.InGame.Control.BuildView
{
    /// <summary>
    ///     建設系モード（設置・削除・ビルドメニュー）の視点モード
    ///     View mode for build-related states (place, delete, build menu)
    /// </summary>
    public enum BuildViewMode
    {
        TopDown,
        FirstPerson,
    }
}
```

`AimPointProvider.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Game.InGame.Control.BuildView
{
    /// <summary>
    ///     設置・削除の照準スクリーン座標を視点モードに応じて提供する
    ///     Provides the aim screen point for placement and deletion based on the view mode
    /// </summary>
    public static class AimPointProvider
    {
        public static BuildViewMode CurrentMode { get; private set; } = BuildViewMode.TopDown;
        
        public static void SetMode(BuildViewMode mode)
        {
            CurrentMode = mode;
        }
        
        public static Vector3 GetAimScreenPoint()
        {
            // FPSはカーソルロックのため画面中央を照準にする
            // FPS locks the cursor, so aim at the screen center
            if (CurrentMode == BuildViewMode.FirstPerson) return new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
            
            return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : UnityEngine.Input.mousePosition;
        }
    }
}
```

- [ ] **Step 2: 失敗するテストを書く**

`Client.Tests/BuildView/AimPointProviderTest.cs`:

```csharp
using Client.Game.InGame.Control.BuildView;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BuildView
{
    /// <summary>
    ///     AimPointProviderのモード別照準座標を検証するテスト
    ///     Tests verifying AimPointProvider aim points per view mode
    /// </summary>
    public class AimPointProviderTest
    {
        [TearDown]
        public void TearDown()
        {
            AimPointProvider.SetMode(BuildViewMode.TopDown);
        }
        
        [Test]
        public void FirstPersonReturnsScreenCenter()
        {
            AimPointProvider.SetMode(BuildViewMode.FirstPerson);
            var point = AimPointProvider.GetAimScreenPoint();
            Assert.AreEqual(Screen.width / 2f, point.x);
            Assert.AreEqual(Screen.height / 2f, point.y);
        }
        
        [Test]
        public void SetModeUpdatesCurrentMode()
        {
            AimPointProvider.SetMode(BuildViewMode.FirstPerson);
            Assert.AreEqual(BuildViewMode.FirstPerson, AimPointProvider.CurrentMode);
        }
    }
}
```

- [ ] **Step 3: コンパイル＆テスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "AimPointProvider"`
Expected: 2件PASS

- [ ] **Step 4: レイ起点3ファイルを置換**

`PlaceSystemUtil.cs` — usingに `using Client.Game.InGame.Control.BuildView;` を追加し、35行・56行・73行の3箇所:

```csharp
// 変更前
var ray = mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
// 変更後
var ray = mainCamera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
```

`ElectricWireEditMode.cs:65` — usingに `using Client.Game.InGame.Control.BuildView;` を追加し同様に置換。

`BlockClickDetectUtil.cs:48-49` — usingに `using Client.Game.InGame.Control.BuildView;` を追加し、2行を1行へ:

```csharp
// 変更前
var mousePosition = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : UnityEngine.Input.mousePosition;
var ray = camera.ScreenPointToRay(mousePosition);
// 変更後
var ray = camera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
```

置換漏れ確認: `grep -rn "ScreenPointToRay(UnityEngine.Input.mousePosition" moorestech_client/Assets/Scripts --include="*.cs"` が0件であること（`MapObjectMiningController.cs`の画面中央レイと`UICursorFollowControl.cs`のUIカーソル追従、`GameObjectToolTipTargetController.cs`のツールチップは照準ではないため対象外）。

- [ ] **Step 5: コンパイル＆既存回帰**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceSystem"`
Expected: 全件PASS（既存のPlaceSystemテストが照準変更で壊れていないこと）

- [ ] **Step 6: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView moorestech_client/Assets/Scripts/Client.Tests/BuildView moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/ElectricWireEditMode.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BlockClickDetectUtil.cs
git commit -m "feat: 照準座標をAimPointProviderへ一元化しFPS視点モードenumを追加"
```

（Unity生成の.metaが未追跡で残っていれば同時に`git add`してよい。以降のタスクも同様）

---

### Task 2: カメラFPS化・自機非表示・クロスヘアの各部品

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/InGameCameraController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Player/PlayerObjectController.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Crosshair/CrosshairView.cs`

**Interfaces:**
- Consumes: なし
- Produces: `InGameCameraController.SetFirstPersonMode(bool enabled)`、`IPlayerObjectController.SetModelVisible(bool visible)`、`CrosshairView.Instance` / `CrosshairView.SetVisible(bool visible)`

MonoBehaviour副作用のみでNUnit単体テスト対象外。検証はコンパイル＋Task 6のE2Eで行う。

- [ ] **Step 1: InGameCameraControllerにFPSモードを追加**

フィールド追加（`_isControllable`の下）:

```csharp
        private const float FirstPersonCameraDistance = 0.15f;
        private const float FirstPersonTweenDuration = 0.25f;
        private static readonly Vector3 FirstPersonTrackedOffset = new(0f, 1.6f, 0f);
        
        private bool _isFirstPerson;
        private Vector3 _storedTrackedObjectOffset;
        private Tweener _offsetTweener;
```

`Update()`冒頭のズーム処理をFPS中スキップに変更:

```csharp
        // 変更前
        var distance = _cinemachineFraming.m_CameraDistance;
        if (UnityEngine.Input.GetKey(KeyCode.F1)) distance -= Time.deltaTime * 3f; // TODO InputManagerに移動
        if (UnityEngine.Input.GetKey(KeyCode.F2)) distance += Time.deltaTime * 3f; // TODO InputManagerに移動
        _cinemachineFraming.m_CameraDistance = Mathf.Clamp(distance, 0.6f, 10);
        
        // 変更後
        // FPS中は距離クランプがFPS距離を上書きするためズーム処理ごと止める
        // Skip zoom while in FPS because the clamp would override the FPS distance
        if (!_isFirstPerson)
        {
            var distance = _cinemachineFraming.m_CameraDistance;
            if (UnityEngine.Input.GetKey(KeyCode.F1)) distance -= Time.deltaTime * 3f; // TODO InputManagerに移動
            if (UnityEngine.Input.GetKey(KeyCode.F2)) distance += Time.deltaTime * 3f; // TODO InputManagerに移動
            _cinemachineFraming.m_CameraDistance = Mathf.Clamp(distance, 0.6f, 10);
        }
```

メソッド追加（`SetEnabled`の下）:

```csharp
        public void SetFirstPersonMode(bool enabled)
        {
            if (_isFirstPerson == enabled) return;
            _isFirstPerson = enabled;
            
            _offsetTweener?.Kill();
            if (enabled)
            {
                // 三人称の追従オフセットを保存し頭部高さ・最小距離へ寄せる
                // Store the third-person tracked offset, then tween to head height and minimum distance
                _storedTrackedObjectOffset = _cinemachineFraming.m_TrackedObjectOffset;
                StartTweenCamera(CameraEulerAngle, FirstPersonCameraDistance, FirstPersonTweenDuration);
                _offsetTweener = DOTween.To(() => _cinemachineFraming.m_TrackedObjectOffset, x => _cinemachineFraming.m_TrackedObjectOffset = x, FirstPersonTrackedOffset, FirstPersonTweenDuration);
            }
            else
            {
                // 距離はこの後のTween（俯瞰 or 復帰）に任せ、オフセットのみ戻す
                // Only restore the offset; distance is handled by the following top-down or restore tween
                _offsetTweener = DOTween.To(() => _cinemachineFraming.m_TrackedObjectOffset, x => _cinemachineFraming.m_TrackedObjectOffset = x, _storedTrackedObjectOffset, FirstPersonTweenDuration);
            }
        }
```

- [ ] **Step 2: PlayerObjectControllerに自機表示切替を追加**

`IPlayerObjectController`インターフェースにメンバー追加:

```csharp
        public void SetModelVisible(bool visible);
```

`PlayerObjectController`クラスにフィールドとメソッド追加（`SetControllable`の下）:

```csharp
        private Renderer[] _modelRenderers;
        
        public void SetModelVisible(bool visible)
        {
            // FPS視点で自機が映り込まないよう見た目だけ切り替える
            // Toggle only the visuals so the player mesh does not block the FPS view
            _modelRenderers ??= GetComponentsInChildren<Renderer>(true);
            foreach (var modelRenderer in _modelRenderers) modelRenderer.enabled = visible;
        }
```

- [ ] **Step 3: CrosshairViewを作成**

`Client.Game/InGame/UI/Crosshair/CrosshairView.cs`:

```csharp
using UnityEngine;

namespace Client.Game.InGame.UI.Crosshair
{
    /// <summary>
    ///     FPS建設モードの画面中央クロスヘア
    ///     Center-screen crosshair for the FPS build mode
    /// </summary>
    public class CrosshairView : MonoBehaviour
    {
        private static CrosshairView _instance;
        public static CrosshairView Instance => _instance;
        
        [SerializeField] private GameObject dotObject;
        
        private void Awake()
        {
            _instance = this;
            dotObject.SetActive(false);
        }
        
        public void SetVisible(bool visible)
        {
            dotObject.SetActive(visible);
        }
    }
}
```

（ルートは常時アクティブでシーンに事前配置し、Awakeで`_instance`設定＝Objectシングルトンパターン。シーンへの配置はTask 5）

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 5: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Control/InGameCameraController.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Player/PlayerObjectController.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Crosshair
git commit -m "feat: FPSカメラモード・自機非表示・クロスヘアViewを追加"
```

---

### Task 3: BuildViewModeController（中核ロジック・TDD）＋DI登録

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/IBuildViewApplier.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/BuildViewApplier.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView/BuildViewModeController.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BuildView/FakeBuildViewApplier.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BuildView/BuildViewModeControllerTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（`builder.Register<PlacementSelection>`の直後）

**Interfaces:**
- Consumes: Task 1の`BuildViewMode`/`AimPointProvider`、Task 2の`SetFirstPersonMode`/`SetModelVisible`/`CrosshairView`、既存の`TweenCameraInfo`（現在は`Client.Game.InGame.UI.UIState.Input`。Task 4で`Client.Game.InGame.Control`へ移設されるため、本タスクではusingで旧namespaceを参照）
- Produces: `BuildViewModeController { BuildViewMode CurrentMode; void OnEnterBuildState(UIStateEnum state); void OnLeaveBuildState(UIStateEnum next); void ManualUpdate(); void ToggleViewMode(); }`、`IBuildViewApplier`（下記7メソッド）。コントローラは`UIStateControl`に依存しない（Task 4で各ステートが駆動する）

- [ ] **Step 1: IBuildViewApplierを作成**

```csharp
using Client.Game.InGame.UI.UIState.Input;

namespace Client.Game.InGame.Control.BuildView
{
    /// <summary>
    ///     視点モード切替の副作用（カメラ・カーソル・クロスヘア・自機表示）を適用する
    ///     Applies view-mode side effects (camera, cursor, crosshair, player model)
    /// </summary>
    public interface IBuildViewApplier
    {
        TweenCameraInfo CaptureCurrentCamera();
        void ApplyTopDownCamera();
        void RestoreCamera(TweenCameraInfo saved);
        void SetFirstPersonCamera(bool enabled);
        void SetCursorVisible(bool visible);
        void SetCrosshairVisible(bool visible);
        void SetCameraRotatable(bool rotatable);
    }
}
```

- [ ] **Step 2: FakeBuildViewApplierと失敗するテストを書く**

`Client.Tests/BuildView/FakeBuildViewApplier.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Control.BuildView;
using Client.Game.InGame.UI.UIState.Input;
using UnityEngine;

namespace Client.Tests.BuildView
{
    /// <summary>
    ///     副作用呼び出しを記録するテスト用Applier
    ///     Test applier recording side-effect calls
    /// </summary>
    public class FakeBuildViewApplier : IBuildViewApplier
    {
        public readonly List<string> Calls = new();
        public TweenCameraInfo CapturedCamera { get; } = new(Vector3.zero, 5f);
        public TweenCameraInfo LastRestoredCamera { get; private set; }
        public bool? LastFirstPersonCamera { get; private set; }
        public bool? LastCursorVisible { get; private set; }
        public bool? LastCrosshairVisible { get; private set; }
        
        public TweenCameraInfo CaptureCurrentCamera()
        {
            Calls.Add("Capture");
            return CapturedCamera;
        }
        
        public void ApplyTopDownCamera()
        {
            Calls.Add("TopDown");
        }
        
        public void RestoreCamera(TweenCameraInfo saved)
        {
            Calls.Add("Restore");
            LastRestoredCamera = saved;
        }
        
        public void SetFirstPersonCamera(bool enabled)
        {
            Calls.Add($"Fps:{enabled}");
            LastFirstPersonCamera = enabled;
        }
        
        public void SetCursorVisible(bool visible)
        {
            Calls.Add($"Cursor:{visible}");
            LastCursorVisible = visible;
        }
        
        public void SetCrosshairVisible(bool visible)
        {
            Calls.Add($"Crosshair:{visible}");
            LastCrosshairVisible = visible;
        }
        
        public void SetCameraRotatable(bool rotatable)
        {
            Calls.Add($"Rotatable:{rotatable}");
        }
    }
}
```

`Client.Tests/BuildView/BuildViewModeControllerTest.cs`:

```csharp
using Client.Game.InGame.Control.BuildView;
using Client.Game.InGame.UI.UIState;
using NUnit.Framework;

namespace Client.Tests.BuildView
{
    /// <summary>
    ///     視点モードのセッション管理・トグル・記憶を検証するテスト
    ///     Tests verifying view-mode session handling, toggling, and memory
    /// </summary>
    public class BuildViewModeControllerTest
    {
        private FakeBuildViewApplier _applier;
        private BuildViewModeController _controller;
        
        [SetUp]
        public void SetUp()
        {
            _applier = new FakeBuildViewApplier();
            _controller = new BuildViewModeController(_applier);
        }
        
        [TearDown]
        public void TearDown()
        {
            AimPointProvider.SetMode(BuildViewMode.TopDown);
        }
        
        [Test]
        public void EnterPlaceBlockInTopDownAppliesTopDownCamera()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            Assert.Contains("Capture", _applier.Calls);
            Assert.Contains("TopDown", _applier.Calls);
            Assert.AreEqual(true, _applier.LastCursorVisible);
        }
        
        [Test]
        public void EnterDeleteBarInTopDownDoesNotMoveCamera()
        {
            _controller.OnEnterBuildState(UIStateEnum.DeleteBar);
            Assert.IsFalse(_applier.Calls.Contains("TopDown"));
            Assert.IsFalse(_applier.Calls.Contains("Restore"));
        }
        
        [Test]
        public void TransitBetweenBuildStatesCapturesCameraOnlyOnce()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.OnLeaveBuildState(UIStateEnum.DeleteBar);
            _controller.OnEnterBuildState(UIStateEnum.DeleteBar);
            Assert.AreEqual(1, _applier.Calls.FindAll(c => c == "Capture").Count);
        }
        
        [Test]
        public void LeaveToBuildStateDoesNotRestoreCamera()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.OnLeaveBuildState(UIStateEnum.BuildMenu);
            Assert.IsFalse(_applier.Calls.Contains("Restore"));
        }
        
        [Test]
        public void LeaveToGameScreenRestoresSavedCameraAndHidesCursor()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.OnLeaveBuildState(UIStateEnum.GameScreen);
            Assert.AreSame(_applier.CapturedCamera, _applier.LastRestoredCamera);
            Assert.AreEqual(false, _applier.LastCursorVisible);
        }
        
        [Test]
        public void ToggleToFirstPersonAppliesFpsCursorLockAndCrosshair()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            Assert.AreEqual(BuildViewMode.FirstPerson, _controller.CurrentMode);
            Assert.AreEqual(BuildViewMode.FirstPerson, AimPointProvider.CurrentMode);
            Assert.AreEqual(true, _applier.LastFirstPersonCamera);
            Assert.AreEqual(true, _applier.LastCrosshairVisible);
            Assert.AreEqual(false, _applier.LastCursorVisible);
        }
        
        [Test]
        public void FirstPersonModeIsRememberedAcrossSessions()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            _controller.OnLeaveBuildState(UIStateEnum.GameScreen);
            _applier.Calls.Clear();
            
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            Assert.Contains("Fps:True", _applier.Calls);
            Assert.IsFalse(_applier.Calls.Contains("TopDown"));
        }
        
        [Test]
        public void BuildMenuInFirstPersonFreesCursorAndHidesCrosshairKeepingCamera()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            _controller.OnLeaveBuildState(UIStateEnum.BuildMenu);
            _controller.OnEnterBuildState(UIStateEnum.BuildMenu);
            Assert.AreEqual(true, _applier.LastCursorVisible);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
            Assert.IsFalse(_applier.Calls.Contains("Fps:False"));
        }
        
        [Test]
        public void LeaveFromFirstPersonDisablesFpsAndRestores()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            _controller.OnLeaveBuildState(UIStateEnum.GameScreen);
            Assert.Contains("Fps:False", _applier.Calls);
            Assert.Contains("Restore", _applier.Calls);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
        }
        
        [Test]
        public void ToggleBackToTopDownInDeleteBarRestoresSavedCamera()
        {
            _controller.OnEnterBuildState(UIStateEnum.DeleteBar);
            _controller.ToggleViewMode();
            _applier.Calls.Clear();
            _controller.ToggleViewMode();
            Assert.Contains("Fps:False", _applier.Calls);
            Assert.Contains("Restore", _applier.Calls);
        }
        
        [Test]
        public void ToggleBackToTopDownInPlaceBlockAppliesTopDown()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            _applier.Calls.Clear();
            _controller.ToggleViewMode();
            Assert.Contains("Fps:False", _applier.Calls);
            Assert.Contains("TopDown", _applier.Calls);
            Assert.IsFalse(_applier.Calls.Contains("Restore"));
        }
    }
}
```

- [ ] **Step 3: コンパイルしてテストが失敗することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BuildViewModeController`が存在しないためコンパイルエラー（これがTDDの失敗確認。Unityではコンパイルエラー＝テスト実行不可がRED状態）

- [ ] **Step 4: BuildViewModeControllerを実装**

```csharp
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.Input;
using UnityEngine;

namespace Client.Game.InGame.Control.BuildView
{
    /// <summary>
    ///     建設系視点モードの記憶・トグル・カメラセッションを管理する
    ///     Owns build view mode memory, toggling, and the camera session across build states
    ///     各建設系ステートがOnEnterBuildState/OnLeaveBuildState/ManualUpdateで駆動する（UIStateControlへの依存なし）
    ///     Driven by each build state via OnEnterBuildState/OnLeaveBuildState/ManualUpdate (no UIStateControl dependency)
    /// </summary>
    public class BuildViewModeController
    {
        public BuildViewMode CurrentMode { get; private set; } = BuildViewMode.TopDown;
        
        private readonly IBuildViewApplier _applier;
        
        private TweenCameraInfo _savedCamera;
        private bool _isSessionActive;
        private UIStateEnum _currentBuildState;
        
        public BuildViewModeController(IBuildViewApplier applier)
        {
            _applier = applier;
        }
        
        // 建設系ステートのOnEnter先頭で呼ぶ
        // Call at the top of a build state's OnEnter
        public void OnEnterBuildState(UIStateEnum state)
        {
            _currentBuildState = state;
            
            // セッション開始時のみ復帰用に現在カメラを保存する
            // Save the current camera for restoration only when the session starts
            if (!_isSessionActive)
            {
                _savedCamera = _applier.CaptureCurrentCamera();
                _isSessionActive = true;
            }
            
            ApplyForState(state);
        }
        
        // 遷移確定時（UITransitContextを返す直前）に呼ぶ。建設系への遷移ならno-op
        // Call right before returning a UITransitContext; no-op when moving to another build state
        public void OnLeaveBuildState(UIStateEnum next)
        {
            if (!_isSessionActive || IsBuildState(next)) return;
            
            // FPSを解除して保存カメラへ復帰し、カーソルは非表示へ戻す（現行踏襲。遷移先のOnEnterが必要なら再表示する）
            // Leave FPS, restore the saved camera, and hide the cursor (existing behavior; the next state's OnEnter re-shows it if needed)
            if (CurrentMode == BuildViewMode.FirstPerson)
            {
                _applier.SetFirstPersonCamera(false);
                _applier.SetCrosshairVisible(false);
            }
            _applier.RestoreCamera(_savedCamera);
            _applier.SetCursorVisible(false);
            _isSessionActive = false;
        }
        
        // 建設系ステート中に毎フレーム呼ぶ（Vトグルと俯瞰時の右クリック回転）
        // Called every frame during build states (V toggle and top-down right-click rotation)
        public void ManualUpdate()
        {
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetKeyDown(KeyCode.V)) ToggleViewMode();
            
            if (CurrentMode != BuildViewMode.TopDown) return;
            
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                _applier.SetCursorVisible(false);
                _applier.SetCameraRotatable(true);
            }
            if (UnityEngine.Input.GetMouseButtonUp(1))
            {
                _applier.SetCursorVisible(true);
                _applier.SetCameraRotatable(false);
            }
        }
        
        public void ToggleViewMode()
        {
            CurrentMode = CurrentMode == BuildViewMode.TopDown ? BuildViewMode.FirstPerson : BuildViewMode.TopDown;
            AimPointProvider.SetMode(CurrentMode);
            
            // 俯瞰へ戻る際はFPSカメラを解除し、設置ステート以外は保存カメラへ戻す
            // When returning to top-down, leave the FPS camera; outside PlaceBlock restore the saved camera
            if (CurrentMode == BuildViewMode.TopDown)
            {
                _applier.SetFirstPersonCamera(false);
                if (_currentBuildState != UIStateEnum.PlaceBlock) _applier.RestoreCamera(_savedCamera);
            }
            
            ApplyForState(_currentBuildState);
        }
        
        private void ApplyForState(UIStateEnum state)
        {
            if (CurrentMode == BuildViewMode.FirstPerson)
            {
                // メニュー表示中はカーソルを解放しクロスヘアを消す
                // Free the cursor and hide the crosshair while the menu is open
                _applier.SetFirstPersonCamera(true);
                var isMenu = state == UIStateEnum.BuildMenu;
                _applier.SetCursorVisible(isMenu);
                _applier.SetCrosshairVisible(!isMenu);
            }
            else
            {
                _applier.SetCursorVisible(true);
                _applier.SetCrosshairVisible(false);
                if (state == UIStateEnum.PlaceBlock) _applier.ApplyTopDownCamera();
            }
        }
        
        private static bool IsBuildState(UIStateEnum state)
        {
            return state is UIStateEnum.BuildMenu or UIStateEnum.PlaceBlock or UIStateEnum.DeleteBar;
        }
    }
}
```

- [ ] **Step 5: BuildViewApplierを実装**

```csharp
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Crosshair;
using Client.Game.InGame.UI.UIState.Input;
using Client.Input;

namespace Client.Game.InGame.Control.BuildView
{
    /// <summary>
    ///     視点モードの副作用を実機（カメラ・カーソル・クロスヘア・自機）へ適用する
    ///     Applies view-mode side effects to the camera, cursor, crosshair, and player model
    /// </summary>
    public class BuildViewApplier : IBuildViewApplier
    {
        private readonly InGameCameraController _inGameCameraController;
        
        public BuildViewApplier(InGameCameraController inGameCameraController)
        {
            _inGameCameraController = inGameCameraController;
        }
        
        public TweenCameraInfo CaptureCurrentCamera()
        {
            return _inGameCameraController.CreateCurrentCameraTweenCameraInfo();
        }
        
        public void ApplyTopDownCamera()
        {
            _inGameCameraController.StartTweenCamera(_inGameCameraController.CreateTopDownTweenCameraInfo());
        }
        
        public void RestoreCamera(TweenCameraInfo saved)
        {
            _inGameCameraController.StartTweenCamera(saved);
        }
        
        public void SetFirstPersonCamera(bool enabled)
        {
            // カメラFPS化・常時視点回転・自機非表示を一括で切り替える
            // Toggle FPS camera, always-on look rotation, and player model visibility together
            _inGameCameraController.SetFirstPersonMode(enabled);
            _inGameCameraController.SetControllable(enabled);
            PlayerSystemContainer.Instance.PlayerObjectController.SetModelVisible(!enabled);
        }
        
        public void SetCursorVisible(bool visible)
        {
            InputManager.MouseCursorVisible(visible);
        }
        
        public void SetCrosshairVisible(bool visible)
        {
            CrosshairView.Instance.SetVisible(visible);
        }
        
        public void SetCameraRotatable(bool rotatable)
        {
            _inGameCameraController.SetControllable(rotatable);
        }
    }
}
```

- [ ] **Step 6: DI登録**

`MainGameStarter.cs`の`builder.Register<PlacementSelection>(Lifetime.Singleton);`の直後に追加（usingに`using Client.Game.InGame.Control.BuildView;`）:

```csharp
            // 建設系視点モード
            // register build view mode
            builder.Register<IBuildViewApplier, BuildViewApplier>(Lifetime.Singleton);
            builder.Register<BuildViewModeController>(Lifetime.Singleton);
```

- [ ] **Step 7: コンパイル＆テスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BuildView"`
Expected: 13件PASS（AimPointProviderTest 2件＋BuildViewModeControllerTest 11件）

- [ ] **Step 8: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BuildView moorestech_client/Assets/Scripts/Client.Tests/BuildView moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs
git commit -m "feat: BuildViewModeControllerで視点モードのセッション管理とVトグルを実装"
```

---

### Task 4: UIStateの統合とScreenClickableCameraController廃止

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/TweenCameraInfo.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/Input/ScreenClickableCameraController.cs`（＋.meta）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/InGameCameraController.cs`（using修正のみ）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/InGameCameraControllerExtension.cs`（using修正のみ）
- Modify: Task 3で作成した`IBuildViewApplier.cs` / `BuildViewApplier.cs` / `BuildViewModeController.cs` / `FakeBuildViewApplier.cs`（using修正のみ）

**Interfaces:**
- Consumes: Task 3の`BuildViewModeController.OnEnterBuildState(UIStateEnum)` / `OnLeaveBuildState(UIStateEnum)` / `ManualUpdate()`
- Produces: `TweenCameraInfo`（namespace `Client.Game.InGame.Control`へ移設、`public const float DefaultTweenDuration = 0.25f`を内包）。Shift+B挙動の削除。各建設系ステートの遷移リターンを`Leave(next)`ローカルヘルパーへ一本化（コントローラへの通知漏れ防止）

- [ ] **Step 1: TweenCameraInfoをControlへ移設**

`Client.Game/InGame/Control/TweenCameraInfo.cs`を新規作成:

```csharp
using UnityEngine;

namespace Client.Game.InGame.Control
{
    /// <summary>
    ///     カメラTweenの目標回転・距離・時間
    ///     Target rotation, distance, and duration for a camera tween
    /// </summary>
    public class TweenCameraInfo
    {
        public const float DefaultTweenDuration = 0.25f;
        
        public readonly Vector3 Rotation;
        public readonly float Distance;
        public readonly float TweenDuration;
        
        public TweenCameraInfo(Vector3 rotation, float distance, float tweenDuration = DefaultTweenDuration)
        {
            Rotation = rotation;
            Distance = distance;
            TweenDuration = tweenDuration;
        }
    }
}
```

（`tweenDuration`のデフォルト引数は既存コードの移設であり新規追加ではないため維持）

`ScreenClickableCameraController.cs`と`.meta`を削除:

```bash
rm moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/Input/ScreenClickableCameraController.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/Input/ScreenClickableCameraController.cs.meta
```

- [ ] **Step 2: PlaceBlockStateを書き換え（Shift+B廃止）**

全文置き換え:

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
        private readonly SkitManager _skitManager;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly BuildViewModeController _buildViewModeController;
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
            // カメラ・カーソルの適用はBuildViewModeControllerに委譲する
            // Camera and cursor handling is delegated to BuildViewModeController
            _buildViewModeController.OnEnterBuildState(UIStateEnum.PlaceBlock);
            
            // ここが重くなったら近いブロックだけプレビューをオンにするなどする
            foreach (var blockGameObject in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                blockGameObject.EnablePreviewOnlyObjects(true, true);
            }
            _blockPlacedDisposable.Add(_blockGameObjectDataStore.OnBlockPlaced.Subscribe(OnPlaceBlock));

            KeyControlDescription.Instance.SetText("Tab: ブロック選択\nV: 視点切替\nQ: 設置高さ上げる\nE: ブロック高さ下げる\nB: 配置モード終了\n左クリック: ブロック配置\nG:ブロック削除");
        }

        public UITransitContext GetNextUpdate()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return Leave(UIStateEnum.PlayerInventory);
            if (InputManager.UI.BlockDelete.GetKeyDown) return Leave(UIStateEnum.DeleteBar);
            if (_skitManager.IsPlayingSkit) return Leave(UIStateEnum.Story);
            // Tabでビルドメニューを開き直す
            // Reopen the build menu with Tab
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) return Leave(UIStateEnum.BuildMenu);
            //TODO InputSystemのリファクタ対象
            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.B)) return Leave(UIStateEnum.GameScreen);

            _buildViewModeController.ManualUpdate();
            _placeSystemStateController.ManualUpdate();
            
            return null;
        }
        
        // 遷移確定をコントローラへ通知してから遷移する（セッション終了判定はコントローラ側）
        // Notify the controller before transiting; it decides whether the session ends
        private UITransitContext Leave(UIStateEnum next)
        {
            _buildViewModeController.OnLeaveBuildState(next);
            return new UITransitContext(next);
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

- [ ] **Step 3: DeleteObjectStateを書き換え**

変更点のみ（他は現状維持。usingに`Client.Game.InGame.Control.BuildView`を追加、`Client.Game.InGame.Control`と`Client.Game.InGame.UI.UIState.Input`のusingは未使用になれば削除）:

```csharp
// ctor: InGameCameraControllerの代わりにBuildViewModeControllerを受け取り、ScreenClickableCameraController生成を削除
public DeleteObjectState(DeleteBarObject deleteBarObject, BuildViewModeController buildViewModeController, RailGraphClientCache cache)
{
    _buildViewModeController = buildViewModeController;
    _deleteBarObject = deleteBarObject;
    deleteBarObject.gameObject.SetActive(false);
}
```

フィールド: `private readonly ScreenClickableCameraController _screenClickableCameraController;` → `private readonly BuildViewModeController _buildViewModeController;`

`OnEnter`: `_screenClickableCameraController.OnEnter(false);` → `_buildViewModeController.OnEnterBuildState(UIStateEnum.DeleteBar);`（先頭で呼ぶ）。`KeyControlDescription`の文言に`V: 視点切替`を追加:

```csharp
KeyControlDescription.Instance.SetText("ドラッグ: まとめて選択\n離す: まとめて削除\nV: 視点切替\nESC: 選択キャンセル\nG: 破壊モード終了\nB: 設置モード\nTab: インベントリ");
```

`GetNextUpdate`: `_screenClickableCameraController.GetNextUpdate();` → `_buildViewModeController.ManualUpdate();`。`HandleTransition`内の3つの遷移リターンをすべて`Leave`経由へ:

```csharp
UITransitContext HandleTransition()
{
    // OpenMenu(ポーズ)もESCにbindされ、ここで拾うとESCの選択キャンセル/モード終了が死ぬため破壊モードでは扱わない
    // OpenMenu(pause) is also bound to ESC; handling it here would shadow ESC's cancel/exit, so skip it in destroy mode
    if (InputManager.UI.BlockDelete.GetKeyDown) return Leave(UIStateEnum.GameScreen);
    if (UnityEngine.Input.GetKeyDown(KeyCode.B)) return Leave(UIStateEnum.BuildMenu);
    if (InputManager.UI.OpenInventory.GetKeyDown) return Leave(UIStateEnum.PlayerInventory);

    // ESCはまず削除選択のキャンセルに使い、キャンセルする選択が無ければ破壊モードを抜ける
    // ESC is used first to cancel the delete selection; with nothing to cancel it leaves destroy mode
    if (InputManager.UI.CloseUI.GetKeyDown && !_deleteObjectService.TryCancelSelection())
    {
        return Leave(UIStateEnum.GameScreen);
    }
    return null;
}
```

クラスに`Leave`ヘルパーを追加（`#region Internal`の外、クラス直下）:

```csharp
// 遷移確定をコントローラへ通知してから遷移する（セッション終了判定はコントローラ側）
// Notify the controller before transiting; it decides whether the session ends
private UITransitContext Leave(UIStateEnum next)
{
    _buildViewModeController.OnLeaveBuildState(next);
    return new UITransitContext(next);
}
```

`OnExit`: `_screenClickableCameraController.OnExit();` の行を削除（カーソル・カメラ処理は`OnLeaveBuildState`が遷移前に実施済み）

- [ ] **Step 3.5: BuildMenuStateを書き換え**

ctorに`BuildViewModeController`を追加し、カーソル制御をコントローラへ移管（usingに`Client.Game.InGame.Control.BuildView`を追加。`Client.Input`のusingは`InputManager.UI`参照が残るため維持）:

```csharp
public BuildMenuState(BuildMenuView buildMenuView, PlacementSelection placementSelection, BuildViewModeController buildViewModeController)
{
    _buildMenuView = buildMenuView;
    _placementSelection = placementSelection;
    _buildViewModeController = buildViewModeController;
}

public void OnEnter(UITransitContext context)
{
    // カーソル表示はBuildViewModeControllerが適用する（FPS中もメニューではカーソル解放）
    // Cursor visibility is applied by BuildViewModeController (freed in the menu even during FPS)
    _buildViewModeController.OnEnterBuildState(UIStateEnum.BuildMenu);
    _buildMenuView.SetActive(true);
    KeyControlDescription.Instance.SetText("クリック: 設置ブロック選択  B: 閉じる");
}
```

`GetNextUpdate`の3つの遷移リターンを`Leave`経由へ（エントリ確定時は`return Leave(UIStateEnum.PlaceBlock);`、`B`/CloseUIは`return Leave(UIStateEnum.GameScreen);`、OpenInventoryは`return Leave(UIStateEnum.PlayerInventory);`）。`Leave`ヘルパーはPlaceBlockStateと同じ実装をクラス直下に追加。

`OnExit`: `InputManager.MouseCursorVisible(false);` の行を削除し`_buildMenuView.SetActive(false);`のみ残す（建設系内遷移では次ステートの`OnEnterBuildState`が、離脱時は`OnLeaveBuildState`がカーソルを設定するため）

- [ ] **Step 4: TweenCameraInfoのnamespace変更に伴うusing修正**

コンパイルエラー駆動で以下を修正:
- `InGameCameraController.cs` / `InGameCameraControllerExtension.cs`: `using Client.Game.InGame.UI.UIState.Input;` を削除（`TweenCameraInfo`は同じ`Client.Game.InGame.Control`namespaceになるためusing不要）
- `IBuildViewApplier.cs` / `BuildViewApplier.cs` / `BuildViewModeController.cs` / `FakeBuildViewApplier.cs`: `using Client.Game.InGame.UI.UIState.Input;` → `using Client.Game.InGame.Control;`（BuildView配下は親namespaceの`Client.Game.InGame.Control`を暗黙参照できるためusing削除でも可）

確認: `grep -rn "ScreenClickableCameraController" moorestech_client/Assets/Scripts --include="*.cs"` が0件であること。

- [ ] **Step 5: コンパイル＆全回帰**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BuildView|UIState|DragDelete|PlaceSystem"`
Expected: 全件PASS

- [ ] **Step 6: Commit**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "feat: 建設・削除ステートをBuildViewModeControllerへ統合しShift+Bを廃止"
```

---

### Task 5: シーンへのクロスヘア配置（uloop経由）

**Files:**
- Modify: MainGameシーン（`uloop execute-dynamic-code`経由のみ。テキスト編集禁止）

**Interfaces:**
- Consumes: Task 2の`CrosshairView`
- Produces: シーン内`CrosshairView`オブジェクト（`CrosshairView.Instance`が実行時に非null）

- [ ] **Step 1: 対象シーンを特定して開く**

Run: `find moorestech_client/Assets -name "*.unity" | grep -i main`
Expected: MainGameシーンのパスが出る（例: `Assets/Scenes/MainGame.unity`。複数出た場合は`MainGameStarter`が配置されているシーンを選ぶ）

`uloop execute-dynamic-code --project-path ./moorestech_client`で開く:

```csharp
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/<実際のパス>.unity");
return "opened";
```

- [ ] **Step 2: クロスヘアUIを生成してシーン保存**

`uloop execute-dynamic-code`で実行（KeyControlDescriptionと同じルートCanvas配下に置く）:

```csharp
var keyControl = UnityEngine.Object.FindObjectsByType<Client.Game.InGame.UI.KeyControl.KeyControlDescription>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)[0];
var canvas = keyControl.GetComponentInParent<UnityEngine.Canvas>(true).rootCanvas;

var root = new UnityEngine.GameObject("CrosshairView", typeof(UnityEngine.RectTransform));
root.transform.SetParent(canvas.transform, false);
var rootRect = root.GetComponent<UnityEngine.RectTransform>();
rootRect.anchorMin = new UnityEngine.Vector2(0.5f, 0.5f);
rootRect.anchorMax = new UnityEngine.Vector2(0.5f, 0.5f);
rootRect.anchoredPosition = UnityEngine.Vector2.zero;

var dot = new UnityEngine.GameObject("Dot", typeof(UnityEngine.RectTransform), typeof(UnityEngine.CanvasRenderer), typeof(UnityEngine.UI.Image));
dot.transform.SetParent(root.transform, false);
var image = dot.GetComponent<UnityEngine.UI.Image>();
image.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<UnityEngine.Sprite>("UI/Skin/Knob.psd");
image.raycastTarget = false;
dot.GetComponent<UnityEngine.RectTransform>().sizeDelta = new UnityEngine.Vector2(8, 8);

var view = root.AddComponent<Client.Game.InGame.UI.Crosshair.CrosshairView>();
var so = new UnityEditor.SerializedObject(view);
so.FindProperty("dotObject").objectReferenceValue = dot;
so.ApplyModifiedProperties();

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
return "crosshair created in " + root.scene.name;
```

注意: `image.raycastTarget = false`は必須。trueだと画面中央ロックカーソルが常にUI上と判定され、`EventSystem.current.IsPointerOverGameObject()`（`CommonBlockPlaceSystem`/`DeleteObjectService`）が設置・削除クリックを弾く。

- [ ] **Step 3: 配置確認**

Run: `uloop find-game-objects --project-path ./moorestech_client --name "CrosshairView"`
Expected: 1件ヒット、子に`Dot`

- [ ] **Step 4: Commit**

```bash
git add -A moorestech_client/Assets
git commit -m "feat: MainGameシーンにクロスヘアUIを配置"
```

---

### Task 6: E2E検証（プレイテストDSL）

**Files:**
- なし（検証のみ。問題発見時は該当タスクの実装を修正してコミット）

**Interfaces:**
- Consumes: Task 1〜5の全成果物
- Produces: FPS建設の動作エビデンス（録画/スクリーンショット）と回帰確認

- [ ] **Step 1: unity-playmode-recorded-playtestスキルを起動**

`unity-playmode-recorded-playtest`スキルの手順に従い、プレイテストDSLで以下のシナリオを実行する:

1. ゲーム起動 → `B`でビルドメニュー → ブロック選択 → PlaceBlock遷移（俯瞰Tweenを確認）
2. `V`キー入力を注入（InputSystem QueueStateEvent。OSレベル入力は使わない）→ FPS化を確認:
   - カメラ距離が0.15付近（`uloop execute-dynamic-code`でCinemachineFramingTransposerの`m_CameraDistance`をダンプ）
   - `AimPointProvider.CurrentMode == FirstPerson`
   - クロスヘア`Dot`がアクティブ、カーソルロック（`Cursor.lockState == Locked`）
   - 自機Rendererが`enabled == false`
3. 画面中央照準でブロック設置（左クリック注入）→ サーバー側にブロックが存在することを確認
4. `V`で俯瞰へ戻し、マウスカーソル照準で設置できることを確認（回帰）
5. `G`で削除モードへ遷移 → FPS/俯瞰の記憶が維持されることを確認
6. `B`で建設モード終了 → 三人称カメラ復帰・自機再表示・クロスヘア非表示を確認

- [ ] **Step 2: エラーログ確認**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 新規エラーなし（`MooresmasterLoaderException`等が出た場合はスキルのworktreeピン留め手順を確認）

- [ ] **Step 3: 全体回帰**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BuildView|UIState|DragDelete|PlaceSystem"`
Expected: 全件PASS

- [ ] **Step 4: 最終コミット（修正が発生した場合）**

```bash
git status
# 変更があれば
git add -A
git commit -m "fix: FPS建設モードのE2E検証で発見した問題を修正"
```

---

## 既知のチューニングポイント（実装後に実機で調整）

- `FirstPersonCameraDistance = 0.15f` / `FirstPersonTrackedOffset = (0, 1.6, 0)`: 頭部位置の見え方次第で調整（`InGameCameraController`の定数）
- FPS解除→復帰Tween中、`Update()`の距離クランプ（最小0.6）が働くため0.15→0.6へ僅かにスナップする。目視で気になる場合のみ対処
- カーソルロック中のツールチップ（`MouseCursorTooltip`）は画面中央付近に表示される。実害があればFPS中の表示位置を後続改善
