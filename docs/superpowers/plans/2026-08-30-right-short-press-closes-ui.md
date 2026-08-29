# パネル外の右短押しでUIと建築モードを解除する Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** UIパネル外で右ボタンを「押して動かさず離す」（右短押し）と、その画面のEscと同じ解除（選択/起点があればそれだけ解除、無ければ画面・モードを閉じる）が起きるようにする。

**Architecture:** 右短押しの判別は純C#状態機械 `RightShortPressInput`（`HotbarKeyInput` 同型：入力値と座標は呼び出し側がプッシュ）に閉じ、Unity入力の読取は `RightShortPressInputService` が担う。各 `IUIState` は `GetNextUpdate()` 内のEsc判定の隣で同サービスを問い合わせ、Escと同じ遷移先を返す（`HotbarTapInputService`/`PlacementTargetPickService` と同じ「ステートが入力解釈サービスを駆動する」形。共通基底や `UIStateControl` 側の横断機構は作らない）。建築モードの二段階解除は `IPlaceSystem.TryCancelInProgressOperation()` を新設し、`PlaceSystemStateController` 経由で現在の設置系（電線ツール・歯車チェーンポール・BPコピー）に委譲する。

**Tech Stack:** Unity C# (uGUI + InputSystem `HybridInput`)、VContainer DI、NUnit EditMode テスト、プレイテストDSL（`Client.Playtest`）＋録画。

## Requirements

設計裁定: `docs/adr/0046-right-short-press-closes-ui-and-build-mode.md`、`.decisions/2026-08-30-*右短押し*.md` / `*右クリック*.md`（5件）。

1. **右短押しの判別**: 右ボタン押下→押下中の移動距離が閾値未満→離す、で1回だけ発火する。ドラッグ（閾値以上移動）は発火しない。受け入れ: `RightShortPressInputTest` の短押し/ドラッグ/パネル上/Reset の4ケースが通る。
2. **パネル外限定**: 押下時点で `UiPointerHitTest.IsPointerOverAnyUi()` が true なら、その押下は離しても発火しない。受け入れ: 同テストのパネル上ケース。
3. **建築モード（PlaceBlock）**: 右短押しで、設置系が進行中操作（電線起点・歯車チェーン起点・BPコピー選択）を持てばそれだけ解除し、無ければ GameScreen へ遷移。受け入れ: `PlaceSystemStateControllerCancelTest` と録画プレイテスト `misc/right-short-press-cancel.cs` の PlaceBlock→GameScreen アサート。
4. **破壊モード（DeleteBar）**: 右短押しで選択中なら `TryCancelSelection()` のみ、無ければ GameScreen へ。Escと同じ二段階。
5. **パネル型UI**: BuildMenu / PlayerInventory / SubInventory / ChallengeList で右短押し→GameScreen（各画面のEscと同じ遷移先）。パネル上の右クリック（アイテム半分取る/1個置く）は従来どおり。
6. **電線ツール**: 既存の「右押下で即起点解除」（`ElectricWireConnectSystem.cs:80`）を右短押し経由の `TryCancelInProgressOperation()` に置き換える。
7. **TPS右ドラッグ回転は不変**: `UiStateCameraPolicyService.UpdateRotationInput()` は触らない。録画プレイテストで右ドラッグ後も PlaceBlock に留まることをアサート。
8. **遷移直後の誤発火防止**: 各対象ステートの `OnEnter` で押下状態をリセット（`HotbarTapInputService.ResetKeyState()` と同じ契約）。
9. **やらないこと**: ヒントHUD・localization.csv の変更、ポーズメニュー/スキット/デバッグ/リサーチツリー/GameScreen への適用、右押下即発火、webui側の背景クリックハンドラ、`UIStateControl` への横断的な閉じ機構。

## Global Constraints

- AGENTS.md 全規約（`#region Internal` はローカル関数用途のみ・日英2行コメント・partial禁止・`Func<>`禁止・デフォルト引数禁止・1ファイル200行以下・1ディレクトリ10ファイル以下・イベントはUniRx・単純getter/setter禁止）
- `.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client`
- テストは `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"`
- 作業は `moores-wt new feature/right-short-press-closes-ui` で切った worktree で行う。メインworktreeでのブランチ操作はhookで拒否される
- 移動閾値は `RightShortPressInput.MoveThresholdPixels = 8f`（agent前提。録画テストで手応えを確認し必要なら変更。裁定ではない）
- 入力読取は `HybridInput.GetMouseButton(1)` / `HybridInput.GetMousePosition()`（カメラ回転と同じ読取面。`InputManager.Playable.ScreenRightClick` は電線ツール1箇所のみの使用で、置換後は未使用になるが定義は残す）
- 新設ディレクトリ: `Client.Game/InGame/UI/UIState/State/CancelInput/`（純状態機械＋サービスの2ファイル）、`Client.Tests/CancelInput/`

---

### Task 0: worktree と bd 着手

**Files:** なし（環境準備）

- [x] **Step 1: worktree作成とEditor起動**

```bash
pwd   # メインworktreeであることを確認
moores-wt new feature/right-short-press-closes-ui
cd ~/moorestech-worktrees/feature-right-short-press-closes-ui   # moores-wt の出力パスに従う
pwd
bd update moorestech-1yza --claim
```

- [x] **Step 2: 初回コンパイルが通ることを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

---

### Task 1: 右短押しの純状態機械 `RightShortPressInput`

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/CancelInput/RightShortPressInput.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/CancelInput/RightShortPressInputTest.cs`

**Interfaces:**
- Produces: `public class RightShortPressInput` — `public void ManualUpdate(bool isRightHeld, Vector2 pointerPosition, bool isPointerOverUi)` / `public bool TryConsumeShortPress()` / `public void Reset()` / `public const float MoveThresholdPixels = 8f`

- [x] **Step 1: 失敗するテストを書く**

```csharp
using Client.Game.InGame.UI.UIState.State.CancelInput;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.CancelInput
{
    /// <summary>
    ///     右短押し/右ドラッグ/パネル上押下の判別とReset契約の回帰試験
    ///     Regression tests for short-press vs drag vs press-over-UI classification and the Reset contract
    /// </summary>
    public class RightShortPressInputTest
    {
        private static readonly Vector2 Origin = new(100f, 100f);

        [Test]
        public void パネル外で動かさず離すと短押しが1回だけ成立する()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(true, Origin + new Vector2(2f, 1f), false);
            input.ManualUpdate(false, Origin + new Vector2(2f, 1f), false);

            Assert.IsTrue(input.TryConsumeShortPress());
            Assert.IsFalse(input.TryConsumeShortPress(), "短押しは1度だけ消費される");
        }

        [Test]
        public void 閾値以上動かしてから離すとドラッグ扱いで成立しない()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(true, Origin + new Vector2(RightShortPressInput.MoveThresholdPixels + 1f, 0f), false);
            // 戻ってきても一度ドラッグになった押下は短押しに復帰しない
            // Once a press became a drag it never turns back into a short press, even if the pointer returns
            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(false, Origin, false);

            Assert.IsFalse(input.TryConsumeShortPress());
        }

        [Test]
        public void パネル上で押した押下は外へ出て離しても成立しない()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, Origin, true);
            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(false, Origin, false);

            Assert.IsFalse(input.TryConsumeShortPress());
        }

        [Test]
        public void Reset時に押下中だった押下は離されても成立せず次の押下から再武装する()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, Origin, false);
            input.Reset();
            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(false, Origin, false);
            Assert.IsFalse(input.TryConsumeShortPress(), "Reset前からの押下は捨てる");

            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(false, Origin, false);
            Assert.IsTrue(input.TryConsumeShortPress(), "離してからの新しい押下は成立する");
        }

        [Test]
        public void Resetは未消費の短押しも捨てる()
        {
            var input = new RightShortPressInput();

            input.ManualUpdate(true, Origin, false);
            input.ManualUpdate(false, Origin, false);
            input.Reset();

            Assert.IsFalse(input.TryConsumeShortPress());
        }
    }
}
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `RightShortPressInput` 未定義のコンパイルエラー（型が無いので失敗）

- [x] **Step 3: 最小限の実装を書く**

```csharp
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.CancelInput
{
    /// <summary>
    ///     右ボタンの押下を「短押し（動かさず離す）」と「ドラッグ」に判別する状態機械。入力読取は呼び出し側がプッシュする
    ///     State machine classifying a right-button press as a short press (released without moving) or a drag; the caller pushes the input
    /// </summary>
    public class RightShortPressInput
    {
        public const float MoveThresholdPixels = 8f;

        private bool _isHeld;
        private Vector2 _pressStartPosition;

        // 押下がまだ短押し候補か。パネル上で押した・閾値以上動いた時点でfalseに落ちる
        // Whether the current press is still a short-press candidate; drops to false when pressed over UI or moved past the threshold
        private bool _isArmed;

        // Reset時に押下中だった押下。離されるまで再武装させない
        // A press that was held at Reset time; it must not re-arm until released
        private bool _isDeadPress;

        private bool _shortPressPending;

        // 押下継続を1フレーム進め、離した瞬間に短押しを確定する
        // Advances the press by one frame and confirms a short press at the moment of release
        public void ManualUpdate(bool isRightHeld, Vector2 pointerPosition, bool isPointerOverUi)
        {
            if (!isRightHeld) _isDeadPress = false;
            var isActiveHeld = isRightHeld && !_isDeadPress;

            if (isActiveHeld != _isHeld)
            {
                HandleHeldChanged(isActiveHeld);
                return;
            }

            DisarmIfMoved();

            #region Internal

            // 押下開始で武装（パネル上なら非武装）、離しで武装中のみ確定
            // Arms on press start (unless over UI) and confirms on release only while still armed
            void HandleHeldChanged(bool nextHeld)
            {
                if (!nextHeld && _isArmed) _shortPressPending = true;

                _isHeld = nextHeld;
                _pressStartPosition = pointerPosition;
                _isArmed = nextHeld && !isPointerOverUi;
            }

            // 閾値以上動いたらドラッグとみなし、この押下では二度と成立させない
            // Moving past the threshold makes it a drag; this press can never become a short press again
            void DisarmIfMoved()
            {
                if (!_isHeld || !_isArmed) return;
                if ((pointerPosition - _pressStartPosition).sqrMagnitude < MoveThresholdPixels * MoveThresholdPixels) return;

                _isArmed = false;
            }

            #endregion
        }

        // 確定した短押しを1回だけ消費する
        // Consumes a confirmed short press exactly once
        public bool TryConsumeShortPress()
        {
            if (!_shortPressPending) return false;

            _shortPressPending = false;
            return true;
        }

        // 押下中の押下を消費済みにし、遷移直後の誤発火を防ぐ
        // Marks the held press consumed so a transition cannot produce a false short press
        public void Reset()
        {
            _isDeadPress = _isHeld;
            _isHeld = false;
            _isArmed = false;
            _shortPressPending = false;
        }
    }
}
```

- [x] **Step 4: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "RightShortPressInputTest"`
Expected: 5 tests PASS

- [x] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/CancelInput moorestech_client/Assets/Scripts/Client.Tests/CancelInput
git commit -m "feat: 右短押しを判別するRightShortPressInput状態機械を追加"
```

（Unityが生成した `.meta` も同時に `git add` する。手動作成は禁止）

---

### Task 2: Unity入力を読む `RightShortPressInputService` とDI登録

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/CancelInput/RightShortPressInputService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs:92-93`（`HotbarKeyInput`/`HotbarTapInputService` 登録の直後）

**Interfaces:**
- Consumes: Task 1 の `RightShortPressInput`
- Produces: `public class RightShortPressInputService` — `public bool TryConsumeShortPressOutsideUi()` / `public void ResetPressState()`

- [x] **Step 1: サービスを書く**

```csharp
using Client.Game.InGame.Control;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State.CancelInput
{
    /// <summary>
    ///     パネル外の右短押しを各UIStateがEsc判定の隣で問い合わせる唯一の入口。入力読取というUnity依存だけをここで解決する
    ///     The single entry point each UIState queries beside its Esc check; only the Unity-dependent input read lives here
    /// </summary>
    public class RightShortPressInputService
    {
        private readonly RightShortPressInput _rightShortPressInput;

        public RightShortPressInputService(RightShortPressInput rightShortPressInput)
        {
            _rightShortPressInput = rightShortPressInput;
        }

        // 毎フレーム呼ぶ。右短押しが成立したフレームだけtrue
        // Call every frame; true only on the frame a short press outside UI is confirmed
        public bool TryConsumeShortPressOutsideUi()
        {
            _rightShortPressInput.ManualUpdate(HybridInput.GetMouseButton(1), HybridInput.GetMousePosition(), UiPointerHitTest.IsPointerOverAnyUi());
            return _rightShortPressInput.TryConsumeShortPress();
        }

        // UIState遷移のたびに押下を捨てる。他状態滞在中はpollされないため復帰直後の誤発火を防ぐ
        // Drops the held press on every UIState transition; nothing polls while another state is active, so this prevents a stale fire on return
        public void ResetPressState()
        {
            _rightShortPressInput.Reset();
        }
    }
}
```

- [x] **Step 2: DI登録を追加する**

`MainGameInteractionRegistration.cs` の `builder.Register<HotbarTapInputService>(Lifetime.Singleton);` の直後に追加:

```csharp
            builder.Register<RightShortPressInput>(Lifetime.Singleton);
            builder.Register<RightShortPressInputService>(Lifetime.Singleton);
```

（`using Client.Game.InGame.UI.UIState.State.CancelInput;` をファイル先頭に追加）

- [x] **Step 3: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

- [x] **Step 4: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/CancelInput moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs
git commit -m "feat: RightShortPressInputServiceを追加しDI登録"
```

---

### Task 3: 設置系の進行中操作を解除する `IPlaceSystem.TryCancelInProgressOperation`

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs:6-17`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemBase.cs:10-30`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Empty/EmptyPlaceSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs:60`（`Disable()` の直前）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/ElectricWireConnectSystem.cs:76-84,103-111`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/GearChainPoleConnectSystem.cs:108-120`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/BlueprintCopySystem.cs:135-141,177-181`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlaceSystemStateControllerCancelTest.cs`

**Interfaces:**
- Produces: `IPlaceSystem.bool TryCancelInProgressOperation()`（解除したものがあればtrue）、`PlaceSystemStateController.bool TryCancelInProgressOperation()`（現在の設置系へ委譲）

- [x] **Step 1: 失敗するテストを書く**

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    /// <summary>
    ///     進行中操作の解除が現在の設置系へ委譲されることを検証
    ///     Verifies that cancelling an in-progress operation is delegated to the current place system
    /// </summary>
    public class PlaceSystemStateControllerCancelTest
    {
        [Test]
        public void 現在の設置系が解除できれば結果をそのまま返す()
        {
            var placeSystem = new CancellablePlaceSystem { CancelResult = true };
            var controller = new PlaceSystemStateController(new SingleSelector(placeSystem), new NullPresenter());
            controller.ManualUpdate();

            Assert.IsTrue(controller.TryCancelInProgressOperation());
            Assert.AreEqual(1, placeSystem.CancelCallCount);
        }

        [Test]
        public void 解除対象が無ければfalseを返す()
        {
            var placeSystem = new CancellablePlaceSystem { CancelResult = false };
            var controller = new PlaceSystemStateController(new SingleSelector(placeSystem), new NullPresenter());
            controller.ManualUpdate();

            Assert.IsFalse(controller.TryCancelInProgressOperation());
        }

        [Test]
        public void ManualUpdate前はEmptyPlaceSystemに委譲されfalseになる()
        {
            var controller = new PlaceSystemStateController(new SingleSelector(new CancellablePlaceSystem { CancelResult = true }), new NullPresenter());

            Assert.IsFalse(controller.TryCancelInProgressOperation());
        }

        private class CancellablePlaceSystem : IPlaceSystem
        {
            public bool CancelResult;
            public int CancelCallCount;
            public bool OwnsWheelInput => false;
            public void Enable() { }
            public void ManualUpdate(PlaceSystemUpdateContext context) { }
            public void Disable() { }

            public bool TryCancelInProgressOperation()
            {
                CancelCallCount++;
                return CancelResult;
            }
        }

        private class SingleSelector : IPlaceSystemSelector
        {
            private readonly IPlaceSystem _placeSystem;
            public SingleSelector(IPlaceSystem placeSystem) { _placeSystem = placeSystem; }
            public IPlaceSystem EmptyPlaceSystem { get; } = new Client.Game.InGame.BlockSystem.PlaceSystem.Empty.EmptyPlaceSystem();
            public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context) => _placeSystem;
        }

        private class NullPresenter : IPlacementFeedbackPresenter
        {
            public void Present(PlacementFeedback feedback) { }
            public void Hide() { }
        }
    }
}
```

（`IPlaceSystemSelector` / `IPlacementFeedbackPresenter` のメンバー名は `PlaceSystemStateControllerFeedbackTest.cs` の `FakePlaceSystemSelector` / `FakePlacementFeedbackPresenter` と同じ形にする。差異があればそちらを正として合わせる）

- [x] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `TryCancelInProgressOperation` 未定義のエラー。同時に `PlaceSystemStateControllerFeedbackTest.FakePlaceSystem` も interface 未実装で失敗する

- [x] **Step 3: interface と基底・Empty・Controller に追加する**

`IPlaceSystem.cs` の `Disable();` の後に:

```csharp
        // 進行中の操作（起点保持・範囲選択等）だけを解除する。解除したものがあればtrue。無ければfalseで呼び出し側がモードを閉じる
        // Cancels only an in-progress operation (held origin, box selection, ...); true when something was cancelled, false lets the caller close the mode
        public bool TryCancelInProgressOperation();
```

`PlaceSystemBase.cs` の `OwnsWheelInput` の直後に:

```csharp
        // 進行中操作を持たない設置系が多数派なので既定はfalse。持つ側だけがoverrideする
        // Most place systems hold no in-progress operation, so the default is false; only the holders override it
        public virtual bool TryCancelInProgressOperation() => false;
```

`EmptyPlaceSystem.cs` に `public bool TryCancelInProgressOperation() => false;` を追加。

`PlaceSystemStateController.cs` の `Disable()` の直前に:

```csharp
        // 右短押し/Escの二段階解除。進行中操作があればそれだけ解除し、呼び出し側はtrueなら遷移しない
        // Two-stage cancel for right short press / Esc: cancels only an in-progress operation; the caller does not transition on true
        public bool TryCancelInProgressOperation()
        {
            return _currentPlaceSystem.TryCancelInProgressOperation();
        }
```

`PlaceSystemStateControllerFeedbackTest.cs` の `FakePlaceSystem` にも `public bool TryCancelInProgressOperation() => false;` を追加する。

- [x] **Step 4: 電線ツールの右押下即解除を置き換える**

`ElectricWireConnectSystem.cs:76-84` の `if (InputManager.Playable.ScreenRightClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) { ... }` ブロック（コメント2行含む）を**削除**し、`Disable()` の直前に追加:

```csharp
        // 右短押し/Escで起点を解除し、進行中の応答を無効化する。起点なしの孤立設置の応答待ちも明示キャンセルとして止める
        // A right short press / Esc releases the origin and invalidates any pending response, including an originless isolated placement still awaiting one
        public override bool TryCancelInProgressOperation()
        {
            var hadInProgress = _sourceBlock != null || _context.RequestSender.IsAwaitingResponse;
            _sourceBlock = null;
            _context.RequestSender.Invalidate();
            return hadInProgress;
        }
```

`IsAwaitingResponse` は `ElectricWireConnect/Parts/ElectricWireExtendRequestSender.cs:33` に既存（`{ get; private set; }`）。不要になった `using Client.Game.InGame.Control;` / `Client.Input` があれば削除。

- [x] **Step 5: 歯車チェーンポールとBPコピーにも実装する**

`GearChainPoleConnectSystem.cs` の `Disable()` の直後に:

```csharp
        public bool TryCancelInProgressOperation()
        {
            if (_sourcePole == null) return false;

            ResetState();
            return true;
        }
```

`BlueprintCopySystem.cs`: `HandleCancel()` ローカル関数とその呼び出し（90行目付近 `HandleCancel();`）を削除し、`Disable()` の直後に:

```csharp
        public override bool TryCancelInProgressOperation()
        {
            if (!_isDragging) return false;

            ResetSelection();
            return true;
        }
```

- [x] **Step 6: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlaceSystemStateController"`
Expected: `PlaceSystemStateControllerCancelTest` 3件 + `PlaceSystemStateControllerFeedbackTest` 3件 PASS

- [x] **Step 7: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem
git commit -m "feat: IPlaceSystem.TryCancelInProgressOperationを追加し電線起点解除を右押下即発火から分離"
```

---

### Task 4: 建築モード（PlaceBlockState）の右短押し配線

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs:20-66,100-108`

**Interfaces:**
- Consumes: `RightShortPressInputService.TryConsumeShortPressOutsideUi()` / `ResetPressState()`、`PlaceSystemStateController.TryCancelInProgressOperation()`

- [x] **Step 1: ctorとフィールドに注入する**

`using Client.Game.InGame.UI.UIState.State.CancelInput;` を追加。フィールド `private readonly RightShortPressInputService _rightShortPressInputService;` を `_hotbarInputService` の下に追加。ctor末尾の引数に `RightShortPressInputService rightShortPressInputService` を追加し `_rightShortPressInputService = rightShortPressInputService;` を代入。

- [x] **Step 2: OnEnterでリセットする**

`OnEnter` の `_hotbarInputService.ResetKeyState();` の直後に:

```csharp
            _rightShortPressInputService.ResetPressState();
```

- [x] **Step 3: GetNextUpdateのEsc判定の隣に追加する**

`if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.GameScreen);` の直後に:

```csharp
            // パネル外の右短押しはEscと同じ二段階。起点/選択があればそれだけ解除し、無ければ建築モードを抜ける
            // A right short press outside UI mirrors Esc: cancel only an in-progress operation, otherwise leave build mode
            if (_rightShortPressInputService.TryConsumeShortPressOutsideUi() && !_placeSystemStateController.TryCancelInProgressOperation())
            {
                return new UITransitContext(UIStateEnum.GameScreen);
            }
```

- [x] **Step 4: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

- [x] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs
git commit -m "feat: 建築モードをパネル外の右短押しで解除する"
```

---

### Task 5: 破壊モード（DeleteObjectState）の右短押し配線

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs:24-40,64-80`

- [ ] **Step 1: 注入とOnEnterリセット**

`using Client.Game.InGame.UI.UIState.State.CancelInput;` 追加。フィールド `private readonly RightShortPressInputService _rightShortPressInputService;` 追加。ctor引数末尾に `RightShortPressInputService rightShortPressInputService` を追加し代入。`OnEnter` 先頭に `_rightShortPressInputService.ResetPressState();` を追加。

- [ ] **Step 2: HandleTransitionのEsc判定を右短押しと共有する**

`HandleTransition()` 内の

```csharp
                if (InputManager.UI.CloseUI.GetKeyDown && !_deleteObjectService.TryCancelSelection())
                {
                    return new UITransitContext(UIStateEnum.GameScreen);
                }
```

を次に置き換える（`TryConsumeShortPressOutsideUi()` は毎フレーム呼ばないと押下開始を取りこぼすため、`||` の短絡に入れず先に評価する）:

```csharp
                // ESC/パネル外の右短押しはまず削除選択のキャンセルに使い、キャンセルする選択が無ければ破壊モードを抜ける
                // ESC and a right short press outside UI first cancel the delete selection; with nothing to cancel they leave destroy mode
                var isRightShortPressed = _rightShortPressInputService.TryConsumeShortPressOutsideUi();
                var isCancelRequested = InputManager.UI.CloseUI.GetKeyDown || isRightShortPressed;
                if (isCancelRequested && !_deleteObjectService.TryCancelSelection())
                {
                    return new UITransitContext(UIStateEnum.GameScreen);
                }
```

`HandleTransition()` は `GetNextUpdate()` の先頭で毎フレーム呼ばれるため、この位置で押下追跡が途切れることはない。

- [ ] **Step 3: コンパイルしてコミットする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs
git commit -m "feat: 破壊モードをパネル外の右短押しでEsc同型に解除する"
```

---

### Task 6: パネル型UI4画面の右短押し配線

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs:16-37`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlayerInventoryState.cs:25-46`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventoryState.cs:47-88`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/ChallengeListState.cs:13-28`

各ファイルで同じ3点を行う（`using Client.Game.InGame.UI.UIState.State.CancelInput;`／フィールド `private readonly RightShortPressInputService _rightShortPressInputService;`／ctor引数末尾に追加して代入／`OnEnter` 先頭に `_rightShortPressInputService.ResetPressState();`）。

- [ ] **Step 1: BuildMenuState**

```csharp
            if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.B) || _rightShortPressInputService.TryConsumeShortPressOutsideUi()) return new UITransitContext(UIStateEnum.GameScreen, null);
```

ただし短絡回避のため、先頭で `var isRightShortPressed = _rightShortPressInputService.TryConsumeShortPressOutsideUi();` を評価してから条件に `|| isRightShortPressed` を足す（以下3画面も同じ形）。`TryConsumeSelectedEntry` の分岐より**後**に置く（エントリ選択が優先）。

- [ ] **Step 2: PlayerInventoryState**

`GetNextUpdate` を:

```csharp
        public UITransitContext GetNextUpdate()
        {
            // 毎フレーム押下を追跡するため先に評価する（短絡で押下開始を取りこぼさない）
            // Evaluate first so the press is tracked every frame (short-circuiting would miss the press start)
            var isRightShortPressed = _rightShortPressInputService.TryConsumeShortPressOutsideUi();

            // Rでリサーチツリーへ、Tab/ESC/パネル外の右短押しでゲーム画面へ戻る
            // Go to research tree with R, or back to game screen with Tab/ESC/right short press outside UI
            if (HybridInput.GetKeyDown(KeyCode.R)) return new UITransitContext(UIStateEnum.ResearchTree);
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown || isRightShortPressed) return new UITransitContext(UIStateEnum.GameScreen);

            return null;
        }
```

- [ ] **Step 3: SubInventoryState**

```csharp
        public UITransitContext GetNextUpdate()
        {
            var isRightShortPressed = _rightShortPressInputService.TryConsumeShortPressOutsideUi();
            if (_shouldClose || InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown || isRightShortPressed)
            {
                return new UITransitContext(UIStateEnum.GameScreen);
            }

            return null;
        }
```

- [ ] **Step 4: ChallengeListState**

```csharp
        public UITransitContext GetNextUpdate()
        {
            var isRightShortPressed = _rightShortPressInputService.TryConsumeShortPressOutsideUi();
            //TODO InputManagerに移す
            if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.T) || isRightShortPressed) return new UITransitContext(UIStateEnum.GameScreen);
            if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);

            return null;
        }
```

- [ ] **Step 5: コンパイルして既存テストを回す**

Run: `uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "UIState|Hotbar|PlaceSystemStateController|RightShortPress"`
Expected: すべて PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/{BuildMenuState,PlayerInventoryState,SubInventoryState,ChallengeListState}.cs
git commit -m "feat: パネル型UI4画面をパネル外の右短押しで閉じる"
```

---

### Task 7: 録画プレイテスト（右短押し解除・右ドラッグ非解除・インベントリ閉じ）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/Operations/PlaytestCancelOps.cs`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/right-short-press-cancel.cs`

**Interfaces:**
- Consumes: `SemanticInput.MouseButtonDown(int)` / `MouseButtonUp(int)` / `MouseMoveTo(Vector2)` / `CurrentMousePosition()`、`PlaytestDriver.Note`、`p.Hotbar.AssignHotbar` / `p.Hotbar.EnterBuildMode`、`p.WaitUiState`、`p.CurrentUiState`、`p.PressKey`
- Produces: `RightShortClick(this PlaytestDriver p)`、`RightDrag(this PlaytestDriver p, Vector2 deltaPixels)`

- [ ] **Step 1: 操作を追加する**

```csharp
using Client.Playtest.Input;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     右短押し（解除）と右ドラッグ（TPS回転）の注入。両者の違いは押下中の移動の有無だけ
    ///     Injects a right short press (cancel) and a right drag (TPS look); the only difference is pointer movement while held
    /// </summary>
    public static class PlaytestCancelOps
    {
        public static async UniTask RightShortClick(this PlaytestDriver p)
        {
            p.Note("右短押し");
            SemanticInput.MouseButtonDown(1);
            await UniTask.DelayFrame(2);
            SemanticInput.MouseButtonUp(1);
            await UniTask.DelayFrame(2);
        }

        public static async UniTask RightDrag(this PlaytestDriver p, Vector2 deltaPixels)
        {
            p.Note("右ドラッグ");
            var start = SemanticInput.CurrentMousePosition();
            SemanticInput.MouseButtonDown(1);
            await UniTask.DelayFrame(2);
            SemanticInput.MouseMoveTo(start + deltaPixels);
            await UniTask.DelayFrame(2);
            SemanticInput.MouseButtonUp(1);
            await UniTask.DelayFrame(2);
        }
    }
}
```

（`Client.Playtest/Operations` は本ファイルで8ファイル。10ファイル規約内）

- [ ] **Step 2: シナリオを書く**

```csharp
// 右短押しでの解除検証: 建築モード→右短押しで抜ける / 右ドラッグでは抜けない / インベントリ→パネル外右短押しで閉じる
// Right short press cancel probe: build mode exits on a short press, stays on a drag, inventory closes on a short press outside the panel
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("right-short-press-cancel", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();

    p.Note("建築モードへ入る");
    await p.Hotbar.AssignHotbar(0, "チェスト");
    await p.Hotbar.EnterBuildMode(0);
    await p.WaitUiState(UIStateEnum.PlaceBlock, 5f);
    // 画面中央（パネル外）に照準してから操作する
    // Aim at the screen center (outside any panel) before the presses
    SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
    await UniTask.DelayFrame(3);

    p.Note("右ドラッグでは建築モードに留まる");
    await p.RightDrag(new Vector2(60f, 0f));
    await UniTask.DelayFrame(5);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "右ドラッグ後もPlaceBlock");
    await p.Screenshot("01-after-right-drag");

    p.Note("右短押しで建築モードを抜ける");
    await p.RightShortClick();
    await p.WaitUiState(UIStateEnum.GameScreen, 5f);
    p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, "右短押しでGameScreen");
    await p.Screenshot("02-after-right-short-press");

    p.Note("インベントリをパネル外の右短押しで閉じる");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 5f);
    // 画面左上端はインベントリパネルの外
    // The top-left corner is outside the inventory panel
    SemanticInput.MouseMoveTo(new Vector2(8f, Screen.height - 8f));
    await UniTask.DelayFrame(3);
    await p.RightShortClick();
    await p.WaitUiState(UIStateEnum.GameScreen, 5f);
    p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, "パネル外右短押しでインベントリが閉じる");
    await p.Screenshot("03-inventory-closed");
});
```

（`p.Hotbar.EnterBuildMode` / `WaitUiState` / `PressKey` / `Screenshot` の正確なシグネチャは `write-scenario.md` の Driver API 表と `scenarios/connect/gear-chain-pole-via-ui.cs` を正とし、差異があれば合わせる。ホットバー割当名「チェスト」が未アンロックで失敗する場合は `p.UnlockBlock("チェスト")` を `AssignHotbar` の前に足す）

- [ ] **Step 3: 実行する**

```bash
uloop control-play-mode --project-path ./moorestech_client --action stop
SKILL=.claude/skills/unity-playmode-recorded-playtest
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/misc/right-short-press-cancel.cs"
```

Expected: result.json の assert 3件すべて pass。失敗したら `troubleshooting.md` を読んで原因を切り分ける（masterピンworktree未作成が最頻）。閾値8pxで右ドラッグが短押し扱いになる場合は `RightDrag` の delta を増やすのではなく、注入の移動が `ManualUpdate` に届いているか（`HybridInput.GetMousePosition()` が注入値か）を先に疑う。

- [ ] **Step 4: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Playtest/Operations .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/right-short-press-cancel.cs
git commit -m "test: 右短押し解除の録画プレイテストシナリオを追加"
```

---

### Task 8: 全ブランチレビュー（必須・省略不可）

- [ ] **Step 1: moores-code-review を実行する**

必ず最後にコードレビュースキル（moores-code-review）で全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘の機械的修正を適用し、設計判断だけをユーザーへ提示する。

- [ ] **Step 2: PR作成と撤収**

```bash
# pr-create スキルでPRを作成（本文にADR-0046と.decisions 5件をリンク）
bd close moorestech-1yza --reason="PR作成済み"
moores-wt rm feature-right-short-press-closes-ui
```

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 機構 | 前例 | 判定 |
|---|---|---|---|---|---|
| 1 | `RightShortPressInput`（純状態機械） | Client.Game `UI/UIState/State/CancelInput/` | 値プッシュ型 `ManualUpdate` | `Client.Game/InGame/Hotbar/HotbarKeyInput.cs`（同役割: 押下の分類） | ok |
| 2 | `RightShortPressInputService` | 同上 | ステートが毎フレーム駆動する `TryXxx` bool | `PlacementTargetPickService.TryPickTargetUnderCursor`（遷移文脈を生むだけで共有状態を書かない判定サービス。層マップ「`TryGet`型boolが正当なのは判定だけで共有状態を書かないサービス」に該当） | ok |
| 3 | 各 `IUIState` の Esc 隣への1行追加＋`OnEnter` リセット | 各ステート | ステート駆動 | `PlaceBlockState` の `_hotbarInputService.ResetKeyState()` / `ResolveBuildModeTap` | ok |
| 4 | `IPlaceSystem.TryCancelInProgressOperation` | PlaceSystem 契約 | interface メンバー追加＋基底 virtual false | `OwnsWheelInput`（同形: 多数派false・持つ側だけoverride） | ok |
| 5 | `PlaceSystemStateController.TryCancelInProgressOperation` | 同 | 現設置系へ委譲 | `Disable()` の委譲 | ok |
| 6 | `DeleteObjectState` の二段階 | 既存 | `TryCancelSelection` 再利用 | 既存Esc | ok |
| 7 | DI登録 | `MainGameInteractionRegistration` | VContainer Singleton | `HotbarKeyInput`/`HotbarTapInputService` | ok |

データフロー: 右ボタン入力 →（`RightShortPressInputService` = 書き手ではなく判定器）→ 各ステート `GetNextUpdate` が `UITransitContext` を返す（既存のEsc経路と同じ矢印位置）。共有モデルへの新規書き込み経路・`UIStateControl` への分岐追加は無し。

機構選択（検査4）: 能動介入案「`UIStateControl` に横断的な右短押し→閉じ機構を足す」は、10状態が各自閉じ条件を持つ既存前例（ADR-0032でも各ステートがヒントを所有）に反するため不採用。各ステートがEsc隣で問い合わせる受動的統合を採用。

機能パリティ（死活表）:
| 操作 | 計画後 | 根拠 |
|---|---|---|
| 建築モードの左クリック設置/ドラッグ | 生きる | 左ボタンは触らない |
| TPS右ドラッグ回転（建築/破壊） | 生きる | `UpdateRotationInput` 不変・短押しのみ発火 |
| 電線ツールの右クリック起点解除 | 生きる（短押し限定へ変わる） | ADR-0046 裁定2の帰結。右ドラッグ中の起点消失は無くなる |
| インベントリの右クリック半分取る/1個置く | 生きる | パネル上押下は非武装 |
| ビルドメニューのエントリ選択 | 生きる | `TryConsumeSelectedEntry` を先に評価 |
| Esc/Tab/B/G/T/R/数字キーの既存閉じ | 生きる | 条件に `||` で足すだけ |
| BPコピーのEscキャンセル（未到達だった） | 到達可能になる | `HandleCancel` 削除→`TryCancelInProgressOperation` に移設。Esc側は従来どおり PlaceBlockState が先に消費し GameScreen へ抜ける（現状不変） |

## 判断記録（ADR）

- 設計裁定: `docs/adr/0046-right-short-press-closes-ui-and-build-mode.md`（ユーザー裁定5件、`.decisions/2026-08-30-*` 参照）
- **二段階解除を `IPlaceSystem` の契約に載せる**（agent前提）: 裁定「起点/選択があればそれだけ解除」を建築モードで実現するには PlaceBlockState が現在の設置系へ問い合わせる必要がある。`OwnsWheelInput` と同形の「多数派false・持つ側だけoverride」で追加。電線（裁定で明示）に加え、同じ「起点」概念を持つ歯車チェーンポール（`_sourcePole`）と「選択」を持つBPコピー（`_isDragging`）にも実装する。裁定文の「起点/選択」の適用であり新規裁定ではない
- **Esc側の建築モードは現状維持**（agent前提）: 右短押しは二段階だが、PlaceBlockState の Esc は従来どおり即 GameScreen（Escを二段階化するのは裁定範囲外。差が気になれば別途裁定）
- **押下追跡は毎フレーム評価し `||` の短絡に入れない**（agent前提）: `ManualUpdate` を呼ばないフレームがあると押下開始を取りこぼす。各ステートで先に `var isRightShortPressed = ...` を評価する
- **移動閾値 8px**（agent前提）: 裁定は「動かさず離す」のみ。値は録画テストで確認
- **`InputManager.Playable.ScreenRightClick` は未使用化しても残す**（agent前提）: InputActionアセット変更は本planの範囲外
