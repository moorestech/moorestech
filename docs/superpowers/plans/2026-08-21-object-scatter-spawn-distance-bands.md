# mapObject独立散布のスポーン距離帯（bands）と小石のスポーン周辺生成 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** `biomeObjectConfig.entries`（独立散布）に鉱脈と同型のスポーン距離帯 `bands` を導入し、v8マスタで小石（チュートリアル対象 `c74efe49-52f3-403b-9c9a-b39eb1c85fce`）をスポーン周辺リングにのみ生成させる。

**Architecture:** 鉱脈の `OreBandPlanner`（outerRadiusMeters 昇順リング・-1=無限）を添字ベースの汎用 `SpawnDistanceRingPlanner` へ一般化して鉱脈・mapObject散布の両方から使う。`ObjectEntry` の flat な `density`/`clusterCount` を `ObjectScatterBand[] bands` へ移し、`ObjectIndependentPlacer`（Poisson散布）と新設 `ObjectBackboneClusterPlacer`（旧クラスタモード）がリングごとに Poisson を回してリング内の候補だけ採用する（鉱脈 `OreEntryPlacer` と同じ手順）。スキーマ・実行時POCO・変換ファクトリ・v8マスタ・テスト設定を一括で bands 形へ移行する。

**Tech Stack:** Unity C#（Game.MapGeneration / Server.Tests）、Mooresmaster SourceGenerator（VanillaSchema yml → `Mooresmaster.Model.*`）、NUnit、uloop、Python3（マスタJSON機械変換）

## Requirements

- R1. `biomeObjectConfig.entries[]` に `bands[]`（要素: `outerRadiusMeters`（-1=無限・最外周）、`density`（非クラスタ時・1haあたり）、`clusterCount`（`useClusterMode` 時））を持たせ、flat な `density`/`clusterCount` はスキーマから削除する（optional・既定値フォールバック・ローダー補完で吸収しない）。受け入れ: スキーマ変更後に flat 値を持つ JSON はロードで弾かれ、bands を持つ JSON が `ObjectEntry.bands` へ写る
- R2. 非クラスタ散布: リングごとに `band.density` で Poisson を回し、**候補点そのもの**のスポーン距離がリング内のものだけ採用する。受け入れ: bands=[{60, d>0},{-1, 0}] で全配置がスポーンXZから 60m 未満、bands=[{60, 0},{-1, d>0}] で全配置が 60m 以上
- R3. クラスタモード: リングごとに `band.clusterCount` を上限にクラスタ中心を Poisson で選び、**クラスタ中心**のスポーン距離がリング内のものだけ採用する（鉱脈と同じ基準）。受け入れ: bands=[{60, clusterCount>0},{-1, 0}] で全配置の `ClusterCenter` がスポーンXZから 60m 未満
- R4. スポーン距離の基準点は鉱脈と同じ `TerrainDimensions.SpawnWorldX/SpawnWorldZ`（ノイズ/ワールド座標）で、候補のワールド座標 `(local + WorldOffset)` との XZ 距離で判定する。5x5 タイルでもリングはワールド座標なので正しく切れる
- R5. リング化は鉱脈と同じ規則: `outerRadiusMeters` 昇順（負値は +∞ として末尾・安定ソート）、重複した外半径の後者は縮退（幅0で候補なし）、空 bands は警告してエントリをスキップ
- R6. 鉱脈側（`OreEntryPlacer`）も同じ汎用リングプランナーを使い、`OreBandPlanner` は削除する。挙動は不変
- R7. `clusterEntries`（階層岩クラスタ）と `treePlacement` は変更しない
- R8. 既存 v8 マスタの全 entries（8バイオーム）を `bands=[{outerRadiusMeters:-1, density:現値, clusterCount:現値}]` へ機械変換する（挙動不変）。テストマスタ2件（forUnitTest / EditModeInPlayingTestMod）は entries が空配列なので変換対象なし
- R9. v8 マスタの grassland と forest の entries に小石（`c74efe49-52f3-403b-9c9a-b39eb1c85fce`）を追加し、bands は「近傍リング1本（density>0）＋最外周 -1（density 0）」とする。近傍リングの半径・密度の具体値は agent 前提の調整値（初期値: outerRadiusMeters 80 / density 15）
- R10. スポーン半径 15m の `SpawnPlacementExclusionStage` は維持する（小石もこの除外を受ける）
- R11. コードrepoの `.moorestech-external-revisions.json` の `moorestech_master` pin を、マスタ変更コミットのハッシュへ更新する
- やらないこと: `MapObjectPin` の未発見時の扱い変更、Pebble1〜3（別GUID）の生成追加、クラスタエントリ・木への帯導入、mooreseditor 側対応、`TmpUnityPjt/MapMaking` / `scripts/mapmaking-parity/species-inventory.json` の更新（Mooresmaster がロードしない資料）

## Global Constraints

- AGENTS.md 準拠: 1ファイル200行以下、1ディレクトリ10ファイル以下、partial禁止、`Func<>`禁止、try-catch原則禁止、日本語→英語の2行セットコメント、`#region Internal` はメソッド内ローカル関数のみ、デフォルト引数禁止、自明なコメント禁止
- スキーマ変更は edit-schema スキルの手順（yml編集 → `_CompileRequester.cs` の `dummyText` 変更 → コンパイル）。`Mooresmaster.Model.*` の手書き禁止。新フィールドは必須＋yml `default`＋全JSON一括更新、`optional: true`・`?? Default`・ローダー補完は禁止
- 作業場所: コードrepo worktree `/Users/katsumi/moorestech-worktrees/object-scatter-spawn-bands`（ブランチ `feature/object-scatter-spawn-bands`、`origin/master` 2dcf5928a 起点）。**本体 `/Users/katsumi/moorestech` は別セッションが `feature/vein-outcrop-veinprefab-series` で作業中のため、チェックアウトを変更しない**
- マスタrepo作業場所: `/Users/katsumi/moorestech-master-worktrees/object-scatter-spawn-bands`（ブランチ `feat/object-scatter-spawn-bands`、起点はコードrepo `origin/master` の pin `fc6aa33e64dd9b1e1c8ede0a71b19465031caafd`。マスタrepoの `master` ブランチ 4e07ed0 には fc6aa33 が未マージなので `master` からは切らない）。本体 `/Users/katsumi/moorestech_master` は `feat/vein-outcrop-veinprefab-series` をチェックアウト中で触らない
- コンパイル: `uloop compile --project-path ./moorestech_client`、テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`（worktree 内で実行）。Unity の Library は本体からコピーして起動時間を短縮する（`Library/` 削除禁止）。「Unity is reloading」エラー時は45秒待って再試行
- 乱数消費順はリングごとに `rng.Next()` を引く（鉱脈と同じ）。タイル間の決定性は既存の `TileSeedMixer` で保たれる
- 時間計測・`Time`・`Stopwatch` は使わない（生成は純粋関数）

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 機構 / 前例 |
|---|---|---|---|
| 1 | `SpawnDistanceRing` / `SpawnDistanceRingPlanner`（新規） | `Game.MapGeneration/Pipeline/Generators/Util/` | 鉱脈 `Generators/Ore/OreBandPlanner.cs` の一般化。Util には `PoissonDiskSampler`・`SpatialGrid` 等の共有配置器部品が既にある（役割同型） |
| 2 | `ObjectScatterBand`（新規POCO） | `Pipeline/Config/Objects/` | `Config/Ore/OreBand.cs` と同型（public field POCO） |
| 3 | `ObjectEntry.bands`（既存POCO変更） | `Config/Objects/BiomeObjectConfig.cs` | `OreEntry.bands` と同型 |
| 4 | `ObjectBackboneClusterPlacer`（新規・`GenerateClusterObjects` を移設） | `Generators/Object/` | 200行制約による分割。`ObjectClusterPlacer`/`ObjectSecondaryPlacer` と同じ internal static 配置器 |
| 5 | スキーマ `bands` | `VanillaSchema/mapGenerate/biomeObjectConfig.yml` | `generation.yml` oreConfig.entries.bands（`overrideCodeGeneratePropertyName: OreBandElement`）と同形 |
| 6 | 変換 `ObjectRuntimeConfigFactory` | `Pipeline/Runtime/` | `OreRuntimeConfigFactory.ToBands` と同役割。ただし `Func` セレクタは使わず直接ループ（AGENTS: `Func<>`禁止。既存鉱脈側の Func は本planでは触らない） |
| 7 | マスタ機械変換 | `../moorestech_master`（worktree） | 前例: `fc6aa33`「MapMakingのobjectConfigを移植」の一括JSON更新 |
| 8 | pin 更新 | `.moorestech-external-revisions.json` | 前例: `2c017da82 chore(master): …masterへpinを更新` |

新規パターン（注目点）: リングプランナーを「添字ベース（`BandIndex`）」で一般化する。interface 経由（`ISpawnDistanceBand.OuterRadiusMeters`）だと POCO に素通しプロパティが要り、`Func` セレクタは禁止のため、呼び出し側が `float[]` の外半径列を渡して添字で元バンドへ戻す形を採る。出所: agent前提（AGENTS.md の `Func<>`禁止・素通しプロパティ禁止の両立）。

データフロー: マスタJSON →（Mooresmaster生成型）→ `ObjectRuntimeConfigFactory` → `BiomeObjectConfig.ObjectEntry.bands` → `ObjectPlacementGenerator` → `ObjectIndependentPlacer` / `ObjectBackboneClusterPlacer` → `PlacementEntry` → 既存の `SpawnPlacementExclusionStage` → `PlacedMapObject`。新規コンポーネントは既存連鎖の「書き手（配置器）」の内部改修のみで、交差点を足さない。

死活表: 既存 entries の生成（desert/mesa の岩・瓦礫）→ 生きる（単一無限リングへ機械変換、密度・clusterCount は同値）／鉱脈帯 → 生きる（同一規則のプランナーへ置換、R6）／clusterEntries・木 → 触らない。

---

### Task 0: 作業環境の準備（worktree・Library・マスタworktree）

**Files:**
- なし（環境準備のみ）

**Interfaces:**
- Produces: コードrepo worktree `/Users/katsumi/moorestech-worktrees/object-scatter-spawn-bands`（作成済み・ブランチ `feature/object-scatter-spawn-bands`）、マスタrepo worktree `/Users/katsumi/moorestech-master-worktrees/object-scatter-spawn-bands`

- [ ] **Step 1: コードrepo worktree の状態確認**

Run:
```bash
cd /Users/katsumi/moorestech-worktrees/object-scatter-spawn-bands && pwd && git branch --show-current && git log --oneline -2
```
Expected: `feature/object-scatter-spawn-bands` / 先頭2件が `docs: mapObject独立散布スポーン距離帯の実装計画を追加(ADR-0027)` と `docs: mapObject独立散布のスポーン距離帯(ADR-0027)と裁定2件を追加`、その親が `2dcf5928a`（origin/master）

- [ ] **Step 2: Unity Library を本体からコピー（未コピー時のみ）**

Run:
```bash
cd /Users/katsumi/moorestech-worktrees/object-scatter-spawn-bands
[ -d moorestech_client/Library ] || rsync -a --info=progress2 /Users/katsumi/moorestech/moorestech_client/Library/ moorestech_client/Library/
ls moorestech_client/Library | head -3
```
Expected: `Library/` が存在する（33GB・数分かかる）

- [ ] **Step 3: マスタrepo worktree を pin コミットから作成**

Run:
```bash
git -C /Users/katsumi/moorestech_master worktree add /Users/katsumi/moorestech-master-worktrees/object-scatter-spawn-bands -b feat/object-scatter-spawn-bands fc6aa33e64dd9b1e1c8ede0a71b19465031caafd
git -C /Users/katsumi/moorestech-master-worktrees/object-scatter-spawn-bands log --oneline -1
```
Expected: `fc6aa33 feat(v8): MapMakingのobjectConfig（メサ・砂漠の岩/瓦礫）を移植しgenerateObjectを有効化`

- [ ] **Step 4: 初回コンパイルで Unity が起動し緑であることを確認**

Run: `cd /Users/katsumi/moorestech-worktrees/object-scatter-spawn-bands && uloop compile --project-path ./moorestech_client`
Expected: エラー0件

---

### Task 1: 汎用スポーン距離リングプランナーを新設し、鉱脈側を載せ替える

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Util/SpawnDistanceRingPlanner.cs`
- Delete: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Ore/OreBandPlanner.cs`（`.meta` も Unity 側で消える。`git rm` で両方削除）
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Ore/OreBand.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Ore/OreEntryPlacer.cs:31-60`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Placement/SpawnDistanceRingPlannerTest.cs`

**Interfaces:**
- Produces:
  - `namespace Game.MapGeneration.Pipeline.Generators.Util`
  - `public readonly struct SpawnDistanceRing { public readonly int BandIndex; public readonly float Inner; public readonly float Outer; public SpawnDistanceRing(int bandIndex, float inner, float outer); public bool Contains(float distance); }`
  - `public static class SpawnDistanceRingPlanner { public static List<SpawnDistanceRing> BuildRings(float[] outerRadiusMeters); }`
  - `public static float[] OreBand.OuterRadiiOf(OreBand[] bands)`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Placement/SpawnDistanceRingPlannerTest.cs`:
```csharp
using Game.MapGeneration.Pipeline.Generators.Util;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Placement
{
    // 鉱脈とmapObject散布が共有するスポーン距離リング化の規則（昇順・負値は無限・重複縮退）を固定する。
    // Pins the spawn-distance ring rules shared by veins and object scatter (ascending, negative = infinite, duplicates degenerate).
    public class SpawnDistanceRingPlannerTest
    {
        [Test]
        public void 外半径昇順に並び負値は無限の最外周になる()
        {
            var rings = SpawnDistanceRingPlanner.BuildRings(new[] { 350f, 250f, -1f });

            Assert.AreEqual(3, rings.Count);
            Assert.AreEqual(1, rings[0].BandIndex);
            Assert.AreEqual(0f, rings[0].Inner);
            Assert.AreEqual(250f, rings[0].Outer);
            Assert.AreEqual(0, rings[1].BandIndex);
            Assert.AreEqual(250f, rings[1].Inner);
            Assert.AreEqual(350f, rings[1].Outer);
            Assert.AreEqual(2, rings[2].BandIndex);
            Assert.AreEqual(350f, rings[2].Inner);
            Assert.AreEqual(float.PositiveInfinity, rings[2].Outer);
        }

        [Test]
        public void 重複した外半径の後者は縮退してリングにならない()
        {
            var rings = SpawnDistanceRingPlanner.BuildRings(new[] { 250f, 250f });

            Assert.AreEqual(1, rings.Count);
            Assert.AreEqual(0, rings[0].BandIndex);
        }

        [Test]
        public void 空配列はリングを作らない()
        {
            Assert.AreEqual(0, SpawnDistanceRingPlanner.BuildRings(new float[0]).Count);
        }

        [Test]
        public void リング判定は内側を含み外側を含まない()
        {
            var ring = new SpawnDistanceRing(0, 250f, 350f);

            Assert.IsTrue(ring.Contains(250f));
            Assert.IsTrue(ring.Contains(349.9f));
            Assert.IsFalse(ring.Contains(350f));
            Assert.IsFalse(ring.Contains(249.9f));
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗（コンパイルエラー）を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `SpawnDistanceRingPlanner` が存在しないコンパイルエラー

- [ ] **Step 3: プランナーを実装する**

`moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Util/SpawnDistanceRingPlanner.cs`:
```csharp
using System.Collections.Generic;

namespace Game.MapGeneration.Pipeline.Generators.Util
{
    // 1つのスポーン距離リング [Inner, Outer)（Outer は +infinity あり）。BandIndex は元バンド配列の添字。
    // One spawn-distance ring [Inner, Outer) (Outer may be +infinity); BandIndex points back into the source band array.
    public readonly struct SpawnDistanceRing
    {
        public readonly int BandIndex;
        public readonly float Inner;
        public readonly float Outer;

        public SpawnDistanceRing(int bandIndex, float inner, float outer)
        {
            BandIndex = bandIndex;
            Inner = inner;
            Outer = outer;
        }

        public bool Contains(float distance) => Inner <= distance && distance < Outer;
    }

    // 外半径列を outerRadiusMeters 昇順（負値=無限は末尾・安定ソート）のリングへ変換する純粋関数。
    // 鉱脈帯と mapObject 散布帯の両方が使う。バンド型に依存しないよう外半径だけを受け取る。
    // Pure function turning outer radii into rings sorted ascending (negative = infinite last, stable).
    // Shared by vein bands and object-scatter bands; takes only the radii so it stays independent of the band type.
    public static class SpawnDistanceRingPlanner
    {
        public static List<SpawnDistanceRing> BuildRings(float[] outerRadiusMeters)
        {
            var rings = new List<SpawnDistanceRing>();
            if (outerRadiusMeters == null || outerRadiusMeters.Length == 0) return rings;

            // 添字を保持したまま安定ソートする。同じ外半径は元の並び順を保つ。
            // Sort stably while keeping the index; equal radii keep their original order.
            var indexed = new List<(float key, int idx)>();
            for (var i = 0; i < outerRadiusMeters.Length; i++)
                indexed.Add((ToSortKey(outerRadiusMeters[i]), i));
            indexed.Sort((a, b) =>
            {
                var c = a.key.CompareTo(b.key);
                return c != 0 ? c : a.idx.CompareTo(b.idx);
            });

            // 内側から順に [inner, outer) を切る。幅0（重複外半径）はリングにしない。
            // Cut [inner, outer) from the inside out; zero-width rings (duplicate radii) are dropped.
            var inner = 0f;
            foreach (var (key, idx) in indexed)
            {
                if (inner < key) rings.Add(new SpawnDistanceRing(idx, inner, key));
                inner = key;
            }
            return rings;
        }

        static float ToSortKey(float outerRadiusMeters)
            => outerRadiusMeters < 0f ? float.PositiveInfinity : outerRadiusMeters;
    }
}
```

- [ ] **Step 4: `OreBand` に外半径列の抽出を足す**

`moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Ore/OreBand.cs` を以下に置き換える:
```csharp
namespace Game.MapGeneration.Pipeline.Config
{
    // 鉱脈エントリ内の1つの距離バンド（スポーン地点中心の同心円リング）。
    // A single distance band (concentric ring around spawn) within an ore entry.
    public class OreBand
    {
        // -1（負値）は無限（最外周）。
        // -1 (negative) means infinite (outermost ring).
        public float outerRadiusMeters = -1f;
        public float density = 0.5f;
        public int maxObjectsPerCluster = 5;
        public float clusterRadius = 8f;
        public float minDistanceBetweenOres = 1.5f;
        public int placementRetries = 10;

        // リングプランナーへ渡す外半径列。バンドの並び順をそのまま保つ。
        // The outer-radius sequence handed to the ring planner, keeping band order.
        public static float[] OuterRadiiOf(OreBand[] bands)
        {
            var radii = new float[bands.Length];
            for (var i = 0; i < bands.Length; i++) radii[i] = bands[i].outerRadiusMeters;
            return radii;
        }
    }
}
```

- [ ] **Step 5: `OreEntryPlacer` を汎用プランナーへ載せ替える**

`moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Ore/OreEntryPlacer.cs` の `Place` 内、以下を置き換える:

置換前（31-32行のコメントと56行）:
```csharp
            // バンド未設定は生成器側で警告してスキップ（OreBandPlanner は純粋関数のため）。
            // Warn and skip when bands are missing (OreBandPlanner stays a pure function).
```
```csharp
            var ranges = OreBandPlanner.BuildRanges(entry.bands);

            foreach (var range in ranges)
            {
                var band = range.Band;
```
置換後:
```csharp
            // バンド未設定は生成器側で警告してスキップ（SpawnDistanceRingPlanner は純粋関数のため）。
            // Warn and skip when bands are missing (SpawnDistanceRingPlanner stays a pure function).
```
```csharp
            var rings = SpawnDistanceRingPlanner.BuildRings(OreBand.OuterRadiiOf(entry.bands));

            foreach (var range in rings)
            {
                var band = entry.bands[range.BandIndex];
```
`range.Contains(dist)` の呼び出しはそのまま（`SpawnDistanceRing.Contains`）。`foreach (var b in entry.bands) { if (b == null) continue; ... }` の null チェックは生成型から null が来ないため削除し、`if (b == null) continue;` の行だけ消す。

- [ ] **Step 6: `OreBandPlanner.cs` を削除する**

Run:
```bash
git rm -q moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Ore/OreBandPlanner.cs moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Ore/OreBandPlanner.cs.meta
grep -rn "OreBandPlanner\|OreBandRange" moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts --include='*.cs' || echo "no refs"
```
Expected: `no refs`

- [ ] **Step 7: コンパイル＆テスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SpawnDistanceRingPlannerTest|VeinSceneOffsetTest|FluidVeinPlacementStageTest"`
Expected: 全 PASS（鉱脈側の既存テストが挙動不変を示す）

- [ ] **Step 8: コミット**

```bash
git add -A moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Util/SpawnDistanceRingPlanner.cs* moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Ore/OreBand.cs moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Ore/OreEntryPlacer.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Placement/SpawnDistanceRingPlannerTest.cs*
git commit -m "refactor(mapgen): 鉱脈の距離帯リング化を汎用SpawnDistanceRingPlannerへ一般化"
```

---

### Task 2: スキーマ・実行時POCO・変換ファクトリを bands 形へ移行する

**Files:**
- Modify: `VanillaSchema/mapGenerate/biomeObjectConfig.yml:116-200`（entries ブロック）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs:8`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Objects/ObjectScatterBand.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Objects/BiomeObjectConfig.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Runtime/ObjectRuntimeConfigFactory.cs:76-110`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/TestGenerationConfigFactory.cs:153-175`（`BuildObjectEntry`）
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Tiling/MultiTileTestWorld.cs:60-78`（`BuildObjectConfig`）
- Modify（一時的にコンパイルを通すため）: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Object/ObjectIndependentPlacer.cs`、`ObjectPlacementGenerator.cs` は Task 3 で本実装するが、このタスクの終わりでコンパイルが通る必要がある。**このタスクでは Task 3 の実装をまとめて行わず、Task 2 と Task 3 を同一コミット単位で続けて進める**（Task 2 の Step 8 コミットは Task 3 完了後）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Placement/ObjectScatterSpawnBandTest.cs`（変換テスト1本をここで追加。配置テストは Task 3 で同ファイルに追記）

**Interfaces:**
- Produces:
  - `public class ObjectScatterBand { public float outerRadiusMeters = -1f; public float density = 1f; public int clusterCount = 8; public static float[] OuterRadiiOf(ObjectScatterBand[] bands); }`（namespace `Game.MapGeneration.Pipeline.Config`）
  - `BiomeObjectConfig.ObjectEntry.bands : ObjectScatterBand[]`（`density`・`clusterCount` フィールドは削除）
  - 生成型: `Mooresmaster.Model.BiomeObjectConfigModule` の entries 要素に `Bands`（要素型 `ObjectScatterBandElement`、プロパティ `OuterRadiusMeters`/`Density`/`ClusterCount`）

- [ ] **Step 1: 失敗するテスト（変換）を書く**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Placement/ObjectScatterSpawnBandTest.cs`:
```csharp
using Game.MapGeneration.Pipeline.Runtime;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Placement
{
    // 独立散布entriesのスポーン距離帯（bands）がJSONから実行時設定へ写り、配置がリング内に収まることを固定する。
    // Pins that object-scatter spawn-distance bands flow from JSON into runtime config and that placement stays inside the ring.
    public class ObjectScatterSpawnBandTest
    {
        [Test]
        public void JSONのbandsが実行時ObjectEntryへ写る()
        {
            var generation = TestGenerationConfigFactory.CreateWithMapObjectGuid(TestGenerationConfigFactory.TestMapObjectGuid);
            var config = GenerationRuntimeConfigFactory.Build(generation);

            var entry = config.grassland.objectConfig.entries[0];
            Assert.AreEqual(1, entry.bands.Length);
            Assert.AreEqual(-1f, entry.bands[0].outerRadiusMeters);
            Assert.AreEqual(1f, entry.bands[0].density);
            Assert.AreEqual(8, entry.bands[0].clusterCount);
        }
    }
}
```

- [ ] **Step 2: スキーマを編集する**

`VanillaSchema/mapGenerate/biomeObjectConfig.yml` の entries ブロック（`- key: entries` 配下 `properties`）で:
1. `- key: density` / `type: number` / `default: 1` の3行（133-135行付近）を削除
2. `- key: clusterCount` / `type: integer` / `default: 8` の3行（175-177行付近）を削除
3. `prefabs` の直後（`- key: scaleRange` の直前）に以下を挿入:
```yaml
    # スポーン地点中心の距離帯。outerRadiusMeters 昇順のリングで、負値は無限（最外周）。
    # density は非クラスタ散布の1haあたり密度、clusterCount はクラスタモードのクラスタ数上限。
    # Spawn-centred distance bands: rings in ascending outerRadiusMeters, negative = infinite (outermost).
    # density is the per-hectare scatter density; clusterCount caps cluster-mode clusters.
    - key: bands
      type: array
      overrideCodeGeneratePropertyName: ObjectScatterBandElement
      items:
        type: object
        properties:
        - key: outerRadiusMeters
          type: number
          default: -1
        - key: density
          type: number
          default: 1
        - key: clusterCount
          type: integer
          default: 8
```
（インデントは同ファイルの `- key: prefabs` と同じ4スペース階層に合わせる）

- [ ] **Step 3: SourceGenerator を起動する**

`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` の値を任意の新しい文字列（例: 現在時刻のGUID）へ変更する。

- [ ] **Step 4: 実行時POCOを作る／変更する**

`moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Objects/ObjectScatterBand.cs`:
```csharp
namespace Game.MapGeneration.Pipeline.Config
{
    // 独立散布エントリ内の1つのスポーン距離バンド（スポーン地点中心の同心円リング）。
    // A single spawn-distance band (concentric ring around spawn) within an object scatter entry.
    public class ObjectScatterBand
    {
        // -1（負値）は無限（最外周）。
        // -1 (negative) means infinite (outermost ring).
        public float outerRadiusMeters = -1f;

        // 非クラスタ散布の1haあたり密度。
        // Per-hectare density for non-cluster scatter.
        public float density = 1f;

        // クラスタモード時のクラスタ数上限。
        // Cluster cap for cluster mode.
        public int clusterCount = 8;

        // リングプランナーへ渡す外半径列。バンドの並び順をそのまま保つ。
        // The outer-radius sequence handed to the ring planner, keeping band order.
        public static float[] OuterRadiiOf(ObjectScatterBand[] bands)
        {
            var radii = new float[bands.Length];
            for (var i = 0; i < bands.Length; i++) radii[i] = bands[i].outerRadiusMeters;
            return radii;
        }
    }
}
```

`moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Objects/BiomeObjectConfig.cs` の `ObjectEntry`:
- `public float density = 1f;` を削除
- `public int clusterCount = 8;` を削除
- `public string[] mapObjectGuids;` の直後に追加:
```csharp
            // スポーン距離帯。非クラスタ時は density、クラスタモード時は clusterCount をリングごとに使う。
            // Spawn-distance bands; scatter uses density, cluster mode uses clusterCount, per ring.
            public ObjectScatterBand[] bands = new ObjectScatterBand[0];
```
クラス先頭コメントも「独立散布エントリ（prefabs は mapObjectGuid 配列へ置換・量は bands で指定）。」へ更新。

- [ ] **Step 5: 変換ファクトリを bands 対応にする**

`moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Runtime/ObjectRuntimeConfigFactory.cs` の entries ループ内、`entries.Add(new BiomeObjectConfig.ObjectEntry { ... })` の直前に挿入:
```csharp
                // スポーン距離帯を並び順のまま写す。リング化は配置時に行う。
                // Copy spawn-distance bands in order; ring construction happens at placement time.
                var bands = new ObjectScatterBand[e.Bands.Length];
                for (var i = 0; i < e.Bands.Length; i++)
                    bands[i] = new ObjectScatterBand
                    {
                        outerRadiusMeters = e.Bands[i].OuterRadiusMeters,
                        density = e.Bands[i].Density,
                        clusterCount = e.Bands[i].ClusterCount
                    };
```
初期化子内で `density = e.Density,` と `clusterCount = e.ClusterCount,` を削除し、`mapObjectGuids = entryGuids,` の直後に `bands = bands,` を追加する。

- [ ] **Step 6: テスト設定ビルダーを bands 形へ更新する**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/TestGenerationConfigFactory.cs` の `BuildObjectEntry`: `["density"] = 1.0,` と `["clusterCount"] = 8,` を削除し、`["prefabs"] = ...` の直後に追加:
```csharp
                    ["bands"] = new JArray(new JObject
                    {
                        ["outerRadiusMeters"] = -1,
                        ["density"] = 1.0,
                        ["clusterCount"] = 8,
                    }),
```

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Tiling/MultiTileTestWorld.cs` の `BuildObjectConfig`: 2つの `ObjectEntry` 初期化子それぞれに `bands = new[] { new ObjectScatterBand { outerRadiusMeters = -1f, density = 1f, clusterCount = 8 } },` を追加する（`density`/`clusterCount` の既定値に等しい単一無限リング）。

- [ ] **Step 7: Task 3 へ続く（このタスク単体ではコンパイルが `entry.density`/`entry.clusterCount` 参照で失敗する。Task 3 のStep 2〜4を実施してからコンパイル・テスト・コミットする）**

---

### Task 3: 配置器をリングごとの Poisson 散布に変更する（非クラスタ／クラスタモード）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Object/ObjectIndependentPlacer.cs`（`GenerateClusterObjects` を削除し、`GenerateIndependent` を帯対応）
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Object/ObjectBackboneClusterPlacer.cs`（旧 `GenerateClusterObjects` を移設＋帯対応）
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Object/ObjectPlacementGenerator.cs:72-87`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Placement/ObjectScatterSpawnBandTest.cs`（Task 2 のファイルへ追記）

**Interfaces:**
- Consumes: `SpawnDistanceRingPlanner.BuildRings(float[])`、`ObjectScatterBand.OuterRadiiOf`、`ObjectEntry.bands`（Task 1/2）
- Produces:
  - `ObjectIndependentPlacer.GenerateIndependent(BiomeObjectConfig.ObjectEntry entry, TerrainDimensions dims, float[,] heights, int hRes, bool[,] mask, float borderMarginPx, System.Random rng, Vector2[] noiseOffsets, List<PlacementEntry> placements, SpatialGrid treeSpatialGrid)`（署名不変）
  - `ObjectBackboneClusterPlacer.Generate(BiomeObjectConfig.ObjectEntry entry, TerrainDimensions dims, float[,] heights, int hRes, bool[,] mask, float borderMarginPx, System.Random rng, Vector2[] noiseOffsets, List<PlacementEntry> placements, SpatialGrid treeSpatialGrid, ObjectAlgorithmConfig objAlgCfg, ref int nextClusterId)`

- [ ] **Step 1: 失敗するテスト（配置）を書く**

`ObjectScatterSpawnBandTest.cs` に以下を追記（using に `System.Collections.Generic`, `Game.MapGeneration.Pipeline`, `Game.MapGeneration.Pipeline.Config`, `Tests.UnitTest.Game.MapGeneration.Tiling`, `UnityEngine` を追加）:
```csharp
        private const int Seed = 11;
        private const float NearRadius = 60f;

        [Test]
        public void 近傍帯だけ密度を持つ散布はスポーンから近傍半径未満にのみ置かれる()
        {
            var output = GenerateScatter(useClusterMode: false,
                new ObjectScatterBand { outerRadiusMeters = NearRadius, density = 30f, clusterCount = 0 },
                new ObjectScatterBand { outerRadiusMeters = -1f, density = 0f, clusterCount = 0 });

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
                Assert.Less(DistanceFromSpawnXz(mapObject.Position, output.SpawnPoint), NearRadius);
        }

        [Test]
        public void 最外周だけ密度を持つ散布はスポーンから近傍半径以上にのみ置かれる()
        {
            var output = GenerateScatter(useClusterMode: false,
                new ObjectScatterBand { outerRadiusMeters = NearRadius, density = 0f, clusterCount = 0 },
                new ObjectScatterBand { outerRadiusMeters = -1f, density = 30f, clusterCount = 0 });

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
                Assert.GreaterOrEqual(DistanceFromSpawnXz(mapObject.Position, output.SpawnPoint), NearRadius);
        }

        [Test]
        public void クラスタモードは近傍帯のクラスタ中心だけをスポーン近傍に置く()
        {
            var output = GenerateScatter(useClusterMode: true,
                new ObjectScatterBand { outerRadiusMeters = NearRadius, density = 0f, clusterCount = 400 },
                new ObjectScatterBand { outerRadiusMeters = -1f, density = 0f, clusterCount = 0 });

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
            {
                Assert.GreaterOrEqual(mapObject.ClusterId, 0);
                var center = new Vector3(mapObject.ClusterCenter.x, 0f, mapObject.ClusterCenter.y);
                Assert.Less(DistanceFromSpawnXz(center, output.SpawnPoint), NearRadius);
            }
        }

        // 1タイル・Grassland/Forest 両方に同じ散布エントリを置き、木は出さずに生成する。
        // Generate one tile with the same scatter entry in Grassland and Forest, with no trees.
        private static MapGenerationOutput GenerateScatter(bool useClusterMode, params ObjectScatterBand[] bands)
        {
            var config = MultiTileTestWorld.BuildConfig(1, Seed);
            config.generateObject = true;
            config.grassland.objectConfig = BuildScatterConfig(useClusterMode, bands);
            config.forest.objectConfig = BuildScatterConfig(useClusterMode, bands);
            return new VanillaGenerator().Generate(config);
        }

        private static BiomeObjectConfig BuildScatterConfig(bool useClusterMode, ObjectScatterBand[] bands)
        {
            return new BiomeObjectConfig
            {
                entries = new[]
                {
                    new BiomeObjectConfig.ObjectEntry
                    {
                        mapObjectGuids = new[] { MultiTileTestWorld.IndependentMapObjectGuid },
                        bands = bands,
                        useClusterMode = useClusterMode,
                        scaleRange = new Vector2(1f, 1f),
                    },
                },
            };
        }

        private static float DistanceFromSpawnXz(Vector3 position, Vector3 spawn)
        {
            return Vector2.Distance(new Vector2(position.x, position.z), new Vector2(spawn.x, spawn.z));
        }
```
（`PlacedMapObject.ClusterCenter` は `PlacementSceneOffset.ToSceneSpace` が `Cluster.Center` も同じシフトで補正するためシーン座標。`output.SpawnPoint` と同じ座標系で比較できる。クラスタ中心の Poisson 間隔は `sqrt(w*l/clusterCount*0.6)` なので、`clusterCount` を 400（間隔≈39m）にして半径60mのリング内に中心が必ず数個入るようにしている。）

- [ ] **Step 2: `ObjectIndependentPlacer` を帯対応にする（クラスタモードは別ファイルへ移設）**

`moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Object/ObjectIndependentPlacer.cs` を以下へ置き換える:
```csharp
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 独立散布（Poisson）。スポーン距離リングごとにそのリングの density で Poisson を回し、リング内の候補だけ採用する。
    // Independent scatter (Poisson): one Poisson pass per spawn-distance ring at that ring's density, keeping only in-ring candidates.
    internal static class ObjectIndependentPlacer
    {
        public static void GenerateIndependent(
            BiomeObjectConfig.ObjectEntry entry, TerrainDimensions dims,
            float[,] heights, int hRes, bool[,] mask, float borderMarginPx,
            System.Random rng, Vector2[] noiseOffsets,
            List<PlacementEntry> placements, SpatialGrid treeSpatialGrid)
        {
            float w = dims.TerrainWidth, l = dims.TerrainLength;
            float area = w * l;

            foreach (var ring in SpawnDistanceRingPlanner.BuildRings(ObjectScatterBand.OuterRadiiOf(entry.bands)))
            {
                var band = entry.bands[ring.BandIndex];
                int desiredCount = Mathf.RoundToInt(band.density * area / 10000f);
                if (desiredCount <= 0) continue;
                float minDist = Mathf.Sqrt(area / desiredCount * 0.8f);
                var points = PoissonDiskSampler.Generate(w, l, minDist, rng.Next());

                foreach (var point in points)
                {
                    // リング判定は候補点そのもののワールド座標距離で行う（鉱脈はクラスタ中心、散布は点）。
                    // The ring test uses the candidate's own world-space distance (veins test the cluster centre, scatter the point).
                    if (!ring.Contains(DistanceFromSpawn(point.x, point.y))) continue;

                    int hx = Mathf.Clamp(Mathf.RoundToInt(point.x / w * (hRes - 1)), 0, hRes - 1);
                    int hz = Mathf.Clamp(Mathf.RoundToInt(point.y / l * (hRes - 1)), 0, hRes - 1);
                    if (!mask[hz, hx] || BiomeMaskBuilder.IsNearMaskEdge(mask, hx, hz, hRes, borderMarginPx)) continue;

                    if (entry.noiseType != MapNoiseType.None)
                    {
                        // 位置は既にワールド座標へ直しているのにノイズだけタイルローカルだと、全タイルが同じ散布を反復する
                        // The position is already world-space; leaving the noise tile-local would repeat one scatter on every tile
                        float noise = ManagedNoise.SampleByType(entry.noiseType,
                            point.x + dims.WorldOffsetX, point.y + dims.WorldOffsetZ,
                            entry.noiseFrequency, noiseOffsets) * entry.noiseAmplitude;
                        if (noise < entry.noiseThreshold) continue;
                    }

                    if (treeSpatialGrid != null)
                    {
                        if (entry.minDistanceFromTree > 0f &&
                            treeSpatialGrid.HasNeighborWithin(point.x, point.y, entry.minDistanceFromTree))
                            continue;
                        if (entry.maxDistanceFromTree > 0f &&
                            !treeSpatialGrid.HasNeighborWithin(point.x, point.y, entry.maxDistanceFromTree))
                            continue;
                    }

                    float height = heights[hz, hx];

                    if (entry.useSlopeFilter)
                    {
                        float slope = ObjectPlacementMath.ComputeSlopeAngle(heights, hx, hz, hRes, w, dims.TerrainHeight, l);
                        float sw = ObjectPlacementMath.EvaluateSlopeFilter(slope, entry.slopeMin, entry.slopeMax, entry.slopeSmoothness);
                        if (sw <= 0f) continue;
                        if (sw < 1f && (float)rng.NextDouble() > sw) continue;
                    }

                    float scale = Mathf.Lerp(entry.scaleRange.x, entry.scaleRange.y, (float)rng.NextDouble());
                    float yRot = (float)rng.NextDouble() * 360f;
                    var rot = Quaternion.Euler(0, yRot, 0);
                    if (entry.slopeAlignment > 0.001f)
                        rot = ObjectPlacementMath.ApplySlopeAlignment(rot, heights, point.x, point.y, w, l, hRes,
                            dims.TerrainHeight, entry.slopeAlignment);

                    float sink = Mathf.Lerp(entry.sinkRange.x, entry.sinkRange.y, (float)rng.NextDouble());

                    placements.Add(new PlacementEntry
                    {
                        MapObjectGuid = ObjectPlacementMath.PickRandomGuid(entry.mapObjectGuids, rng),
                        WorldPosition = new Vector3(point.x + dims.WorldOffsetX, height * dims.TerrainHeight, point.y + dims.WorldOffsetZ),
                        Rotation = rot,
                        Scale = new Vector3(scale, scale, scale),
                        Sink = sink,
                        Cluster = new RockClusterInfo { ClusterId = -1 }
                    });
                }
            }

            #region Internal

            // タイルローカル座標をワールド座標へ直してスポーンXZとの距離を取る（鉱脈 OreEntryPlacer と同じ基準）。
            // Convert tile-local to world and measure the XZ distance to spawn (same basis as OreEntryPlacer).
            float DistanceFromSpawn(float localX, float localZ)
            {
                float dx = (localX + dims.WorldOffsetX) - dims.SpawnWorldX;
                float dz = (localZ + dims.WorldOffsetZ) - dims.SpawnWorldZ;
                return Mathf.Sqrt(dx * dx + dz * dz);
            }

            #endregion
        }
    }
}
```

- [ ] **Step 3: クラスタモードを `ObjectBackboneClusterPlacer` へ移設し帯対応にする**

`moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Object/ObjectBackboneClusterPlacer.cs`:
```csharp
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 旧バックボーンクラスター（clusterMode 互換）。スポーン距離リングごとにそのリングの clusterCount を上限に
    // クラスタ中心を Poisson で選び、中心がリング内のものだけ採用する（鉱脈と同じくクラスタ中心で判定）。
    // Legacy backbone clusters (clusterMode): per spawn-distance ring, Poisson-pick cluster centres up to that ring's
    // clusterCount and keep only centres inside the ring (centre-based, like veins).
    internal static class ObjectBackboneClusterPlacer
    {
        public static void Generate(
            BiomeObjectConfig.ObjectEntry entry, TerrainDimensions dims,
            float[,] heights, int hRes, bool[,] mask, float borderMarginPx,
            System.Random rng, Vector2[] noiseOffsets, List<PlacementEntry> placements,
            SpatialGrid treeSpatialGrid, ObjectAlgorithmConfig objAlgCfg, ref int nextClusterId)
        {
            float w = dims.TerrainWidth, l = dims.TerrainLength;

            foreach (var ring in SpawnDistanceRingPlanner.BuildRings(ObjectScatterBand.OuterRadiiOf(entry.bands)))
            {
                var band = entry.bands[ring.BandIndex];
                if (band.clusterCount <= 0) continue;
                float centerMinDist = Mathf.Sqrt(w * l / band.clusterCount * objAlgCfg.clusterSpacingFactor);
                var centers = PoissonDiskSampler.Generate(w, l, centerMinDist, rng.Next());

                int placed = 0;
                foreach (var center in centers)
                {
                    if (placed >= band.clusterCount) break;

                    // リング判定はクラスタ中心のワールド座標距離（鉱脈 OreEntryPlacer と同じ）。
                    // The ring test uses the cluster centre's world-space distance (as in OreEntryPlacer).
                    float dx = (center.x + dims.WorldOffsetX) - dims.SpawnWorldX;
                    float dz = (center.y + dims.WorldOffsetZ) - dims.SpawnWorldZ;
                    if (!ring.Contains(Mathf.Sqrt(dx * dx + dz * dz))) continue;

                    int cx = Mathf.Clamp(Mathf.RoundToInt(center.x / w * (hRes - 1)), 0, hRes - 1);
                    int cz = Mathf.Clamp(Mathf.RoundToInt(center.y / l * (hRes - 1)), 0, hRes - 1);
                    if (!mask[cz, cx] || BiomeMaskBuilder.IsNearMaskEdge(mask, cx, cz, hRes, borderMarginPx)) continue;

                    if (entry.noiseType != MapNoiseType.None)
                    {
                        float noise = ManagedNoise.SampleByType(entry.noiseType,
                            center.x + dims.WorldOffsetX, center.y + dims.WorldOffsetZ,
                            entry.noiseFrequency, noiseOffsets) * entry.noiseAmplitude;
                        if (noise < entry.noiseThreshold) continue;
                    }

                    placed++;
                    PlaceBackbone(entry, center, cx, cz, dims, heights, hRes, rng, placements, nextClusterId++);
                }
            }
        }

        // クラスタ中心から背骨状にメンバーを並べる（旧 GenerateClusterObjects の内側ループ）。
        // Lay members along a backbone from the cluster centre (the inner loop of the old GenerateClusterObjects).
        static void PlaceBackbone(
            BiomeObjectConfig.ObjectEntry entry, Vector2 center, int cx, int cz,
            TerrainDimensions dims, float[,] heights, int hRes, System.Random rng,
            List<PlacementEntry> placements, int clusterId)
        {
            float w = dims.TerrainWidth, l = dims.TerrainLength;
            int boneCount = Mathf.Min(3 + rng.Next(3), entry.objectsPerCluster);
            float backboneAngle = (float)rng.NextDouble() * Mathf.PI;
            float halfLen = entry.clusterRadius * 0.5f;

            float centerWorldX = center.x + dims.WorldOffsetX;
            float centerWorldZ = center.y + dims.WorldOffsetZ;
            float centerHt = heights[cz, cx] * dims.TerrainHeight;
            var clusterInfo = new RockClusterInfo
            {
                ClusterId = clusterId,
                Center = new Vector3(centerWorldX, centerHt, centerWorldZ),
                HeroCenter = new Vector3(centerWorldX, centerHt, centerWorldZ),
                Angle = backboneAngle,
                Length = entry.clusterRadius,
                FootprintRadius = entry.clusterRadius
            };

            for (int i = 0; i < boneCount; i++)
            {
                float t = boneCount <= 1 ? 0f : (2f * i / (boneCount - 1) - 1f);
                float axisOff = t * halfLen + ((float)rng.NextDouble() - 0.5f) * halfLen * 0.2f;
                float latJit = ((float)rng.NextDouble() - 0.5f) * halfLen * 0.3f;
                float ox = center.x + axisOff * Mathf.Cos(backboneAngle) - latJit * Mathf.Sin(backboneAngle);
                float oz = center.y + axisOff * Mathf.Sin(backboneAngle) + latJit * Mathf.Cos(backboneAngle);
                if (ox < 0 || ox > w || oz < 0 || oz > l) continue;

                int hx = Mathf.Clamp(Mathf.RoundToInt(ox / w * (hRes - 1)), 0, hRes - 1);
                int hz = Mathf.Clamp(Mathf.RoundToInt(oz / l * (hRes - 1)), 0, hRes - 1);
                float height = heights[hz, hx];

                float scale = Mathf.Lerp(entry.scaleRange.x, entry.scaleRange.y, (float)rng.NextDouble());
                float yScale = i == 0
                    ? scale * (0.65f + (float)rng.NextDouble() * 0.15f)
                    : scale * (0.45f + (float)rng.NextDouble() * 0.25f);
                float yRotDeg = backboneAngle * Mathf.Rad2Deg + ((float)rng.NextDouble() - 0.5f) * 30f;
                var rot = Quaternion.Euler(0, yRotDeg, 0);
                if (entry.slopeAlignment > 0.001f)
                    rot = ObjectPlacementMath.ApplySlopeAlignment(rot, heights, ox, oz, w, l, hRes,
                        dims.TerrainHeight, entry.slopeAlignment);

                float sink = Mathf.Lerp(entry.sinkRange.x, entry.sinkRange.y, (float)rng.NextDouble());

                placements.Add(new PlacementEntry
                {
                    MapObjectGuid = ObjectPlacementMath.PickRandomGuid(entry.mapObjectGuids, rng),
                    WorldPosition = new Vector3(ox + dims.WorldOffsetX, height * dims.TerrainHeight, oz + dims.WorldOffsetZ),
                    Rotation = rot,
                    Scale = new Vector3(scale, yScale, scale),
                    Sink = sink,
                    Cluster = clusterInfo
                });
            }
        }
    }
}
```

- [ ] **Step 4: `ObjectPlacementGenerator` の Phase B を bands 前提へ変更する**

`moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Object/ObjectPlacementGenerator.cs` の Phase B ループ本体を以下へ置き換える:
```csharp
                foreach (var entry in objConfig.entries)
                {
                    if (entry.mapObjectGuids == null || entry.mapObjectGuids.Length == 0) continue;

                    // バンド未設定は警告してスキップ（鉱脈 OreEntryPlacer と同じ扱い）。
                    // Warn and skip entries without bands (same treatment as OreEntryPlacer).
                    if (entry.bands == null || entry.bands.Length == 0)
                    {
                        Debug.LogWarning($"[ObjectPlacement] scatter entry '{entry.mapObjectGuids[0]}' has no spawn-distance bands; skipping.");
                        continue;
                    }

                    if (entry.useClusterMode)
                        ObjectBackboneClusterPlacer.Generate(entry, dims, heights, hRes,
                            mask, borderMarginPx, rng, noiseOffsets, placements, treeSpatialGrid, objAlgCfg, ref nextClusterId);
                    else
                        ObjectIndependentPlacer.GenerateIndependent(entry, dims, heights, hRes,
                            mask, borderMarginPx, rng, noiseOffsets, placements, treeSpatialGrid);
                }
```
`using UnityEngine;` を先頭に追加する（`Debug.LogWarning` 用）。

- [ ] **Step 5: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件（`entry.density`/`entry.clusterCount` の参照が残っていれば該当箇所を grep で潰す: `grep -rn "\.density\|\.clusterCount" moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Object moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration --include='*.cs'`）

- [ ] **Step 6: テスト**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ObjectScatterSpawnBandTest|SpawnDistanceRingPlannerTest|MultiTileMapObjectTransferTest|TilePlacementWorldSpaceTest|GenerationRuntimeConfigMapObjectValidationTest|MapGenerationPipelineTest|SpawnBoundaryTest"`
Expected: 全 PASS

- [ ] **Step 7: ファイル行数・ディレクトリ数の規約チェック**

Run:
```bash
wc -l moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Object/*.cs moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Runtime/ObjectRuntimeConfigFactory.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Placement/ObjectScatterSpawnBandTest.cs
ls moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Object/*.cs | wc -l
```
Expected: 全ファイル200行以下、Object ディレクトリ6ファイル

- [ ] **Step 8: コミット（Task 2 + Task 3）**

```bash
git add -A VanillaSchema/mapGenerate/biomeObjectConfig.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration
git commit -m "feat(mapgen): 独立散布entriesにスポーン距離帯bandsを導入しリングごとにPoisson散布する(ADR-0027)"
```

---

### Task 4: v8マスタを bands 形へ機械変換し、小石をスポーン近傍帯に追加する

**Files:**
- Modify: `/Users/katsumi/moorestech-master-worktrees/object-scatter-spawn-bands/server_v8/mods/moorestechAlphaMod_8/master/generation.json`（8バイオームの `objectConfig.entries[*]`）
- Modify: `.moorestech-external-revisions.json`（コードrepo worktree）

**Interfaces:**
- Consumes: Task 2 のスキーマ（`bands[]` 必須、flat `density`/`clusterCount` 廃止）
- Produces: pin 先コミット（Step 4 で得るハッシュ）

- [ ] **Step 1: 機械変換＋小石追加スクリプトを実行する**

Run（scratchpad に置いて実行。`indent=4`・`ensure_ascii=False`・末尾改行なしで元の書式を保つ）:
```bash
cat > /private/tmp/claude-501/-Users-katsumi-moorestech/350dc3ec-be50-4b51-8875-26025df72036/scratchpad/migrate_object_bands.py <<'EOF'
import json
from collections import OrderedDict

PATH = '/Users/katsumi/moorestech-master-worktrees/object-scatter-spawn-bands/server_v8/mods/moorestechAlphaMod_8/master/generation.json'
PEBBLE_GUID = 'c74efe49-52f3-403b-9c9a-b39eb1c85fce'
BIOMES = ['grassland', 'forest', 'savanna', 'desert', 'mesa', 'alpine', 'jungle', 'woods']

def to_bands(entry):
    # flat density/clusterCount を単一無限リングへ畳む（挙動不変）
    rebuilt = OrderedDict()
    for key, value in entry.items():
        if key == 'density':
            rebuilt['bands'] = [OrderedDict([
                ('outerRadiusMeters', -1),
                ('density', value),
                ('clusterCount', entry['clusterCount']),
            ])]
        elif key == 'clusterCount':
            continue
        else:
            rebuilt[key] = value
    assert 'bands' in rebuilt, entry
    return rebuilt

def pebble_entry():
    # 小石: スポーン近傍リングのみ density 15/ha、最外周は 0（agent前提の初期値・master で調整可）
    return OrderedDict([
        ('prefabs', [OrderedDict([('mapObjectGuid', PEBBLE_GUID)])]),
        ('bands', [
            OrderedDict([('outerRadiusMeters', 80), ('density', 15), ('clusterCount', 0)]),
            OrderedDict([('outerRadiusMeters', -1), ('density', 0), ('clusterCount', 0)]),
        ]),
        ('scaleRange', [1, 1]),
        ('slopeAlignment', 0),
        ('sinkRange', [0, 0]),
        ('noiseType', 'None'),
        ('noiseFrequency', 10),
        ('noiseAmplitude', 1),
        ('noiseThreshold', 0.5),
        ('useSlopeFilter', True),
        ('slopeMin', 0),
        ('slopeMax', 25),
        ('slopeSmoothness', 4),
        ('useClusterMode', False),
        ('objectsPerCluster', 4),
        ('clusterRadius', 12),
        ('minDistanceFromTree', 0),
        ('maxDistanceFromTree', 0),
    ])

with open(PATH, encoding='utf-8') as f:
    root = json.load(f, object_pairs_hook=OrderedDict)
ap = root['algorithmParam']
converted = 0
for biome in BIOMES:
    oc = ap[biome]['objectConfig']
    oc['entries'] = [to_bands(e) for e in oc['entries']]
    converted += len(oc['entries'])
for biome in ['grassland', 'forest']:
    ap[biome]['objectConfig']['entries'].append(pebble_entry())

with open(PATH, 'w', encoding='utf-8') as f:
    json.dump(root, f, ensure_ascii=False, indent=4)
print('converted entries:', converted, '+ pebble x2')
EOF
python3 /private/tmp/claude-501/-Users-katsumi-moorestech/350dc3ec-be50-4b51-8875-26025df72036/scratchpad/migrate_object_bands.py
```
Expected: `converted entries: 34 + pebble x2`（grassland 7 + forest 12 + desert 1 + mesa 14。savanna/alpine/jungle/woods は entries 空）

- [ ] **Step 2: 変換結果を検証する**

Run:
```bash
cd /Users/katsumi/moorestech-master-worktrees/object-scatter-spawn-bands && python3 - <<'EOF'
import json
g=json.load(open('server_v8/mods/moorestechAlphaMod_8/master/generation.json'))['algorithmParam']
bad=[]; pebble=0
for b in ['grassland','forest','savanna','desert','mesa','alpine','jungle','woods']:
    for e in g[b]['objectConfig']['entries']:
        if 'density' in e or 'clusterCount' in e or 'bands' not in e: bad.append((b,e.get('prefabs')))
        if any(p['mapObjectGuid']=='c74efe49-52f3-403b-9c9a-b39eb1c85fce' for p in e['prefabs']):
            pebble+=1; print(b,'pebble bands',e['bands'])
print('bad',bad,'pebble entries',pebble)
EOF
git -C /Users/katsumi/moorestech-master-worktrees/object-scatter-spawn-bands diff --stat
```
Expected: `bad [] pebble entries 2`、diff は generation.json 1ファイルのみ

- [ ] **Step 3: 実マスタで小石がスポーン近傍に生成されることを uloop で検証する**

Run（worktree の Unity で実行。5x5・本番解像度の生成は1〜数分かかる）:
```bash
cd /Users/katsumi/moorestech-worktrees/object-scatter-spawn-bands && uloop execute-dynamic-code --project-path ./moorestech_client --code '
var masterDir = "/Users/katsumi/moorestech-master-worktrees/object-scatter-spawn-bands/server_v8";
var path = System.IO.Path.Combine(masterDir, "mods", "moorestechAlphaMod_8", "master", "generation.json");
var root = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(path));
var generation = Mooresmaster.Loader.GenerationModule.GenerationLoader.Load(root);
var output = Game.MapGeneration.Pipeline.MapGenerationPipeline.Generate(generation, 196, masterDir);
var spawn = output.SpawnPoint;
int near = 0, far = 0; float maxNear = 0f, minAny = float.MaxValue;
foreach (var mo in output.MapObjects) {
  if (mo.MapObjectGuid != "c74efe49-52f3-403b-9c9a-b39eb1c85fce") continue;
  var d = UnityEngine.Vector2.Distance(new UnityEngine.Vector2(mo.Position.x, mo.Position.z), new UnityEngine.Vector2(spawn.x, spawn.z));
  if (d < 80f) { near++; maxNear = UnityEngine.Mathf.Max(maxNear, d); } else far++;
  minAny = UnityEngine.Mathf.Min(minAny, d);
}
return $"pebble near(<80m)={near} far={far} maxNear={maxNear:F1} minDist={minAny:F1} spawn={spawn}";
'
```
Expected: `near` が 3 以上（チュートリアルの「3個拾う」を満たす余裕）、`far=0`、`minDist` が 15 以上（クリアランス）。`near` が 3 未満なら Step 1 の `density` を上げて再実行する（master 調整値）。

- [ ] **Step 4: マスタrepo でコミットし、ハッシュを控える**

```bash
cd /Users/katsumi/moorestech-master-worktrees/object-scatter-spawn-bands
git add server_v8/mods/moorestechAlphaMod_8/master/generation.json
git commit -m "feat(v8): objectConfig.entriesをスポーン距離帯bandsへ移行し小石をスポーン近傍帯に追加"
git rev-parse HEAD
```
Expected: 40桁のコミットハッシュが出る

- [ ] **Step 5: コードrepo の pin を更新してコミット**

`.moorestech-external-revisions.json` の `moorestech_master` の `commitHash` を Step 4 のハッシュに書き換える（他のキーは触らない）。

```bash
cd /Users/katsumi/moorestech-worktrees/object-scatter-spawn-bands
git add .moorestech-external-revisions.json
git commit -m "chore(master): objectConfig bands移行と小石近傍帯を含むmasterへpinを更新"
```

---

### Task 5: moores-code-review による全ブランチレビュー（省略不可）

**Files:**
- なし（レビュー実行）

- [ ] **Step 1: 全テストの最終確認**

Run: `cd /Users/katsumi/moorestech-worktrees/object-scatter-spawn-bands && uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapGeneration"`
Expected: 全 PASS

- [ ] **Step 2: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

`moores-code-review` スキルを起動し、`feature/object-scatter-spawn-bands` の `origin/master` からの全差分をレビューする。指摘は修正してコミットし、レビュー記録は `../moorestech_logs/harness/` へ書く（featureブランチにはコミットしない）。

- [ ] **Step 3: bd を閉じる**

```bash
bd close moorestech-8k1 --reason="ADR-0027実装完了: 独立散布entriesにスポーン距離帯bandsを導入し小石をスポーン近傍帯に生成。pin更新済み"
```

---

## 判断記録（ADR）

- 設計セッションADR: `docs/adr/0027-object-scatter-entries-spawn-distance-bands.md`（出所: ユーザー裁定 2026-08-21「map object等の生成に『スポーン地点からの近さによる生成確率』を実装したい。veinにもあるやつ」→「独立散布entriesのみ」／「個数は指定しない。他と同じようにノイズの頻度みたいな感じで指定したい」／最外周「生成しない」）
- 裁定台帳: `.decisions/2026-08-21-スポーン距離帯は独立散布entriesのみに持たせる.md`、`.decisions/2026-08-21-小石は近傍帯のみで密度指定し最外周は生成しない.md`
- planning中の判断:
  - リングプランナーは添字ベース（`SpawnDistanceRing.BandIndex` + `float[] outerRadiusMeters`）で一般化し、鉱脈 `OreBandPlanner` を削除して共用する。interface＋素通しプロパティ／`Func`セレクタの両方を規約上避けるため。出所: agent前提（AGENTS.md `Func<>`禁止・素通しプロパティ禁止）
  - クラスタモードは `ObjectBackboneClusterPlacer` へ分離（200行制約・責務分離）。出所: agent前提（AGENTS.md 200行規約）
  - 小石の初期値は outerRadiusMeters 80 / density 15（1haあたり）・`useSlopeFilter` true `slopeMax` 25・grassland と forest の両方。Step 3 の実測で near<3 なら density を上げる。出所: agent前提（ユーザーは半径・密度の具体値を裁定していない）
  - 小石の prefab は `c74efe49`（小石）のみ。Pebble1〜3 は別GUIDで `MapObjectPin` の対象にならないため含めない。出所: agent前提
  - 空 bands は鉱脈と同じく LogWarning してスキップ（ローダー補完はしない）。出所: agent前提（ADR-0027「optional・既定値フォールバックで吸収しない」＋鉱脈前例）
  - 作業はコードrepo・マスタrepoとも専用 worktree で行い、本体のチェックアウト（別セッションが vein 作業中）を触らない。マスタ worktree の起点は pin `fc6aa33`。出所: agent前提（セッション中に本体へ別セッションのWIPコミット・未追跡ファイルを確認）
  - テストマスタ2件（forUnitTest / EditModeInPlayingTestMod）は entries 空配列のため JSON 変更なし。`TmpUnityPjt/MapMaking/.../migration_backup.json` と `scripts/mapmaking-parity/species-inventory.json` は Mooresmaster がロードしない資料のため更新しない。出所: agent前提
