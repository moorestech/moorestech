# ビルドメニュー素材不足ツールチップ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** ビルドメニューでエントリをホバーしたとき、素材が足りなければツールチップで「素材が足りません」と不足素材（名前・所持/必要）を出し、詳細サイドバーの必要素材も research/craft と同型の不足表現へ揃える。

**Architecture:** 不足判定はホスト(C#)の `BuildMenuMaterialAvailability` が財布・デバッグフラグ・所持インベントリを見て行い、`BuildMenuRequiredItemDto` に `Held` / `Lacking` を載せて `build_menu.entries` で配信する。Web UI は受け取った `held` / `lacking` を書式化するだけで、財布や所持の算術を一切持たない（判定=ホスト・書式=Web）。`BuildMenuTopic` は `ILocalPlayerInventory.OnItemChange` を購読し、ビルドメニュー表示中の所持変化で再配信する（前例: `ResearchTopic`）。

**Tech Stack:** Unity 2022 / C# (Client.WebUiHost, Client.Game, Client.Tests / NUnit) + React 18 + TypeScript + zod + vitest (moorestech_web/webui) + Newtonsoft.Json / Mantine

## Requirements

設計対話（2026-08-28）で確定した要件。受け入れ基準を各行に含む。ADR: `docs/adr/0041-build-menu-material-shortage-tooltip.md`。

1. ビルドメニューのエントリスロットをホバーしたとき、素材が不足していればツールチップを出す。1行目に見出し「素材が足りません」、2行目以降に**不足している素材だけ**を `名前 所持/必要` 形式で並べる。
   受け入れ基準: 不足2件のブロックエントリで、ツールチップに見出し1行＋不足2行が出る。足りている素材の行は出ない。
2. 素材が足りているエントリではエントリスロットのツールチップを出さない（現状の無表示を維持）。
   受け入れ基準: `lacking` が1件も無いエントリでは `HoverTooltip` が `disabled` になり、ツールチップ本文が描画されない。
3. 不足しているエントリスロット自体の見た目は変えない（赤枠・暗転を付けない）。
   受け入れ基準: `BuildMenuSlot` が `SlotFrame` へ `insufficient` を渡さない。
4. 詳細サイドバーの必要アイテムスロットを research/craft と同型にする。必要数バッジを廃し、スロット下に `所持/必要`（不足時は赤字）を置き、不足時は `ItemSlot insufficient` の赤枠、スロットホバーで素材ツールチップ（`名前 / 所持数: N / 必要数: M`）を出す。
   受け入れ基準: `ItemSlot` に `count` を渡さず、`insufficient` と `tooltip` を渡し、別途 `所持/必要` の span が `data-lack` 付きで出る。
5. 不足判定はホスト(C#)で行い、`BuildMenuRequiredItemDto` に `Held`（所持数）と `Lacking`（不足フラグ）を追加して配信する。Web 側は判定ロジックを持たない。
   受け入れ基準: `moorestech_web/webui/src` 配下に財布・所持数から不足を導く新規ロジックが存在しない（`lacking` をそのまま読むだけ）。
6. 財布制ブロック（`setPlacement` を持つブロック）は残り設置数 ≥ 1 なら不足なしとする。残り0のときだけ1セット分の必要素材と所持を突き合わせる。
   受け入れ基準: 残り2・所持0 のベルトで全 `Lacking` が false、残り0・所持不足で `Lacking` が true。
7. 詳細サイドバーの赤表現も同じ `Lacking` を使う（残りがある間は `0/5` でも黒字・白枠）。
   受け入れ基準: Web 側は `item.lacking` のみを赤判定に使い、`held < count` を自前で比較しない。
8. `FreeBlockPlacement` デバッグパラメータON時は `Lacking` を常に false にする。
   受け入れ基準: 同フラグON時、所持0でも `Lacking` が全件 false。
9. 所持数が変化したら配信を更新する。`BuildMenuTopic` が `ILocalPlayerInventory.OnItemChange` と `LocalPlayerInventoryController.OnInventoryRefreshed` を購読し、ビルドメニュー表示中のみ再配信する。
   受け入れ基準: ビルドメニュー非表示中の所持変化では再配信されず、表示中は再配信される。
10. 対象は必要素材を持つエントリ（ブロック・車両）。`blueprint` / `blueprintCopy` / `connectTool` は `CreateRequiredItems()` が空配列を返すため不足も出ない。
    受け入れ基準: これらの kind に専用の分岐を書かない（空配列の帰結として自然に無表示）。

**やらないこと（スコープ境界）:**
- ホットバー（`features/hotbar`）は一切変更しない。不足ツールチップも出さない。
- エントリスロットの外観（赤枠・グレーアウト・選択可否）は変更しない。不足エントリも従来どおりクリックで選択できる。
- 設置時（ゴースト段階）のカーソルtooltip（`ConstructionMaterialShortageReporter`）の挙動は変更しない。
- 「次のセットが買えない」といった先読み警告は実装しない。
- 財布の消費・設置ロジック自体は変更しない（読み取りのみ）。

## Global Constraints

- **AGENTS.md 準拠が必須。** 特に以下:
  - 1ファイル200行以下。超える場合はディレクトリ構造で分割する。`partial` は如何なる条件でも禁止。
  - `Func<>` 禁止。コールバックが要るなら設計を見直す。
  - `try-catch` は外部境界（プロセス起動・ネットワーク送受信・外部JSONパース）限定。今回のコードでは使わない。
  - 単純な getter/setter プロパティ禁止。値の Set は `public void SetHoge`。`{ get; private set; }` は許容。
  - デフォルト引数禁止。引数追加時は呼び出し側を全て変更する。
  - コメントは日本語1行 → 英語1行のセットを3〜10行ごとに。各言語1行に収める（折り返し禁止）。自明なコメントは書かない。
  - `#region Internal` はメソッド内ローカル関数をまとめる用途限定。クラス直下の private メソッド群を囲うのは禁止。
  - イベント発火に `Action` を使わない。UniRx を使う。
  - `.meta` ファイルは絶対に手動作成しない（Unity が生成する）。Prefab/シーン/ScriptableObject をテキスト編集しない。
  - `.cs` を変更したら必ずコンパイルを実行する。
- **設計原則（レビュー差し戻し対象）:**
  - スキーマの `optional: true`・`?? Default` フォールバックで欠損を吸収しない。`Held` / `Lacking` は必須フィールドとして全エントリに載せる。
  - 汎用基盤にドメイン語彙を持ち込まない。判断は具体側で行う。
  - 状態変化の検知は購読で行う。`Update()` 内で毎tickの同値判定をしない。
  - 着手前に前例を探し、そのパターンに従う。
- **時間API:** 本planの範囲では経過時間を扱わない。`Time.deltaTime` / `Stopwatch` は使わない。
- **表示文言（localization.csv、3言語必須: english / japanese / german）:**
  - `ui.buildMenu.materialShortageTitle` = EN `Not enough materials` / JA `素材が足りません` / DE `Nicht genug Materialien`
  - `ui.buildMenu.materialShortageLine` = 全言語 `{itemName} {ownedCount}/{requiredCount}`
  - `ui.buildMenu.materialTooltip` = EN `{itemName}\nOwned: {ownedCount}\nRequired: {requiredCount}` / JA `{itemName}\n所持数: {ownedCount}\n必要数: {requiredCount}` / DE `{itemName}\nBestand: {ownedCount}\nBenötigt: {requiredCount}`
- **コマンド:**
  - コンパイル: `uloop compile --project-path ./moorestech_client`
  - C#テスト: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"`（既定は PlayMode なので `--test-mode EditMode` を必ず付ける）
  - Webテスト: `cd moorestech_web/webui && npm run test`
  - i18nキー生成: `cd moorestech_web/webui && npm run gen:i18n`
- **localization.csv を編集したら**、Unity 側の生成キー（`Mooresmaster.Localization.Generated.LocalizationKeys`）も再生成が要る。触っていないキーで CS0117 が出たら CSV 再生成漏れなので `uloop compile --project-path ./moorestech_client --force-recompile` を実行する。

---

## File Structure

**C#（ホスト側・判定と配信）**

| ファイル | 種別 | 責務 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionMaterialHeldCounts.cs` | 新規 | 所持スタック列から itemId 別所持数を集計する唯一の供給点 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostShortageCalculator.cs` | 変更 | 所持集計を上記へ委譲（重複定義の解消） |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuMaterialAvailability.cs` | 新規 | 1エントリ分の必要素材へ Held / Lacking を付与する（財布・無料設置デバッグを考慮） |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuDtos.cs` | 変更 | `BuildMenuRequiredItemDto` に `Held` / `Lacking` を追加 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs` | 変更 | `CreateDtos` に所持インベントリ引数を追加し、必要素材DTO生成を Availability へ委譲 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuInventoryRepublishGate.cs` | 新規 | 所持変化での再配信可否（ビルドメニュー表示中のみ） |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuTopic.cs` | 変更 | 所持変化の購読と、ビルドメニュー表示中のみの再配信 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs` | 変更 | `BuildMenuTopic` へ `LocalPlayerInventoryController` を渡す |
| `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BuildMenuEntryDtoFactoryTest.cs` | 変更 | Held / Lacking の判定テスト追加、既存呼び出しの引数追従 |
| `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BuildMenuTopicRepublishTest.cs` | 新規 | 所持変化の再配信ゲートの回帰試験 |
| `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs` | 変更 | fixture DTO に Held / Lacking を追加 |
| `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/build_menu_snapshot.json` | 変更 | ワイヤ正準形へ held / lacking を追加 |

**TypeScript（Web UI・書式化と描画）**

| ファイル | 種別 | 責務 |
|---|---|---|
| `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.ts` | 変更 | `BuildMenuRequiredItemSchema` に `held` / `lacking` を追加 |
| `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.test.ts` | 変更 | 新フィールドの必須性テスト追加 |
| `moorestech_web/webui/src/features/buildMenu/buildMenuShortage.ts` | 新規 | エントリから不足素材だけを取り出す純関数 |
| `moorestech_web/webui/src/features/buildMenu/buildMenuShortage.test.ts` | 新規 | 上記の単体テスト |
| `moorestech_web/webui/src/shared/materialTooltipText.ts` | 変更 | `MaterialTooltipKey` にビルドメニューの2キーを追加 |
| `moorestech_web/webui/src/features/buildMenu/BuildMenuSlot.tsx` | 変更 | 不足時のみ `HoverTooltip` を有効化 |
| `moorestech_web/webui/src/features/buildMenu/BuildMenuSlot.test.ts` | 新規 | 不足あり/なしのツールチップ有無テスト |
| `moorestech_web/webui/src/features/buildMenu/BuildMenuDetailSidebar.tsx` | 変更 | 必要素材を research/craft 同型へ |
| `moorestech_web/webui/src/features/buildMenu/BuildMenuDetailSidebar.test.ts` | 新規 | 赤枠・所持/必要・ツールチップの描画テスト |
| `moorestech_web/webui/src/features/buildMenu/style.module.css` | 変更 | `.materialSlot` / `.materialCount` を追加 |
| `Localization/localization.csv` | 変更 | 新規3キーを3言語分追加 |
| `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` | 生成物 | `npm run gen:i18n` で再生成 |

---

### Task 1: ホスト側の不足判定と DTO 拡張

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionMaterialHeldCounts.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostShortageCalculator.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuMaterialAvailability.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuDtos.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/build_menu_snapshot.json`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BuildMenuEntryDtoFactoryTest.cs`

**Interfaces:**
- Consumes: `Game.Construction.ConstructionWalletQuery.IsCoveredByWallet(BlockId) : bool`、`Client.Game.InGame.BlockSystem.PlaceSystem.Targets.IPlacementTarget.CreateRequiredItems() : IReadOnlyList<(Guid itemGuid, int count)>`、`Common.Debug.DebugParameters.GetValueOrDefaultBool(string)`、`Core.Item.Interface.IItemStack`
- Produces:
  - `public static class ConstructionMaterialHeldCounts` に `public static Dictionary<ItemId, int> Tally(IEnumerable<IItemStack> inventoryItems)`
  - `public static class BuildMenuMaterialAvailability` に `public static List<BuildMenuRequiredItemDto> CreateRequiredItemDtos(IPlacementTarget target, ConstructionWalletQuery walletQuery, IReadOnlyDictionary<ItemId, int> heldByItem)`
  - `BuildMenuRequiredItemDto` に `public int Held;` と `public bool Lacking;`
  - `BuildMenuEntryDtoFactory.CreateDtos(IReadOnlyList<IPlacementTarget> targets, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems) : List<BuildMenuEntryDto>` と `CreateDtos(PlacementTargetResolver, ConstructionWalletQuery, IEnumerable<IItemStack>)`（どちらもデフォルト引数なし）

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/WebUi/BuildMenuEntryDtoFactoryTest.cs` の末尾（`BlueprintDeleteServiceStub` クラス定義の直前）に、以下の3テストを追加する。

```csharp
        [Test]
        public void 財布に残りがあるブロックは所持ゼロでも不足にならない()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var straightGuid = Guid.Parse("00000000-0000-0000-0000-000000000015");
            var straightBlockId = MasterHolder.BlockMaster.GetBlockId(straightGuid);
            var datastore = new ClientRemainingPlacementCountDatastore();
            datastore.Apply(straightBlockId, 2);
            var walletQuery = new ConstructionWalletQuery(datastore);

            var targets = new IPlacementTarget[] { new BlockPlacementTarget(straightGuid, null) };
            var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, Array.Empty<IItemStack>())[0];

            Assert.Greater(dto.RequiredItems.Count, 0);
            foreach (var requiredItem in dto.RequiredItems)
            {
                Assert.AreEqual(0, requiredItem.Held);
                Assert.IsFalse(requiredItem.Lacking, "残りがある間は不足にしない");
            }
        }

        [Test]
        public void 財布の残りが尽きたブロックは所持数と必要数を突き合わせて不足を出す()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var straightGuid = Guid.Parse("00000000-0000-0000-0000-000000000015");
            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            var targets = new IPlacementTarget[] { new BlockPlacementTarget(straightGuid, null) };

            // 必要素材の1件目だけを必要数-1だけ持たせ、不足が1件だけ立つ状態を作る
            // Hold one less than required of the first material so exactly one shortage stands
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(straightGuid).RequiredItems;
            var firstItemId = MasterHolder.ItemMaster.GetItemId(requiredItems[0].ItemGuid);
            var heldCount = requiredItems[0].Count - 1;
            var inventory = new List<IItemStack> { ServerContext.ItemStackFactory.Create(firstItemId, heldCount) };

            var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, inventory)[0];

            var first = dto.RequiredItems.Single(item => item.ItemId == firstItemId.AsPrimitive());
            Assert.AreEqual(heldCount, first.Held);
            Assert.IsTrue(first.Lacking);
        }

        [Test]
        public void 無料設置デバッグ中は所持ゼロでも不足にしない()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).BlockGuid;
            var targets = new IPlacementTarget[] { new BlockPlacementTarget(blockGuid, null) };

            // Client.Tests は ClientTestsDebugParametersIsolationFixture で隔離済み。後続テストへ残さないよう必ず消す
            // Client.Tests is already isolated by ClientTestsDebugParametersIsolationFixture; still remove it so later tests are unaffected
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
            try
            {
                var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, Array.Empty<IItemStack>())[0];

                Assert.Greater(dto.RequiredItems.Count, 0);
                foreach (var requiredItem in dto.RequiredItems)
                {
                    Assert.AreEqual(0, requiredItem.Held);
                    Assert.IsFalse(requiredItem.Lacking, "無料設置中は不足にしない");
                }
            }
            finally
            {
                DebugParameters.RemoveBool(DebugParameterKeys.FreeBlockPlacement);
            }
        }

        [Test]
        public void 財布を使わないブロックは所持数をそのままHeldへ載せる()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).BlockGuid;
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(blockGuid).RequiredItems;
            var firstItemId = MasterHolder.ItemMaster.GetItemId(requiredItems[0].ItemGuid);
            var inventory = new List<IItemStack> { ServerContext.ItemStackFactory.Create(firstItemId, requiredItems[0].Count) };

            var targets = new IPlacementTarget[] { new BlockPlacementTarget(blockGuid, null) };
            var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, inventory)[0];

            var first = dto.RequiredItems.Single(item => item.ItemId == firstItemId.AsPrimitive());
            Assert.AreEqual(requiredItems[0].Count, first.Held);
            Assert.IsFalse(first.Lacking);
        }
```

同ファイル冒頭の using に以下2行を追加する。

```csharp
using Common.Debug;
using Core.Item.Interface;
using Game.Context;
```

既存の3箇所の `BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery)` 呼び出し（`CreateDtosは全件が…`、`CreateDtosは財布キー正規化後の…`、`財布を使わないブロックはSetPlacementを持たない`）を `BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, Array.Empty<IItemStack>())` へ書き換える。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BuildMenuEntryDtoFactoryTest"`
Expected: コンパイルエラー（`CreateDtos` に3引数のオーバーロードが無い、`BuildMenuRequiredItemDto.Held` / `.Lacking` が無い）

- [ ] **Step 3: 所持数集計の共有クラスを作る**

Create `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionMaterialHeldCounts.cs`:

```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 所持スタック列からitemId別の所持数を集計する
    /// Tallies held counts per itemId from a sequence of item stacks
    /// </summary>
    public static class ConstructionMaterialHeldCounts
    {
        public static Dictionary<ItemId, int> Tally(IEnumerable<IItemStack> inventoryItems)
        {
            var heldByItem = new Dictionary<ItemId, int>();
            foreach (var stack in inventoryItems)
            {
                heldByItem.TryGetValue(stack.Id, out var current);
                heldByItem[stack.Id] = current + stack.Count;
            }
            return heldByItem;
        }
    }
}
```

- [ ] **Step 4: 既存の重複集計を共有クラスへ差し替える**

Modify `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostShortageCalculator.cs`: 以下のブロックを

```csharp
            // 所持数を集計する
            // Tally held counts
            var heldByItem = new Dictionary<ItemId, int>();
            foreach (var stack in inventoryItems)
            {
                heldByItem.TryGetValue(stack.Id, out var current);
                heldByItem[stack.Id] = current + stack.Count;
            }
```

次の2行へ置き換える。

```csharp
            // 所持集計は唯一の供給点へ委ねる
            // Delegate the held tally to its single supply point
            var heldByItem = ConstructionMaterialHeldCounts.Tally(inventoryItems);
```

- [ ] **Step 5: DTO に Held / Lacking を足す**

Modify `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuDtos.cs`: `BuildMenuRequiredItemDto` を次に置き換える。

```csharp
    public class BuildMenuRequiredItemDto
    {
        public int ItemId;
        public int Count;

        // 所持数と不足フラグはホストが決め切って配る。Web側は財布も所持も再計算しない
        // The host settles the held count and the shortage flag; the web side recomputes neither wallet nor holdings
        public int Held;
        public bool Lacking;
    }
```

- [ ] **Step 6: 不足判定クラスを作る**

Create `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuMaterialAvailability.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Common.Debug;
using Core.Master;
using Game.Construction;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// ビルドメニュー1エントリの必要素材へ所持数と不足フラグを付ける
    /// Attaches the held count and shortage flag to one build-menu entry's required items
    /// </summary>
    public static class BuildMenuMaterialAvailability
    {
        public static List<BuildMenuRequiredItemDto> CreateRequiredItemDtos(IPlacementTarget target, ConstructionWalletQuery walletQuery, IReadOnlyDictionary<ItemId, int> heldByItem)
        {
            // 支払いが発生しない局面では所持数だけ見せて不足は立てない
            // Where no payment happens the held count still shows, but no shortage stands
            var paymentSkipped = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement) || IsCoveredByWallet(target, walletQuery);

            var itemDtos = new List<BuildMenuRequiredItemDto>();
            foreach (var (itemGuid, count) in target.CreateRequiredItems())
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
                heldByItem.TryGetValue(itemId, out var held);
                itemDtos.Add(new BuildMenuRequiredItemDto
                {
                    ItemId = itemId.AsPrimitive(),
                    Count = count,
                    Held = held,
                    Lacking = !paymentSkipped && held < count,
                });
            }
            return itemDtos;
        }

        // 財布の有無も残りも財布へ問い合わせる。財布を持たない種別は常に支払いが起きる
        // Both wallet presence and the remainder come from the wallet; kinds without one always pay
        private static bool IsCoveredByWallet(IPlacementTarget target, ConstructionWalletQuery walletQuery)
        {
            if (target.Kind != PlacementTargetKind.Block) return false;
            return walletQuery.IsCoveredByWallet(((BlockPlacementTarget)target).BlockId);
        }
    }
}
```

`PlacementTargetKind` は `Game.PlacementTarget` 名前空間にあるため、必要なら `using Game.PlacementTarget;` を足す。

- [ ] **Step 7: DTO ファクトリを配線する**

Modify `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs`:

冒頭の using に次を追加する。

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Item.Interface;
```

2つの `CreateDtos` シグネチャへ所持インベントリを足し、所持集計を1回だけ行って各エントリへ渡す。

```csharp
        public static List<BuildMenuEntryDto> CreateDtos(PlacementTargetResolver placementTargetResolver, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems)
        {
            return CreateDtos(placementTargetResolver.CreateUnlockedTargets(), walletQuery, inventoryItems);
        }

        public static List<BuildMenuEntryDto> CreateDtos(IReadOnlyList<IPlacementTarget> targets, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems)
        {
            var dtos = new List<BuildMenuEntryDto>();
            var categoryMaster = MasterHolder.BuildMenuCategoryMaster;

            // 所持集計は全エントリで共有する（エントリごとの再走査を避ける）
            // The held tally is shared across every entry, avoiding a rescan per entry
            var heldByItem = ConstructionMaterialHeldCounts.Tally(inventoryItems);
```

`RequiredItems = CreateRequiredItemDtos(target),` を次へ置き換える。

```csharp
                    RequiredItems = BuildMenuMaterialAvailability.CreateRequiredItemDtos(target, walletQuery, heldByItem),
```

`#region Internal` 内のローカル関数 `CreateRequiredItemDtos` は不要になるため、その定義（コメント2行を含む）を削除する。

- [ ] **Step 8: ワイヤ正準形（fixture）を更新する**

Modify `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs` の `BuildMenuMatchesFixture`: 必要素材を持つ2エントリの `RequiredItems` を次へ置き換える。

```csharp
                    new() { Id = "30000000-0000-4000-8000-000000000001", Kind = "block", CategoryGuid = "10000000-0000-4000-8000-000000000001", SubCategoryGuid = "20000000-0000-4000-8000-000000000001", RequiredItems = new List<BuildMenuRequiredItemDto> { new() { ItemId = 3, Count = 5, Held = 2, Lacking = true } }, SetPlacement = new BuildMenuSetPlacementDto { PerCost = 3, Remaining = 2 }, IconUrl = "/api/block-icons/1.png" },
                    new() { Id = "8f9c2a51-0000-4000-8000-000000000001", Kind = "trainCar", CategoryGuid = "10000000-0000-4000-8000-000000000002", SubCategoryGuid = "20000000-0000-4000-8000-000000000003", RequiredItems = new List<BuildMenuRequiredItemDto> { new() { ItemId = 7, Count = 2, Held = 4, Lacking = false } }, IconUrl = "/api/train-car-icons/8f9c2a51-0000-4000-8000-000000000001.png" },
```

Modify `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/build_menu_snapshot.json`: `itemId: 3` の要素を

```json
        {
          "itemId": 3,
          "count": 5,
          "held": 2,
          "lacking": true
        }
```

へ、`itemId: 7` の要素を

```json
        {
          "itemId": 7,
          "count": 2,
          "held": 4,
          "lacking": false
        }
```

へ置き換える。

- [ ] **Step 9: コンパイルとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BuildMenuEntryDtoFactoryTest|WireContractTest"`
Expected: 全PASS

- [ ] **Step 10: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu moorestech_client/Assets/Scripts/Client.Tests/WebUi
git commit -m "feat: build_menu.entries へ素材の所持数と不足フラグを載せる"
```

---

### Task 2: 所持変化での再配信

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuInventoryRepublishGate.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:156`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BuildMenuTopicRepublishTest.cs`

**Interfaces:**
- Consumes: Task 1 の `BuildMenuEntryDtoFactory.CreateDtos(PlacementTargetResolver, ConstructionWalletQuery, IEnumerable<IItemStack>)`、`Client.Game.InGame.UI.Inventory.Main.LocalPlayerInventoryController.LocalPlayerInventory : ILocalPlayerInventory`、`LocalPlayerInventoryController.OnInventoryRefreshed : IObservable<Unit>`、`ILocalPlayerInventory.OnItemChange : IObservable<int>`
- Produces: `BuildMenuTopic(WebSocketHub hub, UIStateControl uiStateControl, ClientBlueprintLibrary blueprintLibrary, PlacementTargetResolver placementTargetResolver, ConstructionWalletQuery constructionWalletQuery, LocalPlayerInventoryController inventoryController)` と `public class BuildMenuInventoryRepublishGate` の `public bool ShouldRepublish()`

- [ ] **Step 1: 失敗するテストを書く**

Create `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BuildMenuTopicRepublishTest.cs`:

```csharp
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Game.Topics.BuildMenu;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// 所持変化の再配信ゲート（ビルドメニュー表示中のみ）の回帰試験
    /// Regression test for the inventory-change republish gate (only while the build menu is up)
    /// </summary>
    public class BuildMenuTopicRepublishTest
    {
        [Test]
        public void 所持変化の再配信はビルドメニュー表示中だけ行う()
        {
            var controlObject = new GameObject("BuildMenuTopicRepublishTest.Control");
            var control = controlObject.AddComponent<UIStateControl>();

            try
            {
                SetCurrentState(control, UIStateEnum.GameScreen);
                var gate = new BuildMenuInventoryRepublishGate(control);
                Assert.IsFalse(gate.ShouldRepublish());

                SetCurrentState(control, UIStateEnum.BuildMenu);
                Assert.IsTrue(gate.ShouldRepublish());
            }
            finally
            {
                Object.DestroyImmediate(controlObject);
            }

            #region Internal

            void SetCurrentState(UIStateControl target, UIStateEnum state)
            {
                typeof(UIStateControl).GetProperty(nameof(UIStateControl.CurrentState)).SetValue(target, state);
            }

            #endregion
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BuildMenuTopicRepublishTest"`
Expected: コンパイルエラー（`BuildMenuInventoryRepublishGate` が存在しない）

- [ ] **Step 3: 再配信ゲートを実装する**

Create `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuInventoryRepublishGate.cs`:

```csharp
using Client.Game.InGame.UI.UIState;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// 所持変化での再配信可否。閉じている間の所持変化は次の入場で拾い直す
    /// Whether an inventory change should republish; changes while closed are picked up on the next entry
    /// </summary>
    public class BuildMenuInventoryRepublishGate
    {
        private readonly UIStateControl _uiStateControl;

        public BuildMenuInventoryRepublishGate(UIStateControl uiStateControl)
        {
            _uiStateControl = uiStateControl;
        }

        public bool ShouldRepublish()
        {
            return _uiStateControl.CurrentState == UIStateEnum.BuildMenu;
        }
    }
}
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BuildMenuTopicRepublishTest"`
Expected: PASS

- [ ] **Step 5: BuildMenuTopic へ購読を足す**

Modify `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuTopic.cs`:

冒頭の using に `using Client.Game.InGame.UI.Inventory.Main;` を追加する。

フィールドへ次を追加する。

```csharp
        private readonly LocalPlayerInventoryController _inventoryController;
        private readonly BuildMenuInventoryRepublishGate _republishGate;
        private readonly IDisposable _inventorySubscription;
```

コンストラクタを次へ置き換える。

```csharp
        public BuildMenuTopic(WebSocketHub hub, UIStateControl uiStateControl, ClientBlueprintLibrary blueprintLibrary, PlacementTargetResolver placementTargetResolver, ConstructionWalletQuery constructionWalletQuery, LocalPlayerInventoryController inventoryController)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;
            _blueprintLibrary = blueprintLibrary;
            _placementTargetResolver = placementTargetResolver;
            _constructionWalletQuery = constructionWalletQuery;
            _inventoryController = inventoryController;
            _republishGate = new BuildMenuInventoryRepublishGate(uiStateControl);

            // 入場・BP更新・残数変化で再配信
            // Republish on entry, BP updates, and remaining-count changes
            _uiStateControl.OnStateChanged += OnStateChanged;
            _librarySubscription = _blueprintLibrary.OnChanged.Subscribe(_ => SchedulePublish());
            _remainingSubscription = _constructionWalletQuery.OnWalletChanged.Subscribe(_ => SchedulePublish());

            // 不足判定は所持数に依存するため、表示中の所持変化でも配り直す（前例 ResearchTopic）
            // Shortage depends on holdings, so republish on inventory moves while the menu is up (precedent: ResearchTopic)
            _inventorySubscription = new CompositeDisposable(
                inventoryController.LocalPlayerInventory.OnItemChange.Subscribe(_ => SchedulePublishWhileBuildMenuActive()),
                inventoryController.OnInventoryRefreshed.Subscribe(_ => SchedulePublishWhileBuildMenuActive()));
        }
```

`Dispose` へ `_inventorySubscription.Dispose();` を追加する。

`SchedulePublish` の直前に次のメソッドを追加する。

```csharp
        // 閉じている間の所持変化は次の入場時の再配信で足りる
        // Inventory moves while the menu is closed are covered by the republish on the next entry
        private void SchedulePublishWhileBuildMenuActive()
        {
            if (!_republishGate.ShouldRepublish()) return;
            SchedulePublish();
        }
```

`BuildJson` の `Entries` 行を次へ置き換える。

```csharp
                Entries = BuildMenuEntryDtoFactory.CreateDtos(_placementTargetResolver, _constructionWalletQuery, _inventoryController.LocalPlayerInventory),
```

- [ ] **Step 6: DI 配線を更新する**

Modify `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:156`: 該当行を次へ置き換える（`controller` は同ファイル50行目で解決済みの `LocalPlayerInventoryController`）。

```csharp
            var buildMenuTopic = new BuildMenuTopic(hub, uiStateControl, blueprintLibrary, placementTargetResolver, constructionWalletQuery, controller);
```

- [ ] **Step 7: コンパイルとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BuildMenu|WireContractTest"`
Expected: 全PASS

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests/WebUi
git commit -m "feat: ビルドメニュー表示中の所持変化で build_menu.entries を再配信する"
```

---

### Task 3: Web 側ワイヤ契約の追従

**Files:**
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.ts:7-10`
- Test: `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.test.ts`

**Interfaces:**
- Consumes: Task 1 が配信する `held: number` / `lacking: boolean`
- Produces: `BuildMenuRequiredItem` 型が `{ itemId: number; count: number; held: number; lacking: boolean }` になる

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_web/webui/src/bridge/contract/schemas/buildMenu.test.ts` の `describe("BuildMenuEntryDataSchema", ...)` 末尾に次を追加する。

```ts
  it("必要アイテムはheldとlackingを必須で持つ", () => {
    const entry = BuildMenuEntryDataSchema.parse({
      id: "30000000-0000-4000-8000-000000000001",
      kind: "block",
      categoryGuid: "10000000-0000-4000-8000-000000000001",
      subCategoryGuid: "20000000-0000-4000-8000-000000000001",
      requiredItems: [{ itemId: 3, count: 5, held: 2, lacking: true }],
    });
    assert(entry.kind === "block");
    expect(entry.requiredItems[0].held).toBe(2);
    expect(entry.requiredItems[0].lacking).toBe(true);
  });

  it("held/lackingを欠いた必要アイテムは拒否する", () => {
    expect(() => BuildMenuEntryDataSchema.parse({
      id: "30000000-0000-4000-8000-000000000001",
      kind: "block",
      categoryGuid: "10000000-0000-4000-8000-000000000001",
      subCategoryGuid: "20000000-0000-4000-8000-000000000001",
      requiredItems: [{ itemId: 3, count: 5 }],
    })).toThrow();
  });
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npm run test -- buildMenu.test`
Expected: 「held/lackingを欠いた必要アイテムは拒否する」が FAIL（現スキーマは追加キーを無視し、欠落も許す）

- [ ] **Step 3: スキーマを更新する**

Modify `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.ts`: `BuildMenuRequiredItemSchema` を次へ置き換える。

```ts
// held/lacking はホストが財布判定まで済ませた結果。web は再計算せずそのまま読む
// held/lacking arrive already settled by the host's wallet decision; the web reads them without recomputing
export const BuildMenuRequiredItemSchema = z.object({
  itemId: z.number().int(),
  count: z.number().int(),
  held: z.number().int().min(0),
  lacking: z.boolean(),
});
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npm run test -- buildMenu.test wireContract.test`
Expected: 全PASS（`wireContract.test.ts` は Task 1 で更新した fixture を読む）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_web/webui/src/bridge/contract/schemas
git commit -m "feat: build_menu 必要アイテム契約へ held/lacking を必須追加"
```

---

### Task 4: 表示文言の追加

**Files:**
- Modify: `Localization/localization.csv`
- Modify: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（生成物）

**Interfaces:**
- Produces: `L.ui.buildMenu.materialShortageTitle` / `L.ui.buildMenu.materialShortageLine` / `L.ui.buildMenu.materialTooltip`

- [ ] **Step 1: CSV へ3行追加する**

Modify `Localization/localization.csv`: `ui.buildMenu.remainingPlacementCount` の行（59行目）の直後に次の3行を挿入する。

```csv
ui.buildMenu.materialShortageTitle,Not enough materials,Not enough materials,素材が足りません,Nicht genug Materialien
ui.buildMenu.materialShortageLine,{itemName} {ownedCount}/{requiredCount},{itemName} {ownedCount}/{requiredCount},{itemName} {ownedCount}/{requiredCount},{itemName} {ownedCount}/{requiredCount}
ui.buildMenu.materialTooltip,{itemName}\nOwned: {ownedCount}\nRequired: {requiredCount},{itemName}\nOwned: {ownedCount}\nRequired: {requiredCount},{itemName}\n所持数: {ownedCount}\n必要数: {requiredCount},{itemName}\nBestand: {ownedCount}\nBenötigt: {requiredCount}
```

- [ ] **Step 2: 生成キーを再生成する**

Run: `cd moorestech_web/webui && npm run gen:i18n`
Expected: `src/shared/i18n/generated/localizationKeys.ts` に3キーが現れる

- [ ] **Step 3: 鮮度テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npm run test -- localizationKeysFreshness allScreensI18n`
Expected: PASS

- [ ] **Step 4: Unity 側の生成キーも同期する**

Run: `uloop compile --project-path ./moorestech_client --force-recompile`
Expected: エラー0件（CSV 追加で `Mooresmaster.Localization.Generated.LocalizationKeys` が再生成される。触っていないキーの CS0117 が出た場合は CSV 再生成漏れなので、このコマンドをもう一度実行する）

- [ ] **Step 5: コミットする**

```bash
git add Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated
git commit -m "feat: ビルドメニュー素材不足の表示文言3キーを追加"
```

---

### Task 5: 詳細サイドバーの不足表現

**Files:**
- Modify: `moorestech_web/webui/src/shared/materialTooltipText.ts:7`
- Modify: `moorestech_web/webui/src/features/buildMenu/BuildMenuDetailSidebar.tsx`
- Modify: `moorestech_web/webui/src/features/buildMenu/style.module.css`
- Test: `moorestech_web/webui/src/features/buildMenu/BuildMenuDetailSidebar.test.ts`

**Interfaces:**
- Consumes: Task 3 の `BuildMenuRequiredItem`（`held` / `lacking`）、Task 4 の `L.ui.buildMenu.materialTooltip`、既存 `useMaterialTooltipText`、`L.ui.recipe.itemCountSummary`
- Produces: `MaterialTooltipKey` が `typeof L.ui.recipe.materialTooltip | typeof L.ui.research.consumeItemTooltip | typeof L.ui.buildMenu.materialTooltip | typeof L.ui.buildMenu.materialShortageLine` になる

- [ ] **Step 1: 失敗するテストを書く**

Create `moorestech_web/webui/src/features/buildMenu/BuildMenuDetailSidebar.test.ts`:

```ts
import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BuildMenuDisplayEntry } from "./buildMenuGrouping";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
  useItemNameResolver: () => (itemId: number) => `item-${itemId}`,
}));
// MantineProvider依存（Tooltip等）を避けるため共有UIはスタブにする
// Stub the shared UI to avoid MantineProvider dependencies (Tooltip, etc.)
vi.mock("@/shared/ui", () => ({
  FadeRule: () => createElement("mock-fade-rule"),
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  SlotGrid: ({ children }: { children: unknown }) => createElement("mock-slot-grid", null, children as never),
}));

import { BuildMenuDetailSidebar } from "./BuildMenuDetailSidebar";

const entry = (lacking: boolean, held: number): BuildMenuDisplayEntry => ({
  id: "30000000-0000-4000-8000-000000000001",
  kind: "block",
  categoryGuid: "10000000-0000-4000-8000-000000000001",
  subCategoryGuid: "20000000-0000-4000-8000-000000000001",
  requiredItems: [{ itemId: 3, count: 5, held, lacking }],
  displayLabel: "belt",
}) as BuildMenuDisplayEntry;

describe("BuildMenuDetailSidebar", () => {
  it("不足素材は赤枠と赤字の所持/必要を出す", () => {
    const tree = create(createElement(BuildMenuDetailSidebar, { entry: entry(true, 2) })).toJSON();
    const json = JSON.stringify(tree);
    expect(json).toContain('"insufficient":true');
    expect(json).toContain('"data-lack":true');
    // 必要数バッジ(count)は廃止し、所持/必要のテキストへ置き換わっている
    // The required-count badge is gone, replaced by the owned/required text
    expect(json).not.toContain('"count":5');
  });

  it("残りが賄う素材は赤くしない", () => {
    const tree = create(createElement(BuildMenuDetailSidebar, { entry: entry(false, 0) })).toJSON();
    const json = JSON.stringify(tree);
    expect(json).toContain('"insufficient":false');
    expect(json).not.toContain('"data-lack":true');
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npm run test -- BuildMenuDetailSidebar`
Expected: FAIL（`insufficient` も `data-lack` も出ておらず、`"count":5` が残っている）

- [ ] **Step 3: 素材ツールチップキーの union を広げる**

Modify `moorestech_web/webui/src/shared/materialTooltipText.ts`: `MaterialTooltipKey` を次へ置き換える。

```ts
export type MaterialTooltipKey =
  | typeof L.ui.recipe.materialTooltip
  | typeof L.ui.research.consumeItemTooltip
  | typeof L.ui.buildMenu.materialTooltip
  | typeof L.ui.buildMenu.materialShortageLine;
```

- [ ] **Step 4: CSS を追加する**

Modify `moorestech_web/webui/src/features/buildMenu/style.module.css`: `.detailHint,` ブロックの直後に次を追加する。

```css
/* 必要素材1枠。所持/必要の数値を絶対配置で載せる（ResearchDetailPaneの.consumeSlot同型） */
/* One required-material cell; the owned/required numbers sit absolutely over it (same as ResearchDetailPane's .consumeSlot) */
.materialSlot {
  position: relative;
}

.materialCount {
  position: absolute;
  right: -2px;
  bottom: -4px;
  z-index: 2;
  color: var(--count-text);
  font-size: 11px;
  line-height: 1;
  white-space: nowrap;
}

.materialCount[data-lack="true"] {
  color: var(--text-insufficient);
}
```

- [ ] **Step 5: サイドバーを research/craft 同型へ置き換える**

Modify `moorestech_web/webui/src/features/buildMenu/BuildMenuDetailSidebar.tsx`: 冒頭の import に次を追加する。

```tsx
import { useMaterialTooltipText } from "@/shared/materialTooltipText";
```

`const { t } = useI18n();` の直後に次を追加する。

```tsx
  const materialTooltipText = useMaterialTooltipText();
```

`<SlotGrid cols={3}>` の中身を次へ置き換える。

```tsx
                {entry.requiredItems.map((item) => (
                  <div key={item.itemId} className={styles.materialSlot}>
                    {/* 不足判定はホストのlackingが唯一の正。所持と必要の比較をここでやり直さない */}
                    {/* The host's lacking flag is the sole authority; no owned-vs-required comparison happens here */}
                    <ItemSlot
                      itemId={item.itemId}
                      insufficient={item.lacking}
                      tooltip={<span style={{ whiteSpace: "pre-line" }}>
                        {materialTooltipText(L.ui.buildMenu.materialTooltip, item.itemId, item.count, new Map([[item.itemId, item.held]]))}
                      </span>}
                    />
                    <span className={`iconTextOutlineLight ${styles.materialCount}`} data-lack={item.lacking || undefined}>
                      {t(L.ui.recipe.itemCountSummary, { ownedCount: item.held, requiredCount: item.count })}
                    </span>
                  </div>
                ))}
```

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npm run test -- BuildMenuDetailSidebar`
Expected: PASS

Run: `cd moorestech_web/webui && npm run lint && npx tsc -b`
Expected: エラー0件

- [ ] **Step 7: コミットする**

```bash
git add moorestech_web/webui/src/features/buildMenu moorestech_web/webui/src/shared/materialTooltipText.ts
git commit -m "feat: ビルドメニュー詳細サイドバーの必要素材を所持/必要と不足赤枠で出す"
```

---

### Task 6: エントリスロットの不足ツールチップ

**Files:**
- Create: `moorestech_web/webui/src/features/buildMenu/buildMenuShortage.ts`
- Create: `moorestech_web/webui/src/features/buildMenu/buildMenuShortage.test.ts`
- Modify: `moorestech_web/webui/src/features/buildMenu/BuildMenuSlot.tsx`
- Test: `moorestech_web/webui/src/features/buildMenu/BuildMenuSlot.test.ts`

**Interfaces:**
- Consumes: Task 3 の `BuildMenuRequiredItem`、Task 4 の `L.ui.buildMenu.materialShortageTitle` / `L.ui.buildMenu.materialShortageLine`、Task 5 で union を広げた `useMaterialTooltipText`、既存 `HoverTooltip`
- Produces: `shortageItemsOf(entry: BuildMenuDisplayEntry): BuildMenuRequiredItem[]`

- [ ] **Step 1: 失敗するテストを書く（純関数）**

Create `moorestech_web/webui/src/features/buildMenu/buildMenuShortage.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import type { BuildMenuDisplayEntry } from "./buildMenuGrouping";
import { shortageItemsOf } from "./buildMenuShortage";

const entryWith = (requiredItems: BuildMenuDisplayEntry["requiredItems"]): BuildMenuDisplayEntry => ({
  id: "30000000-0000-4000-8000-000000000001",
  kind: "block",
  categoryGuid: "10000000-0000-4000-8000-000000000001",
  subCategoryGuid: "20000000-0000-4000-8000-000000000001",
  requiredItems,
  displayLabel: "belt",
}) as BuildMenuDisplayEntry;

describe("shortageItemsOf", () => {
  it("lackingの立った素材だけを配信順で返す", () => {
    const items = shortageItemsOf(entryWith([
      { itemId: 3, count: 5, held: 2, lacking: true },
      { itemId: 4, count: 1, held: 9, lacking: false },
      { itemId: 5, count: 3, held: 0, lacking: true },
    ]));
    expect(items.map((item) => item.itemId)).toEqual([3, 5]);
  });

  it("不足が無ければ空配列を返す", () => {
    expect(shortageItemsOf(entryWith([{ itemId: 3, count: 5, held: 9, lacking: false }]))).toEqual([]);
  });

  it("必要素材を持たないエントリは空配列を返す", () => {
    expect(shortageItemsOf(entryWith([]))).toEqual([]);
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npm run test -- buildMenuShortage`
Expected: FAIL（`./buildMenuShortage` が解決できない）

- [ ] **Step 3: 純関数を実装する**

Create `moorestech_web/webui/src/features/buildMenu/buildMenuShortage.ts`:

```ts
import type { BuildMenuRequiredItem } from "@/bridge";
import type { BuildMenuDisplayEntry } from "./buildMenuGrouping";

// 不足の正本はホストのlacking。ここは絞り込むだけで判定しない
// The host's lacking flag is the source of truth; this only filters and never decides
export function shortageItemsOf(entry: BuildMenuDisplayEntry): BuildMenuRequiredItem[] {
  return entry.requiredItems.filter((item) => item.lacking);
}
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npm run test -- buildMenuShortage`
Expected: PASS

- [ ] **Step 5: 失敗するテストを書く（スロット描画）**

Create `moorestech_web/webui/src/features/buildMenu/BuildMenuSlot.test.ts`:

```ts
import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BuildMenuDisplayEntry } from "./buildMenuGrouping";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
  useItemNameResolver: () => (itemId: number) => `item-${itemId}`,
}));
vi.mock("@/shared/ui", () => ({
  HoverTooltip: (props: object) => createElement("mock-hover-tooltip", props),
  PlacementTargetFace: (props: object) => createElement("mock-placement-target-face", props),
  SlotFrame: (props: object) => createElement("mock-slot-frame", props),
}));
vi.mock("@/features/hotbar", () => ({ useHotbarDragSource: () => ({}) }));

import { BuildMenuSlot } from "./BuildMenuSlot";

const entryWith = (requiredItems: BuildMenuDisplayEntry["requiredItems"]): BuildMenuDisplayEntry => ({
  id: "30000000-0000-4000-8000-000000000001",
  kind: "block",
  categoryGuid: "10000000-0000-4000-8000-000000000001",
  subCategoryGuid: "20000000-0000-4000-8000-000000000001",
  requiredItems,
  displayLabel: "belt",
}) as BuildMenuDisplayEntry;

const render = (entry: BuildMenuDisplayEntry) => JSON.stringify(create(createElement(BuildMenuSlot, {
  entry,
  onLeftClick: () => undefined,
  onHoverChange: () => undefined,
})).toJSON());

describe("BuildMenuSlot", () => {
  it("不足時は見出しと不足行だけをツールチップに出す", () => {
    const json = render(entryWith([
      { itemId: 3, count: 5, held: 2, lacking: true },
      { itemId: 4, count: 1, held: 9, lacking: false },
    ]));
    expect(json).toContain("ui.buildMenu.materialShortageTitle");
    expect(json).toContain("ui.buildMenu.materialShortageLine");
    expect(json).toContain("item-3");
    expect(json).not.toContain("item-4");
    expect(json).toContain('"disabled":false');
  });

  it("充足時はツールチップを無効にする", () => {
    const json = render(entryWith([{ itemId: 3, count: 5, held: 9, lacking: false }]));
    expect(json).toContain('"disabled":true');
  });

  it("不足していてもスロットに赤枠を付けない", () => {
    const json = render(entryWith([{ itemId: 3, count: 5, held: 2, lacking: true }]));
    expect(json).not.toContain('"insufficient":true');
  });
});
```

- [ ] **Step 6: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npm run test -- BuildMenuSlot`
Expected: FAIL（`mock-hover-tooltip` が描画されない）

- [ ] **Step 7: エントリスロットへツールチップを足す**

Modify `moorestech_web/webui/src/features/buildMenu/BuildMenuSlot.tsx`: 全体を次へ置き換える。

```tsx
import { HoverTooltip, PlacementTargetFace, SlotFrame } from "@/shared/ui";
import { tutorialAnchor, buildMenuEntryAnchorId } from "@/shared/tutorialAnchor";
import { useHotbarDragSource } from "@/features/hotbar";
import { L, useI18n } from "@/shared/i18n";
import { useMaterialTooltipText } from "@/shared/materialTooltipText";
import { shortageItemsOf } from "./buildMenuShortage";
import type { BuildMenuDisplayEntry } from "./buildMenuGrouping";

type Props = {
  entry: BuildMenuDisplayEntry;
  onLeftClick: () => void;
  // BPエントリのみ右クリック削除を受け付ける
  // Only blueprint entries accept right-click deletion
  onRightClick?: () => void;
  onHoverChange: (hovering: boolean) => void;
};

// アイコン有無で画像/テキストを出し分け
// 左押下はホットバーD&D共通制御を通す
// One build-menu slot, rendering an image or a text label depending on icon presence.
// The left press routes through the shared hotbar-D&D pointer control (tap = select, past-threshold drag = a hotbar-assign drag source)
export function BuildMenuSlot({ entry, onLeftClick, onRightClick, onHoverChange }: Props) {
  const { t } = useI18n();
  const materialTooltipText = useMaterialTooltipText();
  const dragHandlers = useHotbarDragSource({ kind: "buildMenuEntry", id: entry.id }, onLeftClick);

  // 不足がある時だけ、見出し1行＋不足素材行のツールチップを出す
  // Only when something is short: a heading line plus one line per missing material
  const shortages = shortageItemsOf(entry);
  const shortageTooltip = (
    <span style={{ whiteSpace: "pre-line" }}>
      {[t(L.ui.buildMenu.materialShortageTitle)]
        .concat(shortages.map((item) => materialTooltipText(L.ui.buildMenu.materialShortageLine, item.itemId, item.count, new Map([[item.itemId, item.held]]))))
        .join("\n")}
    </span>
  );

  return (
    <HoverTooltip label={shortageTooltip} disabled={shortages.length === 0}>
      <SlotFrame
        filled
        testId={`build-menu-entry-${entry.kind}-${entry.id}`}
        onRightDown={onRightClick}
        onHoverChange={onHoverChange}
        {...dragHandlers}
        {...tutorialAnchor(buildMenuEntryAnchorId(entry.kind, entry.id))}
      >
        <PlacementTargetFace iconUrl={entry.iconUrl} displayName={entry.displayLabel} />
      </SlotFrame>
    </HoverTooltip>
  );
}
```

- [ ] **Step 8: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npm run test`
Expected: 全PASS

Run: `cd moorestech_web/webui && npm run lint && npx tsc -b`
Expected: エラー0件

- [ ] **Step 9: 実機で確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

unity-playmode-recorded-playtest スキルのプレイテストDSL（`scripts/run-scenario.sh`）で PlayMode を起動し、B キーでビルドメニューを開いてエントリをホバーする。素材が足りないエントリでツールチップに見出しと不足行が出ること、足りているエントリで出ないこと、詳細サイドバーに `所持/必要` と赤枠が出ることを録画で確認する。

- [ ] **Step 10: コミットする**

```bash
git add moorestech_web/webui/src/features/buildMenu
git commit -m "feat: ビルドメニューのエントリホバーで素材不足をツールチップ表示する"
```

---

### Task 7: 全ブランチレビュー（必須・省略不可）

**Files:**
- 参照: 本ブランチの全差分

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

moores-code-review スキルを起動し、本ブランチの全差分をレビューする。これは無条件に実行する必須ゲートであり、「変更が小さい」「テストが通った」を理由に省略してはならない。

- [ ] **Step 2: 指摘へ対応する**

機械的修正は適用し、設計判断は AskUserQuestion でユーザーへ諮る。対応後に `uloop compile --project-path ./moorestech_client` と `cd moorestech_web/webui && npm run test` を再実行して緑を確認する。

- [ ] **Step 3: コミットする**

```bash
git add -A
git commit -m "fix: moores-code-review の指摘へ対応"
```

---

## 配置と前例（spec-architecture-review 結果）

**データフロー地図（既存パイプラインへの相乗り）**

```
所持インベントリ / 財布 / 解放状態
  → BuildMenuEntryDtoFactory（写像）
  → BuildMenuTopic（build_menu.entries を push）
  → web の useTopic ストア
  → BuildMenuPanel / BuildMenuSlot / BuildMenuDetailSidebar（描画）
```

本planが足すコンポーネントの立ち位置は全て**書き手または読み手**であり、既存フローへ分岐・逆流・並行経路（`bool` 戻り値による制御返し、共有モデルを迂回する第2の書き込み経路、フレーム駆動へのイベント混入）を一切足さない。

| 追加/変更する項目 | 配置先 | 前例（役割同型） |
|---|---|---|
| `ConstructionMaterialHeldCounts`（所持集計） | `Client.Game/InGame/BlockSystem/PlaceSystem/Util/` | 同ディレクトリの `ConstructionCostShortageCalculator`（同じ集計を内包していた元の持ち主） |
| `BuildMenuMaterialAvailability`（必要素材→DTO写像＋不足判定） | `Client.WebUiHost/Game/Topics/BuildMenu/` | 同ディレクトリの `BuildMenuEntryDtoFactory.ResolveSetPlacement`（財布へ問い合わせて表示用DTOを作る同役割） |
| `BuildMenuRequiredItemDto.Held` / `.Lacking` | `BuildMenuDtos.cs` | 同ファイルの `BuildMenuSetPlacementDto`（「判定はホスト側の財布が済ませる」と明記された同型フィールド） |
| 所持変化での再配信（UniRx購読） | `BuildMenuTopic` | `ResearchTopic`（`LocalPlayerInventory.OnItemChange` + `OnInventoryRefreshed` を購読し、画面表示中だけ取り直す） |
| `BuildMenuInventoryRepublishGate`（表示中ゲート） | `Client.WebUiHost/Game/Topics/BuildMenu/` | `ResearchTopic.RefreshWhileResearchScreenActive`（同じ「閉じている間は次の入場で足りる」判断） |
| 素材ツールチップの書式化 | `moorestech_web/webui/src/shared/materialTooltipText.ts` の union 拡張 | `ResearchDetailPane` / `CraftRecipeEntry`（同hookを共有する2つの前例。裁定 2026-08-19） |
| 不足時の赤枠・所持/必要テキスト | `BuildMenuDetailSidebar.tsx` + `style.module.css` | `ResearchDetailPane` の `.consumeSlot` / `.consumeCount`（`data-lack` と `--text-insufficient` を使う同型） |

**機構選択（受動的統合を採用）**

所持変化への追従は、既存の `BuildMenuTopic` の配信機構を無傷のまま活かし、購読を1本足して同じ `SchedulePublish`（`PostLateUpdate` デバウンス）へ流す**受動的統合**を採る。既存の入場・BP更新・財布変化のトリガーは一切変更しない。能動介入案（ビルドメニュー表示中だけ毎フレーム所持を突き合わせて差分検知する、あるいは所持変化時に web へ差分パッチを送る専用経路を新設する）は、`Update()` 内の同値判定を禁じる設計原則に反し、`ResearchTopic` という役割同型の前例が受動側にあるため採らない。

**機能パリティ（死活表）**

| 現在ユーザーが使える操作 | plan後も生きるか | 根拠 |
|---|---|---|
| ビルドメニューのエントリ左クリック選択 | 生きる | `BuildMenuSlot` は `HoverTooltip` でラップするだけ。`HoverTooltip` は子を `cloneElement` するのでDOM構造とハンドラは不変 |
| エントリのホットバーD&D（左ドラッグ） | 生きる | `useHotbarDragSource` のハンドラを従来どおり `SlotFrame` へ展開する |
| BPエントリの右クリック削除 | 生きる | `onRightDown` の配線を変更しない |
| エントリホバーでの詳細サイドバー更新（sticky） | 生きる | `onHoverChange` の配線を変更しない |
| 詳細サイドバーの必要素材の必要数表示 | 形が変わる（退化しない） | 必要数バッジ → `所持/必要` テキスト。必要数は引き続き読め、所持数が増える。ユーザーがプレビュー付きで裁定済み |
| 詳細サイドバーの残り設置数表示 | 生きる | `setPlacement.remaining` の描画を変更しない |
| 素材不足エントリの選択・設置試行 | 生きる | エントリの外観も選択可否も変更しない（裁定どおり） |
| ホットバーからの設置 | 生きる | `features/hotbar` を変更しない |

死ぬ・退化する操作は無いため、裁定ゲートに送る項目は無い。

---

## 判断記録（ADR）

**設計セッションのADR:** `docs/adr/0041-build-menu-material-shortage-tooltip.md`
**ユーザー裁定の蒸留:**
- `.decisions/2026-08-28-ビルドメニューの素材不足はツールチップで示し見た目は変えない.md`
- `.decisions/2026-08-28-ビルドメニューの素材不足判定はホストで行い財布残りは不足としない.md`

**planning中に生じた判断:**

- **所持数集計を `ConstructionMaterialHeldCounts` へ抽出し、`ConstructionCostShortageCalculator` と共有する。**
  出所: agent前提。ビルドメニューは「必要素材全件の所持数」を要求し、既存 calculator は「不足分だけ」を返すため戻り値をそのままは使えない。所持集計だけを共有すれば「所持数の数え方」の定義が2箇所に分かれない。ADR の「既存ロジックを流用する」を、戻り値の再利用ではなく集計の共有として具体化したもの。
- **不足判定は `BuildMenuMaterialAvailability`（`Client.WebUiHost/Game/Topics/BuildMenu/`）に置く。**
  出所: agent前提。財布への問い合わせは `ConstructionWalletQuery.IsCoveredByWallet` 一本に閉じており、この클래스は「必要素材列→DTO列」の写像だけを持つ。`BuildMenuEntryDtoFactory` は169行あり、判定を内包すると200行規約を超える。
- **所持変化での再配信はビルドメニュー表示中に限る（`BuildMenuInventoryRepublishGate`）。**
  出所: agent前提。前例 `ResearchTopic` が「画面を見ていない間の所持変化は次の突入時の取り直しで足りる」として同じゲートを持つ。ADR の Consequences が挙げた配信頻度の懸念はこのゲートで閉じる。
- **エントリツールチップの不足行は新規キー `ui.buildMenu.materialShortageLine` を起こし、既存 `useMaterialTooltipText` を共有する。**
  出所: agent前提。ADR は見出しキーとサイドバー用キーのみ明記していたが、行の書式にも専用キーが要る。パラメータ形状（itemName / ownedCount / requiredCount）が既存の素材ツールチップと同一なので、`MaterialTooltipKey` の union に足して共通hookを共有する（裁定 `.decisions/2026-08-19-素材ツールチップはクラフト側も共通hookへ寄せる.md` の流儀）。
- **`ui.tooltip.placeMaterialShortage`（設置時カーソルtooltipの `{p0} {p1}/{p2}`）は流用しない。**
  出所: agent前提。位置パラメータ形式で web の名前付きパラメータ機構と噛み合わず、片方の文言変更が他方へ波及する。
