# アップグレードシステム フェーズA（基盤）実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `VanillaMachineProcessorComponent` を使う機械（**ElectricMachine と GearMachine の両方**）にモジュールスロット基盤を作り、挿したモジュールで「速度・生産性・省エネ」を変えられるようにする（品質軸=フェーズBは対象外）。Miner系（別プロセッサ）は対象外で別フォロー。

**スコープ注記（Codex監査反映）:** `VanillaGearMachineTemplate` も同じ `VanillaMachineProcessorComponent` を使うため、プロセッサ改修は両者に効く。`blocks.yml` の `moduleSlotCount` とテンプレート組み込みは ElectricMachine と GearMachine の両ケースに行う。「全機械共通」を謳う以上、片方だけだと完了条件が嘘になる。

**Architecture:** 新スキーマ `modules.yml` と `ModuleMaster` でモジュールを定義し、`blocks.yml` に `moduleSlotCount` を追加。機械ブロックに専用サブインベントリ（既存 `IOpenableBlockInventoryComponent` とは別の新インターフェース。`[DisallowMultiple]` 衝突回避）を足し、ブロックstateで永続化。処理開始時にスロット内容から効果を集計（clamp付き純粋関数）してスナップショットし、処理時間・消費電力・追加産出に適用する。**効果スナップショットはセーブにも永続化**し、処理中セーブ/ロードで進捗・効果が壊れないようにする。

**Tech Stack:** Unity / C# / NUnit / Mooresmaster SourceGenerator / uloop CLI

**設計仕様:** `docs/superpowers/specs/2026-06-05-upgrade-system-design.md`

**前提（調査済みの実パターン）:**
- 機械組み立て: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaMachineTemplate.cs`（`New()` と `Load()` 両方を編集）
- ブロックコンポーネント基底: `IBlockComponent`（`Game.Block.Interface/Component/IBlockComponent.cs`）
- 保存: `IBlockSaveState`（`SaveKey` + `GetSaveState()`）。復元はコンポーネントのコンストラクタで `Dictionary<string,string> componentStates` を受け取る。参考実装 `FuelGearGeneratorItemComponent.cs`
- アイテム保存形式: `ItemStackSaveJsonObject`（`itemGuid`+`count`のみ）/ 復元は拡張メソッド `ToItemStack()`
- マスタ: `Core.Master/MasterHolder.cs` / 既存 `ItemMaster.cs` がテンプレート / スキーマ追加手順は後述
- 処理: `VanillaMachineProcessorComponent.cs`（`Idle()`で開始時に`_processingRecipeTicks`固定・出力容量チェック、`Processing()`で完了時出力）
- テスト: `Tests/CombinedTest/Core/MachineIOTest.cs`。`MoorestechServerDIContainerGenerator` で初期化、`GameUpdater.UpdateOneTick()`/`RunFrames(1)`/`SecondsToTicks()` でtick進行、`ForUnitTestModBlockId` でブロックID取得
- コンパイル: `uloop compile --project-path ./moorestech_client`
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`

**コーディング規約（AGENTS.md 抜粋・必読）:**
- 主要処理に日本語→英語の2行セットコメント（各1行）
- 単純 getter/setter プロパティ禁止。値の設定は `public void SetHoge` メソッド
- try-catch 禁止。条件分岐/nullチェックで対応
- `#region Internal` はメソッド内ローカル関数まとめ専用（クラス直下privateには使わない）
- デフォルト引数禁止
- `.cs` 編集後は必ずコンパイル実行
- 新規サーバー .cs を client プロジェクトのテストで動かすには **Unity再起動が必要**（Refresh/Resolveでは不可。memory: server-tests-immutable-package）

---

## ファイル構成

**新規作成:**
- `VanillaSchema/modules.yml` — モジュール定義スキーマ
- `moorestech_server/Assets/Scripts/Core.Master/ModuleMaster.cs` — モジュールマスタ（`ItemMaster.cs` がテンプレート）
- `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/IModuleSlotInventoryComponent.cs` — モジュールスロットの公開インターフェース
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Module/MachineModuleSlotComponent.cs` — モジュールスロット実装（保存/復元/挿入制限）
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Module/MachineModuleEffect.cs` — 効果集計の純粋関数（clamp）
- テスト: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/ModuleMasterTest.cs`
- テスト: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/MachineModuleEffectTest.cs`
- テスト: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotTest.cs`

**修正:**
- `VanillaSchema/blocks.yml` — `ElectricMachine` ケースに `moduleSlotCount` 追加
- `moorestech_server/Assets/Scripts/Core.Master/csc.rsp` — `modules.yml` を追加
- `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — `dummyText` 更新（生成トリガー）
- `moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs` — `ModuleMaster` プロパティ＋Load追加
- `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaMachineTemplate.cs` — `New()`/`Load()` にモジュールスロット組み込み
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs` — 効果スナップショット＋適用
- テスト用JSON: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/modules.json`（新規）＋同 `blocks.json`（テスト機械に `moduleSlotCount` 付与）
- `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs` — モジュール対応テスト機械IDの定数（必要時）

---

# タスク群A1: モジュールマスタ基盤

ゴール: `modules.yml` を追加し、`MasterHolder.ModuleMaster` からモジュール定義をロードできる。`blocks.yml` の `moduleSlotCount` が `ElectricMachineBlockParam.ModuleSlotCount` / `GearMachineBlockParam.ModuleSlotCount` として読める。

> **重要（Codex監査反映・既存mod破壊の回避）:** `MasterHolder.GetJson` は `JsonContents[jsonFileName]` の直アクセスのため、`ModuleMaster` を Load に追加した瞬間 `modules.json` を持たない全mod（master repo, sandbox 等）が落ちる。対策を**両方**行う:
> 1. `MasterHolder` 側に「`modules` が無ければ空の `Modules` を使う」許容ロードを実装（`modules` は新規・任意のため）。実装は Task A1-3 Step4 に含む。
> 2. テストmodに最小 `modules.json` を Task A1-5 で配置し、**A1-5 を A1-3 より先に実施**する（テスト前提データを先に置く）。
>
> 実順序: **A1-1 → A1-2 → A1-4 → A1-5 → A1-3**（マスタ型生成 → スキーマ拡張 → テストデータ → ModuleMaster実装＋配線）。

### Task A1-1: modules.yml スキーマ作成

**Files:**
- Create: `VanillaSchema/modules.yml`

- [ ] **Step 1: スキーマファイルを作成**

`items.yml` / `blocks.yml` の記法に合わせる。`effectAxis` は4軸すべて定義（Phase Aで使うのは Speed/Productivity/Efficiency、Quality はBで使用）。

```yaml
id: modules
type: object
isDefaultOpen: true

properties:
- key: data
  type: array
  overrideCodeGeneratePropertyName: ModuleMasterElement
  items:
    type: object
    properties:
    - key: moduleGuid
      type: uuid
      autoGenerated: true
    - key: name
      type: string
    - key: itemGuid
      type: uuid
      foreignKey:
        schemaId: items
        foreignKeyIdPath: /data/[*]/itemGuid
        displayElementPath: /data/[*]/name
    - key: effectAxis
      type: enum
      options:
      - Speed
      - Productivity
      - Efficiency
      - Quality
    - key: tier
      type: integer
      default: 1
    - key: effectValue
      type: number
      default: 0
    - key: tradeoffValue
      type: number
      default: 0
```

- [ ] **Step 2: コミット**

```bash
cd ~/moorestech
git add VanillaSchema/modules.yml
git commit -m "feat(schema): add modules.yml schema for upgrade modules"
```

### Task A1-2: SourceGenerator に modules.yml を登録

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Core.Master/csc.rsp`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`

- [ ] **Step 1: csc.rsp に modules.yml を追加**

`csc.rsp` の既存の `/additionalfile:.../blocks.yml` などの行の並びに、同じ書式で1行追加する。既存行を Read して相対パス書式を正確に踏襲すること（例: `/additionalfile:Assets/../../VanillaSchema/modules.yml`）。

- [ ] **Step 2: _CompileRequester.cs の dummyText を更新**

`private const string dummyText = "...";` の値を別の文字列に変更（例: 末尾に `-modules` を足す）。これがSourceGenerator再生成のトリガー。

- [ ] **Step 3: Unityを再起動して生成をトリガー**

新規スキーマの型生成と、新規サーバー .cs の反映には Unity 再起動が必要。

Run: uloop-launch スキルでUnityを再起動（または既存Unityを再起動）。

- [ ] **Step 4: 生成型の存在をコンパイルで確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: コンパイル成功。`Mooresmaster.Model.ModulesModule` 名前空間と `ModuleMasterElement` / `Modules` 型、`Mooresmaster.Loader.ModulesModule.ModulesLoader` が生成されている（次タスクで参照する）。

> 注: ドメインリロード中に「Domain Reload in progress」エラーが出たら45秒待って再試行。

- [ ] **Step 5: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Core.Master/csc.rsp moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git commit -m "build(master): register modules.yml with source generator"
```

### Task A1-3: ModuleMaster クラス作成

**Files:**
- Create: `moorestech_server/Assets/Scripts/Core.Master/ModuleMaster.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/ModuleMasterTest.cs`

> このタスクは生成型に依存するため、Task A1-2 完了（生成成功）後に着手。`ItemMaster.cs` を Read してロード/Validate/Initialize/索引パターンを正確に踏襲すること。

- [ ] **Step 1: 失敗するテストを書く**

テスト用 `modules.json`（Task A1-5で作成）に1件のモジュール定義がある前提。まずモジュールGUIDから定義を引けることを検証する。

```csharp
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Other
{
    public class ModuleMasterTest
    {
        // モジュールマスタが modules.json をロードし、GUIDから定義を引けることを検証
        // Verify ModuleMaster loads modules.json and resolves a definition by GUID
        [Test]
        public void LoadAndGetModuleTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.NotNull(MasterHolder.ModuleMaster);
            Assert.Greater(MasterHolder.ModuleMaster.Modules.Data.Length, 0);

            var element = MasterHolder.ModuleMaster.Modules.Data[0];
            var found = MasterHolder.ModuleMaster.GetModuleElement(element.ModuleGuid);
            Assert.AreEqual(element.ModuleGuid, found.ModuleGuid);
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ModuleMasterTest"`
Expected: FAIL（`MasterHolder.ModuleMaster` 未定義でコンパイルエラー、または null）。

- [ ] **Step 3: ModuleMaster を実装**

`ItemMaster.cs` の構造に合わせる。`IMasterValidator`（`Validate(out string)` + `Initialize()`）を実装し、GUID→要素の索引辞書を持つ。

```csharp
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.ModulesModule;
using Mooresmaster.Model.ModulesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    // モジュール定義(modules.json)を保持し、GUIDで引けるようにするマスタ
    // Master that holds module definitions and resolves them by GUID
    public class ModuleMaster : IMasterValidator
    {
        public readonly Modules Modules;
        private Dictionary<System.Guid, ModuleMasterElement> _guidTable;

        public ModuleMaster(JToken jToken)
        {
            Modules = ModulesLoader.Load(jToken);
        }

        public ModuleMasterElement GetModuleElement(System.Guid moduleGuid)
        {
            return _guidTable[moduleGuid];
        }

        public ModuleMasterElement GetModuleElementByItemGuidOrNull(System.Guid itemGuid)
        {
            return Modules.Data.FirstOrDefault(x => x.ItemGuid == itemGuid);
        }

        public bool Validate(out string errorLog)
        {
            // itemGuid が ItemMaster に存在することを検証
            // Validate that each module's itemGuid exists in ItemMaster
            errorLog = "";
            foreach (var module in Modules.Data)
            {
                var id = MasterHolder.ItemMaster.GetItemIdOrNull(module.ItemGuid);
                if (id == null) errorLog += $"[ModuleMaster] Name:{module.Name} has invalid ItemGuid:{module.ItemGuid}\n";
            }
            return errorLog.Length == 0;
        }

        public void Initialize()
        {
            _guidTable = Modules.Data.ToDictionary(x => x.ModuleGuid, x => x);
        }
    }
}
```

> `IMasterValidator` の正確なメソッド名/シグネチャは `ItemMaster.cs` 実装で確認し、相違があれば合わせること。

- [ ] **Step 4: MasterHolder に ModuleMaster を追加**

`MasterHolder.cs` を編集。ItemMaster に依存するため、ItemMaster の Load 後に追加する。

```csharp
// 既存の static プロパティ群に追加
// Add to the existing static property group
public static ModuleMaster ModuleMaster { get; private set; }
```

`Load(MasterJsonFileContainer ...)` 内、`ItemMaster` 初期化の後に追加。**`modules.json` を持たないmodで落ちないよう、不在時は空JSONで生成する**（`GetJson` の直アクセスを避ける）:

```csharp
// モジュールマスタをロード（ItemMaster に依存。modules不在modでは空扱い）
// Load module master (depends on ItemMaster; treat absent modules as empty)
var modulesJson = TryGetJsonOrNull(masterJsonFileContainer, new JsonFileName("modules"))
                  ?? JToken.Parse("{\"data\":[]}");
ModuleMaster = new ModuleMaster(modulesJson);
InitializeMaster(ModuleMaster);
```

`GetJson` の隣に許容版ヘルパーを追加（既存 `GetJson` を Read して `JsonContents` のキー存在チェック付き版を作る）:

```csharp
// 指定ファイルが無ければ null を返す（新規・任意マスタ用）
// Return null when the file is absent (for new/optional masters)
private static JToken TryGetJsonOrNull(MasterJsonFileContainer container, JsonFileName jsonFileName)
{
    var index = 0;
    var contents = container.ConfigJsons[index].JsonContents;
    if (!contents.ContainsKey(jsonFileName)) return null;
    return (JToken)JsonConvert.DeserializeObject(contents[jsonFileName]);
}
```

> `ModuleMaster.Validate` は空データ（`Data.Length == 0`）で必ず成功すること（foreachが回らないので自然に成功）。

- [ ] **Step 5: テスト用 modules.json と blocks.json を用意（Task A1-5 を参照して先に最小データを置く）**

このStepはTask A1-5の Step1-2 を先取りして最小1件の `modules.json` とテスト用 `blocks.json` のmodへの登録を済ませる。詳細はA1-5。

- [ ] **Step 6: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 7: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ModuleMasterTest"`
Expected: PASS。

- [ ] **Step 8: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Core.Master/ModuleMaster.cs moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/ModuleMasterTest.cs
git commit -m "feat(master): add ModuleMaster and load modules.json"
```

### Task A1-4: blocks.yml に moduleSlotCount を追加

**Files:**
- Modify: `VanillaSchema/blocks.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`

- [ ] **Step 1: ElectricMachine と GearMachine の両ケースに moduleSlotCount を追加**

`blocks.yml` の `- when: ElectricMachine` と `- when: GearMachine` の各 `properties:` に追加（既存 `inputSlotCount` 等の隣）。`IMachineParam` インターフェース定義側に置けば両方で共有できるなら、そちらに1箇所追加でもよい（`defineInterface` の `IMachineParam` に `moduleSlotCount` を足す方式。既存スキーマ構造を Read して判断）:

```yaml
        - key: moduleSlotCount
          type: integer
          default: 0
```

> `IMachineParam` 共有プロパティとして定義できれば、生成型は両 BlockParam に `ModuleSlotCount` を持ち、A2のテンプレート組み込みも `machineParam.ModuleSlotCount` で統一できる。

- [ ] **Step 2: 生成トリガー更新**

`_CompileRequester.cs` の `dummyText` を再度変更。

- [ ] **Step 3: Unity再起動 → コンパイル**

Run: Unity再起動後 `uloop compile --project-path ./moorestech_client`
Expected: 成功。`ElectricMachineBlockParam.ModuleSlotCount`（int）が生成されている。

- [ ] **Step 4: コミット**

```bash
cd ~/moorestech
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git commit -m "feat(schema): add moduleSlotCount to ElectricMachine block param"
```

### Task A1-5: テスト用マスタデータ整備

**Files:**
- Create: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/modules.json`
- Modify: 同ディレクトリ `blocks.json`（モジュール対応テスト機械に `moduleSlotCount` を付与）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`（必要時）

> 既存テストmodの `master/` 配下の実ファイルパス・既存JSON構造を Read してから編集。`MasterJsonContents` に `modules` を含める仕組み（mod読込が master ディレクトリ内の全 .json を拾うか、ファイル名固定かは実装を確認）も合わせて確認すること。

- [ ] **Step 1: modules.json を作成（速度/生産性/省エネの3件）**

`itemGuid` はテストmodの既存アイテムGUID（`items.json`）から流用。`effectValue`/`tradeoffValue` は割合（例 0.2 = 20%）。

```json
{
  "data": [
    { "name": "TestSpeedModule",        "itemGuid": "<既存アイテムGUID-A>", "effectAxis": "Speed",        "tier": 1, "effectValue": 0.5, "tradeoffValue": 0.5 },
    { "name": "TestProductivityModule", "itemGuid": "<既存アイテムGUID-B>", "effectAxis": "Productivity", "tier": 1, "effectValue": 1.0, "tradeoffValue": 0.5 },
    { "name": "TestEfficiencyModule",   "itemGuid": "<既存アイテムGUID-C>", "effectAxis": "Efficiency",   "tier": 1, "effectValue": 0.3, "tradeoffValue": 0.0 }
  ]
}
```

`moduleGuid` は `autoGenerated: true` のため、既存の他マスタJSONで autoGenerated GUID をどう扱っているか（明示記載か省略か）を確認して合わせる。

- [ ] **Step 2: テスト機械の blocks.json に moduleSlotCount を付与**

既存のテスト用 ElectricMachine 定義（`ForUnitTestModBlockId.MachineId` に対応する GUID `00000000-0000-0000-0000-000000000001`）の `blockParam` に `"moduleSlotCount": 4` を追加。

- [ ] **Step 3: コンパイル＋既存テストが壊れないことを確認**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineIOTest"`
Expected: 既存機械テストが引き続きPASS（moduleSlotCount追加で回帰がない）。

- [ ] **Step 4: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/
git commit -m "test(master): add test module data and moduleSlotCount to test machine"
```

---

# タスク群A2: モジュールスロットインベントリコンポーネント

ゴール: 機械にモジュール専用スロットが付き、モジュールアイテムのみ挿入可・通常搬入不可・ブロックstateで永続化される。

> ## ⚠ 設計改訂（2026-06-06）— 独立コンポーネントを作らず、既存機械インベントリの第3レンジにする（ユーザーレビュー反映）
>
> **当初は `IModuleSlotInventoryComponent` ＋ 専用 `MachineModuleSlotComponent`（別コンポーネント）として実装する設計（Task A2-1・A2-2）だったが、不採用。** 既存 `VanillaMachineBlockInventoryComponent` は既に入力スロット＋出力スロットを**統一スロット番号**で束ねている（`GetItem`/`SetItem`/`GetSlotSize` がスロット番号で振り分け、`InventoryItems` が両者連結）。モジュールスロットはその後ろに続く**第3レンジ**として足すのが正しい。これで `[DisallowMultiple]` 衝突の懸念自体が消える（新コンポーネントを足さないため）。
>
> **改訂後の実装（Task A2-1・A2-2 を置き換え）:**
> - 新規: `Game.Block/Blocks/Machine/Inventory/VanillaMachineModuleInventory.cs` — 入力/出力インベントリ（`VanillaMachineInputInventory`/`VanillaMachineOutputInventory`）と同じ作りで N 個のモジュールスロットを保持。
> - 修正: `VanillaMachineBlockInventoryComponent.cs` — スロット番号ルーティングに第3レンジを追加。`GetItem`/`SetItem`/`GetSlotSize`/`InventoryItems`/`CreateCopiedItems` を `input + output + module` に拡張。
> - 修正: `VanillaMachineSaveComponent.cs` — 既存の入力/出力スロット永続化にモジュールスロットも含める。
> - 効果集計（タスク群A3）は、別コンポーネントの `GetEquippedModules()` ではなく**この機械インベントリのモジュールスロット範囲を読む**（アクセサ名は実装で確定）。
> - 通常搬入禁止は自動的に満たされる: 搬送系の `InsertItem` は入力サブインベントリにしか入らないため、モジュールスロットがベルト等で埋まることはない。
> - 「モジュールのみ・Count==1・上書き拒否」のプレイヤー装着制限は、**フェーズA2 の per-slot 挿入ガード＋移動プロトコルの事前確認**で守る（A2 計画の設計改訂参照）。Phase A 段階ではスロット枠の追加・保存・読み取りまでで、移動制限は A2 に委ねる。
>
> **以降の Task A2-1・A2-2・A2-3 は歴史的記録（不採用の独立コンポーネント案）。実装は本改訂ブロックに従う。**

### Task A2-1: IModuleSlotInventoryComponent インターフェース

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/IModuleSlotInventoryComponent.cs`

> `IBlockInventory`/`IOpenableBlockInventoryComponent` は `[DisallowMultiple]` 制約により機械に1つしか持てない（既存の入出力インベントリが使用済み）。モジュールスロットは衝突しない**独立インターフェース**として定義する（設計仕様 §6）。

- [ ] **Step 1: インターフェースを作成**

```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Mooresmaster.Model.ModulesModule;

namespace Game.Block.Interface.Component
{
    // 機械のモジュール専用スロット。通常の入出力インベントリとは別枠で識別する
    // Module-only slots on a machine, identified separately from normal IO inventory
    public interface IModuleSlotInventoryComponent : IBlockComponent
    {
        int SlotCount { get; }
        IItemStack GetModule(int slot);

        // モジュールアイテムのみ受け付ける。非モジュール/範囲外は false で拒否
        // Accept module items only; reject non-modules / out-of-range with false
        bool TryInsertModule(int slot, IItemStack moduleItem);
        IItemStack RemoveModule(int slot);

        // 現在装着中のモジュール定義一覧（効果集計の入力）
        // Currently equipped module definitions (input for effect aggregation)
        IReadOnlyList<ModuleMasterElement> GetEquippedModules();
    }
}
```

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 3: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Game.Block.Interface/Component/IModuleSlotInventoryComponent.cs
git commit -m "feat(block): add IModuleSlotInventoryComponent interface"
```

### Task A2-2: MachineModuleSlotComponent 実装（保存/復元/挿入制限）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Module/MachineModuleSlotComponent.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotTest.cs`

- [ ] **Step 1: 失敗するテストを書く（挿入制限）**

```csharp
using System;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class MachineModuleSlotTest
    {
        // モジュールアイテムは挿入でき、非モジュールアイテムは拒否されることを検証
        // Verify module items are inserted and non-module items are rejected
        [Test]
        public void InsertAndRejectTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            ServerContext.WorldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.MachineId, Vector3Int.one, BlockDirection.North,
                Array.Empty<BlockCreateParam>(), out var block);

            var moduleSlot = block.GetComponent<IModuleSlotInventoryComponent>();
            Assert.NotNull(moduleSlot);
            Assert.AreEqual(4, moduleSlot.SlotCount);

            // モジュールアイテム(=modules.jsonの先頭定義のitemGuid)を挿入できる
            // A module item can be inserted
            var moduleElement = MasterHolder.ModuleMaster.Modules.Data[0];
            var moduleItemId = MasterHolder.ItemMaster.GetItemId(moduleElement.ItemGuid);
            var inserted = moduleSlot.TryInsertModule(0, itemStackFactory.Create(moduleItemId, 1));
            Assert.IsTrue(inserted);
            Assert.AreEqual(1, moduleSlot.GetEquippedModules().Count);

            // 非モジュールアイテムは拒否される
            // A non-module item is rejected
            var rejected = moduleSlot.TryInsertModule(1, itemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));
            Assert.IsFalse(rejected);
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineModuleSlotTest"`
Expected: FAIL（`IModuleSlotInventoryComponent` を機械が持たない → `GetComponent` が見つからず例外、またはコンパイルエラー）。

- [ ] **Step 3: MachineModuleSlotComponent を実装**

保存/復元は `FuelGearGeneratorItemComponent` パターンに準拠。モジュール判定は `ModuleMaster.GetModuleElementByItemGuidOrNull`。

```csharp
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.ModulesModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Machine.Module
{
    // 機械のモジュールスロット。モジュールのみ受け付け、ブロックstateで永続化する
    // Machine module slots: accept modules only, persisted via block state
    public class MachineModuleSlotComponent : IModuleSlotInventoryComponent, IBlockSaveState
    {
        public string SaveKey => "machineModuleSlot";
        public int SlotCount { get; }

        private readonly IItemStack[] _slots;

        public MachineModuleSlotComponent(int slotCount)
        {
            SlotCount = slotCount;
            _slots = new IItemStack[slotCount];
            // 空アイテムで初期化
            // Initialize with empty item stacks
            for (var i = 0; i < slotCount; i++) _slots[i] = ServerContext.ItemStackFactory.CreatEmpty();
        }

        // セーブデータからの復元コンストラクタ
        // Restore-from-save constructor
        public MachineModuleSlotComponent(Dictionary<string, string> componentStates, int slotCount) : this(slotCount)
        {
            if (componentStates == null) return;
            if (!componentStates.TryGetValue(SaveKey, out var raw)) return;
            var saved = JsonConvert.DeserializeObject<List<ItemStackSaveJsonObject>>(raw);
            if (saved == null) return;
            for (var i = 0; i < System.Math.Min(slotCount, saved.Count); i++)
            {
                var stack = saved[i]?.ToItemStack();
                if (stack != null) _slots[i] = stack;
            }
        }

        public IItemStack GetModule(int slot)
        {
            BlockException.CheckDestroy(this);
            return _slots[slot];
        }

        public bool TryInsertModule(int slot, IItemStack moduleItem)
        {
            BlockException.CheckDestroy(this);
            // 範囲外は拒否
            // Reject out-of-range
            if (slot < 0 || slot >= SlotCount) return false;
            // 1スロット1枚専用。Count!=1 は拒否
            // One module per slot; reject Count != 1
            if (moduleItem == null || moduleItem.Count != 1) return false;
            // 既に装着済みのスロットは拒否（無条件上書きで紛失させない）
            // Reject if the slot is already occupied (do not silently overwrite/lose it)
            if (_slots[slot].Id != ItemMaster.EmptyItemId) return false;
            // モジュール定義の無いアイテムは拒否
            // Reject items that have no module definition
            var element = ResolveModuleOrNull(moduleItem);
            if (element == null) return false;
            _slots[slot] = moduleItem;
            return true;
        }

        public IItemStack RemoveModule(int slot)
        {
            BlockException.CheckDestroy(this);
            var removed = _slots[slot];
            _slots[slot] = ServerContext.ItemStackFactory.CreatEmpty();
            return removed;
        }

        public IReadOnlyList<ModuleMasterElement> GetEquippedModules()
        {
            BlockException.CheckDestroy(this);
            // 各スロットのアイテムをモジュール定義に解決し、装着中のものだけ返す
            // Resolve each slot item to a module definition and return equipped ones
            return _slots.Select(ResolveModuleOrNull).Where(x => x != null).ToList();
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var serialized = _slots.Select(s => new ItemStackSaveJsonObject(s)).ToList();
            return JsonConvert.SerializeObject(serialized);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }

        private ModuleMasterElement ResolveModuleOrNull(IItemStack itemStack)
        {
            if (itemStack == null || itemStack.Id == ItemMaster.EmptyItemId) return null;
            var itemGuid = MasterHolder.ItemMaster.GetItemMaster(itemStack.Id).ItemGuid;
            return MasterHolder.ModuleMaster.GetModuleElementByItemGuidOrNull(itemGuid);
        }
    }
}
```

> `ItemStackFactory.CreatEmpty()` の綴り（実コード上 `CreatEmpty`）に注意。`ItemStack.cs`/テストで確認済み。

- [ ] **Step 3.5: 装着済みスロットへの拒否テストを追加**

A2-2 Step1 のテストに、同一スロットへ2枚目を挿すと false、`Count=2` のモジュールも false になるケースを追加する。

```csharp
            // 装着済みスロットへの再挿入は拒否
            // Re-inserting into an occupied slot is rejected
            var second = moduleSlot.TryInsertModule(0, itemStackFactory.Create(moduleItemId, 1));
            Assert.IsFalse(second);
            // Count!=1 は拒否
            // Count != 1 is rejected
            var stacked = moduleSlot.TryInsertModule(2, itemStackFactory.Create(moduleItemId, 2));
            Assert.IsFalse(stacked);
```

- [ ] **Step 4: VanillaMachineTemplate と VanillaGearMachineTemplate に組み込み（New と Load 両方）**

`VanillaMachineTemplate.cs` と `VanillaGearMachineTemplate.cs` の両方を Read し、`New()` で `machineParam.ModuleSlotCount` を使って生成、components に追加。`Load()` では `componentStates` を渡す復元コンストラクタを使う。両テンプレートとも同一の `MachineModuleSlotComponent` を使う。

`New()` 内、components 追加箇所付近:

```csharp
// モジュールスロットを生成（slot数はマスタ定義、0なら実質無効）
// Create module slots (count from master; 0 means effectively disabled)
var moduleSlot = new MachineModuleSlotComponent(machineParam.ModuleSlotCount);
components.Add(moduleSlot);
```

`Load()` 内:

```csharp
var moduleSlot = new MachineModuleSlotComponent(componentStates, machineParam.ModuleSlotCount);
components.Add(moduleSlot);
```

必要な `using Game.Block.Blocks.Machine.Module;` を追加。`machineParam` への参照名は既存コードに合わせる（`ElectricMachineBlockParam` へのキャスト変数）。

- [ ] **Step 5: Unity再起動 → コンパイル**

Run: Unity再起動後 `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 6: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineModuleSlotTest"`
Expected: PASS。

- [ ] **Step 7: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Module/MachineModuleSlotComponent.cs moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaMachineTemplate.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotTest.cs
git commit -m "feat(block): add machine module slot component with insert restriction"
```

### Task A2-3: モジュールスロットのセーブ/ロード round-trip テスト

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotTest.cs`

- [ ] **Step 1: 失敗するテストを追加**

ブロックの `GetSaveState()` → 復元で装着モジュールが保たれることを検証。既存のブロックセーブ/ロードのテストパターン（`WorldBlockDatastore` の save/load）を踏襲。round-trip ヘルパーは既存の同種テストを参照。

```csharp
        // モジュール装着状態がセーブ/ロードを跨いで保持されることを検証
        // Verify equipped modules survive a save/load round-trip
        [Test]
        public void ModuleSaveLoadRoundTripTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            ServerContext.WorldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.MachineId, Vector3Int.one, BlockDirection.North,
                Array.Empty<BlockCreateParam>(), out var block);

            var moduleElement = MasterHolder.ModuleMaster.Modules.Data[0];
            var moduleItemId = MasterHolder.ItemMaster.GetItemId(moduleElement.ItemGuid);
            var moduleSlot = block.GetComponent<IModuleSlotInventoryComponent>();
            moduleSlot.TryInsertModule(0, itemStackFactory.Create(moduleItemId, 1));

            // state を取り出して復元（既存の WorldBlockDatastore save/load パターンに合わせる）
            // Extract state and reload (follow existing WorldBlockDatastore save/load pattern)
            var state = block.GetSaveState();
            var reloaded = ServerContext.BlockFactory.Load(block.BlockGuid, block.BlockInstanceId, state, block.BlockPositionInfo);

            var reloadedSlot = reloaded.GetComponent<IModuleSlotInventoryComponent>();
            Assert.AreEqual(1, reloadedSlot.GetEquippedModules().Count);
            Assert.AreEqual(moduleElement.ModuleGuid, reloadedSlot.GetEquippedModules()[0].ModuleGuid);
        }
```

> `block.GetSaveState()` の戻り型は `Dictionary<string,string>`、`BlockFactory.Load` 引数順は調査済み。`BlockPositionInfo`/`BlockGuid`/`BlockInstanceId` のアクセサ名は `IBlock` 実装で確認して合わせること。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ModuleSaveLoadRoundTripTest"`
Expected: 実装が正しければ実は通る可能性がある（A2-2でsave/load実装済みのため）。FAILする場合は `Load()` 側の復元コンストラクタ接続漏れを修正。

- [ ] **Step 3: 必要なら修正**

`VanillaMachineTemplate.Load()` で復元コンストラクタ（`componentStates` 受け取り版）を使っているか確認・修正。

- [ ] **Step 4: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineModuleSlotTest"`
Expected: 2テストともPASS。

- [ ] **Step 5: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotTest.cs
git commit -m "test(block): verify module slot save/load round-trip"
```

---

# タスク群A3: 効果集計 ＋ プロセッサ統合

ゴール: 処理開始時にスロット内容から効果をスナップショットし、処理時間・消費電力・追加産出に clamp 付きで適用する。

### Task A3-1: MachineModuleEffect 効果集計（純粋関数＋clamp）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Module/MachineModuleEffect.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/MachineModuleEffectTest.cs`

- [ ] **Step 1: 失敗するテストを書く（加算とclamp）**

効果の定義（フェーズA）:
- Speed: 処理時間に `1/(1+Σ effectValue)` 係数。トレードオフで消費電力に `(1+Σ tradeoffValue)`。
- Productivity: 追加産出確率 `Σ effectValue`（[0,1]にclamp）。トレードオフで処理時間に `(1+Σ tradeoffValue)`。
- Efficiency: 消費電力に `1/(1+Σ effectValue)` 係数。
- 不変条件: `ProcessingTimeMultiplier` 適用後の ticks は呼び出し側で `>=1` にclamp。`PowerMultiplier >= 0.1`（下限）。`ExtraOutputChance ∈ [0,1]`。

```csharp
using System.Collections.Generic;
using Mooresmaster.Model.ModulesModule;
using NUnit.Framework;
using Game.Block.Blocks.Machine.Module;

namespace Tests.UnitTest.Core.Other
{
    public class MachineModuleEffectTest
    {
        // 速度モジュール2枚で処理時間係数が加算的に下がることを検証
        // Verify two speed modules additively reduce the processing-time multiplier
        [Test]
        public void SpeedAdditiveTest()
        {
            var speed = NewModule("Speed", 0.5f, 0.5f);
            var effect = MachineModuleEffect.Aggregate(new List<ModuleMasterElement> { speed, speed });

            // 1/(1+0.5+0.5) = 0.5
            Assert.AreEqual(0.5f, effect.ProcessingTimeMultiplier, 0.001f);
            // 消費は (1+0.5+0.5)=2.0 倍
            Assert.AreEqual(2.0f, effect.PowerMultiplier, 0.001f);
        }

        // 生産性の追加産出確率は [0,1] にclampされることを検証
        // Verify productivity extra-output chance is clamped to [0,1]
        [Test]
        public void ProductivityClampTest()
        {
            var prod = NewModule("Productivity", 1.0f, 0.0f);
            var effect = MachineModuleEffect.Aggregate(new List<ModuleMasterElement> { prod, prod });
            Assert.AreEqual(1.0f, effect.ExtraOutputChance, 0.001f); // 2.0 を 1.0 にclamp
        }

        // 効果無し(空)では全係数が中立であることを検証
        // Verify neutral multipliers when no modules are equipped
        [Test]
        public void EmptyNeutralTest()
        {
            var effect = MachineModuleEffect.Aggregate(new List<ModuleMasterElement>());
            Assert.AreEqual(1.0f, effect.ProcessingTimeMultiplier, 0.001f);
            Assert.AreEqual(1.0f, effect.PowerMultiplier, 0.001f);
            Assert.AreEqual(0.0f, effect.ExtraOutputChance, 0.001f);
        }

        #region Internal

        // テスト用にモジュール定義を生成する
        // Build a module definition for tests
        ModuleMasterElement NewModule(string axis, float effectValue, float tradeoffValue)
        {
            // ModuleMasterElement のコンストラクタ引数順は生成コードに合わせる
            // Match the generated ModuleMasterElement constructor signature
            return new ModuleMasterElement(
                System.Guid.NewGuid(), $"Test{axis}", System.Guid.NewGuid(),
                axis, 1, effectValue, tradeoffValue);
        }

        #endregion
    }
}
```

> `ModuleMasterElement` のコンストラクタ引数順は生成コードを確認して `NewModule` を合わせること（modules.yml のプロパティ順から推定: moduleGuid, name, itemGuid, effectAxis, tier, effectValue, tradeoffValue）。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineModuleEffectTest"`
Expected: FAIL（`MachineModuleEffect` 未定義）。

- [ ] **Step 3: MachineModuleEffect を実装**

```csharp
using System.Collections.Generic;
using Mooresmaster.Model.ModulesModule;

namespace Game.Block.Blocks.Machine.Module
{
    // 装着モジュール群から機械への効果係数を集計する純粋計算
    // Pure aggregation of equipped modules into effect multipliers for a machine
    public class MachineModuleEffect
    {
        // 消費電力倍率の下限（0以下を防ぐ不変条件）
        // Lower bound for power multiplier (invariant preventing <= 0)
        private const float MinPowerMultiplier = 0.1f;

        public readonly float ProcessingTimeMultiplier;
        public readonly float PowerMultiplier;
        public readonly float ExtraOutputChance;

        private MachineModuleEffect(float processingTimeMultiplier, float powerMultiplier, float extraOutputChance)
        {
            ProcessingTimeMultiplier = processingTimeMultiplier;
            PowerMultiplier = powerMultiplier;
            ExtraOutputChance = extraOutputChance;
        }

        public static MachineModuleEffect Aggregate(IReadOnlyList<ModuleMasterElement> modules)
        {
            // 軸ごとに effectValue / tradeoffValue を合算
            // Sum effectValue / tradeoffValue per axis
            var speedSum = 0f; var speedTradeoff = 0f;
            var prodSum = 0f; var prodTradeoff = 0f;
            var efficiencySum = 0f;

            foreach (var m in modules)
            {
                switch (m.EffectAxis)
                {
                    case "Speed": speedSum += m.EffectValue; speedTradeoff += m.TradeoffValue; break;
                    case "Productivity": prodSum += m.EffectValue; prodTradeoff += m.TradeoffValue; break;
                    case "Efficiency": efficiencySum += m.EffectValue; break;
                    // Quality はフェーズBで扱う（ここでは無視）
                    // Quality is handled in phase B (ignored here)
                }
            }

            // 処理時間: 速度で短縮、生産性ペナルティで延長
            // Processing time: shortened by speed, extended by productivity penalty
            var timeMul = (1f + prodTradeoff) / (1f + speedSum);

            // 消費電力: 速度ペナルティで増、省エネで減。下限clamp
            // Power: increased by speed penalty, reduced by efficiency. Clamp to lower bound
            var powerMul = (1f + speedTradeoff) / (1f + efficiencySum);
            if (powerMul < MinPowerMultiplier) powerMul = MinPowerMultiplier;

            // 追加産出確率: [0,1] にclamp
            // Extra-output chance: clamp to [0,1]
            var extra = prodSum;
            if (extra < 0f) extra = 0f;
            else if (extra > 1f) extra = 1f;

            return new MachineModuleEffect(timeMul, powerMul, extra);
        }
    }
}
```

> 生成コードでの enum プロパティの型を確認すること。`effectAxis` が enum 型として生成され string でない場合、`switch` を生成enum型に合わせる（`case ModuleEffectAxis.Speed:` 等）。テストの `NewModule` も同様に合わせる。

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 5: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineModuleEffectTest"`
Expected: 3テストPASS。

- [ ] **Step 6: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Module/MachineModuleEffect.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Other/MachineModuleEffectTest.cs
git commit -m "feat(block): add MachineModuleEffect aggregation with clamps"
```

### Task A3-2: プロセッサに効果スナップショット＋処理時間適用＋セーブ永続化

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineSaveComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/BlockTemplateUtil.cs`
- Modify: `VanillaMachineTemplate.cs` / `VanillaGearMachineTemplate.cs`（プロセッサ生成にモジュールスロット注入）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotTest.cs`

> **Codex監査反映の必須事項:** 既存ロードは `RemainingSeconds`＋`RecipeGuid` だけ復元し、`_processingRecipeTicks` をベース時間から**再計算**する（`BlockTemplateUtil.MachineLoadState` → プロセッサ復元コンストラクタ）。速度短縮済みの処理を保存すると進捗率が狂い、`_currentEffect` が null だと生産性追加産出も消える。よって**効果スナップショットをセーブに永続化**し、ロード時は再計算せず保存値を使う。

- [ ] **Step 1: 失敗するテストを書く（速度モジュールで処理が速くなる）**

同一レシピの機械を2台用意し、片方に速度モジュールを挿す。同一tick数進めたとき、モジュール側が先に完成することを検証。

```csharp
        // 速度モジュールを挿した機械が、挿さない機械より早く処理完了することを検証
        // Verify a machine with a speed module finishes earlier than one without
        [Test]
        public void SpeedModuleShortensProcessingTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);

            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var plain);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, new Vector3Int(5, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var boosted);

            var speedModule = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == "Speed");
            var speedItemId = MasterHolder.ItemMaster.GetItemId(speedModule.ItemGuid);
            boosted.GetComponent<IModuleSlotInventoryComponent>().TryInsertModule(0, itemStackFactory.Create(speedItemId, 1));

            foreach (var inputItem in recipe.InputItems)
            {
                plain.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
                boosted.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }

            var plainProc = plain.GetComponent<VanillaMachineProcessorComponent>();
            var boostedProc = boosted.GetComponent<VanillaMachineProcessorComponent>();

            // レシピ時間の3/4強だけ進める（速度効果で boosted は先に完了する想定）
            // Advance for ~3/4 of the recipe time (boosted finishes first thanks to speed effect)
            var partialTicks = GameUpdater.SecondsToTicks(recipe.Time) * 3 / 4 + 2;
            for (uint i = 0; i < partialTicks; i++)
            {
                plainProc.SupplyPower(100000); boostedProc.SupplyPower(100000);
                GameUpdater.RunFrames(1);
            }

            Assert.AreEqual(ProcessState.Idle, boostedProc.CurrentState);
            Assert.AreEqual(ProcessState.Processing, plainProc.CurrentState);
        }
```

> 閾値tick数はテスト機械の `recipe.Time` と `effectValue`(A1-5で0.5) に依存。想定が崩れる場合は `effectValue` を上げる。`First(m => m.EffectAxis == "Speed")` は enum生成なら `== ModuleEffectAxis.Speed` に合わせる。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SpeedModuleShortensProcessingTest"`
Expected: FAIL（効果未適用で両機械が同時進行）。

- [ ] **Step 3: プロセッサに効果フィールド＋スナップショットを実装**

(a) フィールド追加:

```csharp
private readonly IModuleSlotInventoryComponent _moduleSlot;
private readonly BlockInstanceId _blockInstanceId; // 決定的乱数seed用（A3-4で使用）
private MachineModuleEffect _currentEffect;          // 処理開始時にスナップショット
private int _processedCycleCount;                    // 完了回数（決定的乱数・セーブ対象。A3-4）
```

(b) 両コンストラクタに `IModuleSlotInventoryComponent moduleSlot, BlockInstanceId blockInstanceId` を**末尾に追加**（デフォルト引数禁止のため呼び出し側を必ず更新）。代入を追加。復元コンストラクタには後述の保存値（スケール済みticks・効果・cycleCount）も引数に追加する（Step5）。

(c) `Idle()` の開始時に効果をスナップショットし、処理時間に適用（最低1tickにclamp）:

```csharp
            if (isStartProcess)
            {
                CurrentState = ProcessState.Processing;
                _processingRecipe = recipe;

                // 処理開始時にモジュール効果をスナップショット（処理中は固定）
                // Snapshot module effect at process start (fixed during processing)
                _currentEffect = MachineModuleEffect.Aggregate(_moduleSlot.GetEquippedModules());

                // 処理時間に時間倍率を適用し、最低1tickにclamp
                // Apply time multiplier to processing ticks, clamp to at least 1 tick
                var baseTicks = GameUpdater.SecondsToTicks(_processingRecipe.Time);
                _processingRecipeTicks = (uint)System.Math.Max(1, (long)System.Math.Round(baseTicks * _currentEffect.ProcessingTimeMultiplier));
                _vanillaMachineInputInventory.ReduceInputSlot(_processingRecipe);
                RemainingTicks = _processingRecipeTicks;
            }
```

必要 using: `using Game.Block.Interface.Component;` `using Game.Block.Blocks.Machine.Module;`

- [ ] **Step 4: セーブに効果スナップショットを永続化**

`VanillaMachineSaveComponent.GetSaveState()` の `VanillaMachineJsonObject` 構築に、効果スナップショット復元に必要なフィールドを追加。`VanillaMachineJsonObject` クラスにも対応プロパティを追加（`JsonProperty` 名は新規でよい）:

```csharp
            // 効果スナップショットを永続化（処理中セーブ/ロードで進捗・効果を保つ）
            // Persist effect snapshot so progress/effect survive mid-process save/load
            ProcessingTotalTicks = _vanillaMachineProcessorComponent.ProcessingRecipeTicks,
            EffectPowerMultiplier = _vanillaMachineProcessorComponent.CurrentPowerMultiplier,
            EffectExtraOutputChance = _vanillaMachineProcessorComponent.CurrentExtraOutputChance,
            ProcessedCycleCount = _vanillaMachineProcessorComponent.ProcessedCycleCount,
```

プロセッサ側に公開アクセサを追加（単純getter禁止規約のため、保存に必要な値は明示的な読み取り専用プロパティで露出。値変更は内部のみ）:

```csharp
public uint ProcessingRecipeTicks => _processingRecipeTicks;
public float CurrentPowerMultiplier => _currentEffect?.PowerMultiplier ?? 1f;
public float CurrentExtraOutputChance => _currentEffect?.ExtraOutputChance ?? 0f;
public int ProcessedCycleCount => _processedCycleCount;
```

> 「単純getter/setter禁止」は値の**設定**を `SetHoge` にする規約。読み取り専用の計算/露出プロパティは許容範囲（既存 `RecipeGuid` 等が同様）。

- [ ] **Step 5: ロードでスナップショットを復元（再計算しない）**

`BlockTemplateUtil.MachineLoadState` を編集し、`VanillaMachineJsonObject` の保存値からプロセッサを復元する。復元コンストラクタを以下のシグネチャに変更:

```csharp
public VanillaMachineProcessorComponent(
    VanillaMachineInputInventory input, VanillaMachineOutputInventory output,
    ProcessState currentState, uint remainingTicks, MachineRecipeMasterElement processingRecipe,
    float requestPower,
    IModuleSlotInventoryComponent moduleSlot, BlockInstanceId blockInstanceId,
    uint processingTotalTicks, float powerMultiplier, float extraOutputChance, int processedCycleCount)
{
    // ... 既存代入 ...
    // ベース時間からの再計算をやめ、保存済みスケール済みticksを使う
    // Do NOT recompute from base time; use the saved scaled total ticks
    _processingRecipeTicks = processingTotalTicks > 0
        ? processingTotalTicks
        : (processingRecipe != null ? GameUpdater.SecondsToTicks(processingRecipe.Time) : 0);
    _currentEffect = MachineModuleEffect.FromSaved(powerMultiplier, extraOutputChance);
    _processedCycleCount = processedCycleCount;
    // ... RequestPower, RemainingTicks, CurrentState 代入 ...
}
```

`MachineModuleEffect` に保存値からの復元ファクトリを追加:

```csharp
// セーブ値から効果を復元（処理時間倍率は残tickに反映済みのため中立1fでよい）
// Rebuild effect from saved values (time multiplier already baked into remaining ticks, so neutral 1f)
public static MachineModuleEffect FromSaved(float powerMultiplier, float extraOutputChance)
{
    return new MachineModuleEffect(1f, powerMultiplier, extraOutputChance);
}
```

`MachineLoadState` 内で上記新コンストラクタへ保存値（`jsonObject.ProcessingTotalTicks` 等）と `moduleSlot`・`blockInstanceId` を渡す。`moduleSlot` は同じ Load 経路で生成済みのものを使う（生成順を調整）。

- [ ] **Step 6: Unity再起動 → コンパイル**

Run: Unity再起動後 `uloop compile --project-path ./moorestech_client`
Expected: 成功（`New()` 側プロセッサ生成のコンストラクタ呼び出しも引数追加に合わせて修正。`New()` では `processingTotalTicks=0, powerMultiplier=1, extraOutputChance=0, processedCycleCount=0` 相当の初期値、または `machineRecipe` を取る第1コンストラクタ側に `moduleSlot`/`blockInstanceId` を足す）。

- [ ] **Step 7: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SpeedModuleShortensProcessingTest"`
Expected: PASS。

- [ ] **Step 8: 既存機械テストの回帰確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineIOTest|GearMachineIoTest"`
Expected: PASS（モジュール0枚=中立効果で従来挙動維持）。

- [ ] **Step 9: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Game.Block/ moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotTest.cs
git commit -m "feat(block): snapshot module effect, apply to time, persist across save/load"
```

### Task A3-3: 省エネ/速度の消費電力倍率を RequestEnergy に適用

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaElectricMachineComponent.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotTest.cs`

> **Codex監査反映:** `VanillaElectricMachineComponent.RequestEnergy` は常に `RequestPower` を返す。`GetSubTicks` の requiredPower だけ変えると、電力網への要求量と実効要求量がズレる。よって**実効要求電力をプロセッサに持たせ、RequestEnergy と GetSubTicks の両方が同じ値を使う**。
>
> スコープ: 電力倍率は electric 経路に適用する。GearMachine の消費（RPM/トルク）への省エネ適用は別フォロー（処理時間効果は A3-2 で両機械に効く）。

- [ ] **Step 1: 失敗するテストを書く（省エネで要求電力が下がる）**

```csharp
        // 省エネモジュールで処理中の要求電力が下がることを検証
        // Verify an efficiency module lowers requested power during processing
        [Test]
        public void EfficiencyModuleLowersRequestPowerTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var effModule = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == "Efficiency");
            block.GetComponent<IModuleSlotInventoryComponent>().TryInsertModule(0, itemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(effModule.ItemGuid), 1));

            foreach (var inputItem in recipe.InputItems)
                block.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));

            var electric = block.GetComponent<VanillaElectricMachineComponent>();
            var proc = block.GetComponent<VanillaMachineProcessorComponent>();
            var baseRequest = proc.RequestPower;

            proc.SupplyPower(100000);
            GameUpdater.RunFrames(1); // 処理開始 → スナップショット

            // 処理中の実効要求電力がベースより低い（effectValue 0.3 → 1/1.3 倍）
            // Effective requested power during processing is lower than base
            Assert.Less(electric.RequestEnergy.AsPrimitive(), baseRequest);
        }
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EfficiencyModuleLowersRequestPowerTest"`
Expected: FAIL（RequestEnergy が常に RequestPower）。

- [ ] **Step 3: 実効要求電力を実装**

プロセッサに実効要求電力プロパティを追加:

```csharp
// 処理中はモジュールの電力倍率を反映した実効要求電力。アイドル中はベース
// Effective requested power: applies module power multiplier while processing, base while idle
public float EffectiveRequestPower => CurrentState == ProcessState.Processing
    ? RequestPower * (_currentEffect?.PowerMultiplier ?? 1f)
    : RequestPower;
```

`VanillaElectricMachineComponent.RequestEnergy` を変更:

```csharp
public ElectricPower RequestEnergy => new ElectricPower(_vanillaMachineProcessorComponent.EffectiveRequestPower);
```

`Processing()` の `GetSubTicks` の requiredPower も実効値に合わせ、要求と進行を一致させる:

```csharp
            var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_currentPower, EffectiveRequestPower);
```

- [ ] **Step 4: Unity再起動 → コンパイル → テスト**

Run: Unity再起動後 `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EfficiencyModuleLowersRequestPowerTest|MachineIOTest"`
Expected: PASS（回帰なし）。

- [ ] **Step 5: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/
git commit -m "feat(block): apply module power multiplier to effective request power"
```

### Task A3-4: 生産性の追加産出（正しい追加出力API＋仮想容量予約＋決定的乱数）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineOutputInventory.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotTest.cs`

> **Codex監査反映（3点）:**
> 1. `InsertOutputSlot` の2回呼びは液体まで倍増し破綻 → 追加産出は**アイテム出力のみ1セット**を入れる専用API。
> 2. 既存 `IsAllowedToOutputItem` は各出力を独立OR判定で、複数出力のスロット食い合いを無視 → **仮想インベントリへ順次挿入してシミュレート**する判定を新設。
> 3. 抽選に共有 static Random（`MachineCurrentPowerToSubSecond`）を流用すると順序/ロード依存 → **保存される `_processedCycleCount` と `_blockInstanceId` から導出する決定的乱数**を使う。

- [ ] **Step 1: 失敗するテストを書く（容量予約＋確率1.0で追加産出）**

```csharp
        // 生産性(確率1.0)モジュールで、完了時に出力が1セット余分に得られることを検証
        // With a productivity module (chance 1.0), completion yields one extra set of item outputs
        [Test]
        public void ProductivityExtraOutputTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            // effectValue 1.0 の生産性モジュール（A1-5の TestProductivityModule）
            // Productivity module with effectValue 1.0
            var prodModule = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == "Productivity");
            block.GetComponent<IModuleSlotInventoryComponent>().TryInsertModule(0, itemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(prodModule.ItemGuid), 1));

            foreach (var inputItem in recipe.InputItems)
                block.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));

            var proc = block.GetComponent<VanillaMachineProcessorComponent>();
            var totalTicks = GameUpdater.SecondsToTicks(recipe.Time) * 4 + 20; // 生産性ペナルティで延びるため余裕
            for (uint i = 0; i < totalTicks; i++) { proc.SupplyPower(100000); GameUpdater.RunFrames(1); }

            // 出力 = レシピ出力 ×2（基本＋追加1セット）
            // Output equals recipe output x2 (base + one extra set)
            var (_, output) = GetInputOutputSlot(block.GetComponent<VanillaMachineBlockInventoryComponent>());
            var expected = recipe.OutputItems[0].Count * 2;
            Assert.AreEqual(expected, output.Sum(s => s.Count));

            #region Internal
            (System.Collections.Generic.List<IItemStack>, System.Collections.Generic.List<IItemStack>) GetInputOutputSlot(VanillaMachineBlockInventoryComponent inv)
            {
                // MachineIOTest.cs と同じリフレクションで input/output を取得
                // Reuse the reflection helper pattern from MachineIOTest.cs
                // ... 実装は MachineIOTest.cs を踏襲 ...
                return (null, null);
            }
            #endregion
        }

        // 出力が満杯で追加産出分が入らないとき、処理を開始しないことを検証
        // When output is full and extra output cannot fit, processing does not start
        [Test]
        public void ProductivityReservesOutputCapacityTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var prodModule = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == "Productivity");
            block.GetComponent<IModuleSlotInventoryComponent>().TryInsertModule(0, itemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(prodModule.ItemGuid), 1));

            // 出力スロットを満杯に（基本出力は入るが追加1セットは入らない状態）にする
            // Fill output so the base output fits but the extra set does not
            // ... VanillaMachineOutputInventory への直接set（リフレクション/SetItem）で満杯化 ...

            foreach (var inputItem in recipe.InputItems)
                block.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));

            var proc = block.GetComponent<VanillaMachineProcessorComponent>();
            proc.SupplyPower(100000);
            GameUpdater.RunFrames(1);

            Assert.AreEqual(ProcessState.Idle, proc.CurrentState);
        }
```

> 出力満杯化と input/output 取得は `MachineIOTest.cs` のリフレクションパターンを実装時に Read して埋める。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ProductivityExtraOutputTest|ProductivityReservesOutputCapacityTest"`
Expected: FAIL。

- [ ] **Step 3: 出力インベントリに仮想容量判定＋アイテム専用追加出力を実装**

`VanillaMachineOutputInventory` に追加。仮想判定は既存スロットを複製し、レシピ出力＋追加セットを順次 `AddItem` してあふれないか確認する:

```csharp
        // レシピ出力に加え、追加産出 extraSets セット分のアイテム出力まで格納できるか仮想挿入で判定
        // Check via virtual insertion whether recipe outputs plus extraSets of item outputs can be stored
        public bool CanStoreOutputs(MachineRecipeMasterElement recipe, int extraSets)
        {
            // 現在の各スロットを複製した仮想スロットを作る
            // Build virtual slots copied from current slots
            var virtualSlots = OutputSlot.Select(s => s).ToList();

            // (1 + extraSets) セット分のアイテム出力を順次挿入できるか
            // Try to insert (1 + extraSets) sets of item outputs sequentially
            var totalSets = 1 + System.Math.Max(0, extraSets);
            for (var set = 0; set < totalSets; set++)
            {
                foreach (var itemOutput in recipe.OutputItems)
                {
                    var id = MasterHolder.ItemMaster.GetItemId(itemOutput.ItemGuid);
                    var stack = ServerContext.ItemStackFactory.Create(id, itemOutput.Count);
                    if (!TryVirtualInsert(virtualSlots, stack)) return false;
                }
            }

            // 液体出力は従来どおり基本1セット分のみ判定（追加産出はアイテムのみ）
            // Fluids: check only the base set (extra output is item-only)
            return IsFluidOutputAllowed(recipe);

            #region Internal
            bool TryVirtualInsert(System.Collections.Generic.List<IItemStack> slots, IItemStack stack)
            {
                for (var i = 0; i < slots.Count; i++)
                {
                    if (!slots[i].IsAllowedToAddWithRemain(stack)) continue;
                    var result = slots[i].AddItem(stack);
                    slots[i] = result.ProcessResultItemStack;
                    if (result.RemainderItemStack.Count == 0) return true;
                    stack = result.RemainderItemStack;
                }
                return false;
            }
            #endregion
        }

        // アイテム出力のみを1セット格納する（生産性の追加産出用。液体は入れない）
        // Insert one set of item outputs only (for productivity extra output; no fluids)
        public void InsertItemOutputsOnly(MachineRecipeMasterElement recipe)
        {
            foreach (var itemOutput in recipe.OutputItems)
                for (var i = 0; i < OutputSlot.Count; i++)
                {
                    var id = MasterHolder.ItemMaster.GetItemId(itemOutput.ItemGuid);
                    var stack = ServerContext.ItemStackFactory.Create(id, itemOutput.Count);
                    if (!OutputSlot[i].IsAllowedToAdd(stack)) continue;
                    _itemDataStoreService.SetItem(i, OutputSlot[i].AddItem(stack).ProcessResultItemStack);
                    break;
                }
        }
```

`IsFluidOutputAllowed` は既存 `IsAllowedToOutputItem` の液体判定部分を切り出す（既存コードを Read して流用）。`_itemDataStoreService` のフィールド名は既存 `InsertOutputSlot` 実装に合わせる。

- [ ] **Step 4: プロセッサで開始判定・完了時抽選を実装**

`Idle()` の開始条件を仮想容量判定に差し替え、開始前に効果を集計して maxExtraSets を予約:

```csharp
        private void Idle()
        {
            var isGetRecipe = _vanillaMachineInputInventory.TryGetRecipeElement(out var recipe);
            if (!isGetRecipe) return;

            // 開始判定のため先に効果を集計（追加産出の最大セット数を予約）
            // Aggregate effect first for the start check (reserve the max extra output sets)
            var effect = MachineModuleEffect.Aggregate(_moduleSlot.GetEquippedModules());
            var maxExtraSets = effect.ExtraOutputChance > 0f ? 1 : 0;

            var isStartProcess = CurrentState == ProcessState.Idle &&
                   _vanillaMachineInputInventory.IsAllowedToStartProcess() &&
                   _vanillaMachineOutputInventory.CanStoreOutputs(recipe, maxExtraSets);

            if (isStartProcess)
            {
                CurrentState = ProcessState.Processing;
                _processingRecipe = recipe;
                _currentEffect = effect; // 開始時スナップショット
                var baseTicks = GameUpdater.SecondsToTicks(_processingRecipe.Time);
                _processingRecipeTicks = (uint)System.Math.Max(1, (long)System.Math.Round(baseTicks * _currentEffect.ProcessingTimeMultiplier));
                _vanillaMachineInputInventory.ReduceInputSlot(_processingRecipe);
                RemainingTicks = _processingRecipeTicks;
            }
        }
```

`Processing()` の完了分岐で基本出力＋確率で追加出力＋cycleCount更新:

```csharp
            if (subTicks >= RemainingTicks)
            {
                RemainingTicks = 0;
                CurrentState = ProcessState.Idle;
                _vanillaMachineOutputInventory.InsertOutputSlot(_processingRecipe);

                // 生産性: 決定的乱数で追加産出（アイテムのみ1セット）
                // Productivity: deterministic roll for one extra set of item outputs
                if (_currentEffect != null && _currentEffect.ExtraOutputChance > 0f &&
                    DeterministicRoll(_blockInstanceId, _processedCycleCount) < _currentEffect.ExtraOutputChance)
                {
                    _vanillaMachineOutputInventory.InsertItemOutputsOnly(_processingRecipe);
                }
                _processedCycleCount++;
            }
```

決定的乱数を private static で実装（共有 static Random は使わない。保存される値のみから導出）:

```csharp
        // blockInstanceId と完了回数から決定的に [0,1) を返す（セーブ/ロード・更新順に非依存）
        // Deterministic [0,1) from blockInstanceId and cycle count (independent of save/load and update order)
        private static double DeterministicRoll(BlockInstanceId blockInstanceId, int cycleCount)
        {
            // 64bit splitmix風のハッシュで decorrelate
            // Decorrelate with a splitmix64-like hash
            ulong x = (ulong)blockInstanceId.AsPrimitive() * 0x9E3779B97F4A7C15UL + (ulong)(uint)cycleCount;
            x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27; x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return (x >> 11) * (1.0 / (1UL << 53));
        }
```

> `BlockInstanceId.AsPrimitive()` の型を確認し、`ulong` キャストを合わせる。

- [ ] **Step 5: Unity再起動 → コンパイル → テスト**

Run: Unity再起動後 `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineModuleSlotTest"`
Expected: 全PASS。

- [ ] **Step 6: 回帰確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineIOTest|GearMachineIoTest|PowerGeneratorTest"`
Expected: PASS。

- [ ] **Step 7: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/
git commit -m "feat(block): productivity extra output with virtual capacity reservation and deterministic roll"
```

### Task A3-5: 処理中セーブ/ロードと抜き差しのスナップショット検証

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotTest.cs`

> **Codex監査反映:** A3-4以前の弱い検証を、(1)処理中セーブ/ロードで効果が壊れない (2)処理中の抜き差しでスナップショットが維持される、の2本に強化する。

- [ ] **Step 1: 処理中セーブ/ロードのテストを書く**

速度モジュールで処理開始 → 数tick進める → save → reload。reload後の合計tickが「スナップショット済み(短縮済み)」で、ベース再計算に戻っていないことを検証。

```csharp
        // 処理中にセーブ/ロードしても、短縮済みの進捗と効果が保持されることを検証
        // Verify shortened progress and effect survive a mid-process save/load
        [Test]
        public void EffectSurvivesMidProcessSaveLoadTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var speedModule = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == "Speed");
            block.GetComponent<IModuleSlotInventoryComponent>().TryInsertModule(0, itemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(speedModule.ItemGuid), 1));
            foreach (var inputItem in recipe.InputItems)
                block.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));

            var proc = block.GetComponent<VanillaMachineProcessorComponent>();
            proc.SupplyPower(100000); GameUpdater.RunFrames(1); // 開始（スナップショット）
            var totalTicksBefore = proc.ProcessingRecipeTicks;

            // 処理中にセーブ/ロード
            // Save/load mid-process
            var state = block.GetSaveState();
            var reloaded = ServerContext.BlockFactory.Load(block.BlockGuid, block.BlockInstanceId, state, block.BlockPositionInfo);
            var reloadedProc = reloaded.GetComponent<VanillaMachineProcessorComponent>();

            // 合計tickが短縮済みのまま（ベース時間からの再計算で増えていない）
            // Total ticks remain shortened (not recomputed back to base)
            Assert.AreEqual(totalTicksBefore, reloadedProc.ProcessingRecipeTicks);
            Assert.Less(reloadedProc.ProcessingRecipeTicks, GameUpdater.SecondsToTicks(recipe.Time));
        }
```

- [ ] **Step 2: 処理中の抜き差しテストを書く（スナップショット維持）**

```csharp
        // 処理開始後にモジュールを抜いても、進行中サイクルの合計tickが変わらないことを検証
        // Verify removing a module mid-process does not change the in-flight cycle's total ticks
        [Test]
        public void EffectSnapshotPersistsAfterModuleRemovalTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var speedModule = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == "Speed");
            var slot = block.GetComponent<IModuleSlotInventoryComponent>();
            slot.TryInsertModule(0, itemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(speedModule.ItemGuid), 1));
            foreach (var inputItem in recipe.InputItems)
                block.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));

            var proc = block.GetComponent<VanillaMachineProcessorComponent>();
            proc.SupplyPower(100000); GameUpdater.RunFrames(1);
            var totalTicks = proc.ProcessingRecipeTicks;

            slot.RemoveModule(0); // 処理中に抜く

            proc.SupplyPower(100000); GameUpdater.RunFrames(1);
            // 合計tickは開始時スナップショットのまま（中立に戻らない）
            // Total ticks stay at the start snapshot (do not revert to neutral)
            Assert.AreEqual(totalTicks, proc.ProcessingRecipeTicks);
            Assert.AreEqual(ProcessState.Processing, proc.CurrentState);
        }
```

- [ ] **Step 3: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EffectSurvivesMidProcessSaveLoadTest|EffectSnapshotPersistsAfterModuleRemovalTest"`
Expected: PASS（A3-2の永続化・スナップショット実装で保証）。FAILなら Load経路の保存値復元（A3-2 Step5）を確認。

- [ ] **Step 4: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotTest.cs
git commit -m "test(block): verify effect snapshot survives mid-process save/load and module removal"
```

---

## フェーズA完了条件チェック（設計仕様 §8.5）

- [x] モジュール専用サブインベントリの**保存**と**移動制限**（挿入制限・通常搬入不可・装着済み上書き拒否） — A2
- [x] 処理開始時の効果スナップショット**＋セーブ永続化** — A3-2 / A3-5
- [x] 加算後の clamp — A3-1
- [x] 消費電力倍率を実効要求電力(RequestEnergy)に適用 — A3-3
- [x] 生産性追加産出の**仮想容量予約**と**決定的抽選** — A3-4
- [x] 効果集計の統一結果に品質フックの場所を確保 — `MachineModuleEffect` に Quality 軸の集計分岐を空けてある（A3-1）
- [ ] **ネットワーク同期** — 本プランの**範囲外**（下記）。サブインベントリの保存・移動制限・サーバー操作APIまでは本プランで完結するが、クライアントへの同期は未対応。

> **完了の定義（矛盾回避）:** 本プランの「完了」は**サーバーサイドのロジック＋セーブ＋サーバー操作API**まで。ネット同期とクライアントUIは含めない（テストは direct component test で成立する）。設計仕様 §8.5 が挙げる「同期」はフェーズA全体の完了条件だが、本実装プランのスコープからは外し、別プランに送る。

## フェーズAの残課題（このプランの範囲外・要フォロー）

- **ネットワーク同期とクライアントUI**: モジュールスロットの内容をクライアントへ送り、装着UIを出す部分。別プランで実装（プロトコル拡張＋クライアント実装）。
- **GearMachine の消費（RPM/トルク）への省エネ適用**: A3-3 の電力倍率は electric 経路のみ。歯車機械の消費削減は別フォロー（処理時間効果は両機械で有効）。
- **プレイヤーによるモジュール装着の正式操作経路**: A2 でサーバー側 API（`TryInsertModule`/`RemoveModule`）は用意するが、プレイヤー操作プロトコル経由の装着は同期と合わせて別プラン。

---

# フェーズB（品質軸）について

本プランの対象外。フェーズA完了後、Aで確定した `MachineModuleEffect` / モジュールスロットの具体APIを踏まえて別プラン（`2026-XX-XX-upgrade-system-phase-b.md`）を作成する。内容: レベルファミリー機構（SourceGenerator自動生成・決定的GUID）、レベル抽選テーブル＋品質シフト、抽選順序の決定性（設計仕様 §7.2）。
