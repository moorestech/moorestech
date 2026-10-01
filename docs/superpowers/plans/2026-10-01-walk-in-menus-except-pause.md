# メニュー中も歩ける（ポーズメニューだけ止める）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** インベントリ・ブロックインベントリ・研究・ビルドメニュー・チャレンジ一覧を開いたまま歩けるようにし、移動を止めるのはポーズメニューだけにする。歩けるようになることで生じる「開いたインベントリから離れる」「Spaceでボタンが押される」問題も同時に解消する。

**Architecture:** 画面ごとの移動停止宣言 `IUIState.LocksPlayerMovement()`（PR #1428）は機構ごと残し、宣言値だけを切り替える。開いたインベントリの自動クローズは、インタラクト候補選定の近傍探索（`OverlapSphere(InteractDistance)`）を共通部品へ切り出し、「開いた対象がまだ近傍探索に掛かるか」を `SubInventoryState.GetNextUpdate()` で毎フレーム問う。Web UI は Tab と同じ場所で Space のブラウザ既定動作を封じる。

**Tech Stack:** Unity C#（Client.Game / Client.Tests）、NUnit EditMode、React + vitest（moorestech_web/webui）、プレイテストDSL（unityプレイ録画テスト）

## Requirements

- R1: PlayerInventory / SubInventory / ChallengeList / ResearchTree / BuildMenu を開いている間、WASD・ジャンプ・ダッシュで自機が動く。受入: 宣言テストで5画面が `false`、録画テストで各画面にてW押下1.5秒の移動量 > 1m
- R2: ポーズメニュー（設定・バグ報告の子画面を含む）を開いている間は動かない。列車乗車中・スキット中に開く入れ子ポーズも同じ。受入: 宣言テストで `PauseMenuState` と `PauseMenuNestedSubState` が `true`、録画テストでポーズ中のW押下で移動量 < 0.05m
- R3: ブロック・列車車両のインベントリは、自機から対象表面までが `InteractTargetSelector.InteractDistance` を超えると自動で閉じゲーム画面へ戻る。距離は別の値を持たず同じ定数を参照する。受入: `InteractReachQueryTest`（範囲内true・範囲外false・ブロック/列車両方）、録画テストで歩いて離れると `GameScreen` へ戻る
- R4: 列車が発車して離れた場合も R3 と同じく閉じる（判定は毎フレームで自機と対象の双方の移動を拾う）。受入: `InteractReachQueryTest` で対象側を動かしても false になる
- R5: 開いた直後に自動クローズしない（開けた位置にいる限り閉じない）。受入: `InteractReachQueryTest` の範囲内ケース、録画テストで開いて1秒待っても `SubInventory` のまま
- R6: Web UI 全体で、文字入力欄以外では Space のブラウザ既定動作（フォーカス中ボタンのクリック・スクロール）を封じる。受入: vitest の判定関数テスト
- R7: クラフトボタンは Space に反応しない（Enter長押し・マウス長押しは従来どおり）。受入: `CraftRecipeEntry.tsx` の keydown/keyup 判定から `" "` が消えている
- R8: 文字入力欄にフォーカス中は移動しない（現状維持・変更しない）
- R9: Shift は一括移動とダッシュを兼ねる（現状維持・変更しない）
- R10: メニュー中の視点は回らない（現状維持・変更しない）

やらないこと:
- 視点操作の追加、Shift の排他、文字入力中の移動許可（R8〜R10）
- `PlayerMovementLockReason`・慣性即停止・押しっぱなし再開の仕組みの変更
- 自動クローズ時のトースト・通知表示

## Global Constraints

- 1ファイル200行未満、1ディレクトリの新規コードは10ファイルまで（`Client.Tests/Interact/` は既に10 .cs のため新規テストは `Client.Tests/Interact/Reach/` へ）
- コメントは日本語1行＋英語1行の2行セット。`#region Internal` はメソッド内ローカル関数用のみ
- partial・`Func<>`・デフォルト引数・try-catch 禁止。単純 getter/setter プロパティ禁止（`{ get; }` の interface 契約と `{ get; private set; }` は可）
- `.meta` は手で作らない（Unity が生成したものをコミットする）
- .cs を変えたら `uloop compile --project-path ./moorestech_client` を必ず通す
- 作業場所: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/walk-in-menus-except-pause`（ブランチ `feat/walk-in-menus-except-pause`）。Editor は `uloop launch` でこの worktree 側に立てる（`moores-wt status` で他者の Editor を確認してから）

---

## File Structure

| ファイル | 責務 | 変更 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/{PlayerInventory,ChallengeList,ResearchTree,BuildMenu,SubInventory}State.cs` | 移動停止宣言 | `LocksPlayerMovement()` を `false` に |
| `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateMovementLockDeclarationTest.cs` | 宣言の全件表 | 期待値更新・改名 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/Selection/InteractOverlap.cs` | 近傍探索の共通部品（レイヤマスク・バッファ拡張込み） | 新規（`InteractTargetSelector` の `OverlapNearby` を切り出し） |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/Selection/InteractTargetSelector.cs` | 候補選定 | `InteractOverlap` を呼ぶよう変更 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/Selection/InteractReachQuery.cs` | 「対象がまだ手の届く範囲か」 | 新規 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventory/ISubInventorySource.cs` | 開いたインベントリの発生元 | `IInteractable ReachTarget { get; }` 追加 |
| `.../SubInventory/BlockSubInventorySource.cs` / `TrainSubInventorySource.cs` | 発生元の実装 | `ReachTarget` 実装（列車はctor引数追加） |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/Core/TrainCarInteractActions.cs` | 列車Fアクション | `TrainSubInventorySource` へ `Interactable` を渡す |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventoryState.cs` | ブロック/列車インベントリ画面 | 範囲外で GameScreen へ。キーヒント定義を別ファイルへ移し200行未満を保つ |
| `.../State/SubInventory/SubInventoryStateHints.cs` | キーヒント定義 | 新規（`SubInventoryState.cs` 末尾から移動） |
| `moorestech_client/Assets/Scripts/Client.Tests/Interact/Reach/InteractReachQueryTest.cs` | 到達判定のテスト | 新規 |
| `moorestech_client/Assets/Scripts/Client.Tests/Interact/Reach/SubInventoryReachTargetTest.cs` | Fアクションが ReachTarget を正しく載せるか | 新規 |
| `moorestech_client/Assets/Scripts/Client.Tests/UIState/Models/SubInventorySourceModelTest.cs` | 列車ソースのモデル化 | ctor 変更に追随 |
| `moorestech_web/webui/src/shared/uiState/activeLayer.ts` / `.test.ts` | Web入力の純関数 | `suppressesBrowserDefaultKey` 追加 |
| `moorestech_web/webui/src/shared/uiState/useWebInputExclusivity.ts` | Web入力の排他 | Tab と同じ場所で Space を封じる |
| `moorestech_web/webui/src/features/recipe/views/CraftRecipeEntry.tsx` | クラフトボタン | Space 判定を外す |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/walk-while-menus-open.cs` | 録画テスト | 新規 |

既存部品の再利用表:

| 新規 | 同じ役割の既存 | 判断 |
|---|---|---|
| `InteractOverlap.OverlapNearby` | `InteractTargetSelector.Scan` 内ローカル関数 `OverlapNearby` | 共通へ切り出し、選定と到達判定の両方が呼ぶ（距離・マスク・バッファ拡張を一元化） |
| `InteractReachQuery` の対象解決 | `InteractableResolver.TryResolve` | 呼ぶ（破棄済み・`IsInteractAvailable=false` の除外もそのまま効く） |
| `suppressesBrowserDefaultKey` | `useWebInputExclusivity` の Tab 封じ | Tab 判定ごと純関数へ寄せ、同じ場所で Space も扱う |

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | 移動停止宣言の値 | 各 `IUIState` | 画面の宣言＋`UIStateControl` 駆動（既存） | PR #1428 の仕組みそのまま |
| 2 | `InteractOverlap` | Client.Game `Interact/Selection` | static 純関数＋呼び出し側所有バッファ | `InteractableResolver`（同ディレクトリの static 解決器） |
| 3 | `InteractReachQuery` | Client.Game `Interact/Selection` | インスタンス（自前バッファ） | `InteractTargetSelector`（バッファを持つ問い合わせ） |
| 4 | `ISubInventorySource.ReachTarget` | UI/UIState/State/SubInventory | 既存 interface へのメンバー追加 | `InventoryIdentifier`（発生元が開いた対象の素性を持つ） |
| 5 | 自動クローズ判定 | `SubInventoryState.GetNextUpdate()` | 毎フレームのステート遷移判定 | 同メソッドの `_shouldClose`（削除イベントで閉じる）と同じ場所 |
| 6 | Space 封じ | `useWebInputExclusivity` | capture keydown の preventDefault | 同フックの Tab 封じ（`.decisions/2026-08-22-ブラウザのTabフォーカス移動は封じる.md`） |

- 5 は「状態変化は購読で」規約の例外に当たらない: 距離は自機と列車の物理進行で連続的に変わり変化通知が存在しない。`GetNextUpdate()` はステートの毎フレーム遷移判定そのもので、`CloseUI` キー等と同列に置くのが前例どおり
- 検査4（受動的統合）: 既存機構（宣言・`UIStateControl`）は無傷のまま値を変えるだけ。自動クローズも既存の遷移判定に条件を1つ足すだけで、凍結・抑止・迂回は導入しない

死活表（同じ機構にぶら下がる操作）:

| 操作 | 計画後 | 根拠 |
|---|---|---|
| Tab/Esc/右短押しでインベントリを閉じる | 生きる | `GetNextUpdate` の既存条件はそのまま |
| ブロック削除でインベントリが閉じる | 生きる | `_shouldClose` 経路は不変 |
| 乗車中は移動しない | 生きる | `PlayerRideFollow` 側の停止は宣言と独立 |
| ポーズ中に慣性で滑らない・閉じたら押下キーで再開 | 生きる | `PlayerMovementLockReason.Ui` の掛け外しは不変 |
| 検索欄で空白を打つ | 生きる | 文字入力欄は Space 封じの対象外 |
| Enter/マウス長押しでクラフト | 生きる | Space だけを外す |
| Space でボタン・タブを押す | **死ぬ** | ユーザー裁定（R6）で意図して封じる |

恒久失敗になりうる経路（自動クローズの判定が構造的に一致しないと、開いた瞬間に毎回閉じて二度と使えない）:

| 状態 | いつ起きるか | ユーザーに見えるもの | 解消 |
|---|---|---|---|
| 開いた対象のコライダが近傍探索のレイヤに無い | 照準レイ・近傍探索とも同じ `InteractOverlap.InteractLayerMask` を使うので、開けた時点でレイヤに居ることが保証される | 起きない | — |
| 開いた対象と `ReachTarget` が別インスタンス | Fアクションが渡す対象を取り違えた場合 | 開いた直後に閉じる | `SubInventoryReachTargetTest` で同一性を固定。録画テストで石窯を開いて1秒保つことを確認 |
| 対象が破棄・`IsInteractAvailable=false`（ブロック撤去中等） | 撤去・削除時 | 閉じる | 意図どおり（削除イベントで閉じる既存経路と同じ結果） |

---

### Task 1: 移動停止をポーズメニューだけにする

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateMovementLockDeclarationTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlayerInventoryState.cs:88-91`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventoryState.cs:167-170`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/ChallengeListState.cs:41-44`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/ResearchTreeState.cs:53-56`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs:64-67`

**Interfaces:**
- Consumes: なし
- Produces: なし（宣言値の変更のみ）

- [ ] **Step 1: 宣言テストの期待値を書き換える**

`UIStateMovementLockDeclarationTest.cs` の1つ目のテストを次に置き換える（メソッド名も変える）:

```csharp
        [Test]
        public void ポーズメニューだけが自機の移動を止めると宣言している()
        {
            // 定数を返す画面の全件表。画面を足すと突き合わせが必ず落ちて宣言の見直しを強制する
            // The full table for constant-returning screens; adding a screen always fails the match and forces a review
            var expectedDeclarations = new Dictionary<Type, bool>
            {
                { typeof(GameScreenState), false },
                { typeof(PlaceBlockState), false },
                { typeof(DeleteObjectState), false },
                { typeof(DebugBlockInfoState), false },
                { typeof(PlayerInventoryState), false },
                { typeof(SubInventoryState), false },
                { typeof(PauseMenuState), true },
                { typeof(ChallengeListState), false },
                { typeof(ResearchTreeState), false },
                { typeof(BuildMenuState), false },
            };
```

（以降の本文は既存のまま。2つ目の `入れ子サブステートはポーズと列車操作中だけ移動を止めると宣言している` は変更しない）

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.UIState.UIStateMovementLockDeclarationTest"`
Expected: `ポーズメニューだけが自機の移動を止めると宣言している` が FAIL（5画面が true を返す）

- [ ] **Step 3: 5画面の宣言を false にする**

`PlayerInventoryState` / `SubInventoryState` / `ChallengeListState` / `ResearchTreeState` / `BuildMenuState` の各メソッドを次にする:

```csharp
        public bool LocksPlayerMovement()
        {
            return false;
        }
```

- [ ] **Step 4: コンパイルとテストを通す**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.(UIState|Player)\."`
Expected: 全件 PASS（`UIStateControlTest.画面の宣言どおりに自機の移動を止めて戻す` は Stub の宣言を使うため影響なし）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateMovementLockDeclarationTest.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/
git commit -m "feat(ui-state): メニュー中も歩けるようにし、移動を止めるのはポーズメニューだけにする"
```

### Task 2: 近傍探索を共通化し「まだ手が届くか」を問えるようにする

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/Selection/InteractOverlap.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/Selection/InteractReachQuery.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/Selection/InteractTargetSelector.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Interact/Reach/InteractReachQueryTest.cs`

**Interfaces:**
- Consumes: `InteractTargetSelector.InteractDistance`（const float 2f・既存）、`InteractableResolver.TryResolve(Collider, Vector3, out IInteractable, out Vector3)`（internal static・既存）
- Produces:
  - `public static class InteractOverlap { public static int OverlapNearby(Vector3 center, ref Collider[] buffer); public const int InitialBufferSize = 64; }`
  - `public class InteractReachQuery { public bool IsWithinReach(IInteractable target, Vector3 playerPosition); }`

- [ ] **Step 1: 失敗するテストを書く**

`Client.Tests/Interact/Reach/InteractReachQueryTest.cs`（既存 `InteractTargetSelectorTestFixture` の対象生成を使う）:

```csharp
using Client.Game.InGame.Interact.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Interact.Reach
{
    /// <summary>
    ///     開いた対象がまだインタラクト距離内にあるかの判定を検証
    ///     Verifies whether an opened target is still within the interact distance
    /// </summary>
    public class InteractReachQueryTest : InteractTargetSelectorTestFixture
    {
        [Test]
        public void ブロックはインタラクト距離内なら届き外なら届かない()
        {
            var block = CreateOpenableBlockTarget(new Vector3(1.5f, 0f, 0f));
            var query = new InteractReachQuery();

            Assert.IsTrue(query.IsWithinReach(block, Vector3.zero));
            Assert.IsFalse(query.IsWithinReach(block, new Vector3(-1.5f, 0f, 0f)));
        }

        [Test]
        public void 列車が発車して離れると届かなくなる()
        {
            var car = CreateTrainCarTarget(new Vector3(1.5f, 0f, 0f));
            var query = new InteractReachQuery();
            Assert.IsTrue(query.IsWithinReach(car, Vector3.zero));

            // 自機は動かさず対象側だけを動かす
            // Only the target moves while the player stays put
            car.transform.position = new Vector3(5f, 0f, 0f);
            Physics.SyncTransforms();

            Assert.IsFalse(query.IsWithinReach(car, Vector3.zero));
        }

        [Test]
        public void 近くに別の対象があっても開いた対象が遠ければ届かない()
        {
            var openedBlock = CreateOpenableBlockTarget(new Vector3(5f, 0f, 0f));
            CreateOpenableBlockTarget(new Vector3(1f, 0f, 0f));
            var query = new InteractReachQuery();

            Assert.IsFalse(query.IsWithinReach(openedBlock, Vector3.zero));
        }

        [Test]
        public void 破棄された対象には届かない()
        {
            var block = CreateOpenableBlockTarget(new Vector3(1f, 0f, 0f));
            var query = new InteractReachQuery();

            // TearDownが破棄済みを二重に破棄しないよう、後始末の対象から外してから壊す
            // Drop it from the cleanup list first so TearDown never destroys it twice
            TargetObjects.Remove(block.gameObject);
            Object.DestroyImmediate(block.gameObject);
            Physics.SyncTransforms();

            Assert.IsFalse(query.IsWithinReach(block, Vector3.zero));
        }
    }
}
```


- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `InteractReachQuery` が無いためコンパイルエラー

- [ ] **Step 3: `InteractOverlap` を切り出す**

`Interact/Selection/InteractOverlap.cs`:

```csharp
using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.Interact.Selection
{
    /// <summary>
    ///     インタラクト距離内の当たり判定を集める。候補選定と到達判定が同じ距離・レイヤを使うための一元化
    ///     Collects colliders within the interact distance so selection and reach checks share one distance and layer set
    /// </summary>
    public static class InteractOverlap
    {
        public const int InitialBufferSize = 64;

        public static readonly int InteractLayerMask = LayerConst.BlockOnlyLayerMask | LayerConst.MapObjectOnlyLayerMask;

        // 飽和したまま返すと取りこぼした候補次第で結果が変わるため、バッファを倍にして採り直す
        // A saturated buffer would make the result depend on which colliders were dropped, so it is doubled and re-queried
        public static int OverlapNearby(Vector3 center, ref Collider[] buffer)
        {
            while (true)
            {
                var count = Physics.OverlapSphereNonAlloc(center, InteractTargetSelector.InteractDistance, buffer, InteractLayerMask);
                if (count < buffer.Length) return count;

                buffer = new Collider[buffer.Length * 2];
            }
        }
    }
}
```

`InteractTargetSelector.cs` を変更する:
- `private const int InitialOverlapBufferSize = 64;` と `private static readonly int InteractLayerMask = ...;` を削除
- `_overlapBuffer` の初期化を `new Collider[InteractOverlap.InitialBufferSize]` に
- `TryGetFrontmostSolidHit(InteractLayerMask, ...)` を `TryGetFrontmostSolidHit(InteractOverlap.InteractLayerMask, ...)` に
- `var hitCount = OverlapNearby(playerPosition);` を `var hitCount = InteractOverlap.OverlapNearby(playerPosition, ref _overlapBuffer);` にし、ローカル関数 `OverlapNearby` を削除（`ContainsCandidate` は残す）

- [ ] **Step 4: `InteractReachQuery` を書く**

`Interact/Selection/InteractReachQuery.cs`:

```csharp
using UnityEngine;

namespace Client.Game.InGame.Interact.Selection
{
    /// <summary>
    ///     開いた対象がまだ手の届く範囲にあるか。候補選定の近傍探索と同じ問い合わせで答える
    ///     Whether an opened target is still within reach, answered by the same nearby query the selection uses
    /// </summary>
    public class InteractReachQuery
    {
        private Collider[] _overlapBuffer = new Collider[InteractOverlap.InitialBufferSize];

        // 開けた位置なら近傍探索に必ず掛かるので、開いた直後に届かない判定にはならない
        // Any position the target was opened from is caught by the nearby query, so it never reads out of reach right after opening
        public bool IsWithinReach(IInteractable target, Vector3 playerPosition)
        {
            var hitCount = InteractOverlap.OverlapNearby(playerPosition, ref _overlapBuffer);
            for (var index = 0; index < hitCount; index++)
            {
                if (!InteractableResolver.TryResolve(_overlapBuffer[index], playerPosition, out var resolved, out _)) continue;
                if (ReferenceEquals(resolved, target)) return true;
            }

            return false;
        }
    }
}
```

- [ ] **Step 5: コンパイルとテストを通す**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Interact\."`
Expected: 新規4件を含め全件 PASS（`InteractTargetSelectorTest` が切り出し後も通ること）

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/Selection/ moorestech_client/Assets/Scripts/Client.Tests/Interact/Reach/
git commit -m "feat(interact): 近傍探索を共通化し開いた対象がまだ手の届く範囲かを問えるようにする"
```

### Task 3: 開いたインベントリを手の届く範囲外で自動で閉じる

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventory/ISubInventorySource.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventory/BlockSubInventorySource.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventory/TrainSubInventorySource.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/Core/TrainCarInteractActions.cs:32`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventoryState.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventory/SubInventoryStateHints.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/Models/SubInventorySourceModelTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateKeyHintCatalogTest.cs`（`using` 追加のみ）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Interact/Reach/SubInventoryReachTargetTest.cs`

**Interfaces:**
- Consumes: `InteractReachQuery.IsWithinReach(IInteractable, Vector3)`（Task 2）、`PlayerSystemContainer.Instance.PlayerObjectController.Position`（既存）
- Produces:
  - `ISubInventorySource.ReachTarget`（`IInteractable ReachTarget { get; }`）
  - `TrainSubInventorySource(long trainCarInstanceId, IInteractable reachTarget)`

- [ ] **Step 1: 失敗するテストを書く**

`Client.Tests/Interact/Reach/SubInventoryReachTargetTest.cs`:

```csharp
using Client.Game.InGame.UI.UIState.State.SubInventory;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Interact.Reach
{
    /// <summary>
    ///     Fで開くアクションが、開いた対象そのものを到達判定の対象として運ぶことを検証
    ///     Verifies the F-open actions carry the opened target itself as the reach target
    /// </summary>
    public class SubInventoryReachTargetTest : InteractTargetSelectorTestFixture
    {
        [Test]
        public void ブロックを開くと開いたブロックの面が到達判定の対象になる()
        {
            var block = CreateOpenableBlockTarget(new Vector3(1f, 0f, 0f));

            var source = block.Actions[0].Execute().TransitContext.GetContext<ISubInventorySource>();

            Assert.AreSame(block, source.ReachTarget);
        }

        [Test]
        public void 車両インベントリを開くと開いた車両の面が到達判定の対象になる()
        {
            var car = CreateTrainCarTarget(new Vector3(1f, 0f, 0f));

            // Actions[0] がF=車両インベントリ（TrainCarInteractable.Initializeの並び）
            // Actions[0] is F = car inventory, per the order in TrainCarInteractable.Initialize
            var source = car.Actions[0].Execute().TransitContext.GetContext<ISubInventorySource>();

            Assert.AreSame(car, source.ReachTarget);
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ReachTarget` が無いためコンパイルエラー

- [ ] **Step 3: 発生元に ReachTarget を持たせる**

`ISubInventorySource.cs` に追加（`using Client.Game.InGame.Interact;` を足す）:

```csharp
        /// <summary>
        /// 開いた世界の対象。離れたら閉じる判定に使う
        /// The world target this inventory was opened from, used to close it once out of reach
        /// </summary>
        IInteractable ReachTarget { get; }
```

`BlockSubInventorySource.cs` に追加（`using Client.Game.InGame.Interact;`）:

```csharp
        public IInteractable ReachTarget => _blockGameObject.Interactable;
```

`TrainSubInventorySource.cs` のctorとプロパティ（`using Client.Game.InGame.Interact;`）:

```csharp
        public IInteractable ReachTarget { get; }

        public TrainSubInventorySource(long trainCarInstanceId, IInteractable reachTarget)
        {
            TrainCarInstanceId = trainCarInstanceId;
            ReachTarget = reachTarget;
            InventoryIdentifier = InventoryIdentifierMessagePack.CreateTrainMessage(trainCarInstanceId);
        }
```

`TrainCarInteractActions.cs:32`:

```csharp
            var container = UITransitContextContainer.Create<ISubInventorySource>(new TrainSubInventorySource(_trainCar.TrainCarInstanceId.AsPrimitive(), _trainCar.Interactable));
```

`SubInventorySourceModelTest.cs` の `new TrainSubInventorySource(7)` 3箇所を `new TrainSubInventorySource(7, new UnusedReachTarget())` にし、同ファイル末尾（クラス内）に追加（`using Client.Game.InGame.Interact;` と `using UnityEngine;`）:

```csharp
        // モデル化だけを見るテストなので到達判定の対象は使われない
        // These tests only cover model building, so the reach target is never consulted
        private class UnusedReachTarget : IInteractable
        {
            public GameObject GameObject => null;
            public bool IsInteractAvailable => false;
            public void SetHighlighted(bool highlighted) { }
        }
```

- [ ] **Step 4: キーヒント定義を別ファイルへ移す**

`SubInventoryState.cs` 末尾の `internal static class SubInventoryStateHints { ... }` を丸ごと削除し、`State/SubInventory/SubInventoryStateHints.cs` を作る:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    internal static class SubInventoryStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Close),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.ShiftLeftClick, LocalizationKeys.Ui.KeyHint.Text.BulkMove),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.RightClick, LocalizationKeys.Ui.KeyHint.Text.HalveOrPlaceOne),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.LeftDrag, LocalizationKeys.Ui.KeyHint.Text.DistributeEvenly),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.DoubleClick, LocalizationKeys.Ui.KeyHint.Text.GatherSameItem),
        };
    }
}
```

（`KeyHint` の名前空間はコンパイルで確認し、`using` を合わせる。`UIStateKeyHintCatalogTest.cs` に `using Client.Game.InGame.UI.UIState.State.SubInventory;` が無ければ足す）

- [ ] **Step 5: 範囲外で閉じる**

`SubInventoryState.cs`:
- `using Client.Game.InGame.Interact.Selection;` と `using Client.Game.InGame.Player;` を追加
- フィールド `private readonly InteractReachQuery _reachQuery = new();` を追加
- `GetNextUpdate()` を次にする:

```csharp
        public UITransitContext GetNextUpdate()
        {
            var isRightShortPressed = _rightShortPressInputService.TryConsumeShortPressOutsideUi();
            if (_shouldClose || InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown || isRightShortPressed || IsOutOfReach())
            {
                return new UITransitContext(UIStateEnum.GameScreen);
            }

            return null;

            #region Internal

            // 自機も列車も動くため毎フレーム測る。距離はFで開くときと同じ近傍探索で決める
            // Both the player and trains move, so it is measured every frame with the same nearby query F-open uses
            bool IsOutOfReach()
            {
                if (CurrentSubInventorySource == null) return false;
                var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                return !_reachQuery.IsWithinReach(CurrentSubInventorySource.ReachTarget, playerPosition);
            }

            #endregion
        }
```

`SubInventoryState.cs` が200行未満であることを `wc -l` で確認する。

- [ ] **Step 6: コンパイルとテストを通す**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.(Interact|UIState)\."`
Expected: 全件 PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/ moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/Core/TrainCarInteractActions.cs moorestech_client/Assets/Scripts/Client.Tests/
git commit -m "feat(sub-inventory): 開いたブロック・車両から手の届く範囲を出たらインベントリを自動で閉じる"
```

### Task 4: Web UI で Space をジャンプ専用にする

**Files:**
- Modify: `moorestech_web/webui/src/shared/uiState/activeLayer.ts`
- Modify: `moorestech_web/webui/src/shared/uiState/activeLayer.test.ts`
- Modify: `moorestech_web/webui/src/shared/uiState/useWebInputExclusivity.ts`
- Modify: `moorestech_web/webui/src/features/recipe/views/CraftRecipeEntry.tsx:71-74`

**Interfaces:**
- Produces: `export function suppressesBrowserDefaultKey(key: string, activeElement: EventTarget | null): boolean`

- [ ] **Step 1: 失敗するテストを書く**

`activeLayer.test.ts` の import に `suppressesBrowserDefaultKey` を足し、末尾に追加:

```ts
describe("suppressesBrowserDefaultKey", () => {
  const button = { matches: () => false } as unknown as EventTarget;
  const textInput = { matches: () => true } as unknown as EventTarget;

  it("Tabは文字入力中でも封じる", () => {
    expect(suppressesBrowserDefaultKey("Tab", button)).toBe(true);
    expect(suppressesBrowserDefaultKey("Tab", textInput)).toBe(true);
  });
  it("Spaceは文字入力欄以外で封じ、ボタンを押させない", () => {
    expect(suppressesBrowserDefaultKey(" ", button)).toBe(true);
    expect(suppressesBrowserDefaultKey(" ", null)).toBe(true);
  });
  it("文字入力欄のSpaceは空白入力として通す", () => {
    expect(suppressesBrowserDefaultKey(" ", textInput)).toBe(false);
  });
  it("Enterなど他のキーは封じない", () => {
    expect(suppressesBrowserDefaultKey("Enter", button)).toBe(false);
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/shared/uiState/activeLayer.test.ts`
Expected: FAIL（`suppressesBrowserDefaultKey` が無い）

- [ ] **Step 3: 判定関数を書き、Tab と同じ場所で使う**

`activeLayer.ts` の `isTextInputElement` の下に追加:

```ts
// Tabのフォーカス移動と、文字入力欄以外のSpace（ボタン押下・スクロール）はゲーム操作と衝突するので既定動作を封じる
// Tab traversal and Space outside text fields (button press, scroll) fight game controls, so their defaults are suppressed
export function suppressesBrowserDefaultKey(key: string, activeElement: EventTarget | null): boolean {
  if (key === "Tab") return true;
  return key === " " && !isTextInputElement(activeElement);
}
```

`useWebInputExclusivity.ts`: import に `suppressesBrowserDefaultKey` を足し、`onKeyDown` の Tab 分岐（コメント2行＋if）を次に置き換える:

```ts
      // Tabと文字入力欄以外のSpaceはゲーム操作と衝突するため既定動作ごと封じる
      // Tab and Space outside text fields fight game controls, so their defaults are suppressed
      if (suppressesBrowserDefaultKey(event.key, document.activeElement)) {
        event.preventDefault();
        return;
      }
```

さらに同フック内に keyup でも同じ封じを入れる（Chromium のボタンは Space の keyup でクリックが確定するため）:

```ts
    const onKeyUp = (event: KeyboardEvent) => {
      if (suppressesBrowserDefaultKey(event.key, document.activeElement)) event.preventDefault();
    };
```

と `document.addEventListener("keyup", onKeyUp, true);` / cleanup に `document.removeEventListener("keyup", onKeyUp, true);` を足す。

`CraftRecipeEntry.tsx:71-74` を次にする:

```tsx
            // キーボードはEnter長押しで連続クラフトする。Spaceはジャンプ専用なので反応させない
            // Keyboard crafting is an Enter hold; Space is reserved for jumping and never crafts
            onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); start(); } }}
            onKeyUp={(e) => { if (e.key === "Enter") stop(); }}
```

- [ ] **Step 4: テストと型検査を通す**

Run: `cd moorestech_web/webui && pnpm vitest run && pnpm tsc --noEmit`
Expected: 全件 PASS・型エラー0

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/shared/uiState/ moorestech_web/webui/src/features/recipe/views/CraftRecipeEntry.tsx
git commit -m "feat(webui): 文字入力欄以外のSpace既定動作を封じジャンプ専用にする"
```

### Task 5: unityプレイ録画テストで通しを確かめる

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/walk-while-menus-open.cs`

**Interfaces:**
- Consumes: Task 1〜4 の全成果、プレイテストDSL（`PlaytestDriver`・`SemanticInput.KeyDown/KeyUp`）

- [ ] **Step 1: シナリオを書く**

手本は `scenarios/building/ui-place-then-open-machine.cs`（上空の足場移設・石窯の設置・F で開く手順をそのまま使う）。書く内容:

```csharp
// メニューを開いたまま歩け、ポーズ中だけ止まり、機械UIは手の届く範囲を出ると閉じることを通しで確かめる
// End-to-end check: walking works with menus open, only pause stops it, and the machine UI closes once out of reach
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;

var ovenBlockName = "石窯";
var flatGroundObjectName = "PlaytestFlatGround";
var testFieldTopY = 200f;
var ovenModelCenterOffset = new Vector3(1.5f, 1f, 1.5f);

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("walk-while-menus-open", options, async p =>
{
    await p.SkipOpeningSkit();
    await p.SetupFlatGround();
    GameObject.Find(flatGroundObjectName).transform.position = new Vector3(0f, testFieldTopY - 2f, 0f);
    p.WarpPlayer(new Vector3(0f, testFieldTopY + 1f, 0f));
    await p.WaitSeconds(1.5f);

    // Wを1.5秒押した移動量を返す
    // Returns how far the player moved while W was held for 1.5 seconds
    async UniTask<float> WalkForward()
    {
        var before = p.PlayerPosition;
        SemanticInput.KeyDown(Key.W);
        await p.WaitSeconds(1.5f);
        SemanticInput.KeyUp(Key.W);
        await p.WaitSeconds(0.5f);
        var delta = p.PlayerPosition - before;
        return new Vector2(delta.x, delta.z).magnitude;
    }

    // 開いて歩いて閉じる。閉じるのはEscape（各画面のCloseUI）
    // Open, walk, and close; closing uses Escape (each screen's CloseUI)
    async UniTask CheckWalkWhileOpen(Key openKey, UIStateEnum state)
    {
        await p.PressKey(openKey);
        await p.WaitUiState(state, 5f);
        var walked = await WalkForward();
        p.Assert(walked > 1f, $"{state} を開いたまま歩けた ({walked:F2}m)");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 5f);
    }

    // 開くキーは GameScreenState.GetNextUpdate の割当（B/T/R）とインベントリの OpenInventory
    // Open keys follow GameScreenState.GetNextUpdate (B/T/R) and the inventory's OpenInventory binding
    await CheckWalkWhileOpen(Key.Tab, UIStateEnum.PlayerInventory);
    await CheckWalkWhileOpen(Key.B, UIStateEnum.BuildMenu);
    await CheckWalkWhileOpen(Key.T, UIStateEnum.ChallengeList);
    await CheckWalkWhileOpen(Key.R, UIStateEnum.ResearchTree);

    // ポーズメニュー中は歩けない
    // Walking is stopped while the pause menu is open
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.PauseMenu, 5f);
    var pauseWalk = await WalkForward();
    p.Assert(pauseWalk < 0.05f, $"ポーズ中は歩けない ({pauseWalk:F2}m)");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 5f);
    await p.Screenshot("01-menus-walk");

    // 石窯を開き、開いた直後は閉じず、歩いて離れると閉じる
    // Open the oven; it stays open right after opening and closes once the player walks away
    p.WarpPlayer(new Vector3(0f, testFieldTopY + 1f, 0f));
    await p.WaitSeconds(1.5f);
    await p.PrepareBlockForUiPlacement(ovenBlockName, 1);
    var ovenOrigin = new Vector3Int(-1, Mathf.RoundToInt(testFieldTopY), 1);
    await p.PlaceBlockViaUi(ovenBlockName, ovenOrigin, BlockDirection.North);
    await p.ExitToGameScreen();
    var oven = await p.WaitBlockGameObject(ovenOrigin);
    p.WarpPlayer(new Vector3(0f, testFieldTopY + 1f, 0f));
    await p.WaitSeconds(1.5f);
    await p.AimAt(oven.transform.position + ovenModelCenterOffset);
    await p.WaitSeconds(0.5f);
    await p.PressInteract();
    await p.WaitUiState(UIStateEnum.SubInventory, 10f);
    await p.WaitSeconds(1f);
    p.Assert(p.CurrentUiState == UIStateEnum.SubInventory, "開いた直後は閉じない");

    SemanticInput.KeyDown(Key.S);
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 5f, "離れると機械UIが閉じる");
    SemanticInput.KeyUp(Key.S);
    await p.Screenshot("02-closed-out-of-reach");
});
```

インベントリを開くキー（`InputManager.UI.OpenInventory` の割当）と、各画面を Escape で閉じられるかは実行で確かめ、違えば実際の割当へ直す。`SemanticInput` の名前空間・`WaitSeconds` 等の API 名はコンパイルエラーで確認して合わせる。

- [ ] **Step 2: 実行する**

Run: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/walk-while-menus-open.cs`
Expected: result.json の全 Assert が pass、録画で各画面中に歩いていること・ポーズで止まること・離れて閉じることが目視できる。Unity のエラーログ（`uloop get-logs --project-path ./moorestech_client --log-type Error`）が実行区間で0件

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/walk-while-menus-open.cs
git commit -m "test(playtest): メニュー中の歩行・ポーズ停止・範囲外クローズの録画シナリオを足す"
```

### Task 6: 全ブランチレビューと PR 作成（省略不可）

- [ ] **Step 1:** 必ず最後に moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）
- [ ] **Step 2:** レビュー指摘の反映が判定経路（`GetNextUpdate` の条件・`InteractReachQuery`・Space 判定）に触れたら、Task 5 の録画テストを反映後のコードで再実行してから先へ進む
- [ ] **Step 3:** 未検証・残課題は1件ずつ `bd create` で起票し、PR 本文に issue ID を列挙する
- [ ] **Step 4:** 録画テストの合否は、期待 Assert の pass に加えて、実行区間の Unity エラー・警告ログ（`Exception` / `Error` / `MooresmasterLoaderException`）が0件であることで判定する
- [ ] **Step 5:** 全作業をコミットし push、pr-create スキルで PR を作る。PR 本文の冒頭に「PR #1428 の移動停止を、ポーズメニュー以外で外す」と書く。その後 `moores-wt rm walk-in-menus-except-pause` で worktree と Editor を畳む

---

## 判断記録（ADR）

ADR（`docs/adr/`）は作らない。どの決定も後から簡単に戻せ、ADR の3条件（戻しにくい・文脈なしに意外・実際のトレードオフ）を満たさないため。裁定は `.decisions/` に記録済み。

| # | 判断 | 出所 |
|---|---|---|
| D1 | 移動を止めるのはポーズメニュー（子画面含む）だけ。棄却: ブロックインベントリだけ止める | ユーザー裁定 2026-10-01 原文「インベントリ、研究、ビルドメニュー等が開いていある間も動いていたい / ESCのメニューだけ開いている途中動かないようにしたい」→ 選択「ESC以外すべて」（`.decisions/2026-10-01-メニュー中の移動停止はESCメニューだけにする.md`） |
| D2 | 開いたインベントリは一定距離離れたら自動で閉じる（一度「開いたまま」を選んだ後に覆った） | ユーザー裁定 2026-10-01 原文「いや、やっぱり今のうちに直しておく。指定距離離れたら自動で閉じる」（`.decisions/2026-10-01-ブロックインベントリは一定距離離れたら自動で閉じる.md`） |
| D3 | 閉じる距離は `InteractDistance` をそのまま参照し、測り方も開くときと同じ。棄却: 5m・10m | ユーザー裁定 2026-10-01 自由記述「ちゃんと同じパラメーターを参照する」→ 復唱に「はい」（`.decisions/2026-10-01-サブインベントリの自動クローズ距離はインタラクト距離の定数を参照する.md`） |
| D4 | 列車の車両インベントリも閉じる（発車して離れた場合も）。棄却: ブロックだけ | ユーザー裁定 2026-10-01 選択「列車も同じく閉じる」 |
| D5 | クラフトボタンは Space に反応しない。棄却: 今のまま両方効く | ユーザー裁定 2026-10-01 選択「Spaceはジャンプ専用」 |
| D6 | Web UI 全体で文字入力欄以外の Space 既定動作を封じる。棄却: クラフトボタンだけ | ユーザー裁定 2026-10-01 選択「Web UI全体で封じる」 |
| D7 | Shift は一括移動とダッシュを兼ねる。棄却: UI上ではダッシュしない | ユーザー裁定 2026-10-01 選択「両方効く」 |
| D8 | メニュー中の視点は回さない。棄却: 視点を回す操作を足す | ユーザー裁定 2026-10-01 選択「今のまま、移動だけ解放」 |
| D9 | 文字入力欄フォーカス中は移動しない（現状維持）。棄却: 入力中も歩く | ユーザー裁定 2026-10-01 選択「入力中は止める（現状維持）」 |
| D10 | 「測り方が同じ」を、候補選定の近傍探索（`OverlapSphere(InteractDistance)`＋`InteractableResolver`）を共通部品にして同じ問い合わせで答えることで実現する。照準レイで開けた位置は近傍探索にも必ず掛かるため、開いた直後には閉じない | agent判断（`InteractTargetSelector` の既存近傍探索を前例とし、R5 を構造で保証するため） |
| D11 | 判定は `SubInventoryState.GetNextUpdate()` で毎フレーム行う | agent判断（`_shouldClose`・`CloseUI` と同じ遷移判定の場所。距離には変化通知が無く購読できない） |
| D12 | 開いた対象は `ISubInventorySource.ReachTarget` として発生元が運ぶ | agent判断（発生元が開いた対象の素性 `InventoryIdentifier` を既に持つ前例に揃える） |
| D13 | 移動停止の仕組み（宣言・`UIStateControl`・停止理由フラグ・慣性即停止・押しっぱなし再開）は残し、宣言値だけを変える | agent判断（PR #1428 の仕組みはポーズ・入れ子ポーズで引き続き使う） |
| D14 | 自動で閉じた後の遷移先は GameScreen、手に持ったアイテム等の扱いは Esc で閉じたときと同じ | agent判断（既存の閉じる経路と同じ `UITransitContext(GameScreen)` を返すため自動的にそうなる） |
| D15 | Space は keydown に加え keyup でも既定動作を封じる | agent判断（Chromium のボタンは Space の keyup でクリックが確定するため） |
