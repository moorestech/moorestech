# Belt Route Unbounded Search Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ベルト2クリック設置の探索から X64×Z64 の箱と高さ上限を無くし、触ったセルだけをその場で調べる重み付き A* で、縦横とも上限なしに経路を引けるようにする。経路なしの理由は1つに畳み、C キーは廃止する。

**Architecture:** 探索が読む世界を `IBeltRouteWorld`（地形の下限・占有・坂の真上・入出力辺の面ビット）に抽象化し、本番は `BeltRouteLiveWorld`（占有・辺はブロック変化時に全ブロックから作り直し、地形は列ごとに初回だけ測る）、テストはテストアセンブリへ移した `BeltRouteGrid` が実装する。面ビットの立て方は `BeltRouteFaceBits` の1本に集め、両世界が共有する。`BeltRouteSearch` は配列と f 値バケットから辞書と二分ヒープへ変わり、評価値を `g + 2h` にして展開数の上限で打ち切る。経路の組み立て（`BeltRouteRecovery`）と接続規則（`BeltRouteTransitionRule`）の中身は保ち、箱の添字を `Vector3Int` と世界の問い合わせに置き換える。

**Tech Stack:** Unity 6 / C# / NUnit（EditMode）/ uloop / プレイテスト DSL と録画シナリオ

## Requirements

- R1. 終点がどれだけ遠くても（建築射程 100 以内）、横・縦の範囲チェックで弾かれない。受け入れ: `BeltRoutePlannerBasicTest.LongOpenRunBeyondTheOldBoxIsStraightAndShortest` が 120×3×120 の平地で (2,0,2)→(110,0,100) の経路を返し、セル数 207・コスト 1035・全セル直線。
- R2. 開けた場所では最短と同じ形になる。受け入れ: R1 と `StraightRunEastIsAllStraightCells`（7 セル・コスト 35）が通る。
- R3. 障害物の周りでは遠回りを許すが、最短の 2 倍（`BeltRouteSearch.HeuristicWeight`）＋余裕 20 を超えない。受け入れ: `BeltRouteReferenceCrossCheckTest` で、参照ソルバが見つけた経路に対して本番のコストが `参照 × 2 + 余裕` 以下。
- R4. 参照ソルバが見つける経路は本番（余裕 20）も見つける（存在）。受け入れ: 同テストで見落とし 0 件。
- R5. 経路なしは探索量の上限（`BeltRoutePlanner.MaxExpansions = 20000`）で決まり、理由行は「経路が見つかりません」（`ui.tooltip.placeBeltRouteNotFound`）の1つ。開発者向けには出どころ（出発状態なし／上限到達／待ち行列の枯渇／組み立て失敗）と展開数を `Debug.Log` で1行出す。受け入れ: `WallOfBlocksYieldsNoRoute` / `TerrainRidgeYieldsNoRoute` / `EndOutsideTheTestWorldYieldsNoRoute` / `EnclosedEndYieldsNoRoute` が `HasPlan == false`。`BeltRouteNoPathReason` 型と辞書キー `placeBeltRouteBlockedByTerrain / ...ByExistingBlock / ...ByBoth / ...OutOfRange` が消えている。
- R6. 始点クリック時に箱の全列の地形を測らない。地形は列ごとに初めて触れたときに測って覚える。受け入れ: `BeltRouteTerrainProfile` と `BeltRouteWorldScanner` が削除され、`BeltRouteLiveWorld.FloorOf` が辞書で記憶する。
- R7. 既存ブロックの占有・坂の真上・入出力辺は、ブロックが置かれた・撤去されたときに作り直す（箱との位置関係で絞らない）。受け入れ: `BeltConveyorPlaceSystem.MarkObstaclesDirty` がセッション中なら必ず dirty にし、`BeltRouteSession.RebuildObstacles` が `BeltRouteLiveWorld.Rebuild` を呼ぶ。
- R8. C キーの同コスト候補送りを無くす。受け入れ: `BeltRouteStepPriority`・`BeltRouteQuery.Priority`・`BeltRouteKeyInput.IsCyclePriorityPressed`・`BeltConveyorPlaceSystem._priority` が消え、録画シナリオ `belt-route-two-click-via-ui.cs` に `Key.C` が無い。
- R9. 既存の規則（直線は前・左・右、坂は曲がらない、山谷なし、坂の真上を空ける、既存の出力口を横取りしない、終点の前を空ける、分岐・分岐器手持ち）は変えない。受け入れ: `BeltRouteRandomizedSearchTest`・`BeltRoutePlanVerifier`・`BeltRouteConveyanceTest`・`BeltRouteWiringTest`・`BeltRouteBranchGeometryTest` が通る。
- R10. 面ビットの立て方（辺の高さ h ↔ セル c のビット(face, k) は h == c.y + k、辺に接する2セルの両方）が本番とテストで1本。受け入れ: `BeltRouteFaceBitsTest` が 4 面 × 2 高さで `BeltRouteFaceBits.Mark` の結果を確かめ、`BeltRouteLiveWorld` とテストの `BeltRouteGrid` がどちらも `BeltRouteFaceBits.Mark` だけで面ビットを立てる。
- R11. テスト専用の型を本番に残さない。受け入れ: `BeltRouteGrid` が `Client.Tests` 配下にあり、本番の `Client.Game` に `BeltRouteGrid`・`BeltRouteEdgeTable` が無い。
- R12. 実機で旧箱（±32）を超える距離の経路が置ける。受け入れ: 録画シナリオ `belt-route-two-click-via-ui.cs` のフェーズ3が (-20,32,-22)→(-14,32,22)（z 距離 44）の 51 セルを置き、フェーズ3b で囲まれた終点を狙ったときの最悪フレーム時間を Note に出す。
- やらないこと: 探索の並列化・フレーム分割、サーバーの接続規則やマスタの変更、分岐の最初のセルが曲がると歯車が繋がらない不具合（別 PR）、`HybridInput` のキー対応表から C を外すこと、`NoGround`/`Unreached` の番兵値の型化（既存の形を引き継ぐ）。

## Global Constraints

- AGENTS.md: 1ファイル200行以下・1ディレクトリ10ファイル以下・partial 禁止・`Func<>` 禁止・デフォルト引数禁止・try-catch は境界のみ・日本語→英語の2行コメント・.meta は Unity 生成（手で作らない。`git rm`/`git mv` は可）・.cs 変更後は必ず `uloop compile --project-path ./moorestech_client`。
- 無音の縮退禁止: 経路なし・上限到達は理由行「経路が見つかりません」に加え、`BeltRoutePlanner` が出どころと展開数を `Debug.Log` へ1行出す（同じ問い合わせは使い回すので毎フレームは出ない）。探索・規則の内側の early return は `// silent-ok:` コメントで「呼び手（Planner）がログを出す」と書く。
- Editor 由来の差分（`.moorestech-external-revisions.json`、`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`、`*.mat`）はコミットしない。コミット前に `git checkout -- .moorestech-external-revisions.json moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`。`git add` はパス明示。
- localization.csv を変えたら `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` の `dummyText` を新しい値にし、`moorestech_web/webui` で `npm run gen:i18n` → `npx vitest run src/shared/i18n`、`uloop compile --force-recompile`（COMPILE_RESULT_UNKNOWN）→ 通常 compile。
- 作業場所: 本体ワーキングツリーの `feature/belt-two-click-route`（ユーザー指示 2026-11-09「PRまだつくらないで　このブランチだけでまずは仕事したいので」。PR は作らない・push はバックアップとしてのみ）。
- 設計の正: `docs/adr/0100-belt-route-unbounded-weighted-search.md`。
- 壁時計の閾値テストは置かない（ユーザー裁定 2026-11-07）。計測はログと ADR への記入だけ。
- 各タスクのコミット時点でコンパイルが通ること（Task 2 の暫定 API は Task 3 で消す）。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反2件・修正2件 / (1) テスト専用の `BeltRouteGrid` が本番に残る → テストアセンブリへ移し、本番の分岐判定は辺の有無だけの `BeltRouteNeighbourEdges` に。(2) 本番から読まれない `BeltRouteSearch.Expansions` → 経路なし時の開発者向けログで読む用途にし、`Run` の結果を enum にして出どころを区別。注目点の機構比較（辞書＋ヒープ／チャンク配列／バケット据え置き）と組み立ての前提変化を ADR 0100 に追記。`BeltRoutePlanner.World` は削除。`Route/` のファイル数が一時的に 10 を超える配置は、新 interface を `World/` に置いて回避。死活表で死ぬ操作は C キーだけ（裁定済み）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強2・弱4・第3バケツ2 / 強: 面ビットの並びの一致を確かめるテストが無い → `BeltRouteFaceBits.Mark` に1本化し `BeltRouteFaceBitsTest` を追加（検査6 第3バケツの LiveWorld/EdgeTable 重複も同時に解消。1問い合わせの参照回数も 8→1）。強: `NoGround`/`Unreached` の番兵 → 既存の形を引き継いで据え置き（やらないことに明記）。弱: `Run` の失敗理由2つと `Plan` の NotFound 3つの出どころ → `BeltRouteSearchOutcome` enum と Planner の開発者ログで区別。弱: `IsCellFree` と `CellCheck.Judge` の重複 → `World/BeltRouteCellJudge.Judge` の1本に。弱: ADR の「Task 5 で記入」「90 セル」の食い違い → ADR を Task 4・207 セルに修正。弱: `Rebuild` の費用の記述 → ADR を「増える」に修正。弱: 組み立ての前提変化 → ADR に追記。第3バケツ: 地形に埋まる判定の二重（`FloorOf` と `PlacementTerrainSinkQuery`）は既存の引き継ぎで本PR外。備考の指摘（Task 2 で `BeltRouteConveyanceTest` の `NoPathReason` がコンパイルを壊す／録画フェーズ3の視線が壁に当たる／`OutOfRange` の grep が無関係に当たる／最悪ケースの計測が無い／上限 40,000 で 200 ms 超の見込み）はそれぞれ Task 2 の Files 追加・x=−20 列へ移動・grep の範囲限定・実機の囲まれた終点の計測フェーズ追加・上限 20,000 と計測後の調整規則で反映

---

## File Structure

`D = moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor`、`T = moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute`

- Create: `D/World/IBeltRouteWorld.cs` — 探索が読む世界（地形・占有・坂の真上・面ビット）
- Create: `D/World/IBeltRouteEdgeSink.cs` — 既存ブロックの辺と坂の真上の書き込み先
- Create: `D/World/BeltRouteFaceBits.cs` — 面ビットの並びと、辺から接する2セルへ面ビットを立てる1本
- Create: `D/World/BeltRouteCellJudge.cs` — 端点・途中セルが置けない理由（既存ブロック・地形・坂の真上）を世界から判定
- Create: `D/World/BeltRouteLiveWorld.cs` — 本番の世界。占有・辺は `Rebuild` で全ブロックから、地形は列ごとに初回だけ
- Modify: `D/World/BeltRouteBlockEdgeScanner.cs` — 書き込み先を `IBeltRouteEdgeSink` に
- Delete: `D/World/BeltRouteTerrainProfile.cs`、`D/World/BeltRouteWorldScanner.cs`（+ .meta）
- Create: `D/Session/Branch/BeltRouteNeighbourEdges.cs` — 分岐判定が隣の辺を写す入れ物（辺の有無だけ）
- Modify: `D/Session/Branch/BeltRouteBranchGeometry.cs`、`D/Session/Branch/BeltRouteBranchOrigin.cs` — `BeltRouteGrid` → `BeltRouteNeighbourEdges`
- Create: `D/Route/Search/BeltRouteOpenHeap.cs` — f 昇順・同 f は g 降順の二分ヒープ
- Modify: `D/Route/Search/BeltRouteSearch.cs` — 辞書＋ヒープの重み付き A*、展開数の上限、`BeltRouteSearchOutcome`
- Modify: `D/Route/Search/BeltRouteTransitionRule.cs` — `IBeltRouteWorld` と `Vector3Int` で判定。無視フラグ削除
- Modify: `D/Route/Search/BeltRouteRecoveryCandidates.cs` — 箱の内外チェックと優先順を削除
- Modify: `D/Route/Search/BeltRoutePlanner.cs` — `IBeltRouteWorld` を受け、経路なしの出どころをログに
- Delete: `D/Route/Search/BeltRouteCostBuckets.cs`、`D/Route/Search/BeltRouteReachability.cs`、`D/Route/BeltRouteObstacleMask.cs`、`D/Route/BeltRouteStepPriority.cs`、`D/Route/BeltRouteEdgeTable.cs`（+ .meta）
- Move+Rewrite: `D/Route/BeltRouteGrid.cs` → `T/World/BeltRouteGrid.cs`（テストの世界。辞書で面ビット、箱の外は占有扱い）
- Modify: `D/Route/BeltRouteState.cs` — `BeltRouteStateCodec` を削除
- Modify: `D/Route/BeltRoutePlan.cs` — `BeltRouteNoPathReason` を削除、`BeltRoutePlanResult.NotFound()` 引数なし
- Modify: `D/Route/BeltRouteQuery.cs` — `Priority` を削除
- Modify: `D/Session/BeltRouteSession.cs` — `BeltRouteLiveWorld` を持つ。`World` を公開
- Modify: `D/Session/BeltRouteRoutingFrame.cs`、`D/Session/BeltRouteCellCheck.cs`、`D/Session/BeltRouteTooltipKey.cs`、`D/Session/BeltRouteKeyInput.cs`、`D/BeltConveyorPlaceSystem.cs`
- Modify: `Localization/localization.csv`（4 キー削除・1 キー追加）と生成物（`Client.Localization/_CompileRequester.cs`、`moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`）
- Create: `T/World/BeltRouteFaceBitsTest.cs`
- Modify: `T/BeltRoutePlannerBasicTest.cs`、`T/BeltRouteRandomizedSearchTest.cs`、`T/BeltRouteReferenceCrossCheckTest.cs`、`T/BeltRouteReferenceSolver.cs`、`T/BeltRoutePlanVerifier.cs`、`T/BeltRouteConveyanceTest.cs`、`T/BeltRouteConveyanceWorld.cs`、`T/BeltRouteBranchGeometryTest.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-route-two-click-via-ui.cs`、`docs/adr/0100-belt-route-unbounded-weighted-search.md`（Consequences の実測）、`docs/playtest-dsl.md`（C の記述があれば）

ディレクトリのファイル数（.meta を除く）: `Route/` 10 → 6、`Route/Search/` 8 → 7、`World/` 3 → 6、`Session/` 10 → 10、`Session/Branch/` 3 → 4、`T/` 8 → 8、`T/World/` 0 → 2。途中のタスクでも 10 を超えない（Task 1 で `World/` 7、`Route/` 10 のまま）。

## Interfaces（タスク間で共有する名前）

```csharp
namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    public interface IBeltRouteWorld
    {
        bool IsBlockedByTerrain(Vector3Int cell);
        bool IsBlockedByBlock(Vector3Int cell);
        bool IsSlopeHead(Vector3Int cell);
        int OutputFaceBits(Vector3Int cell);   // BeltRouteFaceBits.FaceBit の並び
        int InputFaceBits(Vector3Int cell);
    }
    public interface IBeltRouteEdgeSink
    {
        void AddOutputEdge(BeltRouteEdge edge);
        void AddInputEdge(BeltRouteEdge edge);
        void MarkSlopeHead(Vector3Int cell);
    }
    public static class BeltRouteFaceBits
    {
        public static int FaceBit(BlockDirection face, int heightOffset);
        public static void Mark(BeltRouteEdge edge, Dictionary<Vector3Int, int> faceBits);  // 辺に接する2セル × 2段
    }
    public enum BeltRouteCellBlockReason { None, ExistingBlock, Terrain, SlopeHead }
    public static class BeltRouteCellJudge { public static BeltRouteCellBlockReason Judge(IBeltRouteWorld world, Vector3Int cell); }
    public sealed class BeltRouteLiveWorld : IBeltRouteWorld, IBeltRouteEdgeSink { BeltRouteLiveWorld(BlockGameObjectDataStore blocks); void Rebuild(); }
    public static class BeltRouteBlockEdgeScanner { static void Write(BlockGameObject block, IBeltRouteEdgeSink sink); static void Write(BlockMasterElement master, BlockId blockId, BlockPositionInfo position, IBeltRouteEdgeSink sink); static bool IsBelt(BlockMasterElement master); }
}
namespace ...BeltConveyor.Session.Branch
{
    public sealed class BeltRouteNeighbourEdges : IBeltRouteEdgeSink { bool HasOutputEdge(BeltRouteEdge edge); bool HasInputEdge(BeltRouteEdge edge); }
    public static class BeltRouteBranchGeometry { static BeltRouteBranchBlockReason Resolve(BeltRouteNeighbourEdges edges, Vector3Int cell, BlockDirection beltFacing, bool useAlternateExit, out BeltRouteBranchShape shape); }
}
namespace ...BeltConveyor.Route
{
    public sealed class BeltRouteQuery { BeltRouteQuery(Vector3Int startCell, BlockDirection startDirection, bool startIsExistingBelt, Vector3Int endCell, BlockDirection endDirection, bool allowSlopes, bool turnByOutput); }
    public sealed class BeltRoutePlanResult { bool HasPlan; BeltRoutePlan Plan; static Found(plan); static NotFound(); }
}
namespace ...BeltConveyor.Route.Search
{
    public enum BeltRouteSearchOutcome { Reached, Exhausted, CapReached }
    public sealed class BeltRouteOpenHeap { int Count; void Clear(); void Push(int f, int g, BeltRouteState state); (int f, int g, BeltRouteState state) Pop(); }
    public sealed class BeltRouteSearch { const int Unreached; const int HeuristicWeight = 2; int Expansions; int BestCost(BeltRouteState); BeltRouteSearchOutcome Run(BeltRouteTransitionRule rule, IReadOnlyList<(BeltRouteState state, int cost)> startStates, int maxExpansions); void ExpandMore(int extraExpansions); }
    public sealed class BeltRouteTransitionRule { BeltRouteTransitionRule(IBeltRouteWorld world, BeltRouteQuery query); BeltRouteQuery Query; bool IsCellFree(Vector3Int); bool IsSlopeHeadFree(Vector3Int slopeCell); bool IsIntermediatePlaceable(Vector3Int cell, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing); bool CanEnterGoal(BeltRouteState previous); bool CanEnterGoalFrom(BlockDirection outgoing); bool IsTurnAllowed(BlockDirection incoming, BlockDirection outgoing); bool IsConnected(BeltRouteState previous, BeltRouteState next); }
    public sealed class BeltRoutePlanner { const int NoRecoverySlack = 0; const int DefaultRecoverySlack = 20; const int MaxExpansions = 20000; BeltRoutePlanner(IBeltRouteWorld world, int recoverySlack); BeltRoutePlanResult Plan(BeltRouteQuery query); }
}
namespace ...BeltConveyor.Session
{
    public sealed class BeltRouteSession { static CreateAtCell(Vector3Int startCell, BlockDirection startDirection, BlockGameObjectDataStore blocks); static CreateBranch(BeltRouteBranchOrigin branch, BlockGameObjectDataStore blocks); IBeltRouteWorld World; void RebuildObstacles(); bool IsStartUsable(BlockGameObjectDataStore blocks, BeltConveyorHoldingBlock holding, bool useAlternateExit); BeltRoutePlanResult Plan(Vector3Int endCell, BlockDirection endDirection, BeltConveyorHoldingBlock holding); }
    public static class BeltRouteCellCheck { static BeltRouteCellBlockReason JudgeInWorld(BlockGameObjectDataStore blocks, Vector3Int cell); }  // 始点確定前の実ワールド判定だけ残る
    public static class BeltRouteTooltipKey { static TooltipLine NoPathLine(); static TooltipLine CellLine(BeltRouteCellBlockReason reason); static TooltipLine BranchLine(BeltRouteBranchBlockReason reason); }
    public static class BeltRouteRoutingFrame { static List<PlaceInfo> Resolve(BeltRouteSession session, BeltRouteCursorHit hit, BeltConveyorHoldingBlock holding, PlacementFeedback feedback, out bool placeable); }
}
namespace Client.Tests.PlaceSystem.BeltRoute.World
{
    public sealed class BeltRouteGrid : IBeltRouteWorld, IBeltRouteEdgeSink { BeltRouteGrid(Vector3Int min, Vector3Int size); Vector3Int Min; Vector3Int Size; void SetColumnFloor(int x, int z, int floorY); void MarkBlockCell(Vector3Int cell); bool Contains(Vector3Int cell); }
}
```

---

### Task 1: 探索が読む世界の抽象（interface・面ビットの1本化・BeltRouteLiveWorld・分岐判定の辺の入れ物）

**Files:**
- Create: `D/World/IBeltRouteWorld.cs`、`D/World/IBeltRouteEdgeSink.cs`、`D/World/BeltRouteFaceBits.cs`、`D/World/BeltRouteLiveWorld.cs`
- Modify: `D/World/BeltRouteBlockEdgeScanner.cs`
- Create: `D/Session/Branch/BeltRouteNeighbourEdges.cs`
- Modify: `D/Session/Branch/BeltRouteBranchGeometry.cs`、`D/Session/Branch/BeltRouteBranchOrigin.cs`
- Modify: `D/Route/BeltRouteGrid.cs`（両 interface を実装。添字 API はこのタスクでは残す）、`D/Route/BeltRouteEdgeTable.cs`（`FaceBit` を `BeltRouteFaceBits.FaceBit` へ委譲）
- Modify: `T/BeltRouteBranchGeometryTest.cs`、`T/BeltRouteConveyanceWorld.cs`（`BuildNeighbourEdges` の戻り値型）

このタスクは追加と差し替えだけで、旧探索（添字版の `BeltRouteGrid`）はそのまま動く。

- [ ] **Step 1: interface 2本と面ビットの1本を作る**

`D/World/IBeltRouteWorld.cs`:

```csharp
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     探索が読む世界。地形の下限・既存ブロックの占有・既存坂の真上・既存の入出力辺を、セル単位でその場で答える
    ///     The world the search reads: terrain floor, existing block occupancy, existing slope heads and existing port edges, answered per cell on demand
    ///     面ビットは BeltRouteFaceBits.FaceBit の並び（面 0..3 + 4×高さ差 0/1）
    ///     Face bits follow BeltRouteFaceBits.FaceBit (face 0..3 + 4 x height offset 0/1)
    /// </summary>
    public interface IBeltRouteWorld
    {
        bool IsBlockedByTerrain(Vector3Int cell);
        bool IsBlockedByBlock(Vector3Int cell);
        bool IsSlopeHead(Vector3Int cell);
        int OutputFaceBits(Vector3Int cell);
        int InputFaceBits(Vector3Int cell);
    }
}
```

`D/World/IBeltRouteEdgeSink.cs`:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     既存ブロックの入出力辺と坂の真上の書き込み先。本番の世界・分岐判定の辺の入れ物・テストの箱が実装する
    ///     Where existing blocks' port edges and slope heads are written; implemented by the live world, the branch judge's edge holder and the test box
    /// </summary>
    public interface IBeltRouteEdgeSink
    {
        void AddOutputEdge(BeltRouteEdge edge);
        void AddInputEdge(BeltRouteEdge edge);
        void MarkSlopeHead(Vector3Int cell);
    }
}
```

`D/World/BeltRouteFaceBits.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     セルの面ビット。bit = 面(0..3) + 4×高さ差（0:セルの高さ、1:セルの1つ上）。下り坂の入口は1つ上の高さで照合する
    ///     A cell's face bits: bit = face (0..3) + 4 x height offset (0: the cell's level, 1: one above); a down slope's entry matches one level up
    ///     辺から面ビットを立てる計算はここ1本。本番の世界とテストの箱が同じ並びになる
    ///     Raising face bits from an edge lives here alone, so the live world and the test box share one layout
    /// </summary>
    public static class BeltRouteFaceBits
    {
        public static int FaceBit(BlockDirection face, int heightOffset)
        {
            return 1 << ((int)face + BeltRouteDirection.DirectionCount * heightOffset);
        }

        // 辺に接する2セルへ、辺の高さにある段と1つ下の段の両方で面ビットを立てる
        // Raise the face bit on both cells touching the edge, at the edge's level and one level below
        public static void Mark(BeltRouteEdge edge, Dictionary<Vector3Int, int> faceBits)
        {
            var positive = edge.PositiveCell;
            var forward = edge.NormalIsX ? new Vector3Int(1, 0, 0) : new Vector3Int(0, 0, 1);
            var positiveFace = edge.NormalIsX ? BlockDirection.East : BlockDirection.North;
            var negativeFace = BeltRouteDirection.Opposite(positiveFace);
            for (var heightOffset = 0; heightOffset <= 1; heightOffset++)
            {
                var level = positive.y - heightOffset;
                Raise(faceBits, new Vector3Int(positive.x - forward.x, level, positive.z - forward.z), positiveFace, heightOffset);
                Raise(faceBits, new Vector3Int(positive.x, level, positive.z), negativeFace, heightOffset);
            }
        }

        private static void Raise(Dictionary<Vector3Int, int> faceBits, Vector3Int cell, BlockDirection face, int heightOffset)
        {
            faceBits.TryGetValue(cell, out var bits);
            faceBits[cell] = bits | FaceBit(face, heightOffset);
        }
    }
}
```

`D/Route/BeltRouteEdgeTable.cs`: `FaceBit` の本体を `return BeltRouteFaceBits.FaceBit(face, heightOffset);` に委譲する（`using ...BeltConveyor.World;` を足す。Task 3 でファイルごと消える）。

- [ ] **Step 2: 分岐判定の辺の入れ物を作り、幾何と Judge を差し替える**

`D/Session/Branch/BeltRouteNeighbourEdges.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch
{
    /// <summary>
    ///     分岐判定が隣のブロックの入出力辺を写す入れ物。辺の有無だけを答える
    ///     Holds the neighbouring blocks' port edges for the branch judge and answers only whether an edge exists
    /// </summary>
    public sealed class BeltRouteNeighbourEdges : IBeltRouteEdgeSink
    {
        private readonly HashSet<BeltRouteEdge> _outputEdges = new();
        private readonly HashSet<BeltRouteEdge> _inputEdges = new();

        public void AddOutputEdge(BeltRouteEdge edge) => _outputEdges.Add(edge);
        public void AddInputEdge(BeltRouteEdge edge) => _inputEdges.Add(edge);

        // 分岐の向きと出口は辺だけで決まる。坂の真上は判定に使わないので受け流す
        // The branch facing and exits follow from edges alone; slope heads play no part, so they are ignored
        public void MarkSlopeHead(Vector3Int cell)
        {
        }

        public bool HasOutputEdge(BeltRouteEdge edge) => _outputEdges.Contains(edge);
        public bool HasInputEdge(BeltRouteEdge edge) => _inputEdges.Contains(edge);
    }
}
```

`D/Session/Branch/BeltRouteBranchGeometry.cs`: `Resolve(BeltRouteGrid edges, ...)` → `Resolve(BeltRouteNeighbourEdges edges, ...)`、`IsFed` の引数型も同じく。コメント「周囲の入出力辺だけを写した箱」→「写した入れ物」。不要になった `using ...Route;` は `BeltRouteEdge`/`BeltRouteDirection` で使うので残る。

`D/Session/Branch/BeltRouteBranchOrigin.cs`: `var edges = new BeltRouteGrid(cell, Vector3Int.one);` → `var edges = new BeltRouteNeighbourEdges();`。コメント「1セルの箱に写し」→「入れ物に写し」。

`T/BeltRouteBranchGeometryTest.cs`: `NewEdges()` を `return new BeltRouteNeighbourEdges();` に、`AddFeeder`/`AddReceiver` の第1引数型を `BeltRouteNeighbourEdges` に。`using ...Route;` は `BeltRouteEdge` で使うので残す。

`T/BeltRouteConveyanceWorld.cs`: `BuildNeighbourEdges` の戻り値型を `BeltRouteNeighbourEdges` にし、`var edges = new BeltRouteNeighbourEdges();`（`using ...Session.Branch;` を足す）。

- [ ] **Step 3: `BeltRouteGrid` に両 interface を実装させ、`BeltRouteBlockEdgeScanner` の書き込み先を interface にする**

`D/Route/BeltRouteGrid.cs`: クラス宣言を `public sealed class BeltRouteGrid : IBeltRouteWorld, IBeltRouteEdgeSink`（`using ...BeltConveyor.World;`）。`Vector3Int` 版の判定を「箱の外は占有扱い・辺なし」にする。

```csharp
        // 箱の外は占有扱い。テストの世界は箱で閉じ、探索が外へ出ないようにする
        // Outside the box counts as occupied, so a test world is closed and the search never leaves it
        public bool IsBlockedByBlock(Vector3Int cell) => !Contains(cell) || _blockCells[CellIndex(cell)];

        public bool IsBlockedByTerrain(Vector3Int cell)
        {
            if (!Contains(cell)) return false;
            var floor = _columnFloorY[ColumnIndex(cell.x, cell.z)];
            return floor != NoGround && cell.y < floor;
        }

        public bool IsSlopeHead(Vector3Int cell) => Contains(cell) && _slopeHeadCells[CellIndex(cell)];
        public int OutputFaceBits(Vector3Int cell) => Contains(cell) ? _edges.OutputFaceBits(CellIndex(cell)) : 0;
        public int InputFaceBits(Vector3Int cell) => Contains(cell) ? _edges.InputFaceBits(CellIndex(cell)) : 0;
```

`HasOutputEdge`/`HasInputEdge` は分岐判定が使わなくなったので削除する（grep で他に参照が無いことを確認）。添字版（`IsBlockedByTerrainAt` 等）と `CreateAround` はこのタスクでは残す。

`D/World/BeltRouteBlockEdgeScanner.cs`: 2つの `Write` の引数 `BeltRouteGrid grid` → `IBeltRouteEdgeSink sink`、内部の `WriteBelt`/`WriteMachine` の引数と `grid.AddInputEdge`/`grid.AddOutputEdge`/`grid.MarkSlopeHead` を `sink.` に。summary の「箱へ書く」→「書き込み先（本番の世界・分岐判定の入れ物・テストの箱）へ書く」。

- [ ] **Step 4: 本番の世界 `BeltRouteLiveWorld` を書く**

`D/World/BeltRouteLiveWorld.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     本番の世界。占有・坂の真上・入出力辺はブロックが変わるたびに全ブロックから作り直し、地形は列ごとに初めて触れたときだけ測って覚える
    ///     The live world: occupancy, slope heads and port edges are rebuilt from every block whenever blocks change; terrain is probed once per column on first touch
    ///     箱を持たないので範囲の上限が無い（ADR 0100）
    ///     There is no box, so there is no range limit (ADR 0100)
    /// </summary>
    public sealed class BeltRouteLiveWorld : IBeltRouteWorld, IBeltRouteEdgeSink
    {
        private const int NoGround = int.MinValue;

        private readonly BlockGameObjectDataStore _blocks;
        private readonly Dictionary<Vector2Int, int> _floors = new();
        private readonly HashSet<Vector3Int> _blockCells = new();
        private readonly HashSet<Vector3Int> _slopeHeads = new();
        private readonly Dictionary<Vector3Int, int> _outputFaceBits = new();
        private readonly Dictionary<Vector3Int, int> _inputFaceBits = new();

        public BeltRouteLiveWorld(BlockGameObjectDataStore blocks)
        {
            _blocks = blocks;
            Rebuild();
        }

        // ブロックが置かれた・撤去されたら呼ぶ。地形は変わらないので覚えたまま
        // Called when a block is placed or removed; terrain does not change, so its memory is kept
        public void Rebuild()
        {
            _blockCells.Clear();
            _slopeHeads.Clear();
            _outputFaceBits.Clear();
            _inputFaceBits.Clear();
            foreach (var block in _blocks.BlockGameObjectDictionary.Values)
            {
                MarkFootprint(block);
                BeltRouteBlockEdgeScanner.Write(block, this);
            }
        }

        public bool IsBlockedByTerrain(Vector3Int cell)
        {
            var floor = FloorOf(cell.x, cell.z);
            return floor != NoGround && cell.y < floor;
        }

        public bool IsBlockedByBlock(Vector3Int cell) => _blockCells.Contains(cell);
        public bool IsSlopeHead(Vector3Int cell) => _slopeHeads.Contains(cell);
        public int OutputFaceBits(Vector3Int cell) => _outputFaceBits.TryGetValue(cell, out var bits) ? bits : 0;
        public int InputFaceBits(Vector3Int cell) => _inputFaceBits.TryGetValue(cell, out var bits) ? bits : 0;
        public void AddOutputEdge(BeltRouteEdge edge) => BeltRouteFaceBits.Mark(edge, _outputFaceBits);
        public void AddInputEdge(BeltRouteEdge edge) => BeltRouteFaceBits.Mark(edge, _inputFaceBits);
        public void MarkSlopeHead(Vector3Int cell) => _slopeHeads.Add(cell);

        // 初めて触れた列だけ地形を測る。沈み許容は通常設置と同じ（PlacementGroundCellResolver）
        // Probes terrain only on a column's first touch; the sink allowance matches normal placement (PlacementGroundCellResolver)
        private int FloorOf(int x, int z)
        {
            var column = new Vector2Int(x, z);
            if (_floors.TryGetValue(column, out var floor)) return floor;
            // silent-ok: 地表が無い列は下限なし（旧 BeltRouteTerrainProfile と同じ）/ プレビューでそのセルが埋まらないことが見える
            floor = GroundHeightProbe.TryGetFootprintMaxGroundHeight(new Vector3Int(x, 0, z), BlockDirection.North, Vector3Int.one, out var groundMax)
                ? PlacementGroundCellResolver.ResolveCellY(groundMax, 0)
                : NoGround;
            _floors[column] = floor;
            return floor;
        }

        private void MarkFootprint(BlockGameObject block)
        {
            var min = block.BlockPosInfo.MinPos;
            var max = block.BlockPosInfo.MaxPos;
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
            for (var z = min.z; z <= max.z; z++)
                _blockCells.Add(new Vector3Int(x, y, z));
        }
    }
}
```

`PlacementGroundCellResolver.ResolveCellY` は `internal`。同じアセンブリ（Client.Game）なので呼べる（旧 `BeltRouteTerrainProfile` と同じ呼び方）。

- [ ] **Step 5: コンパイルし、既存の BeltRoute テストを回す**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0（新規 .cs の .meta が生成されることを `git status` で確認）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaceSystem\.BeltRoute\."` → 全件 PASS（挙動は変わらない）

- [ ] **Step 6: コミットする**

```bash
git checkout -- .moorestech-external-revisions.json moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/World moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/Branch moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/BeltRouteGrid.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/BeltRouteEdgeTable.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteBranchGeometryTest.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteConveyanceWorld.cs
git commit -m "Task 1: feat(belt): 探索が読む世界を抽象化し、面ビットを1本にまとめ、箱を持たない本番の世界 BeltRouteLiveWorld を足す"
```

---

### Task 2: 探索を辞書とヒープの重み付き A* に作り直す（規則・組み立て・計画・テストの追従）

**Files:**
- Create: `D/Route/Search/BeltRouteOpenHeap.cs`
- Modify: `D/Route/Search/BeltRouteSearch.cs`（全面）、`D/Route/Search/BeltRouteTransitionRule.cs`（全面）、`D/Route/Search/BeltRouteRecoveryCandidates.cs`、`D/Route/Search/BeltRoutePlanner.cs`（全面）
- Modify: `D/Route/BeltRoutePlan.cs`、`D/Route/BeltRouteState.cs`、`D/Route/BeltRouteGrid.cs`（添字 API の削除）
- Delete: `D/Route/Search/BeltRouteCostBuckets.cs`、`D/Route/Search/BeltRouteReachability.cs`、`D/Route/BeltRouteObstacleMask.cs`（+ .meta）
- Modify: `D/Session/BeltRouteSession.cs`（世界の差し替えだけ）、`D/Session/BeltRouteRoutingFrame.cs`（`NoPathLine` の引数だけ）、`D/Session/BeltRouteTooltipKey.cs`（`NoPathLine` の引数だけ）
- Modify: `T/BeltRoutePlannerBasicTest.cs`、`T/BeltRouteReferenceCrossCheckTest.cs`、`T/BeltRouteReferenceSolver.cs`、`T/BeltRoutePlanVerifier.cs`、`T/BeltRouteConveyanceTest.cs`（`NoPathReason` を使う3行）

- [ ] **Step 1: 二分ヒープを書く**

`D/Route/Search/BeltRouteOpenHeap.cs`:

```csharp
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     探索の待ち行列。f が小さい状態を先に、同じ f なら g が大きい（終点に近い）状態を先に取り出す二分ヒープ
    ///     The search's open set: a binary heap that pops the smallest f first and, for equal f, the larger g (closer to the goal) first
    /// </summary>
    public sealed class BeltRouteOpenHeap
    {
        private readonly List<(int f, int g, BeltRouteState state)> _items = new();

        public int Count => _items.Count;

        public void Clear() => _items.Clear();

        public void Push(int f, int g, BeltRouteState state)
        {
            _items.Add((f, g, state));
            SiftUp(_items.Count - 1);
        }

        public (int f, int g, BeltRouteState state) Pop()
        {
            var top = _items[0];
            var last = _items[^1];
            _items.RemoveAt(_items.Count - 1);
            if (0 < _items.Count)
            {
                _items[0] = last;
                SiftDown(0);
            }
            return top;
        }

        private static bool IsBefore((int f, int g, BeltRouteState state) a, (int f, int g, BeltRouteState state) b)
        {
            return a.f < b.f || (a.f == b.f && b.g < a.g);
        }

        private void SiftUp(int index)
        {
            while (0 < index)
            {
                var parent = (index - 1) / 2;
                if (!IsBefore(_items[index], _items[parent])) return;
                (_items[index], _items[parent]) = (_items[parent], _items[index]);
                index = parent;
            }
        }

        private void SiftDown(int index)
        {
            while (true)
            {
                var left = index * 2 + 1;
                if (_items.Count <= left) return;
                var right = left + 1;
                var child = right < _items.Count && IsBefore(_items[right], _items[left]) ? right : left;
                if (!IsBefore(_items[child], _items[index])) return;
                (_items[index], _items[child]) = (_items[child], _items[index]);
                index = child;
            }
        }
    }
}
```

- [ ] **Step 2: 接続規則を世界の問い合わせで書き直す**

`D/Route/Search/BeltRouteTransitionRule.cs` 全文:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     サーバーの接続規則を探索の可否判定へ写した1枚。探索と組み立ての両方がここだけを通る
    ///     The server's connection rules translated into search feasibility; both the search and the recovery go through this alone
    ///     世界はセル単位でその場で答える（IBeltRouteWorld）。箱も添字も持たない
    ///     The world answers per cell on demand (IBeltRouteWorld); there is no box and no index
    /// </summary>
    public sealed class BeltRouteTransitionRule
    {
        private readonly IBeltRouteWorld _world;
        private readonly BeltRouteQuery _query;
        private readonly Vector3Int _endFrontCell;

        // 終点自身の出口の辺も既存の出力口と同じ扱い。辺に接するセルで途中になりうるのは、終点と終点の前それぞれの1つ下だけ
        // The end's own exit edge counts like an existing output; of the cells touching it, only the ones below the end and below its front can be mid-route
        private readonly Vector3Int _cellBelowEnd;
        private readonly Vector3Int _cellBelowEndFront;
        private readonly int _faceBitBelowEnd;
        private readonly int _faceBitBelowEndFront;

        public BeltRouteTransitionRule(IBeltRouteWorld world, BeltRouteQuery query)
        {
            _world = world;
            _query = query;
            _endFrontCell = query.EndCell + BeltRouteDirection.Forward(query.EndDirection);
            _cellBelowEnd = query.EndCell + Vector3Int.down;
            _cellBelowEndFront = _endFrontCell + Vector3Int.down;
            _faceBitBelowEnd = BeltRouteFaceBits.FaceBit(query.EndDirection, 1);
            _faceBitBelowEndFront = BeltRouteFaceBits.FaceBit(BeltRouteDirection.Opposite(query.EndDirection), 1);
        }

        public BeltRouteQuery Query => _query;

        // 地面にも既存ブロックにも既存坂の真上にも当たらないセルか（判定は BeltRouteCellJudge の1本）
        // Whether the cell is clear of ground, existing blocks and existing slope heads (judged by BeltRouteCellJudge alone)
        public bool IsCellFree(Vector3Int cell)
        {
            return BeltRouteCellJudge.Judge(_world, cell) == BeltRouteCellBlockReason.None;
        }

        // 坂の真上が空いているか。既存ブロックが乗るとサーバーが上を優先して繋ぎ坂が切れる
        // Whether the cell above a slope is clear; an existing block there is wired first by the server and cuts the slope
        // 始点・終点は必ず置くので、その真下の坂は既存ブロックが乗るのと同じ。探索で弾かないと組み立てだけが弾いて最短コストの経路が組めず経路なしになる
        // The start and end are always placed, so a slope right below them is as cut as one under an existing block; unless the search rejects it, only recovery does and the shortest cost has no assemblable route
        public bool IsSlopeHeadFree(Vector3Int slopeCell)
        {
            var head = slopeCell + Vector3Int.up;
            if (head == _query.StartCell || head == _query.EndCell) return false;
            return !_world.IsBlockedByBlock(head);
        }

        // 始点・終点以外のセルとして置けるか。incomingは手前セルの出る向き、outgoingはこのセルの出る向き
        // Whether the cell may be a mid-route cell; incoming is the previous cell's exit heading, outgoing this cell's
        public bool IsIntermediatePlaceable(Vector3Int cell, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing)
        {
            // 終点の前のセルは、終点が流す先として空けておく
            // The cell in front of the end stays free as the end's delivery target
            if (cell == _query.StartCell || cell == _query.EndCell || cell == _endFrontCell) return false;
            if (!IsCellFree(cell)) return false;
            if (kind.IsSlope() && !IsSlopeHeadFree(cell)) return false;
            if ((OutputFaceBits(cell) & EntryFaceBits(kind, incoming, outgoing)) != 0) return false;
            return !FeedsForeignInput(cell, kind, incoming, outgoing);
        }

        // 手前の状態から終点セルへ入れるか。終点は空セルで向きが固定の直線
        // Whether the end cell can be entered from the previous state; the end is a straight cell of fixed facing
        public bool CanEnterGoal(BeltRouteState previous)
        {
            var next = previous.Cell + BeltRouteDirection.Forward(previous.Outgoing);
            if (next.x != _query.EndCell.x || next.z != _query.EndCell.z) return false;
            if (previous.ExitHeight != _query.EndCell.y) return false;
            return CanEnterGoalFrom(previous.Outgoing);
        }

        // 分岐器の終点は背面入力なので入る向きが正面と一致、通常ベルトは背面・左右のどこからでも入れる
        // A splitter end takes input from its back so the entry heading must equal its facing; a normal belt accepts back, left or right
        public bool CanEnterGoalFrom(BlockDirection outgoing)
        {
            if (_query.TurnByOutput) return outgoing == _query.EndDirection;
            return IsTurnAllowed(outgoing, _query.EndDirection);
        }

        // 次のセルは前・左・右へ出られる。Uターンだけは入口と出口が同じ面になるので不可（分岐器も幾何は同じ）
        // The next cell may exit forward, left or right; only a U-turn is impossible since entry and exit share a face (splitters share the geometry)
        public bool IsTurnAllowed(BlockDirection incoming, BlockDirection outgoing)
        {
            return outgoing != BeltRouteDirection.Opposite(incoming);
        }

        // 同じ辺で隣り合う2状態が経路として繋がるか（高さ・山谷・曲がりの3条件）
        // Whether two states adjacent across a face connect as a route (height, peak/valley, and turn conditions)
        public bool IsConnected(BeltRouteState previous, BeltRouteState next)
        {
            var expected = previous.Cell + BeltRouteDirection.Forward(previous.Outgoing);
            if (expected.x != next.Cell.x || expected.z != next.Cell.z) return false;
            if (previous.ExitHeight != next.EntryHeight) return false;
            if (!next.Kind.CanFollow(previous.Kind)) return false;
            if (next.Kind.IsSlope()) return next.Outgoing == previous.Outgoing;
            return IsTurnAllowed(previous.Outgoing, next.Outgoing);
        }

        // 既存の出力口の面ビットに、終点自身の出口の辺を重ねる
        // Existing output faces with the end's own exit edge laid over them
        private int OutputFaceBits(Vector3Int cell)
        {
            var bits = _world.OutputFaceBits(cell);
            if (cell == _cellBelowEnd) bits |= _faceBitBelowEnd;
            if (cell == _cellBelowEndFront) bits |= _faceBitBelowEndFront;
            return bits;
        }

        // 入口の面が既存の出力口に向いていると、そのラインの荷物を横取りする
        // An entry face aimed at by an existing output would steal that line's items
        // 坂なしの直線ベルトは背面・左右の3面が入口。分岐器と坂は手前セルへ向く1面だけ（下り坂は1つ上の高さ）
        // A flat straight belt takes input on back, left and right; splitters and slopes only on the face toward the previous cell (a down slope one level up)
        private int EntryFaceBits(BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing)
        {
            if (kind == BeltRouteCellKind.Straight && !_query.TurnByOutput)
            {
                return BeltRouteFaceBits.FaceBit(BeltRouteDirection.Opposite(outgoing), 0)
                       | BeltRouteFaceBits.FaceBit(BeltRouteDirection.Left(outgoing), 0)
                       | BeltRouteFaceBits.FaceBit(BeltRouteDirection.Right(outgoing), 0);
            }
            return BeltRouteFaceBits.FaceBit(BeltRouteDirection.Opposite(incoming), kind.EntryHeightOffset());
        }

        // 分岐器は使わない出口も流すので、その先に既存の受け手がいてはいけない
        // A splitter also emits through its unused exits, so none of them may face an existing receiver
        private bool FeedsForeignInput(Vector3Int cell, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing)
        {
            if (!_query.TurnByOutput || kind != BeltRouteCellKind.Straight) return false;
            var unusedExits = BeltRouteFaceBits.FaceBit(incoming, 0)
                              | BeltRouteFaceBits.FaceBit(BeltRouteDirection.Left(incoming), 0)
                              | BeltRouteFaceBits.FaceBit(BeltRouteDirection.Right(incoming), 0);
            unusedExits &= ~BeltRouteFaceBits.FaceBit(outgoing, 0);
            return (_world.InputFaceBits(cell) & unusedExits) != 0;
        }
    }
}
```

`BeltRouteCellJudge` はこのタスクで `D/World/BeltRouteCellJudge.cs` に作る（Task 3 で `BeltRouteCellCheck` がこれへ寄せる）:

```csharp
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     セルにベルトを置けない理由。端点の判定（セッション）と途中セルの判定（探索規則）が同じ1本を通る
    ///     Why a cell cannot take a belt; endpoint checks (session) and mid-route checks (search rule) share this one judgement
    /// </summary>
    public enum BeltRouteCellBlockReason
    {
        None,
        ExistingBlock,
        Terrain,
        SlopeHead,
    }

    public static class BeltRouteCellJudge
    {
        public static BeltRouteCellBlockReason Judge(IBeltRouteWorld world, Vector3Int cell)
        {
            if (world.IsBlockedByBlock(cell)) return BeltRouteCellBlockReason.ExistingBlock;
            if (world.IsBlockedByTerrain(cell)) return BeltRouteCellBlockReason.Terrain;
            if (world.IsSlopeHead(cell)) return BeltRouteCellBlockReason.SlopeHead;
            return BeltRouteCellBlockReason.None;
        }
    }
}
```

`D/Session/BeltRouteCellCheck.cs` の enum `BeltRouteCellBlockReason` はこのタスクで削除し（`OutOfRange` ごと）、`using ...BeltConveyor.World;` を足して World 側の enum を使う。`JudgeInGrid(BeltRouteGrid grid, cell)` は `if (!grid.Contains(cell)) return ...OutOfRange;` の行を消し、残りを `return BeltRouteCellJudge.Judge(grid, cell);` にする（Task 3 で `JudgeInGrid` 自体を消す）。`D/Session/BeltRouteTooltipKey.cs` の `CellLine` から `OutOfRange` の行を消す。

- [ ] **Step 3: 探索を書き直す**

`D/Route/Search/BeltRouteSearch.cs` 全文:

```csharp
using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     探索の終わり方。終点に届いた・待ち行列が尽きた・展開数の上限に達した
    ///     How the search ended: the goal was reached, the queue drained, or the expansion cap was hit
    /// </summary>
    public enum BeltRouteSearchOutcome
    {
        Reached,
        Exhausted,
        CapReached,
    }

    /// <summary>
    ///     状態（セル・出る向き・種別）の上で重み付き A* を回し、触った状態のコストを辞書に埋める。経路の組み立てはBeltRouteRecovery
    ///     Runs weighted A* over states (cell, exit heading, kind) and records the cost of every touched state in a dictionary; route assembly is BeltRouteRecovery's job
    ///     評価値は g + 2h。開けた場所では最短のまま、障害物の周りだけ遠回りを許して探索量を抑える（ADR 0100）
    ///     The score is g + 2h: shortest in the open, detours allowed only around obstacles, keeping the explored set small (ADR 0100)
    ///     箱を持たないので範囲の上限は無く、展開数の上限だけが探索を止める
    ///     Without a box there is no range limit; only the expansion cap stops the search
    /// </summary>
    public sealed class BeltRouteSearch
    {
        public const int Unreached = int.MaxValue;
        public const int HeuristicWeight = 2;

        private readonly Dictionary<BeltRouteState, int> _bestCost = new();
        private readonly BeltRouteOpenHeap _open = new();

        // 1回のRunとその後のExpandMoreの間だけ有効
        // Valid only during one Run and the ExpandMore calls that follow it
        private BeltRouteTransitionRule _rule;
        private BeltRouteState _goal;
        private int _expansions;

        // 経路なしのとき呼び手がログに出す
        // Reported by the caller's log when there is no route
        public int Expansions => _expansions;

        public int BestCost(BeltRouteState state) => _bestCost.TryGetValue(state, out var g) ? g : Unreached;

        public BeltRouteSearchOutcome Run(BeltRouteTransitionRule rule, IReadOnlyList<(BeltRouteState state, int cost)> startStates, int maxExpansions)
        {
            _bestCost.Clear();
            _open.Clear();
            _rule = rule;
            _expansions = 0;
            var query = rule.Query;
            _goal = new BeltRouteState(query.EndCell, query.EndDirection, BeltRouteCellKind.Straight);
            foreach (var (state, cost) in startStates) Relax(state, cost);
            return ExpandUntil(maxExpansions, true);
        }

        // 終点に届いた後、組み立ての代替候補を増やすために展開を続ける
        // After the goal is reached, keeps expanding so recovery has more alternatives to assemble from
        public void ExpandMore(int extraExpansions)
        {
            ExpandUntil(_expansions + extraExpansions, false);
        }

        private BeltRouteSearchOutcome ExpandUntil(int expansionLimit, bool stopAtGoal)
        {
            while (0 < _open.Count)
            {
                // silent-ok: 上限到達は呼び手（BeltRoutePlanner）が展開数つきでログに出す
                if (expansionLimit <= _expansions) return BestCost(_goal) == Unreached ? BeltRouteSearchOutcome.CapReached : BeltRouteSearchOutcome.Reached;
                var (_, g, state) = _open.Pop();
                // 同じ状態がより小さいgで積み直された後の古い項は飛ばす
                // Skip stale entries left behind when the same state was re-queued with a smaller g
                if (g != BestCost(state)) continue;
                if (state.Equals(_goal))
                {
                    if (stopAtGoal) return BeltRouteSearchOutcome.Reached;
                    continue;
                }
                _expansions++;
                Expand(state, g);
            }
            // silent-ok: 待ち行列の枯渇は呼び手（BeltRoutePlanner）がログに出す
            return BestCost(_goal) == Unreached ? BeltRouteSearchOutcome.Exhausted : BeltRouteSearchOutcome.Reached;
        }

        private void Expand(BeltRouteState state, int g)
        {
            // 出る向きへ水平に1歩
            // One horizontal step along the exit heading
            var nextColumn = state.Cell + BeltRouteDirection.Forward(state.Outgoing);
            var exitHeight = state.ExitHeight;
            for (var k = 0; k < BeltRouteCellKindExtension.KindCount; k++)
            {
                var nextKind = (BeltRouteCellKind)k;
                if (nextKind.IsSlope() && !_rule.Query.AllowSlopes) continue;
                if (!nextKind.CanFollow(state.Kind)) continue;
                var nextCell = new Vector3Int(nextColumn.x, exitHeight - nextKind.EntryHeightOffset(), nextColumn.z);

                // 終点セルそのものへ入るときは終点状態（直線・固定向き）だけを生成する。終点の列の他の高さは普通のセル
                // Entering the end cell itself generates only the goal state (straight, fixed facing); other heights of that column are ordinary cells
                if (nextCell == _goal.Cell)
                {
                    if (nextKind == BeltRouteCellKind.Straight && _rule.CanEnterGoalFrom(state.Outgoing)) Relax(_goal, g + BeltRouteCellKindExtension.StraightCost);
                    continue;
                }
                TryRelax(nextCell, nextKind, state.Outgoing, state.Outgoing, g);
                if (nextKind.IsSlope()) continue;
                TryRelax(nextCell, nextKind, state.Outgoing, BeltRouteDirection.Left(state.Outgoing), g);
                TryRelax(nextCell, nextKind, state.Outgoing, BeltRouteDirection.Right(state.Outgoing), g);
            }
        }

        private void TryRelax(Vector3Int cell, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing, int g)
        {
            if (!_rule.IsIntermediatePlaceable(cell, kind, incoming, outgoing)) return;
            Relax(new BeltRouteState(cell, outgoing, kind), g + kind.Cost());
        }

        private void Relax(BeltRouteState state, int g)
        {
            if (BestCost(state) <= g) return;
            _bestCost[state] = g;
            _open.Push(g + HeuristicWeight * Heuristic(state), g, state);
        }

        // 残り水平距離と残り高低差から下回らないコストを見積もる（一歩は直線5・坂7）
        // Lower-bounds the remaining cost from horizontal distance and height difference (a step is 5 straight, 7 slope)
        private int Heuristic(BeltRouteState state)
        {
            var horizontal = Mathf.Abs(_goal.Cell.x - state.Cell.x) + Mathf.Abs(_goal.Cell.z - state.Cell.z);
            var vertical = Mathf.Abs(_goal.Cell.y - state.ExitHeight);
            return BeltRouteCellKindExtension.StraightCost * Mathf.Max(horizontal, vertical) + (BeltRouteCellKindExtension.SlopeCost - BeltRouteCellKindExtension.StraightCost) * vertical;
        }
    }
}
```

- [ ] **Step 4: 組み立ての候補から箱の内外と優先順を外す**

`D/Route/Search/BeltRouteRecoveryCandidates.cs`:
- `_ranked` の要素型を `(int bound, BeltRouteState state)` に、`Insert`/`IsBefore` を bound だけの比較にする（同じ bound は列挙順を保つ安定挿入のまま）。
- `Collect` の `if (!rule.IsInsideBox(previous.Cell)) continue;` を削除。`Insert((best + placedCost, Rank(rule, previous), previous))` → `Insert((best + placedCost, previous))`。
- `Rank` メソッドを削除。summary の「同じなら優先順に並べる」を「同じなら列挙順（直進・左・右、直線・上り・下り）」に直す。
- `BeltRouteRecovery.cs` は変更なし。

- [ ] **Step 5: 計画と結果型を書き直す**

`D/Route/BeltRoutePlan.cs`: `BeltRouteNoPathReason` enum とその summary を削除。`BeltRoutePlanResult` を次に。

```csharp
    /// <summary>
    ///     探索の結果。経路があればPlan、無ければ経路なし（利用者向けの理由は「見つかりません」の1つ。ADR 0100）
    ///     The search outcome: a plan when a route exists, otherwise no route (the single user-facing reason is "not found", ADR 0100)
    /// </summary>
    public sealed class BeltRoutePlanResult
    {
        public readonly BeltRoutePlan Plan;

        public bool HasPlan => Plan != null;

        private BeltRoutePlanResult(BeltRoutePlan plan)
        {
            Plan = plan;
        }

        public static BeltRoutePlanResult Found(BeltRoutePlan plan) => new(plan);
        public static BeltRoutePlanResult NotFound() => new(null);
    }
```

`D/Route/Search/BeltRoutePlanner.cs` 全文:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     経路計画の入口。世界をその場で読む重み付き探索→組み立て。箱も範囲の上限も無く、探索量の上限だけで経路なしを決める（ADR 0100）
    ///     The entry for route planning: a weighted search reading the world on demand, then recovery; no box or range limit, only the expansion cap decides no-route (ADR 0100)
    /// </summary>
    public sealed class BeltRoutePlanner
    {
        public const int NoRecoverySlack = 0;

        // 本番の余裕。直線4セル分。最短が自分の坂の真上を通る形しか無い配置は2セル分の遠回りで組めるのが実測で、その倍を見ている（ADR 0097）
        // The production slack: four straight cells; layouts whose shortest exists only through an own slope head assemble with a two-cell detour by measurement, so twice that is allowed (ADR 0097)
        public const int DefaultRecoverySlack = 20;

        // 探索量の上限。囲まれた終点・遠すぎる終点はここで止まる。手元で約 100 ms に収まる見込みの値（実測は ADR 0100）
        // The expansion cap; enclosed or far-away ends stop here. Chosen to stay around 100 ms locally (measurements in ADR 0100)
        public const int MaxExpansions = 20000;

        // 最短で組めないとき、組み立ての代替候補を増やすために追加で展開する数
        // Extra expansions run when the shortest cannot be assembled, so recovery sees more alternatives
        private const int RecoveryExtraExpansions = 4000;

        private readonly int _recoverySlack;
        private readonly BeltRouteSearch _search = new();
        private readonly BeltRouteRecovery _recovery;
        private IBeltRouteWorld _world;

        public BeltRoutePlanner(IBeltRouteWorld world, int recoverySlack)
        {
            _world = world;
            _recoverySlack = recoverySlack;
            _recovery = new BeltRouteRecovery(_search);
        }

        // 世界を差し替える（Task 3 で BeltRouteSession が BeltRouteLiveWorld を持つまでの暫定。Task 3 で削除）
        // Swaps the world (interim until BeltRouteSession owns a BeltRouteLiveWorld in Task 3; removed there)
        public void ReplaceWorld(IBeltRouteWorld world)
        {
            _world = world;
        }

        // 始点・終点のセル自体の可否は呼び出し側（セッション）が先に確かめている
        // The start and end cells themselves are validated beforehand by the caller (the session)
        public BeltRoutePlanResult Plan(BeltRouteQuery query)
        {
            var rule = new BeltRouteTransitionRule(_world, query);
            var startStates = BuildStartStates(query, rule);
            if (startStates.Count == 0) return NotFound(query, "no start state");

            var outcome = _search.Run(rule, startStates, MaxExpansions);
            if (outcome != BeltRouteSearchOutcome.Reached) return NotFound(query, outcome.ToString());

            // まず届いたコストちょうどで組む。組めなければ少し探索を進め、余裕ぶん広げて組み直す
            // Assemble at exactly the reached cost first; if that fails, search a little further and assemble again with the slack
            var reached = _search.BestCost(new BeltRouteState(query.EndCell, query.EndDirection, BeltRouteCellKind.Straight));
            var plan = _recovery.Recover(rule, startStates, reached);
            if (plan == null && _recoverySlack != NoRecoverySlack)
            {
                _search.ExpandMore(RecoveryExtraExpansions);
                plan = _recovery.Recover(rule, startStates, reached + _recoverySlack);
            }
            return plan == null ? NotFound(query, "recovery failed") : BeltRoutePlanResult.Found(plan);
        }

        // 利用者には理由行「経路が見つかりません」、開発者には出どころと展開数をログで
        // The user sees the "no route found" line; developers get the origin and the expansion count in the log
        private BeltRoutePlanResult NotFound(BeltRouteQuery query, string origin)
        {
            Debug.Log($"[BeltRoute] no route {query.StartCell}->{query.EndCell}: {origin} expansions:{_search.Expansions}");
            return BeltRoutePlanResult.NotFound();
        }

        // 通常ベルトは直線か上りで出発、分岐器は背面入力のまま3方向へ、分岐の始点は既存ベルトの出口（置かないのでコスト0）
        // A normal belt starts straight or rising, a splitter keeps its back entry and exits three ways, and a branch start is the existing belt's exit (cost 0, never placed)
        private static List<(BeltRouteState state, int cost)> BuildStartStates(BeltRouteQuery query, BeltRouteTransitionRule rule)
        {
            var starts = new List<(BeltRouteState, int)>(3);
            var cell = query.StartCell;
            var direction = query.StartDirection;
            if (query.StartIsExistingBelt)
            {
                starts.Add((new BeltRouteState(cell, direction, BeltRouteCellKind.Straight), 0));
                return starts;
            }

            if (query.TurnByOutput)
            {
                starts.Add((new BeltRouteState(cell, direction, BeltRouteCellKind.Straight), BeltRouteCellKindExtension.StraightCost));
                starts.Add((new BeltRouteState(cell, BeltRouteDirection.Left(direction), BeltRouteCellKind.Straight), BeltRouteCellKindExtension.StraightCost));
                starts.Add((new BeltRouteState(cell, BeltRouteDirection.Right(direction), BeltRouteCellKind.Straight), BeltRouteCellKindExtension.StraightCost));
                return starts;
            }

            starts.Add((new BeltRouteState(cell, direction, BeltRouteCellKind.Straight), BeltRouteCellKindExtension.StraightCost));
            if (query.AllowSlopes && rule.IsSlopeHeadFree(cell)) starts.Add((new BeltRouteState(cell, direction, BeltRouteCellKind.Up), BeltRouteCellKindExtension.SlopeCost));
            return starts;
        }
    }
}
```

- [ ] **Step 6: 不要になった型を消し、Grid の添字 API を落とす**

```bash
cd moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor
git rm Route/Search/BeltRouteCostBuckets.cs Route/Search/BeltRouteCostBuckets.cs.meta Route/Search/BeltRouteReachability.cs Route/Search/BeltRouteReachability.cs.meta Route/BeltRouteObstacleMask.cs Route/BeltRouteObstacleMask.cs.meta
```

- `D/Route/BeltRouteState.cs`: `BeltRouteStateCodec` クラスとその summary を削除。
- `D/Route/BeltRouteGrid.cs`: `CellIndexOrOutside`・`CellAt`・`IsBlockedByTerrainAt`・`IsBlockedByBlockAt`・`IsSlopeHeadAt`・`OutputFaceBits(int)`・`InputFaceBits(int)`・`LayerCellCount` を削除。`CreateAround`・`HorizontalSize`・`HeadroomAboveMax` は `BeltRouteTerrainProfile` が使うので Task 3 まで残す。summary の「探索の内側向けにセル添字で引く版も持つ」の2行を消す。
- `D/Session/BeltRouteSession.cs`（最小追従）: `_grid` フィールドを足し、コンストラクタで `_grid = scanner.Build(terrain); _planner = new BeltRoutePlanner(_grid, BeltRoutePlanner.DefaultRecoverySlack);`、`RebuildObstacles` で `_grid = _scanner.Build(_terrain); _planner.ReplaceWorld(_grid);`、`public BeltRouteGrid Grid => _grid;`。
- `D/Session/BeltRouteRoutingFrame.cs`: `feedback.Add(BeltRouteTooltipKey.NoPathLine(result.NoPathReason));` → `feedback.Add(BeltRouteTooltipKey.NoPathLine());`
- `D/Session/BeltRouteTooltipKey.cs`: `NoPathLine(BeltRouteNoPathReason reason)` → 引数なしで `return new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltRouteBlockedByBoth);`（暫定。Task 3 で `PlaceBeltRouteNotFound` に差し替える）。

- [ ] **Step 7: テストを追従させる**

`T/BeltRoutePlanVerifier.cs`: `new BeltRouteTransitionRule(grid, query, BeltRouteObstacleMask.None)` → `new BeltRouteTransitionRule(grid, query)`。

`T/BeltRouteReferenceSolver.cs`: `Walk` の `if (!_rule.IsInsideBox(nextCell)) continue;` → `if (!_rule.IsCellFree(nextCell)) continue;`（箱の外は Grid が占有扱いで返すので同じ意味）。

`T/BeltRouteConveyanceTest.cs`: `$"no route: {result.NoPathReason}"` の3箇所 → `"no route"`。

`T/BeltRoutePlannerBasicTest.cs`:
- クラスコメントの「Cキーの切り替え」を削る（C のテスト自体は Task 3 で消す。`Priority` 引数はこのタスクでは残す）。
- `ReplacedGridWithSameBoxIsAccepted` を削除。
- `WallOfBlocksIsReportedAsExistingBlockObstacle` → 名前 `WallOfBlocksYieldsNoRoute`、`Assert.AreEqual(BeltRouteNoPathReason.ExistingBlock, ...)` を削除。
- `TerrainRidgeIsReportedAsTerrainObstacle` → `TerrainRidgeYieldsNoRoute`、同様に。
- `EndOutsideBoxIsOutOfRange` → `EndOutsideTheTestWorldYieldsNoRoute`、同様に。
- `CellReceivingExistingOutputIsAvoided` の `Assert.AreEqual(8, result.Plan.Cells.Count);` → `Assert.GreaterOrEqual(result.Plan.Cells.Count, 8);`
- `EndAboveStartDetoursInsteadOfRisingUnderTheEnd` の `Assert.AreEqual(expectedCost, result.Plan.TotalCost);` → `Assert.LessOrEqual(result.Plan.TotalCost, expectedCost * BeltRouteSearch.HeuristicWeight);`、`$"end {query.EndCell}: {result.NoPathReason}"` → `$"end {query.EndCell}: no route"`。

`T/BeltRouteReferenceCrossCheckTest.cs`:
- クラスコメントの3〜6行目を「本番は重み付き探索なので最短性は求めない。参照が見つけたら本番も見つけ（存在）、コストは参照の HeuristicWeight 倍＋余裕以内（上限）。余裕なしの比較は意味を失うので本番の余裕だけで回す」に。
- `[TestCase(...)]` 2つを外し、`[Test] public void RandomSmallBoxesAgreeWithTheExhaustiveReference()` にして `recoverySlack` は `BeltRoutePlanner.DefaultRecoverySlack` のローカル定数に。`KnownGapAllowed` を削除。
- `IsKnownGap`・`IsLongerWithinSlack` を削除し、本体ループを次にする。

```csharp
                var result = new BeltRoutePlanner(grid, BeltRoutePlanner.DefaultRecoverySlack).Plan(query);
                var rule = new BeltRouteTransitionRule(grid, query);
                var referenceCost = new BeltRouteReferenceSolver(rule, CostCap, true).Solve();
                if (referenceCost != BeltRouteReferenceSolver.NotFound) referenceFound++;
                if (result.HasPlan) BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, label);
                var disagreement = Disagreement(result, referenceCost);
                if (disagreement != null) disagreements.Add($"{label}: {disagreement}");
```

```csharp
        // 参照が見つけたら本番も見つける。コストは参照の HeuristicWeight 倍＋余裕まで。参照が上限内に見つけないなら本番も上限内の経路を出さない。一致ならnull
        // When the reference finds a route the production must too, within HeuristicWeight times the reference plus the slack; when the reference finds none within the cap, production must not produce one within the cap; null when they agree
        private static string Disagreement(BeltRoutePlanResult result, int referenceCost)
        {
            if (referenceCost == BeltRouteReferenceSolver.NotFound)
            {
                if (result.HasPlan && result.Plan.TotalCost <= CostCap) return $"production found cost {result.Plan.TotalCost} but the reference found nothing within the cap";
                return null;
            }
            if (!result.HasPlan) return $"reference found a route of cost {referenceCost} but production found none";
            var bound = referenceCost * BeltRouteSearch.HeuristicWeight + BeltRoutePlanner.DefaultRecoverySlack;
            if (bound < result.Plan.TotalCost) return $"production cost {result.Plan.TotalCost} exceeds the bound {bound} (reference {referenceCost})";
            return null;
        }
```

- 末尾の Assert は `Assert.Greater(referenceFound, Iterations / 10, ...)` と `Assert.AreEqual(0, disagreements.Count, ...)` の2つだけにし、`Debug.Log` の集計行も合わせる。

- [ ] **Step 8: コンパイルしテストを回す**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaceSystem\.BeltRoute\."`
Expected: 全件 PASS。`BeltRouteReferenceCrossCheckTest` のログの件数を報告ファイルに写す。

- [ ] **Step 9: コミットする**

```bash
git checkout -- .moorestech-external-revisions.json moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/World/BeltRouteCellJudge.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/World/BeltRouteCellJudge.cs.meta moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/BeltRouteSession.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/BeltRouteRoutingFrame.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/BeltRouteTooltipKey.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/BeltRouteCellCheck.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute
git commit -m "Task 2: feat(belt): 探索を辞書とヒープの重み付き A* にし、展開数の上限だけで経路なしを決める"
```

---

### Task 3: セッション・設置系・入力・文言を箱なしに合わせる（C キー廃止、地形計測と範囲外の削除、テストの箱をテスト側へ）

**Files:**
- Modify: `D/Session/BeltRouteSession.cs`（全面）、`D/Session/BeltRouteRoutingFrame.cs`、`D/Session/BeltRouteCellCheck.cs`、`D/Session/BeltRouteTooltipKey.cs`、`D/Session/BeltRouteKeyInput.cs`、`D/BeltConveyorPlaceSystem.cs`
- Modify: `D/Route/BeltRouteQuery.cs`、`D/Route/Search/BeltRoutePlanner.cs`（`ReplaceWorld` を削除）
- Delete: `D/World/BeltRouteTerrainProfile.cs`、`D/World/BeltRouteWorldScanner.cs`、`D/Route/BeltRouteStepPriority.cs`、`D/Route/BeltRouteEdgeTable.cs`（+ .meta）
- Move+Rewrite: `D/Route/BeltRouteGrid.cs`（+ .meta）→ `T/World/BeltRouteGrid.cs`
- Modify: `Localization/localization.csv`、`moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`、`moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（生成）
- Modify: `T/BeltRoutePlannerBasicTest.cs`、`T/BeltRouteRandomizedSearchTest.cs`、`T/BeltRouteReferenceCrossCheckTest.cs`、`T/BeltRouteConveyanceTest.cs`、`T/BeltRouteConveyanceWorld.cs`、`T/BeltRoutePlanVerifier.cs`（`BeltRouteGrid` の名前空間と `BeltRouteQuery` の引数）

- [ ] **Step 1: `BeltRouteQuery` から `Priority` を外し、不要な型を消す**

`D/Route/BeltRouteQuery.cs`: フィールド `Priority`、コンストラクタ引数 `BeltRouteStepPriority priority` と代入、`IsSameQuery` の `&& Priority.Equals(other.Priority)` を削除。summary の「候補の優先順」を削る。

```bash
cd moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor
git rm Route/BeltRouteStepPriority.cs Route/BeltRouteStepPriority.cs.meta World/BeltRouteTerrainProfile.cs World/BeltRouteTerrainProfile.cs.meta World/BeltRouteWorldScanner.cs World/BeltRouteWorldScanner.cs.meta Route/BeltRouteEdgeTable.cs Route/BeltRouteEdgeTable.cs.meta
mkdir -p ../../../../../Client.Tests/PlaceSystem/BeltRoute/World
git mv Route/BeltRouteGrid.cs ../../../../../Client.Tests/PlaceSystem/BeltRoute/World/BeltRouteGrid.cs
git mv Route/BeltRouteGrid.cs.meta ../../../../../Client.Tests/PlaceSystem/BeltRoute/World/BeltRouteGrid.cs.meta
```

（相対パスは `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/World/` を指す。`World.meta` は Editor が生成する。）

`D/Route/Search/BeltRoutePlanner.cs` から `ReplaceWorld` を削除し `_world` を `readonly` に。

テストの `new BeltRouteQuery(..., BeltRouteStepPriority.Default)` / `new BeltRouteStepPriority(...)` を全て最後の引数なしに直す（`T/BeltRoutePlannerBasicTest.cs` の `Query` ヘルパは1本に畳み、`CyclingPriorityYieldsDifferentEqualCostRoutes` を削除。`T/BeltRouteRandomizedSearchTest.cs` と `T/BeltRouteReferenceCrossCheckTest.cs` の `TryPickQuery`、`T/BeltRouteConveyanceTest.cs` の3箇所。ラベル文字列の ` priority:{query.Priority.Index}` も削る）。

- [ ] **Step 2: テストの世界 `BeltRouteGrid` を辞書で書き直す**

`T/World/BeltRouteGrid.cs` 全文（`using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;` と `...World;`）:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute.World
{
    // テスト用の世界。有限の箱で、列ごとの地形の下限・占有・坂の真上・入出力辺を持つ。箱の外は占有扱いで探索が外へ出ない
    // A test world: a finite box holding per-column terrain floors, occupancy, slope heads and port edges; outside the box counts as occupied so the search never leaves it
    // 面ビットの立て方は本番と同じ BeltRouteFaceBits.Mark
    // Face bits are raised by the same BeltRouteFaceBits.Mark as production
    public sealed class BeltRouteGrid : IBeltRouteWorld, IBeltRouteEdgeSink
    {
        private const int NoGround = int.MinValue;

        public readonly Vector3Int Min;
        public readonly Vector3Int Size;

        private readonly int[] _columnFloorY;
        private readonly HashSet<Vector3Int> _blockCells = new();
        private readonly HashSet<Vector3Int> _slopeHeads = new();
        private readonly Dictionary<Vector3Int, int> _outputFaceBits = new();
        private readonly Dictionary<Vector3Int, int> _inputFaceBits = new();

        public BeltRouteGrid(Vector3Int min, Vector3Int size)
        {
            Min = min;
            Size = size;
            _columnFloorY = new int[size.x * size.z];
            for (var i = 0; i < _columnFloorY.Length; i++) _columnFloorY[i] = NoGround;
        }

        public bool Contains(Vector3Int cell)
        {
            return Min.x <= cell.x && cell.x < Min.x + Size.x &&
                   Min.y <= cell.y && cell.y < Min.y + Size.y &&
                   Min.z <= cell.z && cell.z < Min.z + Size.z;
        }

        public void SetColumnFloor(int x, int z, int floorY) => _columnFloorY[(z - Min.z) * Size.x + (x - Min.x)] = floorY;
        public void MarkBlockCell(Vector3Int cell) => _blockCells.Add(cell);
        public void MarkSlopeHead(Vector3Int cell) => _slopeHeads.Add(cell);
        public void AddOutputEdge(BeltRouteEdge edge) => BeltRouteFaceBits.Mark(edge, _outputFaceBits);
        public void AddInputEdge(BeltRouteEdge edge) => BeltRouteFaceBits.Mark(edge, _inputFaceBits);

        public bool IsBlockedByBlock(Vector3Int cell) => !Contains(cell) || _blockCells.Contains(cell);

        public bool IsBlockedByTerrain(Vector3Int cell)
        {
            if (!Contains(cell)) return false;
            var floor = _columnFloorY[(cell.z - Min.z) * Size.x + (cell.x - Min.x)];
            return floor != NoGround && cell.y < floor;
        }

        public bool IsSlopeHead(Vector3Int cell) => _slopeHeads.Contains(cell);
        public int OutputFaceBits(Vector3Int cell) => _outputFaceBits.TryGetValue(cell, out var bits) ? bits : 0;
        public int InputFaceBits(Vector3Int cell) => _inputFaceBits.TryGetValue(cell, out var bits) ? bits : 0;
    }
}
```

`T/` 配下の `BeltRouteGrid` を使うファイル（`BeltRoutePlannerBasicTest`・`BeltRouteRandomizedSearchTest`・`BeltRouteReferenceCrossCheckTest`・`BeltRouteConveyanceWorld`・`BeltRoutePlanVerifier`）に `using Client.Tests.PlaceSystem.BeltRoute.World;` を足す。旧 Grid の `IsBlockedByBlock(cell)`/`IsBlockedByTerrain(cell)`/`IsSlopeHead(cell)`（`TryPickFreeCell` が使う）と `Min`/`Size`/`SetColumnFloor`/`MarkBlockCell`/`MarkSlopeHead`/`AddOutputEdge`/`AddInputEdge` は名前が同じなので呼び手は変わらない。

- [ ] **Step 3: セッションを本番の世界で書き直す**

`D/Session/BeltRouteSession.cs` 全文:

```csharp
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     始点を決めてから終点を置くまでの状態。本番の世界と探索器を持ち、同じ問い合わせの結果は使い回す
    ///     The state between fixing the start and placing the end; owns the live world and planner and reuses results for identical queries
    /// </summary>
    public sealed class BeltRouteSession
    {
        public readonly Vector3Int StartCell;
        public readonly BlockDirection StartDirection;

        // 分岐の始点。通常の始点ではnull
        // The branch start; null for a normal start
        public readonly BeltRouteBranchOrigin Branch;

        private readonly BeltRouteLiveWorld _world;
        private readonly BeltRoutePlanner _planner;
        private BeltRouteQuery _lastQuery;
        private BeltRoutePlanResult _lastResult;

        private BeltRouteSession(Vector3Int startCell, BlockDirection startDirection, BeltRouteBranchOrigin branch, BlockGameObjectDataStore blocks)
        {
            StartCell = startCell;
            StartDirection = startDirection;
            Branch = branch;
            _world = new BeltRouteLiveWorld(blocks);
            _planner = new BeltRoutePlanner(_world, BeltRoutePlanner.DefaultRecoverySlack);
        }

        public bool IsBranch => Branch != null;
        public IBeltRouteWorld World => _world;

        public static BeltRouteSession CreateAtCell(Vector3Int startCell, BlockDirection startDirection, BlockGameObjectDataStore blocks)
        {
            return new BeltRouteSession(startCell, startDirection, null, blocks);
        }

        // 分岐は既存ベルトのセルが始点で、向きは幾何が選んだ出口
        // A branch starts at the existing belt's cell, heading out of the exit the geometry chose
        public static BeltRouteSession CreateBranch(BeltRouteBranchOrigin branch, BlockGameObjectDataStore blocks)
        {
            return new BeltRouteSession(branch.Cell, branch.ExitDirection, branch, blocks);
        }

        // ブロックの設置・撤去で占有と辺を作り直す。地形は列ごとの記憶のまま
        // Rebuilds occupancy and edges after a block is placed or removed; the per-column terrain memory stays
        public void RebuildObstacles()
        {
            _world.Rebuild();
            _lastQuery = null;
            _lastResult = null;
        }

        // 作り直しで始点が使えなくなったら始点の選び直しへ戻す
        // When the rebuild invalidates the start, the caller returns to start selection
        public bool IsStartUsable(BlockGameObjectDataStore blocks, BeltConveyorHoldingBlock holding, bool useAlternateExit)
        {
            if (IsBranch)
            {
                // 同じ個体でも隣接変化で分岐器の向き・出口が変わっていたら使えない
                // Even for the same belt, a changed splitter facing or exit makes the start unusable
                if (!blocks.TryGetBlockGameObject(Branch.Cell, out var current) || !current.BlockInstanceId.Equals(Branch.Belt.BlockInstanceId)) return false;
                var reason = BeltRouteBranchOrigin.Judge(blocks, current, holding, useAlternateExit, out var rejudged);
                return reason == BeltRouteBranchBlockReason.None && rejudged.SplitterFacing == Branch.SplitterFacing && rejudged.ExitDirection == Branch.ExitDirection;
            }
            return BeltRouteCellJudge.Judge(_world, StartCell) == BeltRouteCellBlockReason.None;
        }

        // カーソルか向きが変わったフレームだけ探索する
        // Searches only on frames where the cursor or heading changed
        public BeltRoutePlanResult Plan(Vector3Int endCell, BlockDirection endDirection, BeltConveyorHoldingBlock holding)
        {
            var query = new BeltRouteQuery(StartCell, StartDirection, IsBranch, endCell, endDirection, holding.AllowsSlopes, holding.IsSplitter);
            if (_lastQuery != null && _lastQuery.IsSameQuery(query)) return _lastResult;

            _lastQuery = query;
            _lastResult = _planner.Plan(query);
            return _lastResult;
        }
    }
}
```

- [ ] **Step 4: 端点判定・理由行・入力を直す**

`D/Session/BeltRouteCellCheck.cs`: `JudgeInGrid` を削除し、`JudgeInWorld(BlockGameObjectDataStore blocks, Vector3Int cell)` だけ残す（戻り値は `World.BeltRouteCellBlockReason`）。summary を「始点確定前の端点セルの可否を実ワールドから判定する。始点確定後は BeltRouteCellJudge が探索の世界から判定する」に。

`D/Session/BeltRouteTooltipKey.cs`: `NoPathLine()` の戻りを `new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltRouteNotFound)` に。

`D/Session/BeltRouteRoutingFrame.cs`: 引数から `BeltRouteStepPriority priority` を外し、`BeltRouteCellCheck.JudgeInGrid(session.Grid, endCell)` → `BeltRouteCellJudge.Judge(session.World, endCell)`（`using ...BeltConveyor.World;`）、`session.Plan(endCell, endDirection, priority, holding)` → `session.Plan(endCell, endDirection, holding)`。

`D/Session/BeltRouteKeyInput.cs`: `IsCyclePriorityPressed` を削除。summary を「2クリック設置が読むキー。R: 向き/分岐の出口の切替、Ctrl: 分岐、左クリック: 始点・終点の確定」に直す。`using UnityEngine;` が不要になれば消す。

- [ ] **Step 5: 設置システムを直す**

`D/BeltConveyorPlaceSystem.cs`:
- `_scanner` フィールドとコンストラクタの `_scanner = new BeltRouteWorldScanner(blockGameObjectDataStore);`、`_priority` フィールドと `Enable` の `_priority = ...` を削除。`using ...BeltConveyor.World;` が不要になれば消す。
- 購読を `blockGameObjectDataStore.OnBlockPlaced.Subscribe(_ => MarkObstaclesDirty()); blockGameObjectDataStore.OnBlockRemoved.Subscribe(_ => MarkObstaclesDirty());` にし、コメントを「ブロックが置かれた・撤去されたら次フレームで占有と辺を作り直す（箱が無いので位置で絞らない）」に。

```csharp
        private void MarkObstaclesDirty()
        {
            if (_session != null) _obstaclesDirty = true;
        }
```

- `UpdateStartSelection` の `BeltRouteSession.CreateAtCell(hit.Cell, _currentDirection, _scanner)` → `BeltRouteSession.CreateAtCell(hit.Cell, _currentDirection, _blocks)`。`UpdateBranchCandidate` の `BeltRouteSession.CreateBranch(branch, _scanner)` → `BeltRouteSession.CreateBranch(branch, _blocks)`。
- `UpdateRouting` の `if (BeltRouteKeyInput.IsCyclePriorityPressed) _priority = _priority.Next();` を削除。`BeltRouteRoutingFrame.Resolve(_session, hit, _priority, _holding, feedback, out var placeable)` → `BeltRouteRoutingFrame.Resolve(_session, hit, _holding, feedback, out var placeable)`。
- クラス summary の「カーソルのセルまでの最短経路を毎フレーム出し」→「カーソルのセルまでの経路を毎フレーム出し」。

- [ ] **Step 6: 辞書キーを整理する**

`Localization/localization.csv` の次の4行を削除:

```
ui.tooltip.placeBeltRouteBlockedByTerrain,...
ui.tooltip.placeBeltRouteBlockedByExistingBlock,...
ui.tooltip.placeBeltRouteBlockedByBoth,...
ui.tooltip.placeBeltRouteOutOfRange,...
```

同じ位置に1行を足す:

```
ui.tooltip.placeBeltRouteNotFound,No route found,No route found,経路が見つかりません,Keine Strecke gefunden,경로를 찾을 수 없습니다
```

生成物の更新:

```bash
python3 -c "import uuid;print('-'.join(f'{b:02X}' for b in uuid.uuid4().bytes))"
# 出た値で moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs の dummyText を書き換える
cd moorestech_web/webui && npm run gen:i18n && npx vitest run src/shared/i18n && cd ../..
uloop compile --project-path ./moorestech_client --force-recompile   # COMPILE_RESULT_UNKNOWN
uloop compile --project-path ./moorestech_client
```

- [ ] **Step 7: コンパイルしテストを回す**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0（`T/World/World.meta`・`BeltRouteGrid.cs.meta` の移動先を `git status` で確認）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaceSystem\."` → 全件 PASS（`BeltRouteWiringTest` が本番の `BeltRouteLiveWorld` を通る）
`grep -rn "StepPriority\|NoPathReason\|TerrainProfile\|WorldScanner\|IsCyclePriorityPressed\|BlockedByBoth\|BeltRouteEdgeTable\|BeltRouteGrid" moorestech_client/Assets/Scripts/Client.Game --include=*.cs` → 0 件
`grep -rn "OutOfRange" moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor --include=*.cs` → `ArgumentOutOfRangeException` 以外 0 件

- [ ] **Step 8: コミットする**

```bash
git checkout -- .moorestech-external-revisions.json moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git add Localization/localization.csv moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs moorestech_web/webui/src/shared/i18n/generated moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute
git commit -m "Task 3: feat(belt): 始点クリックの地形計測と範囲外を無くし、経路なしの理由を1つに畳み、C キーを廃止し、テストの箱をテスト側へ移す"
```

---

### Task 4: 遠距離と面ビットの試験・録画シナリオ・実測の記入

**Files:**
- Modify: `T/BeltRoutePlannerBasicTest.cs`
- Create: `T/World/BeltRouteFaceBitsTest.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-route-two-click-via-ui.cs`
- Modify: `docs/adr/0100-belt-route-unbounded-weighted-search.md`（Consequences の実測と、必要なら `MaxExpansions`）、`D/Route/Search/BeltRoutePlanner.cs`（必要なら `MaxExpansions`）
- Modify: `docs/playtest-dsl.md`（C キーの記述があれば削る）

- [ ] **Step 1: 旧箱を超える距離と囲まれた終点の試験を足す**

`T/BeltRoutePlannerBasicTest.cs` に追加（`FlatGrid()` の隣に `FlatGrid(int size)` を足す）:

```csharp
        private static BeltRouteGrid FlatGrid(int size)
        {
            var grid = new BeltRouteGrid(Vector3Int.zero, new Vector3Int(size, 3, size));
            for (var x = 0; x < size; x++)
            for (var z = 0; z < size; z++)
                grid.SetColumnFloor(x, z, 0);
            return grid;
        }

        // 旧箱（X64×Z64）を超える距離。開けた平地では重み付きでも最短のまま（ADR 0100）
        // Farther than the old 64x64 box; on open ground the weighted search still returns the shortest (ADR 0100)
        [Test]
        public void LongOpenRunBeyondTheOldBoxIsStraightAndShortest()
        {
            var grid = FlatGrid(120);
            var query = Query(new Vector3Int(2, 0, 2), BlockDirection.East, new Vector3Int(110, 0, 100), BlockDirection.North);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.DefaultRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "long run");
            Assert.AreEqual(207, result.Plan.Cells.Count);
            Assert.AreEqual(207 * BeltRouteCellKindExtension.StraightCost, result.Plan.TotalCost);
            Assert.IsTrue(result.Plan.Cells.All(cell => cell.Kind == BeltRouteCellKind.Straight));
        }

        // 囲まれた終点は経路なし（待ち行列が尽きるか展開数の上限で止まる）
        // An enclosed end yields no route (the queue drains or the expansion cap stops the search)
        [Test]
        public void EnclosedEndYieldsNoRoute()
        {
            var grid = FlatGrid(40);
            for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
            for (var y = 0; y < 3; y++)
                if (dx != 0 || dz != 0) grid.MarkBlockCell(new Vector3Int(30 + dx, y, 30 + dz));
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.DefaultRecoverySlack).Plan(Query(new Vector3Int(2, 0, 2), BlockDirection.East, new Vector3Int(30, 0, 30), BlockDirection.East));

            Assert.IsFalse(result.HasPlan);
        }
```

`T/World/BeltRouteFaceBitsTest.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute.World
{
    // 辺から面ビットを立てる1本（BeltRouteFaceBits.Mark）が、辺の高さ h ↔ セル c のビット(face, k) は h == c.y + k、かつ辺に接する2セルの両方、になることを確かめる
    // Checks the single face-bit raiser (BeltRouteFaceBits.Mark): an edge at height h sets bit (face, k) on cell c iff h == c.y + k, on both cells touching the edge
    public class BeltRouteFaceBitsTest
    {
        private static readonly Vector3Int Cell = new(5, 7, 9);

        [TestCase(BlockDirection.North, 0)]
        [TestCase(BlockDirection.East, 0)]
        [TestCase(BlockDirection.South, 0)]
        [TestCase(BlockDirection.West, 0)]
        [TestCase(BlockDirection.North, 1)]
        [TestCase(BlockDirection.East, 1)]
        [TestCase(BlockDirection.South, 1)]
        [TestCase(BlockDirection.West, 1)]
        public void EdgeOnAFaceRaisesTheBitOnBothTouchingCells(BlockDirection face, int heightOffset)
        {
            var bits = new Dictionary<Vector3Int, int>();
            BeltRouteFaceBits.Mark(BeltRouteEdge.FromFace(Cell, face, Cell.y + heightOffset), bits);

            var neighbour = Cell + BeltRouteDirection.Forward(face);
            Assert.AreEqual(BeltRouteFaceBits.FaceBit(face, heightOffset), bits[Cell] & BeltRouteFaceBits.FaceBit(face, heightOffset));
            Assert.AreEqual(BeltRouteFaceBits.FaceBit(BeltRouteDirection.Opposite(face), heightOffset), bits[neighbour] & BeltRouteFaceBits.FaceBit(BeltRouteDirection.Opposite(face), heightOffset));
            // 辺の高さと違う段にはこの面のビットは立たない
            // No bit for this face on the level that does not match the edge height
            var otherOffset = 1 - heightOffset;
            Assert.AreEqual(0, bits[Cell] & BeltRouteFaceBits.FaceBit(face, otherOffset));
        }
    }
}
```

- [ ] **Step 2: 録画シナリオの C キーのフェーズを遠距離と囲まれた終点のフェーズに置き換える**

`belt-route-two-click-via-ui.cs` のフェーズ3（`// 3. Cで別経路` から `await p.ExitToGameScreen();` まで）を次に置き換える。足場は中心 (0,30,0)・50×4×50（`PlaytestSetup.GroundCenter/GroundSize`）なので x,z は -25〜25 の内側。フェーズ1・2の設置物（x 2〜9、z 2〜12、チェストの壁 y32〜33）と視線が交わらないよう x=−20 列を使う。

```csharp
    // ---------------------------------------------------------------- 3. 旧箱を超える遠距離
    p.Note("フェーズ3: 足場の南端から北端近くまで（z 距離 44。旧箱の ±32 を超える）を2クリックで置く");
    p.WarpPlayer(new Vector3(-20f, 33.5f, -25f));
    await p.WaitSeconds(0.5f);
    var beltsBefore = CountBelts();
    await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(-20, 32, -22), BlockDirection.North));
    await p.ClickPlace();

    // 遠い終点を何か所か指して、経路探索中の最悪フレーム時間を実機で測る
    // Sweep a few far targets while routing to measure the worst frame time on the real build
    var worstFrameMs = 0f;
    foreach (var target in new[] { new Vector3Int(-14, 32, 22), new Vector3Int(-24, 32, 20), new Vector3Int(-5, 32, 15), new Vector3Int(-14, 32, 22) })
    {
        await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", target, BlockDirection.North));
        for (var frame = 0; frame < 3; frame++)
        {
            await UniTask.Yield();
            worstFrameMs = Mathf.Max(worstFrameMs, Time.unscaledDeltaTime * 1000f);
        }
    }
    p.Note($"開けた遠距離の経路探索中の最悪フレーム時間: {worstFrameMs:F1} ms");
    await p.Screenshot("03-long-route");
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(new Vector3Int(-14, 32, 22)) != null, 15f, "遠距離の終点が置かれる");
    p.Assert(CountBelts() - beltsBefore == 51, $"開けた平地では最短（44+6+1=51 セル）が置かれる（実際 {CountBelts() - beltsBefore}）");
    await p.WaitBlockGameObject(new Vector3Int(-14, 32, 22));
    await p.ExitToGameScreen();

    // ---------------------------------------------------------------- 3b. 囲まれた終点（探索上限の実測）
    p.Note("フェーズ3b: 高さ2のチェストの輪で囲んだ (-8,32,-10) を終点に狙うと「経路が見つかりません」が出る。探索上限に達するときの最悪フレーム時間を測る");
    for (var dx = -1; dx <= 1; dx++)
    for (var dz = -1; dz <= 1; dz++)
    {
        if (dx == 0 && dz == 0) continue;
        await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(-8 + dx, 32, -10 + dz), BlockDirection.North);
        await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(-8 + dx, 33, -10 + dz), BlockDirection.North);
    }
    await p.WaitBlockGameObject(new Vector3Int(-7, 33, -9));
    await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(-20, 32, -20), BlockDirection.North));
    await p.ClickPlace();
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(-8, 32, -10), BlockDirection.North));
    var enclosedWorstMs = 0f;
    for (var frame = 0; frame < 5; frame++)
    {
        await UniTask.Yield();
        enclosedWorstMs = Mathf.Max(enclosedWorstMs, Time.unscaledDeltaTime * 1000f);
    }
    p.Note($"囲まれた終点の経路探索中の最悪フレーム時間: {enclosedWorstMs:F1} ms");
    p.Assert(HasTooltipKey("ui.tooltip.placeBeltRouteNotFound"), "囲まれた終点では「経路が見つかりません」の理由行が出る");
    await p.Screenshot("04-enclosed-not-found");
    await p.ExitToGameScreen();
```

`HasTooltipKey` は `belt-route-branch-via-ui.cs` の `Tooltip()`/`HasKey()` と同じ実装を `#region Internal` に足す（`ClientDIContext.DIContainer.DIContainerResolver.Resolve<IMouseCursorTooltip>()` の `GetPresentation().Lines.Any(line => line.Key.Key == key)`。必要な using: `Client.Game.InGame.Context`、`Client.Game.InGame.UI.Tooltip`、`VContainer`）。チェストの直置きがそのセルに置けない（足場の上の y=33 に置けない等）場合は、フェーズ3b を `PlaceBlockDirect` の結果を見て調整し、報告に書く。

- `PreviewPositions()` が他で使われていなければ `#region Internal` から削除する。
- ファイル冒頭の解説コメントの「C で同コストの別経路に切り替わること」を「旧箱を超える遠距離の経路が置けること・囲まれた終点で経路なしになること」に直す（日本語ひとかたまり→英語ひとかたまり）。
- 後続フェーズ（4: 荷物を流す）の座標が新フェーズ3・3b の設置物と重ならないことを確かめる。
- `docs/playtest-dsl.md` に C キーの記述があれば削る（`grep -n "Key.C\|Cキー\|C キー" docs/playtest-dsl.md`）。

- [ ] **Step 3: コンパイル・テスト・録画シナリオ**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaceSystem\."` → 全件 PASS
録画シナリオ: `.agents/skills/unity-playmode-recorded-playtest/SKILL.md` の手順で `belt-route-two-click-via-ui` と `belt-route-branch-via-ui` の2本を回す（Editor を前面に。環境要因で回せなければ生のエラーを報告ファイルに書いて DONE_WITH_CONCERNS）。
Expected: 両方とも全フェーズ PASS。2つの「最悪フレーム時間」の Note を報告ファイルへ写す。

- [ ] **Step 4: 実測して ADR に記入し、必要なら上限を下げる**

手元 EditMode で、次の3つの探索時間と展開数を測る。測り方: 一時的なテストファイル `T/TmpBeltRouteTiming.cs` に `System.Diagnostics.Stopwatch` で `BeltRoutePlanner.Plan` を10回回して中央値と、`Debug.Log` された `expansions:` を記録するテストを書き、`uloop run-tests --filter-type class` で実行し、終わったら **ファイルと .meta を削除する**（コミットしない。壁時計の閾値テストを置かない裁定 2026-11-07）。
1. 開けた平地 120×3×120 で (2,0,2)→(110,0,100)（R1 と同じ）
2. 同じ平地の x=60 列に高さ3の壁（z=0〜110）を置き、(2,0,2)→(110,0,100)（遠回り）
3. 囲まれた終点（`EnclosedEndYieldsNoRoute` と同じ配置）

`docs/adr/0100-belt-route-unbounded-weighted-search.md` の Consequences「実測（2026-11-09。計画 Task 4 で記入）」の行を、3つの値（ms）と展開数、録画シナリオの2つの最悪フレーム時間で書き換える。**録画の囲まれた終点で最悪フレーム時間が 150 ms を超えたら**、`BeltRoutePlanner.MaxExpansions` を 100 ms 前後に収まる値へ下げ（例: 10000）、ADR 本文の「`MaxExpansions` = 20,000」と計画のこの行を同じ値に直し、測り直した値を記す。

- [ ] **Step 5: コミットする**

```bash
git checkout -- .moorestech-external-revisions.json moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git add moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute .agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-route-two-click-via-ui.cs docs/adr/0100-belt-route-unbounded-weighted-search.md docs/playtest-dsl.md moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/Search/BeltRoutePlanner.cs
git commit -m "Task 4: test(belt): 旧箱を超える遠距離・囲まれた終点・面ビットの試験を足し、録画シナリオの C フェーズを遠距離と探索上限の実測に置き換え、実測を ADR に記す"
```

---

### Task 5（閉じタスク1）: 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

レビュー指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、Task 4 の録画シナリオ2本を反映後のバイナリで再実施してから完了とする。

### Task 6（閉じタスク2）: セッション終了可能状態にすること

ユーザー指示 2026-11-09「PRまだつくらないで　このブランチだけでまずは仕事したいので」により PR は作らない。全作業をコミットし、`feature/belt-two-click-route` を origin へ push（バックアップ）して終える。master へ戻すときは Revert（PR 1617）の取り消しコミットが要ることを報告に書く。

---

## 判断記録（ADR）

- 設計の正: `docs/adr/0100-belt-route-unbounded-weighted-search.md`、`.decisions/2026-11-09-ベルト経路は探索箱を持たず重み付き探索で上限なしに引きCキーは廃止する.md`。
- D1 箱と高さ上限を無くし、触ったセルだけその場で調べる。出所: ユーザー裁定 2026-11-09 原文「ダメです。最短じゃなくていいので障害物よけてy方向も無限でできるように横方向も、なんかかんがえられない」。
- D2 最短は開けた場所だけ保証し、障害物の周りは遠回りを許す（重み W=2）。出所: ユーザー裁定 2026-11-09 原文「2ok」（提示文「開けた場所では最短、障害物の周りは多少遠回りでよい」への回答）。W=2 の値は agent前提（一般的な重み付き A* の値。遠回りの上限は最短の2倍）。
- D3 経路なしの理由は「経路が見つかりません」1つ。出所: ユーザー裁定 2026-11-09 原文「1 ok」（提示文「経路なしの理由は『見つかりません』1つに畳んでよいか」への回答）。開発者向けには出どころと展開数をログに出す（agent前提: fail-closed の経路はログへ出す規約）。
- D4 C キーの同コスト候補送りを廃止。出所: ユーザー裁定 2026-11-09 原文「Cキー一旦撤廃で」。`HybridInput` の C 対応表は残す（agent前提: 他機能が使えるよう）。
- D5 探索量の上限 `MaxExpansions = 20000`、組み立て失敗時の追加展開 4000。出所: agent前提（設計検査の見積もり: 1展開あたり辞書参照十数回で約 100 ms。Task 4 の実機計測で 150 ms を超えたら下げ、ADR と計画を同じ値に直す）。
- D6 世界の抽象 `IBeltRouteWorld` は本番 `BeltRouteLiveWorld` とテスト `BeltRouteGrid` の2実装、`IBeltRouteEdgeSink` は本番・分岐判定の `BeltRouteNeighbourEdges`・テストの3実装。出所: agent前提（受益者が複数。前例: `BeltRouteBlockEdgeScanner.Write(master, blockId, position, grid)` が「サーバーのブロックからも同じ辺を書けるよう」に分かれている）。interface は `World/` に置く（`Route/` のファイル数を途中のタスクでも 10 以下に保つ。agent前提）。
- D7 テストの世界（`BeltRouteGrid`）はテストアセンブリへ移し、箱の外を占有扱いにして閉じる。本番の分岐判定は辺の有無だけの `BeltRouteNeighbourEdges`。出所: agent前提（AGENTS.md「デバッグ/テスト専用publicをプロダクションに残さない」。設計検査 finding 1）。
- D8 組み立て（`BeltRouteRecovery`）は変えず、探索の g を予算にする。探索がたどった親の列は枝刈りを必ず通る（best(親) ≤ best(子) − 一歩のコスト）。親の列が同じセルの再使用や自分の坂の真上で弾かれる配置だけが追加の展開と余裕に頼る。出所: agent前提（ADR 0097 の2段構えを保つ。設計検査の備考1で確認）。
- D9 参照ソルバ突き合わせは「存在」と「コスト ≤ 参照×W＋余裕」に変え、本番の余裕だけで回す（余裕なしの比較は重み付きでは意味を失う）。出所: agent前提（設計検査の備考1: 余裕なしの見落とし上限は通らない恐れ）。
- D10 `EndAboveStartDetoursInsteadOfRisingUnderTheEnd` と `CellReceivingExistingOutputIsAvoided` の厳密なコスト・セル数の Assert を上限に緩める。出所: D2 の帰結（agent前提）。
- D11 ランタイム挙動（経路・入力・文言）に触れるので録画シナリオ2本を回し、囲まれた終点で探索上限の実機時間を測る。出所: moores-writing-plans の規定（agent前提: 設計検査の備考「最悪ケースが測られない」）。
- D12 Task 2 の `BeltRoutePlanner.ReplaceWorld`・`NoPathLine()` の暫定キー・`BeltRouteCellCheck.JudgeInGrid` の暫定委譲は Task 3 で消す（同一コンパイル単位をタスクに分けるための一時 API）。出所: agent前提。
- D13 作業は本体ワーキングツリーの `feature/belt-two-click-route`、PR は作らない。出所: ユーザー裁定 2026-11-09 原文「PRまだつくらないで　このブランチだけでまずは仕事したいので」。
- D14 Codex 独立質問（codex-audit）は省略。出所: ユーザー裁定 2026-11-09（L字の回で「省略して進める」を選択。環境に codex-audit が無いのは同じ）。
- D15 面ビットを辺から立てる計算は `BeltRouteFaceBits.Mark` の1本にし、本番・テスト両方の世界が書き込み時に辞書へ立てる（1問い合わせ1参照）。出所: agent前提（設計検査 Phase 2.6 強1・第3バケツ1）。
- D16 `BeltRouteSearch.Run` は `BeltRouteSearchOutcome`（Reached/Exhausted/CapReached）を返し、`BeltRoutePlanner` が経路なしの出どころを1行ログに出す。出所: agent前提（設計検査 finding 2・Phase 2.6 弱）。
- D17 `NoGround`/`Unreached` の番兵値は既存の形を引き継いで据え置く。出所: agent前提（設計検査 Phase 2.6 強。型化は本PR外）。
- D18 セルが置けない理由の判定は `World/BeltRouteCellJudge.Judge` の1本にし、探索規則の `IsCellFree` とセッションの端点判定が同じ関数を通る。出所: agent前提（設計検査 Phase 2.6 弱・検査6）。
