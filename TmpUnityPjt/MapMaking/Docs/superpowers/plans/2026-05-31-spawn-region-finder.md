# Spawn リージョン探索プリパス Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 本番生成前に海陸＋バイオーム分類だけを先行計算し、「草原と森林が隣接し十分な広さを持つ場所」を探索してスポーン地点を確定、生成マップ中央にその地点が来るようワールドオフセットを算出する。

**Architecture:** 2 段階方式。段1 は粗グリッドに本番同一の `ClassificationJob`(raw) を走らせ候補ペアを発見。段2 は候補周辺の局所窓に本番同一の post-blur 分類パイプラインを走らせ final winner 上で再判定し、草原連結成分内の pole of inaccessibility をスポーン点 S とする。`G = S − gridCenter` を `InfiniteTerrainManager` 内で各チャンクの `worldOffset` と `Terrain.position` 両方に加算する（ScriptableObject は不変）。

**Tech Stack:** Unity 2022+ / C# / Unity DOTS(Burst Jobs) / Unity Test Runner(NUnit EditMode) / uloop CLI（コンパイル・テスト実行）。

**設計仕様:** `docs/superpowers/specs/2026-05-31-spawn-region-finder-design.md`

---

## File Structure

新規ディレクトリ `Assets/MapGenerator/Pipeline/Spawn/` に探索ロジックを集約する。純粋ロジック（連結成分・距離変換・スコア・オフセット算出）は Burst/Unity 非依存にして単体テスト可能にする。

**新規作成:**
- `Assets/MapGenerator/Pipeline/Spawn/SpawnSearchConfig.cs` — 走査パラメータ群（`[System.Serializable]`、`TerrainGenerationConfig` に内包）
- `Assets/MapGenerator/Pipeline/Spawn/SpawnSearchResult.cs` — 探索結果の値型
- `Assets/MapGenerator/Pipeline/Spawn/ConnectedComponents.cs` — 4近傍 flood-fill による連結成分抽出・面積・隣接接触長（純粋）
- `Assets/MapGenerator/Pipeline/Spawn/DistanceTransform.cs` — 多段距離変換＋pole of inaccessibility 選定（純粋）
- `Assets/MapGenerator/Pipeline/Spawn/CoarseBiomeGrid.cs` — 段1 粗グリッド raw 分類のラッパ（cell↔world 変換）
- `Assets/MapGenerator/Pipeline/Spawn/SpawnRegionFinder.cs` — 段1+段2+採用+オフセット算出のオーケストレータ
- `Assets/MapGenerator/Tests/EditMode/Spawn/ConnectedComponentsTests.cs`
- `Assets/MapGenerator/Tests/EditMode/Spawn/DistanceTransformTests.cs`
- `Assets/MapGenerator/Tests/EditMode/Spawn/SpawnRegionFinderTests.cs`

**変更:**
- `Assets/MapGenerator/Pipeline/Jobs/SmallSeaRemovalJob.cs` — `protectEdgeRegions` フラグ追加（boundary-aware）
- `Assets/MapGenerator/Pipeline/TerrainGenerator.cs` — 段1 用 `ClassifyRawGrid` と段2 用 `RunClassificationDetailed` の internal static API 追加、`RunClassificationPipeline` に SmallSeaRemoval 保護フラグを伝播
- `Assets/MapGenerator/Pipeline/Config/TerrainGenerationConfig.cs` — `useSpawnOffsetSearch` と `spawnSearch` フィールド追加
- `Assets/MapGenerator/InfiniteTerrainManager.cs` — `activeSpawnOffset` 適用
- `Assets/MapGenerator/Editor/MapGeneratorEditor.cs` (または `InfiniteTerrainManagerEditor.cs`) — 任意の「Find Spawn Offset」デバッグボタン

> **コンパイル確認の共通コマンド:** 各タスクの実装後は uloop-compile スキルでコンパイルし、エラー 0 を確認する。テスト実行は uloop-run-tests スキル（EditMode）。本プランの「Run:」表記はそれらのスキル経由で実行する意味。

---

## Task 1: SpawnSearchConfig と TerrainGenerationConfig への配線

**Files:**
- Create: `Assets/MapGenerator/Pipeline/Spawn/SpawnSearchConfig.cs`
- Create: `Assets/MapGenerator/Pipeline/Spawn/SpawnSearchResult.cs`
- Modify: `Assets/MapGenerator/Pipeline/Config/TerrainGenerationConfig.cs`（末尾 `}` の直前にフィールド追加）

- [ ] **Step 1: SpawnSearchConfig を作成**

```csharp
using UnityEngine;

namespace MapGenerator.Pipeline.Spawn
{
    /// <summary>
    /// スポーン候補探索プリパスのパラメータ。TerrainGenerationConfig に内包される。
    /// 面積はすべて m² 単位（セル数だと解像度変更で意味が壊れるため）。
    /// </summary>
    [System.Serializable]
    public class SpawnSearchConfig
    {
        [Header("段1: 粗探索")]
        [Tooltip("段1粗グリッドのセルサイズ(m)")]
        public float scanCellSize = 50f;
        [Tooltip("段1走査範囲(正方, m)。0以下なら生成グリッド外接を自動使用")]
        public float scanExtent = 0f;

        [Header("段2: 局所窓")]
        [Tooltip("段2局所窓の候補外接への追加マージン(m)")]
        public float windowMargin = 200f;

        [Header("合格条件 (m²/m)")]
        [Tooltip("草原連結成分の最小面積(m²)")]
        public float minGrasslandArea = 200000f;
        [Tooltip("森林連結成分の最小面積(m²)")]
        public float minForestArea = 150000f;
        [Tooltip("草原-森林 境界接触長の最小値(m)")]
        public float minBorderContact = 200f;

        [Header("スポーン点クリアランス (m)")]
        [Tooltip("スポーン点の非Grassland境界からの最小距離(m)")]
        public float grassClearanceMin = 30f;
        [Tooltip("スポーン点の海/Beachからの最小距離(m)")]
        public float waterClearanceMin = 60f;

        [Header("スコア重み")]
        public float wGrasslandArea = 1f;
        public float wForestArea = 0.5f;
        public float wBorderContact = 50f;
        public float wInland = 1f;

        [Header("探索制御")]
        [Tooltip("段2検証の初期バッチ件数")]
        public int topK = 32;
        [Tooltip("候補ゼロ時のscanExtent拡大率")]
        public float expandFactor = 1.8f;
        [Tooltip("拡大走査の最大回数")]
        public int maxExpandIterations = 4;
    }
}
```

- [ ] **Step 2: SpawnSearchResult を作成**

```csharp
using UnityEngine;

namespace MapGenerator.Pipeline.Spawn
{
    /// <summary>
    /// SpawnRegionFinder.Find() の結果。座標はすべてワールド(ノイズ)座標系(m)。
    /// </summary>
    public readonly struct SpawnSearchResult
    {
        /// <summary>探索成功（valid候補が見つかった）か。falseならフォールバック。</summary>
        public readonly bool Success;
        /// <summary>生成グリッドに加算するグローバルオフセット G = S - gridCenter。</summary>
        public readonly Vector2 WorldOffset;
        /// <summary>確定したスポーン地点 S（草原final CC内 pole of inaccessibility）。</summary>
        public readonly Vector2 SpawnWorldPosition;
        /// <summary>採用候補の final score。</summary>
        public readonly float Score;
        /// <summary>診断ログ（採用経緯・拡大回数・打ち切り有無）。</summary>
        public readonly string Diagnostics;

        public SpawnSearchResult(bool success, Vector2 worldOffset, Vector2 spawn,
            float score, string diagnostics)
        {
            Success = success;
            WorldOffset = worldOffset;
            SpawnWorldPosition = spawn;
            Score = score;
            Diagnostics = diagnostics;
        }

        public static SpawnSearchResult Fallback(string reason) =>
            new SpawnSearchResult(false, Vector2.zero, Vector2.zero, 0f, reason);
    }
}
```

- [ ] **Step 3: TerrainGenerationConfig にフィールド追加**

`TerrainGenerationConfig.cs` の `using` に `using MapGenerator.Pipeline.Spawn;` を追加し、クラス本体の末尾（既存フィールド群の後、最後の閉じ括弧の前）に挿入する。

```csharp
        // スポーン候補探索プリパス。ONなら生成前に草原-森林隣接の良地を探索しオフセットを自動設定する
        [Header("スポーン候補探索")]
        [Label("スポーン候補探索を使う")]
        public bool useSpawnOffsetSearch = false;
        [Label("探索パラメータ")]
        public SpawnSearchConfig spawnSearch = new SpawnSearchConfig();
```

- [ ] **Step 4: コンパイル確認**

Run: uloop-compile
Expected: エラー 0。`SpawnSearchConfig` が Inspector に表示される。

- [ ] **Step 5: Commit**

```bash
git add Assets/MapGenerator/Pipeline/Spawn/SpawnSearchConfig.cs \
        Assets/MapGenerator/Pipeline/Spawn/SpawnSearchResult.cs \
        Assets/MapGenerator/Pipeline/Config/TerrainGenerationConfig.cs
git commit -m "feat(spawn): add SpawnSearchConfig and result types"
```

---

## Task 2: ConnectedComponents（純粋ロジック・TDD）

整数バイオームグリッド上で、述語に一致するセルの 4 近傍連結成分を抽出し、面積（セル数）・バウンディングボックス・セル一覧を返す。2 つの成分間の境界接触セル数も計算する。

**Files:**
- Create: `Assets/MapGenerator/Pipeline/Spawn/ConnectedComponents.cs`
- Test: `Assets/MapGenerator/Tests/EditMode/Spawn/ConnectedComponentsTests.cs`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using NUnit.Framework;
using MapGenerator.Pipeline.Spawn;

namespace MapGenerator.Tests.EditMode.Spawn
{
    public class ConnectedComponentsTests
    {
        // グリッド: 1=対象, 0=非対象。3x3で中央十字を1つの成分として検出
        [Test]
        public void Label_SingleCrossComponent_AreaIs5()
        {
            // 0 1 0
            // 1 1 1
            // 0 1 0
            int[] grid = { 0,1,0, 1,1,1, 0,1,0 };
            var comps = ConnectedComponents.Label(grid, 3, 3, v => v == 1);
            Assert.AreEqual(1, comps.Count);
            Assert.AreEqual(5, comps[0].Area);
        }

        [Test]
        public void Label_TwoSeparateComponents_AreSeparate()
        {
            // 1 0 1
            // 1 0 1
            int[] grid = { 1,0,1, 1,0,1 };
            var comps = ConnectedComponents.Label(grid, 3, 2, v => v == 1);
            Assert.AreEqual(2, comps.Count);
            Assert.AreEqual(2, comps[0].Area);
            Assert.AreEqual(2, comps[1].Area);
        }

        [Test]
        public void BorderContact_AdjacentComponents_CountsSharedEdges()
        {
            // 左列=A(値1), 右列=B(値2), 中央列=0 ではなく直接隣接させる
            // 1 2
            // 1 2
            int[] grid = { 1,2, 1,2 };
            var aComps = ConnectedComponents.Label(grid, 2, 2, v => v == 1);
            var bComps = ConnectedComponents.Label(grid, 2, 2, v => v == 2);
            int contact = ConnectedComponents.BorderContactCells(
                aComps[0], bComps[0], grid, 2, 2);
            // A列(x=0)とB列(x=1)が縦2セル分隣接 → 2
            Assert.AreEqual(2, contact);
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: uloop-run-tests（EditMode, filter `ConnectedComponentsTests`）
Expected: コンパイルエラー（`ConnectedComponents` 未定義）で FAIL。

- [ ] **Step 3: ConnectedComponents を実装**

```csharp
using System;
using System.Collections.Generic;

namespace MapGenerator.Pipeline.Spawn
{
    /// <summary>
    /// 整数グリッド上の4近傍連結成分。Burst非依存の純粋ロジックで単体テスト可能。
    /// </summary>
    public static class ConnectedComponents
    {
        public sealed class Component
        {
            public int Label;
            public readonly List<int> Cells = new List<int>(); // index = y*width + x
            public int Area => Cells.Count;
            public int MinX = int.MaxValue, MinY = int.MaxValue;
            public int MaxX = int.MinValue, MaxY = int.MinValue;

            public void Add(int x, int y, int width)
            {
                Cells.Add(y * width + x);
                if (x < MinX) MinX = x;
                if (y < MinY) MinY = y;
                if (x > MaxX) MaxX = x;
                if (y > MaxY) MaxY = y;
            }
        }

        /// <summary>
        /// predicate(値) が真のセルを4近傍で連結成分に分割する。面積降順でソートして返す。
        /// </summary>
        public static List<Component> Label(int[] grid, int width, int height, Func<int, bool> predicate)
        {
            var labels = new int[grid.Length];
            for (int i = 0; i < labels.Length; i++) labels[i] = -1;
            var result = new List<Component>();
            var stack = new Stack<int>();

            for (int start = 0; start < grid.Length; start++)
            {
                if (labels[start] != -1) continue;
                if (!predicate(grid[start])) continue;

                var comp = new Component { Label = result.Count };
                labels[start] = comp.Label;
                stack.Push(start);

                while (stack.Count > 0)
                {
                    int idx = stack.Pop();
                    int x = idx % width;
                    int y = idx / width;
                    comp.Add(x, y, width);

                    TryPush(x - 1, y, width, height, grid, predicate, labels, comp.Label, stack);
                    TryPush(x + 1, y, width, height, grid, predicate, labels, comp.Label, stack);
                    TryPush(x, y - 1, width, height, grid, predicate, labels, comp.Label, stack);
                    TryPush(x, y + 1, width, height, grid, predicate, labels, comp.Label, stack);
                }
                result.Add(comp);
            }

            result.Sort((a, b) => b.Area.CompareTo(a.Area));
            return result;
        }

        static void TryPush(int x, int y, int width, int height, int[] grid,
            Func<int, bool> predicate, int[] labels, int label, Stack<int> stack)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            int idx = y * width + x;
            if (labels[idx] != -1) return;
            if (!predicate(grid[idx])) return;
            labels[idx] = label;
            stack.Push(idx);
        }

        /// <summary>
        /// 成分Aと成分Bが4近傍で隣接しているセルの数（Aセル側からカウント）。
        /// 接触長の近似（セル数×cellSize で物理長に換算）。
        /// </summary>
        public static int BorderContactCells(Component a, Component b,
            int[] grid, int width, int height)
        {
            var bSet = new HashSet<int>(b.Cells);
            int contact = 0;
            foreach (int idx in a.Cells)
            {
                int x = idx % width;
                int y = idx / width;
                if (IsNeighborIn(x - 1, y, width, height, bSet)) { contact++; continue; }
                if (IsNeighborIn(x + 1, y, width, height, bSet)) { contact++; continue; }
                if (IsNeighborIn(x, y - 1, width, height, bSet)) { contact++; continue; }
                if (IsNeighborIn(x, y + 1, width, height, bSet)) { contact++; continue; }
            }
            return contact;
        }

        static bool IsNeighborIn(int x, int y, int width, int height, HashSet<int> set)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return false;
            return set.Contains(y * width + x);
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: uloop-run-tests（EditMode, filter `ConnectedComponentsTests`）
Expected: 3 テスト PASS。

- [ ] **Step 5: Commit**

```bash
git add Assets/MapGenerator/Pipeline/Spawn/ConnectedComponents.cs \
        Assets/MapGenerator/Tests/EditMode/Spawn/ConnectedComponentsTests.cs
git commit -m "feat(spawn): add connected components labeling"
```

---

## Task 3: DistanceTransform と pole of inaccessibility（純粋ロジック・TDD）

bool マスク（成分内=true）に対し、各セルの「false 領域（境界）までのチェビシェフ距離（セル単位）」を求める 2 パス距離変換を実装。さらに 2 種の距離マスク（草原境界・水境界）から `min(grassClearance, waterClearance)` を最大化するセルを選ぶ pole of inaccessibility を実装する。

**Files:**
- Create: `Assets/MapGenerator/Pipeline/Spawn/DistanceTransform.cs`
- Test: `Assets/MapGenerator/Tests/EditMode/Spawn/DistanceTransformTests.cs`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using NUnit.Framework;
using MapGenerator.Pipeline.Spawn;

namespace MapGenerator.Tests.EditMode.Spawn
{
    public class DistanceTransformTests
    {
        [Test]
        public void ChebyshevDistance_Center_IsFarthestFromBorder()
        {
            // 5x5 全true → 中央(2,2)が境界(外側false扱い)から最遠=2
            int w = 5, h = 5;
            var mask = new bool[w * h];
            for (int i = 0; i < mask.Length; i++) mask[i] = true;
            var dist = DistanceTransform.ChebyshevToFalse(mask, w, h);
            Assert.AreEqual(1, dist[0]);                 // 角は境界距離1
            Assert.AreEqual(3, dist[2 * w + 2]);         // 中央 (外周false→2まで、+1で3)
        }

        [Test]
        public void PoleOfInaccessibility_PicksConstrainedMaxMinCell()
        {
            // grassClearance と waterClearance を別々に与え、両制約を満たす最大minセルを選ぶ
            int w = 3, h = 1;
            var grass = new float[] { 1f, 3f, 1f }; // セル
            var water = new float[] { 3f, 2f, 3f };
            // 制約 grassMin=2, waterMin=2 → セル0(grass1)失格, セル1(min(3,2)=2)合格, セル2(grass1)失格
            int best = DistanceTransform.PickPole(grass, water, w * h,
                grassMin: 2f, waterMin: 2f);
            Assert.AreEqual(1, best);
        }

        [Test]
        public void PoleOfInaccessibility_NoCellSatisfies_ReturnsMinusOne()
        {
            var grass = new float[] { 1f, 1f };
            var water = new float[] { 1f, 1f };
            int best = DistanceTransform.PickPole(grass, water, 2, grassMin: 5f, waterMin: 5f);
            Assert.AreEqual(-1, best);
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: uloop-run-tests（EditMode, filter `DistanceTransformTests`）
Expected: コンパイルエラーで FAIL。

- [ ] **Step 3: DistanceTransform を実装**

```csharp
namespace MapGenerator.Pipeline.Spawn
{
    /// <summary>
    /// マスク内セルの境界距離変換と pole of inaccessibility 選定。純粋ロジック。
    /// </summary>
    public static class DistanceTransform
    {
        /// <summary>
        /// mask[i]==true のセルについて false セル（および配列外）までのチェビシェフ距離(セル単位)を返す。
        /// false セルは距離0。2パス(前方/後方)伝播の近似チェビシェフ距離。
        /// </summary>
        public static float[] ChebyshevToFalse(bool[] mask, int width, int height)
        {
            int n = width * height;
            var d = new float[n];
            float big = width + height + 2;
            for (int i = 0; i < n; i++) d[i] = mask[i] ? big : 0f;

            // 前方パス
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (d[i] == 0f) continue;
                    float m = d[i];
                    m = Min(m, Sample(d, x - 1, y, width, height) + 1f);
                    m = Min(m, Sample(d, x, y - 1, width, height) + 1f);
                    m = Min(m, Sample(d, x - 1, y - 1, width, height) + 1f);
                    m = Min(m, Sample(d, x + 1, y - 1, width, height) + 1f);
                    d[i] = m;
                }
            // 後方パス
            for (int y = height - 1; y >= 0; y--)
                for (int x = width - 1; x >= 0; x--)
                {
                    int i = y * width + x;
                    if (d[i] == 0f) continue;
                    float m = d[i];
                    m = Min(m, Sample(d, x + 1, y, width, height) + 1f);
                    m = Min(m, Sample(d, x, y + 1, width, height) + 1f);
                    m = Min(m, Sample(d, x + 1, y + 1, width, height) + 1f);
                    m = Min(m, Sample(d, x - 1, y + 1, width, height) + 1f);
                    d[i] = m;
                }
            return d;
        }

        // 配列外は境界扱い(距離0)。これによりマスク端=境界になる。
        static float Sample(float[] d, int x, int y, int width, int height)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return 0f;
            return d[y * width + x];
        }

        static float Min(float a, float b) => a < b ? a : b;

        /// <summary>
        /// grassClearance/waterClearance(各セルの距離・物理m換算済み想定) から
        /// 両制約を満たすセルの中で min(grass, water) 最大のインデックスを返す。無ければ-1。
        /// </summary>
        public static int PickPole(float[] grassClearance, float[] waterClearance, int count,
            float grassMin, float waterMin)
        {
            int best = -1;
            float bestScore = -1f;
            for (int i = 0; i < count; i++)
            {
                float g = grassClearance[i];
                float w = waterClearance[i];
                if (g < grassMin || w < waterMin) continue;
                float s = g < w ? g : w;
                if (s > bestScore)
                {
                    bestScore = s;
                    best = i;
                }
            }
            return best;
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: uloop-run-tests（EditMode, filter `DistanceTransformTests`）
Expected: 3 テスト PASS。

- [ ] **Step 5: Commit**

```bash
git add Assets/MapGenerator/Pipeline/Spawn/DistanceTransform.cs \
        Assets/MapGenerator/Tests/EditMode/Spawn/DistanceTransformTests.cs
git commit -m "feat(spawn): add distance transform and pole-of-inaccessibility"
```

---

## Task 4: SmallSeaRemovalJob を boundary-aware に拡張

段2 局所窓で窓端に接する大海が「小海」誤判定され陸埋めされるのを防ぐ。`protectEdgeRegions` が真のとき、窓端セルに接触した海領域は面積に関わらず除去しない。

**Files:**
- Modify: `Assets/MapGenerator/Pipeline/Jobs/SmallSeaRemovalJob.cs`

- [ ] **Step 1: フラグフィールドと端接触追跡を追加**

`public int minRegionSize;` の直後に追加:

```csharp
        // 段2局所窓用: 窓端に接触する海領域は除去しない（本番では大海なのに窓クリップで小海誤判定するのを防ぐ）
        public bool protectEdgeRegions;
```

- [ ] **Step 2: flood-fill 中に端接触を検知**

`int maxBiome = -1;` `int maxCount = 0;` の宣言の直後に追加:

```csharp
                bool touchesEdge = false;
```

`while (stack.Length > 0)` ループ内、`int x = idx % resolution;` と `int y = idx / resolution;` を取得した直後に追加:

```csharp
                    if (x == 0 || x == resolution - 1 || y == 0 || y == resolution - 1)
                        touchesEdge = true;
```

- [ ] **Step 3: 除去条件にガードを追加**

既存の除去条件を次のように変更:

```csharp
                // 小さな海領域 → 陸に変換（ただしprotectEdgeRegions時に窓端接触領域は保護）
                if (regionPixels.Length < minRegionSize && maxBiome >= 0
                    && !(protectEdgeRegions && touchesEdge))
```

- [ ] **Step 4: コンパイル確認**

Run: uloop-compile
Expected: エラー 0。`protectEdgeRegions` 未指定の既存呼び出しは `false` 既定で従来動作を維持。

- [ ] **Step 5: Commit**

```bash
git add Assets/MapGenerator/Pipeline/Jobs/SmallSeaRemovalJob.cs
git commit -m "feat(spawn): boundary-aware small sea removal for windowed classification"
```

---

## Task 5: 段1 raw 粗グリッド分類 internal API

本番と同一の `ClassificationJob` を、粗解像度・走査範囲の一時 config で 1 回実行し、セルごとの**有効バイオーム配列インデックス**（海は -1）を返す。

**Files:**
- Modify: `Assets/MapGenerator/Pipeline/TerrainGenerator.cs`（クラス内に internal static メソッド追加）
- Create: `Assets/MapGenerator/Pipeline/Spawn/CoarseBiomeGrid.cs`

- [ ] **Step 1: CoarseBiomeGrid を作成**

```csharp
using UnityEngine;

namespace MapGenerator.Pipeline.Spawn
{
    /// <summary>
    /// 段1の粗グリッド分類結果。biomeIndex は有効バイオーム配列(biomeTypes[])へのインデックス、海は-1。
    /// セル中心のワールド座標との相互変換を提供する。
    /// </summary>
    public sealed class CoarseBiomeGrid
    {
        public readonly int[] BiomeIndex; // length = Width*Height, -1=sea
        public readonly int Width;
        public readonly int Height;
        public readonly float CellSize;   // m
        public readonly float OriginX;    // ワールド座標 m（セル(0,0)中心）
        public readonly float OriginZ;

        public CoarseBiomeGrid(int[] biomeIndex, int width, int height,
            float cellSize, float originX, float originZ)
        {
            BiomeIndex = biomeIndex;
            Width = width;
            Height = height;
            CellSize = cellSize;
            OriginX = originX;
            OriginZ = originZ;
        }

        public Vector2 CellCenterWorld(int cx, int cy) =>
            new Vector2(OriginX + cx * CellSize, OriginZ + cy * CellSize);
    }
}
```

- [ ] **Step 2: TerrainGenerator に ClassifyRawGrid を追加**

`GetEnabledBiomeTypes`（`TerrainGenerator.cs:874`）の近くに、`using MapGenerator.Pipeline.Spawn;` を冒頭に追加した上で、internal static メソッドを追加する。`ClassificationJob` のフィールドは `RunClassificationPipeline`（`TerrainGenerator.cs:280-309`）と完全に同じ値で埋める（本番一致が命）。

```csharp
        /// <summary>
        /// 段1: 本番同一のClassificationJob(raw)を粗グリッドで1回実行し、
        /// セルごとの有効バイオーム配列インデックス(海=-1)を返す。SmallSeaRemoval/blurは行わない。
        /// </summary>
        internal static MapGenerator.Pipeline.Spawn.CoarseBiomeGrid ClassifyRawGrid(
            TerrainGenerationConfig config, BiomeType[] biomeTypes,
            float centerX, float centerZ, float extent, float cellSize)
        {
            int biomeCount = biomeTypes.Length;
            int res = Mathf.Max(2, Mathf.CeilToInt(extent / cellSize) + 1);
            float originX = centerX - extent * 0.5f;
            float originZ = centerZ - extent * 0.5f;
            int pixelCount = res * res;

            JobDataConverter.GenerateClassificationOffsets(config, Unity.Collections.Allocator.TempJob,
                out var contOffsets, out var erosionOffsets);
            var biomePermutation = JobDataConverter.GenerateBiomePermutation(
                config.seed, biomeCount, Unity.Collections.Allocator.TempJob);
            var rawBiomeIndex = new Unity.Collections.NativeArray<int>(pixelCount, Unity.Collections.Allocator.TempJob);
            var shoreMask = new Unity.Collections.NativeArray<float>(pixelCount, Unity.Collections.Allocator.TempJob);
            var landMask = new Unity.Collections.NativeArray<float>(pixelCount, Unity.Collections.Allocator.TempJob);
            var beachFactor = new Unity.Collections.NativeArray<float>(pixelCount, Unity.Collections.Allocator.TempJob);

            try
            {
                var classJob = new Jobs.ClassificationJob
                {
                    resolution = res,
                    terrainWidth = extent,
                    terrainLength = extent,
                    worldOffsetX = originX,
                    worldOffsetZ = originZ,
                    continentalnessFrequency = config.continentalnessFrequency,
                    continentalnessOctaves = config.continentalnessOctaves,
                    continentalnessPersistence = config.continentalnessPersistence,
                    landThreshold = config.landThreshold,
                    erosionFrequency = config.erosionFrequency,
                    erosionOctaves = config.erosionOctaves,
                    erosionStrength = config.erosionStrength,
                    beachWidth = 0f,
                    voronoiCellSize = config.voronoiCellSize,
                    voronoiJitter = config.voronoiJitter,
                    biomeCount = biomeCount,
                    seed = config.seed,
                    boundaryWarpOctaves = config.boundaryWarpOctaves,
                    boundaryWarpStrength = config.boundaryWarpStrength,
                    boundaryWarpFrequency = config.boundaryWarpFrequency,
                    continentalnessOffsets = contOffsets,
                    erosionOffsets = erosionOffsets,
                    biomePermutation = biomePermutation,
                    shoreMask = shoreMask,
                    landMask = landMask,
                    beachFactor = beachFactor,
                    rawBiomeIndex = rawBiomeIndex
                };
                classJob.Schedule(pixelCount, 64).Complete();

                var arr = new int[pixelCount];
                rawBiomeIndex.CopyTo(arr);
                return new MapGenerator.Pipeline.Spawn.CoarseBiomeGrid(
                    arr, res, res, extent / (res - 1), originX, originZ);
            }
            finally
            {
                if (contOffsets.IsCreated) contOffsets.Dispose();
                if (erosionOffsets.IsCreated) erosionOffsets.Dispose();
                biomePermutation.Dispose();
                rawBiomeIndex.Dispose();
                shoreMask.Dispose();
                landMask.Dispose();
                beachFactor.Dispose();
            }
        }
```

> **注意:** `ClassificationJob` の名前空間は `MapGenerator.Pipeline.Jobs`。`TerrainGenerator.cs` 冒頭の using に `Jobs` が含まれているか確認し、無ければ `using MapGenerator.Pipeline.Jobs;` を追加（既存コードが `new ClassificationJob` を裸で使っているならそのままで可）。

- [ ] **Step 3: コンパイル確認**

Run: uloop-compile
Expected: エラー 0。

> 検証は Task 9 の統合テストで決定論性とともに確認する。

- [ ] **Step 4: Commit**

```bash
git add Assets/MapGenerator/Pipeline/Spawn/CoarseBiomeGrid.cs \
        Assets/MapGenerator/Pipeline/TerrainGenerator.cs
git commit -m "feat(spawn): add stage-1 raw coarse classification API"
```

---

## Task 6: 段2 詳細分類 internal API（post-blur final winner + land/beach）

候補周辺の局所窓に、本番と同一の分類後処理（SmallSeaRemoval(保護) + Interpolate + Blur）まで実行し、`winnerBiomeIndex` / `landMask` / `beachFactor` をコピーして返す。窓解像度は本番 m/px に厳密一致させる。

**Files:**
- Modify: `Assets/MapGenerator/Pipeline/TerrainGenerator.cs`
- Modify: `Assets/MapGenerator/Pipeline/TerrainGenerator.cs`（`RunClassificationPipeline` に SmallSeaRemoval 保護フラグを伝播）

- [ ] **Step 1: RunClassificationPipeline に保護フラグを伝播**

`RunClassificationPipeline`（`TerrainGenerator.cs:263`）のシグネチャに `bool protectEdgeSea = false` 引数を追加し、内部の `SmallSeaRemovalJob` 構築（`TerrainGenerator.cs:317` 付近）に `protectEdgeRegions = protectEdgeSea,` を追加する。既存呼び出し（`RunClassificationForPlacement` や `Generate` 経路）は引数省略で従来動作（false）を維持。

- [ ] **Step 2: 窓分類結果の型と API を追加**

`TerrainGenerator` クラス内に追加:

```csharp
        /// <summary>段2: 局所窓の本番一致分類結果（post-blur）。</summary>
        internal sealed class WindowClassification
        {
            public int Resolution;        // 窓のサンプル解像度(px)
            public float ActualWindowSize; // 実窓サイズ(m) = (Resolution-1)*pitch
            public float OriginX, OriginZ; // 窓原点ワールド座標(m)
            public float PitchX, PitchZ;   // m/px（本番一致）
            public int[] WinnerBiomeIndex;  // 有効バイオーム配列インデックス
            public float[] LandMask;        // 1=陸,0=海
            public float[] BeachFactor;     // 0-1
        }

        /// <summary>
        /// 段2: 本番m/pxに一致する局所窓で、SmallSeaRemoval(保護)+Interpolate+Blurまで実行し
        /// final winner / land / beach を返す。
        /// </summary>
        internal static WindowClassification RunClassificationDetailed(
            TerrainGenerationConfig baseConfig, BiomeType[] biomeTypes,
            float windowCenterX, float windowCenterZ, float windowSize)
        {
            // 本番m/px（X/Z別管理）
            double pX = (double)baseConfig.terrainWidth / (baseConfig.Resolution - 1);
            double pZ = (double)baseConfig.terrainLength / (baseConfig.Resolution - 1);
            // 正方窓: pitchはX基準で解像度を決め、actualでX/Zそろえる（terrainWidth==terrainLength前提、異なる場合はpを別々に）
            int res = Mathf.CeilToInt((float)(windowSize / pX)) + 1;
            res = Mathf.Max(2, res);
            double actualX = (res - 1) * pX;
            double actualZ = (res - 1) * pZ;

            // 窓原点を本番サンプル格子にスナップ（ワールド原点基準で pX/pZ 刻み）
            double rawOriginX = windowCenterX - actualX * 0.5;
            double rawOriginZ = windowCenterZ - actualZ * 0.5;
            float originX = (float)(System.Math.Round(rawOriginX / pX) * pX);
            float originZ = (float)(System.Math.Round(rawOriginZ / pZ) * pZ);

            // 窓専用configを複製（SOを汚さない）し、overrideResolutionで窓解像度を指定
            var cfg = Object.Instantiate(baseConfig);
            cfg.overrideResolution = res;
            cfg.terrainWidth = (float)actualX;
            cfg.terrainLength = (float)actualZ;
            cfg.worldOffsetX = originX;
            cfg.worldOffsetZ = originZ;

            int biomeCount = biomeTypes.Length;
            int pixelCount = res * res;
            var biomeParams = JobDataConverter.ConvertBiomeParams(cfg, biomeTypes, Unity.Collections.Allocator.TempJob);
            JobDataConverter.GenerateClassificationOffsets(cfg, Unity.Collections.Allocator.TempJob,
                out var contOffsets, out var erosionOffsets);
            var buffers = JobDataConverter.AllocateBuffers(res, biomeCount, 1, Unity.Collections.Allocator.TempJob);
            buffers.biomeParams = biomeParams;

            try
            {
                // 段2は窓端の大海を保護してクリップ誤判定を防ぐ
                RunClassificationPipeline(cfg, biomeCount, buffers, contOffsets, erosionOffsets, protectEdgeSea: true);

                var winner = new int[pixelCount];
                var land = new float[pixelCount];
                var beach = new float[pixelCount];
                buffers.winnerBiomeIndex.CopyTo(winner);
                buffers.landMask.CopyTo(land);
                buffers.beachFactor.CopyTo(beach);

                return new WindowClassification
                {
                    Resolution = res,
                    ActualWindowSize = (float)actualX,
                    OriginX = originX,
                    OriginZ = originZ,
                    PitchX = (float)pX,
                    PitchZ = (float)pZ,
                    WinnerBiomeIndex = winner,
                    LandMask = land,
                    BeachFactor = beach
                };
            }
            finally
            {
                buffers.Dispose();
                if (contOffsets.IsCreated) contOffsets.Dispose();
                if (erosionOffsets.IsCreated) erosionOffsets.Dispose();
                Object.DestroyImmediate(cfg);
            }
        }
```

> **確認事項（実装時に該当箇所を読むこと）:** `JobBuffers` に `winnerBiomeIndex` フィールドが存在することは `RunClassificationPipeline`（`TerrainGenerator.cs:406`）で確認済み。`AllocateBuffers` のシグネチャ（`res, biomeCount, layerCount, allocator`）は `RunClassificationForPlacement`（`TerrainGenerator.cs:907`）と一致させる。`Object.Instantiate`/`DestroyImmediate` は ScriptableObject の一時複製に使う（SO 不変を保証）。

- [ ] **Step 3: コンパイル確認**

Run: uloop-compile
Expected: エラー 0。

- [ ] **Step 4: Commit**

```bash
git add Assets/MapGenerator/Pipeline/TerrainGenerator.cs
git commit -m "feat(spawn): add stage-2 windowed final-winner classification API"
```

---

## Task 7: SpawnRegionFinder 本体（段1候補 → 段2検証 → 採用 → オフセット）

純粋ロジック（CC/距離変換）と internal API（ClassifyRawGrid/RunClassificationDetailed）を統合する。

**Files:**
- Create: `Assets/MapGenerator/Pipeline/Spawn/SpawnRegionFinder.cs`

- [ ] **Step 1: SpawnRegionFinder を実装**

```csharp
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using MapGenerator.Pipeline.Biomes;

namespace MapGenerator.Pipeline.Spawn
{
    /// <summary>
    /// 本番生成前に草原-森林隣接の良地を探索し、スポーン地点Sと中央化オフセットG=S-gridCenterを算出する。
    /// 段1(raw粗探索)で候補ペアを発見し、段2(本番一致final検証)で確定する。
    /// </summary>
    public static class SpawnRegionFinder
    {
        // 段1の候補ペア（粗グリッド上）
        sealed class Candidate
        {
            public Vector2 ApproxCenterWorld; // 草原CC重心(粗・ワールドm)
            public float Score;               // 段1スコア（降順ソート用）
            public float GrassAreaM2;
            public float ForestAreaM2;
            public float ContactM;
        }

        public static SpawnSearchResult Find(
            TerrainGenerationConfig config, BiomeType[] biomeTypes)
        {
            var sb = new StringBuilder();
            int grassBi = System.Array.IndexOf(biomeTypes, BiomeType.Grassland);
            int forestBi = System.Array.IndexOf(biomeTypes, BiomeType.Forest);
            if (grassBi < 0 || forestBi < 0)
                return SpawnSearchResult.Fallback("Grassland/Forestが有効バイオームに含まれない");

            var ss = config.spawnSearch;
            float extent = ss.scanExtent > 0f ? ss.scanExtent : DefaultScanExtent(config);
            Vector2 gridCenter = GridCenterWorld(config);

            for (int iter = 0; iter <= ss.maxExpandIterations; iter++)
            {
                sb.AppendLine($"[iter {iter}] extent={extent:F0}m cell={ss.scanCellSize:F0}m");

                // ---- 段1: 粗探索 ----
                var grid = TerrainGenerator.ClassifyRawGrid(
                    config, biomeTypes, gridCenter.x, gridCenter.y, extent, ss.scanCellSize);
                var candidates = FindCandidates(grid, grassBi, forestBi, ss, sb);

                // ---- 段2: バッチ検証（topK初期バッチ、validが出るまで次バッチ） ----
                candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
                var verified = new List<(SpawnSearchResult res, float finalScore)>();
                int verifiedCount = 0;
                foreach (var cand in candidates)
                {
                    verifiedCount++;
                    var vr = VerifyCandidate(config, biomeTypes, grassBi, forestBi, cand, ss, gridCenter, sb);
                    if (vr.HasValue) verified.Add(vr.Value);

                    // 初期バッチ消化後にvalidが1つでもあれば打ち切って採用へ（現在範囲優先・性能）
                    if (verifiedCount >= ss.topK && verified.Count > 0) break;
                }

                if (verified.Count > 0)
                {
                    verified.Sort((a, b) => b.finalScore.CompareTo(a.finalScore));
                    sb.AppendLine($"採用: score={verified[0].finalScore:F1} (検証{verifiedCount}件)");
                    var best = verified[0].res;
                    return new SpawnSearchResult(true, best.WorldOffset, best.SpawnWorldPosition,
                        verified[0].finalScore, sb.ToString());
                }

                if (verifiedCount >= ss.topK)
                    sb.AppendLine($"探索打ち切り: topK={ss.topK}件検証もvalidなし（保証ではない）");
                extent *= ss.expandFactor;
            }

            return SpawnSearchResult.Fallback(sb.AppendLine("最終的に候補ゼロ → オフセット0フォールバック").ToString());
        }

        // 段1: CC抽出 + 隣接ペアスコアリング
        static List<Candidate> FindCandidates(
            CoarseBiomeGrid grid, int grassBi, int forestBi, SpawnSearchConfig ss, StringBuilder sb)
        {
            float cellArea = grid.CellSize * grid.CellSize; // m²/cell
            var grassComps = ConnectedComponents.Label(grid.BiomeIndex, grid.Width, grid.Height, v => v == grassBi);
            var forestComps = ConnectedComponents.Label(grid.BiomeIndex, grid.Width, grid.Height, v => v == forestBi);

            var result = new List<Candidate>();
            foreach (var g in grassComps)
            {
                if (g.Area * cellArea < ss.minGrasslandArea) continue;
                foreach (var f in forestComps)
                {
                    if (f.Area * cellArea < ss.minForestArea) continue;
                    int contactCells = ConnectedComponents.BorderContactCells(g, f, grid.BiomeIndex, grid.Width, grid.Height);
                    float contactM = contactCells * grid.CellSize;
                    if (contactM < ss.minBorderContact) continue;

                    // 草原CC重心（粗・ワールド）
                    Vector2 centroid = CentroidWorld(g, grid);
                    float grassM2 = g.Area * cellArea;
                    float forestM2 = f.Area * cellArea;
                    float score = ss.wGrasslandArea * grassM2
                                + ss.wForestArea * forestM2
                                + ss.wBorderContact * contactM;
                    result.Add(new Candidate
                    {
                        ApproxCenterWorld = centroid,
                        Score = score,
                        GrassAreaM2 = grassM2,
                        ForestAreaM2 = forestM2,
                        ContactM = contactM
                    });
                }
            }
            sb.AppendLine($"  段1候補ペア: {result.Count}");
            return result;
        }

        // 段2: 本番一致final検証 + pole of inaccessibility
        static (SpawnSearchResult, float)? VerifyCandidate(
            TerrainGenerationConfig config, BiomeType[] biomeTypes, int grassBi, int forestBi,
            Candidate cand, SpawnSearchConfig ss, Vector2 gridCenter, StringBuilder sb)
        {
            float windowSize = WindowSizeFor(cand, ss);
            var win = TerrainGenerator.RunClassificationDetailed(
                config, biomeTypes, cand.ApproxCenterWorld.x, cand.ApproxCenterWorld.y, windowSize);

            int w = win.Resolution, h = win.Resolution;
            int n = w * h;
            float cellAreaM2 = win.PitchX * win.PitchZ;

            // final Grassland / Forest CC
            var grassComps = ConnectedComponents.Label(win.WinnerBiomeIndex, w, h, v => v == grassBi);
            var forestComps = ConnectedComponents.Label(win.WinnerBiomeIndex, w, h, v => v == forestBi);
            if (grassComps.Count == 0 || forestComps.Count == 0) return null;

            // 最大草原CCを採用候補とし、接触長/面積/Forest面積を再判定
            var gComp = grassComps[0];
            if (gComp.Area * cellAreaM2 < ss.minGrasslandArea) return null;

            // 接触する最大のForest CC
            ConnectedComponents.Component bestForest = null;
            float bestContactM = 0f;
            foreach (var f in forestComps)
            {
                if (f.Area * cellAreaM2 < ss.minForestArea) continue;
                int contactCells = ConnectedComponents.BorderContactCells(gComp, f, win.WinnerBiomeIndex, w, h);
                float contactM = contactCells * win.PitchX;
                if (contactM > bestContactM) { bestContactM = contactM; bestForest = f; }
            }
            if (bestForest == null || bestContactM < ss.minBorderContact) return null;

            // edgeMargin内のセルは判定対象外（窓外ピクセル欠落でwinner/距離がズレる）
            float edgeMarginM = EdgeMargin(config, ss);
            int edgeMarginCells = Mathf.CeilToInt(edgeMarginM / win.PitchX);

            // 草原final maskと「非草原 or 海/Beach」マスクを作り距離変換
            var grassMask = new bool[n];
            foreach (int idx in gComp.Cells) grassMask[idx] = true;

            // grassClearance: 草原mask境界までの距離（セル→m）
            var grassDistCell = DistanceTransform.ChebyshevToFalse(grassMask, w, h);
            // waterMask: 陸(landMask>0.5 かつ beachFactor低)を true とし、海/Beachまでの距離を測る
            var landMaskBool = new bool[n];
            for (int i = 0; i < n; i++)
                landMaskBool[i] = win.LandMask[i] > 0.5f && win.BeachFactor[i] < 0.5f;
            var waterDistCell = DistanceTransform.ChebyshevToFalse(landMaskBool, w, h);

            // m換算 + edgeMargin内を失格化
            var grassClear = new float[n];
            var waterClear = new float[n];
            for (int i = 0; i < n; i++)
            {
                int x = i % w, y = i / w;
                bool inMargin = x < edgeMarginCells || x >= w - edgeMarginCells
                             || y < edgeMarginCells || y >= h - edgeMarginCells;
                if (!grassMask[i] || inMargin)
                {
                    grassClear[i] = 0f; waterClear[i] = 0f; continue;
                }
                grassClear[i] = grassDistCell[i] * win.PitchX;
                waterClear[i] = waterDistCell[i] * win.PitchX;
            }

            int best = DistanceTransform.PickPole(grassClear, waterClear, n,
                ss.grassClearanceMin, ss.waterClearanceMin);
            if (best < 0) return null;

            // スポーン点S（窓サンプル格子点・ワールド座標）
            int bx = best % w, by = best / w;
            float sx = win.OriginX + bx * win.PitchX;
            float sz = win.OriginZ + by * win.PitchZ;
            var S = new Vector2(sx, sz);

            // 中央化: G = S - gridCenter
            Vector2 G = S - gridCenter;

            // final score（接触長・面積・スポーン点の最小クリアランスを総合）
            float minClear = Mathf.Min(grassClear[best], waterClear[best]);
            float finalScore = ss.wGrasslandArea * (gComp.Area * cellAreaM2)
                             + ss.wForestArea * (bestForest.Area * cellAreaM2)
                             + ss.wBorderContact * bestContactM
                             + ss.wInland * minClear;

            sb.AppendLine($"  段2 OK: S=({sx:F0},{sz:F0}) contact={bestContactM:F0}m minClear={minClear:F0}m");
            return (new SpawnSearchResult(true, G, S, finalScore, ""), finalScore);
        }

        // ---- ジオメトリ補助 ----

        static Vector2 CentroidWorld(ConnectedComponents.Component comp, CoarseBiomeGrid grid)
        {
            double sx = 0, sy = 0;
            foreach (int idx in comp.Cells)
            {
                int cx = idx % grid.Width;
                int cy = idx / grid.Width;
                sx += cx; sy += cy;
            }
            float ax = (float)(sx / comp.Cells.Count);
            float ay = (float)(sy / comp.Cells.Count);
            return new Vector2(grid.OriginX + ax * grid.CellSize, grid.OriginZ + ay * grid.CellSize);
        }

        static float DefaultScanExtent(TerrainGenerationConfig config)
        {
            // 生成グリッド外接（gridSize × チャンク幅）
            float w = config.gridSizeX * config.terrainWidth;
            float l = config.gridSizeZ * config.terrainLength;
            return Mathf.Max(w, l);
        }

        static Vector2 GridCenterWorld(TerrainGenerationConfig config)
        {
            // InfiniteTerrainManager は coord ∈ [-half, gridSize-half) でチャンクを配置し、
            // チャンク(coord)は worldOffset = coord*ChunkWidth に置かれる。
            // グリッド全体の幾何中心（オフセット0時）を返す。
            int halfX = config.gridSizeX / 2;
            int halfZ = config.gridSizeZ / 2;
            float minX = -halfX * config.terrainWidth;
            float maxX = (config.gridSizeX - halfX) * config.terrainWidth;
            float minZ = -halfZ * config.terrainLength;
            float maxZ = (config.gridSizeZ - halfZ) * config.terrainLength;
            return new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        }

        static float WindowSizeFor(Candidate cand, SpawnSearchConfig ss)
        {
            // 候補草原のおおよその直径を面積から見積もり、マージン+edgeを足す
            float approxDiameter = 2f * Mathf.Sqrt(cand.GrassAreaM2 / Mathf.PI);
            return approxDiameter + 2f * ss.windowMargin;
        }

        static float EdgeMargin(TerrainGenerationConfig config, SpawnSearchConfig ss)
        {
            float pX = config.terrainWidth / (config.Resolution - 1);
            int divisor = Mathf.Max(1, config.boundaryConfig != null ? config.boundaryConfig.blurRadiusDivisor : 2);
            float blendM = (config.biomeBlendRadius + config.biomeBlendRadius / (float)divisor) * pX;
            float beachR = 0f;
            var shore = config.shoreConfig;
            if (shore != null)
                beachR = Mathf.Max(Mathf.Max(shore.beachLandTextureRadius, shore.beachLandTerrainRadius),
                                   Mathf.Max(shore.beachSeaTextureRadius, shore.beachSeaTerrainRadius));
            return blendM + ss.waterClearanceMin + beachR;
        }
    }
}
```

> **実装時の確認事項:** `config.boundaryConfig.blurRadiusDivisor`、`config.biomeBlendRadius`、`config.shoreConfig.beachLand*Radius` のプロパティ名は `RunClassificationPipeline`（`TerrainGenerator.cs:350-353, 375, 384`）で使われている実名と一致させること。`win.BeachFactor` の閾値 0.5 は近似。Beach をより厳密に水扱いする必要があれば `beachFactor > 0` を水側に含める。

- [ ] **Step 2: コンパイル確認**

Run: uloop-compile
Expected: エラー 0。

- [ ] **Step 3: Commit**

```bash
git add Assets/MapGenerator/Pipeline/Spawn/SpawnRegionFinder.cs
git commit -m "feat(spawn): add SpawnRegionFinder two-stage orchestration"
```

---

## Task 8: InfiniteTerrainManager 統合（activeSpawnOffset）

`useSpawnOffsetSearch` ON のとき、全チャンク生成前に `Find()` を 1 回呼び、`spawnWorldPosition = S` を設定、各チャンクの `worldOffset` と `Terrain.position` 両方に G を加算する。SO は不変に保つ。

**Files:**
- Modify: `Assets/MapGenerator/InfiniteTerrainManager.cs`

- [ ] **Step 1: フィールドと探索実行を追加**

`using` に追加:

```csharp
using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Spawn;
```

クラス内フィールドに追加（`_chunks` の近く）:

```csharp
        // スポーン探索で算出したグローバルオフセット G（ワールドm）。各チャンクのworldOffset/位置に加算する
        Vector2 _activeSpawnOffset = Vector2.zero;
```

`RegenerateAllChunks()` の `ClearAllChunks();` の直後に追加:

```csharp
            _activeSpawnOffset = Vector2.zero;
            if (baseConfig.useSpawnOffsetSearch)
            {
                var biomeTypes = TerrainGenerator.GetEnabledBiomeTypesPublic(baseConfig);
                var result = SpawnRegionFinder.Find(baseConfig, biomeTypes);
                Debug.Log($"[SpawnSearch] {(result.Success ? "成功" : "フォールバック")}\n{result.Diagnostics}");
                if (result.Success)
                {
                    _activeSpawnOffset = result.WorldOffset;
                    baseConfig.spawnWorldPosition = result.SpawnWorldPosition;
                }
            }
```

> `GetEnabledBiomeTypes` は現在 `private static`。`InfiniteTerrainManager` から使うため、`TerrainGenerator` に internal/public ラッパ `GetEnabledBiomeTypesPublic` を1行追加する（または `GetEnabledBiomeTypes` を internal に昇格）。SpawnRegionFinder のテストでも使うので internal 化が無難。

- [ ] **Step 2: GenerateChunk で G を両方に加算**

`GenerateChunk` の offset 設定（`InfiniteTerrainManager.cs:38-39`）を変更:

```csharp
            baseConfig.worldOffsetX = coord.x * ChunkWidth + _activeSpawnOffset.x;
            baseConfig.worldOffsetZ = coord.y * ChunkLength + _activeSpawnOffset.y;
```

Terrain GameObject 位置（`InfiniteTerrainManager.cs:52-53`）を変更:

```csharp
            go.transform.position = new Vector3(
                coord.x * ChunkWidth + _activeSpawnOffset.x, 0,
                coord.y * ChunkLength + _activeSpawnOffset.y);
```

> SetNeighbors 等は相対関係なので影響なし。try/finally の offset 復元（`:47`）はそのままで、SO の `worldOffsetX/Z` は最終的に元値へ戻るため不変。

- [ ] **Step 3: TerrainGenerator にラッパを追加**

`GetEnabledBiomeTypes`（`TerrainGenerator.cs:874`）を `internal static` に変更するか、次のラッパを追加:

```csharp
        internal static BiomeType[] GetEnabledBiomeTypesPublic(TerrainGenerationConfig config)
            => GetEnabledBiomeTypes(config);
```

- [ ] **Step 4: コンパイル確認**

Run: uloop-compile
Expected: エラー 0。

- [ ] **Step 5: Commit**

```bash
git add Assets/MapGenerator/InfiniteTerrainManager.cs Assets/MapGenerator/Pipeline/TerrainGenerator.cs
git commit -m "feat(spawn): apply spawn offset to chunk worldOffset and transform"
```

---

## Task 9: 統合・回帰テスト（決定論性 + 予測=本番一致）

**Files:**
- Create: `Assets/MapGenerator/Tests/EditMode/Spawn/SpawnRegionFinderTests.cs`

> 既存テスト `Assets/MapGenerator/Tests/EditMode/PerformanceTests.cs` が `TerrainGenerationConfig` をテスト内で構築するパターン（`PerformanceTests.cs:140` 付近の `GetEnabledBiomeTypes` 再現）を参考に、テスト用 config を `ScriptableObject.CreateInstance<TerrainGenerationConfig>()` で作り、Grassland と Forest を有効化する。

- [ ] **Step 1: 決定論性テストを書く**

```csharp
using NUnit.Framework;
using UnityEngine;
using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Spawn;

namespace MapGenerator.Tests.EditMode.Spawn
{
    public class SpawnRegionFinderTests
    {
        static TerrainGenerationConfig MakeConfig(int seed)
        {
            var c = ScriptableObject.CreateInstance<TerrainGenerationConfig>();
            c.seed = seed;
            c.grasslandEnabled = true;
            c.forestEnabled = true;
            c.gridSizeX = 3; c.gridSizeZ = 3;
            c.terrainWidth = 1000f; c.terrainLength = 1000f;
            c.useSpawnOffsetSearch = true;
            // 軽量化のため低解像度プリセット（段2窓のpxは本番m/pxに従う）
            c.resolutionPreset = TerrainResolutionPreset._256;
            return c;
        }

        [Test]
        public void Find_SameSeed_IsDeterministic()
        {
            var c1 = MakeConfig(160);
            var c2 = MakeConfig(160);
            var bt = TerrainGenerator.GetEnabledBiomeTypesPublic(c1);
            var r1 = SpawnRegionFinder.Find(c1, bt);
            var r2 = SpawnRegionFinder.Find(c2, bt);
            Assert.AreEqual(r1.Success, r2.Success);
            if (r1.Success)
            {
                Assert.AreEqual(r1.SpawnWorldPosition.x, r2.SpawnWorldPosition.x, 0.01f);
                Assert.AreEqual(r1.SpawnWorldPosition.y, r2.SpawnWorldPosition.y, 0.01f);
                Assert.AreEqual(r1.WorldOffset.x, r2.WorldOffset.x, 0.01f);
            }
            Object.DestroyImmediate(c1); Object.DestroyImmediate(c2);
        }

        [Test]
        public void Find_MissingBiome_ReturnsFallback()
        {
            var c = MakeConfig(160);
            c.forestEnabled = false; // Forest無効
            var bt = TerrainGenerator.GetEnabledBiomeTypesPublic(c);
            var r = SpawnRegionFinder.Find(c, bt);
            Assert.IsFalse(r.Success);
            Object.DestroyImmediate(c);
        }
    }
}
```

- [ ] **Step 2: テスト実行**

Run: uloop-run-tests（EditMode, filter `SpawnRegionFinderTests`）
Expected: 2 テスト PASS（決定論性・フォールバック）。

- [ ] **Step 3: 予測=本番一致テストを書く**

`Find()` が成功した場合、返った S を含む窓を `RunClassificationDetailed` で再分類し、S 直下の final winner が Grassland 配列インデックスであることを確認する（段2 と同じ経路なので一致するはず＝回帰固定）。

```csharp
        [Test]
        public void Find_SpawnPoint_IsGrasslandInFinalWinner()
        {
            var c = MakeConfig(160);
            var bt = TerrainGenerator.GetEnabledBiomeTypesPublic(c);
            int grassBi = System.Array.IndexOf(bt, BiomeType.Grassland);
            var r = SpawnRegionFinder.Find(c, bt);
            if (!r.Success) Assert.Ignore("この設定では候補が見つからず予測一致は検証不能");

            var win = TerrainGenerator.RunClassificationDetailed(
                c, bt, r.SpawnWorldPosition.x, r.SpawnWorldPosition.y, 600f);
            // S直下のサンプル点インデックス
            int bx = Mathf.RoundToInt((r.SpawnWorldPosition.x - win.OriginX) / win.PitchX);
            int by = Mathf.RoundToInt((r.SpawnWorldPosition.y - win.OriginZ) / win.PitchZ);
            bx = Mathf.Clamp(bx, 0, win.Resolution - 1);
            by = Mathf.Clamp(by, 0, win.Resolution - 1);
            Assert.AreEqual(grassBi, win.WinnerBiomeIndex[by * win.Resolution + bx]);
            Object.DestroyImmediate(c);
        }
```

- [ ] **Step 4: テスト実行**

Run: uloop-run-tests（EditMode, filter `SpawnRegionFinderTests`）
Expected: PASS（候補が見つからない設定なら Ignore）。

- [ ] **Step 5: Commit**

```bash
git add Assets/MapGenerator/Tests/EditMode/Spawn/SpawnRegionFinderTests.cs
git commit -m "test(spawn): determinism, fallback, and prediction-consistency tests"
```

---

## Task 10: 視覚検証（uloop + 外部監査）

実機で全チャンク再生成し、スポーン地点が草原・森林隣接の中央に来ているかを目視＋外部監査で確認する。

**Files:** （コード変更なし。検証のみ）

- [ ] **Step 1: テスト用 config で探索を ON にする**

`DefaultConfig.asset`（または専用テスト config）の Inspector で `useSpawnOffsetSearch` を ON、Grassland/Forest を有効化。`InfiniteTerrainManager` の Generate All を実行。

- [ ] **Step 2: Scene View でスポーン地点（マップ中央）を撮影**

uloop-screenshot で Scene View をキャプチャ。Console ログ `[SpawnSearch] 成功 ...` の Diagnostics で採用経緯（S 座標・接触長・クリアランス）を確認。

- [ ] **Step 3: 外部監査**

external-audit スキルで「中央のスポーン地点が草原で、隣接して森林があり、海/端から十分離れているか」をスクショ付きで評価依頼。指摘があれば `SpawnSearchConfig` のパラメータ（面積閾値・クリアランス・接触長）を調整し再生成。

- [ ] **Step 4: 最終コミット（パラメータ調整があれば）**

```bash
git add Assets/MapGenerator/Presets/DefaultConfig.asset
git commit -m "chore(spawn): tune spawn search parameters from visual audit"
```

---

## Self-Review

**Spec coverage:**
- 段1 raw 粗探索 → Task 5, 7 ✓
- 段2 本番一致 final 検証（SmallSeaRemoval保護 + Interpolate + Blur）→ Task 4, 6, 7 ✓
- 窓 m/px 厳密一致・Resolution から actualWindowSize 逆算・格子スナップ → Task 6 ✓
- edgeMargin 除外 → Task 7（VerifyCandidate）✓
- 連結成分・面積・接触長 → Task 2, 7 ✓
- pole of inaccessibility + grass/water clearance → Task 3, 7 ✓
- topK バッチ + valid まで継続 + 現在範囲優先 + expand フォールバック → Task 7 ✓
- 中央化 G = S − gridCenter → Task 7（GridCenterWorld）✓
- worldOffset と Terrain.position 両方に加算・SO 不変 → Task 8 ✓
- spawnWorldPosition = S → Task 8 ✓
- SpawnSearchConfig 内包・全パラメータ m² 単位 → Task 1 ✓
- Grassland/Forest 欠如時エラー → Task 7, 9 ✓
- 決定論性・予測=本番一致・フォールバックのテスト → Task 9 ✓
- 視覚検証 → Task 10 ✓

**Placeholder scan:** プレースホルダなし。各 Step に実コードまたは具体コマンドを記載。`win.BeachFactor` 閾値などの近似値には実装時確認の注記を付与。

**Type consistency:**
- `ConnectedComponents.Label`/`BorderContactCells`/`Component.Area`/`Cells` — Task 2 定義、Task 7 で同名使用 ✓
- `DistanceTransform.ChebyshevToFalse`/`PickPole` — Task 3 定義、Task 7 使用 ✓
- `CoarseBiomeGrid`（`BiomeIndex/Width/Height/CellSize/OriginX/OriginZ`）— Task 5 定義、Task 7 使用 ✓
- `WindowClassification`（`Resolution/PitchX/PitchZ/OriginX/OriginZ/WinnerBiomeIndex/LandMask/BeachFactor`）— Task 6 定義、Task 7/9 使用 ✓
- `SpawnSearchResult`（`Success/WorldOffset/SpawnWorldPosition/Score/Diagnostics`/`Fallback`）— Task 1 定義、Task 7/8/9 使用 ✓
- `TerrainGenerator.ClassifyRawGrid`/`RunClassificationDetailed`/`GetEnabledBiomeTypesPublic` — Task 5/6/8 定義、Task 7/9 使用 ✓
- `SmallSeaRemovalJob.protectEdgeRegions` — Task 4 定義、Task 6（`protectEdgeSea` 伝播）使用 ✓

**実装上の確認が必要な既存 API（実装者は該当行を読むこと）:**
- `JobDataConverter.GenerateClassificationOffsets / GenerateBiomePermutation / ConvertBiomeParams / AllocateBuffers`（`TerrainGenerator.cs:275, 901-907`）
- `JobBuffers` のフィールド名（`winnerBiomeIndex/landMask/beachFactor/rawBiomeIndex/biomeParams` 等、`TerrainGenerator.cs:305-308, 405-407`）
- `config.boundaryConfig.blurRadiusDivisor` / `config.biomeBlendRadius` / `config.shoreConfig.beachLand*Radius`（`TerrainGenerator.cs:350-353, 375, 384`）
