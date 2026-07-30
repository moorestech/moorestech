---
spec: docs/superpowers/specs/2026-07-29-localization-foundation-design.md
---

# Localization Mod Dictionary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** mod同梱ローカライズCSVと `<type>.<guid>.<field>` 導出キーを実装し、ホスト側のマスタ名解決・payload同梱を全廃してWeb側辞書解決へ統一する（item/block・研究/チャレンジ・skit・ビルドメニュー）。

**Architecture:** Plan1のバニラ基盤（埋め込み辞書・`Localize`・source擬似ロケール・TS型付きキー）の上に、①mod `localization/localization.csv` の合成、②MasterHolder原文のsource擬似ロケール投入、③`ContentLocalizationKeys`（C#）/`contentKeys.ts`（TS）による導出キー、④DTO/TopicのName削除とGuid化を積む。未翻訳チェーンは 対象言語→english→source（原文）→`[!key]`（ユーザー裁定 2026-07-29「英語→nameの原文」）。skitのみホスト解決の例外（判断記録参照）。

**Tech Stack:** C# (Unity asmdef) / React + zustand / uloop / NUnit（クライアントプロジェクト経由でサーバーテスト実行）

## Global Constraints

- Plan1（`docs/superpowers/plans/2026-07-29-localization-vanilla-foundation.md`）完了が前提
- partial禁止・`Func<>`禁止・try-catch原則禁止（外部境界のみ・根拠コメント必須）・UniRx・200行/ファイル・日本語→英語2行コメント（AGENTS.md）
- 導出キー規約: `<type>.<guid>.<field>`。Guidは `ToString("D")` 小文字。type/fieldはlowerCamel
- キーにmodIdを含めない（ユーザー裁定 2026-07-29）
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

VanillaSchema/buildMenu.yml              ← カテゴリ/サブカテゴリへGuid追加（Task 7）
../moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv ← 新設サンプル
```

---

### Task 1: mod辞書の合成とsource原文投入

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Localization/ModLocalizationMerger.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Localization/MasterSourceTextCollector.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/Localize.cs`

**Interfaces:**
- Consumes: Plan1の `mergedDictionary` 構造・`Mod.Loader.ModsResource`（`ExtractedPath` を各modが持つ。zip modは展開先ディレクトリ）
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
            /* Plan1のLocalizationCsvParserと同仕様のパースを行い、
               言語列ごとに mergedDictionary[code][key] = text、Source列は mergedDictionary["source"][key] = source */
        }
    }
}
```

（注: パーサーはgenerator DLL内の `LocalizationCsvParser` をランタイム参照できない〔RoslynAnalyzer扱いのDLLは実行時参照不可〕ため、`MergeCsv` に同仕様の最小パーサーを実装する。`ModsResource` のAPI実名（`SortedMods`/`ExtractedPath`）は `Mod.Loader/ModsResource.cs` を読んで実名に合わせる — zip modの展開先を返すプロパティが無い場合は `ModsResource` に追加する）

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
            foreach (var pair in sourceTexts) mergedDictionary[SourcePseudoLocale][pair.Key] = pair.Value;
            // 辞書更新をWeb/UIへ通知する（既存の言語変更通知と同じ購読で再取得される）
            // Notify the Web/UI so subscribers refetch, same as a language change
            _onLanguageChangedSubject.OnNext(Unit.Default);
        }

        public static string GetContent(string derivedKey)
        {
            if (mergedDictionary[CurrentLanguageCode].TryGetValue(derivedKey, out var value)) return value;
            if (mergedDictionary[DefaultLanguageCode].TryGetValue(derivedKey, out var english)) return english;
            if (mergedDictionary[SourcePseudoLocale].TryGetValue(derivedKey, out var source)) return source;
            return $"[!{derivedKey}]";
        }
```

Step 1で特定した起動フローから `Localize.MergeGameDictionaries(modsResource)` を呼ぶ（asmdef: Client.Localization に `Mod.Loader`・`Core.Master` 参照を追加）。

- [ ] **Step 5: テストを書く（クライアント経由でNUnit実行）**

`MergeCsv` と `GetContent` のチェーンをユニットテスト化（creating-server-testsスキルの慣習に従い、テスト配置はClient.Localizationのテスト用asmdefが無ければ `Client.Tests` 配下の前例に合わせる）:

```csharp
    [Test]
    public void Modの辞書が後勝ちで合成されsourceフォールバックが機能する()
    {
        // vanilla: english列のみ存在するキー / mod: japaneseを上書き / source: 原文のみのキー
        // 3経路それぞれで GetContent が正しい段を返すことを検証する
    }
```

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
- Produces（C#）: `public static class ContentLocalizationKeys` — `ItemName(Guid)`→`item.<guid>.name` / `BlockName(Guid)` / `ResearchNodeName(Guid)` / `ResearchNodeDescription(Guid)` / `ChallengeTitle(Guid)` / `ChallengeSummary(Guid)` / `ChallengeCategoryName(Guid)` / `CharacterName(Guid)` / `SkitLineText(string skitTitle, int commandId)`→`skit.<title>.<id>.text` / `BuildMenuCategoryName(Guid)`
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
        public static string CharacterName(Guid guid) => $"character.{guid:D}.name";
        public static string BuildMenuCategoryName(Guid guid) => $"buildMenuCategory.{guid:D}.name";
        // skitはGuidを持たないためファイルtitle+コマンドidで識別する（spec裁定済みのGuid例外）
        // Skits have no Guid; identified by file title + command id (adjudicated exception)
        public static string SkitLineText(string skitTitle, int commandId) => $"skit.{skitTitle}.{commandId}.text";
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
  | `buildMenuCategory.${string}.name`;
// skit行キーはホスト解決のみでWebから構築しない（Task 6参照）
// Skit line keys resolve host-side only and are never built from the Web (see Task 6)

export const itemNameKey = (guid: string): ContentLocalizationKey => `item.${guid}.name`;
export const blockNameKey = (guid: string): ContentLocalizationKey => `block.${guid}.name`;
export const researchNodeNameKey = (guid: string): ContentLocalizationKey => `research.${guid}.name`;
export const researchNodeDescriptionKey = (guid: string): ContentLocalizationKey => `research.${guid}.description`;
export const challengeTitleKey = (guid: string): ContentLocalizationKey => `challenge.${guid}.title`;
export const challengeSummaryKey = (guid: string): ContentLocalizationKey => `challenge.${guid}.summary`;
export const buildMenuCategoryNameKey = (guid: string): ContentLocalizationKey => `buildMenuCategory.${guid}.name`;
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

### Task 6: skit台詞のローカライズ（ホスト解決の例外経路）

**Files:**
- Modify: skit再生でテキストをTopicへpushしている箇所（`Client.Skit` → WebUiHostのskit topic。`grep -rn "speakerName\|SkitPresentationData" moorestech_client/Assets/Scripts --include="*.cs"` で実測）
- Modify: `moorestech_web/webui/src/features/skit/SkitPresentation.tsx:106` / `controls/SkitChoiceList.tsx:24` のコメント更新

**Interfaces:**
- Produces: skit行テキストはホストが `Localize.GetContent(ContentLocalizationKeys.SkitLineText(skitTitle, commandId))` で解決してpush（sourceはskit JSONの `body` をインラインフォールバック: 辞書に無ければ原文をそのまま流す）。話者名は `characterId`→characters master のGuid→`CharacterName` キーで解決

- [ ] **Step 1: skit再生のテキスト供給点を特定する**

Run: `grep -rn "body\|Text" moorestech_client/Assets/Scripts/Client.Skit --include="*.cs" | grep -i "command\|push\|topic" | head -20`
テキストコマンド実行→表示データ化（`SkitPresentationData.cs`）の経路で、表示直前の1点を特定する。

- [ ] **Step 2: ホスト解決を差し込む**

特定した1点で:

```csharp
            // skit行はWeb辞書に原文が無いためホスト側で解決する（例外の根拠は判断記録）
            // Skit lines resolve host-side because their source text is not in the web dictionary (see ADR)
            var key = ContentLocalizationKeys.SkitLineText(skitTitle, command.Id);
            var resolved = Localize.TryGetContentWithoutSource(key, out var text) ? text : command.Body;
```

`Localize` へ `TryGetContentWithoutSource(string key, out string text)`（対象言語→englishのみ。無ければfalse）を追加し、フォールバック原文は手元の `command.Body` を使う（skit原文をsource擬似ロケールへ事前投入しない — Addressables全ロードを避ける）。話者名は `overideCharacterName` があればそれを優先し、なければ `CharacterName(guid)` を同様に解決。

- [ ] **Step 3: webuiのコメントを更新する**

`SkitPresentation.tsx:106` の「Unity所有の表示データのためt()を通さない」コメントを「ホスト側でローカライズ解決済みのためt()を通さない」へ更新（構造は不変）。

- [ ] **Step 4: mod辞書でskit行を訳せることを確認する**

Task 8のサンプル辞書に `skit.100_start_game.1.text` の行を入れ、english切替で台詞が変わることをPlayModeで確認。

- [ ] **Step 5: コミット**

```bash
git add -A && git commit -m "feat: skit台詞をローカライズ解決（ホスト解決の例外経路）"
```

---

### Task 7: ビルドメニューカテゴリのGuid付与とキー化

**Files:**
- Modify: `VanillaSchema/buildMenu.yml`（categories/subCategoriesへ `categoryGuid` 追加。edit-schemaスキル参照）
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/buildMenu.json`（全カテゴリへGuid付与）
- Modify: `BuildMenuEntryDtoFactory.cs:37-47`（Category/SubCategoryをGuid化）＋webui buildMenu表示

- [ ] **Step 1: スキーマへGuidを追加する**

edit-schemaスキルの手順に従い `buildMenu.yml` の categories 要素と subCategories 要素へ `- key: categoryGuid / type: uuid / autoGenerated: true` を追加（`research.yml` の `researchNodeGuid` 定義と同形式）。optional にしない（AGENTS.md: フォールバック禁止・全JSON一括更新）。

- [ ] **Step 2: 実データへGuidを付与し、SchemaWatcher再コンパイル→検証**

`buildMenu.json` の全カテゴリ/サブカテゴリへuuidを採番して追加。
Run: `uloop compile --project-path ./moorestech_client && uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: MooresmasterLoaderException が出ない

- [ ] **Step 3: DTOと表示のキー化**

`BuildMenuEntryDtoFactory.CreateCategoryDtos` を `Name` → `CategoryGuid` へ（entryの `Label` はblock/trainCar系なら削除し、webは `EntryType`+`EntryKey`（Guid）から `blockNameKey`/`trainCar…` で解決。blueprint等ユーザー命名エントリのみ `Label` 維持）。webuiのカテゴリタブは `t(buildMenuCategoryNameKey(guid))`。`MasterSourceTextCollector` へカテゴリ原文の投入を追加。

- [ ] **Step 4: 検証・コミット**

Run: `uloop compile --project-path ./moorestech_client && cd moorestech_web/webui && npx tsc -b && npm run test:e2e`

```bash
git add -A && git commit -m "feat: ビルドメニューカテゴリをGuid化し文言を辞書解決へ"
```

---

### Task 8: v8 modサンプル辞書と結合確認

**Files:**
- Create: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`

- [ ] **Step 1: サンプル辞書を作る**

主要アイテム/ブロック数件＋skit1行のenglish実訳（Guidは `items.json`/`blocks.json` の実値を使う）:

```csv
key,Source,english,japanese
item.<小石の実Guid>.name,Pebble,Pebble,小石
item.<原木の実Guid>.name,Log,Log,原木
block.<風力掘削機の実Guid>.name,Wind Drill,Wind Drill,風力掘削機
skit.100_start_game.1.text,...Report log started,...Report log started,...レポート記録開始
```

- [ ] **Step 2: PlayModeで結合確認する**

unity-playmode-recorded-playtestスキルの手順でPlayMode起動:
1. 日本語: 全アイテム名が原文（source経由）で表示される・`[!` が出ない
2. `Localize.SetLanguage("english")`: サンプル辞書のあるアイテムだけ英語名、他はsource原文
3. インベントリ・ビルドメニュー・研究ツリー・ブロックインベントリ・skitで確認

- [ ] **Step 3: コミット（moorestech_master側も）**

```bash
cd ../moorestech_master && git add server_v8/mods/moorestechAlphaMod_8/localization/ && git commit -m "feat: v8 modサンプルローカライズ辞書"
cd ../moorestech && git add -A && git commit -m "chore: mod辞書結合確認の調整" || true
```

---

### Task 9: 既存 Skit/i18n の処分（ユーザー裁定後）

**Files:**
- Delete: `moorestech_client/Assets/AddressableResources/Skit/i18n/`（裁定が「仮置き・廃止」の場合のみ）

- [ ] **Step 1: 裁定を確認する**

specの判断記録「skit導出キーとSkit/i18n吸収廃止」の裁定状態を確認。**未裁定のままこのタスクを実行しない**（実装前にユーザーへ確認する — 機能パリティ死活表の裁定ゲート）。

- [ ] **Step 2: 廃止裁定なら削除し、commandForgeEditor設定の参照が無いことを確認する**

Run: `grep -rn "i18n" moorestech_client/Assets/AddressableResources/Skit/commandForgeEditor.config.yaml moorestech_client/Assets/AddressableResources/Skit/commands.yaml`
参照があればエディタ設定側の除去も同時に行う。削除はUnity上で行い.meta込みでコミット。

```bash
git add -A && git commit -m "chore: 旧Skit i18n辞書を廃止（新基盤へ吸収）"
```

---

### Task 10: 最終レビュー（省略不可）

- [ ] **Step 1: moores-code-reviewスキルで全ブランチレビューを実行する**

必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。

---

## 判断記録（ADR）

- 対応spec: [docs/superpowers/specs/2026-07-29-localization-foundation-design.md](../specs/2026-07-29-localization-foundation-design.md)
- **skitのみホスト解決の例外** — skit原文はAddressables内JSONでWeb辞書のsourceに事前投入できず（全skit事前ロードのコスト）、行は1行ずつpushされる一過性payloadのため「言語切替時の再push漏れ」問題が構造的に無い。mod辞書の訳はホストの `TryGetContentWithoutSource` で効く。出所: agent前提（拒否権つき。ADR 0006「Web側解決統一」の限定例外）
- **skit原文はsource擬似ロケールへ投入しない** — フォールバック原文は再生時に手元の `command.Body` を使う。出所: agent前提（Addressables全ロード回避）
- **buildMenuカテゴリへGuid追加のスキーマ変更** — カテゴリは表示文言を持つが安定IDが無く、名前をIDに使うのは改名で翻訳が切れるため。required追加＋全JSON一括更新（AGENTS.md「変更の波及を恐れない」）。出所: agent前提（拒否権つき）
- **ランタイムはgenerator DLLのパーサーを再利用しない** — RoslynAnalyzer扱いのDLLは実行時参照不可のため、`ModLocalizationMerger.MergeCsv` に同仕様パーサーを持つ（正本仕様はPlan1のC#パーサーテストが固定）。出所: agent前提（Unityの機構上の制約）
- **ItemMasterDtoはItemId（揮発）+ItemGuidの併載** — 表示中の軽量参照はItemIdのまま、ローカライズキーだけGuidを使う。出所: agent前提（既存契約の最小変更）

## 配置と前例

| 項目 | 配置先 | 前例（パス） |
|---|---|---|
| ModLocalizationMerger / MasterSourceTextCollector / ContentLocalizationKeys | Client.Localization | `Localize.cs`（合成辞書の正本はクライアントLocalize — ユーザー裁定） |
| ModsResourceからのCSV列挙 | Mod.Loader公開APIの利用（必要なら最小の公開追加） | `Mod.Config/ModJsonStringLoader.cs:22-30`（mod内相対パスのglob前例） |
| DTOのGuid化 | Client.WebUiHost各Endpoint/Topic | `MachineRecipesTopic.cs`（BlockGuidを既に配信している前例） |
| Web側名前解決hook | src/shared/i18n | `i18nStore.ts`（辞書解決の集約点） |
| buildMenuカテゴリGuid | VanillaSchema/buildMenu.yml | `research.yml` の `researchNodeGuid`（uuid autoGenerated前例） |

データフロー地図（Phase 1.5）: `マスタ/mod CSV →（起動時合成）→ [合成辞書 in Localize] →（/api/i18n・GetContent）→ 表示`。本planの新規コンポーネントは全て「書き手（起動時合成）」か「読み手（表示解決）」であり、既存フローへの交差点（bool戻り・第2書き込み経路）は無い。skitホスト解決も読み手の位置。

機能パリティ（Phase 2.5 死活表）:

| 操作 | 計画後 | 根拠 |
|---|---|---|
| インベントリ/レシピ/研究のアイテム名ツールチップ | 生きる | Guid→辞書→source原文チェーンで現表示と同一文字列 |
| ブロックインベントリのヘッダ名 | 生きる | blockGuid化＋辞書解決。日本語表示はsource経由で不変 |
| ビルドメニューのカテゴリタブ・エントリ名 | 生きる | カテゴリGuid化・エントリはEntryKey(Guid)解決。blueprint命名はLabel維持 |
| skit再生（本文・話者・選択肢） | 生きる | ホスト解決で従来と同経路。未翻訳時は原文 |
| Unity側uGUIブロックインベントリ表示 | 生きる | GetContent置換（表示文字列は不変） |
| 言語切替時のアイテム名即時反映 | 生きる（新規） | Web側解決のため `localization.current` →辞書再fetch→再描画で完結 |
