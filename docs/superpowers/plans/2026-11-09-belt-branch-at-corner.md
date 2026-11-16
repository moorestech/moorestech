# Belt Branch At Corner Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Ctrl+クリックの分岐化を、直線のベルトだけでなく L字の角のベルトでも成立させる。分岐器は「アイテムが入ってくる向き」で置き、出口候補は既存の続きが使う出口を除いた2つにする。

**Architecture:** 分岐の向き・出口の決定を、世界に触れない純粋な幾何 `BeltRouteBranchGeometry`（入力: 周囲の入出力辺を写した1セルの `BeltRouteGrid`・ベルトの向き・R で切り替えた出口を使うか）へ切り出す。`BeltRouteBranchOrigin.Judge` は辺を集めて幾何へ渡すだけになる。設置側（`BeltRoutePlaceInfoBuilder` / `BeltRouteSession` / `BeltConveyorPlaceSystem`）は「横」ではなく幾何が返した `SplitterFacing` と `ExitDirection` を使う。探索（`BeltRoutePlanner` 以下）とサーバーは変えない。

**Tech Stack:** Unity 6 / C# / NUnit（EditMode）/ uloop / プレイテストDSL（`Client.Playtest`）/ 録画シナリオ（`unity-playmode-recorded-playtest`）

## Requirements

- R1. L字の角のベルト（左右どちらか一方の面だけから受け、背面からは受けていない直線ブロック）を Ctrl+クリックすると分岐器に置き換わり、分岐器の向きは流し込んでくる隣の向きになる。受け入れ: `BeltRouteBranchGeometryTest` で、北向きのベルトが西面から受けているとき `SplitterFacing == East`。
- R2. 角の既定の出口は正面（入ってくる向きの先）、R で空いている横へ切り替わる。受け入れ: 同テストで `useAlternateExit=false` → `BranchExit == East`、`true` → `BranchExit == South`（北向きベルト・西から受ける場合）。
- R3. 直線のベルト（横から受けていない）は従来どおり、分岐器の向きはベルトと同じ、既定の出口は右、R で左。受け入れ: 同テストで東向き・辺なしのとき `SplitterFacing == East`、既定 `BranchExit == South`、切替で `North`。
- R4. 2方向以上から受けているベルト（背面+横、両横）は分岐不可で、理由は「このベルトは2方向以上からアイテムを受けています」（`ui.tooltip.placeBeltBranchMultipleInputs`）。受け入れ: 同テストで `MultipleInputs`。録画シナリオのフェーズ4（背面と横から受けるベルト）がこのキーを見る。
- R5. 使わない出口の隣に受け手がいるときは分岐不可で、理由は「使わない出口の隣に受け手があります」（`ui.tooltip.placeBeltBranchUnusedExitHasReceiver`）。受け入れ: 同テストで角の南に受け手があるとき既定（出口=正面）は `UnusedExitHasReceiver`、切替（出口=南）は `None`。
- R6. 置き換え後、既存の続き（角の先のベルト）へも新しい分岐へもアイテムが流れる。受け入れ: `BeltRouteConveyanceTest.CornerBranchSplitsItemsToBothChests` がテスト用サーバーで両方のチェストに届くことを確かめる。
- R7. Ctrl で狙っている間のプレビューは、分岐器（新しい向き）と、選んだ出口の先の最初のセルを見せる。受け入れ: `BeltRoutePlaceInfoBuilder.BuildBranchCandidate` が2件を返し、2件目の位置が `Cell + Forward(ExitDirection)`・向きが `ExitDirection`。
- R8. 録画シナリオ `belt-route-branch-via-ui.cs` に角の分岐のフェーズを足し、分岐器の向き・経路のセル・両方へ流れることを実機で確かめる。プレイテストDSL `BranchPlaceViaUi` は「右か左か」ではなく「R で切り替えた出口を使うか」と「経路が出る向き」を受ける。
- やらないこと: 分岐の最初のセルが横の出口の直後で曲がると歯車の回転が繋がらない不具合（別 PR。ユーザー裁定 2026-11-09）。探索の規則・サーバーの接続規則・マスタの変更。X64×Z64 と高さの範囲制限の撤廃（次の依頼）。

## Global Constraints

- AGENTS.md: 1ファイル200行以下・1ディレクトリ10ファイル以下・partial 禁止・`Func<>` 禁止・デフォルト引数禁止・try-catch は境界のみ・日本語/英語の2行コメント・.meta は手で作らない（Unity が生成したものをコミット）・.cs 変更後は必ず `uloop compile --project-path ./moorestech_client`。
- 無音の縮退禁止の例外: 分岐不可の理由はツールチップの理由行として利用者に出るので、`Judge`/`Resolve` の early return はログを出さず `// silent-ok:` コメントで理由行に出ることを書く（既存 `BeltRouteBranchOrigin.Judge` の流儀）。
- `.moorestech-external-revisions.json` と `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の Editor 由来の差分はコミットしない（`git checkout --` で戻す）。`.mat` の Editor 由来差分も同様。
- localization.csv を変えたら `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` の `dummyText` を新しい値にし、`moorestech_web/webui` で `npm run gen:i18n` を回して生成物（`src/shared/i18n/generated/localizationKeys.ts` 等）を一緒にコミットする。`uloop compile --project-path ./moorestech_client --force-recompile` を1回（COMPILE_RESULT_UNKNOWN が返る）→ 通常の compile。
- 作業場所: 本体ワーキングツリーの `feature/belt-two-click-route`（ユーザー指示 2026-11-09「引き続きlocalでこのブランチに実装していきたい」。PR 1616 がこのブランチを指す）。
- ADR: `docs/adr/0097-belt-two-click-shortest-route-placement.md`「既存ベルトからの分岐」節（2026-11-09 改訂）が正。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反1件・修正1件 / `BeltRouteBranchOrigin` の `Shape` 公開＋素通し2本の二重保持を、形を private に持ち2値だけ公開する形へ畳んだ。死活表で死ぬ操作なし
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強1・弱2・第3バケツ1 / 強: `Resolve` の `out shape` の default と理由 enum の併存 → 既存 `Judge`/`BeltRouteCellCheck` の「理由 enum＋out」の流儀に揃えて据え置き（D12）。弱: 流入分類の bool 群（据え置き）、ADR「直線ベルト」の語（ADR に補足）。第3バケツ: 合流セル判定（`BeltSegmentKind.Merge`）との概念重複は入力源が違うため本PR外。備考の Task 3 の誤り（角自身の入力辺が受け手に見える）は `BuildNeighbourEdges` で修正

---

## File Structure

`Session/` は現在10ファイルで上限。分岐に関する3ファイルを `Session/Branch/` に置く（既存2本は `git mv` で .meta ごと移す）。

- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/Branch/BeltRouteBranchGeometry.cs` — 理由 enum・`BeltRouteBranchShape`・向きと出口を決める純粋な幾何
- Move+Modify: `.../Session/BeltRouteBranchOrigin.cs` → `.../Session/Branch/BeltRouteBranchOrigin.cs` — 辺を集めて幾何へ渡す。形は private に持ち `SplitterFacing`/`ExitDirection` だけ公開
- Move: `.../Session/BeltRouteBranchReplaceSender.cs` → `.../Session/Branch/BeltRouteBranchReplaceSender.cs` — 名前空間だけ変更
- Modify: `.../Session/BeltRoutePlaceInfoBuilder.cs` — `SplitterInfo` が `SplitterFacing` を使う。`BuildBranchCandidate` を追加
- Modify: `.../Session/BeltRouteSession.cs` — `CreateBranch` が `ExitDirection` を使う
- Modify: `.../Session/BeltRouteTooltipKey.cs` — 理由→辞書キーの対応を新 enum に合わせる
- Modify: `.../Session/BeltRouteKeyInput.cs` — summary コメントの R の説明
- Modify: `.../BeltConveyorPlaceSystem.cs` — `_branchSideIsRight` → `_branchUseAlternateExit`、候補プレビューは `BuildBranchCandidate`
- Modify: `Localization/localization.csv` — キー2行の置き換え。生成物: `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`、`moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（gen:i18n が書く他の生成物も含む）
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteBranchGeometryTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteConveyanceWorld.cs` — 搬送試験が共有するテスト用サーバーの補助（既存の private 補助を移す）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteConveyanceTest.cs` — 角の分岐の搬送試験を追加
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/Operations/PlaytestBeltRouteOps.cs`、`moorestech_client/Assets/Scripts/Client.Playtest/PlaytestDriver.cs` — `BranchPlaceViaUi` の引数
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-route-branch-via-ui.cs`、`.agents/skills/unity-playmode-recorded-playtest/references/write-scenario.md`、`docs/playtest-dsl.md` — DSL の呼び出しと角のフェーズ

## Interfaces（タスク間で共有する名前）

```csharp
namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch
{
    public enum BeltRouteBranchBlockReason { None, NotStraightBelt, NoSplitter, MultipleInputs, UnusedExitHasReceiver }

    public readonly struct BeltRouteBranchShape
    {
        public readonly BlockDirection SplitterFacing; // アイテムが入ってくる向き
        public readonly BlockDirection BranchExit;     // 新しい経路が出る出口
        public readonly BlockDirection UnusedExit;     // 残った出口（受け手がいてはいけない）
    }

    public static class BeltRouteBranchGeometry
    {
        public static BeltRouteBranchBlockReason Resolve(BeltRouteGrid edges, Vector3Int cell, BlockDirection beltFacing, bool useAlternateExit, out BeltRouteBranchShape shape);
    }

    public sealed class BeltRouteBranchOrigin
    {
        public readonly BlockGameObject Belt;
        public readonly BlockId SplitterBlockId;
        // 形（BeltRouteBranchShape）は private に持ち、利用者が読む2値だけを式本体で公開する
        public Vector3Int Cell => Belt.BlockPosInfo.OriginalPos;
        public BlockDirection SplitterFacing => _shape.SplitterFacing;
        public BlockDirection ExitDirection => _shape.BranchExit;
        public static BeltRouteBranchBlockReason Judge(BlockGameObjectDataStore blocks, BlockGameObject belt, BeltConveyorHoldingBlock holding, bool useAlternateExit, out BeltRouteBranchOrigin origin);
    }
}

// Session
public static List<PlaceInfo> BeltRoutePlaceInfoBuilder.BuildBranchCandidate(BeltRouteBranchOrigin branch, BeltConveyorHoldingBlock holding);

// Client.Playtest
public static UniTask PlaytestBeltRouteOps.BranchPlaceViaUi(PlaytestDriver driver, string blockName, Vector3Int beltCell, bool useAlternateExit, BlockDirection exitDirection, Vector3Int toOrigin, BlockDirection endDirection);
public static UniTask PlaytestBeltRouteOps.BranchStart(PlaytestDriver driver, Vector3Int beltCell, bool useAlternateExit);
```

---

### Task 1: 分岐の向きと出口を決める幾何（BeltRouteBranchGeometry）とそのテスト

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/Branch/BeltRouteBranchGeometry.cs`
- Move+Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/BeltRouteBranchOrigin.cs` → `.../Session/Branch/BeltRouteBranchOrigin.cs`
- Move: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/BeltRouteBranchReplaceSender.cs` → `.../Session/Branch/BeltRouteBranchReplaceSender.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteBranchGeometryTest.cs`

- [ ] **Step 1: ディレクトリを作り、既存2本を .meta ごと移す**

```bash
cd moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session
mkdir Branch
git mv BeltRouteBranchOrigin.cs Branch/BeltRouteBranchOrigin.cs
git mv BeltRouteBranchOrigin.cs.meta Branch/BeltRouteBranchOrigin.cs.meta
git mv BeltRouteBranchReplaceSender.cs Branch/BeltRouteBranchReplaceSender.cs
git mv BeltRouteBranchReplaceSender.cs.meta Branch/BeltRouteBranchReplaceSender.cs.meta
```

`Branch.meta`（フォルダの .meta）と新規 .cs の .meta は、Editor が起動中なら次の `uloop compile` で生成される。生成されたら `git add` する。手で作らない。

- [ ] **Step 2: 移した2本の名前空間を `...Session.Branch` に変える**

`BeltRouteBranchReplaceSender.cs` は `namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session` → `namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch` のみ。`BeltRouteBranchOrigin.cs` は Step 4 で全面的に書き換える。

- [ ] **Step 3: 幾何を書く**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/Branch/BeltRouteBranchGeometry.cs`:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch
{
    /// <summary>
    ///     既存のベルトを分岐の始点にできない理由
    ///     Why an existing belt cannot become a branch start
    /// </summary>
    public enum BeltRouteBranchBlockReason
    {
        None,
        NotStraightBelt,
        NoSplitter,
        MultipleInputs,
        UnusedExitHasReceiver,
    }

    /// <summary>
    ///     分岐器の向きと出口の割り当て。向きはアイテムが入ってくる向き、出口は新しい経路が出るものと残り
    ///     The splitter's facing and exit assignment: the facing is where items come in from, the exits are the branch's and the remaining one
    /// </summary>
    public readonly struct BeltRouteBranchShape
    {
        public readonly BlockDirection SplitterFacing;
        public readonly BlockDirection BranchExit;
        public readonly BlockDirection UnusedExit;

        public BeltRouteBranchShape(BlockDirection splitterFacing, BlockDirection branchExit, BlockDirection unusedExit)
        {
            SplitterFacing = splitterFacing;
            BranchExit = branchExit;
            UnusedExit = unusedExit;
        }
    }

    /// <summary>
    ///     周囲の入出力辺だけから分岐器の向きと出口を決める。世界には触れない
    ///     Decides the splitter's facing and exits from the surrounding port edges alone; never touches the world
    /// </summary>
    public static class BeltRouteBranchGeometry
    {
        // edges は対象ベルト自身を除いた隣の辺だけを写した箱（辺は向きを持たないので、自分の入力口を混ぜると受け手と区別できない）
        // edges holds only the neighbours' edges, never the belt's own (an edge has no direction, so the belt's own input port would read as a receiver)
        // 分岐器の入口は背面1つ。背面・左・右のどの面から受けているかを数え、2方向以上なら置けない
        // A splitter has one back entry; count which of the back, left and right faces feed the belt, and two or more cannot be placed
        public static BeltRouteBranchBlockReason Resolve(BeltRouteGrid edges, Vector3Int cell, BlockDirection beltFacing, bool useAlternateExit, out BeltRouteBranchShape shape)
        {
            shape = default;
            var fedFromBack = IsFed(edges, cell, BeltRouteDirection.Opposite(beltFacing));
            var fedFromLeft = IsFed(edges, cell, BeltRouteDirection.Left(beltFacing));
            var fedFromRight = IsFed(edges, cell, BeltRouteDirection.Right(beltFacing));
            var sideFeeds = (fedFromLeft ? 1 : 0) + (fedFromRight ? 1 : 0);
            // silent-ok: 理由行「2方向以上からアイテムを受けています」で分岐不可と分かる
            if (sideFeeds == 2 || (fedFromBack && sideFeeds == 1)) return BeltRouteBranchBlockReason.MultipleInputs;

            // 直線は入ってくる向きがベルトの向き。角は横の面から受けるので、入ってくる向きはその面の反対
            // A straight belt's inflow is its facing; a corner is fed through a side face, so the inflow is that face's opposite
            var isCorner = sideFeeds == 1;
            var flow = isCorner ? (fedFromLeft ? BeltRouteDirection.Right(beltFacing) : BeltRouteDirection.Left(beltFacing)) : beltFacing;

            // 既存の続きはベルトの向きの出口を使う。残り2つのうち、直線は右が既定、角は正面が既定（ユーザー裁定 2026-11-09）
            // The existing continuation uses the exit in the belt's facing; of the other two, a straight belt defaults to right and a corner to the front (user decision 2026-11-09)
            // 角で空いている横は元のベルトの背面（入ってくる面でも続きの面でもない残りの面）
            // A corner's free side is the original belt's back, the one face that is neither the inflow nor the continuation
            var primary = isCorner ? flow : BeltRouteDirection.Right(flow);
            var alternate = isCorner ? BeltRouteDirection.Opposite(beltFacing) : BeltRouteDirection.Left(flow);
            var branchExit = useAlternateExit ? alternate : primary;
            var unusedExit = useAlternateExit ? primary : alternate;

            // 分岐器は使わない出口にも流すので、その先に受け手がいてはいけない
            // The splitter also emits through its unused exit, so nothing may receive there
            // silent-ok: 理由行「使わない出口の隣に受け手があります」で分岐不可と分かる
            if (edges.HasInputEdge(BeltRouteEdge.FromFace(cell, unusedExit, cell.y))) return BeltRouteBranchBlockReason.UnusedExitHasReceiver;

            shape = new BeltRouteBranchShape(flow, branchExit, unusedExit);
            return BeltRouteBranchBlockReason.None;
        }

        private static bool IsFed(BeltRouteGrid edges, Vector3Int cell, BlockDirection face)
        {
            return edges.HasOutputEdge(BeltRouteEdge.FromFace(cell, face, cell.y));
        }
    }
}
```

- [ ] **Step 4: `BeltRouteBranchOrigin` を幾何へ委譲する形に書き換える**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/Branch/BeltRouteBranchOrigin.cs` 全文:

```csharp
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch
{
    /// <summary>
    ///     分岐の始点。既存ベルトは同じファミリーの分岐器へ置き換わり、経路は幾何が選んだ出口から出る
    ///     A branch start: the existing belt becomes the family's splitter and the route leaves through the exit the geometry chose
    /// </summary>
    public sealed class BeltRouteBranchOrigin
    {
        public readonly BlockGameObject Belt;
        public readonly BlockId SplitterBlockId;
        private readonly BeltRouteBranchShape _shape;

        private BeltRouteBranchOrigin(BlockGameObject belt, BlockId splitterBlockId, BeltRouteBranchShape shape)
        {
            Belt = belt;
            SplitterBlockId = splitterBlockId;
            _shape = shape;
        }

        public Vector3Int Cell => Belt.BlockPosInfo.OriginalPos;
        public BlockDirection SplitterFacing => _shape.SplitterFacing;
        public BlockDirection ExitDirection => _shape.BranchExit;

        // 周囲の既存ブロックの入出力辺だけを1セルの箱に写し、向きと出口の判断は幾何に任せる
        // Copies only the surrounding blocks' port edges into a one-cell box and leaves the facing and exit decision to the geometry
        public static BeltRouteBranchBlockReason Judge(BlockGameObjectDataStore blocks, BlockGameObject belt, BeltConveyorHoldingBlock holding, bool useAlternateExit, out BeltRouteBranchOrigin origin)
        {
            origin = null;
            // silent-ok: 理由行「分岐は同じ種類の直線ベルトからだけ作れます」で分岐不可と分かる
            if (holding.IsSplitter || !IsStraightBeltOfFamily(belt, holding)) return BeltRouteBranchBlockReason.NotStraightBelt;
            // silent-ok: 候補0件や曖昧なマスタはBeltSplitterCounterpartがログに残す / 理由行で分岐不可と分かる
            if (!BeltSplitterCounterpart.TryGetSplitterBlockId(belt.BlockId, out var splitterBlockId)) return BeltRouteBranchBlockReason.NoSplitter;

            var cell = belt.BlockPosInfo.OriginalPos;
            var edges = new BeltRouteGrid(cell, Vector3Int.one);
            foreach (var neighbour in blocks.BlockGameObjectDictionary.Values)
            {
                if (ReferenceEquals(neighbour, belt) || !IsNeighbour(neighbour, cell)) continue;
                BeltRouteBlockEdgeScanner.Write(neighbour, edges);
            }

            var reason = BeltRouteBranchGeometry.Resolve(edges, cell, belt.BlockPosInfo.BlockDirection, useAlternateExit, out var shape);
            // silent-ok: 幾何が返した理由はそのまま理由行になる
            if (reason != BeltRouteBranchBlockReason.None) return reason;

            origin = new BeltRouteBranchOrigin(belt, splitterBlockId, shape);
            return BeltRouteBranchBlockReason.None;
        }

        private static bool IsStraightBeltOfFamily(BlockGameObject belt, BeltConveyorHoldingBlock holding)
        {
            if (!BeltRouteBlockEdgeScanner.IsBelt(belt.BlockMasterElement)) return false;
            if (!holding.IsSameFamily(belt.BlockId)) return false;
            return belt.BlockId == holding.Family.StraightBlockId;
        }

        // 大きな機械の口も拾えるよう、原点ではなく占有範囲が2セル以内に掛かるものを隣とみなす
        // Treat a block as a neighbour when its footprint, not only its origin, comes within two cells, so large machines' ports are caught
        private static bool IsNeighbour(BlockGameObject block, Vector3Int cell)
        {
            const int reach = 2;
            var min = block.BlockPosInfo.MinPos;
            var max = block.BlockPosInfo.MaxPos;
            return min.x - reach <= cell.x && cell.x <= max.x + reach &&
                   min.y - reach <= cell.y && cell.y <= max.y + reach &&
                   min.z - reach <= cell.z && cell.z <= max.z + reach;
        }
    }
}
```

- [ ] **Step 5: 幾何のテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteBranchGeometryTest.cs`:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 周囲の入出力辺だけから分岐器の向きと出口が決まることを確かめる（ADR 0097「既存ベルトからの分岐」2026-11-09）
    // Checks that the splitter's facing and exits follow from the surrounding port edges alone (ADR 0097, branch section, 2026-11-09)
    public class BeltRouteBranchGeometryTest
    {
        private static readonly Vector3Int Cell = new(10, 3, 10);

        [Test]
        public void StraightBeltKeepsItsFacingAndDefaultsToRight()
        {
            var edges = NewEdges();
            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.East, false, out var shape));
            Assert.AreEqual(BlockDirection.East, shape.SplitterFacing);
            Assert.AreEqual(BlockDirection.South, shape.BranchExit);
            Assert.AreEqual(BlockDirection.North, shape.UnusedExit);

            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.East, true, out shape));
            Assert.AreEqual(BlockDirection.North, shape.BranchExit);
            Assert.AreEqual(BlockDirection.South, shape.UnusedExit);
        }

        [Test]
        public void StraightBeltFedFromBackIsStillStraight()
        {
            var edges = NewEdges();
            AddFeeder(edges, BlockDirection.West);
            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.East, false, out var shape));
            Assert.AreEqual(BlockDirection.East, shape.SplitterFacing);
            Assert.AreEqual(BlockDirection.South, shape.BranchExit);
        }

        // 北向きのベルトが西から受けている角: 分岐器は東向き、既定の出口は正面（東）、R で空いている横（南）
        // A north-facing belt fed from the west: the splitter faces east, the default exit is the front (east), R switches to the free side (south)
        [Test]
        public void CornerFedFromLeftFacesTheFeederAndDefaultsToFront()
        {
            var edges = NewEdges();
            AddFeeder(edges, BlockDirection.West);
            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.North, false, out var shape));
            Assert.AreEqual(BlockDirection.East, shape.SplitterFacing);
            Assert.AreEqual(BlockDirection.East, shape.BranchExit);
            Assert.AreEqual(BlockDirection.South, shape.UnusedExit);

            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.North, true, out shape));
            Assert.AreEqual(BlockDirection.East, shape.SplitterFacing);
            Assert.AreEqual(BlockDirection.South, shape.BranchExit);
            Assert.AreEqual(BlockDirection.East, shape.UnusedExit);
        }

        [Test]
        public void CornerFedFromRightFacesTheFeeder()
        {
            var edges = NewEdges();
            AddFeeder(edges, BlockDirection.East);
            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.North, false, out var shape));
            Assert.AreEqual(BlockDirection.West, shape.SplitterFacing);
            Assert.AreEqual(BlockDirection.West, shape.BranchExit);
            Assert.AreEqual(BlockDirection.South, shape.UnusedExit);
        }

        [Test]
        public void BackAndSideFeedsCannotBranch()
        {
            var edges = NewEdges();
            AddFeeder(edges, BlockDirection.South);
            AddFeeder(edges, BlockDirection.West);
            Assert.AreEqual(BeltRouteBranchBlockReason.MultipleInputs, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.North, false, out _));
        }

        [Test]
        public void BothSideFeedsCannotBranch()
        {
            var edges = NewEdges();
            AddFeeder(edges, BlockDirection.West);
            AddFeeder(edges, BlockDirection.East);
            Assert.AreEqual(BeltRouteBranchBlockReason.MultipleInputs, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.North, false, out _));
        }

        // 使わない出口の先に受け手がいると分岐不可。出口を切り替えて受け手側を分岐にすれば成立する
        // A receiver beyond the unused exit blocks the branch; switching the exit so the receiver's side becomes the branch makes it valid
        [Test]
        public void ReceiverBeyondUnusedExitBlocksOnlyThatChoice()
        {
            var edges = NewEdges();
            AddFeeder(edges, BlockDirection.West);
            AddReceiver(edges, BlockDirection.South);
            Assert.AreEqual(BeltRouteBranchBlockReason.UnusedExitHasReceiver, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.North, false, out _));
            Assert.AreEqual(BeltRouteBranchBlockReason.None, BeltRouteBranchGeometry.Resolve(edges, Cell, BlockDirection.North, true, out var shape));
            Assert.AreEqual(BlockDirection.South, shape.BranchExit);
        }

        private static BeltRouteGrid NewEdges()
        {
            return new BeltRouteGrid(Cell, Vector3Int.one);
        }

        // 隣の出力口がこのセルの face 面へ向いている
        // A neighbour's output port aimed at this cell's given face
        private static void AddFeeder(BeltRouteGrid edges, BlockDirection face)
        {
            edges.AddOutputEdge(BeltRouteEdge.FromFace(Cell, face, Cell.y));
        }

        // 隣の入力口がこのセルの face 面に接している
        // A neighbour's input port touching this cell's given face
        private static void AddReceiver(BeltRouteGrid edges, BlockDirection face)
        {
            edges.AddInputEdge(BeltRouteEdge.FromFace(Cell, face, Cell.y));
        }
    }
}
```

- [ ] **Step 6: この時点ではまだ呼び出し側が旧 API を参照していてコンパイルが通らないので、Task 2 と合わせてコンパイルする（Task 1 単独のコミットは Task 2 の後に続けて行う）**

Task 1 と Task 2 は同じコンパイル単位。Task 2 の Step 5 でコンパイル・テストを通してから、Task 1 の範囲（Branch/ の3本・テスト・.meta）を先にコミットし、続けて Task 2 をコミットする。

- [ ] **Step 7: コミットする（Task 2 のコンパイル確認後）**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/Branch moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/Branch.meta moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteBranchGeometryTest.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteBranchGeometryTest.cs.meta
git commit -m "Task 1: feat(belt): 分岐器の向きと出口を入ってくる向きから決める幾何を切り出す"
```

---

### Task 2: 設置側を幾何の向き・出口に合わせる（Session / PlaceSystem / Tooltip / Localization）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/BeltRoutePlaceInfoBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/BeltRouteSession.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/BeltRouteTooltipKey.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/BeltRouteKeyInput.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs`
- Modify: `Localization/localization.csv`
- Modify（生成物）: `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`、`moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（gen:i18n が書くその他の生成物も）

- [ ] **Step 1: localization.csv のキー2行を置き換える**

`Localization/localization.csv` の次の2行（現在 309〜310 行付近）

```
ui.tooltip.placeBeltBranchSideInputOccupied,This belt receives items from the side,This belt receives items from the side,このベルトは横から荷物を受けています,Dieses Band erhält Gegenstände von der Seite,이 벨트는 옆에서 아이템을 받고 있습니다
ui.tooltip.placeBeltBranchOtherSideHasReceiver,A receiver sits beside the unused exit,A receiver sits beside the unused exit,使わない側の出口の隣に受け手があります,Neben dem ungenutzten Ausgang steht ein Empfänger,사용하지 않는 출구 옆에 받는 블록이 있습니다
```

を次に置き換える:

```
ui.tooltip.placeBeltBranchMultipleInputs,This belt receives items from two or more directions,This belt receives items from two or more directions,このベルトは2方向以上からアイテムを受けています,Dieses Band erhält Gegenstände aus zwei oder mehr Richtungen,이 벨트는 두 방향 이상에서 아이템을 받고 있습니다
ui.tooltip.placeBeltBranchUnusedExitHasReceiver,A receiver sits beside the unused exit,A receiver sits beside the unused exit,使わない出口の隣に受け手があります,Neben dem ungenutzten Ausgang steht ein Empfänger,사용하지 않는 출구 옆에 받는 블록이 있습니다
```

- [ ] **Step 2: 生成物を更新する**

```bash
# 印を新しい値にする（任意の新しい16バイトの16進表記。例は python で生成）
python3 -c "import uuid;print('-'.join(f'{b:02X}' for b in uuid.uuid4().bytes))"
# 出た値で moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs の dummyText を書き換える
cd moorestech_web/webui && npm run gen:i18n && npx vitest run src/shared/i18n && cd ../..
uloop compile --project-path ./moorestech_client --force-recompile   # COMPILE_RESULT_UNKNOWN が返る
uloop compile --project-path ./moorestech_client                      # ここで LocalizationKeys に新キーが生える
```

- [ ] **Step 3: Session 側を書き換える**

`BeltRoutePlaceInfoBuilder.cs`: `using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;` を足し、`SplitterInfo` と新メソッドを次にする。

```csharp
        // 分岐器と、選んだ出口の先に出る最初のセルを候補として見せる
        // Shows the splitter and the first cell beyond the chosen exit as the candidate
        public static List<PlaceInfo> BuildBranchCandidate(BeltRouteBranchOrigin branch, BeltConveyorHoldingBlock holding)
        {
            var firstCell = branch.Cell + BeltRouteDirection.Forward(branch.ExitDirection);
            return new List<PlaceInfo> { SplitterInfo(branch, true), Single(firstCell, branch.ExitDirection, holding.BlockId, true) };
        }

        public static PlaceInfo SplitterInfo(BeltRouteBranchOrigin branch, bool placeable)
        {
            return Single(branch.Cell, branch.SplitterFacing, branch.SplitterBlockId, placeable);
        }
```

`BeltRouteSession.cs`: `using ...Session.Branch;` を足し、`CreateBranch` を次にする。

```csharp
        // 分岐は既存ベルトのセルが始点で、向きは幾何が選んだ出口
        // A branch starts at the existing belt's cell, heading out of the exit the geometry chose
        public static BeltRouteSession CreateBranch(BeltRouteBranchOrigin branch, BeltRouteWorldScanner scanner)
        {
            return new BeltRouteSession(branch.Cell, branch.ExitDirection, branch, BeltRouteTerrainProfile.Scan(branch.Cell), scanner);
        }
```

`BeltRouteTooltipKey.cs`: `using ...Session.Branch;` を足し、`BranchLine` を次にする。

```csharp
        public static TooltipLine BranchLine(BeltRouteBranchBlockReason reason)
        {
            return reason switch
            {
                BeltRouteBranchBlockReason.NotStraightBelt => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltBranchNotStraightBelt),
                BeltRouteBranchBlockReason.NoSplitter => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltBranchNoSplitter),
                BeltRouteBranchBlockReason.MultipleInputs => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltBranchMultipleInputs),
                BeltRouteBranchBlockReason.UnusedExitHasReceiver => new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltBranchUnusedExitHasReceiver),
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }
```

`BeltRouteKeyInput.cs`: summary の2行を次にする。

```csharp
    ///     2クリック設置が読むキー。R=向き/分岐の出口の切り替え、C=同コスト候補の送り、Ctrl=分岐、左クリック=始点・終点の確定
    ///     Keys read by two-click placement: R rotates or switches the branch exit, C cycles equal-cost candidates, Ctrl branches, left click fixes start and end
```

- [ ] **Step 4: `BeltConveyorPlaceSystem` を書き換える（200行以下を保つ。現在195行）**

`using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;` を足す。フィールド `private bool _branchSideIsRight = true;` → `private bool _branchUseAlternateExit;`。`Enable` の `_branchSideIsRight = true;` → `_branchUseAlternateExit = false;`。`UpdateBranchCandidate` を次にする。

```csharp
        private void UpdateBranchCandidate(BlockGameObject belt, PlacementFeedback feedback)
        {
            // R は既定の出口（直線なら右、角なら正面）と残りの出口を切り替える
            // R toggles between the default exit (right for a straight belt, front for a corner) and the other one
            if (BeltRouteKeyInput.IsRotatePressed) _branchUseAlternateExit = !_branchUseAlternateExit;
            if (!PlaceSystemUtil.IsPlaceableFromPlayer(belt.BlockPosInfo.OriginalPos)) { feedback.AddTooFar(); return; }

            var reason = BeltRouteBranchOrigin.Judge(_blocks, belt, _holding, _branchUseAlternateExit, out var branch);
            if (reason != BeltRouteBranchBlockReason.None)
            {
                feedback.Add(BeltRouteTooltipKey.BranchLine(reason));
                ShowPreview(new List<PlaceInfo> { BeltRoutePlaceInfoBuilder.Single(belt.BlockPosInfo.OriginalPos, belt.BlockPosInfo.BlockDirection, _holding.BlockId, false) });
                return;
            }

            ShowPreview(BeltRoutePlaceInfoBuilder.BuildBranchCandidate(branch, _holding));
            if (!BeltRouteKeyInput.IsConfirmPressed) return;

            _session = BeltRouteSession.CreateBranch(branch, _scanner);
            _currentDirection = branch.ExitDirection;
        }
```

- [ ] **Step 5: コンパイルし、分岐に関わるテストを回す**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0（`Branch.meta`・`BeltRouteBranchGeometry.cs.meta`・`BeltRouteBranchGeometryTest.cs.meta` が生成されていることを `git status` で確認）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaceSystem\.BeltRoute\."`
Expected: 全件 PASS（`BeltRouteBranchGeometryTest` 7件を含む）

- [ ] **Step 6: Task 1 をコミットしてから Task 2 をコミットする**

Task 1 の Step 7 を実行したうえで:

```bash
git checkout -- .moorestech-external-revisions.json moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git add Localization/localization.csv moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs moorestech_web/webui/src/shared/i18n/generated moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs
git commit -m "Task 2: feat(belt): L字の角を Ctrl で分岐化できるようにし、分岐器は入ってくる向きで置く"
```

---

### Task 3: 角の分岐の搬送試験（テスト用サーバー）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteConveyanceWorld.cs` — 搬送試験が共有するテスト用サーバーの組み立てと観測（既存の private 補助を移す＋隣の辺だけを写す補助）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteConveyanceTest.cs` — 補助を外へ出し、角の分岐の搬送試験を追加（200行以下を保つため）

- [ ] **Step 1: 補助クラスを作る**

`BeltRouteConveyanceWorld.cs`（既存 `BeltRouteConveyanceTest` の private 補助をそのまま public static に移し、`BuildNeighbourEdges` を足す）:

```csharp
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 搬送試験が共有するテスト用サーバーの組み立てと観測。箱への写し取り・チェスト・tick 回し
    // Test-server setup and observation shared by the conveyance tests: copying into the box, chests, ticking
    public static class BeltRouteConveyanceWorld
    {
        public const int MaxTransportTicks = 600;
        public static readonly ItemId TestItemId = new(1);

        public static IWorldBlockDatastore BootWorld()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return ServerContext.WorldBlockDatastore;
        }

        // サーバーの全ブロックを占有と入出力辺として箱へ写す。地形は無し
        // Copies every server block into the box as occupancy and port edges; there is no terrain
        public static BeltRouteGrid BuildGridFromWorld(IWorldBlockDatastore world)
        {
            var grid = new BeltRouteGrid(new Vector3Int(-8, 0, -8), new Vector3Int(24, 6, 24));
            foreach (var data in world.BlockMasterDictionary.Values)
            {
                var position = data.BlockPositionInfo;
                for (var x = position.MinPos.x; x <= position.MaxPos.x; x++)
                for (var y = position.MinPos.y; y <= position.MaxPos.y; y++)
                for (var z = position.MinPos.z; z <= position.MaxPos.z; z++)
                    grid.MarkBlockCell(new Vector3Int(x, y, z));
                BeltRouteBlockEdgeScanner.Write(data.Block.BlockMasterElement, data.Block.BlockId, position, grid);
            }
            return grid;
        }

        // 本番の Judge と同じく、対象セルのブロック自身を除いた隣の辺だけを1セルの箱へ写す
        // Like the production Judge, copies only the neighbours' edges into a one-cell box, excluding the block on the target cell
        public static BeltRouteGrid BuildNeighbourEdges(IWorldBlockDatastore world, Vector3Int cell)
        {
            var edges = new BeltRouteGrid(cell, Vector3Int.one);
            foreach (var data in world.BlockMasterDictionary.Values)
            {
                if (data.BlockPositionInfo.OriginalPos == cell) continue;
                BeltRouteBlockEdgeScanner.Write(data.Block.BlockMasterElement, data.Block.BlockId, data.BlockPositionInfo, edges);
            }
            return edges;
        }

        public static BlockId ResolveFamilyBlock(BlockId straightId, BeltRouteCellKind kind)
        {
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(straightId, out var family));
            return kind switch
            {
                BeltRouteCellKind.Up => family.UpBlockId.Value,
                BeltRouteCellKind.Down => family.DownBlockId.Value,
                _ => family.StraightBlockId,
            };
        }

        public static IOpenableBlockInventoryComponent PlaceChest(IWorldBlockDatastore world, Vector3Int position)
        {
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.ChestId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest), $"chest placement failed at {position}");
            return chest.GetComponent<IOpenableBlockInventoryComponent>();
        }

        // 片方だけ待つときはsecondをnullにする
        // Pass null as second to wait for one chest only
        public static bool TickUntilDelivered(IOpenableBlockInventoryComponent first, int firstMin, IOpenableBlockInventoryComponent second, int secondMin)
        {
            for (var tick = 0; tick < MaxTransportTicks; tick++)
            {
                GameUpdater.UpdateOneTick();
                if (CountOf(first) < firstMin) continue;
                if (second == null || secondMin <= CountOf(second)) return true;
            }
            return false;
        }

        public static int CountOf(IOpenableBlockInventoryComponent inventory)
        {
            var count = 0;
            for (var i = 0; i < inventory.GetSlotSize(); i++)
                if (inventory.GetItem(i).Id == TestItemId) count += inventory.GetItem(i).Count;
            return count;
        }
    }
}
```

`BeltConveyorPlaceFamilyUtil` の名前空間は既存テストの using に合わせる（コンパイルで解決する）。

- [ ] **Step 2: 既存テストから補助を外し、角の試験を足す**

`BeltRouteConveyanceTest.cs`: `private const int MaxTransportTicks`・`TestItemId`・`BootWorld`・`BuildGridFromWorld`・`ResolveFamilyBlock`・`PlaceChest`・`TickUntilDelivered`・`CountOf` を削除し、using に `using static Client.Tests.PlaceSystem.BeltRoute.BeltRouteConveyanceWorld;` と `using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;` を足す（不要になった using は消す。呼び出し側のコードは名前がそのままなので変更なし）。`BranchSplitsItemsToBothChests` の直後に追加:

```csharp
        // 西から来て北へ曲がる角 (2,0,0) を分岐器に置き換える。分岐器は東向き、既定の出口は正面（東）
        // Replace the corner at (2,0,0), fed from the west and turning north, with a splitter facing east whose default exit is the front (east)
        [Test]
        public void CornerBranchSplitsItemsToBothChests()
        {
            var world = BootWorld();
            var straight = ForUnitTestModBlockId.GetBlock("7b3e1a10-0001-4000-8000-000000000009");
            var splitter = ForUnitTestModBlockId.GetBlock("7b3e1a10-0001-4000-8000-000000000012");
            var corner = new Vector3Int(2, 0, 0);
            Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(0, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(1, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(straight, corner, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(2, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(2, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));

            // 本番の Judge と同じ写し方（角自身を除いた隣の辺）で向きと出口を決める
            // Decide the facing and exit from the same copy the production Judge makes (neighbours' edges, excluding the corner itself)
            var reason = BeltRouteBranchGeometry.Resolve(BuildNeighbourEdges(world, corner), corner, BlockDirection.North, false, out var shape);
            Assert.AreEqual(BeltRouteBranchBlockReason.None, reason);
            Assert.AreEqual(BlockDirection.East, shape.SplitterFacing);
            Assert.AreEqual(BlockDirection.East, shape.BranchExit);

            var query = new BeltRouteQuery(corner, shape.BranchExit, true, new Vector3Int(5, 0, 0), BlockDirection.East, true, false, BeltRouteStepPriority.Default);
            var result = new BeltRoutePlanner(BuildGridFromWorld(world), BeltRoutePlanner.NoRecoverySlack).Plan(query);
            Assert.IsTrue(result.HasPlan, $"no route: {result.NoPathReason}");
            BeltRoutePlanVerifier.AssertValid(BuildGridFromWorld(world), query, result.Plan, "corner branch route");

            Assert.IsTrue(world.RemoveBlock(corner, BlockRemoveReason.ManualRemove));
            Assert.IsTrue(world.TryAddBlock(splitter, corner, shape.SplitterFacing, Array.Empty<BlockCreateParam>(), out _));
            foreach (var cell in result.Plan.Cells)
                Assert.IsTrue(world.TryAddBlock(straight, cell.Cell, cell.Facing, Array.Empty<BlockCreateParam>(), out _), $"placement failed at {cell}");

            var source = PlaceChest(world, new Vector3Int(-1, 0, 0));
            var cornerSink = PlaceChest(world, new Vector3Int(2, 0, 3));
            var branchSink = PlaceChest(world, new Vector3Int(6, 0, 0));
            source.SetItem(0, ServerContext.ItemStackFactory.Create(TestItemId, 6));

            Assert.IsTrue(TickUntilDelivered(cornerSink, 1, branchSink, 1), $"items did not reach both chests corner:{CountOf(cornerSink)} branch:{CountOf(branchSink)}");
        }
```

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0（`BeltRouteConveyanceWorld.cs.meta` が生成される）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.PlaceSystem.BeltRoute.BeltRouteConveyanceTest"`
Expected: 3件 PASS。両ファイルとも 200 行以下であることを `wc -l` で確認

- [ ] **Step 4: コミットする**

```bash
git checkout -- .moorestech-external-revisions.json moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git add moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteConveyanceTest.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteConveyanceWorld.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteConveyanceWorld.cs.meta
git commit -m "Task 3: test(belt): 角を分岐器に置き換えても既存の続きと分岐の両方へ流れることを確かめる"
```

---

### Task 4: プレイテストDSLと録画シナリオ（角の分岐フェーズ）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/Operations/PlaytestBeltRouteOps.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/PlaytestDriver.cs:153`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-route-branch-via-ui.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/references/write-scenario.md:73`
- Modify: `docs/playtest-dsl.md:85`

- [ ] **Step 1: DSL の `BranchPlaceViaUi` / `BranchStart` を「出口の切り替え」と「経路が出る向き」を受ける形にする**

`PlaytestBeltRouteOps.cs` の2メソッドを次にする（`RotateTo` と `RoutePlaceViaUi` は不変）。

```csharp
        // Ctrlを押したまま既存のベルトをクリックして分岐器へ置き換え、exitDirection の出口から終点まで経路を置く
        // Click an existing belt with Ctrl held to replace it with a splitter and route from the exitDirection exit to the end
        // exitDirection は呼び手が配置から決める（直線の既定は右、角の既定は正面。useAlternateExit で残りの出口）
        // The caller derives exitDirection from the layout (a straight belt defaults to right, a corner to front; useAlternateExit picks the other exit)
        public static async UniTask BranchPlaceViaUi(PlaytestDriver driver, string blockName, Vector3Int beltCell, bool useAlternateExit, BlockDirection exitDirection, Vector3Int toOrigin, BlockDirection endDirection)
        {
            await driver.OpenBuildMenuAndSelectBlock(blockName);
            driver.Note($"分岐設置: {blockName} {beltCell} 出口{exitDirection}{(useAlternateExit ? "(R切替)" : "")} -> {toOrigin}:{endDirection}");
            await BranchStart(driver, beltCell, useAlternateExit);
            await driver.AimAt(PlaytestUiOps.PlaceAimPoint(blockName, toOrigin, BlockDirection.North));
            await RotateTo(driver, exitDirection, endDirection);
            await driver.ClickPlace();
            await driver.Until(() => PlaytestBlockOps.GetBlock(toOrigin) != null, 15f, $"UI分岐設置反映: {blockName} {beltCell}->{toOrigin}");
        }

        // 既存ベルトをCtrl+クリックして分岐の始点にする。始点確定後の向きは出口の向きになる（終点の向き回転の起点）
        // Fixes a branch start by Ctrl+clicking an existing belt; the facing after confirmation is the exit heading, the base for rotating the end facing
        public static async UniTask BranchStart(PlaytestDriver driver, Vector3Int beltCell, bool useAlternateExit)
        {
            // ベルトの箱の内側を狙えば、カメラからのレイは必ずその境界箱の面に当たる
            // Aiming inside the belt's box guarantees the camera ray hits that bounding box's face first
            SemanticInput.KeyDown(Key.LeftCtrl);
            await driver.AimAt(new Vector3(beltCell.x + 0.5f, beltCell.y + 0.5f, beltCell.z + 0.5f));
            // 既定の出口は直線なら右、角なら正面。R で残りの出口へ切り替える
            // The default exit is right for a straight belt and front for a corner; R switches to the other exit
            if (useAlternateExit) await driver.PressKey(Key.R);
            await driver.ClickPlace();
            SemanticInput.KeyUp(Key.LeftCtrl);
            await UniTask.DelayFrame(2);
        }
```

`Game.Context` の using が不要になれば消す。

`PlaytestDriver.cs` 153行目を次にする:

```csharp
        public async UniTask BranchPlaceViaUi(string blockName, Vector3Int beltCell, bool useAlternateExit, BlockDirection exitDirection, Vector3Int toOrigin, BlockDirection endDirection) => await PlaytestBeltRouteOps.BranchPlaceViaUi(this, blockName, beltCell, useAlternateExit, exitDirection, toOrigin, endDirection);
```

- [ ] **Step 2: DSL の説明を更新する**

`.agents/skills/unity-playmode-recorded-playtest/references/write-scenario.md` の表の行:

```
| `BranchPlaceViaUi(name, beltCell, useAlternateExit, exitDirection, to, endDir)` | Ctrl+クリックで既存のベルト（直線か L字の角）を分岐器へ置き換え、exitDirection の出口から終点まで経路を置く。既定の出口は直線なら右・角なら正面、useAlternateExit=true で R を1回押して残りの出口 |
```

`docs/playtest-dsl.md` 85行目:

```csharp
await p.BranchPlaceViaUi("ベルトコンベア", new Vector3Int(2, 32, 4), false, BlockDirection.South, new Vector3Int(6, 32, 4), BlockDirection.East);
```

- [ ] **Step 3: 録画シナリオを更新する**

`belt-route-branch-via-ui.cs`:

フェーズ2の呼び出しを `await p.BranchPlaceViaUi("ベルトコンベア", new Vector3Int(6, 32, 4), false, BlockDirection.South, new Vector3Int(6, 32, 1), BlockDirection.South);` にする。

フェーズ4の Note・Assert を次にする（配置は不変。(8,4) は背面(7,4)と横(8,5)の2方向から受ける）:

```csharp
    p.Note("フェーズ4: 2方向以上から受けているベルトはCtrl+クリックしても分岐にできない（赤と理由）");
    ...
    p.Assert(HasKey("ui.tooltip.placeBeltBranchMultipleInputs"), "2方向以上から受けているベルトは分岐不可の理由行が出る");
```

フェーズ4の `await p.ExitToGameScreen();` の後、`#region Internal` の前にフェーズ5を足す:

```csharp
    // ---------------------------------------------------------------- 5. L字の角を分岐化
    p.Note("フェーズ5: 西から来て北へ曲がる角(4,8)をCtrl+クリックすると、分岐器は東向きに置かれ、既定の出口（正面＝東）から(7,8)まで経路が出る");
    await p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(2, 32, 8), BlockDirection.East);
    await p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(3, 32, 8), BlockDirection.East);
    await p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(4, 32, 8), BlockDirection.North);
    await p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(4, 32, 9), BlockDirection.North);
    await p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(4, 32, 10), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(4, 32, 8));
    await p.BranchPlaceViaUi("ベルトコンベア", new Vector3Int(4, 32, 8), false, BlockDirection.East, new Vector3Int(7, 32, 8), BlockDirection.East);
    await p.Until(() => NameAt(new Vector3Int(4, 32, 8)) == "ベルトコンベア分岐器", 15f, "(4,8)が分岐器へ置き換わる");
    p.Assert(ServerContext.WorldBlockDatastore.GetBlockDirection(new Vector3Int(4, 32, 8)) == BlockDirection.East, "角の分岐器は入ってくる向き（東）で置かれる");
    p.Assert(Enumerable.Range(5, 3).All(x => p.GetBlock(new Vector3Int(x, 32, 8)) != null && ServerContext.WorldBlockDatastore.GetBlockDirection(new Vector3Int(x, 32, 8)) == BlockDirection.East), "分岐の3セルが東向きに置かれる");
    await p.WaitBlockGameObject(new Vector3Int(7, 32, 8));
    await p.ExitToGameScreen();
    await p.Screenshot("04-corner-branch");

    p.Note("フェーズ6: 角の手前から流すと、角の先（北）と分岐の先（東）の両方へ届く");
    var cornerSource = await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(1, 32, 8), BlockDirection.East);
    var cornerSink = await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(4, 32, 11), BlockDirection.North);
    var cornerBranchSink = await p.PlaceBlockDirect("鉄のミニコンベアチェスト", new Vector3Int(8, 32, 8), BlockDirection.East);
    await p.RunOnServer("角の手前のチェストへ鉄インゴット6個", () => cornerSource.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ServerContext.ItemStackFactory.Create(itemId, 6)));
    await p.Until(() => 1 <= CountOf(cornerSink, itemId) && 1 <= CountOf(cornerBranchSink, itemId), 60f, "角の先と分岐の先の両方へ荷物が届く");
    await p.Screenshot("05-corner-flow-both");
```

ファイル冒頭の解説コメントに「L字の角を分岐化すると分岐器が入ってくる向きで置かれ、両方へ流れること」を1文足す（日本語ひとかたまり→英語ひとかたまりの順）。

- [ ] **Step 4: コンパイルし、録画シナリオを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

録画シナリオは `.agents/skills/unity-playmode-recorded-playtest/SKILL.md` の手順どおりに実行する（Editor を前面にして `Application.isFocused` が true の状態で回す。`Saves/` に作ったワールドは同スキル絶対規則11の手順で片付ける）。
Expected: `belt-route-branch-via-ui` の全フェーズ PASS。スクショ `04-corner-branch` に東向きの分岐器と東へ伸びる3セル、`05-corner-flow-both` が撮れる。結果ディレクトリは worktree の gitignore 下に置き、コミットしない。

設置距離は `PlacementDistanceRule.MaximumDistance = 100` なので、スポーン (6, 33.5, -3) から z=8〜11 のセルは射程内。

環境要因（Editor が前面にできない等）で実行できない場合は、生のエラーを報告ファイルに書き、ステータス DONE_WITH_CONCERNS で返す（コントローラーが実機で回す）。

- [ ] **Step 5: コミットする**

```bash
git checkout -- .moorestech-external-revisions.json moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git add moorestech_client/Assets/Scripts/Client.Playtest/Operations/PlaytestBeltRouteOps.cs moorestech_client/Assets/Scripts/Client.Playtest/PlaytestDriver.cs .agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-route-branch-via-ui.cs .agents/skills/unity-playmode-recorded-playtest/references/write-scenario.md docs/playtest-dsl.md
git commit -m "Task 4: test(belt): 録画シナリオに L字の角の分岐フェーズを足し、DSL は出口の切り替えと向きを受ける"
```

---

### Task 5（閉じタスク1）: 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

レビュー指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、Task 4 の録画シナリオを反映後のバイナリで再実施してから完了とする。

### Task 6（閉じタスク2）: セッション終了可能状態にすること

pr-create スキルで PR を作成する。本ブランチには PR 1616 が既に開いているので、push で PR を更新し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する。全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態になっていることを確認して終える。

---

## 判断記録（ADR）

- 設計の正: `docs/adr/0097-belt-two-click-shortest-route-placement.md`「既存ベルトからの分岐」節（2026-11-09 改訂）と `.decisions/2026-11-09-L字の角のベルトは入ってくる向きで分岐器に置き換え既定の出口は正面にする.md`。
- D1 分岐器の向き＝アイテムが入ってくる向き。角の既定の出口＝正面、R で空いている横。出所: ユーザー裁定 2026-11-09 原文「今更だけどL字で1本になってる部分ってL字のところをctrlで上書き分岐化ってできないんだっけ　理論上はできるよね？」「じゃあ分岐化について入力を考慮した配置でブロック置換してなにか問題あるの？」「あってます、規定はまっすぐです」。
- D2 2方向以上から受けるベルトは分岐不可のまま。文言を「2方向以上からアイテムを受けています」へ。出所: agent前提（分岐器の入口は背面1つなので幾何的に不可能。文言はユーザー指摘 2026-11-07「荷物ってどういうこと？荷物なの？」を受けて「アイテム」に揃える）。
- D3 向き・出口の決定を純粋な幾何 `BeltRouteBranchGeometry` に切り出し、`Judge` は辺の収集だけを残す。出所: agent前提（前例: `BeltRouteTransitionRule` が「サーバーの接続規則を探索の可否判定へ写した1枚」として世界に触れない判定を1クラスに集めている。役割同型。世界に触れる `Judge` を直接テストするには BlockGameObject の反射生成が要り、`CommonBlockPlaceExistingBlockCauseTest` がその重さを示している）。
- D4 `Session/` が10ファイル上限のため、分岐の3本を `Session/Branch/` へ移す。出所: AGENTS.md（1ディレクトリ10ファイル）。
- D5 理由 enum の名前を `SideInputOccupied` → `MultipleInputs`、`OtherSideHasReceiver` → `UnusedExitHasReceiver` に変え、辞書キーも同名に変える。出所: agent前提（名前は実処理と一致させる、AGENTS.md）。
- D6 分岐不可の early return はログを出さない。出所: agent前提（理由はツールチップの理由行として利用者に見えるので無音ではない。既存 `Judge` の `// silent-ok:` の流儀を踏襲。毎フレーム呼ばれる判定なのでログはスパムになる）。
- D7 歯車の回転が繋がらない不具合は別 PR。出所: ユーザー裁定 2026-11-09 選択「別 PR にする」。
- D8 ランタイム挙動（入力・プレビュー）に触れるので、録画シナリオ `belt-route-branch-via-ui.cs` に角のフェーズを足す（Task 4）。EditModeInPlayingTest では代替しない。出所: moores-writing-plans の規定。
- D9 プレイテストDSL `BranchPlaceViaUi` は「右か左か」から「R で切り替えた出口を使うか＋経路が出る向き」へ。出所: agent前提（角では出口が「横」ではないので sideIsRight が意味を失う。向きを DSL 側で再計算すると幾何の複製になるため、シナリオの作者が配置から書く）。
- D10 Codex 独立質問の消し込み（codex-audit）は省略。出所: ユーザー裁定 2026-11-09 選択「省略して進める」（codex-audit 未導入）。
- D12 `Resolve` は理由 enum を返し、成立時だけ `out shape` を埋める（不成立時は default）。出所: agent前提（同モジュールの `BeltRouteBranchOrigin.Judge`（out origin は null）・`BeltRouteCellCheck`（理由 enum）と同じ流儀。呼び手は理由が None のときだけ out を読む。設計検査 Phase 2.6 強1件を据え置いた判断）。
- D13 搬送試験の補助は `BeltRouteConveyanceWorld` に出す。幾何へ渡す辺は本番 `Judge` と同じく対象ベルト自身を除く（`BuildNeighbourEdges`）。出所: agent前提（設計検査の備考: 辺は向きを持たないので自分の入力口が受け手に見える。200行上限）。
- D11 作業は本体ワーキングツリーの `feature/belt-two-click-route` で行う（ワークスペース隔離の免除）。出所: ユーザー裁定 2026-11-09 原文「PRだしたもののいろいろ出戻りしないといけないので引き続きlocalでこのブランチに実装していきたい、できそう？」。
