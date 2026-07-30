---
spec: docs/superpowers/specs/2026-07-29-localization-foundation-design.md
---

# Localization Mod Dictionary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** mod同梱ローカライズCSVと `<type>.<guid>.<field>` 導出キーを実装し、ホスト側のマスタ名解決・payload同梱を全廃してWeb側辞書解決へ統一する（item/block・研究/チャレンジ・skit・ビルドメニュー）。

**Architecture:** Plan1のバニラ基盤（埋め込み辞書・共通CSV DLL・`Localize`・source擬似ロケール・TS型付きキー）の上に、①mod CSV合成、②MasterHolder原文投入、③Guid導出キー、④DTO/TopicのGuid化を積む。skitは既存CommandForgeEditor辞書を保持し、`Client.Skit` のresolver interfaceに対する `Client.Game` 側のAddressables loader/resolverが開始時に対象言語+englishだけを読み、`skit.*` のみを欠けたキーとしてmod合成辞書へ重ねる。skitの解決順は mod対象言語→skit専用対象言語→mod英語→skit専用英語→JSON原文、それ以外は 対象言語→english→source→`[!key]`。

**Tech Stack:** C# (Unity asmdef) / React + zustand / uloop / NUnit（クライアントプロジェクト経由でサーバーテスト実行）

## Global Constraints

- Plan1（`docs/superpowers/plans/2026-07-29-localization-vanilla-foundation.md`）完了が前提
- partial禁止・`Func<>`禁止・try-catch原則禁止（外部境界のみ・根拠コメント必須）・UniRx・200行/ファイル・日本語→英語2行コメント（AGENTS.md）
- マスタ導出キー規約: `<type>.<guid>.<field>`。Guidは `ToString("D")` 小文字、type/fieldはlowerCamel。Skit fieldだけはCommandForge schema名を正確に保持し `Option1Tag`〜`Option3Tag` の大文字も変えない
- キーにmodIdを含めない（ユーザー裁定 2026-07-29）
- CSV解析はPlan1の `mooresmaster.LocalizationCsv.dll` を参照し、runtime側にparser・行モデル・例外を複製しない
- parserは空fieldを保持するが、merger/resolverは空文字を欠落として登録/返却せず必ず次のfallback段へ進む
- characters/buildMenuのGuidは必須追加し全JSONを一括更新する。optional・`?? Default`・ローダー補完は禁止
- `Client.Skit` は `Client.Localization` / Addressablesを直接参照せず、下流 `Client.Game` の具体resolverをStoryContextへ登録する
- skit開始時にロードする辞書は選択言語とenglishの2ファイルだけ。ゲーム辞書へ取り込むキーは `skit.` 接頭辞だけ
- Skit titleの正本はAddressable asset basename / runtimeの `TextAsset.name`。JSON `meta.title` はキーへ使わず一致検査だけに使う
- 変更の波及を恐れない: DTO契約の破壊的変更はホストとwebuiを同一タスク内で一括更新（AGENTS.md）
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client`
- 各タスク末で必ずコミット

## File Structure

```
moorestech_client/Assets/Scripts/Client.Localization/
├── Localize.cs                          ← Initialize拡張（mod合成・source原文投入の呼び口追加）
├── ModLocalizationMerger.cs             ← 新設・mods配下のCSV列挙と合成
├── MasterSourceTextCollector.cs         ← 新設・MasterHolder原文→source擬似ロケール
└── ContentLocalizationKeys.cs           ← 新設・導出キービルダー

moorestech_client/Assets/Scripts/Client.Skit/
├── Localization/ISkitLocalizationResolver.cs ← 新設・汎用層の解決interface（Localize非依存）
├── Localization/SkitTitle.cs                  ← 新設・asset basenameからtitleを導出する唯一の純粋処理
└── Context/SkitExecutionIdentity.cs           ← 新設・skitTitleをcommandへ渡すcontext

moorestech_client/Assets/Scripts/Client.Game/
└── Skit/Localization/
    ├── SkitLocalizationDictionaryLoader.cs    ← 新設・対象言語+englishのAddressables動的load
    └── SkitLocalizationResolver.cs            ← 新設・skit.*限定合成と5段解決

moorestech_client/Assets/Scripts/Client.WebUiHost/Game/
├── ItemMasterEndpoint.cs                ← Name削除・ItemGuid追加
├── Topics/BlockInventoryTopic.cs        ← BlockName削除・BlockGuid追加
├── Topics/MachineRecipesTopic.cs        ← BlockName削除（BlockGuidは既存）
└── Topics/BuildMenu/BuildMenuEntryDtoFactory.cs ← Label運用の整理・カテゴリキー化

moorestech_web/webui/src/
├── shared/i18n/contentKeys.ts           ← 新設・導出キービルダー＋ContentLocalizationKey型
├── shared/i18n/i18nStore.ts             ← TranslationKeyへcontent key合流
├── bridge/**（契約型）                    ← name削除・guid追加の追従
└── 各消費コンポーネント                    ← 辞書解決化

VanillaSchema/characters.yml             ← 必須characterGuid追加（Task 6）
VanillaSchema/buildMenu.yml              ← カテゴリ/サブカテゴリへ必須Guid追加（Task 7）
../moorestech_master/**/master/characters.json ← 全characters JSONへcharacterGuid追加
../moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv ← 新設サンプル
moorestech_client/Assets/AddressableResources/Skit/i18n/{english,japanese}.json ← 保持しskit.*を追加
```

---

### Task 1: mod辞書の合成とsource原文投入

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Localization/ModLocalizationMerger.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Localization/MasterSourceTextCollector.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Localize.cs`

**Interfaces:**
- Consumes: Plan1の `mergedDictionary` 構造・`Mooresmaster.LocalizationCsv.LocalizationCsvParser`・`Mod.Loader.ModsResource`（`ExtractedPath` を各modが持つ。zip modは展開先ディレクトリ）
- Produces:
  - `public static void Localize.MergeGameDictionaries(ModsResource modsResource)` — ①各mod `localization/localization.csv` を合成（`SortedModIds` 順・後勝ち）②`MasterSourceTextCollector.Collect()` をsource擬似ロケールへ合成。ゲーム開始（MasterHolder.Load後）に呼ぶ
  - `public static string Localize.GetContent(string derivedKey)` — 対象言語→english→source→`[!{derivedKey}]`

- [ ] **Step 1: 呼び出し地点を特定する**

Run: `grep -rn "MasterHolder.Load\|ServerInstanceManager" moorestech_client/Assets/Scripts --include="*.cs" | grep -v Test`
Expected: クライアント側でサーバー起動を駆動している箇所（`ServerInstanceManager.cs:53` の `MasterHolder.Load` はサーバーアセンブリ）。クライアント側の起動フロー（サーバー起動完了後・ハンドシェイク前後）で `ModsResource` に到達できる場所を特定する。`ModsResource` がクライアントへ公開されていない場合は、`ServerInstanceManager`（または `MoorestechServerDIContainerGenerator` の呼び出し側）から `ModsResource` を公開する最小の口を足す（サーバー側にローカライズの知識は入れない — 公開するのはリソースのみ）。

- [ ] **Step 2: ModLocalizationMerger を実装する（サーバーテストで駆動）**

`ModLocalizationMerger.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using Mod.Loader;
using Mooresmaster.LocalizationCsv;

namespace Client.Localization
{
    public static class ModLocalizationMerger
    {
        private const string LocalizationCsvRelativePath = "localization/localization.csv";

        // SortedModIds順に各modのCSVを読み、後勝ちで辞書に上書きする
        // Read each mod's CSV in SortedModIds order; later mods overwrite earlier keys
        public static void Merge(ModsResource modsResource, Dictionary<string, Dictionary<string, string>> mergedDictionary)
        {
            foreach (var mod in modsResource.SortedMods)
            {
                var csvPath = Path.Combine(mod.ExtractedPath, LocalizationCsvRelativePath);
                if (!File.Exists(csvPath)) continue;
                MergeCsv(File.ReadAllText(csvPath), mergedDictionary);
            }
        }

        public static void MergeCsv(string csvText, Dictionary<string, Dictionary<string, string>> mergedDictionary)
        {
            // フォーマットはバニラCSVと完全同一。ヘッダの言語列だけを合成対象にする
            // Same format as the vanilla CSV; only languages present in the header are merged
            var csv = LocalizationCsvParser.Parse(csvText);
            foreach (var row in csv.Rows)
            {
                if (!string.IsNullOrEmpty(row.Source))
                    mergedDictionary["source"][row.Key] = row.Source;
                for (var i = 0; i < csv.LanguageCodes.Length; i++)
                {
                    var text = row.Texts[i];
                    if (!string.IsNullOrEmpty(text))
                        mergedDictionary[csv.LanguageCodes[i]][row.Key] = text;
                }
            }
        }
    }
}
```

`Client.Localization.asmdef` からPlan1でclient/serverへ配置済みの `mooresmaster.LocalizationCsv.dll` を参照する。`rg -l "class LocalizationCsvParser" mooresmaster moorestech_client moorestech_server` が共通ライブラリの1件だけであることを検査する。`ModsResource` のAPI実名（`SortedMods`/`ExtractedPath`）は `Mod.Loader/ModsResource.cs` を読んで合わせる。

- [ ] **Step 3: MasterSourceTextCollector を実装する**

```csharp
using System.Collections.Generic;
using Core.Master;

namespace Client.Localization
{
    public static class MasterSourceTextCollector
    {
        // MasterHolderの表示原文を導出キー→原文のマップとして収集する
        // Collect display source texts from MasterHolder as derivedKey -> text
        public static Dictionary<string, string> Collect()
        {
            var result = new Dictionary<string, string>();
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var master = MasterHolder.ItemMaster.GetItemMaster(itemId);
                result[ContentLocalizationKeys.ItemName(master.ItemGuid)] = master.Name;
            }
            foreach (var block in MasterHolder.BlockMaster.Blocks.Data)
                result[ContentLocalizationKeys.BlockName(block.BlockGuid)] = block.Name;
            // 研究: researchNodeGuid + researchNodeName / researchNodeDescription
            // チャレンジ: challengeGuid + title / summary、カテゴリ: categoryGuid + categoryName / categoryDescription
            /* research/challenges/buildMenuカテゴリ(Task 7以降)を同様に列挙。
               各Masterのコレクション実名は Core.Master の生成クラスを読んで合わせる */
            return result;
        }
    }
}
```

- [ ] **Step 4: Localize へ組み込む**

`Localize.cs` へ追加:

```csharp
        public static void MergeGameDictionaries(Mod.Loader.ModsResource modsResource)
        {
            ModLocalizationMerger.Merge(modsResource, mergedDictionary);
            var sourceTexts = MasterSourceTextCollector.Collect();
            foreach (var pair in sourceTexts)
                if (!string.IsNullOrEmpty(pair.Value))
                    mergedDictionary[SourcePseudoLocale][pair.Key] = pair.Value;
            // 辞書更新をWeb/UIへ通知する（既存の言語変更通知と同じ購読で再取得される）
            // Notify the Web/UI so subscribers refetch, same as a language change
            _onLanguageChangedSubject.OnNext(Unit.Default);
        }

        public static string GetContent(string derivedKey)
        {
            if (mergedDictionary[CurrentLanguageCode].TryGetValue(derivedKey, out var value) && !string.IsNullOrEmpty(value)) return value;
            if (mergedDictionary[DefaultLanguageCode].TryGetValue(derivedKey, out var english) && !string.IsNullOrEmpty(english)) return english;
            if (mergedDictionary[SourcePseudoLocale].TryGetValue(derivedKey, out var source) && !string.IsNullOrEmpty(source)) return source;
            return $"[!{derivedKey}]";
        }
```

Step 1で特定した起動フローから `Localize.MergeGameDictionaries(modsResource)` を呼ぶ（asmdef: Client.Localization に `Mod.Loader`・`Core.Master` 参照を追加）。

- [ ] **Step 5: テストを書く（クライアント経由でNUnit実行）**

`MergeCsv` と `GetContent` のチェーンをユニットテスト化（creating-server-testsスキルの慣習に従い、テスト配置はClient.Localizationのテスト用asmdefが無ければ `Client.Tests` 配下の前例に合わせる）。少なくとも空日本語/空sourceが既存値を上書きせずfallbackを塞がないことを次のfixtureで固定する:

```csharp
    [Test]
    public void 空翻訳は登録せず既存値とfallbackを維持する()
    {
        var dictionaries = new Dictionary<string, Dictionary<string, string>>
        {
            ["english"] = new() { ["item.test.name"] = "Vanilla English" },
            ["japanese"] = new() { ["item.test.name"] = "既存日本語" },
            ["source"] = new() { ["item.test.name"] = "既存原文" },
        };
        var csv = "key,Source,english,japanese\nitem.test.name,,Mod English,\n";

        ModLocalizationMerger.MergeCsv(csv, dictionaries);

        Assert.That(dictionaries["japanese"]["item.test.name"], Is.EqualTo("既存日本語"));
        Assert.That(dictionaries["english"]["item.test.name"], Is.EqualTo("Mod English"));
        Assert.That(dictionaries["source"]["item.test.name"], Is.EqualTo("既存原文"));
    }
```

別ケースで対象言語に空文字が残った辞書を直接与え、`GetContent` / `TryGetContentWithoutSource` が空文字を返さずenglish、その次はsource、その次は`[!key]`へ進むことを各段1assertで検証する。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ModLocalization"`
Expected: PASS

- [ ] **Step 6: コンパイル・コミット**

```bash
uloop compile --project-path ./moorestech_client
git add moorestech_client/Assets/Scripts/Client.Localization/ && git commit -m "feat: mod辞書合成とsource原文投入・GetContent"
```

---

### Task 2: ContentLocalizationKeys（C#/TS両側の導出キービルダー）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Localization/ContentLocalizationKeys.cs`
- Create: `moorestech_web/webui/src/shared/i18n/contentKeys.ts`
- Modify: `moorestech_web/webui/src/shared/i18n/i18nStore.ts`（TranslationKey合流）

**Interfaces:**
- Produces（C#）: `public static class ContentLocalizationKeys` — item/block/research/challenge/character/buildMenuのGuid導出キー、およびCommandForge schema field固定のskitキー
- Produces（TS）: 同名のlowerCamel関数群＋`export type ContentLocalizationKey = \`item.${string}.name\` | \`block.${string}.name\` | ...`（テンプレートリテラル型）。`i18nStore.ts` の `TranslationKey = VanillaLocalizationKey | ContentLocalizationKey`

- [ ] **Step 1: C#側を実装する**

```csharp
using System;

namespace Client.Localization
{
    public static class ContentLocalizationKeys
    {
        public static string ItemName(Guid guid) => $"item.{guid:D}.name";
        public static string BlockName(Guid guid) => $"block.{guid:D}.name";
        public static string ResearchNodeName(Guid guid) => $"research.{guid:D}.name";
        public static string ResearchNodeDescription(Guid guid) => $"research.{guid:D}.description";
        public static string ChallengeTitle(Guid guid) => $"challenge.{guid:D}.title";
        public static string ChallengeSummary(Guid guid) => $"challenge.{guid:D}.summary";
        public static string ChallengeCategoryName(Guid guid) => $"challengeCategory.{guid:D}.name";
        public static string CharacterName(Guid characterGuid) => $"character.{characterGuid:D}.name";
        public static string BuildMenuCategoryName(Guid categoryGuid) => $"buildMenuCategory.{categoryGuid:D}.name";
        public static string BuildMenuSubCategoryName(Guid subCategoryGuid) => $"buildMenuSubCategory.{subCategoryGuid:D}.name";
        public static string SkitTextBody(string skitTitle, int commandId) => SkitField(skitTitle, commandId, "body");
        public static string SkitBackgroundBody(string skitTitle, int commandId) => SkitField(skitTitle, commandId, "body");
        public static string SkitSelectionOption1Tag(string skitTitle, int commandId) => SkitField(skitTitle, commandId, "Option1Tag");
        public static string SkitSelectionOption2Tag(string skitTitle, int commandId) => SkitField(skitTitle, commandId, "Option2Tag");
        public static string SkitSelectionOption3Tag(string skitTitle, int commandId) => SkitField(skitTitle, commandId, "Option3Tag");
        public static string SkitOverrideCharacterName(string skitTitle, int commandId) => SkitField(skitTitle, commandId, "overrideCharacterName");

        private static string SkitField(string skitTitle, int commandId, string field)
        {
            return $"skit.{skitTitle}.{commandId}.{field}";
        }
    }
}
```

- [ ] **Step 2: TS側を実装する**

`contentKeys.ts`:

```typescript
export type ContentLocalizationKey =
  | `item.${string}.name`
  | `block.${string}.name`
  | `research.${string}.name`
  | `research.${string}.description`
  | `challenge.${string}.title`
  | `challenge.${string}.summary`
  | `challengeCategory.${string}.name`
  | `character.${string}.name`
  | `buildMenuCategory.${string}.name`
  | `buildMenuSubCategory.${string}.name`;
// skitキーはUnity側resolverで解決済みの表示文字列をpushするためWebから構築しない
// Skit keys are resolved by the Unity-side resolver before display strings are pushed

export const itemNameKey = (guid: string): ContentLocalizationKey => `item.${guid}.name`;
export const blockNameKey = (guid: string): ContentLocalizationKey => `block.${guid}.name`;
export const researchNodeNameKey = (guid: string): ContentLocalizationKey => `research.${guid}.name`;
export const researchNodeDescriptionKey = (guid: string): ContentLocalizationKey => `research.${guid}.description`;
export const challengeTitleKey = (guid: string): ContentLocalizationKey => `challenge.${guid}.title`;
export const challengeSummaryKey = (guid: string): ContentLocalizationKey => `challenge.${guid}.summary`;
export const buildMenuCategoryNameKey = (guid: string): ContentLocalizationKey => `buildMenuCategory.${guid}.name`;
export const buildMenuSubCategoryNameKey = (guid: string): ContentLocalizationKey => `buildMenuSubCategory.${guid}.name`;
```

`i18nStore.ts`: `export type TranslationKey = VanillaLocalizationKey | ContentLocalizationKey;`（Plan1で用意した拡張点）。barrel（`src/shared/i18n/index.ts`）からre-export。

- [ ] **Step 3: 型と鍵規約の対応テスト**

C#/TSの導出キーが同一文字列を生むことを固定化するテスト（TS側vitestに `itemNameKey("abc")` === `"item.abc.name"` 等の期待値を列挙。C#側はTask 1のテストでカバー）。

Run: `cd moorestech_web/webui && npx vitest run src/shared/i18n/ && npx tsc -b`
Expected: PASS

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Localization/ContentLocalizationKeys.cs moorestech_web/webui/src/shared/i18n/
git commit -m "feat: 導出キービルダーをC#/TS両側に追加"
```

---

### Task 3: ItemMaster配信のGuid化とWeb側アイテム名解決

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/ItemMasterEndpoint.cs:48-53,68-73`
- Modify: `moorestech_web/webui/src/bridge/contract/payloadTypes.ts`（ItemMasterEntry）
- Modify: `moorestech_web/webui/src/bridge/store/itemMasterStore.ts:27-38`
- Modify: `moorestech_web/webui/src/shared/ui/ItemSlot/index.tsx:33-34` ほか `name` 参照全件

**Interfaces:**
- Produces: `ItemMasterDto { int ItemId; string ItemGuid; int MaxStack; }`（Name削除）。Web側は `useItemName(itemId)`（新設hook: itemId→guid→`t(itemNameKey(guid))`）で名前解決

- [ ] **Step 1: DTOを変更する**

`ItemMasterEndpoint.cs` の `BuildResponse`:

```csharp
                dto.Items.Add(new ItemMasterDto
                {
                    ItemId = itemId.AsPrimitive(),
                    ItemGuid = master.ItemGuid.ToString("D"),
                    MaxStack = stackLevelLookup.GetMaxStack(itemId),
                });
```

`ItemMasterDto` から `Name` を削除し `ItemGuid` を追加。

- [ ] **Step 2: webui契約と検証を追従する**

`payloadTypes.ts` の `ItemMasterEntry` を `{ itemId: number; itemGuid: string; maxStack: number }` へ。`itemMasterStore.ts:27-38` の `isItemMasterEntry` から `name` 検査を外し `itemGuid` 検査を足す。

- [ ] **Step 3: 名前解決hookを新設する**

`src/shared/i18n/useItemName.ts`（barrelからexport）:

```typescript
import { useI18n } from "./index";
import { itemNameKey } from "./contentKeys";
import { useItemMaster } from "@/bridge";

// itemId→Guid→辞書解決。マスタ未着はnullを返し呼び出し側が非表示にする
// itemId -> Guid -> dictionary chain; null while the master has not arrived
export function useItemNameResolver(): (itemId: number) => string | null {
  const { t } = useI18n();
  const itemMaster = useItemMaster();
  return (itemId: number) => {
    const guid = itemMaster?.get(itemId)?.itemGuid;
    return guid ? t(itemNameKey(guid)) : null;
  };
}
```

- [ ] **Step 4: `name` 参照を全件置換する**

Run: `cd moorestech_web/webui && grep -rn "\.name" src --include='*.ts' --include='*.tsx' | grep -i "itemMaster\|master?.get\|resolveName"`
全ヒット（`ItemSlot/index.tsx:33-34`・`RecipeContent.tsx:60`・`ResearchTreePanel.tsx:26` の `resolveName` 等）を `useItemNameResolver` 経由へ置換。

- [ ] **Step 5: 検証・コミット**

Run: `uloop compile --project-path ./moorestech_client && cd moorestech_web/webui && npx tsc -b && npm run test && npm run lint`
Expected: 全て成功

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Game/ItemMasterEndpoint.cs moorestech_web/webui/
git commit -m "feat: アイテムマスタ配信をGuid化しWeb側辞書解決へ統一"
```

---

### Task 4: BlockName同梱の全廃（BlockInventoryTopic・MachineRecipesTopic）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs:138` 周辺（`BlockName = blockSource.BlockName` → `BlockGuid`）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/MachineRecipesTopic.cs:57-66`（`BlockName` 削除。`BlockGuid` は既存）
- Modify: webui側の `blockName` 消費コンポーネント（blockInventory詳細ヘッダ等）と契約型

**Interfaces:**
- Produces: `BlockInventoryDto.BlockGuid`（string）。Web側は `t(blockNameKey(blockGuid))` で表示

- [ ] **Step 1: ホスト側からName系フィールドを削除しGuidを流す**

`BlockInventoryDto` の `BlockName` を `BlockGuid` へ置換（`blockSource` にGuidが無ければ `BlockGameObject.BlockMasterElement.BlockGuid` から取得）。`MachineRecipeDto` から `BlockName` を削除。

- [ ] **Step 2: webuiの消費側を置換する**

Run: `cd moorestech_web/webui && grep -rn "blockName\|BlockName" src`
全ヒットを `t(blockNameKey(payload.blockGuid))` へ。契約型（payloadTypes.ts）も同時更新。

- [ ] **Step 3: Unity側のName直結約20箇所を置換する**

Run: `grep -rn "BlockMasterElement.Name\|GetItemMaster(.*).Name\|master.Name" moorestech_client/Assets/Scripts/Client.Game --include="*.cs"`
全ヒット（`MachineBlockInventoryView.cs:46`・`CraftInventoryView.cs:139`・`ItemListView.cs:52`・`MachineRecipeSelectionPanel.cs:105-109`・`MinerBlockInventoryView.cs:137`・`MachineRecipeView.cs:146`・`GeneratorBlockInventoryView.cs:31`・`ChestBlockInventoryView.cs:25`・`ElectricPoleNetworkInfoUIView.cs:24`・`GearEnergyTransformerUIView.cs:27` ほかgrep実測分）を `Localize.GetContent(ContentLocalizationKeys.ItemName(master.ItemGuid))` / `Localize.GetContent(ContentLocalizationKeys.BlockName(element.BlockGuid))` へ置換。asmdef参照（Client.Game→Client.Localization）が無ければ追加。

- [ ] **Step 4: 検証・コミット**

Run: `uloop compile --project-path ./moorestech_client && cd moorestech_web/webui && npx tsc -b && npm run test && npm run test:e2e`
Expected: 全て成功（E2Eの表示文言はsource原文フォールバックにより不変）

```bash
git add -A && git commit -m "feat: ホストのName同梱を全廃しGuid+辞書解決へ統一"
```

---

### Task 5: 研究・チャレンジ文言の辞書解決化

**Files:**
- Modify: 研究/チャレンジのTopic・DTO（`researchTree` topic のノードname/description、チャレンジHUDのtitle/summary）
- Modify: webui `features/research/**`・`features/challenge/**`

- [ ] **Step 1: 配信箇所を実測する**

Run: `grep -rn "researchNodeName\|ResearchNodeName\|\.Title\|\.Summary" moorestech_client/Assets/Scripts/Client.WebUiHost --include="*.cs"`
研究ツリー/チャレンジ系Topicがマスタ文言をpayloadへ入れている箇所を列挙する。

- [ ] **Step 2: name/description/title/summaryをpayloadから削除しGuidのみにする**

研究ノードは `guid` が既にpayloadにある（`ResearchTreePanel.tsx:16` の `node.guid`）。文言フィールドを削除し、webuiは `t(researchNodeNameKey(node.guid))` / `t(researchNodeDescriptionKey(node.guid))`、チャレンジは `t(challengeTitleKey(...))` / `t(challengeSummaryKey(...))` へ。`MasterSourceTextCollector` に研究/チャレンジの原文投入が入っていること（Task 1 Step 3）を確認。

- [ ] **Step 3: 検証・コミット**

Run: `uloop compile --project-path ./moorestech_client && cd moorestech_web/webui && npx tsc -b && npm run test`

```bash
git add -A && git commit -m "feat: 研究・チャレンジ文言を導出キー解決へ移行"
```

---

### Task 6: Skit専用辞書の動的loader/resolverとcharacterGuid

**Files:**
- Modify: `VanillaSchema/characters.yml`（必須 `characterGuid`）
- Modify: `/Users/katsumi/moorestech_master/**/master/characters.json`（全ファイル一括更新）
- Create: `moorestech_client/Assets/Scripts/Client.Skit/Localization/ISkitLocalizationResolver.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Skit/Localization/SkitTitle.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Skit/Context/SkitExecutionIdentity.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Skit/Context/StoryContextExtension.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Skit/Commands/TextCommand.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Skit/Commands/SelectionCommand.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Skit/Commands/BackgroundSkitTextCommand.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/Skit/Localization/SkitLocalizationDictionaryLoader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/Skit/Localization/SkitLocalizationResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Skit/SkitManager.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BackgroundSkit/BackgroundSkitManager.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Localize.cs`
- Modify: `moorestech_client/Assets/AddressableResources/Skit/skits/*.json`（`overideCharacterName` をschema名へ正規化）
- Modify: `moorestech_web/webui/src/features/skit/SkitPresentation.tsx`
- Modify: `moorestech_web/webui/src/features/skit/controls/SkitChoiceList.tsx`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/**/SkitLocalizationResolverTest.cs`

**Interfaces:**
- `ISkitLocalizationResolver.ResolveCommandField(string skitTitle, int commandId, string field, string sourceText)` — scoped対象言語→scoped english→手元原文
- `ISkitLocalizationResolver.ResolveCharacterName(string characterId, string skitTitle, int commandId, bool useOverride, string overrideSource)` — override時は `overrideCharacterName` field、通常時は `characterId`→必須characterGuid→`character.<guid>.name`
- `SkitTitle.FromAssetName(string assetName)` — Addressable asset basename / `TextAsset.name` をSkit title正本として返す唯一の導出処理
- `SkitLocalizationDictionaryLoader.LoadAsync(string languageCode)` — Address `Vanilla/Skit/i18n/{languageCode}` の `translations` から `skit.` かつ非空のキーだけを返す
- `SkitLocalizationResolver.PrepareAsync(string skitTitle)` — 選択言語+englishの2辞書をloadし、各言語のmod合成辞書copyへ欠けたキーだけ追加する
- `Localize.TryGetContentWithoutSource(string key, out string text)` — 現在言語→englishのみ。resolverのscoped辞書を作る際の基準と単体検査に使う

- [ ] **Step 1: characterGuidを必須化し全characters JSONを一括更新する**

`edit-schema` スキルで `VanillaSchema/characters.yml` の `characterId` 前へ `characterGuid`（`type: uuid`, `autoGenerated: true`）を追加する。`find /Users/katsumi/moorestech_master -path '*/master/characters.json'` の全ファイルへ重複しないUUIDを追加し、optional・既存characterId由来の代替Guid・ローダー補完は作らない。`characterId` はCommandForgeの操作IDとして削除・改名しない。

Run: `uloop compile --project-path ./moorestech_client && rg -L '"characterGuid"' $(find /Users/katsumi/moorestech_master -path '*/master/characters.json')`
Expected: compile成功、`characterGuid` 欠落ファイル0件

- [ ] **Step 2: CommandForge field名をschemaへ固定する**

キーfieldは `commands.yaml` のプロパティ名 `body`、`Option1Tag`、`Option2Tag`、`Option3Tag`、`overrideCharacterName` をそのまま使う。既存skit JSONの旧誤字 `overideCharacterName` は全件 `overrideCharacterName` へ一括更新し、C#生成プロパティも `OverrideCharacterName`、JSONも `overrideCharacterName` に固定する。

Run: `rg -n '"overideCharacterName"|\\.overideCharacterName' moorestech_client/Assets/AddressableResources/Skit moorestech_client/Assets/Scripts/Client.Skit`
Expected: ヒット0

- [ ] **Step 3: Client.SkitへLocalize非依存interfaceと実行identityを置く**

```csharp
namespace Client.Skit.Localization
{
    public interface ISkitLocalizationResolver
    {
        string ResolveCommandField(string skitTitle, int commandId, string field, string sourceText);
        string ResolveCharacterName(string characterId, string skitTitle, int commandId,
            bool useOverride, string overrideSource);
    }
}
```

`SkitTitle` は `FromAssetName(string assetName)` だけを持つ純粋なstatic classとし、runtime/test双方のキー導出入口を1つにする。入力は拡張子なしのasset basenameであることを検査してそのまま返し、JSON `meta.title` を読む口は持たない。`SkitExecutionIdentity` はconstructor必須の `string SkitTitle` だけを公開する。`StoryContextExtension` にresolver/identity取得を追加し、汎用 `Client.Skit` asmdefへ `Client.Localization` やAddressables参照を追加しない。

```csharp
namespace Client.Skit.Localization
{
    public static class SkitTitle
    {
        public static string FromAssetName(string assetName)
        {
            if (assetName.Contains(".")) throw new System.ArgumentException("Skit asset name must not contain an extension");
            return assetName;
        }
    }
}
```

- [ ] **Step 4: Client.Game側にAddressables loaderと具体resolverを実装する**

`SkitLocalizationDictionaryLoader` は `AddressableLoader.LoadAsyncDefault<TextAsset>($"Vanilla/Skit/i18n/{languageCode}")` で1ファイルを読み、CommandForge形式 `{ locale, name, translations }` の `translations` から `skit.` で始まり値が空文字でないentryだけを返す。空文字は辞書へ入れず欠落として後段へ進める。外部入力JSON境界のparse失敗は握り潰さず、対象addressを含むエラーとして表面化する。

`SkitLocalizationResolver.PrepareAsync` は `Localize.CurrentLanguageCode` と `english` だけをロードする。`Localize.TryGetDictionary` のmod合成済み辞書から非空entryだけを各言語scopeへcopyし、Skit専用entryも非空かつkeyが無い場合だけ追加するため、空mod値でSkit値を塞がず非空mod値も上書きしない。Resolve時も `TryGetValue && !string.IsNullOrEmpty(value)` を各段で使い、scoped対象言語→scoped english→`sourceText`へ進む。`Localize.OnLanguageChanged` を購読し、skit実行中の変更時は新しい対象言語+englishをreloadしてatomicにscopeを差し替える。reload前の現在行は再pushせず、完了後に次に表示する行から新言語を使う。

```csharp
foreach (var pair in skitDictionary)
{
    if (!string.IsNullOrEmpty(pair.Value) && !scope.ContainsKey(pair.Key))
        scope.Add(pair.Key, pair.Value);
}

public static bool TryGetContentWithoutSource(string key, out string text)
{
    if (mergedDictionary[CurrentLanguageCode].TryGetValue(key, out text) && !string.IsNullOrEmpty(text)) return true;
    if (mergedDictionary[DefaultLanguageCode].TryGetValue(key, out text) && !string.IsNullOrEmpty(text)) return true;
    text = null;
    return false;
}
```

- [ ] **Step 5: skit titleとresolverを両開始経路のStoryContextへ登録する**

Skit titleの正本はAddressable asset basenameであり、runtimeではロード済み `TextAsset.name` を必ず `SkitTitle.FromAssetName` へ渡す。`SkitManager` と `BackgroundSkitManager` は得た同一titleでcommand実行前にresolverを `PrepareAsync` し、`ISkitLocalizationResolver` と `SkitExecutionIdentity` をbuilderへ登録する。JSON `meta.title` はruntime keyへ使わない。resolverの購読はStoryContext終了時にDisposeする。

- [ ] **Step 6: text/background/selection/overrideCharacterNameを表示直前に解決する**

`TextCommand` と `BackgroundSkitTextCommand` は `CommandId` と `body` を使って本文を解決し、override時は同じCommandIdの `overrideCharacterName` を解決する。通常話者名はresolverが `characterId` からmasterを引き、必須 `CharacterGuid` で `CharacterName` キーを作る。`SelectionCommand` は `Option1Tag`〜`Option3Tag` を各field名で解決してからuGUI/Web双方のchoiceへ同じ表示文字列を渡す。音声clip検索は既存JSON原文 `Body` を維持し、翻訳文でvoice mappingを変えない。

- [ ] **Step 7: resolver優先順位と接頭辞境界をテストする**

テストfixtureに各段を別値で入れ、次の5ケースを固定する: ①mod対象が非空ならmod対象、②mod対象が空ならSkit対象、③対象2段が空ならmod英語、④mod英語も空ならSkit英語、⑤4辞書すべて空ならJSON原文。各ケースで空文字そのものを返さないこともassertする。加えて `command.*` / `master.*` がloader出力に含まれないこと、空の `skit.*` がloader出力に含まれないこと、characterGuid欠落を補完しないこと、言語reload後の次のResolveが新scopeを見ることを検証する。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SkitLocalizationResolverTest"`
Expected: 全ケースPASS

- [ ] **Step 8: webuiコメント・コンパイル・コミット**

Skit表示2コンポーネントのコメントは「Unity所有だから」ではなく「Unity側resolverで解決済みの表示文字列をpushするため `t()` を通さない」へ更新する。

```bash
uloop compile --project-path ./moorestech_client
git add VanillaSchema/characters.yml moorestech_client/ && git commit -m "feat: Skit専用辞書を動的ロードし全表示fieldを解決"
cd /Users/katsumi/moorestech_master && git add -- '**/master/characters.json' && git commit -m "feat: 全character masterへ必須characterGuidを追加"
```

---

### Task 7: ビルドメニューカテゴリのGuid付与とキー化

**Files:**
- Modify: `VanillaSchema/buildMenu.yml`（categoriesへ `categoryGuid`、subCategoriesへ `subCategoryGuid` を必須追加。edit-schemaスキル参照）
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/buildMenu.json`（全カテゴリへGuid付与）
- Modify: `BuildMenuEntryDtoFactory.cs:37-47`（Category/SubCategoryをGuid化）＋webui buildMenu表示

- [ ] **Step 1: スキーマへGuidを追加する**

edit-schemaスキルの手順に従い `buildMenu.yml` の categories 要素へ `categoryGuid`、subCategories要素へ `subCategoryGuid` を `type: uuid / autoGenerated: true` で追加する（`research.yml` の `researchNodeGuid` 定義と同形式）。どちらもoptionalにせず、ローダー補完も作らない。

- [ ] **Step 2: 実データへGuidを付与し、SchemaWatcher再コンパイル→検証**

`buildMenu.json` の全カテゴリ/サブカテゴリへuuidを採番して追加。
Run: `uloop compile --project-path ./moorestech_client && uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: MooresmasterLoaderException が出ない

- [ ] **Step 3: DTOと表示のキー化**

`BuildMenuEntryDtoFactory.CreateCategoryDtos` をカテゴリ `Name` → `CategoryGuid`、サブカテゴリ `Name` → `SubCategoryGuid` へ変更する。entryの `Label` はblockとblueprintCopyで削除し、webはblockを `EntryType`+`EntryKey`（Guid）、blueprintCopyをtyped UI keyで解決する。trainCar/connectToolは専用stable key/sourceが未定（trainCar masterにnameなし）のため `Label` を維持し、ユーザー命名blueprintも `Label` を維持する。webuiはカテゴリを `t(buildMenuCategoryNameKey(guid))`、サブカテゴリを `t(buildMenuSubCategoryNameKey(guid))` で表示する。`MasterSourceTextCollector` へ両方の原文を投入する。

- [ ] **Step 4: 検証・コミット**

Run: `uloop compile --project-path ./moorestech_client && cd moorestech_web/webui && npx tsc -b && npm run test:e2e`

```bash
git add -A && git commit -m "feat: ビルドメニューカテゴリをGuid化し文言を辞書解決へ"
```

---

### Task 8: v8 modサンプル辞書と結合確認

**Files:**
- Create: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`
- Modify: `moorestech_client/Assets/AddressableResources/Skit/i18n/english.json`
- Modify: `moorestech_client/Assets/AddressableResources/Skit/i18n/japanese.json`

- [ ] **Step 1: サンプル辞書を作る**

主要アイテム/ブロック数件に加え、同じSkit keyをmod CSVとSkit専用JSONの双方へ意図的に配置して優先順位を検査する。Guidは `items.json`/`blocks.json` の実値を使う:

```csv
key,Source,english,japanese
item.<小石の実Guid>.name,Pebble,Pebble,小石
item.<原木の実Guid>.name,Log,Log,原木
block.<風力掘削機の実Guid>.name,Wind Drill,Wind Drill,風力掘削機
skit.100_start_game.1.body,...Report log started,MOD ENGLISH,MOD JAPANESE
skit.100_start_game.2.body,Position report,MOD ENGLISH 2,
skit.100_start_game.3.body,Crew report,MOD ENGLISH 3,
skit.100_start_game.4.body,Warp report,,
```

既存 `Skit/i18n/*.json` の `command.*` / `master.*` は一切削除・改名せず、`translations` へ次を追加する:

```json
"skit.100_start_game.1.body": "SKIT VALUE 1",
"skit.100_start_game.2.body": "SKIT VALUE 2",
"skit.100_start_game.3.body": "",
"skit.100_start_game.4.body": "SKIT VALUE 4",
"skit.100_start_game.1.overrideCharacterName": "SKIT SPEAKER VALUE"
```

english/japaneseで値を変える。japaneseはcommand 2だけSkit値を持ち、command 3/4は空、englishはcommand 3/4のSkit値を持つfixtureにして5段の境界を作る。Task 6のselection command test fixtureには `Option1Tag`〜`Option3Tag`、背景skitには `skit.200_star_background.1.body` も追加して各field経路を通す。

- [ ] **Step 2: PlayModeで結合確認する**

unity-playmode-recorded-playtestスキルの手順でPlayMode起動:
1. command 1: mod/Skit双方が非空で `MOD JAPANESE`（mod対象言語優先）
2. command 2: mod日本語が空でSkit日本語が非空のためSkit日本語
3. command 3: mod/Skit日本語が空でmod英語が非空のため `MOD ENGLISH 3`
4. command 4: 対象言語2段とmod英語が空でSkit英語が非空のためSkit英語
5. command 6: mod CSV/Skit専用辞書の4段すべてに未登録の実在text commandなのでskit JSON原文
6. どのケースも空文字を表示せず、background本文、selection表示選択肢、overrideCharacterNameも同じ規則で翻訳される
7. `command.*` / `master.*` がゲーム辞書へ漏れず、他画面でも対象言語→english→source→`[!key]` が維持される

- [ ] **Step 3: コミット（moorestech_master側も）**

```bash
cd /Users/katsumi/moorestech_master && git add server_v8/mods/moorestechAlphaMod_8/localization/ && git commit -m "feat: v8 modサンプルローカライズ辞書"
cd /Users/katsumi/moorestech && git add moorestech_client/Assets/AddressableResources/Skit/i18n/ && git commit -m "feat: CommandForge辞書へゲーム台詞キーを追加"
```

---

### Task 9: 既存CommandForgeEditor辞書の保持・完全性・動的ロード結合確認

**Files:**
- Verify: `moorestech_client/Assets/AddressableResources/Skit/i18n/english.json`
- Verify: `moorestech_client/Assets/AddressableResources/Skit/i18n/japanese.json`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/**/SkitLocalizationDictionaryCompletenessTest.cs`

- [ ] **Step 1: CommandForgeEditor用キーの保持を検査する**

english/japanese両ファイルが `locale` / `name` / flatな `translations` を維持し、既存 `command.*` と `master.*` がTask 8前のkey集合から欠落していないことをgit diffとfixture testで検査する。ディレクトリ・JSON・meta・Addressable entryは削除しない。

- [ ] **Step 2: ゲーム台詞キー完全性を検査する**

全skit JSONを走査し、ファイルbasenameを `SkitTitle.FromAssetName` へ渡した結果で `skit.<title>.<id>.<schema field>` を組み立てる。JSON `meta.title` はキーへ使わず、basenameと一致することだけをassertする。実測済みの `100_start_game` / `200_star_background` / `sample_short` は両者一致。`text.body`、`backgroundSkitText.body`、存在するselectionの各OptionTag、override有効行の`overrideCharacterName`について、翻訳対象サンプルがenglish/japanese両方に存在すること、未知field名（`text`/`speaker`/`overideCharacterName`）が無いことを検査する。未翻訳行はJSON原文へ戻る設計なので全行翻訳必須にはしない。

- [ ] **Step 3: Addressables動的ロードを結合検査する**

`Vanilla/Skit/i18n/english` と `Vanilla/Skit/i18n/japanese` の2addressが実在することをAddressable settingsとPlayModeで確認する。english開始ではenglishを1回だけ、日本語開始ではjapanese+englishだけをloadし、全skit JSONや未選択言語をloadしないことをloader fakeの呼び出し記録で検査する。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SkitLocalizationDictionaryCompletenessTest|SkitLocalizationResolverTest"`
Expected: 全テストPASS、ロードaddressは選択言語+englishだけ

- [ ] **Step 4: コンパイル・コミット**

```bash
uloop compile --project-path ./moorestech_client
git add moorestech_client/Assets/Scripts/Client.Tests/ && git commit -m "test: Skit専用辞書の保持と動的ロードを固定"
```

---

### Task 10: 最終レビュー（省略不可）

- [ ] **Step 1: moores-code-reviewスキルで全ブランチレビューを実行する**

必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。

---

## 判断記録（ADR）

- 対応spec: [docs/superpowers/specs/2026-07-29-localization-foundation-design.md](../specs/2026-07-29-localization-foundation-design.md)
- **Skit/i18nはCommandForgeEditor正式辞書として保持する** — `command.*` / `master.*` を維持し、`skit.*` を追加できる正本へ拡張する。削除タスクは置かない。出所: ユーザー裁定 2026-07-29
- **Skit辞書は開始時に対象言語+englishだけをAddressables動的ロードする** — `skit.` だけをmod合成辞書copyへ欠けたキーとして追加し、mod対象→Skit対象→mod英語→Skit英語→JSON原文を保証する。出所: ユーザー裁定 2026-07-29
- **Skitの全表示fieldをCommandId由来キーで扱う** — schemaの `body` / `Option1Tag`〜`Option3Tag` / `overrideCharacterName` をC#/JSONで固定し、本文・背景・選択肢・上書き話者名を同じresolverへ通す。出所: ユーザー裁定 2026-07-29
- **characterGuidを必須追加しcharacterIdは操作IDとして維持する** — 表示名だけGuid導出し、optional・欠損フォールバックは設けず全characters JSONを一括更新する。出所: ユーザー裁定 2026-07-29
- **buildMenuカテゴリ/サブカテゴリへ別々の必須Guidを追加する** — 名前をIDに使わずrequired追加＋全JSON一括更新する。出所: ユーザー裁定 2026-07-29
- **CSV runtimeはPlan1の共通DLLを参照する** — generator/runtimeのparser・行モデル・例外は `mooresmaster.LocalizationCsv.dll` の1実装だけを使う。出所: ユーザー裁定 2026-07-29
- **空翻訳は欠落として次段へ進む** — parserは空fieldを保持するが、mod merger・Localize・Skit loader/resolverはいずれも空文字を登録/返却しない。出所: Task 0 review finding 2026-07-29
- **Skit title正本はAddressable asset basename / TextAsset.name** — runtime/testとも `SkitTitle.FromAssetName` を使い、JSON `meta.title` はbasename一致検査だけに使う。出所: Task 0 review finding 2026-07-29
- **ItemMasterDtoはItemId（揮発）+ItemGuidの併載** — 表示中の軽量参照はItemIdのまま、ローカライズキーだけGuidを使う。出所: agent前提（既存契約の最小変更）

## 配置と前例

| 項目 | 配置先 | 前例（パス） |
|---|---|---|
| ModLocalizationMerger / MasterSourceTextCollector / ContentLocalizationKeys | Client.Localization | `Localize.cs`（合成辞書の正本はクライアントLocalize — ユーザー裁定） |
| ISkitLocalizationResolver / SkitExecutionIdentity | Client.Skit（汎用interface/contextのみ） | `StoryContext` + VContainer service登録。汎用層へLocalize/Addressablesを持ち込まない |
| SkitTitle.FromAssetName | Client.Skit/Localization | Addressable asset basenameをruntime/test共通の純粋導出へ一本化。meta.titleは検証のみ |
| SkitLocalizationDictionaryLoader / SkitLocalizationResolver | Client.Game/Skit/Localization | `SkitManager.PreProcess` / `BackgroundSkitManager.GetStoryContext` の具体service登録前例 |
| ModsResourceからのCSV列挙 | Mod.Loader公開APIの利用（必要なら最小の公開追加） | `Mod.Config/ModJsonStringLoader.cs:22-30`（mod内相対パスのglob前例） |
| DTOのGuid化 | Client.WebUiHost各Endpoint/Topic | `MachineRecipesTopic.cs`（BlockGuidを既に配信している前例） |
| Web側名前解決hook | src/shared/i18n | `i18nStore.ts`（辞書解決の集約点） |
| characterGuid / buildMenuカテゴリ・サブカテゴリGuid | VanillaSchema/characters.yml / buildMenu.yml | `research.yml` の `researchNodeGuid`（uuid autoGenerated前例）。必須化+全JSON更新 |

データフロー地図（Phase 1.5）: `マスタ/mod CSV →（起動時合成）→ [合成辞書 in Localize] →（/api/i18n・GetContent）→ 表示`。Skitは `対象言語+english Addressables → [Client.Game resolverのskit実行scope] → Client.Skit command表示 → uGUI/Web push`。汎用Client.Skitはinterfaceの読み手だけで、具体辞書の書き手はClient.Gameに限定する。

機能パリティ（Phase 2.5 死活表）:

| 操作 | 計画後 | 根拠 |
|---|---|---|
| インベントリ/レシピ/研究のアイテム名ツールチップ | 生きる | Guid→辞書→source原文チェーンで現表示と同一文字列 |
| ブロックインベントリのヘッダ名 | 生きる | blockGuid化＋辞書解決。日本語表示はsource経由で不変 |
| ビルドメニューのカテゴリタブ・エントリ名 | 生きる | カテゴリGuid化・エントリはEntryKey(Guid)解決。blueprint命名はLabel維持 |
| skit再生（本文・背景本文・話者・上書き話者・選択肢） | 生きる | 対象言語+english動的辞書をresolverで解決し、未翻訳時は各JSON原文 |
| CommandForgeEditorのcommand/master表示辞書 | 生きる | 既存Skit/i18nを削除せず、ゲームはskit.*だけをfilterして取り込む |
| skit中の言語切替 | 次の行から反映 | resolver reload後にscopeをatomic swap。表示済み同一行の即時再pushは非目標でQA判定 |
| Unity側uGUIブロックインベントリ表示 | 生きる | GetContent置換（表示文字列は不変） |
| 言語切替時のアイテム名即時反映 | 生きる（新規） | Web側解決のため `localization.current` →辞書再fetch→再描画で完結 |
