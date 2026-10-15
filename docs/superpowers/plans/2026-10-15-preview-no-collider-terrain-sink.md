# 設置プレビューの当たり判定撤去と地形めり込み判定の全設置系統一 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 設置プレビューを当たり判定ゼロの見た目専用にし、地形めり込み（底から1セルより深い沈み）を全設置系・BP各ブロックで同じ基準・同じ高さサンプルで判定する。

**Architecture:** 判定は新設の静的クエリ `PlacementTerrainSinkQuery` が `GroundHeightProbe` と `PlacementGroundCellResolver.ResolveCellY`（設置Yの決定と同じ許容誤差）で行う。「地形が自然に置くセル > ブロックの底セル」なら埋まり。プレビュー制御（`IPlacementPreviewBlockGameObjectController`）は地形を一切知らなくなり、全Colliderを無効化する。BPは共有plannerの要素ごとの不可原因を bool から3値enumへ広げ、クライアントが判定した埋まり位置をリクエストで送り、サーバーはそれを重なりと同じく飛ばす。

**Tech Stack:** Unity C#（moorestech_client / moorestech_server）、NUnit EditMode、MessagePack、uloop。

## Requirements

- R1 地形めり込みの基準: フットプリント内のどこかで地表がブロック底面から1セルの高さを超えたら設置不可。判定式は `PlacementGroundCellResolver.ResolveCellY(groundMax, step, 0) > ブロック底セルY`。受け入れ: 高さ3で地表が底+0.6 → 可、底+1.6 → 不可、高さ1で地表が底+0.6 → 可、底+1.2（上面より上）→ 不可、地表が見つからない → 可（埋まり得ない）
- R2 適用範囲: 通常設置（単体・ドラッグ）、BP貼り付けの各ブロック、ベルト、電線ツールの電柱、歯車チェーンポール、レール橋脚の全6系統で R1 を使う。受け入れ: 各系統で Q/E 等により1セル以上沈めたセルが赤くなり「地形に埋まっています」が出て送信されない
- R3 既定設置位置では不可にならない: 既定設置Y（地形最高点を含むセル、オフセット0）で置いたブロックは R1 で不可にならない（同じ ResolveCellY を使うため構造的に成り立つ）。受け入れ: 端数地形・斜面で歯車チェーンポール・電柱・レール橋脚が置ける
- R4 プレビューは当たり判定を持たない: `PreviewBlockCreator.Create` が全Colliderを `enabled=false` にする。受け入れ: 生成直後のプレビュー配下の `Collider.enabled` が全て false、プレビュー配下に Rigidbody・GroundCollisionDetector が無い
- R5 撤去: `ConfigurePlacementColliders`・`ConfigureTerrainContact`・`SetTerrainContactEnabled`・`DetectCurrentGroundOverlap`・`PlacementGroundContactQuery`・`IPlacementPreviewBlockGameObjectController.DetectGroundOverlaps`・`SetPreview` の `terrainContactEnabled` 引数・歯車チェーンポールの「選択レイ前の前フレームゴーストHide」・`BlockClickDetectUtil`／`DeleteTargetRaycaster` のプレビュー命中スキップ・`GroundCollisionDetector`（クラス・5Prefabの部品・`BlockGameObject` の無効化・`BlockGameObjectColliderSetup` の除外分岐）。受け入れ: `grep -rn "GroundCollisionDetector\|DetectGroundOverlaps\|PlacementGroundContactQuery\|ConfigureTerrainContact" moorestech_client/Assets/Scripts` が0件
- R6 BP部分設置: 埋まるブロックだけ赤く表示して飛ばし、残りを置く。全ブロックが重なり／埋まりで不可ならBP不可。飛ばしたブロックを端点に持つ電線・歯車チェーンは張らない（ADR 0077 決定4と同じ）。受け入れ: サーバーテストで、埋まり位置を送ったブロックが置かれず素材も消費されず、その端点の線も張られない
- R7 BPの除外はクライアントが送る: BPリクエストに埋まりブロックの位置列 `TerrainSunkBlockPositions` を載せる。サーバーは地形を検証せず、その位置の要素を不可にする。受け入れ: 位置列が空なら従来どおり全部置く／位置列にある要素だけ置かない
- R8 表示: 不可理由は既存の `PlacementFeedback.AddBlockedByTerrain`（「地形に埋まっています」）を全系統で使う。BPは埋まり要素が1つでもあれば行を出す（重なり行が「全コピーが全滅のときだけ」なのと非対称にするのは、重なりは既存ブロックが見えて赤ゴーストだけで原因が分かるが、地中への食い込みは見た目で分かりにくく、BPにはカーソルセルの理由行も無いため）
- R9 接続線（電線・歯車チェーン・レール）は地形判定しない（埋まってよい）
- R10 残すもの: #1507 の「4設置系の設置Yを占有範囲の地形最高点を含むセルにする」、接続線返却の MaxStack 分割はそのまま
- やらないこと: ベルトの地形迂回ルート計算（bd moorestech-5wy86）。サーバー側の地形検証。セーブ形式の変更・既存セーブの地中ブロックの扱い。4隅サンプル間の尖りの検出

## Global Constraints

- AGENTS.md の全規約（1ファイル200行以下、1ディレクトリ10ファイルまで、partial・Func<>・デフォルト引数・try-catch禁止、日英2行コメント、`#region Internal` はメソッド内ローカル関数用のみ、.meta手動作成禁止、Prefab は uloop execute-dynamic-code 経由でのみ編集）
- 無音の縮退禁止。黙って続行する行には `silent-ok: <理由> / <誰がどこで気づくか>` を書く
- .cs 変更後は必ず `uloop compile --project-path ./moorestech_client`
- 判定基準の正本: ADR 0081（`docs/adr/0081-preview-has-no-collider-and-terrain-sink-gate-for-all-placement.md`）
- セーブ形式は変えない（`WorldSaveAllInfo.CurrentVersion` は触らない）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 新クエリはGround/（GroundHeightProbe同型）、BPはIBlueprintPasteWorldの2実装に乗せ、死活表で影響操作（クリック・削除・ポール選択レイ）は当たり判定ゼロで生存
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強2・弱1・第3バケツ0 / ADR食い違い2点はADRをplanへ合わせ、重複は据え置き、BPの要素可否判定はIsPlaceableCause 1本へ集約、BlueprintRequest は型を分けず1項目足す（裁定済み）

---

## File Structure

| ファイル | 責務 | 操作 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/PlacementTerrainSinkQuery.cs` | 地形めり込みの唯一の判定（R1） | Create |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Feedback/PlacementCellReasonReporter.cs` | 埋まり列を Placeable と理由行へ反映（入口を1本化） | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/NormalPlacementPreviewStep.cs` | 通常設置も埋まり判定を通す | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs` | ベルトの判定差し替え | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRail/TrainRailPlaceService.cs` | 橋脚の判定差し替え | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWirePoleGhostPart.cs` | 電柱の判定差し替え | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Parts/GearChainPoleExtendPreviewObject.cs` | 歯車ポールの判定差し替え | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/GearChainPoleConnectSystem.cs` | 選択レイ前Hideの撤去 | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/IPlacementPreviewBlockGameObjectController.cs` | 地形APIの撤去 | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/PlacementPreviewBlockGameObjectController.cs` | 同上 | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/BlockPreviewObject.cs` | 地形接触・Collider設定の撤去 | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/PreviewBlockCreator.cs` | Collider無効化（R4） | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/PlacementGroundContactQuery.cs` | 撤去 | Delete |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/GroundCollisionDetector.cs` | 撤去 | Delete |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObject.cs` | 検出器無効化ループの撤去 | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/BlockGameObjectColliderSetup.cs` | 検出器除外分岐の撤去 | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BlockClickDetectUtil.cs` | プレビュー命中スキップの撤去 | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/DeleteTargetRaycaster.cs` | 同上 | Modify |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPasteCopyDraft.cs` | BP要素の不可原因3値 enum を同居（Planning/ は既に10ファイルのため。前例: `BlueprintPasteCopyState` が `BlueprintPasteCopyPlan.cs` に同居） | Modify |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/IBlueprintPasteWorld.cs` | `IsTerrainSunk` 追加 | Modify |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPasteCopyBuilder.cs` / `BlueprintPasteCopyDraft.cs` / `BlueprintPasteCopyPlan.cs` / `BlueprintPasteMaterialAccumulator.cs` / `BlueprintPastePlanner.cs` | NonOverlapFlags → ElementBlockCauses | Modify |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/ServerBlueprintPasteWorld.cs` / `BlueprintPasteOperationHandler.cs` | 送られた埋まり位置で判定 | Modify |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Blueprint/BlueprintRequest.cs` | `TerrainSunkBlockPositions` 追加 | Modify |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/ClientBlueprintPasteWorld.cs` / `BlueprintPastePlaceSender.cs` / `Transport/BlueprintPasteRequestRunner.cs` / `BlueprintPasteFeedbackReporter.cs` / `BlueprintPastePreviewController.cs` / `BlueprintPasteVisualState.cs` | クライアント判定・送信・表示 | Modify |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementTerrainSinkQueryTest.cs` | R1 のテスト | Create |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/Preview/` 配下の #1499/#1507 テスト5本 | 撤去機構のテスト | Delete（Task 3） |

## 配置と前例

- `PlacementTerrainSinkQuery` は `Ground/`（`GroundHeightProbe`・`PlacementGroundCellResolver` と同じ「地表から設置可否・Yを答える」役割の置き場）。静的クラスなのは `GroundHeightProbe` と同型
- BP の要素ごとの不可原因は共有 planner（`Server.Protocol` の `Planning/`）に置く。クライアント・サーバーが同じ planner を回す既存構造（`IBlueprintPasteWorld` の2実装）に従い、地形だけクライアント実装が実判定・サーバー実装がリクエスト値参照になる（既存 `BlueprintPasteOrigin.IsGroundFound` を「サーバーは地形が無いので送信値を信じる」とする前例 `BlueprintPasteOperationHandler.cs` の同型）
- Collider 無効化は `TrainCarPreviewController.DisableColliders`（`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainCar/TrainCarPreviewController.cs:107`）と同型

---

### Task 1: 地形めり込みクエリと全6系統への配線

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/PlacementTerrainSinkQuery.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementTerrainSinkQueryTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Feedback/PlacementCellReasonReporter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/NormalPlacementPreviewStep.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs:141-148`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRail/TrainRailPlaceService.cs:53-59`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWirePoleGhostPart.cs:70-81`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Parts/GearChainPoleExtendPreviewObject.cs:30-40`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/IPlacementPreviewBlockGameObjectController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/PlacementPreviewBlockGameObjectController.cs`
- Modify（テスト追従）: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/NormalPlacementPreviewStepTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Feedback/PlacementCellReasonReporterTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/Entry/RecordingGroundedPreview.cs`、`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/PlacementHeightPlaceSystemWiringTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/TrainCostIntegration/RecordingPierPreview.cs`

**Interfaces:**
- Produces:
  - `public static class PlacementTerrainSinkQuery`
    - `public static bool IsSunk(Vector3Int blockPos, BlockDirection blockDirection, Vector3Int blockSize)`
    - `public static List<bool> DetectSunkCells(IReadOnlyList<PlaceInfo> placeInfos)`（`placeInfos[i].BlockId` のマスターサイズで判定。添字は placeInfos と並走）
    - `internal static bool IsSunkAbove(int blockBaseCellY, float groundMaxHeight, float heightQuantizationStep)`（純粋関数）
  - `PlacementCellReasonReporter.ApplyTerrainSinksAndReport(List<PlaceInfo> placeInfos, IReadOnlyList<PlacementBlockCause> cellCauses, Vector3Int cursorCell, IReadOnlyList<bool> terrainSinks, PlacementFeedback feedback) : int`（旧 `ApplyGroundOverlapsAndReport` の改名。`ResolveCursorAndReportCauses` は削除）
  - `IPlacementPreviewBlockGameObjectController.SetPreview(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster)`（第3引数削除）。`DetectGroundOverlaps()` 削除

- [ ] **Step 1: テストを書く** — `PlacementTerrainSinkQueryTest.cs`

```csharp
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Ground
{
    // 底から1セルより深い沈みだけを埋まりとする（ADR 0081）
    // Only a sink deeper than one cell from the bottom counts as buried (ADR 0081)
    public class PlacementTerrainSinkQueryTest
    {
        private readonly List<GameObject> _slabs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var slab in _slabs) Object.DestroyImmediate(slab);
            _slabs.Clear();
        }

        [TestCase(10, 10.6f, false)]
        [TestCase(10, 10.99f, false)]
        [TestCase(10, 11.2f, true)]
        [TestCase(9, 10.6f, true)]
        [TestCase(11, 10.6f, false)]
        public void 地表が底セル上面を超えたら埋まり(int baseY, float groundMax, bool expected)
        {
            Assert.AreEqual(expected, PlacementTerrainSinkQuery.IsSunkAbove(baseY, groundMax, 0f));
        }

        [Test]
        public void 高さ3は底から0点6沈んでも可で1点6なら不可()
        {
            // 天面が y=30.6 の地面を作る
            // Ground whose top is y=30.6
            CreateGroundSlab(new Vector3(0.5f, 30.1f, 0.5f), new Vector3(4f, 1f, 4f));
            var size = new Vector3Int(1, 3, 1);

            Assert.IsFalse(PlacementTerrainSinkQuery.IsSunk(new Vector3Int(0, 30, 0), BlockDirection.North, size));
            Assert.IsTrue(PlacementTerrainSinkQuery.IsSunk(new Vector3Int(0, 29, 0), BlockDirection.North, size));
        }

        [Test]
        public void 高さ1も上面より上に地表があれば不可()
        {
            CreateGroundSlab(new Vector3(0.5f, 30.1f, 0.5f), new Vector3(4f, 1f, 4f));

            Assert.IsFalse(PlacementTerrainSinkQuery.IsSunk(new Vector3Int(0, 30, 0), BlockDirection.North, Vector3Int.one));
            Assert.IsTrue(PlacementTerrainSinkQuery.IsSunk(new Vector3Int(0, 28, 0), BlockDirection.North, Vector3Int.one));
        }

        [Test]
        public void 地表が無ければ埋まりではない()
        {
            Assert.IsFalse(PlacementTerrainSinkQuery.IsSunk(new Vector3Int(500, 0, 500), BlockDirection.North, Vector3Int.one));
        }

        private void CreateGroundSlab(Vector3 position, Vector3 scale)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.layer = LayerConst.GroundLayer;
            slab.transform.position = position;
            slab.transform.localScale = scale;
            Physics.SyncTransforms();
            _slabs.Add(slab);
        }
    }
}
```

- [ ] **Step 2: クエリを実装する** — `PlacementTerrainSinkQuery.cs`

```csharp
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     地形めり込みの唯一の判定。底から1セルより深く沈んだら埋まり（ADR 0081）
    ///     The single terrain-sink judgement: buried when sunk deeper than one cell from the bottom (ADR 0081)
    /// </summary>
    public static class PlacementTerrainSinkQuery
    {
        public static bool IsSunk(Vector3Int blockPos, BlockDirection blockDirection, Vector3Int blockSize)
        {
            // 地表が無い場所には埋まりようがない
            // Nothing can be buried where there is no ground
            // silent-ok: 地表欠落は設置Y解決側がGroundNotFoundとして理由行に出す / プレイヤーがツールチップで気づく
            if (!GroundHeightProbe.TryGetFootprintMaxGroundHeight(blockPos, blockDirection, blockSize, out var groundMax, out var step)) return false;
            return IsSunkAbove(blockPos.y, groundMax, step);
        }

        public static List<bool> DetectSunkCells(IReadOnlyList<PlaceInfo> placeInfos)
        {
            var sinks = new List<bool>(placeInfos.Count);
            foreach (var info in placeInfos)
            {
                var size = MasterHolder.BlockMaster.GetBlockMaster(info.BlockId).BlockSize;
                sinks.Add(IsSunk(info.Position, info.Direction, size));
            }
            return sinks;
        }

        // 設置Y決定と同じ許容誤差で「地形が自然に置くセル」を出し、底セルより上なら埋まり
        // Derive the terrain's natural cell with the same tolerance as Y resolution; above the base cell means buried
        internal static bool IsSunkAbove(int blockBaseCellY, float groundMaxHeight, float heightQuantizationStep)
        {
            return PlacementGroundCellResolver.ResolveCellY(groundMaxHeight, heightQuantizationStep, 0) > blockBaseCellY;
        }
    }
}
```

`PlaceInfo.Position` が占有範囲の最小角（`BlockPositionInfo.MinPos == OriginalPos`）であることを、`CommonBlockPlacePointCalculator` と `PlacementGroundCellResolver.TryResolveCellFromGround` の呼び出し（同じ `cellPosition` を渡している）で確認してから進む。食い違えば底セルYを `blockPos.GetWorldBlockBoundingBox(...)` の min.y から取る形に直す。

- [ ] **Step 3: 理由反映の入口を1本化する** — `PlacementCellReasonReporter.cs`

`ApplyGroundOverlapsAndReport` を `ApplyTerrainSinksAndReport` に改名し、引数名 `groundOverlaps` → `terrainSinks`、`Report` の引数名も同様に改名する。`ResolveCursorAndReportCauses` を削除する。クラス summary を次に差し替える:

```csharp
    /// <summary>
    ///     セル毎の地形めり込みをPlaceableと理由行へ反映する、全設置系共通の入口（ADR 0081）
    ///     The shared entry for every placement system that applies per-cell terrain sinks to Placeable and reason lines (ADR 0081)
    ///     設置系固有の理由は積まない。共有行の後にその系自身が積む
    ///     System-specific reasons are not pushed here; each system pushes its own after these shared lines
    /// </summary>
```

- [ ] **Step 4: プレビュー制御から地形APIを外す**

`IPlacementPreviewBlockGameObjectController.cs` から `DetectGroundOverlaps()` とその summary を削除し、`SetPreview` を `public void SetPreview(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster);` にする。`PlacementPreviewBlockGameObjectController.cs` で `SetPreview` の第3引数と `previewBlock.SetTerrainContactEnabled(...)` 行、`DetectGroundOverlaps()` メソッドを削除する。

- [ ] **Step 5: 6系統を差し替える**

`NormalPlacementPreviewStep.cs` 本体:

```csharp
    /// <summary>
    ///     通常ブロック設置のプレビュー投入と地形めり込み・カーソル理由集約を1手にまとめる（ADR 0081）
    ///     Bundles normal placement's preview submission with terrain-sink and cursor reason reporting (ADR 0081)
    /// </summary>
    public static class NormalPlacementPreviewStep
    {
        public static int Apply(IPlacementPreviewBlockGameObjectController previewController, List<PlaceInfo> placeInfos, IReadOnlyList<PlacementBlockCause> cellCauses, Vector3Int cursorCell, BlockMasterElement holdingBlockMaster, PlacementFeedback feedback)
        {
            previewController.SetPreview(placeInfos, holdingBlockMaster);
            var terrainSinks = PlacementTerrainSinkQuery.DetectSunkCells(placeInfos);
            return PlacementCellReasonReporter.ApplyTerrainSinksAndReport(placeInfos, cellCauses, cursorCell, terrainSinks, feedback);
        }
    }
```

`CommonBlockPlaceSystem.cs:175-176` のコメントを「プレビュー投入と地形めり込み・カーソル理由集約（ADR 0081）」/「Submit the preview and report terrain sinks and cursor reasons (ADR 0081)」に直す。

`BeltConveyorPlaceSystem.cs:141-148`:

```csharp
            _previewBlockController.SetPreview(_currentPlaceInfos, holdingBlockMaster);
            var terrainSinks = PlacementTerrainSinkQuery.DetectSunkCells(_currentPlaceInfos);

            // 共有の不可原因（既存重複・地形めり込み）は共通Reporterが積む
            // The shared block causes (existing overlap, terrain sink) are pushed by the shared reporter
            var cursorIndex = PlacementCellReasonReporter.ApplyTerrainSinksAndReport(_currentPlaceInfos, placeCauses, placePoint, terrainSinks, feedback);
```

`TrainRailPlaceService.cs:53-59`:

```csharp
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo() };
            _previewBlockController.SetPreview(placeInfos, holdingBlockMaster);
            var terrainSinks = PlacementTerrainSinkQuery.DetectSunkCells(placeInfos);

            // 地形に埋まるセルを設置不可にし、その理由を積む。レール1セルは共有原因を判定しないためNone列を渡す
            // Mark terrain-sunk cells unplaceable and report that reason; the single rail cell judges no shared cause, so a None column is passed
            PlacementCellReasonReporter.ApplyTerrainSinksAndReport(placeInfos, new[] { PlacementBlockCause.None }, placePoint, terrainSinks, feedback);
```

`ElectricWirePoleGhostPart.cs:70-81`（有効化コメントを消し、判定をプレビューから切り離す）:

```csharp
            _previewBlockController.SetActive(true);
            _previewBlockController.SetPreview(placeInfos, poleMaster);

            // 接触は可否として返すだけ。塗りとPlaceableの確定はモード側が持つ
            // The sink is only returned as a judgement; painting and Placeable stay with the mode
            var isGroundClear = !PlacementTerrainSinkQuery.IsSunk(placeInfos[0].Position, placeInfos[0].Direction, poleMaster.BlockSize);
```

`GearChainPoleExtendPreviewObject.PositionGhost`:

```csharp
        public bool PositionGhost(PlaceInfo placeInfo, BlockMasterElement poleBlockMaster)
        {
            _positionedPlaceInfos.Clear();
            _positionedPlaceInfos.Add(placeInfo);

            _ghostController.SetActive(true);
            _ghostController.SetPreview(_positionedPlaceInfos, poleBlockMaster);
            return !PlacementTerrainSinkQuery.IsSunk(placeInfo.Position, placeInfo.Direction, poleBlockMaster.BlockSize);
        }
```

同ファイル冒頭 summary の「（配置と地面判定）」を「（配置と地形めり込み判定）」/「(placement and terrain-sink check)」に、`PositionGhost` の summary を「ゴーストを配置し地形めり込みが無いかを返す入力フェーズのクエリ」/「Input-phase query that positions the ghost and returns whether it is free of terrain sink」に直す。

- [ ] **Step 6: テスト追従**
  - `RecordingGroundedPreview.cs`・`PlacementHeightPlaceSystemWiringTest.cs` 内フェイク・`RecordingPierPreview.cs`・`NormalPlacementPreviewStepTest.cs` 内フェイク: `SetPreview` を2引数にし、`DetectGroundOverlaps` 実装を削除する
  - `NormalPlacementPreviewStepTest.cs`: 「地形接触があっても全セル Placeable」「`DetectGroundOverlapsCallCount == 0`」を検証していたテストを、地面スラブ（上の `CreateGroundSlab` と同形）を置いて「1セル沈めたセルだけ `Placeable=false`、カーソルセルなら `feedback.Lines` に `PlaceBlockedByTerrain` が出る」「既定Yのセルは true のまま」へ書き換える。`DetectGroundOverlapsCallCount` は削除
  - `PlacementCellReasonReporterTest.cs`: 呼び出しを `ApplyTerrainSinksAndReport` へ改名し、`ResolveCursorAndReportCauses` を呼ぶテストがあれば `ApplyTerrainSinksAndReport`（全 false の列）へ置き換える

- [ ] **Step 7: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementTerrainSinkQueryTest|NormalPlacementPreviewStepTest|PlacementCellReasonReporterTest|PlacementHeightPlaceSystemWiringTest|TrainCostIntegration|Ground\."`
Expected: ErrorCount 0 / 全 PASS（この時点では Task 3 で消す #1499 系テストがコンパイルエラーになる場合、そのテストファイルを Task 3 の削除対象から前倒しで削除してよい）

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts
git commit -m "feat(place): 地形めり込みを全設置系で高さサンプルにより判定する"
```

### Task 2: BP貼り付けの埋まりブロックをクライアント判定で飛ばす

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/IBlueprintPasteWorld.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPasteCopyBuilder.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPasteCopyDraft.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPasteCopyPlan.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPasteMaterialAccumulator.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/Planning/BlueprintPastePlanner.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/ServerBlueprintPasteWorld.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/BlueprintPasteOperationHandler.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Blueprint/BlueprintRequest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/ClientBlueprintPasteWorld.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPastePlaceSender.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/Transport/BlueprintPasteRequestRunner.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteFeedbackReporter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPastePreviewController.cs:43`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteVisualState.cs:31`
- Modify（他の `NonOverlapFlags`・`IBlueprintPasteWorld` 実装・`CreatePasteRequest(s)` 参照すべて。`grep -rn "NonOverlapFlags\|IBlueprintPasteWorld\|CreatePasteRequest" moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts` で列挙し、テストのフェイク world を含め全部追従する）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BlueprintProtocolTest/BlueprintPasteResponseTest.cs`（追記）

**Interfaces:**
- Consumes: Task 1 の `PlacementTerrainSinkQuery.IsSunk(Vector3Int, BlockDirection, Vector3Int)`
- Produces:
  - `public enum BlueprintPasteElementBlockCause { None, ExistingBlock, TerrainSunk }`
  - `IBlueprintPasteWorld.IsTerrainSunk(BlockPositionInfo positionInfo) : bool`
  - `BlueprintPasteCopyDraft.ElementBlockCauses : IReadOnlyList<BlueprintPasteElementBlockCause>`（`NonOverlapFlags` を置換）、`BlueprintPasteCopyDraft.IsElementPlaceable(int elementIndex) : bool`、`BlueprintPasteCopyDraft.TerrainSunkPositions() : IEnumerable<Vector3Int>`
  - `BlueprintPasteCopyState.AllOverlapped` は `AllBlocked` に改名（全要素が None 以外）
  - `BlueprintRequest.TerrainSunkBlockPositions : List<Vector3IntMessagePack>`（`[Key(9)]`）、`BlueprintRequest.CreatePasteRequest(Guid, int, List<Vector3Int> origins, List<Vector3Int> terrainSunkBlockPositions)`・`CreatePasteRequests(Guid, int, List<Vector3Int>, List<Vector3Int>)`
  - `ServerBlueprintPasteWorld(PlacementTargetCatalog catalog, IGameUnlockStateDataController unlockState, bool isPaymentWaived, IReadOnlyCollection<Vector3Int> terrainSunkBlockPositions)`

- [ ] **Step 1: サーバーテストを書く** — `BlueprintPasteResponseTest.cs` に追加。既存テストの BP 作成・貼付ヘルパーに従い、2ブロック（AとB）を電線で結んだBPを用意し、Bの貼付先ワールド位置を `TerrainSunkBlockPositions` に入れて貼り付ける。期待: Aは置かれBは置かれない、Bの建設コストは消費されない、A–Bの電線は張られない、`TerrainSunkBlockPositions` が空のリクエストでは両方置かれる。テスト名は `埋まり位置として送ったブロックだけ置かず線も張らない` と `埋まり位置が空なら全ブロックを置く`。

- [ ] **Step 2: 共有 planner を3値へ広げる**

`BlueprintPasteCopyDraft.cs` の namespace 内、クラスの前に置く（新規ファイルにしない）:

```csharp
    // BP要素を置けない原因。地形はクライアントが判定して送る（ADR 0081）
    // Why a blueprint element cannot be placed; terrain is judged and sent by the client (ADR 0081)
    public enum BlueprintPasteElementBlockCause
    {
        None,
        ExistingBlock,
        TerrainSunk,
    }
```

`IBlueprintPasteWorld` に `bool IsTerrainSunk(BlockPositionInfo positionInfo);` を足す。`BlueprintPasteCopyBuilder.Build` のループを次にする:

```csharp
            var blockCauses = new List<BlueprintPasteElementBlockCause>(elements.Count);
            var intersecting = occupancy.FindIntersecting(accepted);
            foreach (var position in occupancy.Positions)
            {
                blockCauses.Add(ResolveCause(position));
            }
            return Create(blueprint, origin, elements, blockCauses);

            #region Internal

            BlueprintPasteElementBlockCause ResolveCause(BlockPositionInfo position)
            {
                if (world.IsOverlapping(position) || OverlapsAccepted(position)) return BlueprintPasteElementBlockCause.ExistingBlock;
                if (world.IsTerrainSunk(position)) return BlueprintPasteElementBlockCause.TerrainSunk;
                return BlueprintPasteElementBlockCause.None;
            }
```

`Create`・`BuildUnobstructed`・座標無効時の空リストも `List<BlueprintPasteElementBlockCause>` にし、線の端点判定は `IsPlaceableCause(blockCauses[indexA])`／`IsPlaceableCause(blockCauses[indexB])`（`BlueprintPasteCopyDraft` の `internal static bool IsPlaceableCause(BlueprintPasteElementBlockCause cause) => cause == BlueprintPasteElementBlockCause.None;` を1本だけ置き、`IsElementPlaceable` もこれを呼ぶ）のどちらかが false なら `overlappingEndpointLineCount++`（ローカル変数を `blockedEndpointLineCount`、`BlueprintPasteCopyDraft.OverlappingEndpointLineCount` と ctor 引数を `BlockedEndpointLineCount` に改名し、`BlueprintPasteOperationHandler.LogSkipped` の `"line endpoint overlaps"` を `"line endpoint blocked"` にして参照を追従）。`BlueprintPasteCopyDraft` の `NonOverlapFlags` を `ElementBlockCauses` に置換し、次を足す:

```csharp
        public bool IsElementPlaceable(int elementIndex) => ElementBlockCauses[elementIndex] == BlueprintPasteElementBlockCause.None;

        // サーバーへ送る埋まり要素の位置
        // Positions of terrain-sunk elements to send to the server
        public IEnumerable<Vector3Int> TerrainSunkPositions()
        {
            for (var i = 0; i < Elements.Count; i++)
            {
                if (ElementBlockCauses[i] == BlueprintPasteElementBlockCause.TerrainSunk) yield return Elements[i].Position;
            }
        }
```

`BlueprintPasteCopyPlan.cs:26`・`BlueprintPasteMaterialAccumulator.cs:30` を `IsElementPlaceable(i)` に、`BlueprintPastePlanner.cs:32` の `occupancy.SelectPlaced(...)` へは `draft.ElementBlockCauses.Select(BlueprintPasteCopyDraft.IsPlaceableCause).ToList()` を渡す（`SelectPlaced` の引数型に合わせる）。`BlueprintPastePlanner.cs:44` は `draft.ElementBlockCauses.All(cause => !BlueprintPasteCopyDraft.IsPlaceableCause(cause))` で `AllBlocked`。`BlueprintPasteCopyState.AllOverlapped` を `AllBlocked` に改名し参照を全部追従。`Elements[i].Position` が `BlockPositionInfo.OriginalPos` と同じ値であることを `BlueprintPasteCopyOccupancy.Create` で確認する（違えば `TerrainSunkPositions` は `occupancy.Positions[i].OriginalPos` 相当を返す形に直す）。

- [ ] **Step 3: リクエストとサーバー実装**

`BlueprintRequest`: `[Key(9)] public List<Vector3IntMessagePack> TerrainSunkBlockPositions { get; set; }` を足し、private ctor・`CreatePasteRequest`・`CreatePasteRequests` に `List<Vector3Int> terrainSunkBlockPositions` を必須引数で足す（他の Create* は `null`）。`CreatePasteRequests` はチャンクごとに同じ埋まり位置列を渡す。

`ServerBlueprintPasteWorld` に ctor 引数 `IReadOnlyCollection<Vector3Int> terrainSunkBlockPositions` を足して `HashSet<Vector3Int>` で保持し:

```csharp
        // サーバーには地形が無いため、クライアントが判定した埋まり位置を使う（ADR 0081）
        // The server has no terrain, so it uses the sunk positions judged by the client (ADR 0081)
        public bool IsTerrainSunk(BlockPositionInfo positionInfo) => _terrainSunkBlockPositions.Contains(positionInfo.OriginalPos);
```

`BlueprintPasteOperationHandler.Handle`: `IsValid()` に `request.TerrainSunkBlockPositions != null && request.TerrainSunkBlockPositions.All(p => p != null)` を足し、world 生成に `request.TerrainSunkBlockPositions.ConvertAll(p => p.Vector3Int)` を渡す。`LogSkipped` の `"block overlaps"` 行を原因別2行（`"block overlaps"` は `ExistingBlock` 数、`"terrain sunk"` は `TerrainSunk` 数）にし、`"AllOverlapped"` を `"AllBlocked"` にする。

- [ ] **Step 4: クライアント実装**

`ClientBlueprintPasteWorld`:

```csharp
        // 地形めり込みは通常設置と同じクエリで判定する（ADR 0081）
        // Terrain sink is judged by the same query as normal placement (ADR 0081)
        public bool IsTerrainSunk(BlockPositionInfo positionInfo) => PlacementTerrainSinkQuery.IsSunk(positionInfo.OriginalPos, positionInfo.BlockDirection, positionInfo.BlockSize);
```

`BlueprintPastePlaceSender.Send`: `origins` の直後に `var terrainSunkPositions = plan.EnumerateCopiesToPlace().SelectMany(copy => copy.Draft.TerrainSunkPositions()).ToList();` を作り、`PendingPaste` に保持して `_runner.Run(operation.BlueprintGuid, operation.RotationStep, operation.Origins, operation.TerrainSunkPositions)` へ渡す。`BlueprintPasteRequestRunner.Run` に引数を足し `CreatePasteRequests` へ渡す。

`BlueprintPasteFeedbackReporter.Report`: `AllOverlapped` 行を次に置き換える:

```csharp
            if (0 < plan.Copies.Count && plan.CountCopies(BlueprintPasteCopyState.AllBlocked) == plan.Copies.Count && HasCause(BlueprintPasteElementBlockCause.ExistingBlock)) feedback.AddBlockedByExistingBlock();
            if (HasCause(BlueprintPasteElementBlockCause.TerrainSunk)) feedback.AddBlockedByTerrain();
```

`HasCause` はメソッド内ローカル関数（`#region Internal`）で `plan.Copies.Any(copy => copy.Draft.ElementBlockCauses.Contains(cause))`。`BlueprintPastePreviewController.cs:43` は `copy.Draft.IsElementPlaceable(i)`、`BlueprintPasteVisualState.cs:31` は `ElementBlockCauses[index]` の比較に置換。

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Blueprint"`
Expected: ErrorCount 0 / 全 PASS（追加2本を含む）

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts
git commit -m "feat(blueprint): 地形に埋まるブロックだけ飛ばし埋まり位置をサーバーへ送る"
```

### Task 3: 設置プレビューから当たり判定を撤去する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/PreviewBlockCreator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/BlockPreviewObject.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/PlacementGroundContactQuery.cs`（+ .meta）
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/GroundCollisionDetector.cs`（+ .meta）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObject.cs:85-89`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/BlockGameObjectColliderSetup.cs:55-57`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/BlockClickDetectUtil.cs:76-90`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/DeleteTargetRaycaster.cs:24-32`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/GearChainPoleConnectSystem.cs:59-61`
- Modify（Prefab、uloop execute-dynamic-code 経由のみ）: GroundCollisionDetector を持つ5Prefab（Charcoal_Kiln / Primitive_Blast_Furnace / Sieve / campfier / press machine）
- Delete（テスト）: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/Preview/` の `PlacementGroundContactQueryCapacityTest.cs`・`PlacementPreviewPhysicsFixture.cs`・`PlacementPreviewProductionWiringTest.cs`・`PlacementTerrainContactBaseCellSinkTest.cs`・`PlacementTerrainContactShapeTest.cs`（+ .meta）
- Modify（テスト）: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/Preview/PreviewBlockColliderIsolationTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/Block/BlockGameObjectColliderSetupTest.cs:95-110`

**Interfaces:**
- Consumes: Task 1 で `SetTerrainContactEnabled`・`DetectGroundOverlaps` の呼び出しが全て消えていること
- Produces: なし（撤去のみ）

- [ ] **Step 1: Prefab 依存を確かめる** — `uloop execute-dynamic-code` で、5Prefab それぞれの `GroundCollisionDetector` を持つ GameObject のパスと、その GameObject 上の他コンポーネント一覧・子の有無をログに出す。さらに `BlockPreviewObject`・`IPreviewOnlyObject` 実装・`IBlockPreviewStateProcessor` 実装の中で `Collider`・`Physics`・`OnTrigger`・`OnCollision` を参照するものが無いか `grep -rn "OnTrigger\|OnCollision\|GetComponent<Collider>\|Physics\." moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor` で確かめる。プレビュー時に Collider 有効を前提とする処理が見つかったら実装を止め、その箇所をユーザー裁定に上げる（本planの前提が崩れるため）

- [ ] **Step 2: プレビュー生成で Collider を無効化する** — `PreviewBlockCreator.cs`

```csharp
        public static BlockPreviewObject Create(BlockId blockId)
        {
            // 実Prefabからプレビュー表示を初期化
            // Initialize preview visuals from the real prefab.
            var block = ClientContext.BlockGameObjectPrefabContainer.CreateBlockGameObject(blockId, Vector3.zero, Quaternion.identity);
            block.SetActive(true);

            var preview = block.AddComponent<BlockPreviewObject>();
            preview.Initialize(blockId);

            // プレビューは見た目専用。初期化で有効化した子も含め当たり判定を全て切る（ADR 0081）
            // Previews are visual-only; disable every collider, including children activated by initialization (ADR 0081)
            foreach (var collider in block.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            return preview;
        }
```

- [ ] **Step 3: `BlockPreviewObject` から地形接触を撤去する** — フィールド `_collisionDetectors`・`_contactColliders`・`_terrainContactConfigured`・`_ownedTerrainContact`、メソッド `ConfigurePlacementColliders`・`ConfigureTerrainContact`・`SetTerrainContactEnabled`・`DetectCurrentGroundOverlap`・`CacheContactColliders`、`Initialize` 末尾の `CacheContactColliders();` を削除し、未使用になった using を消す

- [ ] **Step 4: 撤去と付随の削除**
  - `PlacementGroundContactQuery.cs`・`GroundCollisionDetector.cs` を .meta ごと `git rm`
  - Step 1 で確認した5Prefabから `GroundCollisionDetector` を `uloop execute-dynamic-code`（`PrefabUtility.LoadPrefabContents` → 削除 → `SaveAsPrefabAsset` → `UnloadPrefabContents`）で外す。その GameObject が検出専用（Collider と GroundCollisionDetector／Rigidbody だけで子が無い。campfier・Primitive_Blast_Furnace の `PreviewCollison` が該当見込み）なら GameObject ごと削除する。Sieve・press machine・Charcoal_Kiln は描画本体の `Cube`（MeshRenderer＋非トリガー BoxCollider）に付いている見込みなので、Cube は残し部品だけ外す。帰結: その BoxCollider が `BlockGameObjectColliderSetup.IsClickableCollider` でクリック可能扱いになる（Sieve はクリック可能 Collider 0本→1本で、欠落エラーが消える。press machine・Charcoal_Kiln はベイク済み `ClickCollider` と二重になり、次に `BlockClickColliderBaker` を回すと `ClickCollider` が消える）。この帰結を受け入れて進め、`BlockClickColliderTest` の対象ブロックに5Prefabが含まれないことを確認する。Missing Script が残っていないことを同じ dynamic-code で確かめる
  - `BlockGameObject.cs:85-89` の「地面との衝突判定を無効化」ループを削除
  - `BlockGameObjectColliderSetup.cs:55-57` の GroundCollisionDetector 除外分岐とコメントを削除
  - `BlockClickDetectUtil.TryGetFrontmostSolidHit` のプレビュー命中スキップ行とコメントを削除し、コメントを「最小距離のヒットを1回の走査で選ぶ」/「Pick the nearest hit in one scan without sorting」に直す
  - `DeleteTargetRaycaster.cs` のプレビュー命中スキップ行を削除し、コメントを「全ヒットを候補へ積む」/「Collect every hit as a candidate」に直す
  - `GearChainPoleConnectSystem.ManualUpdate` の「前フレームのゴーストを隠してから実ポールの選択レイを集める」2行コメントと `_previewObject.Hide();` を削除（`ResetState` の Hide は残す）

- [ ] **Step 5: テスト整理**
  - 上記5テストファイルを .meta ごと `git rm`
  - `PreviewBlockColliderIsolationTest.cs` を「`PreviewBlockCreator.Create` 相当で生成したプレビュー配下の全 `Collider.enabled` が false（非アクティブな子を含む）」「プレビュー配下に `Rigidbody` が無い」の2本に書き換える（実 `BlockGameObjectPrefabContainer` が使えない場合は、Collider を複数持つ GameObject に `Create` と同じ無効化ループを当てる形ではなく、`PreviewBlockCreator` の無効化を `internal static void DisableColliders(GameObject)` として切り出してそれをテストする）
  - `BlockGameObjectColliderSetupTest.cs` の `IsClickableCollider_GroundCollisionDetectorCollider_IsNotClickable` を削除

- [ ] **Step 6: 残骸がないことを確かめ、コンパイルとテスト**

Run: `grep -rn "GroundCollisionDetector\|DetectGroundOverlaps\|PlacementGroundContactQuery\|ConfigureTerrainContact\|ConfigurePlacementColliders\|SetTerrainContactEnabled" moorestech_client/Assets/Scripts` → 0件
Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceSystem|BlockGameObjectColliderSetupTest|Blueprint|DeleteTarget|BlockClick"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 7: コミットする**

```bash
git add -A moorestech_client/Assets
git commit -m "refactor(place): 設置プレビューの当たり判定と地形接触機構を撤去する"
```

### Task 4: unityプレイ録画テストで実地形の挙動を確かめる

**Files:**
- Create: 録画シナリオ（`unity-playmode-recorded-playtest` スキルの規定の置き場・形式に従う）

- [ ] **Step 1: シナリオを作り実行する** — `unity-playmode-recorded-playtest` スキル（プレイテストDSL）で、生成ワールドの端数地形・斜面上で次を録画付きで通す:
  1. 通常設置: オフセット0で置ける → Q/E で1段下げると赤＋「地形に埋まっています」、クリックしても置かれない
  2. 歯車チェーンポール・電柱・レール橋脚: 端数地形・斜面で既定位置に置ける（#1507 の退行が無い）
  3. ベルト: 1段下げて地中に入れたセルが赤くなり置かれない
  4. BP貼り付け: 鉄ベルトコンベア・分岐機を含むBPを地面上で動かしても点滅しない（#1499 の元症状）。1段以上下げて一部が丘に食い込む位置で貼ると、埋まるブロックだけ赤く、貼付後に残りだけ置かれている
- [ ] **Step 2: 結果（result.json・録画パス・観測値）を plan の判断記録の下に追記してコミットする**

```bash
git add <シナリオファイル> docs/superpowers/plans/2026-10-15-preview-no-collider-terrain-sink.md
git commit -m "test(place): 地形めり込み統一とプレビュー当たり判定撤去を録画で確かめる"
```

### Task 5: 全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー反映が判定経路・条件式・評価時点に触れたら、Task 4 の unityプレイ録画テストを反映後のバイナリで再実施してから完了とする

### Task 6: PR作成とセッション終了可能状態

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0081-preview-has-no-collider-and-terrain-sink-gate-for-all-placement.md`（ユーザー裁定の出所はADR本文）。裁定記録: `.decisions/2026-10-15-地形めり込み判定は全設置系で底から1セルより深い沈みを不可にする.md`、`.decisions/2026-10-15-地形めり込み判定はGroundHeightProbeの高さサンプルで測る.md`、`.decisions/2026-10-15-BP貼り付けの地形埋まり除外はクライアントが判定してリクエストに載せる.md`、`.decisions/2026-10-15-ベルトの地形迂回ルートは別タスクに切り電線の地形埋まりは許容する.md`
- 判定式を `ResolveCellY(groundMax, step, 0) > baseY` にした: 設置Y決定と同じ許容誤差（格子パッド・浮動小数）を共有し、既定Yでは構造的に不可にならない（R3）。出所: agent前提（ADR 0081 決定1「底から1セルより深い沈み」の実装形）
- 地表が見つからないときは「埋まりではない」とする: 地表欠落の理由行は既存の設置Y解決（GroundNotFound）が出す。出所: agent前提
- BPの除外はブロック番号ではなく貼付先のワールド位置で送る: 1リクエストが複数原点を持ち番号だと（コピー番号, ブロック番号）の対が要るため。位置はクライアント・サーバーが同じ `BlueprintPasteCalculator` で出すので一致する。ADR 0081 の「番号」はこの位置列を指す。出所: agent前提（planning）
- BP要素の不可原因を bool から3値 enum（None / ExistingBlock / TerrainSunk）へ広げ `NonOverlapFlags`・`AllOverlapped` を改名した: 埋まりを重なりフラグへ畳むと名前と意味がずれ、理由行（既存ブロック／地形）を区別できないため。出所: agent前提（planning）
- タスク順を「判定差し替え → BP → 当たり判定撤去」にした: 撤去を先にすると旧判定が消えた時点で4設置系のゲートが一時的に無くなり、Task単位のテストが意味を失うため。出所: agent前提（planning）
- unityプレイ録画テストを Task 4 に含めた: 判定が実地形（TerrainCollider・生成ワールドの端数）とレイに依存し、#1499/#1507 の退行はどちらもユニットテストを通って実地形で出たため。出所: agent前提（planning）
- サーバー側BP共有plannerとプロトコルの改修（BlueprintPasteElementBlockCause enum を BlueprintPasteCopyDraft.cs に同居、IBlueprintPasteWorld.cs・BlueprintPasteCopyBuilder.cs・BlueprintPasteCopyDraft.cs・BlueprintPasteCopyPlan.cs・BlueprintPasteMaterialAccumulator.cs・BlueprintPastePlanner.cs・ServerBlueprintPasteWorld.cs・BlueprintPasteOperationHandler.cs・BlueprintRequest.cs の変更）: BPの埋まり除外をクライアント判定・リクエスト送信・サーバーは送信値で飛ばす形にするための最小改修。サーバー可変状態の同期ではなく単発リクエストの入力追加なので3点セット（イベント＋初期データ＋購読）は不要。地形を持たないサーバーが送信値を信じる形は既存 BlueprintPasteOrigin.IsGroundFound と同型。出所: ユーザー裁定 2026-10-15 選択「クライアントが除外を送る (推奨)」＋形状は agent前提（planning）
- Phase 2.6 裁定（ユーザー裁定 2026-10-15）: ADR 0081 を plan に合わせ「BP除外は位置で送る」「境界は ResolveCellY の許容誤差で比べる」を追記。「プレビュー表示→埋まり判定→理由反映」の3系統の繰り返しと電柱・歯車ポールの1セル判定の繰り返しは据え置き（選択「このままでよい (推奨)」）。BPの要素可否の述語は `BlueprintPasteCopyDraft.IsPlaceableCause` 1本へ集約（検査6の指摘を直接修正）。届け先は master へのPRだけ（選択「masterへのPRだけ (推奨)」）。テストケース 10.999f は float 誤差で境界をまたぐおそれがあるため 10.99f に修正。裁定記録: `.decisions/2026-10-15-BP埋まり除外は位置で送り境界はResolveCellYの許容誤差で比べる.md`
- BlueprintRequest の型分割（ユーザー裁定 2026-10-16）: 操作ごとに型を分けず、貼り付け時だけ値が入る `TerrainSunkBlockPositions`（Key(9)）を1項目足す（提示「推奨: このまま1項目足す」に原文「それで」）。負債: 操作によって null になる欄が1つ増える。裁定記録: `.decisions/2026-10-16-BPリクエストは型を分けず貼り付け専用の除外位置欄を1つ足す.md`
- user-simulator review（2026-10-15）の適用: enum を新規ファイルにせず `BlueprintPasteCopyDraft.cs` へ同居（1ディレクトリ10ファイル規約。前例 `BlueprintPasteCopyPlan.cs:6`）、R8 に非対称の理由を明記、Task 3 Step 4 に描画本体 Cube に付いた検出器の扱いと Collider がクリック可能になる帰結を明記。いずれも規約・前例の適用と手順の明文化で、挙動・範囲の判断ではない。出所: agent前提（user-simulator 指摘を規約照合のうえ採用）
- BP埋まり除外の照合キーを位置から（位置・向き・寸法）へ広げた: 重なる間隔のドラッグ貼り付けで同じ最小角を持つ別形状のブロックを誤って飛ばさないため（タスクレビュー Important）。出所: agent前提（実装中のレビュー所見）

### Task 4 録画結果

生成ワールド（PLAYTEST_MAP_MODE=generated・PLAYTEST_SEED=196・毎回新規world）、レビュー修正 94feda004c 適用後のバイナリで Record=true 実行。3本とも Success=true・Asserts 全PASS・動画は0 byteでない（目視: アバター・地面・HUD・ゴースト・ツールチップを確認）。シナリオは `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/` に置いた。

- 項目1・3（`terrain-sink-normal-and-belt-via-ui.cs`、20/20 PASS）: `moorestech_client/PlaytestResults/20261016_031656/terrain-sink-normal-and-belt-via-ui/result.json`・`recording.mp4`
  - 斜面 start=(-50,-10)、地表 25.44→27.53（16セル）。木のコンベアチェスト(3x2x4) オフセット0: 地表25.435で既定Y=26、プレビュー青、(-50,26,-10) に置けた
  - Qでオフセット-1: Y=25 で赤、`ui.tooltip.placeBlockedByTerrain`、Web UIのツールチップにも「Blocked by terrain」と出た（英語ロケール）。クリックしても置かれない
  - ベルト オフセット0のドラッグ (-42..-40): Y=26,26,27、既定Y=26,27,27 なので中央セル (-41,26) が埋まって赤、両端は青で置かれ、中央は置かれない（ベルト列のYは端点間の直線補間で地形に沿わない。地形追従は bd moorestech-5wy86 の範囲）
  - ベルト Qで-1 のドラッグ (-38..-36): 3セルともY=26（既定Y=27）で赤、置かれない
- 項目2（`terrain-default-y-poles-and-pier-via-ui.cs`、16/16 PASS）: `moorestech_client/PlaytestResults/20261016_031829/terrain-default-y-poles-and-pier-via-ui/result.json`・`recording.mp4`
  - 歯車チェーンポール: フットプリント地表最高25.040 → (-55,25,-16)、電線ツールの電柱: 26.550 → (-46,26,-16)、レール橋脚: 27.729 → (-38,27,-16)。3つとも既定Yに置かれ、地形理由は出ず、置いた位置は埋まり判定に掛からない（#1507 の退行なし）
- 項目4（`blueprint-terrain-sink-partial-paste.cs`、19/19 PASS）: `moorestech_client/PlaytestResults/20261016_031418/blueprint-terrain-sink-partial-paste/result.json`・`recording.mp4`
  - BP＝鉄の歯車ベルトコンベア7＋鉄の歯車ベルトコンベア分岐機1。高さ0で斜面上を往復: 8個そろわないフレーム 0/80・0/72、赤を含むフレーム 0（#1499 の点滅なし）
  - Qで-1: 赤5（分岐機を含む (-47..-43,25,-9)）、青3（(-50..-48,25,-9)）。赤はすべて埋まり判定が真、青はすべて偽。地形理由の行が出る。貼り付け後は青の3個（ベルト）だけ置かれ、赤5個は置かれない
