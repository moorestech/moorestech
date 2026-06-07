# アップグレードシステム フェーズB（品質軸／レベルファミリー）実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **API整合の前提:** API名（`MachineModuleEffect` / `VanillaMachineProcessorComponent` の決定的抽選 `DeterministicRoll`・追加出力API・仮想容量予約）は Phase A 計画（`2026-06-05-upgrade-system-phase-a.md`、特に A3-1/A3-4）に追従する。Phase A 実装でシグネチャが変わったら本プランも同期。

> **⚠ ユーザー確認が必要（B-0）:** 設計仕様§9で未決だった3点を、本プランでは**推奨デフォルト**を決め打ちして以降を詳細化している。実装着手前に **B-0 の3決定をユーザーが承認/調整**すること。デフォルトのまま進めてよいが、品質シフトの式とレベル定義の置き場所はゲームバランス/データ運用に直結するため、明示確認を推奨。

**Goal:** 品質（Quality）モジュールで、機械の産出物が決定的な確率分布に従って上位レベル（Mk2/Mk3…）の独立アイテムになる。レベル違いは独立ItemId（設計仕様§3決定）で表現し、ファミリー定義・レベル抽選・品質シフト・抽選順序の決定性（§7.2）を実装する。

**Architecture:** 新スキーマ `levelFamilies.yml` でファミリー（基準アイテム＋順序付きレベル変種＋アップグレード合成レシピ参照）を定義。`LevelFamilyMaster` が「基準ItemId＋レベル番号→変種ItemId」を解決。`MachineModuleEffect` に Quality 軸の集計（品質シフト量）を追加し clamp。`VanillaMachineProcessorComponent` の完了時出力で、Phase A の決定的抽選（`DeterministicRoll`）を使い、出力アイテムを確率的に上位レベルへ差し替える。仮想容量予約（A3-4）は変種出力にも効く。

**Tech Stack:** Unity / C# / NUnit / Mooresmaster SourceGenerator / uloop CLI

**設計仕様:** `docs/superpowers/specs/2026-06-05-upgrade-system-design.md`（§3 レベル表現, §5.3 clamp, §7.2 抽選順序, §8.5 フェーズB）
**前提プラン:** `2026-06-05-upgrade-system-phase-a.md`（A3-4 まで完了していること）

---

# タスク群B-0: 未決事項の決定（着手前にユーザー承認）

設計仕様§9で未決だった3点を、以下の**推奨デフォルト**で確定する。各 `- [ ]` はユーザー承認のチェック（コードは書かない）。

### Decision 1: レベル定義の置き場所 → **新スキーマ `levelFamilies.yml`**

- [ ] **承認: items.yml 同居ではなく新規スキーマにする**

理由: ファミリーは「基準アイテム＋順序付きレベル変種＋アップグレード合成」という関係概念で、フラットなアイテム一覧（items.yml）とは責務が違う。Phase A の `modules.yml`（新概念=新スキーマ）と同じ判断。変種アイテム自体は引き続き `items.json`（データ）に存在し、`levelFamilies.yml` はそれらを束ねる。

### Decision 2: 品質シフトの計算式 → **隣接2レベルの決定的分布**

- [ ] **承認: 下記の式を採用する**

品質モジュールの効果合計 `qualitySum = Σ effectValue`（clamp `[0, maxLevelUp]`、`maxLevelUp = レベル数-1`）。
- 整数部 `floor(qualitySum)` = 必ず上がるレベル数。
- 小数部 `frac(qualitySum)` = もう1段上がる確率。
- 産出レベル = `1 + floor(qualitySum) + (roll < frac ? 1 : 0)`、`maxLevel` で上限clamp。
- `roll` は Phase A の `DeterministicRoll`（`blockInstanceId`＋永続サイクルカウント由来、§7.2）。

この式の利点: 1産出あたり**隣接2レベルだけの分布**（確率 `frac` と `1-frac`）なので「分布和=1」（§5.3）が自明に成立、決定的、実装が単純、低レベル確率の圧迫（§9）も「分布が上位へ平行移動」として直感的。

> **代替案（不採用）:** 全レベルにわたる連続分布（低レベルを指数的に圧迫）。表現力は高いが、分布和=1のclampと決定的抽選の実装が重く、初版には過剰。必要になればB完了後に式だけ差し替え可能（集計部を純粋関数に隔離するため）。

### Decision 3: 変種アイテムのGUIDと自動生成 → **明示変種＋決定的GUIDユーティリティ（ランタイム自動生成ツールは後続）**

- [ ] **承認: 変種アイテムは items.json に明示し、GUIDは `DeterministicGuidUtil` で基準から算出。エディタ自動生成ツールは別タスク**

理由: 調査の結果、SourceGenerator は**型**を生成するが**データ（items.json の実エントリ）**は生成しない。決定的GUIDユーティリティもコード内に存在しない。よって初版は「変種アイテムを items.json に置く＋GUIDを `{baseItemGuid}:lv{n}` から決定的に算出するユーティリティを追加」する最小構成にする。設計仕様§3の「スキーマ自動生成」の理想（ファミリー定義から変種アイテム＋レシピをエディタが自動生成）は、別途エディタ拡張タスクとして B 完了後に積む（本プランの「残課題」に記載）。

---

## ファイル構成

**新規（スキーマ/生成）:**
- `VanillaSchema/levelFamilies.yml` — ファミリー定義スキーマ
- 修正 `moorestech_server/Assets/Scripts/Core.Master/csc.rsp` — `levelFamilies.yml` 登録
- 修正 `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — `dummyText` 更新

**新規（マスタ/ユーティリティ）:**
- `moorestech_server/Assets/Scripts/Core.Master/LevelFamilyMaster.cs` — ファミリー保持・変種解決（`ModuleMaster.cs` テンプレート）
- `moorestech_server/Assets/Scripts/Core.Master/DeterministicGuidUtil.cs` — seed文字列→決定的GUID
- 修正 `moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs` — `LevelFamilyMaster` 追加（許容ロード）

**修正（効果/抽選）:**
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Module/MachineModuleEffect.cs` — Quality 軸集計（A3-1で空けた分岐）
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs` — 完了出力をレベル変種へ差し替え（決定的抽選＋容量予約）

**テスト:**
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/DeterministicGuidUtilTest.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/MachineModuleEffectTest.cs`（Quality追記）
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/LevelFamilyMasterTest.cs`
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/QualityModuleOutputTest.cs`
- テストmod: `master/levelFamilies.json` ＋ `items.json`（変種アイテム）

---

# タスク群B-1: 決定的GUIDユーティリティ

ゴール: 同じ seed 文字列から常に同じGUIDが得られる。

### Task B-1-1: 失敗するテストを書く

**Files:**
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/DeterministicGuidUtilTest.cs`

- [ ] **Step 1: テストを書く**

```csharp
using System;
using Core.Master;
using NUnit.Framework;

namespace Tests.UnitTest.Core.Other
{
    public class DeterministicGuidUtilTest
    {
        // 同一seedは同一GUID、異なるseedは異なるGUIDを返すことを検証
        // Same seed yields same GUID; different seeds yield different GUIDs
        [Test]
        public void DeterministicTest()
        {
            var a1 = DeterministicGuidUtil.Create("base-guid:lv2");
            var a2 = DeterministicGuidUtil.Create("base-guid:lv2");
            var b  = DeterministicGuidUtil.Create("base-guid:lv3");
            Assert.AreEqual(a1, a2);
            Assert.AreNotEqual(a1, b);
            Assert.AreNotEqual(Guid.Empty, a1);
        }
    }
}
```

- [ ] **Step 2: 失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "DeterministicGuidUtilTest"`
Expected: FAIL（未定義）。

### Task B-1-2: 実装

**Files:**
- Create: `moorestech_server/Assets/Scripts/Core.Master/DeterministicGuidUtil.cs`

- [ ] **Step 1: 実装（MD5ハッシュ→16バイト→Guid。UUIDv5系）**

```csharp
using System;
using System.Security.Cryptography;
using System.Text;

namespace Core.Master
{
    // seed文字列から決定的にGUIDを生成する（UUIDv5系。MD5の先頭16バイトを使用）
    // Generate a deterministic GUID from a seed string (UUIDv5-style, first 16 bytes of MD5)
    public static class DeterministicGuidUtil
    {
        public static Guid Create(string seed)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(seed));
            return new Guid(hash); // 16バイトちょうど
        }
    }
}
```

> try-catch 禁止規約に注意（本実装は例外を投げない）。`MD5` 使用は暗号用途でないため問題なし（衝突耐性は不要、決定性のみ要件）。

- [ ] **Step 2: コンパイル → テスト PASS**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "DeterministicGuidUtilTest"`
Expected: PASS。

- [ ] **Step 3: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Core.Master/DeterministicGuidUtil.cs \
  moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/DeterministicGuidUtilTest.cs
git commit -m "feat(master): add DeterministicGuidUtil for level-family GUIDs"
```

---

# タスク群B-2: levelFamilies スキーマとマスタ

ゴール: `MasterHolder.LevelFamilyMaster` で「基準ItemId＋レベル→変種ItemId」を解決できる。

> Phase A A1（modules.yml）と完全に同じ手順構成。実順序: **B-2-1（スキーマ）→ B-2-2（生成登録）→ B-2-4（テストデータ）→ B-2-3（マスタ実装＋配線）**。`modules.json` 不在mod対策（許容ロード）も同様に行う。

### Task B-2-1: levelFamilies.yml スキーマ

**Files:**
- Create: `VanillaSchema/levelFamilies.yml`

- [ ] **Step 1: スキーマ作成**

`modules.yml`（Phase A A1-1）の記法に合わせる。`baseItemGuid` ＋ 順序付き `levelItems`（各レベルのitemGuidとアップグレード合成レシピ参照）。

```yaml
id: levelFamilies
type: object
isDefaultOpen: true

properties:
- key: data
  type: array
  overrideCodeGeneratePropertyName: LevelFamilyMasterElement
  items:
    type: object
    properties:
    - key: familyGuid
      type: uuid
      autoGenerated: true
    - key: name
      type: string
    - key: baseItemGuid
      type: uuid
      foreignKey:
        schemaId: items
        foreignKeyIdPath: /data/[*]/itemGuid
        displayElementPath: /data/[*]/name
    - key: levelItemGuids
      type: array
      items:
        type: uuid
        foreignKey:
          schemaId: items
          foreignKeyIdPath: /data/[*]/itemGuid
          displayElementPath: /data/[*]/name
```

> `levelItemGuids[0]` = レベル1（=基準と同一でもよい）、`[1]`=レベル2… の順序付き配列。アップグレード合成レシピ（craftRecipes）は本配列とは独立に craftRecipes.json に定義し、入力=下位レベル/出力=上位レベルで結ぶ（B-2-4で最小データ）。配列要素型が `uuid` で生成できない場合は object でラップ（`{ itemGuid: uuid }`）に変更し、実生成型に合わせる。

- [ ] **Step 2: コミット**

```bash
cd ~/moorestech
git add VanillaSchema/levelFamilies.yml
git commit -m "feat(schema): add levelFamilies.yml schema"
```

### Task B-2-2: SourceGenerator 登録

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Core.Master/csc.rsp`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`

- [ ] **Step 1: csc.rsp に1行追加**（`modules.yml` の隣、同書式 `/additionalfile:...levelFamilies.yml`）
- [ ] **Step 2: `_CompileRequester.cs` の `dummyText` 変更**（末尾に `-levelfamilies` 等）
- [ ] **Step 3: Unity再起動で生成トリガー**
- [ ] **Step 4: コンパイルで生成型確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。`Mooresmaster.Model.LevelFamiliesModule` / `LevelFamilyMasterElement` / `Mooresmaster.Loader.LevelFamiliesModule.LevelFamiliesLoader` が生成。

- [ ] **Step 5: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Core.Master/csc.rsp moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git commit -m "build(master): register levelFamilies.yml with source generator"
```

### Task B-2-3: LevelFamilyMaster 実装＋配線

**Files:**
- Create: `moorestech_server/Assets/Scripts/Core.Master/LevelFamilyMaster.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/LevelFamilyMasterTest.cs`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Other
{
    public class LevelFamilyMasterTest
    {
        // 基準ItemIdとレベル番号から変種ItemIdを解決できることを検証
        // Verify variant ItemId resolves from base ItemId + level number
        [Test]
        public void ResolveVariantTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.NotNull(MasterHolder.LevelFamilyMaster);
            var family = MasterHolder.LevelFamilyMaster.Families.Data[0];
            var baseItemId = MasterHolder.ItemMaster.GetItemId(family.BaseItemGuid);

            // レベル2(=index1)の変種ItemIdが解決でき、基準とは別IDであること
            // Level 2 (index 1) variant resolves to a different ItemId than base
            var lv2 = MasterHolder.LevelFamilyMaster.GetVariantItemId(baseItemId, 2);
            Assert.AreNotEqual(baseItemId, lv2);
            // 上限超えは最大レベルにclamp（存在しないレベルでも落ちない）
            // Out-of-range level clamps to max (does not throw)
            var capped = MasterHolder.LevelFamilyMaster.GetVariantItemId(baseItemId, 999);
            Assert.AreEqual(MasterHolder.LevelFamilyMaster.GetMaxLevelItemId(baseItemId), capped);
        }
    }
}
```

- [ ] **Step 2: 失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "LevelFamilyMasterTest"`
Expected: FAIL（未定義）。

- [ ] **Step 3: LevelFamilyMaster を実装**（`ModuleMaster.cs` テンプレート）

```csharp
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.LevelFamiliesModule;
using Mooresmaster.Model.LevelFamiliesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    // レベルファミリー定義を保持し、基準ItemId＋レベル→変種ItemId を解決するマスタ
    // Master holding level families; resolves variant ItemId from base ItemId + level
    public class LevelFamilyMaster : IMasterValidator
    {
        public readonly LevelFamilies Families;
        // baseItemId -> レベル順の変種ItemId配列（index0=レベル1）
        // baseItemId -> level-ordered variant ItemId array (index0 = level 1)
        private Dictionary<ItemId, ItemId[]> _byBase;

        public LevelFamilyMaster(JToken jToken)
        {
            Families = LevelFamiliesLoader.Load(jToken);
        }

        public ItemId GetVariantItemId(ItemId baseItemId, int level)
        {
            // レベルは1始まり。範囲外は[1,max]にclamp
            // Level is 1-based; clamp out-of-range into [1, max]
            var arr = _byBase[baseItemId];
            var idx = level - 1;
            if (idx < 0) idx = 0;
            if (idx >= arr.Length) idx = arr.Length - 1;
            return arr[idx];
        }

        public ItemId GetMaxLevelItemId(ItemId baseItemId)
        {
            var arr = _byBase[baseItemId];
            return arr[arr.Length - 1];
        }

        public bool HasFamily(ItemId baseItemId) => _byBase.ContainsKey(baseItemId);

        public bool Validate(out string errorLog)
        {
            errorLog = "";
            foreach (var f in Families.Data)
            {
                if (MasterHolder.ItemMaster.GetItemIdOrNull(f.BaseItemGuid) == null)
                    errorLog += $"[LevelFamilyMaster] Name:{f.Name} invalid BaseItemGuid:{f.BaseItemGuid}\n";
                foreach (var g in f.LevelItemGuids)
                    if (MasterHolder.ItemMaster.GetItemIdOrNull(g) == null)
                        errorLog += $"[LevelFamilyMaster] Name:{f.Name} invalid level itemGuid:{g}\n";
            }
            return errorLog.Length == 0;
        }

        public void Initialize()
        {
            _byBase = Families.Data.ToDictionary(
                f => MasterHolder.ItemMaster.GetItemId(f.BaseItemGuid),
                f => f.LevelItemGuids.Select(g => MasterHolder.ItemMaster.GetItemId(g)).ToArray());
        }
    }
}
```

> `IMasterValidator` の正確なシグネチャ・`ItemId` 型・`GetItemIdOrNull` は `ItemMaster.cs`/`ModuleMaster.cs`（Phase A）で確認して合わせる。`LevelItemGuids` の生成型（`Guid[]` か object配列か）は B-2-1 の生成結果に合わせる。

- [ ] **Step 4: MasterHolder に許容ロードで追加**

Phase A A1-3 Step4 の `TryGetJsonOrNull` パターンを再利用。ItemMaster の後にロード:

```csharp
public static LevelFamilyMaster LevelFamilyMaster { get; private set; }
// ...Load内、ItemMaster後...
var familiesJson = TryGetJsonOrNull(masterJsonFileContainer, new JsonFileName("levelFamilies"))
                   ?? JToken.Parse("{\"data\":[]}");
LevelFamilyMaster = new LevelFamilyMaster(familiesJson);
InitializeMaster(LevelFamilyMaster);
```

- [ ] **Step 5: コンパイル → テスト PASS**（B-2-4 のテストデータ投入後）

### Task B-2-4: テスト用 levelFamilies.json ＋ 変種アイテム

**Files:**
- Create: テストmod `master/levelFamilies.json`
- Modify: テストmod `master/items.json`（レベル2変種アイテムを追加）
- Modify: テストmod `master/craftRecipes.json`（任意: レベルアップ合成。本プランの抽選テストには不要なら省略可）

- [ ] **Step 1: 変種アイテムを items.json に追加**

基準アイテム（既存）に対し、レベル2変種を1件追加。GUIDは `DeterministicGuidUtil.Create("<baseGuid>:lv2")` で算出した固定値を記載（テストデータは静的なので算出値をベタ書き。算出は一度ローカルで実行 or 既存の autoGenerated GUID 形式に合わせて任意の固定UUIDでも可）。

- [ ] **Step 2: levelFamilies.json を作成**

```json
{
  "data": [
    { "name": "TestFamily", "baseItemGuid": "<基準アイテムGUID>", "levelItemGuids": ["<基準アイテムGUID>", "<レベル2変種GUID>"] }
  ]
}
```

- [ ] **Step 3: コンパイル → 既存テスト回帰なし＋ LevelFamilyMasterTest PASS**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "LevelFamilyMasterTest|MachineIOTest"`
Expected: PASS。

- [ ] **Step 4: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Core.Master/LevelFamilyMaster.cs \
  moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs \
  moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/LevelFamilyMasterTest.cs \
  moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/
git commit -m "feat(master): add LevelFamilyMaster and level-family test data"
```

---

# タスク群B-3: MachineModuleEffect に品質シフトを実装

ゴール: Quality モジュールの効果が品質シフト量として集計され clamp される。

### Task B-3-1: 失敗するテスト（Quality集計）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/MachineModuleEffectTest.cs`

- [ ] **Step 1: テストを追記**

```csharp
        // 品質モジュール効果が QualityShift に加算され、整数/小数部に分解できることを検証
        // Verify quality module effect accumulates into QualityShift (integer + fractional parts)
        [Test]
        public void QualityShiftAccumulateTest()
        {
            var q = NewModule("Quality", 0.7f, 0.0f);
            var effect = MachineModuleEffect.Aggregate(new System.Collections.Generic.List<ModuleMasterElement> { q, q });
            // 0.7 + 0.7 = 1.4 → 必ず1段上げ + 0.4 の確率でもう1段
            Assert.AreEqual(1.4f, effect.QualityShift, 0.001f);
        }

        // 品質効果なしでは QualityShift = 0（中立）
        // No quality effect → QualityShift = 0 (neutral)
        [Test]
        public void QualityNeutralTest()
        {
            var effect = MachineModuleEffect.Aggregate(new System.Collections.Generic.List<ModuleMasterElement>());
            Assert.AreEqual(0f, effect.QualityShift, 0.001f);
        }
```

- [ ] **Step 2: 失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineModuleEffectTest"`
Expected: FAIL（`QualityShift` 未定義）。

### Task B-3-2: MachineModuleEffect 実装

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Module/MachineModuleEffect.cs`

- [ ] **Step 1: Quality 分岐と `QualityShift` を追加**

A3-1 で空けてあった `// Quality はフェーズBで扱う` のコメント箇所を実装。`QualityShift` フィールドを追加し、`Aggregate` で集計＋clamp:

```csharp
public readonly float QualityShift;

// ...Aggregate内: 軸ごと合算ループに Quality を追加...
case "Quality": qualitySum += m.EffectValue; break;

// ...集計後（clamp）...
// 品質シフト量。負はなし。上限は呼び出し側のレベル数に依存するためここでは下限のみ
// Quality shift amount; no negatives. Upper clamp depends on level count (done by caller)
var qualityShift = qualitySum < 0f ? 0f : qualitySum;

// コンストラクタ・return に qualityShift を追加
```

`MachineModuleEffect` のコンストラクタ引数とフィールドに `QualityShift` を足す（A3-1 の `ProcessingTimeMultiplier`/`PowerMultiplier`/`ExtraOutputChance` と並べる）。

> 上限clamp（`maxLevelUp = レベル数-1`）は**産出時にレベル数が分かる呼び出し側**（B-4）で行う。集計段では下限0のみ。

- [ ] **Step 2: コンパイル → テスト PASS**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineModuleEffectTest"`
Expected: 既存3＋新規2 PASS。

- [ ] **Step 3: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Module/MachineModuleEffect.cs \
  moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/MachineModuleEffectTest.cs
git commit -m "feat(block): add quality shift aggregation to MachineModuleEffect"
```

---

# タスク群B-4: プロセッサ完了出力のレベル抽選

ゴール: 完了時、基準アイテムの出力を品質分布で上位レベル変種へ決定的に差し替える。

> **Codex監査反映の整合（Phase A A3-4）:** 出力差し替えは A3-4 の「追加出力API（`InsertItemOutputsOnly`）＋仮想容量予約（`CanStoreOutputs`）＋決定的抽選（`DeterministicRoll`）」と同じ仕組みに乗せる。変種アイテムも独立ItemId（独立スタック）なので、容量予約は変種IDで見積もる。レベルアップで出力スタックが分かれて埋まる場合に消失しないこと。

### Task B-4-1: 失敗するテスト（品質モジュールで上位レベル産出）

**Files:**
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/QualityModuleOutputTest.cs`

- [ ] **Step 1: テストを書く**

基準アイテムを産むレシピの機械に、`floor(qualitySum) >= 1` になる品質モジュールを装着 → 完了時に必ずレベル2変種が出ることを検証（整数部1段は確定なので決定的に検証可能）。

```csharp
using System;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class QualityModuleOutputTest
    {
        // qualitySum>=1 の品質モジュールで、完了時に基準でなくレベル2変種が出力されることを検証
        // With qualitySum>=1, completion outputs the level-2 variant instead of the base item
        [Test]
        public void QualityModuleProducesUpgradedOutputTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            // 出力が「ファミリー基準アイテム」であるレシピを使う（テストデータで保証）
            // Use a recipe whose output is the family base item (guaranteed by test data)
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(/* 出力=基準アイテム のレシピ */);
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, new Vector3Int(0,0,0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            // qualitySum>=1 になるよう品質モジュールを装着（effectValue=1.0 のもの、または2枚）
            var quality = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == "Quality");
            var qItemId = MasterHolder.ItemMaster.GetItemId(quality.ItemGuid);
            block.GetComponent<IModuleSlotInventoryComponent>().TryInsertModule(0, itemStackFactory.Create(qItemId, 1));

            // 入力投入 → 1サイクル完了まで進める
            foreach (var input in recipe.InputItems)
                block.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(itemStackFactory.Create(input.ItemGuid, input.Count));
            var proc = block.GetComponent<VanillaMachineProcessorComponent>();
            for (uint i = 0; i < GameUpdater.SecondsToTicks(recipe.Time) + 2; i++) { proc.SupplyPower(100000); GameUpdater.RunFrames(1); }

            // 出力スロットにレベル2変種が入っていること
            var baseItemGuid = recipe.OutputItems[0].ItemGuid;
            var baseItemId = MasterHolder.ItemMaster.GetItemId(baseItemGuid);
            var lv2 = MasterHolder.LevelFamilyMaster.GetVariantItemId(baseItemId, 2);
            var outputInv = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.IsTrue(OutputContains(outputInv, lv2));
            Assert.IsFalse(OutputContains(outputInv, baseItemId));

            #region Internal
            bool OutputContains(VanillaMachineBlockInventoryComponent inv, ItemId id) { /* 出力スロット走査。既存テストの出力確認方法を踏襲 */ return false; }
            #endregion
        }
    }
}
```

> レシピ・出力スロット確認方法・`SupplyPower`/tick進行は Phase A の A3-2/A3-4 テスト、既存 `MachineIOTest` を Read して合わせる。テストデータは「出力が基準アイテム＝ファミリーbase のレシピ」と「そのレベル2変種」を B-2-4 で用意済みにしておく。

- [ ] **Step 2: 失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "QualityModuleOutputTest"`
Expected: FAIL（まだ基準アイテムが出力される）。

### Task B-4-2: 完了出力のレベル差し替えを実装

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`

- [ ] **Step 1: 完了時の出力生成にレベル抽選を挟む**

A3-4 の完了出力処理（`InsertItemOutputsOnly` / `DeterministicRoll` / 容量予約）の直前で、各出力アイテムについてファミリーがあればレベルを抽選して変種IDへ差し替える純粋ヘルパーを追加。スナップショットした `QualityShift` を使う。

```csharp
// 出力アイテムを品質分布で上位レベルへ差し替える（ファミリーが無ければ素通し）
// Replace an output item with an upgraded variant per quality distribution (pass-through if no family)
private IItemStack ApplyQualityLevel(IItemStack output)
{
    var baseId = output.Id;
    if (!MasterHolder.LevelFamilyMaster.HasFamily(baseId)) return output;

    var shift = _currentEffect != null ? _currentEffect.QualityShift : 0f;
    if (shift <= 0f) return output;

    // 整数部=確定上げ、小数部=決定的抽選で+1
    // Integer part = guaranteed level-ups; fractional part = +1 via deterministic roll
    var guaranteed = (int)System.Math.Floor(shift);
    var frac = shift - guaranteed;
    var extra = DeterministicRoll() < frac ? 1 : 0;   // A3-4 と同じ決定的抽選（サイクルカウント前進）
    var level = 1 + guaranteed + extra;               // GetVariantItemId 側で最大レベルにclamp

    var variantId = MasterHolder.LevelFamilyMaster.GetVariantItemId(baseId, level);
    return ServerContext.ItemStackFactory.Create(variantId, output.Count);
}
```

差し替えは「仮想容量予約（`CanStoreOutputs`）の見積もり」と「実出力（`InsertItemOutputsOnly`）」の**両方**で同じ `ApplyQualityLevel` を通すこと（変種IDで容量を見積もらないと、レベルアップで別スタックが埋まり消失しうる）。

> `_currentEffect` / `DeterministicRoll` / `InsertItemOutputsOnly` / `CanStoreOutputs` の正確な名前は A3-4 実装で確認。`DeterministicRoll` を品質抽選にも使うと、生産性抽選（A3-4）とサイクルカウントを共有する点に注意——両者で `DeterministicRoll` を呼ぶ順序が決定性に影響する。**1サイクル内の抽選順序を固定**（例: 生産性→品質）し、その順序をコメントで明示（§7.2）。

- [ ] **Step 2: Unity再起動 → コンパイル → テスト PASS**

Run: Unity再起動後 `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "QualityModuleOutputTest"`
Expected: PASS。

- [ ] **Step 3: 容量予約整合テストを追加**

出力スロットが満杯近くで、レベルアップ変種が入る容量が無い場合に、A3-4 同様**処理が開始されない／アイテム消失しない**ことを検証（変種IDでの予約が効いているか）。

- [ ] **Step 4: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs \
  moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/QualityModuleOutputTest.cs
git commit -m "feat(block): apply quality level upgrade to machine output with deterministic roll"
```

---

# タスク群B-5: セーブ/同期整合

ゴール: レベル変種ItemIdがセーブ・A2同期で矛盾なく round-trip する。

### Task B-5-1: 変種出力のセーブ round-trip

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/QualityModuleOutputTest.cs`

- [ ] **Step 1: テストを追加**

変種が出力スロットに入った機械をセーブ→ロードし、出力スロットの変種ItemIdが保持されることを検証。変種は独立ItemIdなので既存のアイテム保存（`ItemStackSaveJsonObject` = itemGuid+count）にそのまま乗るはず。落ちる場合は変種アイテムが items.json に存在しない（GUID不一致）ことを疑う。

```csharp
        // レベル変種の出力がセーブ/ロードを跨いで保持されることを検証
        // Verify upgraded variant output survives a save/load round-trip
        [Test]
        public void VariantOutputSaveLoadTest()
        {
            // ... B-4-1 で変種を産出させた block を GetSaveState → BlockFactory.Load → 出力スロットに変種IDが残る ...
        }
```

- [ ] **Step 2: 実行 → PASS**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "QualityModuleOutputTest"`
Expected: 全PASS。

- [ ] **Step 3: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/QualityModuleOutputTest.cs
git commit -m "test(block): verify variant output save/load round-trip"
```

> **同期（A2）整合:** 変種は独立ItemIdなので、A2 のインベントリ同期（`ItemMessagePack` = Id+Count）にそのまま乗る。追加実装は不要だが、A2 実装済みなら手動で機械出力にレベル変種が表示されることを実機確認するとよい（自動テストは上記サーバー側で十分）。

---

## フェーズB完了条件

- [ ] B-0 の3決定をユーザーが承認（レベル定義=新スキーマ / 品質式=隣接2レベル決定的分布 / 変種=明示＋決定的GUID）
- [ ] `DeterministicGuidUtil` で seed→GUID が決定的
- [ ] `LevelFamilyMaster` が基準ItemId＋レベル→変種ItemId を解決（範囲外clamp）
- [ ] `MachineModuleEffect.QualityShift` が品質効果を集計（下限0）
- [ ] 完了出力が品質分布で上位レベル変種へ決定的に差し替わる（生産性と抽選順序固定、§7.2）
- [ ] 容量予約が変種IDで効き、消失/未開始が正しい（§7.1）
- [ ] 変種出力がセーブ round-trip する

## 残課題（B範囲外・要フォロー）

- **エディタ自動生成ツール（設計仕様§3の理想）:** `levelFamilies.yml` 定義から変種アイテム（items.json）とアップグレード合成レシピ（craftRecipes.json）を**自動生成**するエディタ拡張。本プランは変種を手動データとして扱う最小構成。自動生成は `DeterministicGuidUtil` を使えば実装可能だが、別タスク（Unity Editor拡張）。
- **品質式の高度化:** 隣接2レベル分布 → 全レベル連続分布への差し替え（集計を純粋関数に隔離してあるので式だけ交換可能）。バランス調整時に検討（§9）。
- **クライアントでのレベル可視化:** 変種アイテムのアイコン/ツールチップでレベルを示すUI（A2のアイテム表示に乗る範囲を超える装飾）。
