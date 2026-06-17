# ベルトコンベア自動立体交差 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ベルトコンベアをドラッグで引くとき、経路上の既存ブロックを自動で上に跨ぐ立体交差プロファイルを生成する。

**Architecture:** 既存のクライアント経路計算（`CommonBlockPlacePointCalculator`）の後段に、冪等な「障害物スキャン → 2パス包絡線 → Y上昇・縦方向再計算」レイヤーを重ねる。障害物が無ければ恒等変換となり既存挙動は不変。サーバーは `PlaceInfo` ごとに Y + VerticalDirection を受け取り縦バリアントを設置するだけなので変更不要。

**Tech Stack:** C# / Unity / NUnit（EditMode純粋テスト + PlayModeランタイムテスト）/ uloop CLI。

## Global Constraints

- 1ファイル200行以下。1ディレクトリ10ファイル以下。`partial` 絶対禁止。
- `#region Internal` はメソッド内ローカル関数限定。`#endregion` の下にコードを書かない。
- 主要処理に日本語→英語の2行セットコメント（各1行）を約3〜10行ごと。
- `try-catch` 禁止。デフォルト引数禁止（引数追加時は呼び出し側を全て変更）。
- 単純 getter/setter プロパティ禁止。値設定は `SetXxx` メソッド。
- `.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client`。
- `.meta` は手動作成禁止（Unityが自動生成。`uloop compile` で生成される）。
- テスト実行は `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<regex>"`。
- ベルトの昇降は 1セルあたり Y を 1 だけ変える（slope ≤ 1）。「コーナー + 勾配」バリアントは存在しない。
- 立体交差は「上に跨ぐのみ」。跨げないセルは `Placeable=false`。

---

## ファイル構成

新規ディレクトリ:
`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/`

| ファイル | 責務 |
|---|---|
| `ConveyorVerticalEnvelope.cs` | 純粋関数 `Solve`。2パス包絡線 + コーナー踊り場化 + 端点固定の可否判定。 |
| `ConveyorObstacleScanner.cs` | 各経路セルの直上スタックを探索し下限 `int[]` を返す。 |
| `ConveyorOverpassRaiser.cs` | scan → solve → `PlaceInfo` 列に Y・縦方向・可否を反映。 |

変更:
| ファイル | 変更内容 |
|---|---|
| `Common/CommonBlockPlacePointCalculator.cs` | 静的 `CalculatePoint` に `isOccupied` 引数追加。後段に Raiser 呼び出し1段追加。インスタンス側に `IsOccupied` probe 追加。 |
| `Client.Tests/CommonBlockPlacePointCalculatorTest.cs` | 静的呼び出しに `_ => false` 引数追加（期待値は不変）。 |

新規テスト:
| ファイル | 種別 |
|---|---|
| `Client.Tests/ConveyorVerticalEnvelopeTest.cs` | EditMode 純粋ロジック |
| `Client.Tests/ConveyorObstacleScannerTest.cs` | EditMode 純粋ロジック |
| `Client.Tests/ConveyorOverpassRaiserTest.cs` | EditMode 純粋ロジック |
| `Client.Tests/PlayModeTest/ConveyorOverpassRuntimeTest.cs` | PlayMode ランタイム搬送検証 |

---

### Task 1: ConveyorVerticalEnvelope（純粋包絡線ロジック）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/ConveyorVerticalEnvelope.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/ConveyorVerticalEnvelopeTest.cs`

**Interfaces:**
- Produces: `ConveyorVerticalEnvelope.Solve(int[] lowerBounds, int fixedStart, int fixedEnd, int cornerIndex)` → `(int[] beltY, bool[] feasible)`。`beltY[i] >= lowerBounds[i]`、隣接差 ≤ 1、コーナー前後3セルは平坦。端点が固定値に戻らないセルは `feasible=false`。

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/ConveyorVerticalEnvelopeTest.cs`:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using NUnit.Framework;

namespace Client.Tests
{
    public class ConveyorVerticalEnvelopeTest
    {
        [Test]
        public void FlatNoObstacle_ReturnsIdentity()
        {
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(new[] { 0, 0, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 0, 0, 0 }, beltY);
            Assert.AreEqual(new[] { true, true, true }, feasible);
        }

        [Test]
        public void SingleHeight1Obstacle_RaisesAndReturns()
        {
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(new[] { 0, 0, 1, 0, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 0, 0, 1, 0, 0 }, beltY);
            Assert.AreEqual(new[] { true, true, true, true, true }, feasible);
        }

        [Test]
        public void Height2Obstacle_BuildsTwoCellRamps()
        {
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(new[] { 0, 0, 0, 2, 0, 0, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 0, 0, 1, 2, 1, 0, 0 }, beltY);
            Assert.AreEqual(new[] { true, true, true, true, true, true, true }, feasible);
        }

        [Test]
        public void ObstacleTooCloseToEnds_MarksEndpointsInfeasible()
        {
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(new[] { 0, 2, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 1, 2, 1 }, beltY);
            Assert.AreEqual(new[] { false, true, false }, feasible);
        }

        [Test]
        public void ExistingUpRampBaseline_IsIdempotent()
        {
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(new[] { 1, 1, 2 }, 1, 2, -1);
            Assert.AreEqual(new[] { 1, 1, 2 }, beltY);
            Assert.AreEqual(new[] { true, true, true }, feasible);
        }

        [Test]
        public void ObstacleAtCorner_FlattensIntoPlateau()
        {
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(new[] { 0, 0, 1, 0, 0 }, 0, 0, 2);
            Assert.AreEqual(new[] { 0, 1, 1, 1, 0 }, beltY);
            Assert.AreEqual(new[] { true, true, true, true, true }, feasible);
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConveyorVerticalEnvelopeTest"`
Expected: コンパイルエラー（`ConveyorVerticalEnvelope` 未定義）で FAIL。

- [ ] **Step 3: 実装を書く**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/ConveyorVerticalEnvelope.cs`:

```csharp
using System;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // 障害物クリア下限・隣接差≤1・端点固定を満たす最小のベルト高さプロファイルを求める純粋ロジック
    // Pure logic computing the minimal belt-height profile satisfying clearance lower bounds, adjacency<=1, and fixed endpoints.
    public static class ConveyorVerticalEnvelope
    {
        public static (int[] beltY, bool[] feasible) Solve(int[] lowerBounds, int fixedStart, int fixedEnd, int cornerIndex)
        {
            var n = lowerBounds.Length;
            if (n == 0) return (Array.Empty<int>(), Array.Empty<bool>());

            // 2パスで下限と隣接差≤1を満たす最小プロファイルを求める
            // Two passes give the minimal profile satisfying lower bounds and adjacency<=1.
            var y = TwoPass(lowerBounds);

            // コーナーは勾配を通せないため前後3セルを平坦な踊り場に引き上げて再計算する
            // The corner cannot carry a slope, so raise its 3-cell neighborhood into a flat plateau and re-solve.
            if (cornerIndex >= 1 && cornerIndex <= n - 2 && !(y[cornerIndex - 1] == y[cornerIndex] && y[cornerIndex] == y[cornerIndex + 1]))
            {
                var plateau = Math.Max(y[cornerIndex - 1], Math.Max(y[cornerIndex], y[cornerIndex + 1]));
                var raised = (int[])lowerBounds.Clone();
                raised[cornerIndex - 1] = Math.Max(raised[cornerIndex - 1], plateau);
                raised[cornerIndex] = Math.Max(raised[cornerIndex], plateau);
                raised[cornerIndex + 1] = Math.Max(raised[cornerIndex + 1], plateau);
                y = TwoPass(raised);
            }

            // 端点は固定値。包絡線がそれを超えて上がったらランプを戻しきれない＝設置不可
            // Endpoints are fixed. If the envelope rose above them, the ramp cannot return -> not placeable.
            var feasible = new bool[n];
            for (var i = 0; i < n; i++) feasible[i] = true;
            feasible[0] = y[0] == fixedStart;
            feasible[n - 1] = y[n - 1] == fixedEnd;

            return (y, feasible);

            #region Internal

            int[] TwoPass(int[] bounds)
            {
                var r = (int[])bounds.Clone();
                for (var i = 1; i < n; i++) r[i] = Math.Max(r[i], r[i - 1] - 1);
                for (var i = n - 2; i >= 0; i--) r[i] = Math.Max(r[i], r[i + 1] - 1);
                return r;
            }

            #endregion
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConveyorVerticalEnvelopeTest"`
Expected: 6 テスト全て PASS。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/ConveyorVerticalEnvelope.cs moorestech_client/Assets/Scripts/Client.Tests/ConveyorVerticalEnvelopeTest.cs
git commit -m "feat: ベルト立体交差の垂直包絡線アルゴリズムを追加"
```

---

### Task 2: ConveyorObstacleScanner（障害物スキャン）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/ConveyorObstacleScanner.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/ConveyorObstacleScannerTest.cs`

**Interfaces:**
- Consumes: なし。
- Produces: `new ConveyorObstacleScanner().ComputeLowerBounds(IReadOnlyList<Vector3Int> cells, Func<Vector3Int,bool> isOccupied)` → `int[]`。各セルの基準Yから連続占有スタックの直上Yを返す（占有なしなら `cell.y`）。

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/ConveyorObstacleScannerTest.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    public class ConveyorObstacleScannerTest
    {
        private static readonly List<Vector3Int> FlatPath = new()
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(2, 0, 0),
        };

        [Test]
        public void NoObstacle_ReturnsBaseY()
        {
            var bounds = new ConveyorObstacleScanner().ComputeLowerBounds(FlatPath, _ => false);
            Assert.AreEqual(new[] { 0, 0, 0 }, bounds);
        }

        [Test]
        public void Height1Obstacle_RaisesByOne()
        {
            var occupied = new HashSet<Vector3Int> { new(1, 0, 0) };
            var bounds = new ConveyorObstacleScanner().ComputeLowerBounds(FlatPath, occupied.Contains);
            Assert.AreEqual(new[] { 0, 1, 0 }, bounds);
        }

        [Test]
        public void Height2Obstacle_RaisesByTwo()
        {
            var occupied = new HashSet<Vector3Int> { new(1, 0, 0), new(1, 1, 0) };
            var bounds = new ConveyorObstacleScanner().ComputeLowerBounds(FlatPath, occupied.Contains);
            Assert.AreEqual(new[] { 0, 2, 0 }, bounds);
        }

        [Test]
        public void FloatingBlockAboveFreeBase_DoesNotRaise()
        {
            var occupied = new HashSet<Vector3Int> { new(1, 2, 0) };
            var bounds = new ConveyorObstacleScanner().ComputeLowerBounds(FlatPath, occupied.Contains);
            Assert.AreEqual(new[] { 0, 0, 0 }, bounds);
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConveyorObstacleScannerTest"`
Expected: コンパイルエラー（`ConveyorObstacleScanner` 未定義）で FAIL。

- [ ] **Step 3: 実装を書く**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/ConveyorObstacleScanner.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // 経路各セルの直上に積み上がった既存ブロックを調べ、跨ぐのに必要な最小ベルト高さ下限を返す
    // Scans existing blocks stacked above each path cell and returns the minimal belt-height lower bound to clear them.
    public class ConveyorObstacleScanner
    {
        // 無限ループ防止の安全上限
        // Safety cap to prevent an infinite scan loop.
        private const int MaxScanHeight = 64;

        public int[] ComputeLowerBounds(IReadOnlyList<Vector3Int> cells, Func<Vector3Int, bool> isOccupied)
        {
            var bounds = new int[cells.Count];
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];

                // 基準Yから連続して占有されている高さを上に辿り、その直上を下限とする
                // Walk up the contiguous occupied stack from base Y; the cell just above becomes the lower bound.
                var y = cell.y;
                var scanned = 0;
                while (scanned < MaxScanHeight && isOccupied(new Vector3Int(cell.x, y, cell.z)))
                {
                    y++;
                    scanned++;
                }

                bounds[i] = y;
            }
            return bounds;
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConveyorObstacleScannerTest"`
Expected: 4 テスト全て PASS。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/ConveyorObstacleScanner.cs moorestech_client/Assets/Scripts/Client.Tests/ConveyorObstacleScannerTest.cs
git commit -m "feat: ベルト経路の障害物スキャナを追加"
```

---

### Task 3: ConveyorOverpassRaiser（PlaceInfo列への反映）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/ConveyorOverpassRaiser.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/ConveyorOverpassRaiserTest.cs`

**Interfaces:**
- Consumes: `ConveyorObstacleScanner.ComputeLowerBounds`、`ConveyorVerticalEnvelope.Solve`、`Server.Protocol.PacketResponse.PlaceInfo`（`Position`/`VerticalDirection`/`Placeable` は可変）、`Game.Block.Interface.BlockVerticalDirection`（`Up`/`Horizontal`/`Down`）。
- Produces: `new ConveyorOverpassRaiser().Raise(List<PlaceInfo> placeInfos, int cornerIndex, Func<Vector3Int,bool> isOccupied)`（void、引数 `placeInfos` を破壊的に更新）。障害物が無ければ何も変えない（縦方向も維持）。

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/ConveyorOverpassRaiserTest.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests
{
    public class ConveyorOverpassRaiserTest
    {
        private static List<PlaceInfo> FlatHorizontalPath(int length)
        {
            // 始点から+X方向に水平に並ぶベルト列を作る
            // Build a horizontal belt row extending in +X from the origin.
            var list = new List<PlaceInfo>();
            for (var x = 0; x < length; x++)
            {
                list.Add(new PlaceInfo
                {
                    Position = new Vector3Int(x, 0, 0),
                    Direction = BlockDirection.East,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    Placeable = true,
                });
            }
            return list;
        }

        [Test]
        public void NoObstacle_LeavesEverythingUnchanged()
        {
            var infos = FlatHorizontalPath(3);
            new ConveyorOverpassRaiser().Raise(infos, 2, _ => false);

            for (var x = 0; x < 3; x++)
            {
                Assert.AreEqual(new Vector3Int(x, 0, 0), infos[x].Position);
                Assert.AreEqual(BlockVerticalDirection.Horizontal, infos[x].VerticalDirection);
                Assert.IsTrue(infos[x].Placeable);
            }
        }

        [Test]
        public void Height1ObstacleInMiddle_BuildsOverpass()
        {
            var infos = FlatHorizontalPath(5);
            var occupied = new HashSet<Vector3Int> { new(2, 0, 0) };
            new ConveyorOverpassRaiser().Raise(infos, 4, occupied.Contains);

            // Y: 0,0,1,0,0 のオーバーパス（手前で登り、上を渡り、先で下る）
            // Y profile 0,0,1,0,0: ramp up, cross over, ramp down.
            Assert.AreEqual(0, infos[0].Position.y);
            Assert.AreEqual(0, infos[1].Position.y);
            Assert.AreEqual(1, infos[2].Position.y);
            Assert.AreEqual(0, infos[3].Position.y);
            Assert.AreEqual(0, infos[4].Position.y);

            Assert.AreEqual(BlockVerticalDirection.Horizontal, infos[0].VerticalDirection);
            Assert.AreEqual(BlockVerticalDirection.Up, infos[1].VerticalDirection);
            Assert.AreEqual(BlockVerticalDirection.Horizontal, infos[2].VerticalDirection);
            Assert.AreEqual(BlockVerticalDirection.Down, infos[3].VerticalDirection);
            Assert.AreEqual(BlockVerticalDirection.Horizontal, infos[4].VerticalDirection);
        }

        [Test]
        public void TallObstacleWithNoRampRoom_MarksEndpointsUnplaceable()
        {
            // 高さ2の障害物を3セルで跨ぐにはランプ長が足りない（高さ1・3セルは跨げてしまう点に注意）
            // A height-2 obstacle has no room to ramp within 3 cells (note: a height-1 obstacle in 3 cells IS crossable).
            var infos = FlatHorizontalPath(3);
            var occupied = new HashSet<Vector3Int> { new(1, 0, 0), new(1, 1, 0) };
            new ConveyorOverpassRaiser().Raise(infos, 2, occupied.Contains);

            // 端点を固定高さに戻しきれず両端が設置不可になる
            // The endpoints cannot return to the fixed height, so both ends become unplaceable.
            Assert.IsFalse(infos[0].Placeable);
            Assert.IsFalse(infos[2].Placeable);
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConveyorOverpassRaiserTest"`
Expected: コンパイルエラー（`ConveyorOverpassRaiser` 未定義）で FAIL。

- [ ] **Step 3: 実装を書く**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/ConveyorOverpassRaiser.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // 障害物スキャン→包絡線→PlaceInfo列にY上昇・縦方向再計算・設置可否を反映する
    // Scan obstacles -> envelope -> apply raised Y, recompute vertical direction, and mark placeability on the PlaceInfo list.
    public class ConveyorOverpassRaiser
    {
        private readonly ConveyorObstacleScanner _scanner = new();

        public void Raise(List<PlaceInfo> placeInfos, int cornerIndex, Func<Vector3Int, bool> isOccupied)
        {
            if (placeInfos.Count == 0) return;

            // 障害物下限から最終ベルト高さプロファイルと端点可否を求める
            // Compute the final belt-height profile and endpoint feasibility from the obstacle lower bounds.
            var cells = placeInfos.Select(info => info.Position).ToList();
            var lowerBounds = _scanner.ComputeLowerBounds(cells, isOccupied);
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(lowerBounds, cells[0].y, cells[^1].y, cornerIndex);

            // 上昇したセルとその隣接のみ縦方向を再計算する（無関係なセルの既存方向は維持）
            // Recompute vertical direction only for raised cells and their neighbors (untouched cells keep their existing direction).
            var raised = new bool[placeInfos.Count];
            for (var i = 0; i < placeInfos.Count; i++) raised[i] = beltY[i] != cells[i].y;

            for (var i = 0; i < placeInfos.Count; i++)
            {
                var info = placeInfos[i];
                var pos = info.Position;
                pos.y = beltY[i];
                info.Position = pos;

                if (NeighborhoodChanged(i)) info.VerticalDirection = ResolveVertical(i);
                if (!feasible[i]) info.Placeable = false;
            }

            #region Internal

            bool NeighborhoodChanged(int i)
            {
                if (raised[i]) return true;
                if (i > 0 && raised[i - 1]) return true;
                if (i + 1 < raised.Length && raised[i + 1]) return true;
                return false;
            }

            BlockVerticalDirection ResolveVertical(int i)
            {
                // 次が高ければ登り、前が高ければ下り、それ以外は水平
                // Up if the next cell is higher, Down if the previous is higher, otherwise Horizontal.
                if (i + 1 < beltY.Length && beltY[i + 1] > beltY[i]) return BlockVerticalDirection.Up;
                if (i - 1 >= 0 && beltY[i - 1] > beltY[i]) return BlockVerticalDirection.Down;
                return BlockVerticalDirection.Horizontal;
            }

            #endregion
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConveyorOverpassRaiserTest"`
Expected: 3 テスト全て PASS。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/ConveyorOverpassRaiser.cs moorestech_client/Assets/Scripts/Client.Tests/ConveyorOverpassRaiserTest.cs
git commit -m "feat: 障害物オーバーパスをPlaceInfo列に反映するRaiserを追加"
```

---

### Task 4: CommonBlockPlacePointCalculator への配線

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlacePointCalculator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/CommonBlockPlacePointCalculatorTest.cs:117-124`

**Interfaces:**
- Consumes: `ConveyorOverpassRaiser.Raise`。`BlockGameObjectDataStore.IsOverlapPositionInfo(BlockPositionInfo)`。`Game.Block.Interface.BlockPositionInfo(Vector3Int, BlockDirection, Vector3Int)`。
- Produces: 静的 `CommonBlockPlacePointCalculator.CalculatePoint(Vector3Int, Vector3Int, bool, BlockDirection, BlockMasterElement, Func<PlaceInfo,BlockMasterElement,bool>, Func<Vector3Int,bool>)`（`isOccupied` 引数を末尾追加）。インスタンス側 5 引数 `CalculatePoint` のシグネチャは不変。

- [ ] **Step 1: 既存テストを新シグネチャに更新（まだ実装が無いので失敗する）**

`CommonBlockPlacePointCalculatorTest.cs` の静的呼び出し（現 117-124 行）を次に変更。**期待値（ExpectedPoints）は一切変更しない。**

```csharp
            List<PlaceInfo> actual = CommonBlockPlacePointCalculator.CalculatePoint(
                testCase.PlaceStartPoint,
                testCase.PlaceEndPoint,
                isStartDirectionZ,
                BlockDirection.North,
                blockMasterElement,
                (_, _) => true,
                _ => false
            );
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CommonBlockPlacePointCalculatorTest"`
Expected: コンパイルエラー（引数の数不一致）で FAIL。

- [ ] **Step 3: 静的 `CalculatePoint` に `isOccupied` 引数を追加し後段に Raiser を呼ぶ**

`CommonBlockPlacePointCalculator.cs` 28 行目の静的メソッドシグネチャを変更:

```csharp
        public static List<PlaceInfo> CalculatePoint(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockMasterElement holdingBlockMasterElement, Func<PlaceInfo, BlockMasterElement, bool> isNotExistBlock, Func<Vector3Int, bool> isOccupied)
```

同メソッド本体の `CalcPlaceDirection` 呼び出し直後（現 38-39 行付近）を次に変更:

```csharp
            List<PlaceInfo> placeInfos = CalcPlaceDirection(positions);

            // 障害物を自動で跨ぐ立体交差プロファイルを後段で重ねる（コンベア配置時のみ）
            // Layer the auto-overpass profile that steps over obstacles (conveyor placement only).
            if (enableConveyorPlacement)
            {
                new ConveyorOverpass.ConveyorOverpassRaiser().Raise(placeInfos, startToCornerDistance, isOccupied);
            }

            placeInfos = CalcPlaceable(placeInfos);

            return placeInfos;
```

- [ ] **Step 4: インスタンス側で `isOccupied` probe を供給**

`CommonBlockPlacePointCalculator.cs` 23-26 行のインスタンスメソッドを変更:

```csharp
        public List<PlaceInfo> CalculatePoint(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockMasterElement holdingBlockMasterElement)
        {
            return CalculatePoint(startPoint, endPoint, isStartDirectionZ, blockDirection, holdingBlockMasterElement, IsNotExistBlock, IsOccupied);
        }
```

クラス末尾の `IsNotExistBlock` メソッドの下（現 374 行付近、`#if UNITY_EDITOR` ブロックがあればその前）に追加:

```csharp
        // 1×1×1セルに既存ブロックが存在するか（障害物スキャン用）
        // Whether a 1x1x1 cell is occupied by an existing block (used by obstacle scanning).
        private bool IsOccupied(Vector3Int cell)
        {
            var positionInfo = new BlockPositionInfo(cell, BlockDirection.North, Vector3Int.one);
            return _blockGameObjectDataStore.IsOverlapPositionInfo(positionInfo);
        }
```

- [ ] **Step 5: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラーなし。（"Domain Reload in progress" が出たら 45 秒待ってリトライ。）

- [ ] **Step 6: 既存テスト + 新規ユニットテストが全て通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CommonBlockPlacePointCalculatorTest|ConveyorVerticalEnvelopeTest|ConveyorObstacleScannerTest|ConveyorOverpassRaiserTest"`
Expected: 全テスト PASS（既存6ケースは期待値不変で通る＝冪等性の確認）。

- [ ] **Step 7: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlacePointCalculator.cs moorestech_client/Assets/Scripts/Client.Tests/CommonBlockPlacePointCalculatorTest.cs
git commit -m "feat: ベルト経路計算に自動立体交差レイヤーを配線"
```

---

### Task 5: ランタイム搬送検証（PlayMode テスト）

形状が正しくても Up/Horizontal/Down が実際に接続して搬送しなければ無意味。実ゲーム起動でアイテムが立体交差を通過することを検証する。
REQUIRED: playmode-test スキルのテンプレート/制約に従う。

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlayModeTest/ConveyorOverpassRuntimeTest.cs`

**Interfaces:**
- Consumes: `Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil`（`EnterPlayModeUtil`/`LoadMainGame`/`PlaceBlock`/`InsertItemToBlock`）、`ConveyorOverpassRaiser.Raise`、`Server.Protocol.PacketResponse.PlaceInfo`、`Game.Context.ServerContext.WorldBlockDatastore.Exists(Vector3Int)`、`Core.Update.GameUpdater`、`Game.Block.Blocks.Chest.VanillaChestComponent`。
- `EditModeInPlayingTestMod` のブロック名: ベルト=「直進高速ベルトコンベア」「上り高速ベルトコンベア」「下り高速ベルトコンベア」、障害物機械=「釜」(1×1×1)、シンク=「量子チェスト」(1×1×1)。

**設計の要点:** 座標を手で推測すると Up/Down の接続オフセットを誤りやすい。よって**実際の `ConveyorOverpassRaiser` が出力する `Position` と `VerticalDirection` をそのまま設置する**。VerticalDirection→バリアント名の対応はサーバーの `GetVerticalOverrideBlockId` と同じ意味。これでテストの設置列＝機能が emit する列となり、接続が繋がるか（＝本当に検証したいこと）だけを問う。

- [ ] **Step 1: テストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlayModeTest/ConveyorOverpassRuntimeTest.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using Client.Tests.EditModeInPlayingTest.Util;
using Core.Master;
using Core.Update;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.PlayModeTest
{
    public class ConveyorOverpassRuntimeTest
    {
        [UnityTest]
        public IEnumerator ItemTraversesOverpassOverObstacle()
        {
            // PlayMode に遷移しゲームを起動する
            // Enter PlayMode and boot the game.
            yield return EditModeInPlayingTestUtil.EnterPlayModeUtil();
            LogAssert.ignoreFailingMessages = true;
            yield return TestBody().ToCoroutine();
            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask TestBody()
            {
                await EditModeInPlayingTestUtil.LoadMainGame();

                // 障害物の釜を経路中央(x=2)に置く。ベルトはその上を跨ぐ。
                // Place the obstacle (kiln) at the path center (x=2). The belt steps over it.
                EditModeInPlayingTestUtil.PlaceBlock("釜", new Vector3Int(2, 0, 0), BlockDirection.East);

                // +X方向に水平に並ぶ5セルのベルト列を組み、Raiser で立体交差プロファイルを生成する
                // Build a 5-cell horizontal belt row in +X and let the Raiser generate the overpass profile.
                var infos = new List<PlaceInfo>();
                for (var x = 0; x < 5; x++)
                {
                    infos.Add(new PlaceInfo
                    {
                        Position = new Vector3Int(x, 0, 0),
                        Direction = BlockDirection.East,
                        VerticalDirection = BlockVerticalDirection.Horizontal,
                        Placeable = true,
                    });
                }
                new ConveyorOverpassRaiser().Raise(infos, 4, cell => ServerContext.WorldBlockDatastore.Exists(cell));

                // Raiser が出力した座標と縦方向どおりにベルトを設置する
                // Place belts exactly at the positions and vertical directions the Raiser produced.
                IBlock firstBelt = null;
                foreach (var info in infos)
                {
                    if (!info.Placeable) continue;
                    var name = VariantName(info.VerticalDirection);
                    var belt = EditModeInPlayingTestUtil.PlaceBlock(name, info.Position, info.Direction);
                    firstBelt ??= belt;
                }

                // 末尾ベルト(x=4,y=0,East)の出力先(x=5,y=0)にシンクのチェストを置く
                // Place the sink chest at the output of the last belt (x=4,y=0,East) -> (x=5,y=0).
                var sinkChest = EditModeInPlayingTestUtil.PlaceBlock("量子チェスト", new Vector3Int(5, 0, 0), BlockDirection.East);

                // 先頭ベルトにアイテムを挿入する
                // Insert an item into the first belt.
                var itemId = new ItemId(1);
                EditModeInPlayingTestUtil.InsertItemToBlock(firstBelt, itemId, 1);

                // 到達するまで tick を回す（タイムアウト付き）
                // Tick until the item arrives (with timeout).
                var chest = sinkChest.GetComponent<VanillaChestComponent>();
                var arrived = false;
                for (var tick = 0; tick < 600 && !arrived; tick++)
                {
                    GameUpdater.UpdateOneTick();
                    arrived = ChestHasItem(chest, itemId);
                    await UniTask.Yield();
                }

                Assert.IsTrue(arrived, "立体交差を越えてアイテムがチェストに到達しなかった / item did not reach the sink chest over the overpass");
            }

            string VariantName(BlockVerticalDirection vertical)
            {
                // 縦方向に対応するベルトバリアント名（サーバーのGetVerticalOverrideBlockIdと同義）
                // Belt variant name for the vertical direction (mirrors the server's GetVerticalOverrideBlockId).
                if (vertical == BlockVerticalDirection.Up) return "上り高速ベルトコンベア";
                if (vertical == BlockVerticalDirection.Down) return "下り高速ベルトコンベア";
                return "直進高速ベルトコンベア";
            }

            bool ChestHasItem(VanillaChestComponent chest, ItemId targetItemId)
            {
                // チェスト内に対象アイテムが入ったか確認する
                // Check whether the target item landed in the chest.
                foreach (var stack in chest.InventoryItems)
                {
                    if (stack.Id == targetItemId && stack.Count > 0) return true;
                }
                return false;
            }

            #endregion
        }
    }
}
```

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラーなし。`VanillaChestComponent.InventoryItems` / `WorldBlockDatastore.Exists` / ブロック名等のAPI名が異なる場合は実在シンボルに合わせて修正（`grep` で確認）。

- [ ] **Step 3: PlayMode テストを実行**

Run: `uloop run-tests --port 56902 --project-path ./moorestech_client --filter-type regex --filter-value "Client\\.Tests\\.PlayModeTest\\.ConveyorOverpassRuntimeTest"`
Expected: PASS。ドメインリロードのため結果報告は遅延する。"Domain Reload in progress" エラー時は 45 秒待ってリトライ。結果は `~/Library/Application Support/sakastudio/moorestech/TestResults.xml` でも確認可能。

- [ ] **Step 4: 失敗時の調査（接続が繋がらない場合）**

到達しない場合、Up/Down ベルトの接続方向・配置Yが production の接続ジオメトリと一致しているかを `uloop get-logs --project-path ./moorestech_client --log-type Error` と配置座標で確認。Raiser が出力する (Y, VerticalDirection) の並びと、本テストで手置きした並びが一致しているはずなので、ズレがあれば Raiser 側 or テスト座標を修正。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/PlayModeTest/ConveyorOverpassRuntimeTest.cs
git commit -m "test: ベルト立体交差のランタイム搬送PlayModeテストを追加"
```

---

## Self-Review

**Spec coverage:**
- 上に跨ぐのみ / 障害物高さ+1 → Task 1 Solve（needY = スタック上端+1, 上昇のみ）✓
- L字経路保持 → 既存 XZ 経路を変更せず Y のみ後段で操作 ✓
- 高さ違いドラッグ対応 → Solve の端点固定 `fixedStart`/`fixedEnd`、下限合成（Task 1 idempotent テスト）✓
- 跨げない場合 `Placeable=false` → Task 1 feasible / Task 3 ObstacleTooClose ✓
- コーナー制約 → Task 1 ObstacleAtCorner プラトー ✓
- サーバー変更不要 → Task に server 変更なし ✓
- 既存テスト不変 → Task 4 Step 6 で確認（冪等性）✓
- ランタイム正当性 → Task 5 ✓

**Placeholder scan:** プレースホルダなし。全コードブロックは実コード。Task 5 のみ API 名差異の可能性を明記しコンパイルで担保。

**Type consistency:** `Solve` 戻り `(int[] beltY, bool[] feasible)`、`ComputeLowerBounds` 戻り `int[]`、`Raise` は void 破壊更新、`BlockVerticalDirection.Up/Horizontal/Down`、`isOccupied: Func<Vector3Int,bool>` — Task 1〜4 で一貫。
