# チュートリアル改善 (ADR 0048) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 序盤チュートリアルの相対ゴーストを風車の向き・地形に耐える形へ直し、チャレンジチェーンを再編（掘削機設置のblockPlace化・3件削除・シャフト+粉砕機統合・ベルト教育2段・文言）する。

**Architecture:** クライアントは既存の設置パイプライン（`CommonBlockPlaceSystem` の reporter 列）とチュートリアル振り分け（`ITutorialViewManager`）への追加で実現し、サーバーは新完了判定1種を `ChallengeFactory` へ登録する。マスタデータは `moorestech_master:tools/tutorial_v3_port/generate_challenges.py`（正本）を編集して再生成し、本repoのピンを更新する。

**Tech Stack:** Unity C# (uloop) / VanillaSchema YAML + Mooresmaster SourceGenerator / Python (マスタ生成) / NUnit

## Requirements

ADR: `docs/adr/0048-tutorial-refinements-ghost-rotation-and-chain-cleanup.md`（出所・棄却案は全てここ。裁定原文は `.decisions/2026-08-30-*.md` 5件）

- R1 相対ゴーストの offset / blockDirection はアンカーローカル座標（アンカーNorth基準）とし、設置済みアンカーの BlockDirection で回転して目標セル・向きを出す。受け入れ: 風車を4方位どれで置いてもゴースト位置に置けば歯車が繋がる（Task 7 レイアウトテスト）
- R2 風車設置チャレンジ中、設置プレビューにシャフト・粉砕機のゴーストを連結表示し、連結セルのどれかが設置不可（既存ブロック重なり・地形の埋まり/浮き）なら風車自体を設置不可にする。受け入れ: 連結セルが塞がれたとき `PlaceInfo.Placeable=false` になり、カーソルにツールチップ理由が出る
- R3 「木のシャフトで風車と繋ぐ」「粉砕機を設置して動かす」を「シャフトと粉砕機を設置して動かす」1件に統合し relativeBlockPlacePreview 2個を同時表示。完了判定は gearConnectedBlock（粉砕機）のまま。受け入れ: 1チャレンジ内の相対ゴースト2件が両方表示される（Task 2 テスト）
- R4 「粘土を入手する」「青銅の鉱石を5個採掘する」の前に「粘土鉱脈に風力掘削機を設置する」「青銅の鉱脈に風力掘削機を設置する」（blockPlaceOnVein＋veinPin）を新設し、アイテム段からピンを外す。veinRestrictedPlacement は掛けない
- R5 「青銅シートを作る」「木釘を9本作る」「合板を作る」を削除する
- R6 原始研究4の直後に「歯車ベルトコンベアを設置する」（場所指定なし blockPlace）→「木のシャフトをベルトの横に設置する」（ベルトをアンカーに相対ゴースト）を新設。後段の完了はシャフトがそのベルト種別に歯車接続した時点（回転不問）
- R7 文言: タイトル「原木を3個入手する」／「①木の板をクリックして選択」／クラフト要求チャレンジ全部に keyControl Tab「インベントリを開く」
- R8 マスタ変更は moorestech_master の新ブランチ+PR とし、本repo `.moorestech-external-revisions.json` のピンをそのpush済みコミットへ更新する（AGENTS.md規約）
- やらないこと: bush非インタラクト化（ADR 0043 / moorestech-68i0 が別途実装中）／サーバー側の設置検証追加（鉱脈限定と同じくクライアント限定が前例）／多ホップ（ギアネットワーク到達）判定

## Global Constraints

- partial禁止・`Func<>`禁止・try-catch原則禁止・1ファイル200行以下・イベントはUniRx（AGENTS.md）
- `.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client`。テストは `--filter-type regex` で限定。PlayMode遷移後のドメインリロードエラーは45秒待ってリトライ
- 実装worktreeは `moores-wt new` で作成済みのものを使う（メインworktree作業禁止）
- challenges.json は直接編集禁止。正本は `moorestech_master:tools/tutorial_v3_port/generate_challenges.py`。**CHALLENGES 表の key は絶対に変えない**（GUID＝uuid5(key)、localization キーが崩れる）。新チャレンジは新keyを足す
- `.moorestech-external-revisions.json` は Unity 起動時に `ExternalRepositorySyncService` が working tree の値へ書き戻すため、意図した hash だけをコミットする
- マスタ文言変更時は mod 側 `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv` を追随させる（japanese/english/german、Source列=日本語原文）。vanilla `Localization/localization.csv` を触った場合のみ `_CompileRequester.cs` バンプ＋webui `pnpm gen:i18n` が必要
- スキーマ（challenges.yml）変更後の再生成: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` を書き換えて uloop compile（ファイル追加ではないので csc.rsp 不要）
- 新TaskParam/TutorialParam を追加したら `ChallengeMasterUtil` の switch に case を足す（default が error になり既存マスタ検証が落ちる）。`MasterSourceTextCollector.GetTutorialDisplayText` も未知typeで例外を投げるため case 追加必須

## File Structure

本repo（コード）:
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/PlacementGuide/RelativeBlockPlacePreviewTutorialManager.cs`（回転＋複数エントリ化）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/PlacementGuide/AnchorRelativeDirectionUtil.cs`（向き合成）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/PlacementGuide/RelativeBlockPlacePreviewEntry.cs`（1件分の状態+ITutorialView）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/BlockPlacePreviewTutorialManager.cs`（guidキーの複数ゴースト）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/TutorialGhostEntry.cs`（ゴースト1体分）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ChainPreview/ChainPlacePreviewState.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ChainPlacementReporter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/PlacementGuide/ChainBlockPlacePreviewTutorialManager.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`（reporter差し込み＋連結ゴースト描画）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（新manager結線）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Localization/MasterSourceTextCollector.cs`
- Modify: `VanillaSchema/challenges.yml`（taskCompletionType `gearConnectToBlock`・tutorialType `chainBlockPlacePreview`）
- Create: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/GearConnectToBlockChallengeTask.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/{VanillaChallengeType.cs,ChallengeFactory.cs}`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs`
- Modify: `Localization/localization.csv`（連結不可ツールチップキー1行）＋ `moorestech_web/webui` の `pnpm gen:i18n` 生成物
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Tutorial/PlacementGuideTutorialDispatchTest.cs`（拡張）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/GearConnectToBlockChallengeTaskTest.cs`（新規）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EarlyGame/EarlyGameGearTutorialLayoutTest.cs`（新規=閉PR#1286から復活+4方位化）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/challenges.json`

moorestech_master（データ、新ブランチ `feature/tutorial-chain-refinements-adr0048`、origin/master 起点）:
- Modify: `tools/tutorial_v3_port/generate_challenges.py`
- Regenerate: `server_v8/mods/moorestechAlphaMod_8/master/challenges.json`
- Modify: `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`

## 配置と前例

- 連結セルの不可化: `VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable`（`PlaceSystem/Common/VeinPlacementReporter.cs:24`）と完全同型の static reporter を `CommonBlockPlaceSystem.GroundClickControl` の鉱脈チェック直後（素材チェック前）に1行差し込む。`PlacementBlockCause` enum は拡張しない（enum注記どおり、reporter が TooltipLine を直接積む）
- チュートリアル→設置系の状態受け渡し: `VeinRestrictedPlacementState`（tutorialGuidキー・SetHoge/Clear）と同型の `ChainPlacePreviewState` を新設。manager は `VeinRestrictedPlacementTutorialManager` 同型
- 座標回転: `BlockPositionInfo.ConvertBlockLocalToWorldCell`（セル）＋ `BlockDirection.GetCoordinateConvertAction`（方向ベクトル）。前例 `BlockConnectorConnectPositionCalculator.cs:26-36`
- 新完了判定: `BlockPlaceOnVeinChallengeTask`（イベントで候補を積み ManualUpdate で判定）の骨格＋ `IGearEnergyTransformer.GetGearConnects()` の1ホップ照合（`SimpleGearService.cs:39`）。ギアネットワークの internal は開けない
- サーバー側設置検証は追加しない（`PlaceBlockProtocol.cs:74` に鉱脈判定も無いのが前例。ADR 0038 と同じクライアント限定層）
- 新規パターン（ユーザー注目点）: tutorialType `chainBlockPlacePreview`（設置中ブロック基準の連結ゴースト）は前例なしの新設。ADR 0048 決定1の帰結

## 機能パリティ（死活表）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 通常ブロック設置（チュートリアル外） | 生きる | reporter は state にエントリが無ければ即 return |
| 既存 blockPlacePreview（絶対座標）チュートリアル | 生きる | BlockPlacePreviewTutorialManager の複数化は自 tutorialGuid キーで後方同値 |
| veinRestrictedPlacement / veinPin | 生きる | 触らない |
| 風車の向き回転操作（設置中） | 生きる | 連結ゴーストは pending 方向で回して追従する（R2実装） |
| 旧「木のシャフトで繋ぐ」等の既存セーブ進行 | チャレンジGUIDが変わるため未完了扱いで新チェーンを進む | 序盤圧縮（PR#50）時と同じ扱い。イベント展示は新規セーブ前提 |

---

### Task 1: 相対ゴーストの回転対応

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/PlacementGuide/AnchorRelativeDirectionUtil.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/PlacementGuide/RelativeBlockPlacePreviewTutorialManager.cs:92-94`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Tutorial/AnchorRelativeDirectionUtilTest.cs`

**Interfaces:**
- Produces: `static BlockDirection AnchorRelativeDirectionUtil.RotateByAnchor(BlockDirection localDirection, BlockDirection anchorDirection)`（水平4方位を回す。垂直系はそのまま返す）
- Produces（変更後の意味）: `RelativeBlockPlacePreviewTutorialParam.Offset/BlockDirection` は「アンカーNorth基準ローカル」

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Game.Block.Interface;
using NUnit.Framework;

namespace Client.Tests.UnitTest.Tutorial
{
    public class AnchorRelativeDirectionUtilTest
    {
        // アンカーが回った分だけ相対向きも回る
        // The relative direction rotates together with the anchor
        [TestCase(BlockDirection.North, BlockDirection.North, BlockDirection.North)]
        [TestCase(BlockDirection.East, BlockDirection.North, BlockDirection.East)]
        [TestCase(BlockDirection.North, BlockDirection.East, BlockDirection.East)]
        [TestCase(BlockDirection.East, BlockDirection.East, BlockDirection.South)]
        [TestCase(BlockDirection.West, BlockDirection.South, BlockDirection.East)]
        [TestCase(BlockDirection.South, BlockDirection.West, BlockDirection.East)]
        public void RotateByAnchorComposesHorizontalRotation(BlockDirection local, BlockDirection anchor, BlockDirection expected)
        {
            Assert.AreEqual(expected, Client.Game.InGame.Tutorial.PlacementGuide.AnchorRelativeDirectionUtil.RotateByAnchor(local, anchor));
        }
    }
}
```

期待値はクォータニオン合成の地上真値（North=+Z基準の時計回り合成）。もしテスト実行で `GetCoordinateConvertAction` の回転規約と食い違ったら、**テスト期待値ではなく実装のマッピングを直す前に**、`BlockDirection.GetRotation()` の実装（`Game.Block.Interface/BlockDirection.cs:33`）を読んで正しい合成規約をテスト側コメントに記録して揃える。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "AnchorRelativeDirectionUtilTest"`
Expected: コンパイルエラー（AnchorRelativeDirectionUtil 未定義）

- [ ] **Step 3: 実装**

```csharp
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.Tutorial.PlacementGuide
{
    /// <summary>
    ///     アンカーNorth基準のローカル向きを、設置済みアンカーの向きで回してワールド向きへ写す
    ///     Maps an anchor-North-basis local direction into world space using the placed anchor's direction
    /// </summary>
    public static class AnchorRelativeDirectionUtil
    {
        private static readonly BlockDirection[] HorizontalDirections =
            { BlockDirection.North, BlockDirection.East, BlockDirection.South, BlockDirection.West };

        public static BlockDirection RotateByAnchor(BlockDirection localDirection, BlockDirection anchorDirection)
        {
            // チュートリアルの水平配置のみ対象。垂直系はそのまま通す
            // Only horizontal tutorial layouts are rotated; vertical variants pass through
            if (System.Array.IndexOf(HorizontalDirections, localDirection) < 0) return localDirection;
            if (System.Array.IndexOf(HorizontalDirections, anchorDirection) < 0) return localDirection;

            var rotate = anchorDirection.GetCoordinateConvertAction();
            var worldForward = rotate(localDirection.GetCoordinateConvertAction()(Vector3Int.forward));
            foreach (var candidate in HorizontalDirections)
                if (candidate.GetCoordinateConvertAction()(Vector3Int.forward) == worldForward)
                    return candidate;
            return localDirection;
        }
    }
}
```

`RelativeBlockPlacePreviewTutorialManager.Update()` の目標セル算出（現 L92-94 付近）を差し替える:

```csharp
// アンカーの向きで回したローカルセル・向きを使う（gearConnectsと同じ換算）
// Use the anchor-rotated local cell and direction (same conversion as gearConnects)
_targetCell = anchor.BlockPosInfo.ConvertBlockLocalToWorldCell(_currentParam.Offset);
var worldDirection = AnchorRelativeDirectionUtil.RotateByAnchor(_direction, anchor.BlockPosInfo.BlockDirection);
...
_blockPlacePreviewTutorialManager.SetTargetCell(_targetBlockId, _targetCell.Value, worldDirection, _pinTutorialGuid);
```

注意: `ConvertBlockLocalToWorldCell` は原点補正込み（アンカーの回転でブロック原点が動く分を吸収する）。既存の `OriginalPos + Offset`（無回転）と North アンカーで同値になることを確認する（North なら回転行列が単位で origin 補正もゼロのはず。違えばマスタ側 offset の基準がズレるので Task 6 のレイアウトテストで捕まえる）。

- [ ] **Step 4: コンパイル＋テストPASS確認**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: Step 2 と同じテストコマンド
Expected: PASS

- [ ] **Step 5: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/PlacementGuide moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Tutorial
git commit -m "feat(tutorial): 相対ゴーストのoffsetと向きをアンカーの向きで回転する"
```

### Task 2: 相対ゴーストの複数同時表示

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/BlockPlacePreviewTutorialManager.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/TutorialGhostEntry.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/PlacementGuide/RelativeBlockPlacePreviewTutorialManager.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/PlacementGuide/RelativeBlockPlacePreviewEntry.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Tutorial/PlacementGuideTutorialDispatchTest.cs`（拡張）

**Interfaces:**
- Produces: `BlockPlacePreviewTutorialManager.SetTargetCell(BlockId blockId, Vector3Int cell, BlockDirection direction, string tutorialGuid)` — **シグネチャ不変**だが tutorialGuid ごとに独立したゴースト/Webピンを持つ。`ClearTarget(string tutorialGuid)` へ引数追加（現 `ClearTarget()` の全呼び出し元を更新）
- Produces: `RelativeBlockPlacePreviewTutorialManager.ApplyTutorial` は tutorialGuid ごとの `RelativeBlockPlacePreviewEntry`（`ITutorialView` 実装）を返し、同時複数エントリを `Update()` で全件追従する

- [ ] **Step 1: 失敗するテストを書く** — `PlacementGuideTutorialDispatchTest` に追加（既存テストの `TutorialsElement` 構築ヘルパを流用）:

```csharp
[Test]
public void TwoRelativePreviewsInOneChallengeBothStayActive()
{
    // 同一チャレンジ内の相対ゴースト2件が上書きされず両方生きる
    // Two relative previews in one challenge must both stay active instead of last-wins
    var first = manager.ApplyTutorial(CreateRelativeTutorial(shaftParam, tutorialGuidA));
    var second = manager.ApplyTutorial(CreateRelativeTutorial(crusherParam, tutorialGuidB));
    Assert.IsNotNull(first);
    Assert.IsNotNull(second);
    Assert.AreNotSame(first, second);
    first.CompleteTutorial();
    Assert.IsTrue(manager.HasActiveEntry(tutorialGuidB)); // 片方の完了で他方が消えない
    Assert.IsFalse(manager.HasActiveEntry(tutorialGuidA));
}
```

`HasActiveEntry(Guid)` はテスト可視のための public 読み取り（`{ get; private set; }` 相当のクエリ）。既存テストの構築方法（`RelativeBlockPlacePreviewTest.cs` / `PlacementGuideTutorialDispatchTest.cs` の param 生成）に合わせて `CreateRelativeTutorial` を書く。

- [ ] **Step 2: 失敗確認** — Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlacementGuideTutorialDispatchTest"` → FAIL/コンパイルエラー

- [ ] **Step 3: 実装**

`TutorialGhostEntry.cs`（ゴースト1体分。現 BlockPlacePreviewTutorialManager の `_previewObject`/`_targetCell`/`_previewCancellation` 群をこのクラスへ移す）:

```csharp
namespace Client.Game.InGame.Tutorial
{
    /// <summary>
    ///     チュートリアルゴースト1体分の実体。Webピンは tutorialGuid 由来のIDで独立させる
    ///     One tutorial ghost instance; its web pin id derives from the tutorialGuid so entries stay independent
    /// </summary>
    public class TutorialGhostEntry { /* BlockId, Vector3Int cell, BlockDirection, TutorialBlockPreviewObject, CancellationTokenSource, string WebPinId => $"block-place-preview-pin-{tutorialGuid}" */ }
}
```

`BlockPlacePreviewTutorialManager`: `Dictionary<string, TutorialGhostEntry> _entries` に置換。`SetTargetCell` は tutorialGuid で upsert（同 guid 同値なら早期return、変化なら該当エントリのみ再生成）。`ClearTarget(tutorialGuid)` は該当エントリのみ破棄＋`WorldPinStateStore.Instance.RemovePin(entry.WebPinId)`。自身の `ApplyTutorial`（絶対座標型）は自分の tutorialGuid を使うので挙動不変。

`RelativeBlockPlacePreviewTutorialManager`: 単数フィールド群を `Dictionary<Guid, RelativeBlockPlacePreviewEntry> _entries` へ。`ApplyTutorial` はエントリ生成して返す。`Update()` は全エントリを追従（アンカー最寄り取得→回転→SetTargetCell）。`OnBlockPlaced` 購読は manager が1本持ち、設置イベントで全エントリと照合。エントリの `CompleteTutorial()` は manager の `Complete(guid)` を呼び、`ClearTarget(guid)` して辞書から除去。200行制限のため判定ローカル関数は `#region Internal` に。

- [ ] **Step 4: コンパイル＋テストPASS** — Task 1 と同形。既存 `RelativeBlockPlacePreviewTest`・`PlacementGuideTutorialDispatchTest` 全件PASSも確認:
`uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "RelativeBlockPlacePreview|PlacementGuideTutorialDispatch|BlockPlacePreview"`

- [ ] **Step 5: コミット** — `git commit -m "feat(tutorial): 相対ゴーストをtutorialGuidキーで複数同時表示できるようにする"`

### Task 3: サーバー新完了判定 gearConnectToBlock

**Files:**
- Modify: `VanillaSchema/challenges.yml:70-80`（enum）+ taskParam cases（`gearConnectedBlock` case:151-159 の直後）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`（dummyText バンプ→再生成）
- Modify: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/VanillaChallengeType.cs`, `ChallengeFactory.cs:12-21`
- Create: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/GearConnectToBlockChallengeTask.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs:109-128`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/challenges.json`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/GearConnectToBlockChallengeTaskTest.cs`

**Interfaces:**
- Produces: taskCompletionType 文字列 `gearConnectToBlock`、TaskParam `GearConnectToBlockTaskParam { Guid BlockGuid /*設置して繋ぐ側*/, Guid ConnectedBlockGuid /*接続先種別*/ }`（Mooresmaster 生成）
- Consumes: `IGearEnergyTransformer.GetGearConnects()`（`Game.Gear/Common/IGearEnergyTransformer.cs:24`）

- [ ] **Step 1: スキーマ追加** — challenges.yml の options に `- gearConnectToBlock`、cases に:

```yaml
              - when: gearConnectToBlock
                type: object
                properties:
                # 設置して繋ぐ側のブロック / The block the player places to connect
                - key: blockGuid
                  type: uuid
                  foreignKey:
                    schemaId: blocks
                    foreignKeyIdPath: /data/[*]/blockGuid
                    displayElementPath: /data/[*]/name
                # 接続先のブロック種別（回転は問わず接続成立で完了） / Target block kind; completes on connection regardless of rotation
                - key: connectedBlockGuid
                  type: uuid
                  foreignKey:
                    schemaId: blocks
                    foreignKeyIdPath: /data/[*]/blockGuid
                    displayElementPath: /data/[*]/name
```

`_CompileRequester.cs` の dummyText を書き換え → `uloop compile --project-path ./moorestech_client` → `Mooresmaster.Model.ChallengesModule.GearConnectToBlockTaskParam` が生成されることを確認（`grep -r "GearConnectToBlockTaskParam" moorestech_server/Assets/Scripts` 等）。

- [ ] **Step 2: 失敗するテストを書く** — `GearConnectedBlockChallengeTaskTest.cs` を雛形に（DI生成→`InitializeCurrentChallenges()`→`TryAddBlock`→`GameUpdater.UpdateOneTick()`→完了GUID検査）。forUnitTest challenges.json に新チャレンジを追加（既存 gearConnectedBlock 例 :278-291 を複製し guid 例 `00000000-0000-0000-4567-000000000105`、taskCompletionType `gearConnectToBlock`、blockGuid=Shaft、connectedBlockGuid=GearBeltConveyor 相当のテストブロック。forUnitTest blocks.json に GearBeltConveyor が無ければ gearConnects を持つ既存の別種ブロック（Gear等）を接続先に使う）:

```csharp
[Test]
public void シャフトを対象ブロックの横に置くと回転していなくても完了する()
{
    // ベルト相当ブロックを先に置き、隣にシャフトを置く（発電機なし＝RPM 0）
    // Place the belt-like target first, then the shaft next to it with no generator (RPM stays 0)
    world.TryAddBlock(beltBlockId, new Vector3Int(0, 0, 0), BlockDirection.North, ...);
    world.TryAddBlock(shaftBlockId, new Vector3Int(0, 0, 1), BlockDirection.North, ...);
    GameUpdater.UpdateWithWait();
    Assert.IsTrue(challengeDatastore.GetOrCreateChallengeInfo(playerId).CompletedChallenges.Any(c => c.ChallengeGuid == ChallengeGuid));
}

[Test]
public void シャフト単体では完了しない() { /* シャフトだけ置いて1tick回し、未完了をassert */ }
```

- [ ] **Step 3: 失敗確認** — Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "GearConnectToBlockChallengeTaskTest"` → FAIL（type未登録でマスタ検証 or Factory例外）

- [ ] **Step 4: 実装** — `GearConnectToBlockChallengeTask.cs`（`BlockPlaceOnVeinChallengeTask` の骨格を写す）:

```csharp
/// <summary>
///     対象ブロックが接続先種別へ歯車接続した時に達成する。回転（RPM）は見ない
///     Completes when the placed block gear-connects to the target kind; RPM is never inspected
/// </summary>
public class GearConnectToBlockChallengeTask : IChallengeTask
{
    // OnBlockPlaceEvent で blockGuid 一致の設置を候補集合へ積み、初回ティックで既存ブロックも回収
    // ManualUpdate で候補ごとに TryGetComponent<IGearEnergyTransformer> → GetGearConnects()
    //   → connect.Transformer.BlockInstanceId を WorldBlockDatastore で引き BlockGuid == ConnectedBlockGuid で完了
}
```

`VanillaChallengeType.cs` に `public const string GearConnectToBlock = "gearConnectToBlock";`、`ChallengeFactory` に `_taskCreators.Add(VanillaChallengeType.GearConnectToBlock, GearConnectToBlockChallengeTask.Create);`。
`ChallengeMasterUtil.TaskParamValidation` に case 追加（両GUIDの実在＋双方 `BlockParam is not IGearConnectors` なら error。gearConnectedBlock case :109-125 を雛形に）。

- [ ] **Step 5: テストPASS＋マスタ検証テスト** — Run: `uloop run-tests ... --filter-value "GearConnectToBlockChallengeTaskTest|ChallengeMasterValidationTest"` → PASS
- [ ] **Step 6: コミット** — `git commit -m "feat(challenge): 歯車接続成立で完了するgearConnectToBlock判定を追加する"`

### Task 4: 連結設置検査＋連結ゴースト（chainBlockPlacePreview）

**Files:**
- Modify: `VanillaSchema/challenges.yml`（tutorialType enum:169-181 に `chainBlockPlacePreview`、cases に param）
- Create: `.../PlaceSystem/ChainPreview/ChainPlacePreviewState.cs`
- Create: `.../PlaceSystem/Common/ChainPlacementReporter.cs`
- Create: `.../Tutorial/PlacementGuide/ChainBlockPlacePreviewTutorialManager.cs`
- Modify: `.../PlaceSystem/Common/CommonBlockPlaceSystem.cs`（:168 の鉱脈チェック直後に reporter、:181 の歯車接続線の後に連結ゴースト描画）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（SerializeField+RegisterComponent、:176-177 の並びに追加）＋ MainGame.unity への AddComponent（`uloop execute-dynamic-code` 経由。手動シーン編集禁止）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Localization/MasterSourceTextCollector.cs:115-140`（`chainBlockPlacePreview` → null）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs`（TutorialValidation に case）
- Modify: `Localization/localization.csv`（`ui.tooltip.placeChainBlocked,接続先のスペースが埋まっています,...`）→ `_CompileRequester` バンプ＋`cd moorestech_web/webui && pnpm gen:i18n`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ChainPlacementReporterTest.cs`

**Interfaces:**
- Produces: tutorialParam `ChainBlockPlacePreviewTutorialParam { ChainBlocksElement[] ChainBlocks }`、各要素 `{ Guid BlockGuid, Vector3Int Offset, string BlockDirection }`（設置中ブロックのNorth基準ローカル。設置プレビューの pending 向きで回転）
- Produces: `ChainPlacePreviewState.SetChain(Guid tutorialGuid, BlockId anchorBlockId, IReadOnlyList<ChainGhost> chain)` / `Clear(Guid)` / `TryGetChain(BlockId holdingBlockId, out IReadOnlyList<ChainGhost>)`
- Produces: `static void ChainPlacementReporter.MarkChainBlockedCellsAsNotPlaceable(List<PlaceInfo> placeInfos, BlockMasterElement holdingBlockMaster, int cursorIndex, ChainPlacePreviewState state, IExistingBlockQuery existingBlockQuery, PlacementFeedback feedback)`

- [ ] **Step 1: スキーマ追加＋再生成** — challenges.yml cases に:

```yaml
              - when: chainBlockPlacePreview
                type: object
                openedByDefault: true
                properties:
                # 設置中ブロックのNorth基準ローカルで連結ゴースト群を定義する
                # Chain ghosts defined in the being-placed block's North-basis local frame
                - key: chainBlocks
                  type: array
                  items:
                    type: object
                    properties:
                    - key: blockGuid
                      type: uuid
                      foreignKey:
                        schemaId: blocks
                        foreignKeyIdPath: /data/[*]/blockGuid
                        displayElementPath: /data/[*]/name
                    - key: offset
                      type: vector3Int
                    - key: blockDirection
                      type: enum
                      default: North
                      options: [UpNorth, UpEast, UpSouth, UpWest, North, East, South, West, DownNorth, DownEast, DownSouth, DownWest]
```

（enum options の正確な並びは既存 relativeBlockPlacePreview case :350-366 を逐語コピー）。`_CompileRequester` バンプ→compile→生成型確認。array<object> の書式は `VanillaSchema` の既存前例（blocks.yml の gearConnects 等）に合わせ、`edit-schema` スキルを必ず参照する。

- [ ] **Step 2: 失敗するテストを書く** — `ChainPlacementReporterTest.cs`（`Client.Tests/PlaceSystem/` の既存 `PlacementTargetCatalogTest` 同様の pure C# 形式。`IExistingBlockQuery` をテストダブルで差し替え）:

```csharp
[Test]
public void 連結セルが既存ブロックで塞がれているとカーソルセルが設置不可になる()
{
    // 風車のカーソルセルに対し、シャフト位置(-1,0,2)が塞がれているケース
    // Chain cell (-1,0,2) is occupied, so the windmill's cursor cell must turn unplaceable
    var placeInfos = CreateSingleCellRun(windmillMaster, new Vector3Int(10, 0, 10), BlockDirection.North);
    var query = new StubExistingBlockQuery(occupied: new Vector3Int(9, 0, 12)); // 10,0,10 + (-1,0,2)
    ChainPlacementReporter.MarkChainBlockedCellsAsNotPlaceable(placeInfos, windmillMaster, 0, stateWithChain, query, feedback);
    Assert.IsFalse(placeInfos[0].Placeable);
    Assert.IsTrue(feedback.Lines.Any(l => l.Key == LocalizationKeys.UiTooltipPlaceChainBlocked));
}

[Test]
public void 連結セルが空いていれば設置可のまま() { /* query空でPlaceable維持をassert */ }

[Test]
public void 設置向きを東へ回すと連結セルも回る() { /* direction=Eastで回転後セルの塞ぎだけが効くことをassert */ }
```

（`LocalizationKeys` の実キー名・`feedback.Lines` の読み口は既存 `VeinPlacementReporter` のテスト（05d234b4e が追加したテスト群）を開いて同じ流儀に合わせる）

- [ ] **Step 3: 失敗確認** → コンパイルエラー
- [ ] **Step 4: 実装**

`ChainPlacePreviewState`（`VeinRestrictedPlacementState.cs:11` を雛形に、tutorialGuid オーナートークン方式）。
`ChainBlockPlacePreviewTutorialManager`（`VeinRestrictedPlacementTutorialManager` 同型。ApplyTutorial で param→`ChainGhost`（BlockId, Vector3Int offset, BlockDirection）リスト化して SetChain、CompleteTutorial で Clear）。
`ChainPlacementReporter`: 保持ブロックが state に無ければ即 return。カーソル PlaceInfo ごとに各 chain 要素のワールドセルを `原点 + 回転(offset)`（回転は `placeInfo.Direction` の `GetCoordinateConvertAction`。ブロック原点補正は `BlockDirection.GetBlockModelOriginPos` 系の前例に従い、Task 1 と同じ換算をローカル関数に切り出して共有）で求め、(a) `existingBlockQuery` の重なり、(b) 地形: `PlacementGroundCellResolver.TryResolveCellFromGround` で当該XZの地表セルYを取り、chainセルYと不一致（埋まり/浮き）なら blocked。blocked が1つでもあれば `placeInfo.Placeable = false`、`i == cursorIndex` のとき `feedback.Add(new TooltipLine(ui.tooltip.placeChainBlocked))`。
`CommonBlockPlaceSystem`: `:168` の `VeinPlacementReporter...` 直後に reporter 呼び出し1行、`:181` の後に連結ゴースト描画（`BlockPlacePreviewTutorialManager` は使わず、`TutorialPreviewBlockCreator` で chain 要素ぶんのプレビューを持つ小さな描画パート `ChainPlacementPreviewPart` を `ChainPreview/` に新設。カーソル移動・向き変更で `SetTransform`、非表示条件は state 空 or 距離外。ゴースト色は blocked セルのみ `SetPlaceableColor(false)`）。
localization.csv（vanilla）へ1行追加→`_CompileRequester`バンプ→`pnpm gen:i18n`。
`MasterSourceTextCollector.GetTutorialDisplayText` に `ChainBlockPlacePreviewTutorialParam => null` の case。`ChallengeMasterUtil.TutorialValidation` に各 chainBlocks の blockGuid 実在チェック case。
DI: `MainGameStarter` に SerializeField+`RegisterComponent(...).AsSelf().As<ITutorialViewManager>()`（:176-177 の並び）。シーンへの AddComponent は `uloop execute-dynamic-code` で行う（Prefab/シーンの直編集禁止）。

- [ ] **Step 5: コンパイル＋テストPASS** — `uloop run-tests ... --filter-value "ChainPlacementReporterTest|LocalizationKeysFreshness|ChallengeMasterValidationTest"`（webui側は `cd moorestech_web/webui && pnpm test -- localizationKeysFreshness` 相当。CIで赤にならないことをローカルで確認）
- [ ] **Step 6: コミット** — `git commit -m "feat(place): 風車設置時に連結ゴーストを表示し連結セルが塞がれていれば設置不可にする"`

### Task 5: マスタデータ再編（moorestech_master）

**Files:**（moorestech_master、origin/master 起点の新ブランチ `feature/tutorial-chain-refinements-adr0048`）
- Modify: `tools/tutorial_v3_port/generate_challenges.py`
- Regenerate: `server_v8/mods/moorestechAlphaMod_8/master/challenges.json`
- Modify: `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`

**Interfaces:**
- Consumes: Task 3 の `gearConnectToBlock`、Task 4 の `chainBlockPlacePreview`（文字列は逐語一致必須）
- Produces: 28本の新チェーン＋push済みコミットhash（Task 6 が使う）

- [ ] **Step 1: generate_challenges.py の CHALLENGES 表を再編**（key不変・新規は新key。ヘルパ追加: `chain_preview(entries)`＝chainBlockPlacePreview、task種 `gearConnectTo`→`gearConnectToBlock`＋`connectedBlockGuid` 出力）:

変更一覧（順序どおり）:
1. key `木を伐採して原木を入手する`: title→`原木を3個入手する`（summary・count=3 据え置き）
2. key `木の板を5枚作る`: iv 文言→`①木の板をクリックして選択`
3. key `木の棒を2本作る`・`砕いた石材を2個作る`・`レンガを作る`・`青銅鉱石の粉を1個作る`・`青銅インゴットを作る`（クラフト要求全部）: tutorials に `key('GameScreen','Tab','インベントリを開く')` を追加（既にある行はそのまま）
4. 新規 key `粘土鉱脈に風力掘削機を設置する`（task=blockOnVein, blockGuid=風力掘削機, veinGuid=粘土鉱脈, `vein('粘土鉱脈...','粘土鉱脈の上に設置')`＋`key('GameScreen','B','ビルドメニューを開く')`）を `粘土を入手する` の直前に挿入し、`粘土を入手する` から veinPin を除去
5. 新規 key `青銅の鉱脈に風力掘削機を設置する`（同型）を `青銅の鉱石を2個採掘する` の直前に挿入し、同チャレンジから veinPin を除去
6. key `燃料式風車を設置する`: tutorials に `chain_preview([(木のシャフト,(-1,0,2),'East'),(原始的な粉砕機,(-4,0,2),'North')])` を追加
7. key `木のシャフトで風車と繋ぐ`・`粉砕機を設置して動かす` の2行を削除し、新規 key `シャフトと粉砕機を設置して動かす`（task=gearConnected, blockGuid=原始的な粉砕機, tutorials=`relative_preview(燃料式風車,木のシャフト,(-1,0,2),'East','ここに木のシャフトを設置')`＋`relative_preview(燃料式風車,原始的な粉砕機,(-4,0,2),'North','ここに粉砕機を設置')`）を同位置に置く
8. key `青銅シートを作る`・`木釘を3本作る`・`合板を作る` の3行を削除（prev連結は生成側が並び順で張るため自動で繋がることを確認。明示prevなら付け替え）
9. `原始研究7を完了する`（新番号4）の直後に新規2行: key `歯車ベルトコンベアを設置する`（task=blockPlace, blockGuid=直線歯車ベルトコンベア, `key('GameScreen','B','ビルドメニューを開く')`）→ key `木のシャフトをベルトの横に設置する`（task=gearConnectTo, blockGuid=木のシャフト, connectedBlockGuid=直線歯車ベルトコンベア, `relative_preview(直線歯車ベルトコンベア,木のシャフト,(0,0,1),'North','ベルトの横に置くと繋がる')`）

- [ ] **Step 2: 再生成＋冪等確認** — `python3 tools/tutorial_v3_port/generate_challenges.py` → `OK: 28 challenges`（32→…→28: +2 -1 -3 +2）。再実行で `git status --short` 差分ゼロ
- [ ] **Step 3: localization.csv 追随** — plan `2026-08-28-early-game-compression-master-data.md:430-540` の Task M4 heredoc パターンを流用: 削除チャレンジのキー行削除・新規チャレンジ/チュートリアルのキー行追加・文言変更行の Source/japanese/english/german 更新。整合スクリプト（同plan Task M4 Step 1/3）で `challenge.*`==28本・orphan 0 を確認
- [ ] **Step 4: コミット＋push＋PR** — `git checkout -b feature/tutorial-chain-refinements-adr0048 origin/master`（moorestech_master 側）→ commit → push → `gh pr create`（本文に moorestech ADR 0048 参照、🤖 Generated with [Claude Code](https://claude.com/claude-code) 付き）。**PR無しのpush止まり禁止**

### Task 6: ピン更新＋レイアウト検証テスト

**Files:**
- Modify: `.moorestech-external-revisions.json`（moorestech_master ピンを Task 5 のpush済みコミットへ）
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EarlyGame/EarlyGameGearTutorialLayoutTest.cs`

**Interfaces:**
- Consumes: `PinnedMasterRepository`（`Client.Tests/Support/PinnedMasterRepository.cs`、`git show <hash>:<path>` でピン済みマスタを読む）

- [ ] **Step 1: 閉PR#1286 のテストを復活** — `git show feature/early-game-compression-master:moorestech_client/Assets/Scripts/Client.Tests/EarlyGame/EarlyGameGearTutorialLayoutTest.cs > <新規パス>` で取得し、新チェーンへ改修:
  - 統合チャレンジ（key `シャフトと粉砕機を設置して動かす` の uuid5 GUID）から relative_preview 2件を読む形へ
  - **4方位パラメタライズ**: 風車を North/East/South/West で設置し、`ConvertBlockLocalToWorldCell`＋`AnchorRelativeDirectionUtil` で解決したセル・向きにシャフト・粉砕機を置き、風車に原木投入で粉砕機の `CurrentRpm > 0` を検証
  - ベルト段: ベルト設置→ピン済み offset(0,0,1) にシャフト設置→ `GetGearConnects()` にベルトが含まれることを検証（RPM不要）
- [ ] **Step 2: ピン更新前に FAIL することを確認**（旧ピンは統合チャレンジが無い）→ Expected: FAIL/Ignore
- [ ] **Step 3: ピン更新** — `.moorestech-external-revisions.json` の commitHash を Task 5 コミットへ書き換えてコミット（他フィールド不変）
- [ ] **Step 4: テストPASS＋マスタ整合一式** — `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "EarlyGameGearTutorialLayoutTest|TutorialAnchorContractTest|ChallengeMasterValidationTest|LocalizeContent|GameDictionaryRecomposition"`
  - FAILした場合の座標是正: 旧plan `2026-08-28-early-game-compression-master-data.md:648` の手順（gearConnects 印字→セル再計算→generate スクリプト修正→再生成→ピン再更新）
- [ ] **Step 5: コミット** — `git commit -m "test: 接続チュートリアル4方位とベルト隣接のレイアウト検証を追加しマスタピンを更新する"`

### Task 7: unityプレイ録画テストで通し確認

- [ ] **Step 1**: `unity-playmode-recorded-playtest` スキルのプレイテストDSL（`scripts/run-scenario.sh`）で、新チェーンの「燃料式風車を設置する」→「シャフトと粉砕機を設置して動かす」を風車East向きで通し、粉砕機稼働＝チャレンジ完了と録画を確認する（masterピンはブランチ互換コミットのworktreeを使う）
- [ ] **Step 2**: 録画を確認し、連結ゴーストの表示・塞がりセルの赤表示・完了時のピン消滅を目視確認。問題があれば該当Taskへ戻る

### Task 8: 全ブランチレビュー（必須・省略不可）

- [ ] 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（moores-code-review。自動実行・ゴール文言による省略不可）。指摘対応後、pr-create で本repo PR を作成し、moorestech_master PR と相互リンクする

## 判断記録（ADR）

- 設計裁定の正本: `docs/adr/0048-tutorial-refinements-ghost-rotation-and-chain-cleanup.md`（裁定原文リンク付き）。関連: ADR 0038・0043
- 新タスク完了型の名称 `gearConnectToBlock`・新チュートリアル型 `chainBlockPlacePreview`・param形状: agent前提（ADR 0048 が命名をplanへ委任）
- ベルト段のアンカーを「直線歯車ベルトコンベア（種別・最寄り解決）」とし、上り/下りベルトはチュートリアル対象外: agent前提（相対ゴーストの最寄りアンカー解決は種別ベースが既存仕様）
- 連結ゴースト描画は BlockPlacePreviewTutorialManager を使わず設置システム側の描画パートで持つ: agent前提（チュートリアルゴーストはアンカー既設前提・こちらは設置中カーソル追従で駆動元が異なる。VeinPlacementReporter と同じ「設置パイプラインの1ステップ」に置く）
- 地形判定は「地表セルYと連結セルYの一致」で埋まり/浮きの両方を弾く: agent前提（ユーザー裁定「地形の埋まりを考慮」の実装解釈。通常設置の GroundNotFound と同じ resolver を使う）
- チャレンジ数28本維持（+2設置分割 -1統合 -3削除 +2ベルト）: 機械的帰結
- マスタピンは Task 5 ブランチのpush済みコミットへ更新（現ピン 6fdf04d は序盤圧縮PR#50を含まない古い状態のため、origin/master 起点で切り直す）: agent前提
