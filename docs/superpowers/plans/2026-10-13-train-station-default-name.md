# 駅の既定名の命名システム Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 新しく置いた蒸気機関車駅に、周辺の鉱脈・地物から "Iron Ore Station" "Log Station 2" のような英語の既定名を付ける。

**Architecture:** サーバーは設置時（`VanillaTrainStationTemplate.New`）に、周辺特徴の走査（`TrainStationNameFeatureScanner`）で特徴のアイテム／液体を選び、英語名（mod の localization.csv の english 列。欠ければマスタ name）で "{name} Station" を組み立て、全駅名と重ならない最小番号を付けた文字列を既存の `TrainStationComponent(string)` に渡す。駅名は今までどおりの文字列で、セーブ形式・同期・Web UI は変えない。

**Tech Stack:** Unity C#（サーバー Game.Block / Server.Boot）、共通 CSV パーサー `mooresmaster.LocalizationCsv`（`Mooresmaster.LocalizationCsv.LocalizationCsvParser`）。

## Requirements

- R1 新しく置いた蒸気機関車駅（`TrainStationComponent` を持つブロック）には、設置時に既定名の文字列が付く。受け入れ: 鉱脈の近くに置いた駅の `StationName` が "<英語名> Station"
- R2 特徴の優先順は「水平半径32以内の最寄り鉱脈（アイテム・液体）→ 範囲内の採掘可能な地物を最初の earnItems のアイテムで数えた最多アイテム → 代替 "Station"」。受け入れ: 鉱脈・地物・何も無い位置の3ケースのテストが通る
- R3 地物の採掘アイテムは既存の `IMinableMapObjectParam.EarnItems` と `MapObjectMaster.IsDecoration` で取る。飾りと破壊済みは数えない
- R4 番号は、設置時に全駅名と比べて重ならない最小の番号（番号なし → " 2" → " 3"…）。改名された名前とも比べる。撤去後は空いた番号を再び使う。受け入れ: 2番の駅を "<名前> Station 3" へ改名すると、次の同特徴の駅は2番、その次は4番
- R5 英語名は全 mod の `localization/localization.csv` の english 列（`item.<guid>.name` / `fluid.<guid>.name`）から取り、無ければマスタの name。後の mod が前の mod を上書きする
- R6 既存セーブの駅名（"test"・空文字）は変わらない。セーブ形式は変えない
- やらないこと: 構造での保存・表示時翻訳／番号カウンタ／Web UI・DTO・同期の変更／既存駅への後付け／「既定名へ戻す」操作／貨物・液体プラットフォームへの命名

## Global Constraints

- AGENTS.md 全規約（1ファイル200行未満、1ディレクトリ10ファイルまで、partial・`Func<>`・デフォルト引数禁止、日英2行コメント、`#region Internal` はメソッド内ローカル関数のみ、fail-closed 経路はログ必須）
- try-catch は外部入力（mod の CSV のパース・ファイル読み込み）の境界だけ。根拠コメントを書き、catch した失敗は `Debug.LogError` に残す
- 走査半径 `32`（水平、XZ）。書式: `"{name} Station"` / `"{name} Station {number}"` / `"Station"` / `"Station {number}"`
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`
- 土台は PR #1415 のブランチ（`origin/feature/train-timetable-ui`）。本PRのベースも `feature/train-timetable-ui`

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / CSV読み込みはModsResourceを持つServer.Boot、命名はGame.Block、同期・セーブ・Web UIは不変で死活表は全操作生存
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強1・弱3・第3バケツ1 / 強（mod上書き順がADRとずれる）はModJsonStringLoaderの順へ修正、弱はユーザー指示「たかが自動名前付けでそんなに色々考える必要ある？」により不採用

---

## 配置と前例

| # | 項目 | 配置先 | 機構・前例 |
|---|---|---|---|
| 1 | `ModEnglishContentNameLoader`（mod CSV の english 列を読む） | Server.Boot `Localization/` | ModsResource を持つのは Server.Boot。読み方の前例はクライアント `Client.Localization/Dictionary/ModLocalizationMerger.cs` |
| 2 | `TrainStationFeatureEnglishNames`（GUID→英語名の表、マスタ name へ縮退） | Game.Block `Blocks/TrainRail/Naming/` | 駅命名だけが使う読み取り表。Server.Boot が作って DI 登録 |
| 3 | `TrainStationNameFeatureScanner` | Game.Block `Blocks/TrainRail/Naming/` | 鉱脈・地物 datastore を ctor 注入（Game.Block は Game.Map.Interface 参照済み）。前例 `VanillaGearMapObjectMinerProcessorComponent` の `GetWithinBoundingBox` |
| 4 | `TrainStationDefaultNameAllocator` | Game.Block `Blocks/TrainRail/Naming/` | 決まった名前の文字列を返す。全駅の列挙は `ServerContext.WorldBlockDatastore`（DI 循環のため注入不可。前例 `BlockPlaceOnVeinChallengeTask.cs:80`） |

---

### Task 1: mod の英語名の表

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/Naming/TrainStationFeatureEnglishNames.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Localization/ModEnglishContentNameLoader.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Composition/ServerContextRegistration.cs`（initializer コンテナへ `TrainStationFeatureEnglishNames` を AddSingleton。`ModsResource` の受け取り方は `MoorestechServerDIContainerGenerator.cs:50` から `ServerContextRegistration.Create(...)` への引数の流れに合わせる）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStation/Naming/ModEnglishContentNameLoaderTest.cs`

**Interfaces:**
- Produces:
  - `TrainStationFeatureEnglishNames(IReadOnlyDictionary<string, string> englishTexts)`、`string GetItemName(Guid itemGuid)`、`string GetFluidName(Guid fluidGuid)`
  - `static class ModEnglishContentNameLoader`、`static Dictionary<string, string> Load(ModsResource modsResource)`（key → english の辞書。`item.*.name` と `fluid.*.name` だけ）

- [ ] **Step 1: 英語名の表を書く**

```csharp
using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.Block.Blocks.TrainRail.Naming
{
    // 駅の既定名に使う英語名。modの英語訳が無ければマスタのnameへ縮退する
    // English names for default station names; falls back to the master name when no mod translation exists
    public class TrainStationFeatureEnglishNames
    {
        private readonly IReadOnlyDictionary<string, string> _englishTexts;

        public TrainStationFeatureEnglishNames(IReadOnlyDictionary<string, string> englishTexts)
        {
            _englishTexts = englishTexts;
        }

        public string GetItemName(Guid itemGuid)
        {
            // キーはADR 0006の導出規約 <type>.<guid>.name
            // Keys follow the ADR 0006 convention <type>.<guid>.name
            if (_englishTexts.TryGetValue($"item.{itemGuid}.name", out var english)) return english;
            return MasterHolder.ItemMaster.GetItemMaster(itemGuid).Name;
        }

        public string GetFluidName(Guid fluidGuid)
        {
            if (_englishTexts.TryGetValue($"fluid.{fluidGuid}.name", out var english)) return english;
            return MasterHolder.FluidMaster.GetFluidMaster(fluidGuid).Name;
        }
    }
}
```

（`GetItemMaster(Guid)` / `GetFluidMaster(Guid)` の name プロパティ名は生成物で確認する。GUID 文字列の書式は小文字ハイフン区切り（`Guid.ToString()` の既定 "D"）で、v8 の CSV（`item.582040ec-093b-4c8e-8fe3-f4ec030cf1ca.name`）と一致する。走査は現存の鉱脈・地物から特徴を取るため、ここに渡る GUID はマスタに必ず在る。）

- [ ] **Step 2: ローダーを書く**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Mod.Loader;
using Mooresmaster.LocalizationCsv;
using UnityEngine;

namespace Server.Boot.Localization
{
    // 全modのlocalization.csvからitem/fluid名の英語訳だけを集める
    // Collect only English item/fluid names from every mod's localization.csv
    public static class ModEnglishContentNameLoader
    {
        private const string LocalizationCsvRelativePath = "localization/localization.csv";
        private const string EnglishLanguageCode = "english";

        public static Dictionary<string, string> Load(ModsResource modsResource)
        {
            var englishTexts = new Dictionary<string, string>();
            foreach (var mod in modsResource.Mods.Values)
            {
                var csvPath = Path.Combine(mod.ExtractedPath, LocalizationCsvRelativePath);
                if (!File.Exists(csvPath)) continue;
                MergeEnglish(csvPath, englishTexts);
            }
            return englishTexts;
        }

        private static void MergeEnglish(string csvPath, Dictionary<string, string> englishTexts)
        {
            // modのCSVは外部入力のため、読み込みとパースの失敗をこの境界で隔離する
            // A mod CSV is external input, so read and parse failures are isolated at this boundary
            LocalizationCsv csv;
            try
            {
                csv = LocalizationCsvParser.Parse(File.ReadAllText(csvPath));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ModEnglishContentName] skipped unreadable localization csv: {csvPath} {exception.Message}");
                return;
            }

            var englishIndex = Array.IndexOf(csv.LanguageCodes, EnglishLanguageCode);
            if (englishIndex < 0) return;
            foreach (var row in csv.Rows)
            {
                if (!row.Key.StartsWith("item.") && !row.Key.StartsWith("fluid.")) continue;
                var text = row.Texts[englishIndex];
                if (string.IsNullOrEmpty(text)) continue;
                englishTexts[row.Key] = text;
            }
        }
    }
}
```

（パース結果の型名・`LanguageCodes`・`Rows`・`Texts` は `ModLocalizationMerger.MergeCsv` の使い方に合わせる。mod の走査順は「マスタとローカライズが共有する上書き順」（`Mod.Config/ModJsonStringLoader.cs:19` の `modResource.Mods.OrderBy(pair => pair.Key, StringComparer.Ordinal)`）と同じにする。コード例の `foreach (var mod in modsResource.Mods.Values)` はこの順序付きの列挙に置き換える。catch する例外型は `LocalizationCsvException` と IO 系に絞れるなら絞る。Server.Boot.asmdef が `Mod.Loader` と `mooresmaster.LocalizationCsv.dll` を参照しているかを確かめ、無ければ足す。）

- [ ] **Step 3: テストを書く**（一時ディレクトリに `<mod>/localization/localization.csv` を作り、`ModsResource` をその親で作る。作り方はクライアント `Client.Tests/Localization/Composition/ModLocalizationMergerTest.cs:106` 付近の前例に合わせる）

```csharp
[Test]
public void LoadsOnlyEnglishItemAndFluidNames()
{
    var modsDirectory = CreateModWithCsv("key,Source,english,japanese\nitem.11111111-0000-0000-0000-000000000001.name,鉄,Iron,鉄\nfluid.22222222-0000-0000-0000-000000000001.name,水,Water,水\nui.foo,a,Foo,あ\nitem.33333333-0000-0000-0000-000000000001.name,石,,石\n");
    var texts = ModEnglishContentNameLoader.Load(new ModsResource(modsDirectory));
    Assert.AreEqual("Iron", texts["item.11111111-0000-0000-0000-000000000001.name"]);
    Assert.AreEqual("Water", texts["fluid.22222222-0000-0000-0000-000000000001.name"]);
    Assert.IsFalse(texts.ContainsKey("ui.foo"));
    Assert.IsFalse(texts.ContainsKey("item.33333333-0000-0000-0000-000000000001.name"));
}

[Test]
public void UnreadableCsvIsSkippedWithError()
{
    var modsDirectory = CreateModWithCsv("not,a\n\"broken");
    LogAssert.Expect(LogType.Error, new Regex(@"\[ModEnglishContentName\] skipped unreadable localization csv"));
    Assert.AreEqual(0, ModEnglishContentNameLoader.Load(new ModsResource(modsDirectory)).Count);
}
```

（壊れた CSV の中身は `LocalizationCsvParser` が実際に例外を投げる形にする。投げなければ別の壊し方を選ぶ。）

- [ ] **Step 4: コンパイルしテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ModEnglishContentNameLoaderTest"`
Expected: ErrorCount 0 / PASS

- [ ] **Step 5: コミット** `git commit -m "feat: modのlocalization.csvから英語のアイテム・液体名を読む"`

---

### Task 2: 周辺特徴の走査

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/Naming/TrainStationNameFeature.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/Naming/TrainStationNameFeatureScanner.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStation/Naming/TrainStationNameFeatureScannerTest.cs`

**Interfaces:**
- Consumes: Task 1 の `TrainStationFeatureEnglishNames`
- Produces:
  - 抽象クラス `TrainStationNameFeature` の `abstract string FormatName(TrainStationFeatureEnglishNames names, int number)`（number 1 は番号なし）と、サブクラス `ItemTrainStationNameFeature(Guid)`（`ItemGuid`）／`FluidTrainStationNameFeature(Guid)`（`FluidGuid`）／`FallbackTrainStationNameFeature`
  - `TrainStationNameFeatureScanner(IItemMapVeinDatastore, IFluidMapVeinDatastore, IMapObjectDatastore)`、`TrainStationNameFeature Scan(BlockPositionInfo position)`、`const float ScanRadius = 32f`

- [ ] **Step 1: 特徴の型を書く**

```csharp
using System;

namespace Game.Block.Blocks.TrainRail.Naming
{
    // 既定名の元になる特徴。番号1は番号なしで名前を組み立てる
    // Feature a default name derives from; number 1 renders without a number
    public abstract class TrainStationNameFeature
    {
        public abstract string FormatName(TrainStationFeatureEnglishNames names, int number);

        protected static string FormatStation(string baseName, int number)
        {
            return number == 1 ? $"{baseName} Station" : $"{baseName} Station {number}";
        }
    }

    public sealed class ItemTrainStationNameFeature : TrainStationNameFeature
    {
        public Guid ItemGuid { get; }
        public ItemTrainStationNameFeature(Guid itemGuid) { ItemGuid = itemGuid; }
        public override string FormatName(TrainStationFeatureEnglishNames names, int number) => FormatStation(names.GetItemName(ItemGuid), number);
    }

    public sealed class FluidTrainStationNameFeature : TrainStationNameFeature
    {
        public Guid FluidGuid { get; }
        public FluidTrainStationNameFeature(Guid fluidGuid) { FluidGuid = fluidGuid; }
        public override string FormatName(TrainStationFeatureEnglishNames names, int number) => FormatStation(names.GetFluidName(FluidGuid), number);
    }

    // 周辺に何も無い駅の名前
    // Name for a station with nothing nearby
    public sealed class FallbackTrainStationNameFeature : TrainStationNameFeature
    {
        public override string FormatName(TrainStationFeatureEnglishNames names, int number) => number == 1 ? "Station" : $"Station {number}";
    }
}
```

- [ ] **Step 2: 走査を書く**

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Map.Interface.MapObject;
using Game.Map.Interface.Vein;
using UnityEngine;

namespace Game.Block.Blocks.TrainRail.Naming
{
    // 駅の周辺から既定名の特徴を選ぶ（鉱脈→地物の採掘アイテム→代替）
    // Pick the default-name feature around a station (vein, then mined item of map objects, then fallback)
    public class TrainStationNameFeatureScanner
    {
        public const float ScanRadius = 32f;

        private readonly IItemMapVeinDatastore _itemVeins;
        private readonly IFluidMapVeinDatastore _fluidVeins;
        private readonly IMapObjectDatastore _mapObjects;

        public TrainStationNameFeatureScanner(IItemMapVeinDatastore itemVeins, IFluidMapVeinDatastore fluidVeins, IMapObjectDatastore mapObjects)
        {
            _itemVeins = itemVeins;
            _fluidVeins = fluidVeins;
            _mapObjects = mapObjects;
        }

        public TrainStationNameFeature Scan(BlockPositionInfo position)
        {
            var center = new Vector2((position.MinPos.x + position.MaxPos.x + 1) * 0.5f, (position.MinPos.z + position.MaxPos.z + 1) * 0.5f);
            return FindNearestVein() ?? FindMostMinedItem() ?? new FallbackTrainStationNameFeature();

            #region Internal

            TrainStationNameFeature FindNearestVein()
            {
                // 同距離はアイテム優先、次にGUID文字列順（その順に並べた候補の先勝ち）
                // Ties prefer items, then GUID string order (the first candidate in that order wins)
                TrainStationNameFeature best = null;
                var bestDistance = float.MaxValue;
                foreach (var vein in SortedByGuid(ItemVeinCandidates())) Consider(new ItemTrainStationNameFeature(vein.ContentGuid), vein.Min, vein.Max);
                foreach (var vein in SortedByGuid(FluidVeinCandidates())) Consider(new FluidTrainStationNameFeature(vein.ContentGuid), vein.Min, vein.Max);
                return best;

                void Consider(TrainStationNameFeature feature, Vector3Int min, Vector3Int max)
                {
                    var distance = HorizontalDistanceToCells(min, max);
                    if (ScanRadius < distance || bestDistance <= distance) return;
                    best = feature;
                    bestDistance = distance;
                }
            }

            TrainStationNameFeature FindMostMinedItem()
            {
                // 採掘可能な地物を最初の採掘アイテムで数え、最多（同数はGUID文字列順の先）を選ぶ
                // Count minable map objects by their first earned item; pick the most frequent (ties go to GUID string order)
                var counts = new SortedDictionary<string, (Guid ItemGuid, int Count)>(StringComparer.Ordinal);
                var min = new Vector3(center.x - ScanRadius, 0, center.y - ScanRadius);
                var max = new Vector3(center.x + ScanRadius, 0, center.y + ScanRadius);
                foreach (var mapObject in _mapObjects.GetWithinBoundingBox(min, max))
                {
                    if (mapObject.IsDestroyed) continue;
                    if (ScanRadius < Vector2.Distance(center, new Vector2(mapObject.Position.x, mapObject.Position.z))) continue;
                    var element = MasterHolder.MapObjectMaster.GetMapObjectElement(mapObject.MapObjectGuid);
                    if (MapObjectMaster.IsDecoration(element) || element.MiningParam is not IMinableMapObjectParam minable || minable.EarnItems.Length == 0) continue;
                    var itemGuid = minable.EarnItems[0].ItemGuid;
                    var key = itemGuid.ToString();
                    counts[key] = counts.TryGetValue(key, out var entry) ? (itemGuid, entry.Count + 1) : (itemGuid, 1);
                }

                TrainStationNameFeature best = null;
                var bestCount = 0;
                foreach (var (itemGuid, count) in counts.Values)
                {
                    if (count <= bestCount) continue;
                    best = new ItemTrainStationNameFeature(itemGuid);
                    bestCount = count;
                }
                return best;
            }

            IEnumerable<(Guid ContentGuid, Vector3Int Min, Vector3Int Max)> ItemVeinCandidates()
            {
                foreach (var vein in _itemVeins.Veins) yield return (MasterHolder.ItemMaster.GetItemGuid(vein.VeinItemId), vein.VeinRangeMin, vein.VeinRangeMax);
            }

            IEnumerable<(Guid ContentGuid, Vector3Int Min, Vector3Int Max)> FluidVeinCandidates()
            {
                foreach (var vein in _fluidVeins.Veins) yield return (MasterHolder.FluidMaster.GetFluidGuid(vein.VeinFluidId), vein.VeinRangeMin, vein.VeinRangeMax);
            }

            List<(Guid ContentGuid, Vector3Int Min, Vector3Int Max)> SortedByGuid(IEnumerable<(Guid ContentGuid, Vector3Int Min, Vector3Int Max)> veins)
            {
                var sorted = new List<(Guid ContentGuid, Vector3Int Min, Vector3Int Max)>(veins);
                sorted.Sort((a, b) => string.CompareOrdinal(a.ContentGuid.ToString(), b.ContentGuid.ToString()));
                return sorted;
            }

            float HorizontalDistanceToCells(Vector3Int min, Vector3Int max)
            {
                var dx = Mathf.Max(min.x - center.x, 0, center.x - (max.x + 1));
                var dz = Mathf.Max(min.z - center.y, 0, center.y - (max.z + 1));
                return Mathf.Sqrt(dx * dx + dz * dz);
            }

            #endregion
        }
    }
}
```

`IMinableMapObjectParam` の名前空間・`EarnItems` の要素型と `ItemGuid` の名前は `Core.Master/Validator/MapObjectMasterUtil.cs:25` の使い方に、`MapObjectMaster.IsDecoration` の引数型も同ファイル周辺に合わせる。200行を超えたら鉱脈探索を `TrainStationNearestVeinFinder` へ分ける。

- [ ] **Step 3: テストを書く**（`TrainTestHelper.CreateEnvironment()` で ForUnitTest の map.json を読む環境を作り、`ServerContext.ItemMapVeinDatastore` 等を渡す。駅サイズ 3×1×8 の `BlockPositionInfo` を直接作る）

```csharp
[Test]
public void NearestVeinWins()
{
    // 原点の駅は中心(1.5,4)。最寄りは(0,5,1)のTest1鉱脈（距離約2.06）
    // A station at the origin has center (1.5,4); nearest is the Test1 vein at (0,5,1), ~2.06 away
    var feature = CreateScanner().Scan(new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, new Vector3Int(3, 1, 8)));
    Assert.IsInstanceOf<ItemTrainStationNameFeature>(feature);
    Assert.AreEqual(Guid.Parse("00000000-0000-0000-1234-000000000001"), ((ItemTrainStationNameFeature)feature).ItemGuid);
}

[Test]
public void MinedItemOfMapObjectsWhenNoVein()
{
    // (100,0,100)のTestMiningRockだけが半径内（鉱脈はx≤20,z≤1で範囲外）
    // Only TestMiningRock at (100,0,100) is in range; veins sit at x<=20, z<=1
    var feature = CreateScanner().Scan(new BlockPositionInfo(new Vector3Int(95, 0, 95), BlockDirection.North, new Vector3Int(3, 1, 8)));
    Assert.IsInstanceOf<ItemTrainStationNameFeature>(feature);
    Assert.AreEqual(ExpectedTestMiningRockEarnItemGuid, ((ItemTrainStationNameFeature)feature).ItemGuid);
}

[Test]
public void FallbackWhenNothingNearby()
{
    var feature = CreateScanner().Scan(new BlockPositionInfo(new Vector3Int(60, 0, 60), BlockDirection.North, new Vector3Int(3, 1, 8)));
    Assert.IsInstanceOf<FallbackTrainStationNameFeature>(feature);
}
```

`ExpectedTestMiningRockEarnItemGuid` は `Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json` の TestMiningRock（`00000000-0000-2222-0000-000000000001`）の最初の earnItems の itemGuid を書き写す。実装前に上の距離計算が test map で成り立つか（他の鉱脈が同距離にならないか）を確かめ、成り立たなければテストの設置位置を変える（期待値を実装に合わせて変えない）。

- [ ] **Step 4: コンパイルしテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainStationNameFeatureScannerTest"`
Expected: ErrorCount 0 / PASS

- [ ] **Step 5: コミット** `git commit -m "feat: 駅の周辺から既定名の特徴を選ぶ走査を追加"`

---

### Task 3: 既定名の割り当てと駅テンプレートへの組み込み

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/Naming/TrainStationDefaultNameAllocator.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/Train/VanillaTrainStationTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/TrainStationComponent.cs`（引数なし ctor と `UnnamedStationName` 定数を削除。新設駅は必ず名前を受け取る。ロード ctor の既定値は空文字のまま）
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStation/TrainStationNameTest.cs`（初期名 `string.Empty` の期待を既定名に変更）
- Modify: 駅の初期名を空文字前提で読む既存テスト（`grep -rn "StationName" moorestech_server/Assets/Scripts/Tests` で全件確認）、`new VanillaIBlockTemplates` / `new TrainStationComponent()` の呼び出し側（`grep -rn` で全件）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStation/Naming/TrainStationDefaultNameAllocatorTest.cs`

**Interfaces:**
- Consumes: Task 1 の `TrainStationFeatureEnglishNames`、Task 2 の `TrainStationNameFeatureScanner` / `TrainStationNameFeature.FormatName`
- Produces: `TrainStationDefaultNameAllocator(TrainStationNameFeatureScanner, TrainStationFeatureEnglishNames)`、`string Allocate(BlockPositionInfo position)`

- [ ] **Step 1: 割り当てを書く**

```csharp
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Context;

namespace Game.Block.Blocks.TrainRail.Naming
{
    // 新設駅の既定名を、全駅名と重ならない最小の番号で決める
    // Decide a new station's default name with the smallest number not used by any station
    public class TrainStationDefaultNameAllocator
    {
        private readonly TrainStationNameFeatureScanner _scanner;
        private readonly TrainStationFeatureEnglishNames _names;

        public TrainStationDefaultNameAllocator(TrainStationNameFeatureScanner scanner, TrainStationFeatureEnglishNames names)
        {
            _scanner = scanner;
            _names = names;
        }

        public string Allocate(BlockPositionInfo position)
        {
            var feature = _scanner.Scan(position);
            var existingNames = CollectExistingNames();
            var number = 1;
            while (existingNames.Contains(feature.FormatName(_names, number))) number++;
            return feature.FormatName(_names, number);

            #region Internal

            HashSet<string> CollectExistingNames()
            {
                // 設置中の駅はまだdatastoreに居ないので含まれない
                // The station being placed is not in the datastore yet, so it is excluded
                var names = new HashSet<string>();
                foreach (var data in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
                {
                    if (data.Block.TryGetComponent<TrainStationComponent>(out var station)) names.Add(station.StationName);
                }
                return names;
            }

            #endregion
        }
    }
}
```

- [ ] **Step 2: テンプレートへ組み込む**

- `VanillaTrainStationTemplate`: ctor に `TrainStationDefaultNameAllocator defaultNameAllocator` を足し、`New` の `new TrainStationComponent()` を `new TrainStationComponent(_defaultNameAllocator.Allocate(positionInfo))` に変える。`Load` は変更なし
- `VanillaIBlockTemplates`: ctor に `IItemMapVeinDatastore`、`IFluidMapVeinDatastore`、`IMapObjectDatastore`、`TrainStationFeatureEnglishNames` を足し、`new TrainStationDefaultNameAllocator(new TrainStationNameFeatureScanner(itemVeins, fluidVeins, mapObjects), englishNames)` を作って `VanillaTrainStationTemplate` へ渡す（同じ initializer コンテナに登録済み: `ServerContextRegistration.cs:111-114`）

- [ ] **Step 3: テストを書く**（`TrainTestHelper.CreateEnvironment()` で実サーバー環境を作り、`ServerContext.WorldBlockDatastore.TryAddBlock` で `ForUnitTestModBlockId.TestTrainStation` を置く。ForUnitTest mod に localization.csv が無ければ英語名はマスタ name（Test1 の `name`）になる）

```csharp
[Test]
public void SameFeatureGetsSmallestFreeNumber()
{
    TrainTestHelper.CreateEnvironment();
    var first = PlaceStation(Vector3Int.zero);
    var second = PlaceStation(new Vector3Int(4, 0, 0));
    Assert.AreEqual($"{Test1Name} Station", first.StationName);
    Assert.AreEqual($"{Test1Name} Station 2", second.StationName);

    // 2番の駅を3番の名前へ改名すると、次は空いた2番、その次は4番になる
    // Renaming station 2 to number 3 frees 2 for the next station and pushes the one after to 4
    second.SetStationName($"{Test1Name} Station 3");
    Assert.AreEqual($"{Test1Name} Station 2", PlaceStation(new Vector3Int(8, 0, 0)).StationName);
    Assert.AreEqual($"{Test1Name} Station 4", PlaceStation(new Vector3Int(12, 0, 0)).StationName);
}

[Test]
public void RemovedStationNumberIsReused()
{
    TrainTestHelper.CreateEnvironment();
    PlaceStation(Vector3Int.zero);
    PlaceStation(new Vector3Int(4, 0, 0));
    ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(4, 0, 0), BlockRemoveReason.ManualRemove);
    Assert.AreEqual($"{Test1Name} Station 2", PlaceStation(new Vector3Int(4, 0, 0)).StationName);
}

[Test]
public void FallbackNameWhenNothingNearby()
{
    TrainTestHelper.CreateEnvironment();
    Assert.AreEqual("Station", PlaceStation(new Vector3Int(60, 0, 60)).StationName);
    Assert.AreEqual("Station 2", PlaceStation(new Vector3Int(64, 0, 60)).StationName);
}
```

（`PlaceStation` は置いたブロックから `TrainStationComponent` を返すテスト内ヘルパー。`Test1Name` は ForUnitTest の items.json の Test1（`00000000-0000-0000-1234-000000000001`）の `name`。(4,0,0)・(8,0,0)・(12,0,0) の駅が原点の駅と同じ特徴（Test1 鉱脈）になるか、(64,0,60) が代替になるかを Task 2 の距離計算で確かめ、ならなければ位置をずらす。`RemoveBlock` の正確なシグネチャは `IWorldBlockDatastore` で確認。）

既存 `TrainStationNameTest` の「初期名は空文字」は「初期名は `$"{Test1Name} Station"`」へ書き換える。

- [ ] **Step 4: コンパイルしテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainStation|Train.*Station|RequestBlockInventoryTest|TrainSchedule"`
Expected: ErrorCount 0 / PASS（既存の駅関連テストも全て通る）

- [ ] **Step 5: コミット** `git commit -m "feat: 新設駅に周辺の特徴から英語の既定名を付ける"`

---

### Task 4: unityプレイ録画テスト

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/train/train-station-default-name.cs`

- [ ] **Step 1:** `unity-playmode-recorded-playtest` スキルの DSL で、v8 ワールドの鉱脈の近く（`map.json` の mapVeins から1つ選ぶ）と、鉱脈も地物も無い場所に駅を1つずつ置き、それぞれの駅ブロックを F で開いて駅名欄を録画する。1つ目は "<鉱石の英語名> Station"、2つ目は "Station" であることをスクショで確かめる。続けて同じ鉱脈の近くにもう1つ置き、"… Station 2" になることを確かめる
- [ ] **Step 2:** シナリオを `run-scenario.sh` で実行し、result.json とスクショ・録画のパスを PR 本文用に控える。駅ブロック UI が開けない等で止まった場合は、原因を（実測／推測の札付きで）報告ファイルへ書き、bd に起票して PR 本文の未検証欄に明記する
- [ ] **Step 3: コミット** `git commit -m "test: 駅の既定名を録画プレイテストで確認するシナリオを追加"`

---

### Task 5: 全ブランチレビュー

- [ ] 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー指摘の反映が判定経路（走査・番号の決め方）や条件式に触れたら、Task 4 の録画テストを反映後に再実施する

### Task 6: PR 作成とセッション終了可能状態

- [ ] セッション終了可能状態にすること: pr-create スキルで PR を作成する。ベースは `feature/train-timetable-ui`（PR #1415）。PR 本文に「PR #1415 の拡張・別PR」と ADR 0079 へのリンクを書く。ベースとのコンフリクトがあればマージして解消・コンパイル確認のうえ push する（解消の実作業は pr-create 経由で opus subagent に委譲される）。全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態になっていることを確認して終える。PR 作成後に `moores-wt rm train-station-default-name`

---

## 判断記録（ADR）

- 設計 ADR: `docs/adr/0079-train-station-default-name-from-nearby-features.md`
- 裁定: `.decisions/2026-10-13-駅の既定名*.md`。うち「構造で保存し表示時に翻訳」「番号は特徴ごとの連番で再利用しない」は、同日の後続裁定（英語の文字列で確定する／空いている最小番号にする）で覆った
- planning 中の判断:
  - 計画の初版（構造保存・カウンタ・セーブv6・DTO/Web UI改修）は、型検査への回答でユーザーが「複雑過ぎ」と差し戻したため、文字列で確定する最小構成へ作り直した（出所: ユーザー裁定 2026-10-13、ADR 0079）
  - 特徴は抽象クラス＋Item/Fluid/Fallback のサブクラスにし、名前の書式は各型が持つ。判別子と使わない欄（Guid.Empty 等）を並べた形にしない（出所: agent判断。初版の型検査の強い指摘を、作り直した特徴の型にも当てた）
  - 全駅の列挙は `ServerContext.WorldBlockDatastore` を呼び出し時に引く。`WorldBlockDatastore` は `BlockFactory` → `VanillaIBlockTemplates` に依存し、注入すると DI が循環するため
  - `ModEnglishContentNameLoader.cs` は Server.Boot に置く。`ModsResource` を持つのは Server.Boot で、Game.Block には英語名の表（`TrainStationFeatureEnglishNames.cs`）だけを渡す。ADR 0006 決定4（サーバーは辞書に非関与）の例外で、用途は既定名の生成に限る（ADR 0079 agent前提）
  - `VanillaTrainStationTemplate.cs` は ctor で `TrainStationDefaultNameAllocator` を受け取り、`New` だけで既定名を付ける（`Load` はセーブ値）
  - `VanillaIBlockTemplates.cs` の ctor に鉱脈・地物 datastore と英語名の表を足し、走査と割り当てを組み立てて駅テンプレートへ渡す（同じ initializer コンテナ内なので注入できる）
  - `TrainStationComponent.cs` の引数なし ctor を消し、新設駅は必ず名前を受け取る
  - `SetTrainStationNameProtocol.cs` は変更しない（改名は今までどおり文字列を上書きする）
  - 録画プレイテスト（Task 4）を含める。駅名は UI に出るランタイム挙動のため
  - 型検査（作り直し後）の弱3件（CSVキーの接頭辞判定・「番号1は番号なし」とGUID順ソートの重複記述）は採らない（出所: ユーザー指示 2026-10-13「たかが自動名前付けでそんなに色々考える必要ある？」を細部の簡素優先と解釈した agent判断）
  - 本PR外のリファクタ提案: `ModEnglishContentNameLoader` とクライアント `ModLocalizationMerger.cs` の CSV 読み込みは重複する（共通化は本PR外）
  - 本PR外のリファクタ提案（初版の型検査の第3バケツ）: ブロックと鉱脈の XZ の幾何を `Game.Block.Interface/Extension/BlockPositionInfoExtension.cs`（`OverlapsVeinXz`）へ集める案。今回は距離計算をスキャナー内に置く
