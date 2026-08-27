# ブロック設置Yを地形最高点基準にする Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 通常ブロック設置（単体＋ドラッグ）の設置セルYを、占有範囲の地形最高点を上回る最初の整数セルにして、地形への埋まりを解消する。

**Architecture:** 地表探査の既存エントリポイント `SlopeBlockPlaceSystem.TryGetGroundPoint` / `GetBlockFourCornerMaxHeight` を再利用し、
新規 static クラス `PlacementGroundCellResolver` が「地形最高点 → セルY」の純粋変換と、`PlaceInfo` 列に対するセル毎のY書き換えを担う。
`PlaceSystemUtil.CalcPlacePoint` は純粋関数のまま触らず、物理を既に触っている `TryGetRayHitBlockPosition` の側で
地面ヒット時のみYを差し替える。ドラッグ列は `CommonBlockPlaceSystem` の設置点列更新直後に同じ resolver を通す。

**Tech Stack:** Unity 6000.3.8f1 / C# / NUnit（EditMode）/ uloop CLI

## Requirements

- R1. 地面へのレイヒットで設置するとき、設置セルYは「占有範囲の地形最高点を上回る最初の整数セル」になること。
  受け入れ基準: 地形最高点 32.4 → セルY 33。地形最高点がちょうど 32.0 → セルY 32（浮かせない）。
- R2. 埋まるくらいなら浮かせる。地面との隙間は許容し、隙間を理由に設置を拒否しないこと。
  受け入れ基準: 四隅の高低差が大きい斜面でも設置は成立し、`PlacementBlockCause` に新しい拒否理由を追加しない。
- R3. ドラッグ範囲設置では、開始セルのYコピーをやめ、セル毎に地形高さを解決して階段状に追従すること。
  受け入れ基準: X方向へ伸ばしたドラッグ列の各セルYが、そのセル自身の占有範囲の地形最高点から決まる。
- R4. Q/Eの手動高さオフセット（`heightOffset`）は、地形解決後のYに加算されて生き続けること。
  受け入れ基準: 地形最高点 32.4・heightOffset=2 → セルY 35。
- R5. 既存ブロックの面にヒットして設置する経路（積み重ね）は一切変更しないこと。
  受け入れ基準: `PlaceSystemUtilCalcPlacePointTest` の全テストが無改変で通る。
- R6. 地表探査が失敗したセルは、既存の挙動（レイヒット点の床関数由来のY）を保つこと。
  受け入れ基準: 探査失敗で例外を投げず、そのセルのYを書き換えない。

**やらないこと（スコープ境界）:**
- ベルトコンベア専用経路（`BeltConveyorPlaceSystem`）・レール専用経路（`TrainRailPlaceService`）のY決定は変更しない（bd: moorestech-izz2）
- 設計図(BP)貼り付け（`BlueprintPasteSystem`・`SnapHitPointToCell`）は変更しない（bd: moorestech-izz2）
- 地面めり込み判定（`GroundCollisionDetector` のトリガー方式）の欠陥修理はしない（bd: moorestech-y768）
- 見た目Yのオフセット（論理セルと描画の分離）は導入しない（ADR 0037 で棄却）
- 地形の量子化・傾斜による設置拒否は導入しない（ADR 0037 で棄却）

## Global Constraints

- ADR: `docs/adr/0037-block-placement-y-from-terrain-max-height.md` が本planの正。裁定記録は `.decisions/2026-08-28-ブロック設置Yは地形最高点を上回るセルにする.md`
- 1ファイル200行以下。1ディレクトリ10ファイルまで
- `partial` 禁止・`Func<>` 禁止・try-catch 原則禁止・デフォルト引数禁止
- 単純なgetter/setterプロパティ禁止。値のSetは `SetHoge` メソッド
- コメントは主要セクションに日本語1行→英語1行の2行セット。日本語は処理・変数20字／メソッド30字が目安
- 複雑なメソッドは `#region Internal` ＋ローカル関数。クラス直下のprivateメソッド群を `#region Internal` で囲うのは禁止
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する
- `.meta` ファイルは手動作成しない（Unity が生成する。生成された `.meta` のコミットは可）
- テストは `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"` で対象を限定して実行する
- `uloop run-tests` の180秒CLIタイムアウトは失敗ではない。結果XMLは `.uloop/outputs/TestResults` を見る
- 作業場所: worktree `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/block-place-y`（ブランチ `feature/block-place-y-terrain-max-height`）

---

## File Structure

| ファイル | 責務 |
|---|---|
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/PlacementGroundCellResolver.cs`（新規） | 地形最高点→セルYの純粋変換と、`PlaceInfo` 列へのセル毎Y書き換え |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/SlopeBlockPlaceSystem.cs`（変更） | 四隅最高点探査に失敗を返す Try 版を追加 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs`（変更） | 地面ヒット時のみ、`CalcPlacePoint` の結果Yを地形解決値へ差し替える |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`（変更） | ドラッグ列の生成直後に resolver を通す |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementGroundCellResolverTest.cs`（新規） | セルY変換の純粋ロジックテスト |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementGroundCellApplyTest.cs`（新規） | 実コライダーを置いた列書き換えテスト |

`PlaceSystem/Util/` は既に6ファイルあり、地形解決は「建設コスト」系とは責務が違うため `PlaceSystem/Ground/` を新設する。

## 配置と前例

- **役割「PlaceInfo列を走査してセル属性を書き換える static クラス」の前例**:
  `PlaceSystem/Feedback/PlacementCellReasonReporter.cs`、`PlaceSystem/Common/MinerVeinPlacementReporter.cs`、
  `PlaceSystem/Util/ConstructionCostPreviewMarker.cs`。いずれも static・リストを受けて破壊的に書き換え・`CommonBlockPlaceSystem.ManualUpdate` から順に呼ばれる。
  `PlacementGroundCellResolver` はこの形に合わせる。
- **役割「地表を探査する」の前例**: `SlopeBlockPlaceSystem.TryGetGroundPoint` / `GetBlockFourCornerMaxHeight`
  （`Client.Game/InGame/BlockSystem/SlopeBlockPlaceSystem.cs:70-118`）。新しい探査機構は作らずこれを呼ぶ。
- **層**: すべて `Client.Game` 内。`Core.*` およびサーバー側への追加はゼロ。サーバーは地形を知らないまま（ADR 0037 Context）。
- **データフロー**: レイヒット → `CalcPlacePoint`（XZ確定・純粋） → **［新］地形解決でY確定** → `PlaceInfo`列 → プレビュー／可否判定 → 送信。
  新規コンポーネントの立ち位置は「Yの書き手」1人であり、下流へ制御を返す `bool` 戻り値も第2の書き込み経路も足さない。

## 機能パリティ死活表（同じ機構にぶら下がる操作）

| 操作 | plan後も生きるか | 根拠 |
|---|---|---|
| Q/E の手動高さオフセット | 生きる | resolver が `groundMaxHeight` から求めたセルYに `heightOffset` を加算する（R4・Task 1 Step 3） |
| 既存ブロック面への隣接／積み重ね設置 | 生きる | `surfaceType != null` 分岐は無変更。地形解決は地面ヒット時のみ（R5・Task 2 Step 3） |
| 採掘機の鉱脈内判定 | 生きる | Y確定を `MinerVeinPlacementReporter` より前に置く（Task 3 Step 3 の挿入位置） |
| 電線自動接続プレビュー | 生きる | 同上。`ApplyAutoConnect` は確定後のY列を見る |
| 建設コスト・素材不足表示 | 生きる | 同上 |
| ドラッグの水平ライン設置 | **変わる（階段状追従になる）** | ユーザー裁定済み（ADR 0037 / R3） |
| 「地形に埋まっています」ツールチップ | 生きる（発火頻度は下がる） | `PlacementCellReasonReporter` は無変更 |

---

### Task 1: 地形最高点からセルYを決める純粋変換

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/PlacementGroundCellResolver.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementGroundCellResolverTest.cs`

**Interfaces:**
- Consumes: なし（純粋関数のみ）
- Produces:
  - `public static class PlacementGroundCellResolver`
  - `public static int ResolveCellY(float groundMaxHeight, int heightOffset)`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementGroundCellResolverTest.cs`:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.Ground
{
    // 地形最高点からセルYを決める純粋変換の検証
    // Verify the pure conversion from the terrain max height to a cell Y
    public class PlacementGroundCellResolverTest
    {
        // 端数のある地表は上のセルへ切り上げる（埋まるくらいなら浮かせる）
        // Fractional ground rounds up to the cell above, floating rather than sinking
        [Test]
        public void 端数のある地表は上のセルへ切り上げる()
        {
            Assert.AreEqual(33, PlacementGroundCellResolver.ResolveCellY(32.4f, 0));
            Assert.AreEqual(33, PlacementGroundCellResolver.ResolveCellY(32.9f, 0));
            Assert.AreEqual(1, PlacementGroundCellResolver.ResolveCellY(0.1f, 0));
        }

        // 整数ちょうどの地表は浮かせない
        // Ground exactly on an integer must not float
        [Test]
        public void 整数ちょうどの地表は浮かせない()
        {
            Assert.AreEqual(32, PlacementGroundCellResolver.ResolveCellY(32f, 0));
            Assert.AreEqual(0, PlacementGroundCellResolver.ResolveCellY(0f, 0));
        }

        // 整数近傍の浮動小数点誤差で1段浮かない
        // Floating-point noise near an integer must not float one cell
        [Test]
        public void 整数近傍の誤差で一段浮かない()
        {
            Assert.AreEqual(32, PlacementGroundCellResolver.ResolveCellY(32.0001f, 0));
            Assert.AreEqual(32, PlacementGroundCellResolver.ResolveCellY(31.9999f, 0));
        }

        // 負の高さでも切り上げ規約は変わらない
        // The round-up convention is unchanged for negative heights
        [Test]
        public void 負の高さでも切り上げる()
        {
            Assert.AreEqual(-3, PlacementGroundCellResolver.ResolveCellY(-3.4f, 0));
            Assert.AreEqual(-4, PlacementGroundCellResolver.ResolveCellY(-4f, 0));
        }

        // 手動高さオフセットは地形解決後のセルYへ加算される
        // The manual height offset is added on top of the terrain-resolved cell Y
        [Test]
        public void 手動オフセットは地形解決後に加算される()
        {
            Assert.AreEqual(35, PlacementGroundCellResolver.ResolveCellY(32.4f, 2));
            Assert.AreEqual(31, PlacementGroundCellResolver.ResolveCellY(32.4f, -2));
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlacementGroundCellResolverTest"`
Expected: コンパイルエラー（`PlacementGroundCellResolver` が存在しない）

- [ ] **Step 3: 最小限の実装を書く**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/PlacementGroundCellResolver.cs`:

```csharp
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     地形の高さから設置セルYを決める。埋まるくらいなら浮かせる（ADR 0037）
    ///     Decides the placement cell Y from the terrain height, floating rather than sinking (ADR 0037)
    /// </summary>
    public static class PlacementGroundCellResolver
    {
        // 整数ちょうどの地表が探査誤差で1段浮くのを防ぐ許容量
        // Tolerance that keeps ground exactly on an integer from floating one cell due to probe noise
        private const float IntegerGroundTolerance = 0.001f;

        // 占有範囲の地形最高点を上回る最初のセルを返す。手動オフセットはその後に加算する
        // Returns the first cell above the footprint's terrain max height, then adds the manual offset
        public static int ResolveCellY(float groundMaxHeight, int heightOffset)
        {
            return Mathf.CeilToInt(groundMaxHeight - IntegerGroundTolerance) + heightOffset;
        }
    }
}
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → エラー0を確認
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlacementGroundCellResolverTest"`
Expected: 5テストすべて PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground \
        moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground \
        docs/adr/0037-block-placement-y-from-terrain-max-height.md \
        .decisions/2026-08-28-ブロック設置Yは地形最高点を上回るセルにする.md \
        docs/superpowers/plans/2026-08-28-block-place-y-terrain-max-height.md
git commit -m "feat: 地形最高点から設置セルYを決める純粋変換を追加"
```

---

### Task 2: 単体設置のYを地形最高点基準にする

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/SlopeBlockPlaceSystem.cs:96-118`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/PlacementGroundCellResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs:37-47`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementGroundCellApplyTest.cs`

**Interfaces:**
- Consumes: `PlacementGroundCellResolver.ResolveCellY(float, int)`（Task 1）
- Produces:
  - `public static bool SlopeBlockPlaceSystem.TryGetBlockFourCornerMaxHeight(Vector3Int blockPos, BlockDirection blockDirection, Vector3Int blockSize, out float maxHeight)`
  - `public static Vector3Int PlacementGroundCellResolver.ResolveCellFromGround(Vector3Int cellPosition, BlockDirection blockDirection, Vector3Int blockSize, int heightOffset)`
    — 探査に失敗したら `cellPosition` をそのまま返す

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementGroundCellApplyTest.cs`:

```csharp
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Ground
{
    // 実コライダーを置いてセル位置の地形解決を検証する
    // Verify the terrain resolution of a cell position against real colliders
    public class PlacementGroundCellApplyTest
    {
        private readonly List<GameObject> _slabs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var slab in _slabs) Object.DestroyImmediate(slab);
            _slabs.Clear();
        }

        // 地表32.4のセルはY33へ持ち上がる
        // A cell over ground at 32.4 is lifted to Y 33
        [Test]
        public void 端数のある地表の上のセルへ持ち上がる()
        {
            CreateGroundSlab(new Vector3(100.5f, 31.9f, 200.5f), new Vector3(6f, 1f, 6f));

            var resolved = PlacementGroundCellResolver.ResolveCellFromGround(
                new Vector3Int(100, 0, 200), BlockDirection.North, Vector3Int.one, 0);

            Assert.AreEqual(new Vector3Int(100, 33, 200), resolved);
        }

        // 四隅のうち最も高い地表に合わせる
        // The highest of the four corners wins
        [Test]
        public void 四隅の最高点に合わせる()
        {
            CreateGroundSlab(new Vector3(300.5f, 9.5f, 400.5f), new Vector3(6f, 1f, 6f));
            CreateGroundSlab(new Vector3(301f, 14.2f, 401f), Vector3.one);

            var resolved = PlacementGroundCellResolver.ResolveCellFromGround(
                new Vector3Int(300, 0, 400), BlockDirection.North, Vector3Int.one, 0);

            // 高い段の上面は 14.2 + 0.5 = 14.7 なのでセルYは15
            // The high slab's top is 14.2 + 0.5 = 14.7, so the cell Y is 15
            Assert.AreEqual(15, resolved.y);
        }

        // 手動オフセットは地形解決後に加算される
        // The manual offset is added after the terrain resolution
        [Test]
        public void 手動オフセットが加算される()
        {
            CreateGroundSlab(new Vector3(500.5f, 19.9f, 600.5f), new Vector3(6f, 1f, 6f));

            var resolved = PlacementGroundCellResolver.ResolveCellFromGround(
                new Vector3Int(500, 0, 600), BlockDirection.North, Vector3Int.one, 3);

            Assert.AreEqual(24, resolved.y);
        }

        // 地表が無いセルは元のY（=呼び出し側の値）を保つ
        // A cell with no ground keeps its original Y
        [Test]
        public void 地表が無いセルは元のYを保つ()
        {
            var original = new Vector3Int(900, 7, 900);

            var resolved = PlacementGroundCellResolver.ResolveCellFromGround(
                original, BlockDirection.North, Vector3Int.one, 0);

            Assert.AreEqual(original, resolved);
        }

        // XZは書き換えない
        // XZ is never rewritten
        [Test]
        public void XZは書き換えない()
        {
            CreateGroundSlab(new Vector3(700.5f, 4.9f, 800.5f), new Vector3(6f, 1f, 6f));

            var resolved = PlacementGroundCellResolver.ResolveCellFromGround(
                new Vector3Int(700, 0, 800), BlockDirection.North, Vector3Int.one, 0);

            Assert.AreEqual(700, resolved.x);
            Assert.AreEqual(800, resolved.z);
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

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlacementGroundCellApplyTest"`
Expected: コンパイルエラー（`ResolveCellFromGround` が存在しない）

- [ ] **Step 3: 実装を書く**

3-a. `SlopeBlockPlaceSystem.cs` の `GetBlockFourCornerMaxHeight`（96-118行）を、失敗を返す Try 版へ置き換える。
既存の `GetBlockFourCornerMaxHeight` は `[Obsolete]` の `GetSlopeBeltConveyorTransform`(L50) と
`SlopeBlockGroundProbeTest`(L38) が呼んでいるため、シグネチャを残したまま Try 版へ委譲する形にする:

```csharp
        // 四隅すべての地表が取れたときだけ最高点を返す。探査失敗を呼び出し側が扱えるようにbool戻り
        // Returns the max height only when all four corners hit ground; the bool lets callers handle a failed probe
        public static bool TryGetBlockFourCornerMaxHeight(Vector3Int blockPos, BlockDirection blockDirection, Vector3Int blockSize, out float maxHeight)
        {
            maxHeight = 0f;
            var (minPos, maxPos) = blockPos.GetWorldBlockBoundingBox(blockDirection, blockSize);

            // boundingBoxは3次元なので水平の四隅はXとZで組む。Vector2の暗黙変換に任せると鉛直Yを渡してz=0を探査してしまう
            // The bounding box is 3D, so the horizontal corners pair X with Z; the Vector2 conversion would pass the vertical Y and probe z=0
            if (!TryProbeCornerHeight(minPos.x, minPos.z, out var minXMinZ)) return false;
            if (!TryProbeCornerHeight(minPos.x, maxPos.z, out var minXMaxZ)) return false;
            if (!TryProbeCornerHeight(maxPos.x, minPos.z, out var maxXMinZ)) return false;
            if (!TryProbeCornerHeight(maxPos.x, maxPos.z, out var maxXMaxZ)) return false;

            maxHeight = Mathf.Max(Mathf.Max(minXMinZ, minXMaxZ), Mathf.Max(maxXMinZ, maxXMaxZ));
            return true;

            #region Internal

            bool TryProbeCornerHeight(float worldX, float worldZ, out float height)
            {
                if (!TryGetGroundPoint(worldX, worldZ, out var groundPoint))
                {
                    height = 0f;
                    return false;
                }
                height = groundPoint.y;
                return true;
            }

            #endregion
        }

        public static float GetBlockFourCornerMaxHeight(Vector3Int blockPos, BlockDirection blockDirection, Vector3Int blockSize)
        {
            if (!TryGetBlockFourCornerMaxHeight(blockPos, blockDirection, blockSize, out var maxHeight))
                throw new InvalidOperationException($"四隅の地表が見つかりませんでした blockPos:{blockPos}");
            return maxHeight;
        }
```

3-b. `PlacementGroundCellResolver.cs` へセル解決を追加する（`using` に `Client.Game.InGame.BlockSystem` と `Game.Block.Interface` を足す）:

```csharp
        // セルの占有範囲の地形最高点からYを決め直す。地表が取れなければ元のセルを返す
        // Re-decides Y from the footprint's terrain max height; returns the original cell when no ground is found
        public static Vector3Int ResolveCellFromGround(Vector3Int cellPosition, BlockDirection blockDirection, Vector3Int blockSize, int heightOffset)
        {
            if (!SlopeBlockPlaceSystem.TryGetBlockFourCornerMaxHeight(cellPosition, blockDirection, blockSize, out var groundMaxHeight)) return cellPosition;

            return new Vector3Int(cellPosition.x, ResolveCellY(groundMaxHeight, heightOffset), cellPosition.z);
        }
```

3-c. `PlaceSystemUtil.TryGetRayHitBlockPosition`（37-47行）を、地面ヒット時のみ地形解決を通す形へ置き換える:

```csharp
        public static bool TryGetRayHitBlockPosition(Camera mainCamera, int heightOffset, BlockDirection currentBlockDirection, BlockMasterElement holdingBlock, out Vector3Int pos, out BlockPreviewBoundingBoxSurface surface)
        {
            pos = Vector3Int.zero;
            surface = null;

            if (!TryGetRayHitPosition(mainCamera, out var hitPos, out surface)) return false;

            pos = CalcPlacePoint(holdingBlock, hitPos, heightOffset, currentBlockDirection, surface);

            // 地面ヒットのYはレイの当たった高さでなく占有範囲の地形最高点から決める（ADR 0037）。ブロック面ヒットは整数グリッド上なので触らない
            // A ground hit decides Y from the footprint's terrain max height, not the ray height (ADR 0037); block-face hits sit on the integer grid and stay untouched
            if (surface == null) pos = PlacementGroundCellResolver.ResolveCellFromGround(pos, currentBlockDirection, holdingBlock.BlockSize, heightOffset);

            return true;
        }
```

`PlaceSystemUtil.cs` の using に `using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;` を追加する。

**注意:** `CalcPlacePoint` は heightOffset を既に加算しているため、地面ヒット分岐では
`ResolveCellFromGround` が返すYが `CalcPlacePoint` のYを**上書き**する（二重加算にはならない）。

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → エラー0を確認
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlacementGroundCellApplyTest|PlacementGroundCellResolverTest|SlopeBlockGroundProbeTest|PlaceSystemUtilCalcPlacePointTest"`
Expected: 全テスト PASS（`PlaceSystemUtilCalcPlacePointTest` と `SlopeBlockGroundProbeTest` は無改変で通ること = R5）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/SlopeBlockPlaceSystem.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/PlacementGroundCellResolver.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs \
        moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementGroundCellApplyTest.cs
git commit -m "feat: 地面ヒット時の設置Yを占有範囲の地形最高点基準にする"
```

---

### Task 3: ドラッグ列のYをセル毎に地形解決する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/PlacementGroundCellResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs:124-131`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementGroundCellApplyTest.cs`（追記）

**Interfaces:**
- Consumes: `PlacementGroundCellResolver.ResolveCellFromGround(Vector3Int, BlockDirection, Vector3Int, int)`（Task 2）
- Produces:
  - `public static void PlacementGroundCellResolver.ApplyGroundCellY(List<PlaceInfo> placeInfos, Vector3Int blockSize, int heightOffset)`

- [ ] **Step 1: 失敗するテストを書く**

`PlacementGroundCellApplyTest.cs` の末尾（`private void CreateGroundSlab` の直前）へ追記する:

```csharp
        // ドラッグ列は各セルがそれぞれの地形高さへ追従する
        // Each cell of a drag run follows its own terrain height
        [Test]
        public void ドラッグ列は各セルがそれぞれの地形へ追従する()
        {
            // 隣接セルは四隅を共有するため、段差はセル境界のX平面に薄い柱で立てる
            // Adjacent cells share corners, so the steps are thin pillars standing on the cell-boundary X planes
            CreateGroundSlab(new Vector3(1001.5f, 9.9f, 1100.5f), new Vector3(8f, 1f, 6f));
            CreateGroundSlab(new Vector3(1002f, 13.9f, 1100.5f), new Vector3(0.2f, 1f, 6f));
            CreateGroundSlab(new Vector3(1003f, 17.9f, 1100.5f), new Vector3(0.2f, 1f, 6f));

            var placeInfos = new List<PlaceInfo>
            {
                new() { Position = new Vector3Int(1000, 0, 1100), Direction = BlockDirection.North, Placeable = true },
                new() { Position = new Vector3Int(1001, 0, 1100), Direction = BlockDirection.North, Placeable = true },
                new() { Position = new Vector3Int(1002, 0, 1100), Direction = BlockDirection.North, Placeable = true },
            };

            PlacementGroundCellResolver.ApplyGroundCellY(placeInfos, Vector3Int.one, 0);

            // 四隅の最高点は 10.4 / 14.4 / 18.4 なのでセルYは 11 / 15 / 19
            // The four-corner maxima are 10.4 / 14.4 / 18.4, so the cell Ys are 11 / 15 / 19
            Assert.AreEqual(11, placeInfos[0].Position.y);
            Assert.AreEqual(15, placeInfos[1].Position.y);
            Assert.AreEqual(19, placeInfos[2].Position.y);
        }

        // 地表の無いセルは元のYを保ったまま他セルの解決を妨げない
        // A cell with no ground keeps its Y and does not stop the others from resolving
        [Test]
        public void 地表の無いセルが混じっても他セルは解決される()
        {
            CreateGroundSlab(new Vector3(1200.5f, 5.9f, 1300.5f), new Vector3(4f, 1f, 4f));

            var placeInfos = new List<PlaceInfo>
            {
                new() { Position = new Vector3Int(1200, 0, 1300), Direction = BlockDirection.North, Placeable = true },
                new() { Position = new Vector3Int(1900, 42, 1900), Direction = BlockDirection.North, Placeable = true },
            };

            PlacementGroundCellResolver.ApplyGroundCellY(placeInfos, Vector3Int.one, 0);

            Assert.AreEqual(7, placeInfos[0].Position.y);
            Assert.AreEqual(42, placeInfos[1].Position.y);
        }
```

冒頭の using に `using Server.Protocol.PacketResponse;` を追加する（`PlaceInfo` の名前空間）。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlacementGroundCellApplyTest"`
Expected: コンパイルエラー（`ApplyGroundCellY` が存在しない）

- [ ] **Step 3: 実装を書く**

3-a. `PlacementGroundCellResolver.cs` へ列書き換えを追加する（using に `System.Collections.Generic` と `Server.Protocol.PacketResponse` を足す）:

```csharp
        // ドラッグ列の各セルを自分の真下の地形へ追従させる。開始セルのYコピーをここで打ち消す
        // Makes each cell of a drag run follow the terrain beneath it, cancelling the start cell's Y copy
        public static void ApplyGroundCellY(List<PlaceInfo> placeInfos, Vector3Int blockSize, int heightOffset)
        {
            foreach (var placeInfo in placeInfos)
            {
                placeInfo.Position = ResolveCellFromGround(placeInfo.Position, placeInfo.Direction, blockSize, heightOffset);
            }
        }
```

3-b. `CommonBlockPlaceSystem.cs` の `GroundClickControl` 内、`UpdateCurrentPlaceInfos` 呼び出し（124行）と
`SetPreviewAndGroundDetect`（126行）の間へ地形解決を挿入する。既存の
`var placeCauses = UpdateCurrentPlaceInfos(placePoint, holdingBlockMaster);` の直後に次を足す:

```csharp
                // 各セルのYを自分の真下の地形へ追従させる（ドラッグ開始セルのYコピーを打ち消す。ADR 0037）
                // Make each cell's Y follow the terrain beneath it, cancelling the drag start cell's Y copy (ADR 0037)
                if (isGroundHit) PlacementGroundCellResolver.ApplyGroundCellY(_currentPlaceInfos, holdingBlockMaster.BlockSize, _dragState.HeightOffset);
```

`isGroundHit` は 111行の `TryGetRayHitBlockPosition` の `out` 第2引数から得る。111行を次へ置き換える:

```csharp
                if (!TryGetRayHitBlockPosition(_mainCamera, _dragState.HeightOffset, _currentBlockDirection, holdingBlockMaster, out var placePoint, out var hitSurface)) { _autoConnectPreview.Hide(); return; }

                // 地面ヒットのときだけ地形追従する。ブロック面ヒット（積み重ね）は整数グリッド上なので触らない
                // Follow the terrain only on a ground hit; block-face hits (stacking) sit on the integer grid and stay untouched
                var isGroundHit = hitSurface == null;
```

`CommonBlockPlaceSystem.cs` の using に `using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;` を追加する。

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → エラー0を確認
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlacementGroundCell|SlopeBlockGroundProbeTest|PlaceSystemUtilCalcPlacePointTest|CommonBlockPlacePointCalculatorTest"`
Expected: 全テスト PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/PlacementGroundCellResolver.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs \
        moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementGroundCellApplyTest.cs
git commit -m "feat: ドラッグ列のYをセル毎に地形解決して階段状に追従させる"
```

---

### Task 4: 実プレイでの埋まり解消を録画付きで確認する

**Files:**
- Modify: なし（検証のみ。不具合が出たら Task 2/3 のファイルを修正する）

**Interfaces:**
- Consumes: Task 1〜3 の実装
- Produces: なし

- [ ] **Step 1: プレイテストDSLでシナリオを実行する**

`unity-playmode-recorded-playtest` スキルを起動し、同梱の `scripts/run-scenario.sh` で
「自動生成マップの起伏のある地形へ、UI経路（ビルドメニュー→クリック）でブロックを1個設置する」シナリオを実行する。

Expected: `result.json` が成功で返り、録画に設置の様子が残る

- [ ] **Step 2: 録画で埋まりを目視確認する**

録画を確認し、次の3点を判定する:
1. 平地に置いたブロックの底面が地面へ沈んでいないこと（R1）
2. 斜面に置いたブロックが埋まらず、低い側に隙間ができていること（R2）
3. ドラッグで斜面を横断したとき、各ブロックが階段状に地形へ乗っていること（R3）

Expected: 3点すべて満たす。満たさない場合は Task 2/3 の実装へ戻る

- [ ] **Step 3: エラーログが出ていないことを確認する**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 本変更由来のエラーが0件

- [ ] **Step 4: コミットする**

```bash
git add -A
git commit -m "chore: 地形追従設置のプレイ検証を反映"
```

（変更が無ければコミットは不要。その場合は次のタスクへ進む）

---

### Task 5: 全ブランチレビュー（省略不可）

**Files:**
- Modify: レビュー指摘に応じて Task 1〜3 のファイル

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

`moores-code-review` スキルを起動し、`master...feature/block-place-y-terrain-max-height` の全差分をレビューする。
このステップはゴール達成を理由に省略してはならない（AGENTS.md「PR前レビュー」・subagent-driven-development の mandatory-gate）。

- [ ] **Step 2: 機械的指摘を修正しコミットする**

```bash
git add -A
git commit -m "fix: コードレビュー指摘の修正"
```

- [ ] **Step 3: 設計判断の指摘はユーザーへ諮る**

AskUserQuestion で設計判断だけをまとめて提示し、裁定を得てから反映する。

---

## 判断記録（ADR）

- 正となる設計ADR: `docs/adr/0037-block-placement-y-from-terrain-max-height.md`
- 裁定記録: `.decisions/2026-08-28-ブロック設置Yは地形最高点を上回るセルにする.md`
- 関連タスク: bd `moorestech-sw4f`（本体）／`moorestech-izz2`・`moorestech-y768`（スコープ外の派生）

planning中に生じた判断:

- **`CalcPlacePoint` を純粋関数のまま残し、地形解決は `TryGetRayHitBlockPosition` 側で行う。**
  `CalcPlacePoint` 内で物理探査を呼ぶと既存の純粋ロジックテスト（`PlaceSystemUtilCalcPlacePointTest`）が
  コライダー配置なしでは通らなくなる。`TryGetRayHitBlockPosition` は既に `Physics.Raycast` を呼んでおり、
  物理の境界はそこに引かれている。
  出所: agent前提（既存 `PlaceSystemUtilCalcPlacePointTest` が純粋ロジックテストである事実）

- **地形解決は地面ヒット（`surface == null`）時のみ適用する。**
  既存ブロック面へのヒット（積み重ね）で地形へ吸着させると積み重ねが壊れる。R5 の受け入れ基準がこれを守る。
  出所: agent前提（ADR 0037 のスコープ「通常ブロック設置」と `PlaceSystemUtil.cs:139-164` の面スナップが整数グリッド前提である事実）

- **探査失敗時は元のYを保ち、例外を投げない。**
  既存の `GetBlockFourCornerMaxHeight` は探査失敗で `InvalidOperationException` を投げる。
  毎フレーム走る設置プレビューでこれが飛ぶとゲームが止まるため、Try 版を新設して失敗を戻り値で扱う。
  既存シグネチャは `[Obsolete]` 経路とテストが呼んでいるため、Try 版へ委譲する形で残す。
  出所: agent前提（`SlopeBlockPlaceSystem.cs:114` の throw と、`CommonBlockPlaceSystem.ManualUpdate` が毎フレーム走る事実）

- **切り上げに 0.001 の許容量を入れる。**
  地表がちょうど整数高さ（テスト用平地・ブロック上面）のとき、探査誤差で 32.0001 を返すと `Ceil` が 33 になり
  1セル浮く。既存の `PlaceSystemUtil.cs:131` も同じ理由で `+0.001f` を入れている（前例）。
  出所: agent前提（`PlaceSystemUtil.cs:129-131` のイプシロン補正コメントが同種の誤差を記録している）

- **新規ディレクトリ `PlaceSystem/Ground/` を作る。**
  `PlaceSystem/Util/` は既に6ファイルあり、うち5つが建設コスト系。地形解決は責務が違うため分ける。
  出所: agent前提（AGENTS.md「1ディレクトリ10ファイルまで／責務で分割」）
