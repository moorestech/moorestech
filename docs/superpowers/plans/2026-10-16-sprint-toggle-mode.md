# 走りのトグル/ホールド設定 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 走り（Sprint）を「トグル / ホールド」から設定画面で選べるようにし、既定をトグルにする。

**Architecture:** 走り状態の判定は `Client.Input` の純C#クラス `SprintInputResolver` 1か所に集め、`StarterAssetsInputs.sprint` を書くのはその結果だけにする（`ThirdPersonController`・フライは `sprint` を読むだけで無改変）。方式の保存は言語と同じ PlayerPrefs の静的窓口 `SprintInputModePreference`。Web UI とは言語選択と同じ「action（書く）＋topic（現在値の配信）」の組で繋ぎ、ポーズメニューの設定ページに `ModeSwitch` を1つ足す。メニュー中の切替抑止は IUIState の宣言（`LocksPlayerMovement` と同じ形）を UIStateControl が自機へプッシュする。

**Tech Stack:** Unity 6 / C# / Unity InputSystem / UniRx / React + Mantine + zod（moorestech_web/webui）/ vitest / NUnit（InputTestFixture）

## Requirements

1. 設定画面（ポーズメニュー→設定）に「走り: トグル / ホールド」の択一を置く。受け入れ: 設定ページに言語選択と並んで表示され、選ぶと即時に方式が変わる。
2. 既定はトグル。受け入れ: PlayerPrefs に保存値が無い状態で起動するとトグル。
3. 選んだ方式は再起動後も残る。受け入れ: PlayerPrefs キー `SprintInputMode` に `Toggle`/`Hold` が保存され、起動時に読まれる。
4. ホールドは現状どおり（Shift押下中だけ走る）。受け入れ: 既存の `PlayerMovementUiLockTest`・`TextInputMovementLockTest` がホールドで通る。
5. トグル時、Shift を押してから離すまでに他のキー・クリックが無ければ、離した瞬間に走/歩を反転する。受け入れ: Shift単独タップで反転、Shift+R・Shift+クリック・Shift押下中にWASD押し始めでは反転しない。
6. トグルの走り状態は止まっても、ESCメニュー・文字入力を挟んでも維持する。受け入れ: 入力無効→有効で状態が戻る。
7. トグル時、移動を止めないメニュー（PlayerInventory・SubInventory・ResearchTree・BuildMenu・ChallengeList）を開いている間は Shift で反転しない。受け入れ: それらのステート中のタップで反転しない。GameScreen・PlaceBlock・DeleteBar では反転する。
8. トグル時、ゲーム開始直後の走り状態は ON。設定画面でホールド→トグルへ切り替えた瞬間も ON。
9. フライの×6速は同じ `sprint` に従う（フライ側無改変）。
10. ゲームパッド左トリガー（Sprint action の既存割当）も同じ方式（専用処理なし）。

やらないこと: 走り状態のHUD表示、ゲームパッド正式対応、走り状態そのものの保存、キーリバインド。

## Global Constraints

- ADR: `docs/adr/0082-sprint-toggle-mode-default.md`、裁定: `.decisions/2026-10-16-*走り*`・`*Shift*`・`*フライの高速*`
- 1ファイル200行未満。`ThirdPersonController.cs`（421行の既存）は触らない
- コメントは日本語1行→英語1行の2行セット。`Func<>`・partial・デフォルト引数・try-catch 禁止。イベント発火は UniRx
- `.meta` を手で作らない。Prefab/Scene をテキスト編集しない（本planは Prefab 変更不要）
- fail-closed・縮退経路は理由をログに出す
- .cs 変更後は `uloop compile --project-path ./moorestech_client`（worktree ルートで実行）
- Web: `cd moorestech_web/webui && npm run gen:i18n && npx vitest run <対象>`、型検査 `npx tsc --noEmit`

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 全新設が書き手で読み手無改変、前例（Localize・LocalizationTopic・LocksPlayerMovement）準拠、死活表で兼用Shift操作の生存を確認
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強4・弱4・第3バケツ0 / 強はTS型の一本化を採用・ADR agent前提を修正・他2件このまま、弱は保存値の定義域検証のみ採用

---

## 配置と前例

| # | 項目 | 配置先 | 機構 / 前例 |
|---|---|---|---|
| 1 | `SprintInputMode` enum | Client.Input | 新規 |
| 2 | `SprintInputModePreference`（PlayerPrefs＋変化通知） | Client.Input | PlayerPrefs＋UniRx Subject。前例 `Client.Localization/Localize.cs`（`LanguagePreferenceKey`・`OnLanguageChanged`） |
| 3 | `SprintInputResolver`（走り状態の判定） | Client.Input | 純C#。入力→状態の解決は入力層の責務 |
| 4 | `InputKey.IsBoundControl(InputControl)` | Client.Input/InputManager.cs | 既存クラスへの追加。Sprint以外のボタン押下を見分ける |
| 5 | `StarterAssetsInputs` の sprint 書き込み | StarterAssetsInputs asmdef | 既存。`InputSystem.onAnyButtonPress` 購読 |
| 6 | `IUIState.AllowsSprintToggle()` | Client.Game UIState | 前例 `IUIState.LocksPlayerMovement()` と同形の宣言 |
| 7 | `IPlayerObjectController.SetSprintToggleAllowed(bool)` | Client.Game Player | 前例 `SetMovementLock` のプッシュ経路 |
| 8 | `SprintInputModeTopic` / `SetSprintInputModeActionHandler` | Client.WebUiHost | 前例 `LocalizationTopic`・`LocalizationActions` |
| 9 | `SprintModeSelect.tsx` | webui features/settings | 前例 `LanguageSelect.tsx`（ModeSwitch） |

データフロー: `Shift入力 / 他ボタン押下 / UIステート宣言 / 設定変更` →（書き手）→ `SprintInputResolver.IsSprinting` → `StarterAssetsInputs.sprint` →（読み手）`ThirdPersonController` / `PlayerFlightMotion`。新設は全て書き手。読み手は無改変。

機能パリティ（死活表）:
| 操作 | 計画後 | 根拠 |
|---|---|---|
| ホールド走り | 生存 | 設定ホールドで従来同等（Req4） |
| Shift+R 縦回転・Shift+クリック範囲削除・ベルト/電柱向き | 生存 | それらは `HybridInput.GetKey(LeftShift)` を直接読み、sprint とは独立 |
| メニュー中の Shift+クリック一括移動 | 生存 | Web UI 側の処理で無関係 |
| フライ×6 | 生存（トグル時は状態に従う） | 裁定どおり |

---

### Task 1: 走り状態の判定（SprintInputResolver）と方式の保存

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Input/Sprint/SprintInputMode.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Input/Sprint/SprintInputModePreference.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Input/Sprint/SprintInputResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Input/InputManager.cs`（`InputKey` に `IsBoundControl` 追加）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Player/SprintInputResolverTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Player/SprintInputModePreferenceTest.cs`

**Interfaces:**
- Produces:
  - `public enum SprintInputMode { Toggle, Hold }`（namespace `Client.Input`）
  - `public static class SprintInputModePreference { public static SprintInputMode Current { get; } ; public static IObservable<SprintInputMode> OnChanged { get; } ; public static void SetMode(SprintInputMode mode); }`
  - `public class SprintInputResolver { public SprintInputResolver(SprintInputMode mode); public bool IsSprinting { get; private set; } public void SetMode(SprintInputMode mode); public void SetToggleAllowed(bool allowed); public void OnSprintPressed(); public void OnSprintReleased(); public void OnOtherButtonPressed(); public void ResyncHeld(bool isHeld); }`
  - `InputKey.IsBoundControl(InputControl control) : bool`
  - `SprintInputModePreference.PreferenceKey`（`internal const string`。テストの退避・復元用。`Client.Input`→`Client.Tests` の InternalsVisibleTo は既存）

- [x] **Step 1: テストを書く** — `SprintInputResolverTest.cs`（純C#。加えて `SprintInputModePreferenceTest.cs` に「未保存→Toggle」「"Hold"保存→Hold」「"2"保存→Toggle＋LogWarning（`LogAssert.Expect(LogType.Warning, new Regex("Unknown saved sprint mode"))`）」の3ケースを置く。PlayerPrefs は SetUp で `PlayerPrefs.HasKey/GetString(SprintInputModePreference.PreferenceKey)` を退避し TearDown で復元する（前例 `LocalizationLanguageSelectionContractTest`））

```csharp
using Client.Input;
using NUnit.Framework;

namespace Client.Tests.Player
{
    public class SprintInputResolverTest
    {
        [Test]
        public void トグルは開始時に走りで単独タップで反転する()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Toggle);
            Assert.IsTrue(resolver.IsSprinting);

            resolver.OnSprintPressed();
            Assert.IsTrue(resolver.IsSprinting);
            resolver.OnSprintReleased();
            Assert.IsFalse(resolver.IsSprinting);
        }

        [Test]
        public void トグルは押下中に他ボタンが押されたら反転しない()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Toggle);
            resolver.OnSprintPressed();
            resolver.OnOtherButtonPressed();
            resolver.OnSprintReleased();
            Assert.IsTrue(resolver.IsSprinting);
        }

        [Test]
        public void トグルは抑止中のタップで反転しない()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Toggle);
            resolver.SetToggleAllowed(false);
            resolver.OnSprintPressed();
            resolver.OnSprintReleased();
            Assert.IsTrue(resolver.IsSprinting);
        }

        [Test]
        public void 入力無効を挟んだ押下は離しで反転しない()
        {
            // 無効中に押したShiftを有効化後に離しても反転しない（押下を見ていないため）
            var resolver = new SprintInputResolver(SprintInputMode.Toggle);
            resolver.ResyncHeld(true);
            resolver.OnSprintReleased();
            Assert.IsTrue(resolver.IsSprinting);
        }

        [Test]
        public void ホールドは押下中だけ走る()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Hold);
            Assert.IsFalse(resolver.IsSprinting);
            resolver.OnSprintPressed();
            resolver.OnOtherButtonPressed();
            Assert.IsTrue(resolver.IsSprinting);
            resolver.OnSprintReleased();
            Assert.IsFalse(resolver.IsSprinting);
        }

        [Test]
        public void ホールドからトグルへ切り替えると走りONから始まる()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Hold);
            resolver.SetMode(SprintInputMode.Toggle);
            Assert.IsTrue(resolver.IsSprinting);
        }

        [Test]
        public void トグルからホールドへ切り替えると押下中かどうかに揃う()
        {
            var resolver = new SprintInputResolver(SprintInputMode.Toggle);
            resolver.SetMode(SprintInputMode.Hold);
            Assert.IsFalse(resolver.IsSprinting);
        }
    }
}
```

- [x] **Step 2: 実装を書く**

`SprintInputMode.cs`:
```csharp
namespace Client.Input
{
    public enum SprintInputMode
    {
        Toggle,
        Hold,
    }
}
```

`SprintInputResolver.cs`:
```csharp
namespace Client.Input
{
    /// <summary>
    /// 走りの押下列から走り状態を解決する。トグル/ホールドの判定はここだけに置く
    /// Resolves the sprint state from sprint key events; the toggle/hold rule lives only here
    /// </summary>
    public class SprintInputResolver
    {
        public bool IsSprinting { get; private set; }

        private SprintInputMode _mode;
        private bool _isHeld;
        private bool _isCleanTap;
        private bool _isToggleAllowed = true;

        public SprintInputResolver(SprintInputMode mode)
        {
            _mode = mode;
            IsSprinting = mode == SprintInputMode.Toggle;
        }

        // トグルへ入る瞬間は開始時と同じく走りから始める
        // Entering toggle starts sprinting, the same as at game start
        public void SetMode(SprintInputMode mode)
        {
            _mode = mode;
            _isCleanTap = false;
            IsSprinting = mode == SprintInputMode.Toggle || _isHeld;
        }

        public void SetToggleAllowed(bool allowed)
        {
            _isToggleAllowed = allowed;
        }

        public void OnSprintPressed()
        {
            _isHeld = true;
            _isCleanTap = true;
            if (_mode == SprintInputMode.Hold) IsSprinting = true;
        }

        // 押下中の他ボタンはShiftを修飾キーとして使った印
        // Another button during the press marks Shift as used as a modifier
        public void OnOtherButtonPressed()
        {
            _isCleanTap = false;
        }

        public void OnSprintReleased()
        {
            _isHeld = false;
            if (_mode == SprintInputMode.Hold)
            {
                IsSprinting = false;
                return;
            }

            // 単独タップかつ抑止外のときだけ反転する
            // Flip only for a clean tap outside suppression
            if (_isCleanTap && _isToggleAllowed) IsSprinting = !IsSprinting;
            _isCleanTap = false;
        }

        // 入力無効の間は押下を見ていないため、実値へ揃えてタップ判定を捨てる
        // Presses are unseen while input is disabled, so resync to the held value and drop the tap
        public void ResyncHeld(bool isHeld)
        {
            _isHeld = isHeld;
            _isCleanTap = false;
            if (_mode == SprintInputMode.Hold) IsSprinting = isHeld;
        }
    }
}
```

`SprintInputModePreference.cs`:
```csharp
using System;
using UniRx;
using UnityEngine;

namespace Client.Input
{
    /// <summary>
    /// 走りの操作方式を端末ごとに保存し、変化を通知する（言語設定と同じPlayerPrefs）
    /// Stores the sprint input mode per device and notifies changes (same PlayerPrefs as the language setting)
    /// </summary>
    public static class SprintInputModePreference
    {
        internal const string PreferenceKey = "SprintInputMode";
        private static readonly Subject<SprintInputMode> ChangedSubject = new();

        public static IObservable<SprintInputMode> OnChanged => ChangedSubject;

        public static SprintInputMode Current => Load();

        public static void SetMode(SprintInputMode mode)
        {
            if (Load() == mode) return;
            PlayerPrefs.SetString(PreferenceKey, mode.ToString());
            PlayerPrefs.Save();
            ChangedSubject.OnNext(mode);
        }

        private static SprintInputMode Load()
        {
            // 未保存は既定のトグル（ADR 0082）
            // Unsaved means the default toggle (ADR 0082)
            if (!PlayerPrefs.HasKey(PreferenceKey)) return SprintInputMode.Toggle;

            // TryParseは"2"等の数値文字列も通すため、定義域に閉じているかも確かめる
            // TryParse also accepts numeric strings like "2", so check the value is within the defined set
            var saved = PlayerPrefs.GetString(PreferenceKey);
            if (Enum.TryParse(saved, out SprintInputMode mode) && Enum.IsDefined(typeof(SprintInputMode), mode)) return mode;

            // 壊れた保存値は既定へ戻し、開発者ログに残す
            // A corrupt saved value falls back to the default and is logged for developers
            Debug.LogWarning($"[SprintInputModePreference] Unknown saved sprint mode '{saved}'. Using Toggle.");
            return SprintInputMode.Toggle;
        }
    }
}
```

`InputManager.cs` の `InputKey` に追加（`GetKeyUp` の下）:
```csharp
        // このキーに割り当てた実コントロールか（修飾キー判定で自分自身を除くため）
        // Whether the control is bound to this key (to exclude itself when detecting modifier use)
        public bool IsBoundControl(InputControl control)
        {
            return _inputAction.controls.Contains(control);
        }
```
（`using UnityEngine.InputSystem;` は既存。`Contains` に `using System.Linq;` が無ければ追加する）

- [x] **Step 3: コンパイルしテストを実行**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "SprintInputResolverTest|SprintInputModePreferenceTest"`（`--filter-type regex`）
Expected: ErrorCount 0 / 10 PASS

- [x] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Input moorestech_client/Assets/Scripts/Client.Tests/Player/SprintInputResolverTest.cs*
git commit -m "feat: 走りのトグル/ホールド判定と方式の保存を追加"
```

### Task 2: StarterAssetsInputs への接続とメニュー中の抑止

**Files:**
- Modify: `moorestech_client/Assets/Dependencies/StarterAssets/InputSystem/StarterAssetsInputs.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/IUIState.cs`（`AllowsSprintToggle()` 追加）
- Modify: IUIState 実装すべて（`grep -rl ": IUIState\|, IUIState" moorestech_client/Assets/Scripts` で列挙。少なくとも `GameScreenState.cs`・`PlaceBlockState.cs`・`DeleteObjectState.cs`・`PlayerInventoryState.cs`・`SubInventoryState.cs`・`PauseMenuState.cs`・`ChallengeListState.cs`・`ResearchTreeState.cs`・`DebugBlockInfoState.cs`・`TrainHUDScreenState.cs`・`BuildMenuState.cs`・`SkitState.cs`、テスト用 Fake の IUIState 実装も）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/UIStateControl.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Player/PlayerObjectController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/Fakes/FakePlayerObjectController.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Player/SprintToggleInputTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateSprintToggleDeclarationTest.cs`（前例 `Client.Tests/UIState/UIStateMovementLockDeclarationTest.cs` と同じ骨格の全件表テスト）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Player/PlayerMovementUiLockTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/Player/TextInputMovementLockTest.cs`（Setup でホールドを明示）

**Interfaces:**
- Consumes: Task 1 の `SprintInputResolver`・`SprintInputModePreference`・`InputKey.IsBoundControl`
- Produces:
  - `IUIState.AllowsSprintToggle() : bool` — true は `GameScreenState`・`PlaceBlockState`・`DeleteObjectState`（DeleteBar）のみ。他は false
  - `IPlayerObjectController.SetSprintToggleAllowed(bool allowed)`
  - `StarterAssetsInputs.SetSprintToggleAllowed(bool allowed)`

- [x] **Step 1: テストを書く** — `SprintToggleInputTest.cs`（`PlayerMovementUiLockTest` の Setup を踏襲。InputTestFixture）

```csharp
using Client.Input;
using Client.Tests.Common;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Player
{
    public class SprintToggleInputTest : InputTestFixture
    {
        private GameObject _root;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private StarterAssetsInputs _inputs;
        private bool _hadSavedMode;
        private string _savedMode;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            TestReflection.ResetInputManagerCache();
            _ = InputManager.Player;
            InputSystem.Update();
            // 開発者自身の設定を壊さないよう退避してから既定（未保存）にする
            // Stash the developer's own setting, then start from the unsaved default
            _hadSavedMode = PlayerPrefs.HasKey(SprintInputModePreference.PreferenceKey);
            _savedMode = PlayerPrefs.GetString(SprintInputModePreference.PreferenceKey);
            PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);

            _root = new GameObject("SprintToggleInputTest");
            _inputs = _root.AddComponent<StarterAssetsInputs>();
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(_root);
            if (_hadSavedMode) PlayerPrefs.SetString(SprintInputModePreference.PreferenceKey, _savedMode);
            else PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void 既定トグルで開始時から走りShift単独タップで歩きになる()
        {
            Assert.IsTrue(_inputs.sprint);
            Press(_keyboard.leftShiftKey);
            _inputs.SprintInput(true);
            Release(_keyboard.leftShiftKey);
            _inputs.SprintInput(false);
            Assert.IsFalse(_inputs.sprint);
        }

        [Test]
        public void Wを押したままShift単独タップで反転する()
        {
            // 先に押されていたキーは修飾判定に数えない（押下〜離しの間に押されたボタンだけを見る）
            Press(_keyboard.wKey);
            Press(_keyboard.leftShiftKey);
            _inputs.SprintInput(true);
            Release(_keyboard.leftShiftKey);
            _inputs.SprintInput(false);
            Assert.IsFalse(_inputs.sprint);
        }

        [Test]
        public void Shift押下中にWを押し始めたら反転しない()
        {
            Press(_keyboard.leftShiftKey);
            _inputs.SprintInput(true);
            Press(_keyboard.wKey);
            Release(_keyboard.leftShiftKey);
            _inputs.SprintInput(false);
            Assert.IsTrue(_inputs.sprint);
        }

        [Test]
        public void ShiftとRの同時押しでは反転しない()
        {
            Press(_keyboard.leftShiftKey);
            _inputs.SprintInput(true);
            Press(_keyboard.rKey);
            Release(_keyboard.rKey);
            Release(_keyboard.leftShiftKey);
            _inputs.SprintInput(false);
            Assert.IsTrue(_inputs.sprint);
        }

        [Test]
        public void Shift押下中のクリックでは反転しない()
        {
            Press(_keyboard.leftShiftKey);
            _inputs.SprintInput(true);
            Click(_mouse.leftButton);
            Release(_keyboard.leftShiftKey);
            _inputs.SprintInput(false);
            Assert.IsTrue(_inputs.sprint);
        }

        [Test]
        public void メニュー中はタップで反転しない()
        {
            _inputs.SetSprintToggleAllowed(false);
            _inputs.SprintInput(true);
            _inputs.SprintInput(false);
            Assert.IsTrue(_inputs.sprint);
        }

        [Test]
        public void 入力無効を挟んでも走り状態を維持する()
        {
            _inputs.SprintInput(true);
            _inputs.SprintInput(false);
            Assert.IsFalse(_inputs.sprint);
            _inputs.SetInputEnable(false);
            Assert.IsFalse(_inputs.sprint);
            _inputs.SetInputEnable(true);
            Assert.IsFalse(_inputs.sprint);
        }

        [Test]
        public void 設定をホールドへ変えると押下中だけ走る()
        {
            SprintInputModePreference.SetMode(SprintInputMode.Hold);
            Assert.IsFalse(_inputs.sprint);
            _inputs.SprintInput(true);
            Assert.IsTrue(_inputs.sprint);
            _inputs.SprintInput(false);
            Assert.IsFalse(_inputs.sprint);
        }
    }
}
```
（`Click` が InputTestFixture に無ければ `Press(_mouse.leftButton); Release(_mouse.leftButton);` に置き換える。テスト中の `onAnyButtonPress` は `Press` で同期発火する）

- [x] **Step 2: StarterAssetsInputs を書き換える**

フィールド・初期化:
```csharp
		public bool sprint;
		private SprintInputResolver _sprintResolver;
		private IDisposable _sprintModeSubscription;
		private IDisposable _otherButtonSubscription;

		private void Awake()
		{
			// 方式の現在値で解決器を作り、設定変更を購読する
			// Build the resolver with the current mode and subscribe to setting changes
			_sprintResolver = new SprintInputResolver(SprintInputModePreference.Current);
			_sprintModeSubscription = SprintInputModePreference.OnChanged.Subscribe(mode =>
			{
				_sprintResolver.SetMode(mode);
				ApplySprint();
			});
			ApplySprint();
		}

		private void OnEnable()
		{
			// Sprint以外のボタン押下はShiftを修飾キーとして使った印
			// Any non-sprint button press marks Shift as used as a modifier
			_otherButtonSubscription = InputSystem.onAnyButtonPress.Call(control =>
			{
				if (InputManager.Player.Sprint.IsBoundControl(control)) return;
				_sprintResolver.OnOtherButtonPressed();
			});
		}

		private void OnDisable()
		{
			_otherButtonSubscription?.Dispose();
		}

		private void OnDestroy()
		{
			_sprintModeSubscription.Dispose();
		}

		public void SetSprintToggleAllowed(bool allowed)
		{
			_sprintResolver.SetToggleAllowed(allowed);
		}

		private void ApplySprint()
		{
			sprint = inputEnable && _sprintResolver.IsSprinting;
		}
```
`SetInputEnable` の `sprint = enable && InputManager.Player.Sprint.GetKey;` を置換:
```csharp
			_sprintResolver.ResyncHeld(enable && InputManager.Player.Sprint.GetKey);
			ApplySprint();
```
`SprintInput` を置換:
```csharp
		public void SprintInput(bool newSprintState)
		{
            if (!inputEnable) return;
			if (newSprintState) _sprintResolver.OnSprintPressed();
			else _sprintResolver.OnSprintReleased();
			ApplySprint();
		}
```
using 追加: `using System;` `using UniRx;`（`UnityEngine.InputSystem` は `#if` 外でも必要なので `onAnyButtonPress` を使う箇所の using を `#if ENABLE_INPUT_SYSTEM` の外へ出す）。asmdef（`StarterAssetsInputs`）に `UniRx` 参照が無ければ追加する。`InputSystem.onAnyButtonPress.Call` は InputSystem 1.1+ の `InputControlExtensions` 拡張（`using UnityEngine.InputSystem.Utilities;` が要る場合あり）。
ファイルが200行を超えそうなら解決器との接続部を `SprintInputBinding`（同ディレクトリの純C#クラス）へ切り出す。

- [x] **Step 3: IUIState 宣言とプッシュ経路**

`IUIState.cs` に追加:
```csharp
        /// <summary>
        /// 滞在中にShift単独タップで走りのトグルを反転してよいか。trueはゲーム画面・設置・削除だけ（メニュー中のShiftは一括移動用）
        /// Whether a lone Shift tap may flip the sprint toggle here; true only for the game screen, placement and deletion (Shift in menus is for bulk moves)
        /// </summary>
        public bool AllowsSprintToggle();
```
`GameScreenState`・`PlaceBlockState`・`DeleteObjectState` は `public bool AllowsSprintToggle() { return true; }`、その他の全実装は `return false;`（`LocksPlayerMovement` の直下に置く）。

`UIStateControl.ApplyMovementLock` を拡張（メソッド名を `ApplyPlayerInputPolicy` に改名し呼び出し2か所も変更）:
```csharp
        private void ApplyPlayerInputPolicy(IUIState uiState)
        {
            _playerObjectController.SetMovementLock(PlayerMovementLockReason.Ui, uiState.LocksPlayerMovement());
            _playerObjectController.SetSprintToggleAllowed(uiState.AllowsSprintToggle());
        }
```
`IPlayerObjectController` に `public void SetSprintToggleAllowed(bool allowed);`、`PlayerObjectController` に実装:
```csharp
        // メニュー中のShiftで走りを反転させないための宣言を入力へ渡す
        // Pass the screen's declaration to input so Shift in menus never flips sprint
        public void SetSprintToggleAllowed(bool allowed)
        {
            _starterAssetsInputs.SetSprintToggleAllowed(allowed);
        }
```
`_starterAssetsInputs` は `Initialize` 冒頭で `_starterAssetsInputs = controller.GetComponent<StarterAssetsInputs>();`（同一GameObjectに既存。Prefab変更不要）。`FakePlayerObjectController` は呼び出し値を記録するだけの実装を足す。

- [x] **Step 4: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SprintToggleInputTest|SprintInputResolverTest|SprintInputModePreferenceTest|PlayerMovementUiLockTest|TextInputMovementLockTest|PlayerFlightMotionTest|UIState"`
Expected: ErrorCount 0 / 全 PASS。

既存の `PlayerMovementUiLockTest`・`TextInputMovementLockTest` は**無条件に** Setup で保存値を退避（`SprintToggleInputTest` と同じ書き方）し、`StarterAssetsInputs` を AddComponent する**前に** `SprintInputModePreference.SetMode(SprintInputMode.Hold)` を呼び、TearDown で復元する。理由: 既定トグルでは `IsSprinting` 初期値が true のため、「ダッシュキーを押していれば再開」の `Assert.IsTrue(_inputs.sprint)` がホールドの再同期が壊れていても通ってしまい、Req4 を検証できないため。

`UIStateSprintToggleDeclarationTest`: `UIStateMovementLockDeclarationTest` を複製し、期待表を `GameScreen=true, PlaceBlock=true, DeleteBar=true, それ以外=false`、検査メソッドを `AllowsSprintToggle()` に変える（画面を足すと突き合わせが落ちて宣言の見直しを強制する）。

- [x] **Step 5: コミット**

```bash
git add moorestech_client/Assets
git commit -m "feat: 走りトグルを入力へ接続しメニュー中の反転を抑止"
```

### Task 3: 設定画面に走りの方式を追加（Web UI ⇄ Unity）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/SprintInputModeTopic.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Settings/SettingsActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`（topic と action の登録）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/Settings/SprintInputModeContractTest.cs`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`、`moorestech_web/webui/src/bridge/contract/payloadTypes.ts`、`moorestech_web/webui/src/bridge/contract/validators.ts`、`moorestech_web/webui/src/bridge/transport/protocol.ts`、`moorestech_web/webui/src/bridge/transport/actionContract.ts`
- Create: `moorestech_web/webui/src/features/settings/SprintModeSelect.tsx`、`moorestech_web/webui/src/features/settings/SprintModeSelect.test.ts`
- Modify: `moorestech_web/webui/src/features/settings/index.ts`、`moorestech_web/webui/src/features/pauseMenu/PauseMenuPanel.tsx`、`moorestech_web/webui/src/features/pauseMenu/PauseMenuPanel.test.ts`（モック追加）
- Modify: `Localization/localization.csv`（3行追加）→ `npm run gen:i18n` で `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` 再生成

**Interfaces:**
- Consumes: Task 1 の `SprintInputModePreference`・`SprintInputMode`
- Produces:
  - topic `settings.sprintInputMode` payload `{ mode: "toggle" | "hold" }`
  - action `settings.setSprintInputMode` payload `{ mode: "toggle" | "hold" }`、不明値は `ActionResult.Fail("unknown_sprint_mode")`

- [ ] **Step 1: C# 側**

`SprintInputModeTopic.cs`（`LocalizationTopic` と同形。`EnsureRegistered` は不要）:
```csharp
using System;
using Client.Input;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// 走りの操作方式を snapshot と変化時 event で配信する
    /// Publishes the sprint input mode as a snapshot and on each change
    /// </summary>
    public class SprintInputModeTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "settings.sprintInputMode";

        private readonly IDisposable _modeSubscription;

        public SprintInputModeTopic(WebSocketHub hub)
        {
            _modeSubscription = SprintInputModePreference.OnChanged.Subscribe(_ => hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _modeSubscription.Dispose();
        }

        private static string BuildJson()
        {
            return WebUiJson.Serialize(new SprintInputModeData { Mode = SprintInputModeWire.ToWire(SprintInputModePreference.Current) });
        }

        private class SprintInputModeData
        {
            public string Mode;
        }
    }
}
```

`SettingsActions.cs`（`SprintInputModeWire` も同ファイルに置く。wire 表記は小文字）:
```csharp
using Client.Input;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    public static class SettingsActions
    {
        public static void Register(WebSocketHub hub)
        {
            hub.RegisterAction(new SetSprintInputModeActionHandler());
        }
    }

    /// <summary>
    /// Webからの走り方式の変更を保存窓口へ渡す
    /// Passes a sprint-mode change from the web to the preference store
    /// </summary>
    public class SetSprintInputModeActionHandler : IActionHandler
    {
        public string ActionType => "settings.setSprintInputMode";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var wire = payload?["mode"]?.ToString();

            // 不明値は失敗契約で返し、保存値は変えない
            // An unknown value returns the failure contract and leaves the saved value as is
            if (!SprintInputModeWire.TryFromWire(wire, out var mode)) return UniTask.FromResult(ActionResult.Fail("unknown_sprint_mode"));
            SprintInputModePreference.SetMode(mode);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    public static class SprintInputModeWire
    {
        public static string ToWire(SprintInputMode mode)
        {
            return mode == SprintInputMode.Toggle ? "toggle" : "hold";
        }

        public static bool TryFromWire(string wire, out SprintInputMode mode)
        {
            mode = SprintInputMode.Toggle;
            if (wire == "toggle") return true;
            mode = SprintInputMode.Hold;
            return wire == "hold";
        }
    }
}
```
（`SprintInputModeTopic` から `Client.WebUiHost.Game.Actions.SprintInputModeWire` を using する）

`WebUiGameBinder.cs`: `LocalizationActions.Register(hub);` の直下に `SettingsActions.Register(hub);`、topic 登録群の末尾に `hub.RegisterTopic(SprintInputModeTopic.TopicName, new SprintInputModeTopic(hub));`。

- [ ] **Step 2: C# 契約テスト** — `SprintInputModeContractTest.cs`（`LocalizationLanguageSelectionContractTest` 同形）

```csharp
using System.Threading.Tasks;
using Client.Input;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi.Settings
{
    public class SprintInputModeContractTest
    {
        private bool _hadSavedMode;
        private string _savedMode;

        // 開発者自身の設定を退避し、既定（未保存）から始める
        // Stash the developer's own setting and start from the unsaved default
        [SetUp]
        public void SetUp()
        {
            _hadSavedMode = PlayerPrefs.HasKey(SprintInputModePreference.PreferenceKey);
            _savedMode = PlayerPrefs.GetString(SprintInputModePreference.PreferenceKey);
            PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadSavedMode) PlayerPrefs.SetString(SprintInputModePreference.PreferenceKey, _savedMode);
            else PlayerPrefs.DeleteKey(SprintInputModePreference.PreferenceKey);
        }

        [Test]
        public async Task 登録済みアクションでホールドを保存できる()
        {
            var hub = new WebSocketHub();
            SettingsActions.Register(hub);
            var handler = hub.ResolveAction("settings.setSprintInputMode");

            var result = await handler.ExecuteAsync(JObject.Parse("{\"mode\":\"hold\"}"));

            Assert.IsTrue(result.Ok);
            Assert.AreEqual(SprintInputMode.Hold, SprintInputModePreference.Current);
        }

        [Test]
        public async Task 不明な値は失敗し保存値を変えない()
        {
            var hub = new WebSocketHub();
            SettingsActions.Register(hub);
            var handler = hub.ResolveAction("settings.setSprintInputMode");

            var result = await handler.ExecuteAsync(JObject.Parse("{\"mode\":\"run\"}"));

            Assert.IsFalse(result.Ok);
            Assert.AreEqual("unknown_sprint_mode", result.Error);
            Assert.AreEqual(SprintInputMode.Toggle, SprintInputModePreference.Current);
        }
    }
}
```
（`ResolveAction` の戻り型・`ActionResult.Error` の名前は `LocalizationLanguageSelectionContractTest.cs:150` 付近の実例に合わせる）

- [ ] **Step 3: Web 側**

`schemas/ui.ts`（`LocalizationDataSchema` の直下）:
```ts
// 走りの操作方式。既定のトグルはUnity側が決める
// Sprint input mode; Unity decides the default toggle
export const SprintInputModeDataSchema = z.object({
  mode: z.enum(["toggle", "hold"]),
});
```
`payloadTypes.ts`: `export type SprintInputModeData = z.infer<typeof SprintInputModeDataSchema>;` と `export type SprintInputMode = SprintInputModeData["mode"];`（import 追加。前例 `FilterSplitterMode`）
`protocol.ts`: `Topics` に `sprintInputMode: "settings.sprintInputMode",`、payload マップに `[Topics.sprintInputMode]: SprintInputModeData;`
`validators.ts`: `[Topics.sprintInputMode]: SprintInputModeDataSchema,`
`actionContract.ts`: payload 型マップに `"settings.setSprintInputMode": { mode: SprintInputMode };`（`SprintInputMode` を payloadTypes から import。手書きリテラルは置かない）、一覧に `"settings.setSprintInputMode",`

`localization.csv`（`ui.settings.language` の直下。列: key,Source,english,japanese,german,korean）:
```
ui.settings.sprintMode,Sprint,Sprint,走り,Sprinten,달리기
ui.settings.sprintModeToggle,Toggle,Toggle,トグル,Umschalten,토글
ui.settings.sprintModeHold,Hold,Hold,ホールド,Halten,누르기
```
→ `cd moorestech_web/webui && npm run gen:i18n`

`SprintModeSelect.tsx`:
```tsx
import { Title } from "@mantine/core";
import { dispatchAction, Topics, useTopic, type SprintInputMode } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { ModeSwitch } from "@/shared/ui";

// 走りの操作方式の択一。現在値はsettings.sprintInputModeトピックを正とする
// Exclusive selector for the sprint input mode; the settings.sprintInputMode topic is authoritative
export function SprintModeSelect() {
  const { t } = useI18n();
  const mode = useTopic(Topics.sprintInputMode)?.mode;
  const label = t(L.ui.settings.sprintMode);

  return (
    <section aria-label={label}>
      <Title order={2}>{label}</Title>
      {mode !== undefined && (
        <ModeSwitch
          value={mode}
          options={[
            { value: "toggle", label: t(L.ui.settings.sprintModeToggle), testId: "sprint-mode-option-toggle" },
            { value: "hold", label: t(L.ui.settings.sprintModeHold), testId: "sprint-mode-option-hold" },
          ]}
          onChange={(next) => void dispatchAction("settings.setSprintInputMode", { mode: next as SprintInputMode })}
          orientation="horizontal"
          testId="sprint-mode-select"
        />
      )}
    </section>
  );
}
```
（`ModeSwitch` の `orientation` が取りうる値と `onChange` の型は `src/shared/ui` の定義に合わせる。キャストは前例 `FilterSplitterInventory.tsx` の `mode as FilterSplitterMode` と同形。`SprintInputMode` が `@/bridge` から再輸出されていなければ `FilterSplitterMode` と同じ経路で輸出する）

`index.ts`: `export { SprintModeSelect } from "./SprintModeSelect";`
`PauseMenuPanel.tsx` の settings ケース:
```tsx
      case "settings": return <PauseMenuSubPage title={t(L.ui.pauseMenu.settings)}><LanguageSelect /><SprintModeSelect /></PauseMenuSubPage>;
```
`PauseMenuPanel.test.ts` の `@/features/settings` モックに `SprintModeSelect: () => createElement("mock-sprint-mode-select", { "data-testid": "sprint-mode-select" }),` を追加。

`SprintModeSelect.test.ts`（`LanguageSelect.test.ts` のモック構成を踏襲）: (1) topic `{mode:"toggle"}` で ModeSwitch の `value` が `"toggle"`、(2) `onChange("hold")` で `dispatchAction` が `("settings.setSprintInputMode", {mode:"hold"})` で呼ばれる、(3) topic 未着（`useTopic` が undefined）では ModeSwitch を描かない、の3ケース。

- [ ] **Step 4: 検証**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SprintInputModeContractTest|LocalizationLanguageSelectionContractTest"`
Run: `cd moorestech_web/webui && npx tsc --noEmit && npx vitest run src/features/settings src/features/pauseMenu src/bridge src/shared/i18n`
Expected: すべて PASS（`localizationKeysFreshness`・`allScreensI18n` テスト含む）

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets moorestech_web/webui/src Localization/localization.csv
git commit -m "feat: 設定画面に走りのトグル/ホールド切替を追加"
```

### Task 4: unityプレイ録画テストで通し確認

**Files:**
- Create: 録画シナリオ（`unity-playmode-recorded-playtest` スキルの DSL 置き場の規約に従う）

- [ ] **Step 1:** `unity-playmode-recorded-playtest` スキルで PlayMode を起動し、次を録画で確認する: (a) 起動直後 W 押しで走り速度、(b) Shift 単独タップで歩き速度に落ちる、(c) PlaceBlock 中に Shift+R で縦回転しても走り/歩きが変わらない、(d) インベントリを開いて Shift タップしても変わらない、(e) ポーズメニュー→設定でホールドを選ぶと Shift 押下中だけ走る。速度はプレイヤーの水平移動量（m/s）をログで観測値として残す。
- [ ] **Step 2:** 結果（result.json・録画パス・観測値）を報告ファイルに記録してコミットする。

### Task 5（必須・最終）: 全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー指摘の反映が判定経路・条件式に触れたら Task 4 の録画テストを反映後に再実施する。

### Task 6（必須・最終）: PR作成

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0082-sprint-toggle-mode-default.md`（裁定1〜8・agent前提）
- 裁定: `.decisions/2026-10-16-走りはShiftを押すたびに切り替えるトグル式を既定にしホールド式を設定で選べる.md` ほか同日の走り関連6件
- agent前提（planning）: 走り判定は `Client.Input/Sprint/SprintInputResolver` 1か所。`ThirdPersonController`（421行・既存超過）を避け、`PlayerObjectController` は同一GameObjectの `StarterAssetsInputs` を `GetComponent` で得てプッシュする（Prefab 変更を避けるため）
- agent前提（planning）: 「他のキー・クリック」の検出は `InputSystem.onAnyButtonPress`（Sprint に割り当てたコントロールは除外）。Shift より先に押されていたキーは数えない（押下〜離しの間に「押された」ボタンだけを見る）
- agent前提（planning）: メニュー判定は IUIState 宣言 `AllowsSprintToggle()`（前例 `LocksPlayerMovement`）。true は GameScreen・PlaceBlock・DeleteBar。移動を止める画面（ポーズ・列車HUD・スキット・デバッグ）は入力自体が無効なので false で差は出ない
- agent前提（planning）: 既存の `PlayerMovementUiLockTest`・`TextInputMovementLockTest` はホールド挙動の検証なので、落ちたら Setup でホールドを明示する
- unityプレイ録画テスト: 入力・移動速度・UIステートを跨ぐ挙動なので Task 4 に含める
- agent前提（planning）: `onAnyButtonPress` は1イベントで複数ボタンが同時に変化すると最初の1つしか通知しない。キーボードはキーごとに別イベントなので実害なし、パッドの同フレーム同時押しの取りこぼしは受け入れる（パッドは正式対応外）
- agent前提（planning）: `Mobile/Scripts/CanvasInputs/UICanvasControllerInput.cs` の `SprintInput(bool)` 呼び出しはトグルの意味に変わる。モバイルは出荷対象外なので手を入れない
- Phase 2.6 裁定（シミュレーター予測→ユーザー承認 2026-10-16）: Web側の走り方式の型はスキーマから導出した `SprintInputMode` 1本にする（選択「1本にする」）。軽微4件のうち保存値の未定義値（`Enum.IsDefined`）だけ拾う（選択「保存値の未定義値を弾く」）
- Phase 2.6 前提宣言（シミュレーター予測・確信高。ユーザー拒否権つき）: 型は Interface 層へ切り出さず Client.Input に置く（クライアント内参照のみ・前例 Localize）／`t()` の欠損分岐は足さない（翻訳器が `[!key]` を返し、鮮度テストが担保）／ADR 0082 の agent前提を「判定規則は Resolver、書き込みは ApplySprint」へ修正済み
- review 反映（user-simulator review・Fable判事）: 既存ホールドテストを無条件にホールド化（既定トグルでは誤って通るため）、`AllowsSprintToggle` の全件表テスト追加、Wを押したままのタップのテスト追加、テストの PlayerPrefs を退避・復元に統一
