# デバッグ用フライモード Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** デバッグシートで許可したときだけ、スペース4連打で重力を切って空中を高速移動でき、スペース2連打で戻れるフライモードを追加する。

**Architecture:** 速度と上下の物理は `ThirdPersonController`（StarterAssets・独立asmdef）側の新クラス `PlayerFlightMotion` が持ち、`ThirdPersonController` には「飛行中か」と「上下入力」を外から受け取る `SetFlying` / `SetFlightVerticalInput` だけを足す。連打の判定、デバッグトグルの参照、Q/E の読み取りは `Client.Game` 側の `PlayerFlyModeController` が持ち、`PlayerObjectController.Update` から毎フレーム駆動する（基盤にデバッグの語彙を持ち込まない）。建築の設置高さ・乗車など既存の Q/E の用途は一切改変しない。

**Tech Stack:** Unity 6000.3 / C# / Unity Input System（`InputManager`・`HybridInput`）/ NUnit（EditMode, `InputTestFixture`）/ プレイテストDSL（unityプレイ録画テスト）

## Requirements

設計ADR: `docs/adr/0075-debug-fly-mode-toggled-by-space-taps.md`

- R1: デバッグシートに `Fly mode (Space x4 / x2)` トグル（既定オフ・`AddBoolWithSave` で保存）がある。受入: シートに表示され、再起動後も値が残る。
- R2: トグルがオンのとき、スペースを押下間隔0.3秒以内で4回押すとフライモードに入る。受入: 4連打で飛行状態になる。2回目と3回目の間が0.3秒を超えたら入らない。
- R3: トグルがオフのときは4連打しても入らず、無視した理由を `Debug.Log` に出す。受入: 飛行状態にならない。ログ1行が出る。
- R4: フライ中にスペースを押下間隔0.3秒以内で2回押すと解除する。トグルのオン/オフは問わない。解除は縦速度0から通常の重力で落下する。受入: 2連打で通常状態に戻り、足場へ落下する。
- R5: フライ中の水平速度は `SprintSpeed × 2`、Shift 押下中は `SprintSpeed × 6`（現値 15 / 45 m/s）。WASD はカメラの水平向きに沿って動き、カメラの上下の傾きは無視する。キーを離すとその場で止まる（加減速なし）。受入: 速度の単体テストと、録画テストでの移動距離。
- R6: フライ中は E で上昇、Q で下降し、速度は水平と同じ（Shift で×6も同じ）。同時押しは相殺して0。受入: 単体テストと、録画テストで E 1秒で上昇。
- R7: フライ中は重力とジャンプを止め、入った瞬間に縦速度を0にしてその場に浮く。受入: 録画テストで、入力なしのとき高度が保たれる。
- R8: 着地してもフライは解除しない。受入: 2連打以外で通常状態に戻る経路が無い（コード上）。
- R9: 地形・ブロック・列車との当たり判定は維持する（CharacterController.Move を通す）。受入: 移動が `_controller.Move` 経由であることをコードで確認できる。
- R10: 移動ロック中（UI表示・乗車中）は連打を数えない（途中の連打は捨てる）。フライ中の移動も上下も止まる。ロックが外れたら、フライ状態のまま再開する。受入: 単体テスト。
- R11: フライの入り・解除を `Debug.Log` に出す。HUD は出さない。
- R12: y<-50 の落下復帰はフライ中も解除後も既存のまま働く（`PlayerObjectController.LateUpdate` を変更しない）。
- R13: フライ状態は保存しない。起動直後は常に通常移動。トグルをオフにしても飛行中は解除しない。
- やらないこと: 建築モードの設置高さ（Q/E）、E の乗車、`Playable.Ride` の入力定義の改変。サーバー・プロトコルの変更。すり抜け（ノークリップ）。HUD 表示。`ThirdPersonController` の200行分割リファクタ（判断記録 J3）。

## Global Constraints

- 1ファイル200行未満。新規ファイルは必ず200行未満（既存で超過している `ThirdPersonController.cs` は J3 のとおり分割しない）
- partial 禁止、`Func<>` 禁止、デフォルト引数禁止、try-catch 禁止、C# `event Action` によるイベント新設禁止
- 単純な getter/setter プロパティ禁止（`{ get; private set; }` は可）。値の Set は `public void SetHoge(...)`
- 主要処理に日本語→英語の2行コメント（各1行）。`#region Internal` はメソッド内ローカル関数用途だけ
- `.meta` を手で作らない（Unity の自動生成をコミットする）
- fail-closed で無視する経路（トグルオフでの4連打）は理由をログへ出す
- 速度の倍率: 通常 `2`、Shift `6`（`SprintSpeed` に掛ける。ADR 0075）。連打の間隔: `0.3` 秒。発動 `4` 回、解除 `2` 回
- `.cs` を変更したら `uloop compile --project-path ./moorestech_client` を通す（ErrorCount 0）
- 作業ディレクトリ: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/debug-fly-mode`（最初に `pwd` で確認）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 7項目すべて前例どおり。検査4は「重力を打ち消す受動案」と比較して飛行中のみ重力を呼ばない分岐を採用。死活表で死ぬ操作0
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱2・第3バケツ1 / 弱2件は J8・J9 で現状維持と判断（ユーザー委任による）。第3バケツは CinematicCameraController の自由移動（本PR外）

---

## File Structure

| ファイル | asmdef | 責務 | 新規/変更 |
|---|---|---|---|
| `moorestech_client/Assets/Dependencies/StarterAssets/ThirdPersonController/Scripts/PlayerFlightMotion.cs` | ThirdPersonController | 飛行中フラグ・上下入力・飛行速度と縦速度の算出（発動条件は知らない） | 新規 |
| `moorestech_client/Assets/Dependencies/StarterAssets/ThirdPersonController/Scripts/ThirdPersonController.cs` | ThirdPersonController | `SetFlying` / `SetFlightVerticalInput` の受け口と、Update/Move の飛行分岐 | 変更 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Player/FlyMode/SpaceTapSequence.cs` | Client.Game | 押下時刻から連続回数を数える純粋クラス | 新規 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Player/FlyMode/PlayerFlyModeController.cs` | Client.Game | スペース連打の判定・トグル参照・Q/E 読み取り・`ThirdPersonController` へのプッシュ・ログ | 新規 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Player/PlayerObjectController.cs` | Client.Game | `PlayerFlyModeController` の生成と毎フレーム駆動、操作可否の判定の共通化 | 変更 |
| `moorestech_client/Assets/Scripts/Client.Common/DebugConst.cs` | Client.Common | `FlyModeLabel` / `FlyModeKey` 定数 | 変更 |
| `moorestech_client/Assets/Scripts/Client.DebugSystem/DebugSheet/DebugSheetController.cs` | Client.DebugSystem | トグル1行追加 | 変更 |
| `moorestech_client/Assets/Scripts/Client.Tests/Player/PlayerFlightMotionTest.cs` | Client.Tests | 速度・縦速度の単体テスト | 新規 |
| `moorestech_client/Assets/Scripts/Client.Tests/Player/PlayerFlyModeControllerTest.cs` | Client.Tests | 連打判定・トグル・ロックの単体テスト | 新規 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/debug-fly-mode.cs` | （execute-dynamic-code） | unityプレイ録画テストのシナリオ | 新規 |

同じ役割の既存部品の探索結果:

| 新規 | 同役割の既存 | 判断 |
|---|---|---|
| `PlayerFlightMotion` | `PlayerPlatformFollowService`（同フォルダ。`ThirdPersonController` から処理を委譲された plain class） | 新規。委譲クラスを置く形はこの前例に合わせる。飛行の物理を担う既存部品は無い |
| `SpaceTapSequence` | 連打・ダブルタップ判定は repo 内に無い（`grep -rn "DoubleTap\|TapCount\|MultiTap"` で0件） | 新規 |
| `PlayerFlyModeController` | デバッグトグルで挙動を変える部品（`MiningProgressState` の `DebugParameters.GetValueOrDefaultBool(...)` の読み取り） | 読み方は同じ `DebugParameters.GetValueOrDefaultBool` を呼ぶ |
| WASD+Q/E+Shift の自由移動 | `Client.DebugSystem/CinematicCameraController.cs`（デバッグ用カメラの自由移動。カメラの Transform を直接動かし、legacy `UnityEngine.Input`、Q/E 同時押しは Q 優先） | 呼ばない。動かす対象がカメラの Transform で、当たり判定（R9）も移動ロック（R10）も通らないため。共通化は本PR外の提案に回す（判断記録 J10） |
| Q/E 読み取り | `CommonBlockPlaceDragState.UpdateHeightOffsetByInput`（`HybridInput.GetKeyDown(KeyCode.Q/E)`） | 同じ `HybridInput` を呼ぶ（押しっぱなしなので `GetKey`）。InputManager へのアクション追加はしない（J4） |

### データフロー

```
（スペース押下: InputManager.Player.Jump.GetKeyDown）→ PlayerFlyModeController（連打判定・トグル参照）
   → ThirdPersonController.SetFlying / SetFlightVerticalInput ［PlayerFlightMotion の状態］
   → ThirdPersonController.Update/Move（重力スキップ・速度算出）→ CharacterController.Move（当たり判定つき）
```

`PlayerFlyModeController` は `ThirdPersonController` への**書き手**、`ThirdPersonController` は読み手。既存の通常移動経路は `IsFlying == false` のとき1行も変わらない。

### 配置と前例

| # | 項目 | 配置先 | 機構 | 前例 / 判定 |
|---|---|---|---|---|
| 1 | `PlayerFlightMotion` | ThirdPersonController asmdef | plain class へ委譲 | `PlayerPlatformFollowService`（同フォルダ）。飛行の物理は移動基盤の語彙で、デバッグの語彙を含まない → ok |
| 2 | `ThirdPersonController.SetFlying` / `SetFlightVerticalInput` | 同上（既存クラスへの追加） | 外からの `SetHoge` プッシュ | layer-map「汎用基盤コンポーネントへの状態伝達」、既存 `SetControllable` → ok |
| 3 | `SpaceTapSequence` | Client.Game/InGame/Player/FlyMode | 純粋クラス | 前例なし（連打判定は repo 初）→ 新規 |
| 4 | `PlayerFlyModeController` | 同上 | `ManualUpdate` 駆動（購読ではない） | 駆動側 `PlayerObjectController` が操作可否の正を持つ。`PlaceSystemStateController.ManualUpdate` と同じ「呼び出し側が毎フレーム駆動」→ ok |
| 5 | `PlayerObjectController.Update` / `IsControllable` | Client.Game（既存クラスへの追加） | MonoBehaviour Update | 同クラスの既存 `LateUpdate` と同じ `isRuntimeStarted` ガード → ok |
| 6 | `DebugConst.FlyModeLabel/Key`・シートのトグル | Client.Common / Client.DebugSystem | `AddBoolWithSave` + `DebugParameters` | `TrainUnitDebugOverlayKey` 等。サーバーは参照しないので `Common.Debug.DebugParameterKeys` には置かない → ok |
| 7 | 録画シナリオ | `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/` | execute-dynamic-code | 同ディレクトリの既存シナリオ → ok |

検査4（機構選択）: 飛行中は `JumpAndGravity` を呼ばない（既存の重力処理を飛行中だけ止める＝能動介入）。受動的統合案として「重力処理はそのまま動かし、`PlayerFlyModeController` が毎フレーム重力を打ち消す上向き速度を足す」案と比較した。受動案では、`Grounded` 判定・ジャンプのタイムアウト・落下アニメの遷移が重力と同時に走り続ける。毎フレーム打ち消しても縦速度が `_terminalVelocity` の上限や `Grounded` 時の -2 リセットと干渉し、ホバー（R7）が成立しない。さらに打ち消しのために `_verticalVelocity` を外へ公開する必要があり、基盤の内部状態が漏れる。よって、飛行中だけ重力を呼ばない分岐を `ThirdPersonController` 内に置く。通常時（`IsFlying == false`）の挙動は変わらない。

### 死活表（同じ移動機構にぶら下がる操作）

| 操作 | 本 plan 後 | 根拠 |
|---|---|---|
| 歩き・Shift 走り | 生きる | `IsFlying == false` では `targetSpeed` と加減速の式が従来どおり |
| ジャンプ（スペース） | 生きる（フライ中のみ無効） | 通常時は `JumpAndGravity` を従来どおり呼ぶ。1回目のスペースは従来どおりジャンプする |
| 重力・落下・落下アニメ | 生きる（フライ中のみ無効） | 同上 |
| Ctrl によるカメラ角固定・向きの回転 | 生きる | `Move` の回転処理は変更しない（飛行中も同じ処理を通る） |
| 動く足場の追従 | 生きる | `ApplyPlatformFollow` の呼び出しは変更しない |
| 落下復帰（y<-50） | 生きる | `PlayerObjectController.LateUpdate` は変更しない |
| UI 中の移動ロック | 生きる | `SetControllable` は不変。`ApplyMovementLock` は判定式を `IsControllable()` へ移しただけで、結果は同じ |
| 列車への乗車（E）・乗車追従 | 生きる | `Playable.Ride` と `PlayerRideFollow` は不変。フライ中に E を押すと、上昇と乗車が同時に起きる（ADR 0075 のユーザー裁定どおり） |
| 建築モードの設置高さ（Q/E） | 生きる | `CommonBlockPlaceDragState` は不変。フライ中は、上下移動と設置高さの変更が同時に起きる（同上） |

死ぬ操作・退化する操作は無い。

---

### Task 1: ThirdPersonController に飛行の受け口と物理を足す

**Files:**
- Create: `moorestech_client/Assets/Dependencies/StarterAssets/ThirdPersonController/Scripts/PlayerFlightMotion.cs`
- Modify: `moorestech_client/Assets/Dependencies/StarterAssets/ThirdPersonController/Scripts/ThirdPersonController.cs`（フィールド宣言部・`SetControllable` の直後・`Update`・`Move`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Player/PlayerFlightMotionTest.cs`

**Interfaces:**
- Consumes: なし
- Produces:
  - `StarterAssets.PlayerFlightMotion`（`public class`）: `bool IsFlying { get; private set; }`, `void SetFlying(bool isFlying)`, `void SetVerticalInput(float verticalInput)`, `float ResolveSpeed(bool sprint, float sprintSpeed)`, `float ResolveVerticalVelocity(float flightSpeed, bool movementLocked)`
  - `StarterAssets.ThirdPersonController`: `public void SetFlying(bool isFlying)`, `public void SetFlightVerticalInput(float verticalInput)`

- [ ] **Step 1: テストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Player/PlayerFlightMotionTest.cs`:

```csharp
using NUnit.Framework;
using StarterAssets;

namespace Client.Tests.Player
{
    /// <summary>
    ///     飛行速度と縦速度の規則を確認する
    ///     Verifies the flight speed and vertical velocity rules
    /// </summary>
    public class PlayerFlightMotionTest
    {
        private const float SprintSpeed = 7.5f;

        [Test]
        public void 飛行速度は走りの2倍でShift中は6倍()
        {
            var motion = new PlayerFlightMotion();

            Assert.AreEqual(15f, motion.ResolveSpeed(false, SprintSpeed), 0.0001f);
            Assert.AreEqual(45f, motion.ResolveSpeed(true, SprintSpeed), 0.0001f);
        }

        [Test]
        public void 上下入力は水平と同じ速さで縦速度になる()
        {
            var motion = new PlayerFlightMotion();
            motion.SetFlying(true);

            motion.SetVerticalInput(1f);
            Assert.AreEqual(45f, motion.ResolveVerticalVelocity(45f, false), 0.0001f);

            motion.SetVerticalInput(-1f);
            Assert.AreEqual(-15f, motion.ResolveVerticalVelocity(15f, false), 0.0001f);
        }

        [Test]
        public void 移動ロック中は上下入力があっても縦速度は0()
        {
            var motion = new PlayerFlightMotion();
            motion.SetFlying(true);
            motion.SetVerticalInput(1f);

            Assert.AreEqual(0f, motion.ResolveVerticalVelocity(15f, true), 0.0001f);
        }

        [Test]
        public void 飛行の入り直しで前回の上下入力を持ち越さない()
        {
            var motion = new PlayerFlightMotion();
            motion.SetFlying(true);
            motion.SetVerticalInput(1f);

            motion.SetFlying(false);
            motion.SetFlying(true);

            Assert.AreEqual(0f, motion.ResolveVerticalVelocity(15f, false), 0.0001f);
        }
    }
}
```

- [ ] **Step 2: `PlayerFlightMotion` を書く**

`moorestech_client/Assets/Dependencies/StarterAssets/ThirdPersonController/Scripts/PlayerFlightMotion.cs`:

```csharp
using UnityEngine;

namespace StarterAssets
{
    // 飛行中の速度規則と上下入力を持つ。いつ飛ぶかは外から決める
    // Owns flight speed rules and vertical input; when to fly is decided outside
    public class PlayerFlightMotion
    {
        // 走り(SprintSpeed)に掛ける倍率。ADR 0075
        // Multipliers applied to SprintSpeed (ADR 0075)
        private const float FlightSpeedMultiplier = 2f;
        private const float FlightSprintSpeedMultiplier = 6f;

        public bool IsFlying { get; private set; }
        private float _verticalInput;

        public void SetFlying(bool isFlying)
        {
            // 入り直しで前回の上下入力を持ち越さない
            // Never carry the previous vertical input across re-entries
            IsFlying = isFlying;
            _verticalInput = 0f;
        }

        public void SetVerticalInput(float verticalInput)
        {
            _verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
        }

        public float ResolveSpeed(bool sprint, float sprintSpeed)
        {
            return sprintSpeed * (sprint ? FlightSprintSpeedMultiplier : FlightSpeedMultiplier);
        }

        public float ResolveVerticalVelocity(float flightSpeed, bool movementLocked)
        {
            // 移動ロック中は水平と同じく上下も止める
            // Halt vertical motion while locked, same as horizontal
            return movementLocked ? 0f : _verticalInput * flightSpeed;
        }
    }
}
```

- [ ] **Step 3: `ThirdPersonController` に受け口と飛行分岐を足す**

(a) フィールド（`private bool _movementLocked;` の直後）:

```csharp
		// 飛行の物理は委譲先が持つ。発動の判定は外側（Client.Game）が行う
		// Flight physics live in the delegate; activation is decided outside (Client.Game)
		private readonly PlayerFlightMotion _flightMotion = new();
```

(b) `SetControllable` の直後にメソッドを追加:

```csharp
        public void SetFlying(bool isFlying)
        {
            // 入るときはその場に浮き、抜けるときは縦速度0から落ち始める
            // Hover in place on entry and start falling from zero vertical speed on exit
            _flightMotion.SetFlying(isFlying);
            _verticalVelocity = 0.0f;
            _input.jump = false;

            // ジャンプ・落下の姿勢は重力処理でしか更新されないため、入退の瞬間に下ろしておく
            // Jump/fall poses are only updated by the gravity step, so clear them on every switch
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDJump, false);
                _animator.SetBool(_animIDFreeFall, false);
            }
        }

        public void SetFlightVerticalInput(float verticalInput)
        {
            _flightMotion.SetVerticalInput(verticalInput);
        }
```

(c) `Update` を置き換え:

```csharp
		private void Update()
		{
			// 飛行中は重力とジャンプを止め、押されたジャンプも捨てる
			// While flying, skip gravity and jumping, and drop any pressed jump
			if (_flightMotion.IsFlying) _input.jump = false;
			else JumpAndGravity();
			GroundedCheck();
            Move(); 
		}
```

(d) `Move` の先頭の `float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;` を置き換え:

```csharp
			// 飛行中は走りを基準にした飛行速度、通常時は歩き/走り
			// Use the sprint-based flight speed while flying, otherwise walk/sprint
			float flightSpeed = _flightMotion.ResolveSpeed(_input.sprint, SprintSpeed);
			float targetSpeed = _flightMotion.IsFlying ? flightSpeed : (_input.sprint ? SprintSpeed : MoveSpeed);
```

(e) `Move` の速度分岐（`if (_movementLocked) { _speed = 0.0f; }` の直後、`else if (currentHorizontalSpeed < ...` の前）に挿入:

```csharp
			// 飛行中は加減速せず、押している間だけ目標速度で動く
			// While flying, skip acceleration and move at the target speed only while keys are held
			else if (_flightMotion.IsFlying)
			{
				_speed = targetSpeed * inputMagnitude;
			}
```

(f) `Move` の `_controller.Move(...)` 行を置き換え（当たり判定は CharacterController.Move がそのまま担う）:

```csharp
			// 飛行中の縦速度はQ/E入力、通常時は重力で積算した値
			// Vertical speed comes from Q/E while flying, otherwise from integrated gravity
			float verticalVelocity = _flightMotion.IsFlying ? _flightMotion.ResolveVerticalVelocity(flightSpeed, _movementLocked) : _verticalVelocity;

			// move the player
			_controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, verticalVelocity, 0.0f) * Time.deltaTime);
```

- [ ] **Step 4: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.Player.PlayerFlightMotionTest"`
Expected: ErrorCount 0 / 4 tests PASS。続けて既存の `Client.Tests.Player.PlayerMovementUiLockTest` も PASS（通常経路の回帰なし）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Dependencies/StarterAssets/ThirdPersonController/Scripts/PlayerFlightMotion.cs moorestech_client/Assets/Dependencies/StarterAssets/ThirdPersonController/Scripts/PlayerFlightMotion.cs.meta moorestech_client/Assets/Dependencies/StarterAssets/ThirdPersonController/Scripts/ThirdPersonController.cs moorestech_client/Assets/Scripts/Client.Tests/Player/PlayerFlightMotionTest.cs moorestech_client/Assets/Scripts/Client.Tests/Player/PlayerFlightMotionTest.cs.meta
git commit -m "feat(player): ThirdPersonControllerに飛行の受け口と飛行速度の規則を足す"
```

---

### Task 2: スペース連打でフライモードを入退し、デバッグシートのトグルで許可する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Player/FlyMode/SpaceTapSequence.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Player/FlyMode/PlayerFlyModeController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Player/PlayerObjectController.cs`（フィールド・`Initialize`・`Update` 新設・`ApplyMovementLock`）
- Modify: `moorestech_client/Assets/Scripts/Client.Common/DebugConst.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.DebugSystem/DebugSheet/DebugSheetController.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Player/PlayerFlyModeControllerTest.cs`

**Interfaces:**
- Consumes: `ThirdPersonController.SetFlying(bool)`, `ThirdPersonController.SetFlightVerticalInput(float)`（Task 1）
- Produces:
  - `Client.Game.InGame.Player.FlyMode.SpaceTapSequence`: `int RegisterTap(float tapTime)`, `void Reset()`
  - `Client.Game.InGame.Player.FlyMode.PlayerFlyModeController`: ctor `PlayerFlyModeController(ThirdPersonController controller)`, `bool IsFlying { get; private set; }`, `void ManualUpdate(bool isControllable, float unscaledTime)`
  - `Client.Game.DebugConst.FlyModeLabel` / `FlyModeKey`（`"FlyMode"`）

- [ ] **Step 1: テストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Player/PlayerFlyModeControllerTest.cs`:

```csharp
using System.Reflection;
using Client.Game;
using Client.Game.InGame.Player;
using Client.Game.InGame.Player.FlyMode;
using Client.Input;
using Client.Tests.Common;
using Common.Debug;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Player
{
    /// <summary>
    ///     スペース連打によるフライモードの入退を確認する
    ///     Verifies entering and leaving fly mode by tapping space
    /// </summary>
    public class PlayerFlyModeControllerTest : InputTestFixture
    {
        private GameObject _playerRoot;
        private PlayerFlyModeController _flyMode;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            TestReflection.ResetInputManagerCache();
            _ = InputManager.Player;
            InputSystem.Update();

            // 実際のThirdPersonControllerを初期化済みで用意する
            // Prepare a real, initialized ThirdPersonController
            _playerRoot = new GameObject("PlayerFlyModeControllerTestPlayer");
            _playerRoot.AddComponent<CharacterController>();
            _playerRoot.AddComponent<StarterAssetsInputs>();
            var thirdPersonController = _playerRoot.AddComponent<ThirdPersonController>();
            var playerObjectController = _playerRoot.AddComponent<PlayerObjectController>();
            typeof(PlayerObjectController).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(playerObjectController, thirdPersonController);
            playerObjectController.Initialize(Vector3.zero, Vector3.zero);
            _flyMode = new PlayerFlyModeController(thirdPersonController);
        }

        public override void TearDown()
        {
            InputManager.Player.Jump.SetKeyDownForTest(false);
            Object.DestroyImmediate(_playerRoot);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void トグルオンで4連打するとフライに入る()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);

            TapSpace(true, 0.0f, 0.1f, 0.2f, 0.3f);

            Assert.IsTrue(_flyMode.IsFlying);
        }

        [Test]
        public void 押下間隔が03秒を超えると数え直す()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);

            TapSpace(true, 0.0f, 0.1f, 0.5f, 0.6f);

            Assert.IsFalse(_flyMode.IsFlying);

            // 数え直した2回に続けて2回押せば成立する
            // Two more taps after the restarted pair complete the sequence
            TapSpace(true, 0.7f, 0.8f);
            Assert.IsTrue(_flyMode.IsFlying);
        }

        [Test]
        public void トグルオフでは4連打してもフライに入らない()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, false);

            TapSpace(true, 0.0f, 0.1f, 0.2f, 0.3f);

            Assert.IsFalse(_flyMode.IsFlying);
        }

        [Test]
        public void フライ中に2連打すると解除する()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
            TapSpace(true, 0.0f, 0.1f, 0.2f, 0.3f);

            TapSpace(true, 1.0f, 1.1f);

            Assert.IsFalse(_flyMode.IsFlying);
        }

        [Test]
        public void フライ中の間隔の空いた単押しでは解除しない()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
            TapSpace(true, 0.0f, 0.1f, 0.2f, 0.3f);

            TapSpace(true, 1.0f, 1.5f);

            Assert.IsTrue(_flyMode.IsFlying);
        }

        [Test]
        public void トグルをオフにしてもフライ中は2連打で解除できる()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
            TapSpace(true, 0.0f, 0.1f, 0.2f, 0.3f);
            DebugParameters.SaveBool(DebugConst.FlyModeKey, false);

            Assert.IsTrue(_flyMode.IsFlying, "トグルオフだけでは解除しない");
            TapSpace(true, 1.0f, 1.1f);
            Assert.IsFalse(_flyMode.IsFlying);
        }

        [Test]
        public void 操作不可の間の押下は数えず途中の連打も捨てる()
        {
            DebugParameters.SaveBool(DebugConst.FlyModeKey, true);

            TapSpace(true, 0.0f, 0.1f);
            TapSpace(false, 0.15f, 0.2f);
            TapSpace(true, 0.25f, 0.3f);

            Assert.IsFalse(_flyMode.IsFlying, "ロックを挟んだ連打が通算された");
        }

        private void TapSpace(bool isControllable, params float[] tapTimes)
        {
            // 押下フレームと非押下フレームを交互に流す
            // Alternate pressed and released frames
            foreach (var tapTime in tapTimes)
            {
                InputManager.Player.Jump.SetKeyDownForTest(true);
                _flyMode.ManualUpdate(isControllable, tapTime);
                InputManager.Player.Jump.SetKeyDownForTest(false);
                _flyMode.ManualUpdate(isControllable, tapTime + 0.01f);
            }
        }
    }
}
```

（`DebugParameters` の保存先は assembly 全体の `ClientTestsDebugParametersIsolationFixture` が隔離済みなので、保存値の復元はしない〔前例: `BlockPickResolverTest`〕。`SetKeyDownForTest` は `Client.Input.InputKey` の internal で、`PlayableInteractInputTest` 等が既に使っている）

- [ ] **Step 2: `DebugConst` とデバッグシートにトグルを足す**

`moorestech_client/Assets/Scripts/Client.Common/DebugConst.cs` の `FreeBlockPlacementLabel` の直後:

```csharp

        public const string FlyModeLabel = "Fly mode (Space x4 / x2)";
        public const string FlyModeKey = "FlyMode";
```

`moorestech_client/Assets/Scripts/Client.DebugSystem/DebugSheet/DebugSheetController.cs` の `rootPage.AddBoolWithSave(false, FreeBlockPlacementLabel, DebugParameterKeys.FreeBlockPlacement);` の直後:

```csharp
            rootPage.AddBoolWithSave(false, FlyModeLabel, FlyModeKey);
```

- [ ] **Step 3: `SpaceTapSequence` を書く**

`moorestech_client/Assets/Scripts/Client.Game/InGame/Player/FlyMode/SpaceTapSequence.cs`:

```csharp
namespace Client.Game.InGame.Player.FlyMode
{
    // 押下時刻の列から連続回数を数える。間隔が開けば1から数え直す
    // Counts consecutive taps from press times, restarting at 1 after a long gap
    public class SpaceTapSequence
    {
        private const float MaxTapIntervalSeconds = 0.3f;

        private int _count;
        private float _lastTapTime;

        public int RegisterTap(float tapTime)
        {
            _count = _count > 0 && tapTime - _lastTapTime <= MaxTapIntervalSeconds ? _count + 1 : 1;
            _lastTapTime = tapTime;
            return _count;
        }

        public void Reset()
        {
            _count = 0;
        }
    }
}
```

- [ ] **Step 4: `PlayerFlyModeController` を書く**

`moorestech_client/Assets/Scripts/Client.Game/InGame/Player/FlyMode/PlayerFlyModeController.cs`:

```csharp
using Client.Input;
using Common.Debug;
using StarterAssets;
using UnityEngine;

namespace Client.Game.InGame.Player.FlyMode
{
    // デバッグ用フライモードの入退と上下入力をThirdPersonControllerへ渡す
    // Drives entering/leaving the debug fly mode and pushes vertical input to ThirdPersonController
    public class PlayerFlyModeController
    {
        private const int EnterTapCount = 4;
        private const int ExitTapCount = 2;

        public bool IsFlying { get; private set; }

        private readonly ThirdPersonController _controller;
        private readonly SpaceTapSequence _tapSequence = new();

        public PlayerFlyModeController(ThirdPersonController controller)
        {
            _controller = controller;
        }

        public void ManualUpdate(bool isControllable, float unscaledTime)
        {
            // 操作不可の間は連打を数えず、途中の連打も捨てる
            // While uncontrollable, ignore taps and discard any partial sequence
            if (!isControllable)
            {
                _tapSequence.Reset();
                return;
            }

            if (InputManager.Player.Jump.GetKeyDown) HandleSpaceTap();
            if (IsFlying) _controller.SetFlightVerticalInput(ReadVerticalInput());

            #region Internal

            void HandleSpaceTap()
            {
                // 飛行中は2連打で解除、通常時は4連打で発動
                // Two taps leave while flying, four taps enter otherwise
                var tapCount = _tapSequence.RegisterTap(unscaledTime);
                if (IsFlying && tapCount >= ExitTapCount) SetFlying(false);
                else if (!IsFlying && tapCount >= EnterTapCount) TryEnter();
            }

            void TryEnter()
            {
                // トグルオフは無視するが、理由をログへ残す
                // Ignore when the toggle is off, but log why
                if (!DebugParameters.GetValueOrDefaultBool(DebugConst.FlyModeKey))
                {
                    _tapSequence.Reset();
                    Debug.Log($"[FlyMode] Space x{EnterTapCount} ignored: debug sheet toggle '{DebugConst.FlyModeLabel}' is off");
                    return;
                }
                SetFlying(true);
            }

            float ReadVerticalInput()
            {
                // EとQの同時押しは相殺する
                // Holding both E and Q cancels out
                var vertical = 0f;
                if (HybridInput.GetKey(KeyCode.E)) vertical += 1f;
                if (HybridInput.GetKey(KeyCode.Q)) vertical -= 1f;
                return vertical;
            }

            #endregion
        }

        private void SetFlying(bool isFlying)
        {
            // 入退のたびに連打を数え直し、ログに残す
            // Restart the tap count on every switch and log it
            IsFlying = isFlying;
            _tapSequence.Reset();
            _controller.SetFlying(isFlying);
            Debug.Log(isFlying ? "[FlyMode] Entered fly mode" : "[FlyMode] Left fly mode");
        }
    }
}
```

- [ ] **Step 5: `PlayerObjectController` から毎フレーム駆動する**

`moorestech_client/Assets/Scripts/Client.Game/InGame/Player/PlayerObjectController.cs`:

(a) using に `using Client.Game.InGame.Player.FlyMode;` を追加。

(b) フィールド `private PlayerRideFollow _rideFollow;` の直後:

```csharp
        private PlayerFlyModeController _flyMode;
```

(c) `Initialize` の `_rideFollow = new PlayerRideFollow(...);` の直後:

```csharp
            _flyMode = new PlayerFlyModeController(controller);
```

(d) `LateUpdate` の直前に `Update` を新設:

```csharp
        private void Update()
        {
            // 地形構築前は動かさない。操作可否はロックと乗車の両方で決まる
            // Stay still before terrain exists; controllability combines locks and riding
            if (!isRuntimeStarted) return;
            _flyMode.ManualUpdate(IsControllable(), Time.unscaledTime);
        }
```

(e) `ApplyMovementLock` を共通判定へ置き換え:

```csharp
        private void ApplyMovementLock()
        {
            // 乗車中の停止は追従状態が正。別フラグへ写すと二重管理になる
            // The follow state is the authority for the riding stop; a separate flag would duplicate it
            controller.SetControllable(IsControllable());
        }

        private bool IsControllable()
        {
            return _movementLocks.Count == 0 && !_rideFollow.IsFollowing();
        }
```

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Player\."`
Expected: ErrorCount 0 / `PlayerFlyModeControllerTest` 7件・`PlayerFlightMotionTest` 4件・既存 `PlayerMovementUiLockTest` / `PlayerRideFollowTest` / `PlayerRuntimeStartGateTest` が全て PASS。`PlayerObjectController.cs` が200行未満であることを `wc -l` で確認。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Player/FlyMode moorestech_client/Assets/Scripts/Client.Game/InGame/Player/FlyMode.meta moorestech_client/Assets/Scripts/Client.Game/InGame/Player/PlayerObjectController.cs moorestech_client/Assets/Scripts/Client.Common/DebugConst.cs moorestech_client/Assets/Scripts/Client.DebugSystem/DebugSheet/DebugSheetController.cs moorestech_client/Assets/Scripts/Client.Tests/Player/PlayerFlyModeControllerTest.cs moorestech_client/Assets/Scripts/Client.Tests/Player/PlayerFlyModeControllerTest.cs.meta
git commit -m "feat(player): スペース連打でデバッグ用フライモードに入退する"
```

---

### Task 3: unityプレイ録画テストで実機の飛行を確かめる

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/debug-fly-mode.cs`

**Interfaces:**
- Consumes: `DebugConst.FlyModeKey`（Task 2）、`PlayerFlyModeController` の実機挙動（Task 1・2）
- Produces: なし

- [ ] **Step 1: シナリオを書く**

```csharp
// デバッグ用フライモードの録画検証（ADR 0075）: トグルオフでは4連打で飛ばない→オンで4連打→E上昇→ホバー→W+Shiftで高速水平移動→戻る→2連打で解除して足場へ落下
// Recorded check of the debug fly mode (ADR 0075): no flight while the toggle is off, then enter with 4 taps, rise with E, hover, fast horizontal flight, return, and leave with 2 taps to fall onto the scaffold
using Client.Game;
using Client.Playtest;
using Client.Playtest.Input;
using Common.Debug;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("debug-fly-mode", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();
    await UniTask.Delay(1000, ignoreTimeScale: true);
    var groundY = p.PlayerPosition.y;

    // 1: トグルオフでは4連打しても飛ばない
    // 1: Four taps do nothing while the toggle is off
    p.Note("トグルオフで4連打しても飛ばないことを確認する");
    DebugParameters.SaveBool(DebugConst.FlyModeKey, false);
    for (var i = 0; i < 4; i++) { SemanticInput.KeyDown(Key.Space); await UniTask.DelayFrame(1); SemanticInput.KeyUp(Key.Space); await UniTask.DelayFrame(1); }
    SemanticInput.KeyDown(Key.E); await UniTask.Delay(1000, ignoreTimeScale: true); SemanticInput.KeyUp(Key.E);
    await UniTask.Delay(1500, ignoreTimeScale: true);
    p.Assert(Mathf.Abs(p.PlayerPosition.y - groundY) < 0.5f, $"トグルオフでは地上のまま y={p.PlayerPosition.y} ground={groundY}");

    // 2: トグルオンで4連打→Eで上昇（期待15m/s）
    // 2: Toggle on, four taps, then rise with E (expected 15 m/s)
    p.Note("トグルオンで4連打してフライに入り、Eで1秒上昇する");
    DebugParameters.SaveBool(DebugConst.FlyModeKey, true);
    await UniTask.Delay(1500, ignoreTimeScale: true);
    for (var i = 0; i < 4; i++) { SemanticInput.KeyDown(Key.Space); await UniTask.DelayFrame(1); SemanticInput.KeyUp(Key.Space); await UniTask.DelayFrame(1); }
    var startY = p.PlayerPosition.y;
    SemanticInput.KeyDown(Key.E); await UniTask.Delay(1000, ignoreTimeScale: true); SemanticInput.KeyUp(Key.E);
    p.Assert(p.PlayerPosition.y - startY > 10f, $"E 1秒で10m以上上昇（期待15m） rise={p.PlayerPosition.y - startY}");
    await p.Screenshot("01-flying-up");

    // 3: 入力なしでホバーする（重力が止まっている）
    // 3: Hover without input (gravity is off)
    var hoverY = p.PlayerPosition.y;
    await UniTask.Delay(1000, ignoreTimeScale: true);
    p.Assert(Mathf.Abs(p.PlayerPosition.y - hoverY) < 0.5f, $"入力なしで高度を保つ drift={p.PlayerPosition.y - hoverY}");

    // 4: W+Shiftで高速水平移動（期待45m/s）→S+Shiftで戻る
    // 4: Fast horizontal flight with W+Shift (expected 45 m/s), then come back with S+Shift
    p.Note("W+Shiftで1秒水平移動し、S+Shiftで戻る");
    var before = p.PlayerPosition;
    SemanticInput.KeyDown(Key.LeftShift); SemanticInput.KeyDown(Key.W);
    await UniTask.Delay(1000, ignoreTimeScale: true);
    SemanticInput.KeyUp(Key.W);
    var moved = Vector2.Distance(new Vector2(before.x, before.z), new Vector2(p.PlayerPosition.x, p.PlayerPosition.z));
    p.Assert(moved > 30f, $"W+Shift 1秒で30m以上（期待45m） moved={moved}");
    await p.Screenshot("02-flying-fast");
    SemanticInput.KeyDown(Key.S);
    await UniTask.Delay(1000, ignoreTimeScale: true);
    SemanticInput.KeyUp(Key.S); SemanticInput.KeyUp(Key.LeftShift);

    // 5: 2連打で解除し、足場へ落下する
    // 5: Leave with two taps and fall onto the scaffold
    p.Note("スペース2連打で解除し、足場へ落下する");
    for (var i = 0; i < 2; i++) { SemanticInput.KeyDown(Key.Space); await UniTask.DelayFrame(1); SemanticInput.KeyUp(Key.Space); await UniTask.DelayFrame(1); }
    await p.Until(() => Mathf.Abs(p.PlayerPosition.y - groundY) < 0.5f, 10f, "解除後に足場の高さへ戻る");
    await p.Screenshot("03-landed");

    DebugParameters.SaveBool(DebugConst.FlyModeKey, false);
});
```

（W/S の戻りでカメラ向きが変わらない前提。足場は 50x50 なので、戻りが足場外へずれた場合は Step 2 の結果で `PlayerPosition` を見て判断する。足場外に落ちた場合も y<-50 の落下復帰で戻るため、Until は最大10秒で判定できる）

- [ ] **Step 2: 実行する**

`unity-playmode-recorded-playtest` スキルの `references/run-scenario.md` の手順どおりに、`uloop control-play-mode --project-path ./moorestech_client --action stop` → `.claude/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .claude/skills/unity-playmode-recorded-playtest/scenarios/misc/debug-fly-mode.cs` をバックグラウンド実行し、result.json を回収する。
Expected: 全 Assert / Until が PASS。合否は肯定行だけで決めない。さらに `uloop get-logs --project-path ./moorestech_client --log-type Error` と、シナリオ区間の Warning/Error に `NullReference` / `Exception` / `refus` / `mismatch` が0件であることを確認する。`[FlyMode] Space x4 ignored` が1回（手順1）、`[FlyMode] Entered fly mode`・`[FlyMode] Left fly mode` が各1回出ていることをログで確認する。録画 mp4 の上昇・水平移動・落下を目視で確かめる。

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/debug-fly-mode.cs
git commit -m "test(playtest): デバッグ用フライモードの録画シナリオを足す"
```

---

### Task 4: moores-code-review で全ブランチレビュー（省略不可）

- [ ] **Step 1:** 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。
- [ ] **Step 2:** レビュー反映がソースの判定経路・条件式・その評価時点（`ThirdPersonController.Update/Move` の分岐、`PlayerFlyModeController.ManualUpdate`、`PlayerObjectController.Update`）に触れたら、Task 3 の unityプレイ録画テストを反映後のバイナリで再実施してから完了とする。
- [ ] **Step 3:** plan・録画記録に「未検証」「未確認」として残ったものは、1件ずつ `bd create` で起票し、結論に bd の ID を列挙する。

### Task 5: PR 作成（セッション終了可能状態にする）

- [ ] **Step 1:** 全作業をコミットする（`git status` が clean。Unity が書き戻す `.moorestech-external-revisions.json` の差分はコミットしない）。
- [ ] **Step 2:** セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。
- [ ] **Step 3:** PR 作成後、`moores-wt rm debug-fly-mode` で worktree と Editor を畳む。

---

## 判断記録（ADR）

設計 ADR: `docs/adr/0075-debug-fly-mode-toggled-by-space-taps.md`（ユーザー裁定7件・agent前提9件。出所欄はそちらが正）。裁定台帳: `.decisions/2026-10-01-フライ*.md`（8件）。

planning 中に生じた判断:

- J1: 飛行の物理は `ThirdPersonController` と同じ asmdef の新クラス `PlayerFlightMotion` に置き、`ThirdPersonController` には `SetFlying` / `SetFlightVerticalInput` のプッシュ口だけを足す。連打判定・デバッグトグル参照は `Client.Game` 側に置く。
  出所: agent判断（`ThirdPersonController` の asmdef は `Client.Game` を参照できない。基盤側に「デバッグ」「連打」の語彙を持ち込まない規約〔layer-map「汎用基盤コンポーネントへの状態伝達」〕と、委譲クラスの前例 `PlayerPlatformFollowService`）
- J2: `PlayerFlyModeController` は MonoBehaviour にせず、`PlayerObjectController.Update` から `ManualUpdate(isControllable, Time.unscaledTime)` で駆動する。
  出所: agent判断（操作可否〔移動ロック・乗車追従〕の正は `PlayerObjectController` にある。時刻を引数にして単体テストで間隔を決定的に与えるため。`unscaledTime` はポーズ等の timeScale 変更で連打判定が狂わないため）
- J3: `ThirdPersonController.cs`（既存 375 行）は今回分割しない。追加は約20行に抑え、飛行の規則は新ファイルへ出す。
  出所: agent判断（ユーザー裁定 2026-10-01「デバッグモードのものだからプロダクションの影響を最小限にする」に従い、プロダクションの移動処理を大きく組み替えない。既存の200行超過は本件で生じたものではない）
- J4: Q/E は `InputManager` にアクションを足さず、`HybridInput.GetKey(KeyCode.Q/E)` で読む。
  出所: agent判断（同じキーを読む既存の `CommonBlockPlaceDragState` と同じ経路。入力定義〔.inputactions と生成コード〕を変えるとプロダクションの入力に触れるため、上記ユーザー裁定に従う）
- J5: `IsFlying { get; private set; }` を `PlayerFlyModeController` の公開状態にする（自クラス内の分岐でも使う）。テストはこれを観測する。
  出所: agent判断（AGENTS.md の `{ get; private set; }` 許容規定）
- J6: 4連打の4打目で地上ジャンプが始まっていても、`SetFlying(true)` で縦速度を0にしてその場に浮かせる。打鍵の順序（同フレームで `ThirdPersonController.Update` が先か後か）による差は、高々1フレーム分の上昇で無視できる。
  出所: agent判断
- J7: unityプレイ録画テストを Task 3 に含める（入力・移動のランタイム挙動に触れるため）。キー注入は `PressKey`（操作間に0.5秒の間隔が自動で入り、0.3秒の連打判定を満たせない）ではなく `SemanticInput.KeyDown/KeyUp` をフレーム単位で直接使う。
  出所: agent判断
- J8: `ThirdPersonController.SetFlying` は `_verticalVelocity` と `_input.jump` を無条件に書き換える（Phase 2.6 弱・5-F）。既存の書き手（接地時の -2、ジャンプ初速、空中での jump クリア）は毎フレームの条件付き更新で、`SetFlying` は入退の瞬間に1回だけ走る状態リセットなので、書き手を1本にまとめない。
  出所: agent判断（ユーザー委任 2026-10-01「残りの質問はいい感じに決めといて」）
- J9: `IsControllable()` を `SetControllable`（ロック変化時）と `PlayerFlyModeController.ManualUpdate`（毎フレーム）の2箇所に渡す（Phase 2.6 弱・検査6）。導出式は1本（`IsControllable()`）で、消費者ごとにタイミングが違うだけなので現状維持とする。
  出所: agent判断（同上）
- J10: 既存のデバッグ用カメラ `CinematicCameraController` の WASD/Q/E/Shift 自由移動は、本PRでは共通化しない（Phase 2.6 第3バケツ）。レビュー依頼文の「本PR外のリファクタ提案」に1行で載せる。
  出所: agent判断（spec-architecture-review Phase 2.6 の第3バケツ規定）
- J11: user-simulator review の Warning 2件を適用した。(1) `SetFlying` で Animator の Jump/FreeFall を下ろす（`JumpAndGravity` を飛ばす間に姿勢が凍結するため）。ADR 0075 のアニメーションの文言もこれに合わせた。(2) テストでの `DebugParameters` 保存値の復元をやめた（`ClientTestsDebugParametersIsolationFixture` が隔離済み）。
  出所: agent判断（シミュレーター予測の Warning を適用。ユーザー承認は未取得）
- J12: unityプレイ録画テストの連打は `Record = true` で `Time.captureDeltaTime = 1/30` 固定になるため、`DelayFrame(1)` 刻みのタップは約0.067秒間隔で決定的に0.3秒以内に収まる（非前面 Editor の CPU 絞りの影響を受けない）。
  出所: agent判断（シミュレーター判事の裏取り事実）
