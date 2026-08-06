# 通常モードTPSの左Alt自由カーソルとスポイト Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 通常モード（GameScreen）の三人称視点で左Altを押している間だけマウスカーソルを解放し、画面上の任意の位置にある設置物をミドルクリックでスポイトできるようにする。

**Architecture:** 照準点を返す `AimPointProvider` を「視点モード」＋「三人称時の照準ソース」の2入力へ拡張し、照準ソースの判断は基盤側で行わず `UiStateCameraPolicyService`（UIステート滞在中のカーソル/回転ポリシーの単一所有者）がゾーンごとにプッシュする。同サービスへGameplayゾーンの修飾入力として左Altホールドを足し、Buildゾーンの右ドラッグ（`UpdateRotationInput`）と同形にする。

**Tech Stack:** Unity 2022 / C# / UniRx / VContainer / Unity Input System（`InputTestFixture`）/ プレイテストDSL（`Client.Playtest`）

## Requirements

設計の裁定は `docs/adr/0008-aim-source-pushed-by-ui-state.md` と `.decisions/2026-08-05-*.md` が正。以下は受け入れ基準つきの要件列挙。

1. 通常モードの三人称で左Altを押している間、マウスカーソルが解放されカメラ回転が止まる — 受け入れ: `UiStateCameraPolicyService` がAlt押下で `CameraInteractionMode.PointerFree`、離すと `CameraLook` を applier へプッシュする
2. 左Altはホールド判定であり、トグルではない — 受け入: Alt押下→離すで元の状態へ戻り、押しっぱなしでない限り自由カーソルにならない
3. 左Altが解放するのは照準そのもの。Alt中はスポイト・ブロックを開く・採掘のすべてがカーソル位置を照準にする — 受け入: Alt中に `AimPointProvider.GetCurrentMode()` が `Mouse` を返す
4. 一人称では左Altを受け付けない — 受け入: 一人称で左Altを押しても applier へ何もプッシュされない
5. 左Alt非押下中の三人称照準は明示的に画面中央 — 受け入: `AimPointProvider.GetCurrentMode()` が `ScreenCenter` を返し、マウス座標を動かしても照準点が画面中央から動かない
6. 設置・破壊モードの三人称照準は右ドラッグ中も含め常にカーソル位置 — 受け入: `EnterBuildMode()` 後、右ドラッグ中でも `GetCurrentMode()` が `Mouse` のまま
7. 左Alt押下の瞬間にカーソルを画面中央へワープする — 受け入: Alt押下で applier の `WarpCursorToScreenCenter()` が1回呼ばれる
8. 修飾キーは左Altのみ — 受け入: `HybridInput` の KeyCode→Key マップに `LeftAlt` が入り、右Altは対象外
9. Alt中に視点を切り替えたらホールドは破棄される — 受け入: Alt保持中にV切替すると `CameraLook` へ戻り、押し直すまで自由カーソルにならない
10. `GameScreenState` は毎フレームこの入力更新をサービスへ委譲する — 受け入: `GetNextUpdate()` の先頭で `UpdateGameplayFreeCursorInput()` を呼ぶ
11. 通常モードの操作説明に左Altの行が出る — 受け入: `KeyControlDescription` の GameScreen 文言に左Altの説明が含まれる
12. 既存のプレイ録画シナリオ2本が新挙動で通る — 受け入: `block-eyedropper-via-ui` と `placement-pick-wire-and-train-via-ui` がAltホールド付きで成功する
13. 「視点を動かさずAlt＋ミドルクリックで画面中央外のブロックをスポイトできる」が通しで検証される — 受け入: プレイ録画シナリオに当該確認項目が追加され成功する
14. Alt無しでは画面中央外の設置物がスポイトされない — 受け入: 同シナリオにネガティブ確認項目が追加され成功する

**やらないこと（スコープ境界）**

- クロスヘアの表示条件は変えない（一人称限定のまま。`CrosshairView` に触らない）
- Alt中のUIヒットテストをスキップする分岐は入れない（HUD上ではスポイト不成立のまま。`PlacementTargetPickService` に触らない）
- Menuゾーン（インベントリ・ポーズ等）の挙動は変えない
- 右Altは対象にしない
- Alt+TabがOSに奪われる件への対策は入れない

## Global Constraints

- コメントは日本語1行→英語1行の2行セットを約3〜10行ごとに挿入する。日本語・英語それぞれ必ず1行に収める。日本語本文の長さ目安は処理・変数20字、メソッド30字
- 1ファイル200行以下。`partial` 禁止。`Func<>` 禁止。try-catch 禁止（外部境界を除く）
- 単純なgetter/setterプロパティ禁止。値のSetは `public void SetHoge` メソッドで行う
- `#region Internal` はメソッド内ローカル関数をまとめる用途に限定。クラス直下のprivateメソッド群を囲うのは禁止
- イベント発火に `Action` を使わない（UniRx を使う）
- `.meta` ファイルは絶対に手動作成しない（Unity自動生成）
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "正規表現"` で対象を限定して実行する
- 作業ブランチは `fix/fps-build-mode-camera`。着手前に `pwd` と `git branch --show-current` で確認する

---

### Task 1: AimPointProvider を視点＋照準ソースの2入力にする

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/ViewMode/AimPointProvider.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/ViewMode/AimPointProviderTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Mining/MapObjectMiningAimTest.cs`

**Interfaces:**
- Consumes: `PlayerViewMode`（既存 enum。`ThirdPerson` / `FirstPerson`）
- Produces:
  - `enum ThirdPersonAimSource { ScreenCenter, Cursor }`（namespace `Client.Game.InGame.Control.ViewMode`）
  - `static void AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource aimSource)`
  - 既存の `static void AimPointProvider.SetViewMode(PlayerViewMode viewMode)` / `static AimPointMode AimPointProvider.GetCurrentMode()` / `static Vector3 AimPointProvider.GetAimScreenPoint()` はシグネチャ据え置き

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/ViewMode/AimPointProviderTest.cs` を丸ごと次で置き換える。

```csharp
using Client.Game.InGame.Control.ViewMode;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.ViewMode
{
    public class AimPointProviderTest
    {
        [TearDown]
        public void TearDown()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.ScreenCenter);
        }

        [Test]
        public void ScreenCenterModeReturnsScreenCenter()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            var point = AimPointProvider.GetAimScreenPoint();
            Assert.AreEqual(Screen.width / 2f, point.x);
            Assert.AreEqual(Screen.height / 2f, point.y);
        }

        [Test]
        public void FirstPersonUsesScreenCenterAim()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.GetCurrentMode());
        }

        [Test]
        public void FirstPersonIgnoresCursorAimSource()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.Cursor);
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.GetCurrentMode());
        }

        [Test]
        public void ThirdPersonWithScreenCenterSourceUsesScreenCenter()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.ScreenCenter);
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.GetCurrentMode());
        }

        [Test]
        public void ThirdPersonWithCursorSourceUsesMouseAim()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.Cursor);
            Assert.AreEqual(AimPointMode.Mouse, AimPointProvider.GetCurrentMode());
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "AimPointProviderTest"`
Expected: コンパイルエラー（`ThirdPersonAimSource` / `SetThirdPersonAimSource` が存在しない）

- [ ] **Step 3: AimPointProvider を実装する**

`moorestech_client/Assets/Scripts/Client.Game/InGame/Control/ViewMode/AimPointProvider.cs` を丸ごと次で置き換える。

```csharp
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.Control.ViewMode
{
    public enum AimPointMode
    {
        Mouse,
        ScreenCenter,
    }

    /// <summary>
    ///     三人称のとき照準をどこから取るか
    ///     Where the aim point comes from in third person
    /// </summary>
    public enum ThirdPersonAimSource
    {
        ScreenCenter,
        Cursor,
    }

    /// <summary>
    ///     設置・削除・操作用の照準座標を視点モードと照準ソースから解決する
    ///     Resolves aim points for placement, deletion, and interaction from the view mode and aim source
    /// </summary>
    public static class AimPointProvider
    {
        private static PlayerViewMode _viewMode = PlayerViewMode.ThirdPerson;
        private static ThirdPersonAimSource _thirdPersonAimSource = ThirdPersonAimSource.ScreenCenter;

        public static void SetViewMode(PlayerViewMode viewMode)
        {
            _viewMode = viewMode;
        }

        // 三人称の照準ソースはUIステート側のポリシー所有者がプッシュする
        // The aim source in third person is pushed by the UI-state-side policy owner
        public static void SetThirdPersonAimSource(ThirdPersonAimSource aimSource)
        {
            _thirdPersonAimSource = aimSource;
        }

        public static AimPointMode GetCurrentMode()
        {
            // 一人称は照準ソースに関わらず常に画面中央
            // First person always aims at the screen center regardless of the aim source
            if (_viewMode == PlayerViewMode.FirstPerson) return AimPointMode.ScreenCenter;

            return _thirdPersonAimSource == ThirdPersonAimSource.Cursor ? AimPointMode.Mouse : AimPointMode.ScreenCenter;
        }

        public static Vector3 GetAimScreenPoint()
        {
            if (GetCurrentMode() == AimPointMode.ScreenCenter) return new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);

            return HybridInput.GetMousePosition();
        }
    }
}
```

- [ ] **Step 4: 既存の採掘照準テストを新既定値へ追従させる**

`moorestech_client/Assets/Scripts/Client.Tests/Mining/MapObjectMiningAimTest.cs` の2箇所を修正する。

TearDown 内（既存の `AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);` の直後）へ1行追加:

```csharp
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.ScreenCenter);
```

`MiningUpdateUsesConfiguredMouseAndCenterAim` 内の三人称アサート直前へ1行追加:

```csharp
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.Cursor);
            Assert.AreEqual(mousePoint, (Vector2)AimPointProvider.GetAimScreenPoint());
```

- [ ] **Step 5: コンパイルしてテストが通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "AimPointProviderTest|MapObjectMiningAimTest"`
Expected: 全PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Control/ViewMode/AimPointProvider.cs moorestech_client/Assets/Scripts/Client.Tests/ViewMode/AimPointProviderTest.cs moorestech_client/Assets/Scripts/Client.Tests/Mining/MapObjectMiningAimTest.cs
git commit -m "AimPointProviderを視点と照準ソースの2入力にする"
```

---

### Task 2: カーソルワープと左Alt読み取りの土台を足す

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Input/InputManager.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Input/HybridInput.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/IPlayerCameraInteractionApplier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/PlayerCameraInteractionApplier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/FakePlayerCameraInteractionApplier.cs`

**Interfaces:**
- Produces:
  - `static void InputManager.WarpMouseCursorToScreenCenter()`
  - `static bool HybridInput.GetKeyUp(KeyCode keyCode)`
  - `HybridInput` の KeyCode→Key マップに `KeyCode.LeftAlt => Key.LeftAlt` が追加される
  - `IPlayerCameraInteractionApplier.WarpCursorToScreenCenter()`（既存の `SetInteractionMode(CameraInteractionMode)` はそのまま）
  - `FakePlayerCameraInteractionApplier.Calls` に `"Warp"` が記録される

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/UIState/FakePlayerCameraInteractionApplier.cs` を丸ごと次で置き換える。

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Control;

namespace Client.Tests.UIState
{
    public class FakePlayerCameraInteractionApplier : IPlayerCameraInteractionApplier
    {
        public readonly List<string> Calls = new();

        public void SetInteractionMode(CameraInteractionMode mode)
        {
            Calls.Add($"Mode:{mode}");
        }

        public void WarpCursorToScreenCenter()
        {
            Calls.Add("Warp");
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: コンパイルエラー（`IPlayerCameraInteractionApplier` に `WarpCursorToScreenCenter` が存在しない）

- [ ] **Step 3: InputManager にカーソルワープを足す**

`moorestech_client/Assets/Scripts/Client.Input/InputManager.cs` の `MouseCursorVisible` メソッド直後へ次を追加する。

```csharp
        // ロック解除直後のカーソル出現位置はOS任せのため明示的に画面中央へ寄せる
        // The unlocked cursor appears wherever the OS left it, so pull it to the screen center explicitly
        public static void WarpMouseCursorToScreenCenter()
        {
            if (Mouse.current == null) return;
            Mouse.current.WarpCursorPosition(new Vector2(Screen.width / 2f, Screen.height / 2f));
        }
```

- [ ] **Step 4: HybridInput に左Altと GetKeyUp を足す**

`moorestech_client/Assets/Scripts/Client.Input/HybridInput.cs` の `GetKey` メソッド直後へ次を追加する。

```csharp
        public static bool GetKeyUp(KeyCode keyCode)
        {
            var key = ToInputSystemKey(keyCode);
            var released = key.HasValue && Keyboard.current != null
                ? Keyboard.current[key.Value].wasReleasedThisFrame
                : UnityEngine.Input.GetKeyUp(keyCode);

            // 離した通知は抑止しない。抑止するとホールド系の修飾キーが押しっぱなし状態で固着する
            // Release notifications are never suppressed; suppressing one would strand a held modifier key
            return released;
        }
```

同ファイルの `ToInputSystemKey` の switch へ1行追加する（`KeyCode.LeftControl => Key.LeftCtrl,` の直前に置く）。

```csharp
                KeyCode.LeftAlt => Key.LeftAlt,
```

- [ ] **Step 5: applier 契約へワープを足す**

`moorestech_client/Assets/Scripts/Client.Game/InGame/Control/IPlayerCameraInteractionApplier.cs` を丸ごと次で置き換える。

```csharp
namespace Client.Game.InGame.Control
{
    public interface IPlayerCameraInteractionApplier
    {
        void SetInteractionMode(CameraInteractionMode mode);

        // ワープは状態でなく1回限りの動作のためモード契約とは別メソッドにする
        // Warping is a one-shot action rather than a state, so it stays outside the mode contract
        void WarpCursorToScreenCenter();
    }
}
```

`moorestech_client/Assets/Scripts/Client.Game/InGame/Control/PlayerCameraInteractionApplier.cs` の `SetInteractionMode` 直後へ次を追加する。

```csharp
        public void WarpCursorToScreenCenter()
        {
            InputManager.WarpMouseCursorToScreenCenter();
        }
```

- [ ] **Step 6: コンパイルして既存テストが通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UiStateCameraPolicyServiceTest|UIStateCameraInteractionTest|UIStateFocusRestorationTest"`
Expected: 全PASS（既存アサートは `Mode:` 列のみを見ており `Warp` はまだ発生しない）

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Input/InputManager.cs moorestech_client/Assets/Scripts/Client.Input/HybridInput.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Control/IPlayerCameraInteractionApplier.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Control/PlayerCameraInteractionApplier.cs moorestech_client/Assets/Scripts/Client.Tests/UIState/FakePlayerCameraInteractionApplier.cs
git commit -m "カーソルワープと左Alt読み取りの入力土台を足す"
```

---

### Task 3: UiStateCameraPolicyService に Gameplay の左Altホールドと照準ソース push を足す

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/CameraPolicy/UiStateCameraPolicyService.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/CameraPolicy/UiStateCameraPolicyServiceTest.cs`

**Interfaces:**
- Consumes: `AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource)`（Task 1）/ `IPlayerCameraInteractionApplier.WarpCursorToScreenCenter()`（Task 2）/ `HybridInput.GetKeyDown(KeyCode)` `HybridInput.GetKeyUp(KeyCode)`（Task 2）
- Produces: `void UiStateCameraPolicyService.UpdateGameplayFreeCursorInput()`。既存の `EnterGameplay()` / `EnterMenu()` / `EnterBuildMode()` / `UpdateRotationInput()` / `ExitToNeutral()` / `RestoreAfterApplicationFocus()` はシグネチャ据え置き

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/UIState/CameraPolicy/UiStateCameraPolicyServiceTest.cs` の `Setup` にキーボードを足し、`TearDown` を新設し、テストを追加する。

まず `using` に次の2行が含まれているようにする。

```csharp
using Client.Game.InGame.Control.ViewMode;
using UnityEngine;
```

`Setup` を次で置き換える（`_keyboard` フィールド宣言を `private Mouse _mouse;` の直後へ足す）。

```csharp
        private Mouse _mouse;
        private Keyboard _keyboard;
```

```csharp
        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _applier = new FakePlayerCameraInteractionApplier();
            _viewModeController = new PlayerViewModeController(new FakePlayerViewApplier());
            _service = new UiStateCameraPolicyService(_applier, _viewModeController);
        }

        public override void TearDown()
        {
            // 照準ソースは静的なためテスト間で持ち越さない
            // The aim source is static, so never carry it across tests
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.ScreenCenter);
            base.TearDown();
        }
```

クラス末尾（`ExitToNeutralFreesPointerAndRestoreReappliesZone` の後）へ次のテストを追加する。

```csharp
        [Test]
        public void GameplayZoneTpsFreesPointerWhileLeftAltHeld()
        {
            _service.EnterGameplay();

            _applier.Calls.Clear();
            Press(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            CollectionAssert.AreEqual(new[] { "Warp", "Mode:PointerFree" }, _applier.Calls);

            _applier.Calls.Clear();
            Release(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);
        }

        [Test]
        public void GameplayZoneAltHoldSwitchesAimSourceToCursor()
        {
            _service.EnterGameplay();
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.GetCurrentMode());

            Press(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            Assert.AreEqual(AimPointMode.Mouse, AimPointProvider.GetCurrentMode());

            Release(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.GetCurrentMode());
        }

        [Test]
        public void GameplayZoneFpsIgnoresLeftAlt()
        {
            _viewModeController.ToggleViewMode();
            _service.EnterGameplay();

            _applier.Calls.Clear();
            Press(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            Release(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            CollectionAssert.IsEmpty(_applier.Calls);
        }

        [Test]
        public void GameplayViewToggleDiscardsAltHold()
        {
            _service.EnterGameplay();
            Press(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();

            // 視点切替でホールドを破棄し、押し直すまで自由カーソルにならない
            // A view toggle discards the hold; the cursor stays locked until Alt is pressed again
            _applier.Calls.Clear();
            _viewModeController.ToggleViewMode();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.GetCurrentMode());
        }

        [Test]
        public void ExitToNeutralClearsAltHold()
        {
            _service.EnterGameplay();
            Press(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();

            _service.ExitToNeutral();

            _applier.Calls.Clear();
            _service.RestoreAfterApplicationFocus();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);
        }

        [Test]
        public void BuildZoneKeepsCursorAimSourceDuringRightDrag()
        {
            _service.EnterBuildMode();
            Assert.AreEqual(AimPointMode.Mouse, AimPointProvider.GetCurrentMode());

            // 右ドラッグで回転しても照準はカーソルのまま（プレビューが画面中央へ跳ねない）
            // The aim stays on the cursor even while right-drag rotates, so the preview never jumps to the center
            Press(_mouse.rightButton);
            _service.UpdateRotationInput();
            Assert.AreEqual(AimPointMode.Mouse, AimPointProvider.GetCurrentMode());
        }

        [Test]
        public void MenuZoneCentersAimSource()
        {
            _service.EnterBuildMode();
            _service.EnterMenu();
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.GetCurrentMode());
        }
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UiStateCameraPolicyServiceTest"`
Expected: コンパイルエラー（`UpdateGameplayFreeCursorInput` が存在しない）

- [ ] **Step 3: サービスを実装する**

`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/CameraPolicy/UiStateCameraPolicyService.cs` を丸ごと次で置き換える。

```csharp
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using Client.Input;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.CameraPolicy
{
    /// <summary>
    ///     UIステート滞在中のカーソル/回転ポリシーと三人称照準ソースの単一所有者。
    ///     Gameplay:常時回転（左Alt押下中だけ自由カーソル）／Menu:自由カーソル／Build:視点別。
    ///     Single owner of the cursor/rotation policy and the third-person aim source while staying in UI states.
    ///     Gameplay always rotates except while left Alt is held; Menu frees the cursor; Build follows the view mode.
    /// </summary>
    public class UiStateCameraPolicyService
    {
        private readonly IPlayerCameraInteractionApplier _cameraInteractionApplier;
        private readonly PlayerViewModeController _viewModeController;
        private PolicyZone _currentZone = PolicyZone.Menu;
        private bool _isFirstPerson;
        private bool _isGameplayFreeCursorHeld;

        public UiStateCameraPolicyService(IPlayerCameraInteractionApplier cameraInteractionApplier, PlayerViewModeController viewModeController)
        {
            _cameraInteractionApplier = cameraInteractionApplier;
            _viewModeController = viewModeController;

            // 常設購読でゾーン別に視点切替へ追従する（アプリ寿命Singletonのため破棄しない）
            // Permanent subscription that follows view toggles per zone (never disposed: app-lifetime singleton)
            viewModeController.OnViewModeChanged.Subscribe(OnViewModeChanged);
        }

        public void EnterGameplay()
        {
            _currentZone = PolicyZone.Gameplay;
            _isGameplayFreeCursorHeld = false;
            ApplyZonePolicy();
        }

        public void EnterMenu()
        {
            _currentZone = PolicyZone.Menu;
            _isGameplayFreeCursorHeld = false;
            ApplyZonePolicy();
        }

        public void EnterBuildMode()
        {
            _currentZone = PolicyZone.Build;
            _isGameplayFreeCursorHeld = false;
            ApplyZonePolicy();
        }

        public void UpdateRotationInput()
        {
            // FPSは常時回転のため右ドラッグ切替はTPS限定
            // FPS always rotates, so right-drag toggling is TPS-only
            if (_isFirstPerson) return;

            if (HybridInput.GetMouseButtonDown(1)) _cameraInteractionApplier.SetInteractionMode(CameraInteractionMode.CameraLook);
            if (HybridInput.GetMouseButtonUp(1)) _cameraInteractionApplier.SetInteractionMode(CameraInteractionMode.PointerFree);
        }

        public void UpdateGameplayFreeCursorInput()
        {
            // FPSは画面中央照準で一貫させるため左Altを受け付けない
            // FPS keeps its screen-center aim, so the left Alt hold is ignored there
            if (_isFirstPerson) return;

            if (HybridInput.GetKeyDown(KeyCode.LeftAlt))
            {
                // 解放の瞬間に中央へ寄せ、非押下中の画面中央照準と連続させる
                // Warp to the center on release so the aim continues from the screen-center aim
                _cameraInteractionApplier.WarpCursorToScreenCenter();
                _isGameplayFreeCursorHeld = true;
                ApplyZonePolicy();
            }

            if (!HybridInput.GetKeyUp(KeyCode.LeftAlt)) return;
            _isGameplayFreeCursorHeld = false;
            ApplyZonePolicy();
        }

        public void ExitToNeutral()
        {
            // 退出時は自由カーソルへ戻し、ポリシーを押さない次のUIが背後の回転を継承しないようにする
            // Exit returns to a free cursor so UIs that push no policy never inherit background rotation
            _isGameplayFreeCursorHeld = false;
            _cameraInteractionApplier.SetInteractionMode(CameraInteractionMode.PointerFree);
        }

        public void RestoreAfterApplicationFocus()
        {
            // フォーカス復帰は進行中の右ドラッグとAltホールドを破棄して現ゾーンのポリシーへ戻す
            // Focus restore discards any in-progress right-drag or Alt hold and reapplies the current zone policy
            _isGameplayFreeCursorHeld = false;
            ApplyZonePolicy();
        }

        private void OnViewModeChanged(PlayerViewMode mode)
        {
            // 判定に使う視点はどのゾーンでも即時同期する（同期漏れはFPSでAltを受け付ける穴になる）
            // Sync the view used for decisions in every zone; a missed sync would let FPS accept the Alt hold
            _isFirstPerson = mode == PlayerViewMode.FirstPerson;

            // Gameplayの左Altホールドは視点切替で破棄する（押し直しで復帰。右ドラッグと同じ扱い）
            // A view toggle discards the gameplay Alt hold; press again to resume, same as the right-drag case
            if (_currentZone == PolicyZone.Gameplay && _isGameplayFreeCursorHeld)
            {
                _isGameplayFreeCursorHeld = false;
                ApplyZonePolicy();
                return;
            }

            if (_currentZone != PolicyZone.Build) return;
            ApplyZonePolicy();
        }

        private void ApplyZonePolicy()
        {
            _isFirstPerson = _viewModeController.GetCurrentMode() == PlayerViewMode.FirstPerson;

            var cameraLook = (_currentZone == PolicyZone.Gameplay && !_isGameplayFreeCursorHeld)
                || (_currentZone == PolicyZone.Build && _isFirstPerson);
            _cameraInteractionApplier.SetInteractionMode(cameraLook ? CameraInteractionMode.CameraLook : CameraInteractionMode.PointerFree);
            AimPointProvider.SetThirdPersonAimSource(ResolveAimSource());

            #region Internal

            ThirdPersonAimSource ResolveAimSource()
            {
                // Buildは右ドラッグ中もカーソル照準を保ち、プレビューが画面中央へ跳ねないようにする
                // Build keeps the cursor aim even mid right-drag so the preview never jumps to the screen center
                if (_currentZone == PolicyZone.Build) return ThirdPersonAimSource.Cursor;

                // Gameplayは左Alt押下中のみカーソル。Menuは照準を使わないため中央固定
                // Gameplay uses the cursor only while left Alt is held; Menu never aims, so it stays centered
                return _currentZone == PolicyZone.Gameplay && _isGameplayFreeCursorHeld ? ThirdPersonAimSource.Cursor : ThirdPersonAimSource.ScreenCenter;
            }

            #endregion
        }

        private enum PolicyZone
        {
            Gameplay,
            Menu,
            Build,
        }
    }
}
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UiStateCameraPolicyServiceTest"`
Expected: 全PASS（既存4テスト＋新規7テスト）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/CameraPolicy/UiStateCameraPolicyService.cs moorestech_client/Assets/Scripts/Client.Tests/UIState/CameraPolicy/UiStateCameraPolicyServiceTest.cs
git commit -m "Gameplayゾーンの左Altホールドと三人称照準ソースのpushを足す"
```

---

### Task 4: GameScreenState から左Alt入力を委譲し操作説明を更新する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateCameraInteractionTest.cs`

**Interfaces:**
- Consumes: `UiStateCameraPolicyService.UpdateGameplayFreeCursorInput()`（Task 3）
- Produces: なし（既存の `IUIState` 実装のまま）

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateCameraInteractionTest.cs` へ次のテストとヘルパを追加する。`using` に `using Client.Game.InGame.UI.UIState.State.SubInventory;` と `using Client.Game.InGame.Train.Unit;` が無ければ足す。

```csharp
        [Test]
        public void GameScreenDelegatesLeftAltFreeCursorToPolicyService()
        {
            SetUpGameStateController();
            var applier = new FakePlayerCameraInteractionApplier();
            var state = CreateGameScreenState(applier);
            state.OnEnter(new UITransitContext(UIStateEnum.GameScreen));

            // 左Alt押下がGetNextUpdateからサービスへ届いていることだけを確認する
            // Verify only that the left Alt press reaches the service from GetNextUpdate
            applier.Calls.Clear();
            Press(_keyboard.leftAltKey);
            state.GetNextUpdate();
            CollectionAssert.AreEqual(new[] { "Warp", "Mode:PointerFree" }, applier.Calls);

            applier.Calls.Clear();
            Release(_keyboard.leftAltKey);
            state.GetNextUpdate();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, applier.Calls);
        }

        private GameScreenState CreateGameScreenState(FakePlayerCameraInteractionApplier applier)
        {
            // 各サービスは入力が無ければ即falseで返るため、依存はnullのままで足りる
            // Each service returns false immediately without input, so null dependencies are enough here
            var skitManager = (SkitManager)FormatterServices.GetUninitializedObject(typeof(SkitManager));
            return new GameScreenState(
                skitManager,
                new GameScreenSubInventoryInteractService(null),
                new RideVehicleInputService(),
                new PlacementTargetPickService(null),
                CreateCameraPolicy(applier));
        }
```

`Setup` にキーボードを足す（`private Mouse _mouse;` の直後に `private Keyboard _keyboard;` を宣言し、`_mouse = InputSystem.AddDevice<Mouse>();` の直後へ次を追加）。

```csharp
            _keyboard = InputSystem.AddDevice<Keyboard>();
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UIStateCameraInteractionTest"`
Expected: `GameScreenDelegatesLeftAltFreeCursorToPolicyService` が FAIL（`applier.Calls` が空）

- [ ] **Step 3: GameScreenState から委譲する**

`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs` の `GetNextUpdate()` の先頭へ次を挿入する（`if (InputManager.UI.OpenInventory.GetKeyDown)` の行より前）。

```csharp
        public UITransitContext GetNextUpdate()
        {
            // 左Alt押下中だけカーソルを解放し、照準をカーソル位置へ移す
            // Free the cursor and move the aim onto it only while left Alt is held
            _cameraPolicyService.UpdateGameplayFreeCursorInput();

            if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);
```

- [ ] **Step 4: 操作説明へ左Altの行を足す**

同ファイルの `OnEnter` にある `KeyControlDescription.Instance.SetText(...)` を次で置き換える。

```csharp
            KeyControlDescription.Instance.SetText("Tab: インベントリ\n1~9: アイテム持ち替え\nV: 視点切替\nB: ブロック配置\nG:ブロック削除\n左Alt長押し: カーソル解放\nミドルクリック: 設置物をスポイト\nT: チャレンジ一覧\nR: リサーチツリー\nF3: デバッグモード\n");
```

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UIStateCameraInteractionTest|UIStateFocusRestorationTest"`
Expected: 全PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateCameraInteractionTest.cs
git commit -m "GameScreenStateから左Alt自由カーソルを委譲し操作説明を更新する"
```

---

### Task 5: プレイ録画シナリオを実操作忠実にし、通しで検証する

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/block-eyedropper-via-ui.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/placement-pick-wire-and-train-via-ui.cs`

**Interfaces:**
- Consumes: 実装済みの左Altホールド挙動（Task 3・Task 4）
- Produces: なし（シナリオはテスト資産）

**背景（実装者向け）:** これまでこの2本は通常モード中に `p.AimAt(...)` でマウス絶対座標を注入してスポイトしていた。`SemanticInput` の注入は `Mouse.current.position` を直接書き換えるためカーソルロックを迂回しており、実プレイヤーには不可能な操作を検証して通っていた。Task 1 で非Alt時の照準が画面中央固定になるため、Altホールドを入れないとこの2本は落ちる。

- [ ] **Step 1: 共通のAltホールドヘルパを block-eyedropper-via-ui へ足す**

`.agents/skills/unity-playmode-recorded-playtest/scenarios/building/block-eyedropper-via-ui.cs` の `using` に次を足す。

```csharp
using UnityEngine.InputSystem;
```

同ファイルの `MiddleClickAsync` 定義の直後へ次のローカル関数を追加する。

```csharp
    // 通常モードは左Alt押下中だけ照準がカーソルへ移るため、狙って押すまでを1組で行う
    // In gameplay the aim follows the cursor only while left Alt is held, so aim and click as one unit
    async UniTask PickWithAltHoldAsync(Vector3 worldPosition)
    {
        SemanticInput.KeyDown(Key.LeftAlt);
        await UniTask.DelayFrame(2);
        await p.AimAt(worldPosition);
        await MiddleClickAsync();
        SemanticInput.KeyUp(Key.LeftAlt);
        await UniTask.DelayFrame(3);
    }
```

- [ ] **Step 2: 通常モード中のピックをAltホールド版へ差し替える**

同ファイルの確認項目1（`posA`）のブロックを次で置き換える。

```csharp
    // 確認項目1: GameScreen中に左Altを押しながらNorth向きチェストをピックし、PlaceBlock遷移とCurrentTarget反映を検証する
    // Check item 1: hold left Alt and pick a North-facing chest during GameScreen, then verify the transition and CurrentTarget
    p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, "初期状態はGameScreen");
    p.PlaceBlockDirect("木のチェスト", posA, BlockDirection.North);
    var chestColliderA = (await p.WaitBlockGameObject(posA)).GetComponentsInChildren<Collider>().First(c => c.name == "ClickCollider");

    // 確認項目6: 左Altを押さなければ画面中央外の設置物はスポイトされない
    // Check item 6: without left Alt, an object away from the screen center is never picked
    await p.AimAt(chestColliderA.bounds.center);
    await MiddleClickAsync();
    await UniTask.DelayFrame(3);
    p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, "項目6: Alt無しではGameScreenのまま");
    p.Assert(CurrentBlockTarget() == null, "項目6: Alt無しではターゲットが選ばれない");
    await p.Screenshot("00-no-pick-without-alt");

    await PickWithAltHoldAsync(chestColliderA.bounds.center);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "項目1: ピック成功でPlaceBlockへ遷移");
    p.Assert(CurrentBlockTarget() != null, "項目1: CurrentTargetがBlockPlacementTargetになる");
    p.Assert(CurrentBlockTarget()?.BlockId == chestBlockId, "項目1: 選択ブロックがチェストになる");
    await p.Screenshot("01-pick-in-gamescreen");
```

**注意:** 確認項目3・2・4・5 は `PlaceBlock` 滞在中（Buildゾーン＝常にカーソル照準）なので変更しない。既存の `PickBlockAtAsync` はそのまま使う。

- [ ] **Step 3: シナリオを実行して通ることを確認する**

Run: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh .agents/skills/unity-playmode-recorded-playtest/scenarios/building/block-eyedropper-via-ui.cs`
Expected: 全確認項目がPASS。録画とスクリーンショットが出力される。「Unity is reloading (Domain Reload in progress)」が出たら45秒待って再実行する

- [ ] **Step 4: placement-pick-wire-and-train-via-ui の通常モードピックをAltホールド版へ差し替える**

`.agents/skills/unity-playmode-recorded-playtest/scenarios/building/placement-pick-wire-and-train-via-ui.cs` の `using` に `using UnityEngine.InputSystem;` を足し、`MiddleClickAsync` 定義の直後へ Step 1 と同じ `PickWithAltHoldAsync` を追加する。

確認1（GameScreen中の電線ピック）の2行を置き換える。

```csharp
    p.Note("GameScreen中に左Altを押しながら電線をミドルクリックでスポイトする");
    p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, "初期状態はGameScreen");

    await PickWithAltHoldAsync(wireColliders[wireColliders.Length / 2].bounds.center);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "項目1: 電線ピックでPlaceBlockへ遷移");
```

**注意:** 確認2（PlaceBlock中の列車ピック）は Buildゾーンなので `await p.AimAt(carCollider.bounds.center); await MiddleClickAsync();` のまま変更しない。

- [ ] **Step 5: シナリオを実行して通ることを確認する**

Run: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh .agents/skills/unity-playmode-recorded-playtest/scenarios/building/placement-pick-wire-and-train-via-ui.cs`
Expected: 全確認項目がPASS

- [ ] **Step 6: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/building/block-eyedropper-via-ui.cs .agents/skills/unity-playmode-recorded-playtest/scenarios/building/placement-pick-wire-and-train-via-ui.cs
git commit -m "プレイ録画シナリオを左Altホールド前提の実操作忠実な形へ直す"
```

---

### Task 6: 全ブランチレビュー（必須・省略不可）

**Files:**
- 変更なし（レビュー実行のみ）

- [ ] **Step 1: moores-code-review スキルでブランチ全体をレビューする**

`moores-code-review` スキルを起動し、`fix/fps-build-mode-camera` ブランチの全変更をレビューする。ゴール文言による省略は不可。指摘のうち機械的修正は適用し、設計判断は `AskUserQuestion` でユーザー裁定を仰ぐ。

- [ ] **Step 2: 指摘対応後に全テストを再実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "AimPointProviderTest|MapObjectMiningAimTest|UiStateCameraPolicyServiceTest|UIStateCameraInteractionTest|UIStateFocusRestorationTest|PlayerViewMode"`
Expected: 全PASS

- [ ] **Step 3: コミットする**

```bash
git add -A
git commit -m "moores-code-reviewの指摘を反映する"
```

---

## 判断記録（ADR）

設計裁定の正本は `docs/adr/0008-aim-source-pushed-by-ui-state.md` と `.decisions/2026-08-05-*.md`。planning中に新たに生じた判断は以下。

- **Alt制御の置き場は `UiStateCameraPolicyService`（新規サービスを作らない）** — 出所: agent前提（[[2026-08-05-Alt自由カーソルはUiStateCameraPolicyServiceが担う]]）。設計対話中は削除済みの `BuildModeCameraInteractionService` を参照して「兄弟サービス新設」と裁定していたが、同ブランチのより新しいレビュー裁定 [[2026-08-05-カメラポリシーは単一サービスがUIState側で所有する]] がAlt実装を見越して単一所有へ統合していたため覆した
- **`IPlayerCameraInteractionApplier` へ `WarpCursorToScreenCenter()` を追加する** — 出所: agent前提。同interfaceは直前のレビューで `SetInteractionMode` 単一メソッド契約へ畳まれたが、それは「カーソル解放と回転可否が逆相1状態である」ことを型で表す意図であり、ワープは状態でなく1回限りの動作なので別メソッドが妥当と判断した。**ユーザー注目点**（畳んだ直後に増やすため）
- **`HybridInput.GetKeyUp` は入力抑止（`Suppress`）を通さない** — 出所: agent前提。押下側と非対称だが、Web UIのテキストフォーカスで離した通知が抑止されるとホールド修飾キーが押しっぱなしで固着し、自由カーソルから抜けられなくなるため。**ユーザー注目点**（既存の `GetKeyDown` / `GetKey` との非対称）
- **視点切替でAltホールドを破棄する** — 出所: agent前提。既存裁定 [[2026-08-05-V往復中のドラッグ破棄は許容する]]（右ドラッグ中のV往復でドラッグ状態を破棄する）と同形に揃えた
- **`OnViewModeChanged` で `_isFirstPerson` を全ゾーン同期する** — 出所: agent前提。既存実装はBuildゾーン以外で早期returnしており `_isFirstPerson` が更新されない。Gameplayゾーンで視点だけ切り替えると古い値が残り、一人称でも左Altを受け付ける穴になるため冒頭で同期する
- **Menuゾーンは照準ソースを明示的に画面中央へプッシュする** — 出所: agent前提。押し漏れによる古いモードの残留を避け、ゾーンごとに常に決定論的な値を押す形に揃えた
- **GameScreenStateの委譲テストは依存をnullで構築する** — 出所: agent前提。`GameScreenSubInventoryInteractService` / `RideVehicleInputService` / `PlacementTargetPickService` はいずれも入力が無ければ依存へ触れる前にfalseで返るため、既存 `CreatePlaceBlockState` が `PlacementTargetPickService(null)` を使っている前例に合わせた
