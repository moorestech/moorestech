# 採掘tooltipへの取得アイテム名前置 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 手掘りのカーソルtooltipを `{取得アイテム名} : {動作文}` 形式にし、カーソルを合わせただけで何が手に入るかを分かるようにする。

**Architecture:** `IMiningTargetObject` は取得物の `Guid` 列（マスタ由来データ）だけを公開する。ローカライズと連結は `MiningControllerContext` がフォーカス実体の変化時に1回だけ行い、結果文字列を保持する。`MiningFocusState` は保持された文字列の有無で「名前つきキー」と「既存キー」を出し分けるだけにする。辞書には名前つきバリアントのキーを4件新設する。

**Tech Stack:** Unity C#（`moorestech_client`）／ UniRx ／ Mooresmaster SourceGenerator（`Localization/localization.csv` → `LocalizationKeys`）／ NUnit（EditMode）／ vitest（`moorestech_web/webui`）

## Requirements

設計対話（2026-08-25）で確定した要件。受け入れ基準つき。

- R1. tooltipは `{取得アイテム名} : {動作文}` の形になる。区切りは半角スペース＋コロン＋半角スペース（` : `）。
  - 受け入れ基準: 小石を落とす岩にカーソルを合わせると「小石 : 左クリック長押しで採掘」が出る。
- R2. 動作語は「採掘」に統一する。進捗掘りは「左クリック長押しで採掘」、`InstantPickUp` は「左クリックで採掘」。
  - 受け入れ基準: 名前つき文言に「取得」の語が現れない。
- R3. 名前の出所はドロップアイテム名。mapObject は `MapObjectMasterElement.EarnItems[].ItemGuid`、鉱脈は `ItemVeinParam.ItemGuid`。`mapObjectName` / `veinName` は使わない。
  - 受け入れ基準: `MapObjectGameObject` / `OutcropGameObject` のいずれも `MapObjectName` / `VeinName` を参照しない。
- R4. `earnItems` が空の対象（ブッシュ・サボテン・草・花）は名前欄ごと出さず、現行の文言（`ui.tooltip.holdToGet` =「左クリック長押しで取得する」）のままにする。
  - 受け入れ基準: 取得物ゼロの対象では `TextKey` が既存キーのまま・`TextParams` が空。
- R5. 採掘できない状態（`ToolMismatch` / `HandMiningNotAllowed`）でも名前を前置する。
  - 受け入れ基準: 「鉄鉱石 : このアイテムが必要です: 鉄のツルハシ」「タングステン鉱石 : 手掘りできません」の形になる。
- R6. 液体鉱脈（`FluidVeinParam`。水・原油）は名前を出さず、理由文だけを現行どおり表示する。tooltip自体は出す。
  - 受け入れ基準: `OutcropGameObject` が液体鉱脈で空の取得物列を返し、`TextKey` が `ui.tooltip.cannotHandMine` のままになる。
- R7. `earnItems` が複数のときは全件を `", "` 区切りで並べる。
  - 受け入れ基準: 取得物2件の対象で「小石, 原木 : …」の形になる。
- R8. 表示名の解決は毎フレーム行わない。フォーカス実体が変わった時と言語が切り替わった時だけ作り直す。
  - 受け入れ基準: `MiningFocusState.GetNextUpdate` の中に `Localize.GetContent` / `string.Join` による取得物名の組み立てが無い。
- R9. Web UI は辞書キー＋`{p0}`パラメータで追従する。生成物 `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` を再生成する。
  - 受け入れ基準: `localizationKeysFreshness` テストが通る。

### やらないこと（スコープ境界）

- `earnItems` が空である master データ自体の是非（掘れるのに何も出ない対象の存在）には触れない。データは1行も変更しない。
- ブロック破壊（`DeleteObjectService` 系）のtooltipには触れない。対象は `IMiningTargetObject` の2実装のみ。
- `MouseCursorTooltip` のuGUIビュー・レイアウト・Web UI側の見た目には触れない（辞書キーの追加のみ）。
- 露頭のフォーカス演出（`OutcropGameObject.SetFocused` の空実装）は今回も空のままにする。

## Global Constraints

- 対象リポジトリ: `moorestech`。ブランチは master 起点の `feature/mining-tooltip-earn-item-name`。作業は `moores-wt new feature/mining-tooltip-earn-item-name` で作った使い捨てworktreeで行う（メインワークツリーでのブランチ操作はhookが物理拒否する）。
- コメントは日本語1行 → 英語1行のセットを約3〜10行ごとに入れる。各言語とも1行に収める。自明なコメントは書かない。
- `partial` 禁止。`Func<>` 禁止。`try-catch` 禁止（外部境界のみ例外）。デフォルト引数禁止。
- 1ファイル200行以下。
- 単純なgetter/setterプロパティは禁止。`{ get; private set; }` は許容。
- イベント発火に `Action` を使わない。UniRx を使う。
- 経過時間の計測に実時間APIを使わない（本planでは時間計測を扱わない）。
- `.meta` ファイルを手で作らない。既存ファイルの変更・新規 `.cs` の追加のみで、Prefab・シーン・ScriptableObject は一切触らない。
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する（省略不可）。
- 文言の正は `Localization/localization.csv`（リポジトリ側）。`moorestech_master` 側の mod csv は今回変更しない。
- 新設する辞書キーと文言（逐語・変更禁止）:

  | key | Source / english | japanese |
  | --- | --- | --- |
  | `ui.tooltip.namedMineHold` | `{p0} : Hold left-click to mine` | `{p0} : 左クリック長押しで採掘` |
  | `ui.tooltip.namedMineClick` | `{p0} : Left-click to mine` | `{p0} : 左クリックで採掘` |
  | `ui.tooltip.namedRequiredItems` | `{p0} : Requires: {p1}` | `{p0} : このアイテムが必要です: {p1}` |
  | `ui.tooltip.namedCannotHandMine` | `{p0} : Cannot be mined by hand` | `{p0} : 手掘りできません` |

---

## File Structure

| ファイル | 変更種別 | 責務 |
| --- | --- | --- |
| `Localization/localization.csv` | Modify（末尾へ4行追記） | 名前つきtooltip文言の正 |
| `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` | Modify（スクリプト再生成） | Web UI 側のキーカタログ |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/IMiningTargetObject.cs` | Modify | 採掘対象が取得物の `Guid` 列を公開する口 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs` | Modify | `EarnItems` から取得物 `Guid` 列を解決 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObject.cs` | Modify | `ItemVeinParam` から解決。液体鉱脈は空列 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningControllerContext.cs` | Modify | フォーカス変化時と言語切替時にだけ表示名を組み立てて保持 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningFocusState.cs` | Modify | 名前の有無でキーを出し分ける |
| `moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningTargetFocusContextTest.cs` | Modify | 表示名の解決・キャッシュ・空列のテスト |
| `moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningFocusStateTest.cs` | Modify | 4分岐 × 名前あり/なしの提示テスト |
| `moorestech_client/Assets/Scripts/Client.Tests/Mining/AttackTrackingMiningTarget.cs` | Modify | テストダブルのインターフェース追従 |

## 配置と前例（spec-architecture-review の結果）

### データフロー地図

```
MiningController.Update（Raycast）
  → MiningControllerContext.SetFocusTarget（フォーカス実体の唯一の書き手）
  → ［CurrentFocusTarget ＋ CurrentFocusTargetEarnItemNames］
  → MiningFocusState（読み手）
  → MouseCursorTooltip → uGUI ／ TooltipTopic → Web UI
```

本planが足すものの立ち位置:

- `IMiningTargetObject.EarnItemGuids` … **データ源**（マスタ由来の読み取りのみ。判断を持たない）
- `MiningControllerContext.CurrentFocusTargetEarnItemNames` … 共有状態への**書き手**。既存の `SetFocusTarget` が唯一の書き込み点で、2人目の書き手を作らない
- `MiningFocusState` … **読み手**。名前を作らず、受け取った文字列を並べるだけ

交差点（分岐・逆流・並行経路）は追加していない。`MiningFocusState` から対象へ名前を問い直す経路も、tooltipへ直接文字列を流す経路も作らない。

### 配置決定と前例

| 項目 | 配置先 | 前例 |
| --- | --- | --- |
| `EarnItemGuids` | `Client.Game.InGame.Mining.IMiningTargetObject` | 同インターフェースの `DestroySoundType`（対象がマスタ由来の属性を公開する既存の形） |
| 取得物Guidの解決 | `MapObjectGameObject.Initialize` / `OutcropGameObject.Initialize` | 両者が既に `Initialize` でマスタから `DestroySoundType` / `_usableToolItemIds` を前倒し解決している |
| 表示名の組み立て | `MiningControllerContext` | `MiningFocusState.ShowRecommendMiningTools` の `Localize.GetContent(ContentLocalizationKeys.ItemName(guid))` ＋ `string.Join(", ")` と同形 |
| 言語切替への追従 | `Localize.OnLanguageChanged.Subscribe`（UniRx） | プロジェクト標準（`Action` 禁止規約）。`Localize` が公開する唯一の変化通知 |
| tooltipの提示 | `MouseCursorTooltip.Show(key, textParams)` | 既存の全tooltip呼び出しと同一。生文字列は渡さない |
| 文言の追加 | `Localization/localization.csv` | 既存 `ui.tooltip.*` 5件と同一 |

`Core.Master` への追加はゼロ。`Client.Localization` への追加もゼロ。共有層はいずれも読むだけで、採掘ドメインの語彙は `Client.Game.InGame.Mining` の内側に閉じている。

### 機能パリティ（死活表）

tooltip機構にぶら下がる表示が計画後も生きるかを全件確認した。

| 表示 | 計画後 | 根拠 |
| --- | --- | --- |
| `Ready` の長押し案内 | 生きる（取得物ありは文言が「採掘」へ変わる＝裁定済み） | Task 4 |
| `InstantPickUp` の案内 | 生きる（同上） | Task 4 |
| `ToolMismatch` の必要ツール一覧 | 生きる（`{p1}` へ位置が移るだけ） | Task 4 |
| `HandMiningNotAllowed` の不可文言 | 生きる | Task 4 |
| 採掘進捗中／Idle の `Hide()` | 生きる | `MiningProgressState` / `MiningIdleState` は未変更 |
| `ui.tooltip.worldTarget` 等 採掘以外のtooltip | 生きる | 未変更 |
| Web UI 側の tooltip 表示 | 生きる | キー＋`{p0}`契約が不変。生成物はTask 1で更新 |

死ぬ・退化する操作は無い。裁定を要する項目は残っていない。

### 新規パターン（レビュー注目点）

- **既存文言 `ui.tooltip.holdToGet` / `ui.tooltip.pickUpLeftClick` が「取得物ゼロの対象専用」へ格下げされる。** 通常プレイで最も多く見えるのは新設キー側になる。これはユーザー裁定（取得物ゼロは現行文言のまま）の直接の帰結。

---

### Task 1: 名前つきtooltip文言の辞書追加とWeb UI生成物の更新

**Files:**
- Modify: `Localization/localization.csv`（末尾に4行追記）
- Modify: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（スクリプトで再生成）
- Test: `moorestech_web/webui/src/shared/i18n/localizationKeysFreshness.test.ts`（既存。修正しない）

**Interfaces:**
- Consumes: なし（先頭タスク）
- Produces: 生成される C# キー `LocalizationKeys.Ui.Tooltip.NamedMineHold` / `NamedMineClick` / `NamedRequiredItems` / `NamedCannotHandMine`（いずれも `Mooresmaster.Localization.Generated.LocalizationKey` 型）。Web 側は `L.ui.tooltip.namedMineHold` 等。

- [ ] **Step 1: 失敗を先に確認するため、現状の freshness テストが通ることを確かめる**

Run:
```bash
cd moorestech_web/webui && pnpm vitest run src/shared/i18n/localizationKeysFreshness.test.ts
```
Expected: PASS（2 tests。CSV変更前なので生成物と一致している）

- [ ] **Step 2: CSVへ4行追記する**

`Localization/localization.csv` の末尾に、以下4行をこの順で追記する（列は `key,Source,english,japanese`）。

```csv
ui.tooltip.namedMineHold,{p0} : Hold left-click to mine,{p0} : Hold left-click to mine,{p0} : 左クリック長押しで採掘
ui.tooltip.namedMineClick,{p0} : Left-click to mine,{p0} : Left-click to mine,{p0} : 左クリックで採掘
ui.tooltip.namedRequiredItems,{p0} : Requires: {p1},{p0} : Requires: {p1},{p0} : このアイテムが必要です: {p1}
ui.tooltip.namedCannotHandMine,{p0} : Cannot be mined by hand,{p0} : Cannot be mined by hand,{p0} : 手掘りできません
```

追記前に末尾へ改行があるかを確認すること:
```bash
tail -c 1 Localization/localization.csv | xxd
```
`0a` で終わっていなければ、追記前に改行を1つ足す。

- [ ] **Step 3: freshness テストを実行して失敗を確認する**

Run:
```bash
cd moorestech_web/webui && pnpm vitest run src/shared/i18n/localizationKeysFreshness.test.ts
```
Expected: FAIL — `localizationKeys freshness > generated file matches the CSV source of truth` が、生成物に `namedMineHold` 等が無いという差分で落ちる

- [ ] **Step 4: Web UI のキーカタログを再生成する**

Run:
```bash
cd moorestech_web/webui && pnpm gen:i18n
```
Expected: `src/shared/i18n/generated/localizationKeys.ts` が更新され、`git diff` に `namedMineHold` / `namedMineClick` / `namedRequiredItems` / `namedCannotHandMine` の4キーが `L.ui.tooltip` と `VanillaLocalizationKeys` の両方に現れる。`generated/contentKeys.ts` は変化しない。

- [ ] **Step 5: freshness テストを実行して通ることを確認する**

Run:
```bash
cd moorestech_web/webui && pnpm vitest run src/shared/i18n/localizationKeysFreshness.test.ts
```
Expected: PASS

- [ ] **Step 6: コミットする**

```bash
git add Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts
git commit -m "feat(localization): 採掘tooltipの名前つき文言キーを4件追加する"
```

---

### Task 2: 採掘対象が取得物のGuid列を公開する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/IMiningTargetObject.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObject.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Mining/AttackTrackingMiningTarget.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Mining/Outcrop/OutcropMiningTargetTest.cs`

**Interfaces:**
- Consumes: なし
- Produces: `IMiningTargetObject.EarnItemGuids` → `System.Collections.Generic.IReadOnlyList<System.Guid>`。取得物が無い対象（`earnItems` 空・液体鉱脈）は `Count == 0` を返す。null は返さない。

- [ ] **Step 1: 露頭の取得物解決について失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Mining/Outcrop/OutcropMiningTargetTest.cs` に追加する。既存の `CreateOutcrop(Guid, out GameObject)` ヘルパと `_outcrop`（IronVein）をそのまま使う。テストmod `ForUnitTest` の鉱脈は次のとおり:

| veinGuid | 名前 | veinType | handMiningType | 中身 |
| --- | --- | --- | --- | --- |
| `11111111-0000-0000-0000-000000000001` | test:IronVein | item | minable | itemGuid `00000000-0000-0000-1234-000000000001` |
| `11111111-0000-0000-0000-000000000002` | test:WaterVein | fluid | none | fluidGuid |
| `11111111-0000-0000-0000-000000000004` | test:UnmineableItemVein | item | none | itemGuid `00000000-0000-0000-1234-000000000003` |

クラス先頭の定数群へ追加する。

```csharp
        private static readonly Guid WaterVeinGuid = new("11111111-0000-0000-0000-000000000002");
        private static readonly Guid IronVeinEarnItemGuid = new("00000000-0000-0000-1234-000000000001");
        private static readonly Guid UnmineableVeinEarnItemGuid = new("00000000-0000-0000-1234-000000000003");
```

テスト3件を追加する。

```csharp
        [Test]
        public void アイテム鉱脈の露頭は取得アイテムのGuidを返す()
        {
            // 表示名の出所は鉱脈マスタのitemGuid。veinNameは英語のマスタ名なので使わない（ADR 0033）
            // The display name comes from the vein master's itemGuid; veinName is an English master name (ADR 0033)
            CollectionAssert.AreEqual(new[] { IronVeinEarnItemGuid }, _outcrop.EarnItemGuids);
        }

        [Test]
        public void 液体鉱脈の露頭は取得アイテムを持たない()
        {
            var waterOutcrop = CreateOutcrop(WaterVeinGuid, out _);

            // 液体はアイテム名を持たないので名前欄を空にする（ADR 0033）
            // A fluid has no item name, so the name slot stays empty (ADR 0033)
            CollectionAssert.IsEmpty(waterOutcrop.EarnItemGuids);
        }

        [Test]
        public void 手掘り不可のアイテム鉱脈でも取得アイテムのGuidは返す()
        {
            var unmineableOutcrop = CreateOutcrop(UnmineableVeinGuid, out _);

            // 掘れなくても何が埋まっているかは見せる（ADR 0033）
            // Even an unmineable vein still reveals what it holds (ADR 0033)
            CollectionAssert.AreEqual(new[] { UnmineableVeinEarnItemGuid }, unmineableOutcrop.EarnItemGuids);
        }
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run:
```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.Mining\.Outcrop\..*"
```
Expected: コンパイルエラー — `'OutcropGameObject' does not contain a definition for 'EarnItemGuids'`

- [ ] **Step 3: インターフェースへ取得物Guid列を追加する**

`IMiningTargetObject.cs` の `using` に `using System;` を足し、`IMiningTargetObject` 内の `SoundEffectType DestroySoundType { get; }` の直後へ追加する。

```csharp
        // tooltipへ出す取得物の名前解決用。文言の組み立ては呼び出し側が持つ
        // Identifies what this target yields for the tooltip; the caller composes the text
        IReadOnlyList<Guid> EarnItemGuids { get; }
```

- [ ] **Step 4: MapObjectGameObject に取得物Guid列を実装する**

`MapObjectGameObject.cs` の `EmptyToolItemIds` の定義の直後へフィールドを追加する。

```csharp
        // 取得物ゼロのmapObjectが多数あるため、空の共有インスタンスを使い回す
        // Many map objects yield nothing, so one shared empty instance is reused
        private static readonly IReadOnlyList<Guid> NoEarnItemGuids = Array.Empty<Guid>();

        private IReadOnlyList<Guid> _earnItemGuids = NoEarnItemGuids;
        public IReadOnlyList<Guid> EarnItemGuids => _earnItemGuids;
```

`Initialize` 内、`if (MapObjectMasterElement == null) { ... return; }` の直後（`if (mapObjectInfo.IsDestroyed)` の前）へ追加する。

```csharp
            // 取得物はマスタ確定時に1度だけ拾う。毎フレームの解決を避けるための前倒し
            // Resolve the yields once when the master is settled, so nothing resolves per frame
            var earnItems = MapObjectMasterElement.EarnItems;
            var earnItemGuids = new Guid[earnItems.Length];
            for (var index = 0; index < earnItems.Length; index++) earnItemGuids[index] = earnItems[index].ItemGuid;
            _earnItemGuids = earnItemGuids;
```

- [ ] **Step 5: OutcropGameObject に取得物Guid列を実装する**

`OutcropGameObject.cs` の `NoHandMiningTools` の定義の直後へ追加する。

```csharp
        private static readonly IReadOnlyList<Guid> NoEarnItemGuids = Array.Empty<Guid>();

        private IReadOnlyList<Guid> _earnItemGuids = NoEarnItemGuids;
        public IReadOnlyList<Guid> EarnItemGuids => _earnItemGuids;
```

`Initialize` 内、`_handMiningTools = ...` の行の直後へ追加する。

```csharp
            // 液体鉱脈はアイテム名を持たないため名前欄を空のままにする（ADR 0033）
            // A fluid vein has no item name, so its name slot stays empty (ADR 0033)
            _earnItemGuids = element.VeinParam is ItemVeinParam itemVeinParam
                ? new[] { itemVeinParam.ItemGuid }
                : NoEarnItemGuids;
```

- [ ] **Step 6: テストダブルを追従させる**

`AttackTrackingMiningTarget.cs` の `AttackTrackingMiningTarget` クラスへ、`DestroySoundType` の直後に追加する（`using System;` を足す）。

```csharp
        // 打撃回数だけを見るfixtureなので取得物は持たない
        // This fixture only counts attacks, so it yields nothing
        public IReadOnlyList<Guid> EarnItemGuids => Array.Empty<Guid>();
```

`MiningTargetFocusContextTest.cs` の `FocusTrackingMiningTarget` と `MiningFocusStateTest.cs` の `OutcomeStubMiningTarget` にも、それぞれ `DestroySoundType` の直後へ同じ1行を足す（両ファイルとも Task 3 / Task 4 で取得物を注入できる形へ差し替えるので、ここでは空を返す実装で通す）。

```csharp
            public IReadOnlyList<Guid> EarnItemGuids => Array.Empty<Guid>();
```

`MiningTargetFocusContextTest.cs` は `using System;` を、`MiningFocusStateTest.cs` は `System.Guid` / `System.Array` を完全修飾で使うか `using System;` を足す（同ファイルは既に `System.Guid` を完全修飾で書いている箇所があるため、既存の書き方に合わせる）。

- [ ] **Step 7: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 8: テストを実行して通ることを確認する**

Run:
```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.Mining\..*"
```
Expected: 新規2件を含めて全PASS

- [ ] **Step 9: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/IMiningTargetObject.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObject.cs \
        moorestech_client/Assets/Scripts/Client.Tests/Mining
git commit -m "feat(mining): 採掘対象が取得アイテムのGuid列を公開する"
```

---

### Task 3: フォーカス変化時にだけ取得アイテム名を組み立てて保持する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningControllerContext.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningTargetFocusContextTest.cs`

**Interfaces:**
- Consumes: `IMiningTargetObject.EarnItemGuids`（Task 2）
- Produces: `MiningControllerContext.CurrentFocusTargetEarnItemNames` → `string`。取得物が無いフォーカス・フォーカス無しでは `string.Empty`。複数件は `", "` 区切りで連結される。

- [ ] **Step 1: 失敗するテストを書く**

`MiningTargetFocusContextTest.cs` の `FocusTrackingMiningTarget` を、取得物を注入できる形へ変える。Task 2 で足した `EarnItemGuids => Array.Empty<Guid>()` を次へ差し替える。

```csharp
            public IReadOnlyList<Guid> EarnItemGuids { get; }

            public FocusTrackingMiningTarget(string name, GameObject gameObject, List<string> focusEventLog, IReadOnlyList<Guid> earnItemGuids)
            {
                _name = name;
                GameObject = gameObject;
                _focusEventLog = focusEventLog;
                EarnItemGuids = earnItemGuids;
            }
```

既存テスト `SetFocusTargetPushesOnlyWhenTargetChanges` にある3つの生成箇所（`firstTarget` / `sameObjectWrapper` / `secondTarget`）へ、第4引数 `Array.Empty<Guid>()` を渡す。デフォルト引数は使わない。

そのうえで以下のテストを追加する。

```csharp
        private static readonly Guid FirstEarnItemGuid = new("00000000-0000-0000-9999-000000000001");
        private static readonly Guid SecondEarnItemGuid = new("00000000-0000-0000-9999-000000000002");

        [Test]
        public void 取得アイテム名はフォーカス変化時に組み立てて保持される()
        {
            // 実辞書を通す。未登録キーは[!key]へ落ちるが、連結と保持の検証には十分
            // Resolve through the real dictionary; unknown keys fall back to [!key], which is enough here
            Localize.Initialize();

            var context = new MiningControllerContext(null);
            var focusEventLog = new List<string>();
            var twoItemObject = new GameObject("TwoItemTarget");
            var noItemObject = new GameObject("NoItemTarget");
            var twoItemTarget = new FocusTrackingMiningTarget("two", twoItemObject, focusEventLog, new[] { FirstEarnItemGuid, SecondEarnItemGuid });
            var noItemTarget = new FocusTrackingMiningTarget("none", noItemObject, focusEventLog, Array.Empty<Guid>());

            Assert.AreEqual(string.Empty, context.CurrentFocusTargetEarnItemNames);

            context.SetFocusTarget(twoItemTarget);
            var expected =
                $"{Localize.GetContent(ContentLocalizationKeys.ItemName(FirstEarnItemGuid))}, " +
                $"{Localize.GetContent(ContentLocalizationKeys.ItemName(SecondEarnItemGuid))}";
            Assert.AreEqual(expected, context.CurrentFocusTargetEarnItemNames);

            // 取得物ゼロの対象では名前欄を空に戻す
            // A target that yields nothing clears the name slot
            context.SetFocusTarget(noItemTarget);
            Assert.AreEqual(string.Empty, context.CurrentFocusTargetEarnItemNames);

            context.SetFocusTarget(null);
            Assert.AreEqual(string.Empty, context.CurrentFocusTargetEarnItemNames);

            Object.DestroyImmediate(twoItemObject);
            Object.DestroyImmediate(noItemObject);
        }
```

ファイル先頭の `using` へ `using System;`、`using Client.Localization;`、`using Mooresmaster.Localization.Generated;` を足す。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run:
```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.Mining\.MiningTargetFocusContextTest"
```
Expected: コンパイルエラー — `'MiningControllerContext' does not contain a definition for 'CurrentFocusTargetEarnItemNames'`

- [ ] **Step 3: MiningControllerContext に解決と保持を実装する**

`MiningControllerContext.cs` を次の内容へ差し替える。

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using UniRx;

namespace Client.Game.InGame.Mining
{
    /// <summary>
    ///     採掘ステート群が共有する状態と照合を持つコンテキスト
    ///     Context holding the state and lookups shared by the mining states
    /// </summary>
    public class MiningControllerContext
    {
        public IMiningTargetObject CurrentFocusTarget { get; private set; }

        // フォーカス中の対象から取れるものの表示名。取得物が無ければ空文字
        // Display names of what the focused target yields; empty when it yields nothing
        public string CurrentFocusTargetEarnItemNames { get; private set; } = string.Empty;

        public readonly LocalPlayerEquipment LocalPlayerEquipment;

        public MiningControllerContext(LocalPlayerEquipment localPlayerEquipment)
        {
            LocalPlayerEquipment = localPlayerEquipment;

            // 言語切替で保持中の名前が古くなるため、1本だけ購読して作り直す
            // Held names go stale on a language switch, so one subscription rebuilds them
            Localize.OnLanguageChanged.Subscribe(_ => ResolveEarnItemNames());
        }

        public void SetFocusTarget(IMiningTargetObject target)
        {
            var currentGameObject = CurrentFocusTarget?.GameObject;
            var nextGameObject = target?.GameObject;

            // 実体変更時だけ通知
            // Notify only on concrete change
            if (currentGameObject != nextGameObject)
            {
                CurrentFocusTarget?.SetFocused(false);
                target?.SetFocused(true);
            }

            if (ReferenceEquals(CurrentFocusTarget, target)) return;

            CurrentFocusTarget = target;

            // 毎フレーム文字列を作らないよう、フォーカスが変わった瞬間だけ解決する
            // Resolve only at the moment focus changes, so no string is built per frame
            ResolveEarnItemNames();
        }

        private void ResolveEarnItemNames()
        {
            var earnItemGuids = CurrentFocusTarget?.EarnItemGuids;
            if (earnItemGuids == null || earnItemGuids.Count == 0)
            {
                CurrentFocusTargetEarnItemNames = string.Empty;
                return;
            }

            var itemNames = new string[earnItemGuids.Count];
            for (var index = 0; index < earnItemGuids.Count; index++)
                itemNames[index] = Localize.GetContent(ContentLocalizationKeys.ItemName(earnItemGuids[index]));

            CurrentFocusTargetEarnItemNames = string.Join(", ", itemNames);
        }
    }
}
```

- [ ] **Step 4: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 5: テストを実行して通ることを確認する**

Run:
```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.Mining\.MiningTargetFocusContextTest"
```
Expected: 既存の `SetFocusTargetPushesOnlyWhenTargetChanges` を含め全PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningControllerContext.cs \
        moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningTargetFocusContextTest.cs
git commit -m "feat(mining): フォーカス変化時にだけ取得アイテム名を解決して保持する"
```

---

### Task 4: 名前の有無でtooltipのキーを出し分ける

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningFocusState.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningFocusStateTest.cs`

**Interfaces:**
- Consumes: `MiningControllerContext.CurrentFocusTargetEarnItemNames`（Task 3）、`LocalizationKeys.Ui.Tooltip.NamedMineHold` / `NamedMineClick` / `NamedRequiredItems` / `NamedCannotHandMine`（Task 1）
- Produces: なし（末端の表示）

- [ ] **Step 1: 失敗するテストを書く**

`MiningFocusStateTest.cs` の `OutcomeStubMiningTarget` を、取得物Guid列を注入できる形へ変える。

```csharp
        private sealed class OutcomeStubMiningTarget : IMiningTargetObject
        {
            private readonly MiningStartOutcome _outcome;
            private readonly List<ItemId> _recommendedToolItemIds;

            public GameObject GameObject { get; }
            public SoundEffectType DestroySoundType => SoundEffectType.DestroyStone;
            public IReadOnlyList<System.Guid> EarnItemGuids { get; }

            public OutcomeStubMiningTarget(MiningStartOutcome outcome, ItemId recommendedToolItemId, IReadOnlyList<System.Guid> earnItemGuids)
            {
                _outcome = outcome;
                _recommendedToolItemIds = new List<ItemId> { recommendedToolItemId };
                EarnItemGuids = earnItemGuids;
                GameObject = new GameObject("OutcomeStubMiningTarget");
            }
            // TryBeginHandMining / SetFocused / SendAttack は既存のまま
        }
```

`RunFocusState` を取得物Guid列つきで呼べるようにする。既存の2つのオーバーロードは本体を捨てて新しい3引数版へ委譲させる。

```csharp
        private IMiningState RunFocusState(MiningStartOutcome outcome)
        {
            return RunFocusState(outcome, new MiningFocusState(), System.Array.Empty<System.Guid>());
        }

        private IMiningState RunFocusState(MiningStartOutcome outcome, MiningFocusState focusState)
        {
            return RunFocusState(outcome, focusState, System.Array.Empty<System.Guid>());
        }
```

3引数版は次のとおり（既存の2引数版の本体を移し、スタブへ取得物を渡す形にする）。

```csharp
        private static readonly System.Guid EarnItemGuid = new("00000000-0000-0000-9999-000000000001");

        private IMiningState RunFocusState(MiningStartOutcome outcome, MiningFocusState focusState, IReadOnlyList<System.Guid> earnItemGuids)
        {
            var context = new MiningControllerContext(CreateEquipmentHoldingTool());
            var stubTarget = new OutcomeStubMiningTarget(outcome, MasterHolder.ItemMaster.GetItemId(ToolItemGuid), earnItemGuids);
            _stubTargetObjects.Add(stubTarget.GameObject);
            context.SetFocusTarget(stubTarget);

            Assert.IsFalse(InputManager.Playable.ScreenLeftClick.GetKey, "左クリックが押されていない前提が崩れている");
            return focusState.GetNextUpdate(context, 0.01f);
        }
```

そのうえで以下4件を追加する。

```csharp
        [Test]
        public void 取得物のある採掘可能な対象には名前つきの長押し文言を出す()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.Ready, focusState, new[] { EarnItemGuid });

            Assert.AreSame(focusState, next);
            var presentation = MouseCursorTooltip.Instance.GetPresentation();
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.NamedMineHold.Key, presentation.TextKey);
            CollectionAssert.AreEqual(
                new[] { Localize.GetContent(ContentLocalizationKeys.ItemName(EarnItemGuid)) },
                presentation.TextParams);
        }

        [Test]
        public void 取得物のあるPickUp対象には名前つきの単クリック文言を出す()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.InstantPickUp, focusState, new[] { EarnItemGuid });

            Assert.AreSame(focusState, next);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.NamedMineClick.Key, MouseCursorTooltip.Instance.GetPresentation().TextKey);
        }

        [Test]
        public void 取得物のある手掘り不可の対象には名前つきの不可文言を出す()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.HandMiningNotAllowed, focusState, new[] { EarnItemGuid });

            Assert.AreSame(focusState, next);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.NamedCannotHandMine.Key, MouseCursorTooltip.Instance.GetPresentation().TextKey);
        }

        [Test]
        public void 取得物のある装備不一致の対象には名前を先頭に必要ツールを続ける()
        {
            var focusState = new MiningFocusState();
            var next = RunFocusState(MiningStartOutcome.ToolMismatch, focusState, new[] { EarnItemGuid });

            Assert.AreSame(focusState, next);
            var presentation = MouseCursorTooltip.Instance.GetPresentation();
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.NamedRequiredItems.Key, presentation.TextKey);

            // 名前が{p0}・必要ツールが{p1}という並びを固定する
            // Pin the ordering: the name is {p0} and the required tools are {p1}
            Assert.AreEqual(2, presentation.TextParams.Count);
            Assert.AreEqual(Localize.GetContent(ContentLocalizationKeys.ItemName(EarnItemGuid)), presentation.TextParams[0]);
            Assert.AreEqual(
                Localize.GetContent(ContentLocalizationKeys.ItemName(ToolItemGuid)),
                presentation.TextParams[1]);
        }
```

既存4テスト（取得物なし）はキーが既存キーのままであることを検証し続けるため、変更しない。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run:
```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.Mining\.MiningFocusStateTest"
```
Expected: 新規4件がFAIL（`TextKey` が既存キー `ui.tooltip.holdToGet` 等のまま）

- [ ] **Step 3: MiningFocusState を出し分ける実装へ変える**

`MiningFocusState.cs` の `GetNextUpdate` を次の内容へ差し替える（`using System;` を追加する）。

```csharp
        public IMiningState GetNextUpdate(MiningControllerContext context, float dt)
        {
            // フォーカスが外れたのであればIdleに遷移
            // If the focus is lost, transition to Idle
            var currentTarget = context.CurrentFocusTarget;
            if (currentTarget == null) return new MiningIdleState();

            // 装備を渡して可否・種別・ツールを一度に問い合わせる
            // Ask once for availability, kind and tool with the equipment applied
            var equippedItemId = context.LocalPlayerEquipment.SelectedItem.Id;
            var outcome = currentTarget.TryBeginHandMining(equippedItemId, out var usableMiningTool, out var recommendedToolItemIds);

            // 表示名はコンテキストがフォーカス変化時に解決済み
            // The context already resolved the display names when focus changed
            var earnItemNames = context.CurrentFocusTargetEarnItemNames;

            switch (outcome)
            {
                case MiningStartOutcome.Unavailable:
                    return new MiningIdleState();
                case MiningStartOutcome.InstantPickUp:
                    return PickUpProcess(context);
                case MiningStartOutcome.HandMiningNotAllowed:
                    // 掘れない理由を出して維持する
                    // Show why it cannot be mined and keep focus
                    ShowEarnItemNamed(
                        LocalizationKeys.Ui.Tooltip.NamedCannotHandMine,
                        LocalizationKeys.Ui.Tooltip.CannotHandMine,
                        Array.Empty<string>());
                    return this;
                case MiningStartOutcome.ToolMismatch:
                    // 無効装備ならフォーカス維持
                    // Keep focus for invalid equipment
                    ShowRecommendMiningTools(recommendedToolItemIds);
                    return this;
            }

            // クリックしていない場合はフォーカスを維持
            // If not clicked, maintain focus
            if (!InputManager.Playable.ScreenLeftClick.GetKey)
            {
                ShowEarnItemNamed(
                    LocalizationKeys.Ui.Tooltip.NamedMineHold,
                    LocalizationKeys.Ui.Tooltip.HoldToGet,
                    Array.Empty<string>());
                return this;
            }

            // マイニング状態に遷移
            // Transition to mining state
            MouseCursorTooltip.Instance.Hide();
            return new MiningProgressState(currentTarget, usableMiningTool);

            #region Internal

            IMiningState PickUpProcess(MiningControllerContext pickUpContext)
            {
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown)
                {
                    MouseCursorTooltip.Instance.Hide();
                    return new MiningCompleteState(pickUpContext.CurrentFocusTarget);
                }

                // 左クリックがされていなければ現状を維持
                // If left click is not pressed, maintain the current state
                ShowEarnItemNamed(
                    LocalizationKeys.Ui.Tooltip.NamedMineClick,
                    LocalizationKeys.Ui.Tooltip.PickUpLeftClick,
                    Array.Empty<string>());
                return this;
            }

            void ShowRecommendMiningTools(List<ItemId> toolItemIds)
            {
                var localizedToolNames = new List<string>();
                foreach (var toolItemId in toolItemIds)
                {
                    var toolItemGuid = MasterHolder.ItemMaster.GetItemMaster(toolItemId).ItemGuid;
                    localizedToolNames.Add(Localize.GetContent(
                        ContentLocalizationKeys.ItemName(toolItemGuid)));
                }

                // 必要アイテム名をパラメータにまとめ、文言全体は表示側で解決する
                // Join required item names as a parameter and let the presentation resolve the full sentence
                ShowEarnItemNamed(
                    LocalizationKeys.Ui.Tooltip.NamedRequiredItems,
                    LocalizationKeys.Ui.Tooltip.RequiredItems,
                    new[] { string.Join(", ", localizedToolNames) });
            }

            // 取得物名が無い対象は名前欄ごと落として従来文言へ戻す（ADR 0033）
            // A target that yields nothing drops the name slot and falls back to the original text (ADR 0033)
            void ShowEarnItemNamed(LocalizationKey namedKey, LocalizationKey unnamedKey, IReadOnlyList<string> textParams)
            {
                if (earnItemNames.Length == 0)
                {
                    MouseCursorTooltip.Instance.Show(unnamedKey, textParams);
                    return;
                }

                var namedParams = new string[textParams.Count + 1];
                namedParams[0] = earnItemNames;
                for (var index = 0; index < textParams.Count; index++) namedParams[index + 1] = textParams[index];
                MouseCursorTooltip.Instance.Show(namedKey, namedParams);
            }

            #endregion
        }
```

- [ ] **Step 4: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 5: テストを実行して通ることを確認する**

Run:
```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.Mining\..*"
```
Expected: 既存8件＋新規6件が全PASS

- [ ] **Step 6: ファイル行数を確認する**

Run:
```bash
wc -l moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningFocusState.cs \
      moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningControllerContext.cs
```
Expected: いずれも200行以下

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningFocusState.cs \
        moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningFocusStateTest.cs
git commit -m "feat(mining): 採掘tooltipに取得アイテム名を前置し動作語を採掘へ統一する"
```

---

### Task 5: 周辺テストの巻き添え確認

**Files:**
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Tooltip/TooltipPresentationEqualityTest.cs`（既存。修正しない）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Localization/`（既存。修正しない）
- Test: `moorestech_web/webui`（既存。修正しない）

**Interfaces:**
- Consumes: Task 1〜4の全成果
- Produces: なし

- [ ] **Step 1: tooltip とローカライズの既存テストを実行する**

Run:
```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.(Tooltip|Localization)\..*"
```
Expected: 全PASS。落ちた場合、辞書キー追加で件数を数えているテストがあれば期待値を実数へ更新する（文言の追加自体はRequirementsの確定事項なので、テスト側を追従させる）

- [ ] **Step 2: Web UI のテストを一式実行する**

Run:
```bash
cd moorestech_web/webui && pnpm test
```
Expected: 全PASS

- [ ] **Step 3: Unity Console にエラーが無いことを確認する**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 出力なし

- [ ] **Step 4: 差分が残っていればコミットする**

```bash
git status --short
git commit -am "test: 辞書キー追加に伴う既存テストの期待値を追従させる"
```
（差分が無ければこのコミットは行わない）

---

### Task 6: 全ブランチレビュー（省略不可）

**Files:**
- 本ブランチの全差分

**Interfaces:**
- Consumes: Task 1〜5の全成果
- Produces: レビュー指摘への対応済みブランチ

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

必ず `moores-code-review` スキルを起動し、master との差分全体をレビューする。ゴール文言による省略は禁止。

- [ ] **Step 2: 機械的な指摘を修正し、設計判断はユーザーへ諮る**

- [ ] **Step 3: 修正後に再コンパイルとテストを実行する**

Run:
```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.Mining\..*"
```
Expected: エラー0件・全PASS

- [ ] **Step 4: コミットする**

```bash
git add -A
git commit -m "fix: コードレビュー指摘を反映する"
```

---

## 判断記録（ADR）

- 設計裁定の正本: [docs/adr/0033-mining-tooltip-shows-earned-item-name.md](../../adr/0033-mining-tooltip-shows-earned-item-name.md)
- ユーザー裁定の蒸留: [.decisions/2026-08-25-採掘tooltipは取得アイテム名を前置する.md](../../../.decisions/2026-08-25-採掘tooltipは取得アイテム名を前置する.md)
- タスク台帳: `bd show moorestech-ifcc`

planning中に新たに生じた判断:

- **`IMiningTargetObject` が公開するのは `IReadOnlyList<Guid>`（`ItemId` ではない）。** ローカライズキー `ContentLocalizationKeys.ItemName(Guid)` が `Guid` を取り、mapObject の `EarnItems[].ItemGuid` も鉱脈の `ItemVeinParam.ItemGuid` も `Guid` を直接持つため、`ItemId` を経由すると `ItemMaster` への往復が増えるだけになる。
  出所: agent前提（既存 `MiningFocusState.ShowRecommendMiningTools` が `ItemId` → `GetItemMaster().ItemGuid` と往復している形の解消）
- **表示名の組み立ては `MiningControllerContext.SetFocusTarget` に置く。** 既に「実体変更時だけ」を判定している唯一の変化点であり、設計原則「変化を起こす操作の直後にプッシュする」に一致する。対象個体ごとの購読（数千個体）は採らない。
  出所: agent前提（ADR 0033 の該当箇所と同一）
- **`SetFocusTarget` に `ReferenceEquals` の早期returnを足す。** 同一参照での再解決を防ぐため。既存テスト `SetFocusTargetPushesOnlyWhenTargetChanges` が検証している「同一GameObjectの別ラッパは差し替わる」挙動は、参照が異なるため維持される。
  出所: agent前提
- **名前つき文言は既存キーの置き換えではなく新設キーにする。** 取得物ゼロの対象では既存文言（「取得する」）をそのまま出す裁定があるため、両方のキーが必要になる。
  出所: ユーザー裁定 2026-08-25「名前なしの現行文言のまま」の帰結
- **Web UI 側の生成物 `localizationKeys.ts` の再生成をTask 1に含める。** `localizationKeysFreshness` テストがCSVと生成物の一致を検査しており、再生成を忘れるとWeb UIのテストが落ちる。
  出所: agent前提（`moorestech_web/webui/src/shared/i18n/localizationKeysFreshness.test.ts` の実在）
