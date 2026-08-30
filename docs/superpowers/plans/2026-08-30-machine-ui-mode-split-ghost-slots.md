# 機械UI 2モード分離・スロット固定・ゴーストスロット Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 機械UI（Web UI）のタブを廃してSatisfactory方式の「レシピ選択モード⇄インベントリモード」へ分け、サーバーで入力/出力/液体スロットをレシピ順に固定し、空スロットに半透明ゴースト（配置されるべき素材/生産物）を描く。

**Architecture:** サーバーは `MachineProcessContext.BindSelectedRecipe` がレシピ選択時に入力/出力サブインベントリへ束縛レシピをプッシュし（`SetHoge`プッシュ規約）、`VanillaMachineInputInventory` / `VanillaMachineOutputInventory` が「スロットi＝素材i / 生産物j」を `InsertItem`・`InsertionCheck`・`ReplaceItem`・液体 `AddLiquid` で強制する。未選択は全拒否。Web UIは既存 `block_inventory.current`（選択GUID・スロット配置）と `crafting.machine_recipes`（レシピ内容。液体を追加）から表示スロット数とゴースト内容を導出し、新規ワイヤ・新プロトコルは作らない。レシピ選択画面は共有 `RecipeRow` を流用した行リストへ置換する。

**Tech Stack:** C# (Unity, NUnit) / React + TypeScript + Mantine + CSS Modules + zod / vitest + react-test-renderer / Playwright e2e (mock-host)

**bd:** `moorestech-j2kx`

## Requirements

設計ADR: `docs/adr/0042-machine-ui-satisfactory-mode-split-and-ghost-slots.md`。裁定: `.decisions/2026-08-30-機械*.md` 6件。

- R1 タブ（`ModeSwitch`）廃止。未選択で開く→レシピ選択モード。行を選ぶ→インベントリモードへ自動遷移。受入: `machine-tab-switch` が存在せず、未選択機械で `machine-recipe-selection` が、選択済機械で `machine-inventory-body` が表示される。
- R2 インベントリモード上部に選択中レシピ表示（出力アイコン＋レシピ名（出力アイテム名）＋秒数、testId `machine-selected-recipe`）。クリックでレシピ選択モードへ戻る。受入: クリック後 `machine-recipe-selection` 表示、`machine-inventory-body` 非表示。
- R3 レシピ選択モードは行リスト。各行＝共有 `RecipeRow`（素材列→中央列は秒数＋静止矢印のみ→結果列）、行の上辺にレシピ名。行全体クリックで `machine_recipe.select set`。選択中行は `data-selected="true"`。ホバー詳細プレビュー・9列グリッド・右クリック解除は廃止。受入: 右クリックで `machine_recipe.select clear` が送られない。
- R4 レシピ解除の導線をUIに設けない（プロトコル `clear` は残す）。
- R5 サーバー: 選択中の機械は入力スロットiに素材iのみ受け入れる（`InsertItem`・`InsertionCheck`・プレイヤー移動 `ReplaceItem`/`SetItem`）。出力スロットjには生産物j（実現出力k→j=k%生産物数）のみ排出。液体は入力タンクiに `InputFluids[i]`、出力タンクjに `OutputFluids[j]`。
- R6 サーバー: 未選択の機械は入力アイテム・液体を全て拒否する。
- R7 Web: 入力＝素材数、出力＝生産物数、液体＝レシピ液体数のスロットだけ描き、余剰は非表示。
- R8 Web: 空スロットにゴースト（入力=素材、出力=生産物、液体=レシピ液体）。実物があるスロットはゴーストを出さない。ゴーストの個数バッジはレシピ必要数。
- R9 ワイヤ: `crafting.machine_recipes` に `inputFluids` / `outputFluids`（`{fluidId, fluidGuid, amount}`）を追加し、zod `.strict()`・C# DTO・全フィクスチャ・mock-hostを同時更新。
- R10 `webui-design` §8.7 を実装より先に新仕様へ改訂する。
- R11 モジュールスロット・進捗矢印・分間生産数・稼働状態フッタ・大型パネルレイアウト・レシピ0件機械の小型表示は現状維持。
- やらないこと: uGUI側の変更（2026-08-17裁定）、既存セーブの不整合補正（後方互換不要規約）、新プロトコル/イベントの新設。

## Global Constraints

- AGENTS.md 全規約（1ファイル200行以下・1ディレクトリ10ファイル以下・partial禁止・`Func<>`禁止・try-catch禁止・日英2行コメント・`#region Internal`はローカル関数のみ・デフォルト引数禁止・Action不使用でUniRx）。
- `.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client` を実行する。テストは `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"`。
- Web: `moorestech_web/webui` で `npm run test`（vitest）・`npm run lint`・`npm run typecheck`・e2e は `npm run test:e2e -- <spec>`（実コマンドは `package.json` を確認）。
- localization.csv 編集後は `npm run gen:i18n` を実行し生成物をコミットする（`localizationKeysFreshness.test.ts` が検査）。
- Web UIの様式は `.claude/skills/webui-design/SKILL.md` §4（スロット状態はdata属性、`SlotGrid`、`useSlotMouse`）と §8.17（RecipeRow）に従う。
- 作業は `moores-wt new feature/machine-ui-mode-split-ghost-slots` で作った使い捨てworktreeで行う。

---

## 事前計画レビューでの補正（2026-08-30・各タスク本文より優先する）

タスク1派遣前の事前計画レビューで検出した10件。**以下は各Taskの本文記述を上書きする拘束条件**である。

- **C1（Task 5/6・ディレクトリ規約）** `details/machine/` は現存7ファイル。plan通りに作ると11ファイルになりAGENTS.md「1ディレクトリ10ファイルまで」に違反する。レシピ選択行リスト一式（`MachineRecipeSelectionRow.tsx` / `.test.ts` / `MachineRecipeSelectionList.tsx` / `machineRecipeSelectionList.module.css`）は `details/machine/recipeSelection/` サブディレクトリへ置く。`SelectedRecipeHeader.tsx` と `machineSlotGhosts.ts`(+`.test.ts`) は `details/machine/` 直下でよい。
- **C2（Task 1・出力束縛／ユーザー裁定 2026-08-30）** R5の「出力スロット j = k % 生産物数」は**ItemId一致では判定しない**。`MachineOutputFactoryUtil.CreateRealizedOutputs` はベースセットと追加出力セットで `ApplyQualityLevel` を独立に抽選するため、実現出力 k と k+生産物数 が別ItemIdになり得るからである。スロット j は「**生産物 j のレベルファミリーに属するアイテム**」を受け入れる枠とし、既存物とスタックできない変種が来た場合は「スロットが埋まっている」扱いで `CanStoreOutputs` を false にして機械を待機させる（既存の出力詰まり挙動）。判定に必要な `Core.Master.ItemMaster` へ `public IReadOnlyList<ItemId> GetLevelVariants(ItemId baseItemId)`（ファミリー無しなら `baseItemId` 1件）を追加し、含有判定は `MachineRecipeSlotBindingUtil` 側で行う。裁定: `.decisions/2026-08-30-機械の出力スロットはレベルファミリー枠として束縛し変種違いは待機させる.md`
- **C3（Task 2・液体テスト）** `VanillaMachineFluidInventoryComponent.GetFluidInventory()` は `Amount > 0` のタンクだけを返すためタンク番号順ではない。液体束縛テストは戻り値の index で読まず、入力インベントリが保持する生タンク列（`FluidInputSlot` 等）を index で読むこと。
- **C4（Task 2・液体テスト）** plan L567-578 の後半アサートは `AddLiquid` より前に取得したスナップショットを判定しており常に成立する死んだアサート。`AddLiquid` 後に再取得したタンク列で「束縛外タンクが空のまま」を検証する形へ書き換える（`designatedRemainder` の検証だけで済ませない）。
- **C5（Task 3・型ゲート）** `MachineRecipeSelectionTab.test.ts` は `MachineRecipe` のファクトリを持つため、`inputFluids`/`outputFluids` 必須化で `npm run typecheck` が落ちる。Task 3 で同ファイルのファクトリにも `inputFluids: [], outputFluids: []` を足す（Task 6 でファイルごと削除されるが型ゲートを通すため必要）。
- **C6（Task 1/2・200行規約）** `VanillaMachineBlockInventoryComponent.cs`(198行) / `VanillaMachineProcessorComponent.cs`(200行) / `VanillaMachineFluidInventoryComponent.cs`(217行) は追記で200行を超える。**束縛ガードを別クラス（例 `Inventory/` 配下の専用クラス）へ切り出して200行以下を守る**こと。partial は禁止。
- **C7（Task 2・返却の乖離）** `MachineRecipeRefundUtil.CanRefundAllItems` は汎用 `OpenableInventoryItemDataStoreService`（空きスロットならどこでも可）でシミュレートするのに、`ExecuteRefund` が呼ぶ `input.InsertItem` は本planで束縛規則へ変わるため、乖離時に `Debug.LogError("返却シミュレーションと実挿入の乖離でアイテムが消失した")` へ落ちる。**`CanRefundAllItems` のシミュレーションも束縛規則で行うよう Task 2 で修正し、回帰テストを1本足す**。
- **C8（Task 1・Step 8のExpected）** 「全PASS」と「`ReplaceItemIntoWrongSlotIsRejected` は FAIL のままでよい」が同一行で矛盾している。Expected は「**`ReplaceItemIntoWrongSlotIsRejected` 以外は全PASS**」に一本化する（同テストは Task 2 で通す）。
- **C9（Task 6・代表出力ガード）** 現行 `buildMachineRecipeSelectionRows` は代表アイコンが取れないレシピを `flatMap` で除外している。新版でも**代表出力の存在ガードを残す**こと（`filter(blockGuid)` だけにしない）。`SelectedRecipeHeader` の `itemId ?? 0` フォールバックは作らず、代表出力が無いレシピはヘッダを出さない。
- **C10（Task 5・液体スロットのクランプ）** レシピの液体数が機械の実タンク数を超えると `data.fluidSlots[i]` が `undefined` になり `FluidSlot` が `fluid.kind` 参照で落ちる。`buildMachineSlotView` に実タンク数（`SlotLayoutDto` 由来）を渡し、`fluidIndices` を実在範囲へクランプする（「無いものは描かない」）。

---

## File Structure

サーバー（`moorestech_server/Assets/Scripts/`）
- Create `Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeSlotBindingUtil.cs` — レシピからスロット番号を引く純関数（MasterHolder読取のみ）
- Modify `Game.Block/Blocks/Machine/State/MachineProcessContext.cs` — `SelectedRecipe` を `{get; private set;}` にし `BindSelectedRecipe` で入出力へプッシュ
- Modify `Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`, `Game.Block/Blocks/CleanRoom/Machine/CleanRoomMachineProcessorComponent.cs` — 直接代入を `BindSelectedRecipe` へ
- Modify `Game.Block/Blocks/Machine/Inventory/VanillaMachineInputInventory.cs` — 束縛レシピ保持、`InsertItem`/`InsertionCheck`/`IsAllowedToPlace`/`IsFluidAllowedAt`、index整列の `ReduceInputSlot`
- Modify `Game.Block/Blocks/Machine/Inventory/VanillaMachineOutputInventory.cs` — 束縛レシピ保持、index整列の `CanStoreOutputs`/`InsertOutputSlot`、`IsAllowedToPlace`
- Modify `Game.Block/Blocks/Machine/Inventory/IVanillaMachineSubInventory.cs` — `IsAllowedToPlace(int localSlot, IItemStack)` 追加
- Modify `Game.Block/Blocks/Machine/Inventory/VanillaMachineModuleInventory.cs` — `IsAllowedToPlace` は常にtrue
- Modify `Game.Block/Blocks/Machine/Inventory/VanillaMachineBlockInventoryComponent.cs` — `ReplaceItem`/`SetItem` の束縛ガード、`SortExcludedSlots` を全スロット
- Modify `Game.Block/Blocks/Machine/MachineRecipeMaster.cs` — `RecipeConfirmation` をindex整列へ
- Modify `Game.Block/Blocks/Machine/VanillaMachineFluidInventoryComponent.cs` — `AddLiquid` の受入ゲート
- Test Create `Tests/CombinedTest/Core/MachineSlotBindingTest.cs`, `Tests/CombinedTest/Core/MachineFluidSlotBindingTest.cs`
- Test Modify `Tests/CombinedTest/Server/PacketTest/RequestBlockInventoryTest.cs`, `Tests/CombinedTest/Server/PacketTest/Event/BlockInventoryUpdateEventPacketTest.cs`, ほか失敗したもの

クライアントホスト（`moorestech_client/Assets/Scripts/Client.WebUiHost/`）
- Modify `Game/Topics/MachineRecipesTopic.cs` — `InputFluids`/`OutputFluids` 追加

Web（`moorestech_web/webui/`）
- Modify `src/bridge/contract/schemas/recipes.ts` — `MachineRecipeFluidSchema`、`inputFluids`/`outputFluids`
- Modify `src/app/slotTokens.css` — `--slot-ghost-opacity`
- Modify `src/shared/ui/SlotFrame/index.tsx`, `style.module.css`, `index.test.ts` — `ghost` → `data-ghost`
- Modify `src/shared/ui/ItemSlot/index.tsx`, `style.module.css` — `ghost` prop
- Modify `src/shared/ui/FluidSlot/index.tsx`, `style.module.css`, `src/shared/ui/FluidSlotRow/index.tsx` — `ghost` prop
- Create `src/features/blockInventory/details/machine/machineSlotGhosts.ts` (+ `.test.ts`) — 表示スロットとゴースト導出
- Modify `src/features/blockInventory/details/machine/MachineInventoryBody.tsx` — レシピ分スロット＋ゴースト
- Create `src/features/blockInventory/details/machine/SelectedRecipeHeader.tsx` — 選択中レシピ表示（クリックで戻る）
- Create `src/features/blockInventory/details/machine/MachineRecipeSelectionRow.tsx` (+ `.test.ts`), `MachineRecipeSelectionList.tsx`, `machineRecipeSelectionList.module.css`
- Delete `src/features/blockInventory/details/machine/MachineRecipeSelectionTab.tsx`, `.test.ts`, `machineRecipeSelection.module.css`
- Modify `src/features/blockInventory/details/machine/machineRecipeSelectionLogic.ts` (+ `.test.ts`) — `machineInitialTab` 削除、行データにレシピ本体
- Modify `src/features/blockInventory/details/MachineSection.tsx` (+ `.test.ts`) — 2モード
- Modify `Localization/localization.csv`, `src/shared/i18n/generated/*`（gen）
- Modify `e2e/mock-host/fixtures/recipeFixtures.ts`, `e2e/mock-host/blockDetailFixtures.ts`, `e2e/tests/block/machineRecipe.spec.ts`, `e2e/tests/block/machineGestures.spec.ts`
- Modify `.agents/skills/webui-design/SKILL.md` §2（L145）・§8.7

---

### Task 0: worktree・様式書の先行改訂

**Files:**
- Modify: `.agents/skills/webui-design/SKILL.md:145`, `:271-282`

- [x] **Step 1: worktree作成とbd claim**

```bash
pwd
moores-wt new feature/machine-ui-mode-split-ghost-slots
cd ~/moorestech-worktrees/feature-machine-ui-mode-split-ghost-slots   # moores-wt の出力パスに従う
bd update moorestech-j2kx --claim
```

- [x] **Step 2: §2 の大型パネル行を書き換える**

`SKILL.md:145` の「中身は `ModeSwitch` を横向きタブバーとした「インベントリ / レシピ選択」の2タブ切替（§8.7）。」を次に置換:

```
中身はタブを持たず「レシピ選択モード / インベントリモード」の2画面をSatisfactory方式で往復する（§8.7）。
```

- [x] **Step 3: §8.7 を全面置換する**

`## 8.7 機械レシピ選択タブ` から `## 8.8` の直前までを次に置換:

```markdown
## 8.7 機械UI（レシピ選択モード / インベントリモード）

- **タブは持たない（ADR 0042、ユーザー裁定 2026-08-30）。** 対象レシピが1件以上ある機械は2つの画面を往復する。
  - レシピ未選択で開くと**レシピ選択モード**。行を左クリックすると `machine_recipe.select set` を送り、同時に**インベントリモード**へ切り替える。
  - インベントリモード上部の**選択中レシピ表示**（出力 `ItemSlot`（個数バッジ無し）＋レシピ名（出力アイテム名）＋秒数、testId `machine-selected-recipe`）を左クリックするとレシピ選択モードへ戻る。ホバーツールチップは `ui.blockInventory.changeRecipe`。
  - レシピ解除の導線（右クリック解除・解除ボタン）は設けない。0件ならどちらの画面も出さず従来表示のまま。
- **機械UIの中身は基本的に中央揃え。** 稼働状態ラベル（待機中/稼働中/停止中。Halted のみ `--text-insufficient`、他は`--text-high-contrast`）は両モード共通フッタとして常時表示する。電力率テキストは稼働状態ラベルの隣に、**稼働状態が停止中(halted)でない場合だけ**併記する（ADR 0010、要求電力0で稼働する機械を停止中に潰さないため状態で決める）。
- **インベントリモードはレシピ分のスロットだけ描く。** 入力＝素材数、出力＝生産物数、液体＝レシピ液体数（入力タンク→出力タンクの順）。機械固有の余剰スロットは描かない（サーバーもスロット固定で余剰へ入れない）。
  - **ゴーストスロット**: 空の入力スロットに素材、空の出力スロットに生産物、空の液体スロットにレシピ液体を `data-ghost="true"` で描く。不透明度は `--slot-ghost-opacity` のみで表現し、新しい色相・光彩・枠線を足さない。個数バッジはレシピ必要数。実物があるスロットはゴーストを出さない。
  - **加工行は進捗矢印をパネル中央に固定**し、左右を等幅（1fr auto 1fr）にして入力は矢印へ右寄せ、出力は矢印から左寄せで対称に置く。
  - **モジュールスロットは加工行から1段下げ、`--text-muted` の「アップグレードスロット」ラベルを直上に付けて**用途を明示する。
- **レシピ選択モードは行リスト。** 各行は §8.17 の共有 `RecipeRow` を流用し、中央列は所要秒数＋静止矢印（`arrowValue={null}`）のみ、操作欄は空。ブロックアイコン/名は開いている機械自身なので出さない。レシピ名（出力アイテム名）は行の上辺に `--text-muted` のテキストで置く。行全体（`data-testid="machine-recipe-<guid>"`）が左クリック対象で、行内の `ItemSlot` は操作を持たない。選択中行は `data-selected="true"` で示し、新しい色相・光彩は足さない。ホバー詳細プレビュー領域・9列アイコングリッドは廃止済みで復活させない。
```

- [x] **Step 4: コミット**

```bash
git add .agents/skills/webui-design/SKILL.md
git commit -m "docs(webui-design): §8.7を機械UI2モード仕様へ改訂 (ADR 0042)"
```

---

### Task 1: サーバー — レシピ束縛の純関数と入出力サブインベントリのスロット固定

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeSlotBindingUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/MachineProcessContext.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs:58,122`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/Machine/CleanRoomMachineProcessorComponent.cs:50` と `ChangeSelection` 内の `_context.SelectedRecipe = recipe`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineInputInventory.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineOutputInventory.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/IVanillaMachineSubInventory.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineModuleInventory.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineSlotBindingTest.cs`

**Interfaces:**
- Produces: `MachineRecipeSlotBindingUtil.FindInputSlotIndex(MachineRecipeMasterElement recipe, ItemId itemId) : int`（無ければ -1）、`FindOutputSlotIndex(recipe, int realizedOutputIndex) : int`（`index % recipe.OutputItems.Length`）
- Produces: `VanillaMachineInputInventory.SetBoundRecipe(MachineRecipeMasterElement recipe)`（null=未選択）、`bool IsAllowedToPlace(int localSlot, IItemStack itemStack)`、`bool IsFluidAllowedAt(int tankIndex, FluidId fluidId)`
- Produces: `VanillaMachineOutputInventory.SetBoundRecipe(MachineRecipeMasterElement recipe)`、`bool IsAllowedToPlace(int localSlot, IItemStack itemStack)`
- Produces: `MachineProcessContext.BindSelectedRecipe(MachineRecipeMasterElement recipe)`、`SelectedRecipe { get; private set; }`
- Produces: `IVanillaMachineSubInventory.IsAllowedToPlace(int localSlot, IItemStack itemStack)`

- [x] **Step 1: 失敗するテストを書く**

`Tests/CombinedTest/Core/MachineSlotBindingTest.cs`（`MachineIOTest` と同じ初期化。レシピは `ForUnitTestModMachineRecipeId` 相当が無いため `MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0]`＝TestElectricMachine: 入力 Test1×3, Test2×1 / 出力 Test3×1、機械スロットは入2/出3）:

```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    // レシピ束縛によるスロット固定（ADR 0042 R5/R6）
    // Recipe-bound slot fixing (ADR 0042 R5/R6)
    public class MachineSlotBindingTest
    {
        [Test]
        public void UnselectedMachineRejectsAllInserts()
        {
            var (block, recipe, factory) = Setup(selectRecipe: false);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var item = factory.Create(recipe.InputItems[0].ItemGuid, 3);

            var remainder = inventory.InsertItem(item);

            Assert.AreEqual(3, remainder.Count);
            Assert.IsFalse(inventory.InsertionCheck(new List<IItemStack> { item }));
            Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(0).Id);
        }

        [Test]
        public void SelectedMachineRoutesEachInputToItsRecipeSlot()
        {
            var (block, recipe, factory) = Setup(selectRecipe: true);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();

            // 素材1を先に入れても素材0のスロットは空のまま
            // Inserting input 1 first leaves input 0's slot empty
            inventory.InsertItem(factory.Create(recipe.InputItems[1].ItemGuid, 1));
            inventory.InsertItem(factory.Create(recipe.InputItems[0].ItemGuid, 3));

            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid), inventory.GetItem(0).Id);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(recipe.InputItems[1].ItemGuid), inventory.GetItem(1).Id);
        }

        [Test]
        public void SelectedMachineRejectsItemNotInRecipe()
        {
            var (block, recipe, factory) = Setup(selectRecipe: true);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var foreign = factory.Create(recipe.OutputItems[0].ItemGuid, 1);

            var remainder = inventory.InsertItem(foreign);

            Assert.AreEqual(1, remainder.Count);
            Assert.IsFalse(inventory.InsertionCheck(new List<IItemStack> { foreign }));
        }

        [Test]
        public void ReplaceItemIntoWrongSlotIsRejected()
        {
            var (block, recipe, factory) = Setup(selectRecipe: true);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var input1 = factory.Create(recipe.InputItems[1].ItemGuid, 1);

            // スロット0は素材0専用なので素材1は置けず、そのまま返る
            // Slot 0 is bound to input 0, so input 1 bounces back untouched
            var returned = inventory.ReplaceItem(0, input1);

            Assert.AreEqual(input1.Id, returned.Id);
            Assert.AreEqual(1, returned.Count);
            Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(0).Id);
        }

        [Test]
        public void ProcessedOutputLandsInBoundOutputSlot()
        {
            var (block, recipe, factory) = Setup(selectRecipe: true);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var input in recipe.InputItems) inventory.InsertItem(factory.Create(input.ItemGuid, input.Count));
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();

            var ticks = GameUpdater.SecondsToTicks(recipe.Time) + 5;
            for (var i = 0; i < ticks; i++)
            {
                processor.SupplyExternalPower(10000);
                GameUpdater.UpdateOneTick();
            }

            // 出力スロット0（統合スロット=入力数）に生産物0、他の出力スロットは空
            // Output slot 0 (unified index = input count) holds output 0; other output slots stay empty
            var inputCount = recipe.InputItems.Length;
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid), inventory.GetItem(inputCount).Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(inputCount + 1).Id);
        }

        private static (IBlock block, Mooresmaster.Model.MachineRecipesModule.MachineRecipeMasterElement recipe, IItemStackFactory factory) Setup(bool selectRecipe)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            if (selectRecipe) MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            return (block, recipe, ServerContext.ItemStackFactory);
        }
    }
}
```

`Setup` で使う `GetComponent<VanillaMachineBlockInventoryComponent>()` は `MachineIOTest` と同じ `Game.Block.Interface.Extension` の拡張。`GameUpdater.SecondsToTicks` は `Core.Update`（AGENTS.md「時間に関して」）。

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MachineSlotBindingTest"`
Expected: `UnselectedMachineRejectsAllInserts` / `ReplaceItemIntoWrongSlotIsRejected` / `SelectedMachineRejectsItemNotInRecipe` が FAIL（現状は何でも入る）

- [x] **Step 3: 純関数 util を作る**

`Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeSlotBindingUtil.cs`:

```csharp
using Core.Master;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.RecipeSelection
{
    /// <summary>
    ///     選択レシピとスロット番号の対応（スロットi＝素材i、出力スロットj＝生産物j）を引く純関数
    ///     Pure lookups for the recipe-to-slot binding (input slot i = input i, output slot j = output j)
    /// </summary>
    internal static class MachineRecipeSlotBindingUtil
    {
        // 素材のスロット番号。レシピに無いアイテムは-1
        // Input slot index for the item; -1 when the recipe does not use it
        public static int FindInputSlotIndex(MachineRecipeMasterElement recipe, ItemId itemId)
        {
            for (var i = 0; i < recipe.InputItems.Length; i++)
            {
                if (MasterHolder.ItemMaster.GetItemId(recipe.InputItems[i].ItemGuid) == itemId) return i;
            }
            return -1;
        }

        // 実現出力k（追加セット込み）の出力スロット番号。品質変種でIDが変わるため番号で引く
        // Output slot for realized output k (extra sets included); indexed, since quality variants change the id
        public static int FindOutputSlotIndex(MachineRecipeMasterElement recipe, int realizedOutputIndex)
        {
            return realizedOutputIndex % recipe.OutputItems.Length;
        }

        // 入力タンクiが受け入れる液体か
        // Whether input tank i accepts the fluid
        public static bool IsInputFluidBoundTo(MachineRecipeMasterElement recipe, int tankIndex, FluidId fluidId)
        {
            if (tankIndex < 0 || recipe.InputFluids.Length <= tankIndex) return false;
            return MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[tankIndex].FluidGuid) == fluidId;
        }
    }
}
```

- [x] **Step 4: `IVanillaMachineSubInventory` に配置可否を足す**

```csharp
// 既存メンバーの下に追加
// Added below the existing members
        // プレイヤー操作でこのローカルスロットへ置けるか（レシピ束縛の判定）
        // Whether a player may place the stack into this local slot (recipe-binding rule)
        bool IsAllowedToPlace(int localSlot, IItemStack itemStack);
```

`VanillaMachineModuleInventory` には `public bool IsAllowedToPlace(int localSlot, IItemStack itemStack) => true;` を追加（モジュール判定は既存の装備プロトコル側が持つため変えない）。

- [x] **Step 5: `VanillaMachineInputInventory` を束縛対応にする**

フィールド追加とメソッド差し替え（既存の `InsertItem(IItemStack)`, `InsertItem(List)`, `InsertionCheck`, `ReduceInputSlot` のアイテム部分を置換）:

```csharp
        // 選択レシピ。nullは未選択で何も受け入れない（ADR 0042）
        // The selected recipe; null means unselected and nothing is accepted (ADR 0042)
        private MachineRecipeMasterElement _boundRecipe;

        public void SetBoundRecipe(MachineRecipeMasterElement recipe)
        {
            _boundRecipe = recipe;
        }

        public bool IsAllowedToPlace(int localSlot, IItemStack itemStack)
        {
            if (itemStack.Id == ItemMaster.EmptyItemId) return true;
            return _boundRecipe != null && MachineRecipeSlotBindingUtil.FindInputSlotIndex(_boundRecipe, itemStack.Id) == localSlot;
        }

        public bool IsFluidAllowedAt(int tankIndex, FluidId fluidId)
        {
            return _boundRecipe != null && MachineRecipeSlotBindingUtil.IsInputFluidBoundTo(_boundRecipe, tankIndex, fluidId);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            // 素材iはスロットiにだけ積む。レシピ外・未選択はそのまま返す
            // Input i stacks only into slot i; foreign items and unselected machines bounce it back
            var slot = ResolveBoundSlot(itemStack);
            if (slot < 0) return itemStack;
            var result = InputSlot[slot].AddItem(itemStack);
            _itemDataStoreService.SetItem(slot, result.ProcessResultItemStack);
            return result.RemainderItemStack;
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            var remainders = new List<IItemStack>(itemStacks.Count);
            foreach (var stack in itemStacks) remainders.Add(InsertItem(stack));
            return remainders;
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            // 実挿入と同じ束縛規則でスロット複製へ仮想挿入する
            // Virtually insert into copied slots under the same binding rule as the real insert
            var simulated = new List<IItemStack>(InputSlot);
            foreach (var stack in itemStacks)
            {
                var slot = ResolveBoundSlot(stack);
                if (slot < 0) return false;
                var result = simulated[slot].AddItem(stack);
                if (result.RemainderItemStack.Count != 0) return false;
                simulated[slot] = result.ProcessResultItemStack;
            }
            return true;
        }

        private int ResolveBoundSlot(IItemStack itemStack)
        {
            if (_boundRecipe == null) return -1;
            return MachineRecipeSlotBindingUtil.FindInputSlotIndex(_boundRecipe, itemStack.Id);
        }
```

`ReduceInputSlot` のアイテム部分を index 整列へ（液体部分は Task 2 で差し替える）:

```csharp
            // 素材iはスロットiから減らす（束縛済みなので探索しない）
            // Consume input i from slot i (bound, so no search)
            for (var i = 0; i < recipe.InputItems.Length; i++)
            {
                var item = recipe.InputItems[i];
                if (item.IsRemain.HasValue && item.IsRemain.Value) continue;
                _itemDataStoreService.SetItem(i, InputSlot[i].SubItem(item.Count));
            }
```

`using Game.Block.Blocks.Machine.RecipeSelection;` を追加。

- [x] **Step 6: `VanillaMachineOutputInventory` を束縛対応にする**

```csharp
        private MachineRecipeMasterElement _boundRecipe;

        public void SetBoundRecipe(MachineRecipeMasterElement recipe)
        {
            _boundRecipe = recipe;
        }

        // 出力スロットjは生産物jの素材ID（品質変種は同一レベルファミリー）だけ置ける
        // Output slot j accepts only output j (quality variants share the level family)
        public bool IsAllowedToPlace(int localSlot, IItemStack itemStack)
        {
            if (itemStack.Id == ItemMaster.EmptyItemId) return true;
            if (_boundRecipe == null || _boundRecipe.OutputItems.Length <= localSlot) return false;
            var baseId = MasterHolder.ItemMaster.GetItemId(_boundRecipe.OutputItems[localSlot].ItemGuid);
            return MachineRecipeSlotBindingUtil.IsOutputVariantOf(baseId, itemStack.Id);
        }
```

`MachineRecipeSlotBindingUtil` に追加（`ItemMaster` へはメソッドを足さない — レイヤーマップ「ItemMasterへのメソッド追加はほぼ常に誤り」。`GetLevelVariantItemId` は範囲外をクランプするため「次レベルが同じIDになったら末尾」で打ち切る）:

```csharp
        // itemId が baseId 自身か、そのレベル変種（品質モジュール由来）か
        // Whether itemId is baseId itself or one of its level variants (from quality modules)
        public static bool IsOutputVariantOf(ItemId baseId, ItemId itemId)
        {
            if (baseId == itemId) return true;
            if (!MasterHolder.ItemMaster.HasLevelFamily(baseId)) return false;
            for (var level = 1; ; level++)
            {
                var variant = MasterHolder.ItemMaster.GetLevelVariantItemId(baseId, level);
                if (variant == itemId) return true;
                if (variant == MasterHolder.ItemMaster.GetLevelVariantItemId(baseId, level + 1)) return false;
            }
        }
```

`CanStoreOutputs` の仮想挿入と `InsertOutputSlot` の `InsertItemOutputs` を index 整列へ:

```csharp
            // 実現出力kは出力スロット(k % 生産物数)へ固定で積む
            // Realized output k always lands in output slot (k % output count)
            var simulatedSlots = OutputSlot.ToList();
            for (var k = 0; k < itemOutputs.Count; k++)
            {
                var slot = MachineRecipeSlotBindingUtil.FindOutputSlotIndex(_boundRecipe, k);
                if (!simulatedSlots[slot].IsAllowedToAdd(itemOutputs[k])) return false;
                var result = simulatedSlots[slot].AddItem(itemOutputs[k]);
                if (result.RemainderItemStack.Count != 0) return false;
                simulatedSlots[slot] = result.ProcessResultItemStack;
            }
            return true;
```

```csharp
            void InsertItemOutputs()
            {
                for (var k = 0; k < itemOutputs.Count; k++)
                {
                    var slot = MachineRecipeSlotBindingUtil.FindOutputSlotIndex(_boundRecipe, k);
                    _itemDataStoreService.SetItem(slot, OutputSlot[slot].AddItem(itemOutputs[k]).ProcessResultItemStack);
                }
            }
```

- [x] **Step 7: `MachineProcessContext.BindSelectedRecipe` と呼び出し側**

`MachineProcessContext`:

```csharp
        public MachineRecipeMasterElement SelectedRecipe { get; private set; }

        // 選択レシピを保持し、入出力インベントリへスロット束縛をプッシュする
        // Store the selection and push the slot binding into the input/output inventories
        public void BindSelectedRecipe(MachineRecipeMasterElement recipe)
        {
            SelectedRecipe = recipe;
            InputInventory.SetBoundRecipe(recipe);
            OutputInventory.SetBoundRecipe(recipe);
        }
```

`VanillaMachineProcessorComponent.cs:58` と `CleanRoomMachineProcessorComponent.cs:50` の `{ SelectedRecipe = selectedRecipe }` 初期化子を削除し、直後に `_context.BindSelectedRecipe(selectedRecipe);` を呼ぶ。両 `ChangeSelection` の `_context.SelectedRecipe = recipe;` を `_context.BindSelectedRecipe(recipe);` に置換。

- [x] **Step 8: コンパイルしてテストを通す**

Run: `uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MachineSlotBindingTest|MachineIOTest|MachineRecipeChangeRefundTest|MachineRecipeSelectionTest|QualityModuleOutputTest"`
Expected: 全PASS（`ReplaceItemIntoWrongSlotIsRejected` は Task 2 まで FAIL のままでよい）

- [x] **Step 9: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Block moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineSlotBindingTest.cs*
git commit -m "feat(server): 機械の入出力スロットを選択レシピの素材順へ固定する (ADR 0042)"
```

---

### Task 2: サーバー — プレイヤー操作・整理・液体タンクの束縛ゲートと既存テスト更新

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineBlockInventoryComponent.cs:52-60,94-105,150-170`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineRecipeMaster.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineInputInventory.cs`（`ReduceInputSlot` 液体部）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineFluidInventoryComponent.cs:127-160`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineFluidSlotBindingTest.cs`
- Test Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/RequestBlockInventoryTest.cs:43-44`, `Tests/CombinedTest/Server/PacketTest/Event/BlockInventoryUpdateEventPacketTest.cs:52,75`, 失敗した他テスト

**Interfaces:**
- Consumes: Task 1 の `IsAllowedToPlace` / `IsFluidAllowedAt`

- [x] **Step 1: 液体束縛テストを書く**

`Tests/CombinedTest/Core/MachineFluidSlotBindingTest.cs`（FluidMachine: 入力タンク3・出力タンク2、レシピ `38dfacce-1234-4612-8c7c-29112c12409a`: 入力液体 [fluid1×1, fluid2×2]、出力液体 [fluid3×4]）:

```csharp
using System;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    // 液体タンクもレシピ順に束縛される（ADR 0042 R5）
    // Fluid tanks are bound to the recipe order as well (ADR 0042 R5)
    public class MachineFluidSlotBindingTest
    {
        private static readonly Guid FluidRecipeGuid = Guid.Parse("38dfacce-1234-4612-8c7c-29112c12409a");

        [Test]
        public void InputTankAcceptsOnlyItsBoundFluid()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(FluidRecipeGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var fluidInventory = block.GetComponent<VanillaMachineFluidInventoryComponent>();
            var fluid0 = MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[0].FluidGuid);
            var fluid1 = MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[1].FluidGuid);

            // タンク指定無しの流入は束縛タンクへ入る（fluid1はタンク1へ）
            // Undesignated inflow lands in the bound tank (fluid 1 goes to tank 1)
            var remainder = fluidInventory.AddLiquid(new FluidStack(2, fluid1), default);
            var tanks = fluidInventory.GetFluidInventory();

            Assert.AreEqual(0, remainder.Amount);
            Assert.AreEqual(fluid1, tanks[1].FluidId);
            Assert.AreNotEqual(fluid1, tanks[0].FluidId);

            // タンク0へ束縛外の液体を指定しても拒否される
            // Fluid 1 designated to tank 0 is refused
            var designatedRemainder = fluidInventory.AddLiquid(new FluidStack(1, fluid1), MachineFluidTestUtil.ConnectedToTank(0));
            Assert.AreEqual(1, designatedRemainder.Amount);
            Assert.AreNotEqual(fluid0, tanks[1].FluidId);
        }

        [Test]
        public void UnselectedMachineRejectsFluid()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var fluidInventory = block.GetComponent<VanillaMachineFluidInventoryComponent>();

            var remainder = fluidInventory.AddLiquid(new FluidStack(5, new FluidId(1)), default);

            Assert.AreEqual(5, remainder.Amount);
        }
    }
}
```

`MachineFluidTestUtil.ConnectedToTank(int)` は `MachineFluidIOTest` にタンク指定の `ConnectedInfo` を組む前例があればそれを `Tests/Util/MachineFluidTestUtil.cs` へ切り出して使う。前例が無ければ `ConnectedInfo` と `IFluidConnector.Option.ConnectTankIndex` を満たす最小スタブを同ファイルに書く（`VanillaMachineFluidInventoryComponent.AddLiquid` の `connectedInfo.TargetConnector is IFluidConnector receiverConnector` 分岐を通す）。`GetFluidInventory()` の返却順が入力タンク順であることを実装で確認し、違えば `_inputInventory.FluidInputSlot` を直接読む形にテストを直す。

- [x] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MachineFluidSlotBindingTest"`
Expected: FAIL（現状は任意タンクへ入る）

- [x] **Step 3: 液体の束縛を実装する**

`VanillaMachineFluidInventoryComponent.AddLiquid` を置換:

```csharp
        public FluidStack AddLiquid(FluidStack fluidStack, ConnectedInfo connectedInfo)
        {
            var tankIndex = connectedInfo.TargetConnector is IFluidConnector receiverConnector
                ? receiverConnector.Option.ConnectTankIndex
                : -1;

            // タンク指定ありはそのタンクが束縛液体の時だけ受け入れる
            // A designated tank accepts only when it is bound to this fluid
            if (0 <= tankIndex && tankIndex < _inputInventory.FluidInputSlot.Count)
            {
                if (!_inputInventory.IsFluidAllowedAt(tankIndex, fluidStack.FluidId)) return fluidStack;
                return AddTo(tankIndex);
            }

            // 指定無しは束縛タンクへ直行する（未選択・レシピ外は拒否）
            // Undesignated inflow goes straight to the bound tank (refused when unselected or foreign)
            for (var i = 0; i < _inputInventory.FluidInputSlot.Count; i++)
            {
                if (_inputInventory.IsFluidAllowedAt(i, fluidStack.FluidId)) return AddTo(i);
            }
            return fluidStack;

            #region Internal

            FluidStack AddTo(int index)
            {
                var result = _inputInventory.FluidInputSlot[index].AddLiquid(fluidStack);
                if (0 < result.AcceptedAmount) _onChangeBlockState.OnNext(Unit.Default);
                return result.Remainder;
            }

            #endregion
        }
```

`MachineRecipeMaster.RecipeConfirmation` を index 整列へ（アイテム・液体とも）:

```csharp
            var recipeBlockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            if (recipeBlockId != blockId) return false;

            // 素材iはスロットiに束縛されているので番号で照合する
            // Input i is bound to slot i, so match by index
            for (var i = 0; i < recipe.InputItems.Length; i++)
            {
                if (inputSlot.Count <= i) return false;
                var required = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[i].ItemGuid);
                if (inputSlot[i].Id != required || inputSlot[i].Count < recipe.InputItems[i].Count) return false;
            }

            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                if (fluidInputSlot.Count <= i) return false;
                var required = MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[i].FluidGuid);
                if (fluidInputSlot[i].FluidId != required || fluidInputSlot[i].Amount < recipe.InputFluids[i].Amount) return false;
            }
            return true;
```

`VanillaMachineInputInventory.ReduceInputSlot` の液体部分:

```csharp
            // 液体iはタンクiから減らす
            // Consume fluid i from tank i
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                var container = _fluidContainers[i];
                container.Amount -= recipe.InputFluids[i].Amount;
                if (container.Amount > 0) continue;
                container.Amount = 0;
                container.FluidId = FluidMaster.EmptyFluidId;
            }
```

- [x] **Step 4: プレイヤー操作と整理のゲート**

`VanillaMachineBlockInventoryComponent`:

```csharp
        // 束縛外のスロットへは置けず、そのまま返す（プレイヤー移動プロトコルの入口）
        // A stack that violates the binding bounces back untouched (entry point of the player move protocol)
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);

            var (subInventory, localSlot) = ResolveSlot(slot);
            if (!subInventory.IsAllowedToPlace(localSlot, itemStack)) return itemStack;
            var current = subInventory.Items[localSlot];
            if (current.Id == itemStack.Id)
            {
                var result = current.AddItem(itemStack);
                subInventory.SetItem(localSlot, result.ProcessResultItemStack);
                return result.RemainderItemStack;
            }
            subInventory.SetItem(localSlot, itemStack);
            return current;
        }

        // 入れ替え経路（move service の全量swap）も束縛を守る。ロード復元はサブインベントリの SetItemWithoutEvent を使うため影響しない
        // The swap path (full-stack swap in the move service) also honors the binding; load restore uses the sub-inventory's SetItemWithoutEvent and is unaffected
        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);

            var (subInventory, localSlot) = ResolveSlot(slot);
            if (!subInventory.IsAllowedToPlace(localSlot, itemStack)) return;
            subInventory.SetItem(localSlot, itemStack);
        }
```

`SortExcludedSlots` は全スロットを返す（束縛スロットの並べ替えは意味を持たない）:

```csharp
        // スロットは全て束縛済みで整理対象にならない
        // Every slot is recipe-bound, so none participates in sorting
        public IReadOnlyCollection<int> SortExcludedSlots
        {
            get
            {
                BlockException.CheckDestroy(this);
                return Enumerable.Range(0, GetSlotSize()).ToList();
            }
        }
```

- [x] **Step 5: コンパイルと機械系全テスト**

Run: `uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Machine|BlockInventory|SortInventory|CleanRoom|Gear.*Machine|Blueprint"`
Expected: `MachineSlotBindingTest`・`MachineFluidSlotBindingTest` PASS。`RequestBlockInventoryTest`・`BlockInventoryUpdateEventPacketTest`・`SortInventoryProtocolTest` 等、未選択機械へ `SetItem` する前提のテストが FAIL する。

- [x] **Step 6: 失敗した既存テストを新仕様へ更新する**

方針: 機械へ任意アイテムを `SetItem` しているテストは、`MachineRecipeSelectTestUtil.SelectRecipe(block, MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0])` を先に呼び、置く位置とIDを `recipe.InputItems[i]`（スロットi）・`recipe.OutputItems[j]`（スロット `InputItems.Length + j`）から取る。例（`RequestBlockInventoryTest.cs:43-44`）:

```csharp
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var input0 = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid);
            var output0 = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            machineComponent.SetItem(0, itemStackFactory.Create(input0, 2));
            machineComponent.SetItem(recipe.InputItems.Length, itemStackFactory.Create(output0, 5));
```

期待値（レスポンスに含まれるID/個数）も同じ変数で書き換える。`SortInventoryProtocolTest` が機械の並べ替えを検証していれば「機械は整理されない」へ期待値を反転する。`MachineFluidIOTest` が任意タンクへ液体を入れていればレシピ順のタンクへ直す。

Run: 同上フィルタ
Expected: 全PASS

- [x] **Step 7: コミット**

```bash
git add moorestech_server/Assets/Scripts
git commit -m "feat(server): プレイヤー操作・整理・液体タンクにもレシピ束縛を適用し既存テストを更新 (ADR 0042)"
```

---

### Task 3: ワイヤ — `crafting.machine_recipes` に液体を追加

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/MachineRecipesTopic.cs`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/recipes.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/recipeFixtures.ts`
- Modify: `MachineRecipe` リテラルを持つテスト全件: `src/bridge/contract/validators.test.ts`, `contentGuidContracts.test.ts`, `schemas/guidValidation.test.ts`, `src/features/blockInventory/details/MachineSection.test.ts`, `machine/MachineRecipeSelectionTab.test.ts`（Task 6で削除するため触らない）, `src/features/recipe/logic/craftLogic.test.ts`, `views/RecipeContent.test.ts`, `views/MachineRecipeEntry.localization.test.ts`

**Interfaces:**
- Produces: `MachineRecipe.inputFluids: { fluidId: number; fluidGuid: string; amount: number }[]`、`outputFluids` 同型（`payloadTypes.ts` の `MachineRecipe` は zod 推論なので自動追従）

- [x] **Step 1: zod に失敗するテストを足す**

`src/bridge/contract/schemas/guidValidation.test.ts` の `machineRecipe` フィクスチャに `inputFluids: [], outputFluids: []` を足し、`cases` に次を追加:

```ts
    { label: "machine recipe inputFluids fluidGuid", schema: MachineRecipeSchema, payload: { ...machineRecipe, inputFluids: [{ fluidId: 1, amount: 1, fluidGuid: invalidGuid }] } },
```

- [x] **Step 2: 実行して失敗を確認**

Run: `cd moorestech_web/webui && npx vitest run src/bridge/contract/schemas/guidValidation.test.ts`
Expected: FAIL（`.strict()` で `inputFluids` が unrecognized key）

- [x] **Step 3: スキーマを拡張する**

`recipes.ts` の `MachineRecipeSchema` を置換:

```ts
// 液体は研究側 ResearchUnlockFluidSchema と同型（fluidGuid で名前・色を解決する）
// Fluids share the shape of ResearchUnlockFluidSchema (name/color resolve via fluidGuid)
export const MachineRecipeFluidSchema = z.object({
  fluidId: z.number(),
  fluidGuid: GuidSchema,
  amount: z.number(),
}).strict();
export const MachineRecipeSchema = z.object({
  recipeGuid: GuidSchema,
  blockGuid: GuidSchema,
  blockId: z.number(),
  time: z.number(),
  inputItems: z.array(MachineRecipeItemSchema),
  outputItems: z.array(MachineRecipeItemSchema),
  inputFluids: z.array(MachineRecipeFluidSchema),
  outputFluids: z.array(MachineRecipeFluidSchema),
}).strict();
```

- [x] **Step 4: 全リテラルへ `inputFluids: [], outputFluids: []` を足す**

```bash
cd moorestech_web/webui && grep -rln "inputItems: \[" src e2e
```

出てきた各ファイルの `MachineRecipe` 形リテラル（`recipeGuid`+`blockGuid` を持つもの）に `inputFluids: [], outputFluids: []` を追加する。`e2e/mock-host/fixtures/recipeFixtures.ts` の `bbbbbbbb` レシピだけは液体ゴーストのe2e用に `inputFluids: [{ fluidId: 1, fluidGuid: WATER_FLUID_GUID, amount: 10 }]` とする（`WATER_FLUID_GUID` は `fluidMasterFixtures.ts` から import。無ければ `blockDetailFixtures.ts` が使っている定数の出所を辿る）。

Run: `npx vitest run && npm run typecheck`
Expected: PASS

- [x] **Step 5: C# DTO を拡張する**

`MachineRecipesTopic.cs` の `MachineRecipeDto` に追加し、`BuildJson` で詰める:

```csharp
        public List<RecipeFluidDto> InputFluids;
        public List<RecipeFluidDto> OutputFluids;
```

```csharp
    public class RecipeFluidDto
    {
        public int FluidId;
        public string FluidGuid;
        public float Amount;
    }
```

```csharp
                    InputFluids = BuildFluids(recipe.InputFluids),
                    OutputFluids = BuildFluids(recipe.OutputFluids),
```

`#region Internal` 内にローカル関数を追加（`InputFluids`/`OutputFluids` の要素型が別クラスなら2本に分ける）:

```csharp
            List<RecipeFluidDto> BuildFluids(IEnumerable<Mooresmaster.Model.MachineRecipesModule.InputFluidsElement> fluids)
            {
                var list = new List<RecipeFluidDto>();
                foreach (var fluid in fluids)
                {
                    list.Add(new RecipeFluidDto
                    {
                        FluidId = MasterHolder.FluidMaster.GetFluidId(fluid.FluidGuid).AsPrimitive(),
                        FluidGuid = fluid.FluidGuid.ToString("D"),
                        Amount = fluid.Amount,
                    });
                }
                return list;
            }
```

生成型名は `Mooresmaster.Model.MachineRecipesModule` 内の実名（`InputFluidsElement` / `OutputFluidsElement` 等）を `grep -rn "class .*Fluids" moorestech_server/Assets/Scripts/Mooresmaster*` で確認して合わせる。

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [x] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_web/webui
git commit -m "feat(wire): machine_recipesにinputFluids/outputFluidsを追加 (ADR 0042 R9)"
```

---

### Task 4: Web shared — ゴースト表現（SlotFrame / ItemSlot / FluidSlot）

**Files:**
- Modify: `moorestech_web/webui/src/app/slotTokens.css`
- Modify: `moorestech_web/webui/src/shared/ui/SlotFrame/index.tsx`, `style.module.css`, `index.test.ts`
- Modify: `moorestech_web/webui/src/shared/ui/ItemSlot/index.tsx`, `style.module.css`
- Modify: `moorestech_web/webui/src/shared/ui/FluidSlot/index.tsx`, `style.module.css`
- Modify: `moorestech_web/webui/src/shared/ui/FluidSlotRow/index.tsx`

**Interfaces:**
- Produces: `SlotFrame` prop `ghost?: boolean` → `data-ghost="true"`
- Produces: `ItemSlot` prop `ghost?: { itemId: number; count: number }`（`itemId<=0` かつ ghost あり → ゴースト描画）
- Produces: `FluidSlot` prop `ghost?: { fluidGuid: string; amount: number }`（`fluid.kind === "empty"` かつ ghost あり → ゴースト描画）
- Produces: `FluidSlotRow` prop `ghosts?: ({ fluidGuid: string; amount: number } | undefined)[]`（index 対応）

- [x] **Step 1: SlotFrame テストを足す**

`SlotFrame/index.test.ts` の最初の `it` に `ghost: true` を渡し、`expect(markup).toContain('data-ghost="true"');` を追加。

Run: `npx vitest run src/shared/ui/SlotFrame`
Expected: FAIL

- [x] **Step 2: SlotFrame に `ghost` を通す**

`Props` に `ghost?: boolean;` を追加、`renderSlotFrame` の分割代入に `ghost` を足し、div に `data-ghost={ghost ? "true" : undefined}` を追加。`style.module.css` 末尾:

```css
/* ゴーストは面もリングも通常の空スロットのまま。内容（アイコン・個数）だけを共通トークンで透かす */
/* A ghost keeps the empty slot's face and ring untouched; only its content (icon, count) is faded via the shared token */
.slot[data-ghost="true"] > * {
  opacity: var(--slot-ghost-opacity);
}
```

`slotTokens.css` の `:root` に追加:

```css
  /* ゴーストスロット（配置されるべきアイテムの半透明プレビュー）の内容不透明度。ADR 0042 */
  /* Content opacity of a ghost slot (translucent preview of the item that belongs there). ADR 0042 */
  --slot-ghost-opacity: 0.35;
```

Run: `npx vitest run src/shared/ui/SlotFrame`
Expected: PASS

- [x] **Step 3: ItemSlot に `ghost` を足す**

`Props` に追加:

```ts
  // 空スロットに置く「入るべきアイテム」の半透明プレビュー。実物（itemId>0）があれば無視する
  // Translucent preview of the item that belongs in an empty slot; ignored when a real item (itemId>0) is present
  ghost?: { itemId: number; count: number };
```

本体で `hasItem` の直後に:

```ts
  const ghostShown = ghost !== undefined && !hasItem && ghost.itemId > 0;
  const shownItemId = ghostShown ? ghost.itemId : itemId;
  const shownCount = ghostShown ? ghost.count : count;
```

`resolvedName` は `shownItemId` で解決し、`HoverTooltip` の `disabled` は `!(hasItem || ghostShown) || ...`、`SlotFrame` に `ghost={ghostShown}`、`filled={filled}` は実物のみ（ゴーストで白面にしない）。描画部を:

```tsx
        {hasItem || ghostShown ? (
          <>
            <ItemIcon itemId={shownItemId} alt={resolvedName ?? t(L.ui.common.itemFallback, { itemId: shownItemId })} className={styles.icon} />
            {(owned || ghostShown) && shownCount !== undefined && shownCount > 0 ? <span className={`iconTextOutlineLight ${styles.count}`}>{shownCount}</span> : null}
          </>
        ) : null}
```

- [x] **Step 4: FluidSlot / FluidSlotRow に `ghost` を足す**

`FluidSlot`:

```tsx
export default function FluidSlot({ fluid, ghost }: { fluid: FluidSlotData; ghost?: { fluidGuid: string; amount: number } }) {
  const { t } = useI18n();
  const fluidMaster = useFluidMaster();

  // 空タンクにゴーストがあれば、フィル無しでアイコンと必要量だけを透かして描く
  // An empty tank with a ghost draws the icon and required amount faded, without a fill
  if (fluid.kind === "empty") {
    if (ghost === undefined) return <div data-testid="fluid-slot" className={styles.slot} />;
    const ghostName = t(fluidNameKey(ghost.fluidGuid));
    return (
      <HoverTooltip label={ghostName} disabled={!ghostName}>
        <div data-testid="fluid-slot" data-ghost="true" className={styles.slot}>
          <FluidIcon fluidGuid={ghost.fluidGuid} className={styles.icon} />
          <span className={`iconTextOutlineDark ${styles.amount}`}>{formatAmount(ghost.amount)}</span>
        </div>
      </HoverTooltip>
    );
  }
  // 以降は既存のまま
```

`FluidSlot/style.module.css` 末尾:

```css
.slot[data-ghost="true"] > * {
  opacity: var(--slot-ghost-opacity);
}
```

`FluidSlotRow`: `ghosts?: ({ fluidGuid: string; amount: number } | undefined)[]` を Props に足し、`<FluidSlot key={i} fluid={fluid} ghost={ghosts?.[i]} />`。

- [x] **Step 5: 検証**

Run: `npx vitest run src/shared && npm run typecheck && npm run lint`
Expected: PASS

- [x] **Step 6: コミット**

```bash
git add src/app/slotTokens.css src/shared/ui
git commit -m "feat(webui): SlotFrame/ItemSlot/FluidSlotにゴースト表現(data-ghost)を追加 (ADR 0042 R8)"
```

---

### Task 5: Web — 表示スロット・ゴースト導出ロジックと MachineInventoryBody

**Files:**
- Create: `moorestech_web/webui/src/features/blockInventory/details/machine/machineSlotGhosts.ts`
- Test: `moorestech_web/webui/src/features/blockInventory/details/machine/machineSlotGhosts.test.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/details/machine/MachineInventoryBody.tsx`

**Interfaces:**
- Consumes: `MachineRecipe`（Task 3 の液体付き）、`splitSlotIndices`（`../detailLogic`）、`FluidSlotData`
- Produces:

```ts
export type GhostItem = { itemId: number; count: number };
export type GhostFluid = { fluidGuid: string; amount: number };
export type BoundItemSlot = { index: number; ghost: GhostItem };       // index は統合スロット番号
export type MachineSlotView = {
  inputs: BoundItemSlot[];
  outputs: BoundItemSlot[];
  fluidIndices: number[];                 // data.fluidSlots 内の表示対象 index（入力タンク→出力タンク）
  fluidGhosts: (GhostFluid | undefined)[]; // fluidIndices と同順
};
export function buildMachineSlotView(recipe: MachineRecipe, layout: { input: number; output: number; module: number }, totalItemSlots: number, inputTankCount: number): MachineSlotView;
```

- [x] **Step 1: 失敗するテストを書く**

```ts
import { describe, expect, it } from "vitest";
import type { MachineRecipe } from "@/bridge";
import { buildMachineSlotView } from "./machineSlotGhosts";

const recipe: MachineRecipe = {
  recipeGuid: "84000000-0000-4000-8000-000000000001",
  blockGuid: "85000000-0000-4000-8000-000000000001",
  blockId: 10, time: 7,
  inputItems: [{ itemId: 1, count: 2 }, { itemId: 5, count: 1 }],
  outputItems: [{ itemId: 2, count: 1 }],
  inputFluids: [{ fluidId: 3, fluidGuid: "86000000-0000-4000-8000-000000000001", amount: 10 }],
  outputFluids: [],
};

describe("buildMachineSlotView", () => {
  it("入力は素材数・出力は生産物数だけを統合スロット番号付きで返す", () => {
    const view = buildMachineSlotView(recipe, { input: 3, output: 3, module: 1 }, 7, 2);
    expect(view.inputs).toEqual([
      { index: 0, ghost: { itemId: 1, count: 2 } },
      { index: 1, ghost: { itemId: 5, count: 1 } },
    ]);
    expect(view.outputs).toEqual([{ index: 3, ghost: { itemId: 2, count: 1 } }]);
  });

  it("液体は入力タンク→出力タンクの順でレシピ分だけ返す", () => {
    const view = buildMachineSlotView(recipe, { input: 3, output: 3, module: 1 }, 7, 2);
    expect(view.fluidIndices).toEqual([0]);
    expect(view.fluidGhosts).toEqual([{ fluidGuid: "86000000-0000-4000-8000-000000000001", amount: 10 }]);
  });

  it("出力液体は入力タンク数の後ろの番号を指す", () => {
    const withOutputFluid = { ...recipe, inputFluids: [], outputFluids: [{ fluidId: 4, fluidGuid: "86000000-0000-4000-8000-000000000002", amount: 4 }] };
    const view = buildMachineSlotView(withOutputFluid, { input: 3, output: 3, module: 1 }, 7, 2);
    expect(view.fluidIndices).toEqual([2]);
  });
});
```

Run: `npx vitest run src/features/blockInventory/details/machine/machineSlotGhosts.test.ts`
Expected: FAIL（モジュール無し）

- [x] **Step 2: 実装**

```ts
// 選択レシピから「描くスロット」と各スロットのゴースト内容を導出する（ADR 0042 R7/R8）
// Derives which slots to draw and each slot's ghost content from the selected recipe (ADR 0042 R7/R8)
import type { MachineRecipe } from "@/bridge";
import { splitSlotIndices } from "../detailLogic";

export type GhostItem = { itemId: number; count: number };
export type GhostFluid = { fluidGuid: string; amount: number };
export type BoundItemSlot = { index: number; ghost: GhostItem };
export type MachineSlotView = {
  inputs: BoundItemSlot[];
  outputs: BoundItemSlot[];
  fluidIndices: number[];
  fluidGhosts: (GhostFluid | undefined)[];
};

export function buildMachineSlotView(
  recipe: MachineRecipe,
  layout: { input: number; output: number; module: number },
  totalItemSlots: number,
  inputTankCount: number,
): MachineSlotView {
  const { input, output } = splitSlotIndices(layout, totalItemSlots);
  // スロットi＝素材i、出力スロットj＝生産物j（サーバーの束縛と同じ規則）
  // Slot i = input i, output slot j = output j (the same rule the server enforces)
  const inputs = recipe.inputItems.slice(0, input.length).map((item, i) => ({ index: input[i], ghost: { itemId: item.itemId, count: item.count } }));
  const outputs = recipe.outputItems.slice(0, output.length).map((item, j) => ({ index: output[j], ghost: { itemId: item.itemId, count: item.count } }));
  // 液体行は入力タンク→出力タンクの連結順（BlockDetailDtoBuilder と同順）
  // The fluid row is inputs then outputs, matching BlockDetailDtoBuilder's concatenation order
  const fluidIndices = [
    ...recipe.inputFluids.map((_, i) => i),
    ...recipe.outputFluids.map((_, j) => inputTankCount + j),
  ];
  const fluidGhosts = [...recipe.inputFluids, ...recipe.outputFluids].map((fluid) => ({ fluidGuid: fluid.fluidGuid, amount: fluid.amount }));
  return { inputs, outputs, fluidIndices, fluidGhosts };
}
```

`inputTankCount` はワイヤに無い。`BlockInventoryOpen.fluidSlots` は入力→出力連結のみなので、Web側は「入力タンク数＝`fluidSlots.length - recipe.outputFluids.length`」では機械固有の余剰で崩れる。よって `MachineDetailDto.SlotLayout` に `InputTank` を足す（`SlotLayoutDto { Input, Output, Module, InputTank }`、`BlockDetailDtoBuilder.cs:43` で `ElectricMachineBlockParam.InputTankCount`（それ以外は0）を詰め、zod `MachineDetailDataSchema` の `slotLayout` と全フィクスチャ（`e2e/mock-host/blockDetailFixtures.ts` の `slotLayout`、`Client.Tests/WebUi/WireFixtures/block_inventory_machine.json`・`block_inventory_gear_machine.json`・`WireContractBlockDetailTest.cs` の `SlotLayoutDto` 初期化子）を同時更新する。これは Task 5 の Step 2 に含める（ワイヤ変更なので `uloop compile` と `WireContractBlockDetailTest` の実行まで）。

Run: `npx vitest run src/features/blockInventory && npm run typecheck && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "WireContractBlockDetailTest"`
Expected: PASS

- [x] **Step 3: MachineInventoryBody をレシピ分スロット＋ゴーストにする**

`MachineInventoryBody` の Props を `{ data: BlockInventoryOpen; recipe: MachineRecipe }` に変え（レシピ0件機械は Task 6 で従来通り `recipe` 無しの経路を残すため、`recipe: MachineRecipe | null` とし null なら現行の全スロット描画）、本体の入出力部分を置換:

```tsx
  const { input, output, module } = splitSlotIndices(machine.slotLayout, data.itemSlots.length);
  const view = recipe === null ? null : buildMachineSlotView(recipe, machine.slotLayout, data.itemSlots.length, machine.slotLayout.inputTank);
  const inputSlots = view === null ? input.map((i) => ({ index: i, ghost: undefined })) : view.inputs;
  const outputSlots = view === null ? output.map((i) => ({ index: i, ghost: undefined })) : view.outputs;
  const fluids = view === null ? data.fluidSlots : view.fluidIndices.map((i) => data.fluidSlots[i]);

  const slotAt = ({ index, ghost }: { index: number; ghost: GhostItem | undefined }) => {
    const slot = data.itemSlots[index];
    return (
      <ItemSlot
        key={index}
        itemId={slot.itemId}
        count={slot.count}
        ghost={ghost}
        onLeftDown={(shiftKey) => gestures.onLeftDown(index, shiftKey)}
        onRightDown={() => gestures.onRightDown(index)}
        onDoubleClick={() => gestures.onDoubleClick(index)}
      />
    );
  };
```

`SlotGrid cols={Math.max(1, inputSlots.length)}` / `outputSlots.length`、`<FluidSlotRow fluids={fluids} ghosts={view?.fluidGhosts} testId="machine-fluid-slots" />`。モジュール・分間生産数は不変。

Run: `npx vitest run src/features/blockInventory && npm run typecheck && npm run lint`
Expected: PASS（`MachineSection.test.ts` は Task 6 で書き直すため、この時点で型エラーが出るなら `MachineSection.tsx` の呼び出しに `recipe={selectedRecipe ?? null}` を仮で足す）

- [x] **Step 4: コミット**

```bash
git add -A moorestech_web/webui/src moorestech_client/Assets/Scripts
git commit -m "feat(webui): 機械インベントリをレシピ分スロット＋ゴースト描画にする (ADR 0042 R7/R8)"
```

---

### Task 6: Web — 2モード化（選択行リスト・選択中ヘッダ・MachineSection）とローカライズ

**Files:**
- Create: `moorestech_web/webui/src/features/blockInventory/details/machine/MachineRecipeSelectionRow.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/details/machine/MachineRecipeSelectionList.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/details/machine/machineRecipeSelectionList.module.css`
- Create: `moorestech_web/webui/src/features/blockInventory/details/machine/SelectedRecipeHeader.tsx`
- Test: `moorestech_web/webui/src/features/blockInventory/details/machine/MachineRecipeSelectionRow.test.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/details/machine/machineRecipeSelectionLogic.ts`, `.test.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/details/MachineSection.tsx`, `.test.ts`
- Delete: `machine/MachineRecipeSelectionTab.tsx`, `machine/MachineRecipeSelectionTab.test.ts`, `machine/machineRecipeSelection.module.css`
- Modify: `Localization/localization.csv`（+ `npm run gen:i18n` 生成物）

`machine/` は削除3・新規4で計 10 ファイル以下（`machineInventoryBody.module.css`, `MachineInventoryBody.tsx`, `machineRecipeSelectionLogic.ts`, `.test.ts`, `machineSlotGhosts.ts`, `.test.ts`, 新規4）＝10。超えるならテストを `machine/tests/` サブディレクトリへ移す。

**Interfaces:**
- Produces: `MachineRecipeSelectionRowData = { recipe: MachineRecipe; selected: boolean }`、`buildMachineRecipeSelectionRows(recipes, blockGuid, selectedRecipeGuid)`（アイコン列は廃止）
- Produces: `MachineRecipeSelectionRow({ row, onSelect: (recipeGuid: string) => void })`、`MachineRecipeSelectionList({ rows, onSelected: () => void })`、`SelectedRecipeHeader({ recipe, onChangeRecipe: () => void })`

- [x] **Step 1: ローカライズキー**

`Localization/localization.csv` の `ui.blockInventory.inventoryTab` / `ui.blockInventory.recipeSelectionTab` / `ui.blockInventory.recipeSelectionHint` の3行を削除し、次を追加:

```
ui.blockInventory.changeRecipe,Change recipe,Change recipe,レシピを変更,Rezept ändern
```

```bash
cd moorestech_web/webui && npm run gen:i18n && npx vitest run src/shared/i18n
```
Expected: PASS（`recipeSelectionHint` を参照する箇所は Task 6 で消える。残れば typecheck が指摘する）

- [x] **Step 2: 行データロジックを書き換える（テスト先行）**

`machineRecipeSelectionLogic.test.ts` を次に置換:

```ts
import { describe, expect, it } from "vitest";
import type { MachineRecipe } from "@/bridge";
import { buildMachineRecipeSelectionRows } from "./machineRecipeSelectionLogic";

const blockA = "85000000-0000-4000-8000-000000000001";
const blockB = "85000000-0000-4000-8000-000000000002";
const emptyGuid = "00000000-0000-0000-0000-000000000000";
function recipe(recipeGuid: string, blockGuid: string): MachineRecipe {
  return { recipeGuid, blockGuid, blockId: 1, time: 1, inputItems: [{ itemId: 1, count: 1 }], outputItems: [{ itemId: 2, count: 1 }], inputFluids: [], outputFluids: [] };
}

describe("buildMachineRecipeSelectionRows", () => {
  it("開いている機械のレシピだけを選択フラグ付きで返す", () => {
    const a = recipe("84000000-0000-4000-8000-000000000001", blockA);
    const rows = buildMachineRecipeSelectionRows([a, recipe("84000000-0000-4000-8000-000000000002", blockB)], blockA, a.recipeGuid);
    expect(rows).toEqual([{ recipe: a, selected: true }]);
  });

  it("空GUIDはどの行も選択しない", () => {
    const a = recipe("84000000-0000-4000-8000-000000000001", blockA);
    expect(buildMachineRecipeSelectionRows([a], blockA, emptyGuid)[0].selected).toBe(false);
  });
});
```

`machineRecipeSelectionLogic.ts`:

```ts
// 開いている機械に対応するレシピを選択行データへ変換する
// Converts the open machine's recipes into selection-row data
import type { MachineRecipe } from "@/bridge";

const emptyGuid = "00000000-0000-0000-0000-000000000000";

export type MachineRecipeSelectionRowData = { recipe: MachineRecipe; selected: boolean };

export function buildMachineRecipeSelectionRows(
  recipes: readonly MachineRecipe[],
  blockGuid: string,
  selectedRecipeGuid: string,
): MachineRecipeSelectionRowData[] {
  const hasSelection = selectedRecipeGuid !== emptyGuid;
  return recipes
    .filter((recipe) => recipe.blockGuid === blockGuid)
    .map((recipe) => ({ recipe, selected: hasSelection && recipe.recipeGuid === selectedRecipeGuid }));
}

export function hasSelectedRecipe(selectedRecipeGuid: string): boolean {
  return selectedRecipeGuid !== emptyGuid;
}
```

Run: `npx vitest run src/features/blockInventory/details/machine/machineRecipeSelectionLogic.test.ts`
Expected: PASS

- [x] **Step 3: 選択行のテストを書く**

`MachineRecipeSelectionRow.test.ts`（`MachineRecipeSelectionTab.test.ts` と同じモック様式）:

```ts
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { MachineRecipe } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
  useItemNameResolver: () => (itemId: number) => `item-${itemId}`,
}));
vi.mock("@mantine/core", () => ({
  Box: ({ children, ...props }: { children: unknown }) => createElement("mock-box", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("@/shared/ui", () => ({
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  ProgressArrowGlyph: (props: object) => createElement("mock-arrow", props),
}));

import MachineRecipeSelectionRow from "./MachineRecipeSelectionRow";

const recipe: MachineRecipe = {
  recipeGuid: "84000000-0000-4000-8000-000000000001",
  blockGuid: "85000000-0000-4000-8000-000000000001",
  blockId: 10, time: 7,
  inputItems: [{ itemId: 1, count: 2 }], outputItems: [{ itemId: 9, count: 1 }],
  inputFluids: [], outputFluids: [],
};

describe("MachineRecipeSelectionRow", () => {
  it("レシピ名を出力アイテム名で出し、行クリックで選択通知する", () => {
    const onSelect = vi.fn();
    const tree = create(createElement(MachineRecipeSelectionRow, { row: { recipe, selected: true }, onSelect }));
    const root = tree.root.findByProps({ "data-testid": `machine-recipe-${recipe.recipeGuid}` });

    expect(root.props["data-selected"]).toBe("true");
    expect(tree.root.findByProps({ "data-testid": `machine-recipe-${recipe.recipeGuid}-name` }).props.children).toBe("item-9");
    act(() => root.props.onClick());
    expect(onSelect).toHaveBeenCalledWith(recipe.recipeGuid);
  });
});
```

Run: `npx vitest run src/features/blockInventory/details/machine/MachineRecipeSelectionRow.test.ts`
Expected: FAIL（モジュール無し）

- [x] **Step 4: 行・リスト・ヘッダを実装する**

`MachineRecipeSelectionRow.tsx`:

```tsx
// レシピ選択行: 上辺にレシピ名、骨格は共有RecipeRow（中央列は秒数＋静止矢印のみ）
// Recipe selection row: recipe name on top, shared RecipeRow skeleton (center column = duration + static arrow only)
import { Box, Text } from "@mantine/core";
import { ItemSlot } from "@/shared/ui";
import { L, useI18n, useItemNameResolver } from "@/shared/i18n";
import RecipeRow from "@/features/recipe/views/RecipeRow";
import type { MachineRecipeSelectionRowData } from "./machineRecipeSelectionLogic";
import styles from "./machineRecipeSelectionList.module.css";

type Props = { row: MachineRecipeSelectionRowData; onSelect: (recipeGuid: string) => void };

export default function MachineRecipeSelectionRow({ row, onSelect }: Props) {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();
  const { recipe } = row;
  // レシピ名は代表出力（先頭の生産物）のアイテム名
  // The recipe name is the representative output's (first product's) item name
  const name = recipe.outputItems.length > 0 ? resolveItemName(recipe.outputItems[0].itemId) : "";

  return (
    <Box
      className={styles.row}
      data-testid={`machine-recipe-${recipe.recipeGuid}`}
      data-selected={row.selected ? "true" : undefined}
      role="button"
      onClick={() => onSelect(recipe.recipeGuid)}
    >
      <Text className={styles.name} data-testid={`machine-recipe-${recipe.recipeGuid}-name`}>{name}</Text>
      <RecipeRow
        testId={`machine-recipe-${recipe.recipeGuid}-row`}
        arrowValue={null}
        arrowTestId={`machine-recipe-${recipe.recipeGuid}-arrow`}
        duration={t(L.ui.blockInventory.recipeDuration, { seconds: recipe.time })}
        materials={recipe.inputItems.map((item, i) => <ItemSlot key={i} itemId={item.itemId} count={item.count} />)}
        action={null}
        result={recipe.outputItems.map((item, i) => <ItemSlot key={i} itemId={item.itemId} count={item.count} />)}
      />
    </Box>
  );
}
```

`MachineRecipeSelectionList.tsx`:

```tsx
// 機械のレシピ選択モード本体。行クリックで選択Actionを送り、親へ遷移を通知する
// The machine's recipe-selection mode; a row click dispatches the select action and notifies the parent
import { Stack } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { MachineRecipeSelectionRowData } from "./machineRecipeSelectionLogic";
import MachineRecipeSelectionRow from "./MachineRecipeSelectionRow";

type Props = { rows: MachineRecipeSelectionRowData[]; onSelected: () => void };

export default function MachineRecipeSelectionList({ rows, onSelected }: Props) {
  const onSelect = (recipeGuid: string) => {
    void dispatchAction("machine_recipe.select", { operation: "set", recipeGuid });
    onSelected();
  };
  return (
    <Stack gap="xs" data-testid="machine-recipe-selection">
      {rows.map((row) => <MachineRecipeSelectionRow key={row.recipe.recipeGuid} row={row} onSelect={onSelect} />)}
    </Stack>
  );
}
```

`machineRecipeSelectionList.module.css`（色相・光彩を足さない。選択は `RecipeBox` 既存枠に `data-selected` でシアン系ベベルを写す。値は `slotTokens.css` の `--sel-c*` 族と同じ変数を参照）:

```css
/* 行全体がクリック対象。名前は行の上辺に置き、選択中は枠の外周を選択シアンで示す */
/* The whole row is the click target; the name sits on the top edge and a selected row shows the selection cyan on its outline */
.row {
  cursor: pointer;
  min-width: 0;
}

.name {
  color: var(--text-muted);
  font-size: var(--recipe-info-text-size);
  line-height: 1;
  padding: 0 0.75rem 0.25rem;
}

.row[data-selected="true"] :global(.recipeBox) {
  border-color: var(--select-cyan);
}
```

`:global(.recipeBox)` が CSS Modules のハッシュ名で当たらない場合は、`RecipeRow` に `className` 追加はせず、`.row[data-selected="true"]` 自身に `outline: 1px solid var(--select-cyan); outline-offset: -1px;` を付ける形へ切り替える。

`SelectedRecipeHeader.tsx`:

```tsx
// インベントリモード上部の選択中レシピ表示。クリックでレシピ選択モードへ戻る（ADR 0042 R2）
// Selected-recipe header atop the inventory mode; clicking returns to recipe selection (ADR 0042 R2)
import { Group, Text } from "@mantine/core";
import type { MachineRecipe } from "@/bridge";
import { HoverTooltip, ItemSlot } from "@/shared/ui";
import { L, useI18n, useItemNameResolver } from "@/shared/i18n";

type Props = { recipe: MachineRecipe; onChangeRecipe: () => void };

export default function SelectedRecipeHeader({ recipe, onChangeRecipe }: Props) {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();
  const iconItemId = recipe.outputItems[0]?.itemId ?? recipe.inputItems[0]?.itemId ?? 0;
  return (
    <HoverTooltip label={t(L.ui.blockInventory.changeRecipe)} disabled={false}>
      <Group justify="center" gap="xs" role="button" data-testid="machine-selected-recipe" style={{ cursor: "pointer" }} onClick={onChangeRecipe}>
        <ItemSlot itemId={iconItemId} />
        <Text data-testid="machine-selected-recipe-name">{resolveItemName(iconItemId)}</Text>
        <Text c="dimmed" size="sm" data-testid="machine-selected-recipe-time">{t(L.ui.blockInventory.recipeDuration, { seconds: recipe.time })}</Text>
      </Group>
    </HoverTooltip>
  );
}
```

`HoverTooltip` が `@/shared/ui` から export されていなければ `@/shared/ui/HoverTooltip` を直接 import する。インラインstyleが lint で弾かれるなら `machineRecipeSelectionList.module.css` に `.header { cursor: pointer; }` を足して使う。

Run: `npx vitest run src/features/blockInventory/details/machine/MachineRecipeSelectionRow.test.ts`
Expected: PASS

- [x] **Step 5: MachineSection を2モードへ**

`MachineSection.test.ts` を次の観点で書き直す（既存のモック様式を踏襲し、`./machine/MachineRecipeSelectionList`・`./machine/SelectedRecipeHeader`・`./machine/MachineInventoryBody` をモック）:

```ts
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen, MachineDetailData } from "@/bridge";

const recipeGuid = "84000000-0000-4000-8000-000000000001";
const blockGuid = "85000000-0000-4000-8000-000000000001";
const otherBlockGuid = "85000000-0000-4000-8000-000000000002";
const emptyGuid = "00000000-0000-0000-0000-000000000000";

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopic: () => ({
    recipes: [{
      recipeGuid, blockGuid, blockId: 10, time: 7,
      inputItems: [{ itemId: 1, count: 2 }], outputItems: [{ itemId: 2, count: 1 }],
      inputFluids: [], outputFluids: [],
    }],
  }),
}));
vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
vi.mock("@mantine/core", () => ({
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
}));
vi.mock("./LackHighlightText", () => ({ default: (props: object) => createElement("mock-lack", props) }));
vi.mock("./PowerRateText", () => ({ default: (props: object) => createElement("mock-power", props) }));
vi.mock("./machine/MachineInventoryBody", () => ({ default: (props: object) => createElement("mock-inventory-body", props) }));
vi.mock("./machine/MachineRecipeSelectionList", () => ({ default: (props: object) => createElement("mock-recipe-selection-list", props) }));
vi.mock("./machine/SelectedRecipeHeader", () => ({ default: (props: object) => createElement("mock-selected-recipe-header", props) }));

import MachineSection from "./MachineSection";

function machine(selectedRecipeGuid: string, machineBlockGuid: string): MachineDetailData {
  return {
    recipeGuid: emptyGuid, selectedRecipeGuid, blockGuid: machineBlockGuid, recipeTime: 7,
    outputItems: [], currentState: "idle", currentPower: 0, requestPower: 0,
    slotLayout: { input: 2, output: 1, module: 0, inputTank: 0 },
  };
}
const data = { open: true, itemSlots: [], fluidSlots: [], progress: null } as unknown as BlockInventoryOpen;

describe("MachineSection", () => {
  it("未選択機械はレシピ選択リストを出し、インベントリ本体を出さない", () => {
    const tree = create(createElement(MachineSection, { data, machine: machine(emptyGuid, blockGuid) }));
    expect(tree.root.findAllByType("mock-recipe-selection-list" as never)).toHaveLength(1);
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(0);
  });

  it("選択済機械はヘッダ＋本体を出し、ヘッダのonChangeRecipeでリストへ戻り、onSelectedで本体へ戻る", () => {
    const tree = create(createElement(MachineSection, { data, machine: machine(recipeGuid, blockGuid) }));
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(1);
    const header = tree.root.findByType("mock-selected-recipe-header" as never);
    act(() => header.props.onChangeRecipe());
    const list = tree.root.findByType("mock-recipe-selection-list" as never);
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(0);
    act(() => list.props.onSelected());
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(1);
  });

  it("レシピ0件の機械はヘッダもリストも出さず本体だけ出す", () => {
    const tree = create(createElement(MachineSection, { data, machine: machine(emptyGuid, otherBlockGuid) }));
    expect(tree.root.findAllByType("mock-inventory-body" as never)).toHaveLength(1);
    expect(tree.root.findAllByType("mock-recipe-selection-list" as never)).toHaveLength(0);
    expect(tree.root.findAllByType("mock-selected-recipe-header" as never)).toHaveLength(0);
  });
});
```

`MachineDetailData` の実フィールドは `src/bridge/contract/schemas/inventory.ts:44` 付近の `MachineDetailDataSchema` に合わせる（不足フィールドがあれば追加）。

`MachineSection.tsx`:

```tsx
import { useState } from "react";
import { Group, Stack } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import type { BlockInventoryOpen, MachineDetailData } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import LackHighlightText from "./LackHighlightText";
import PowerRateText from "./PowerRateText";
import { machineStateDisplay } from "./detailLogic";
import MachineInventoryBody from "./machine/MachineInventoryBody";
import MachineRecipeSelectionList from "./machine/MachineRecipeSelectionList";
import SelectedRecipeHeader from "./machine/SelectedRecipeHeader";
import { buildMachineRecipeSelectionRows, hasSelectedRecipe } from "./machine/machineRecipeSelectionLogic";

// 機械: 未選択→レシピ選択モード、選択済→インベントリモード。ヘッダで選択モードへ戻れる（ADR 0042）
// Machine: unselected → recipe-selection mode, selected → inventory mode; the header returns to selection (ADR 0042)
export default function MachineSection({ data, machine }: { data: BlockInventoryOpen; machine: MachineDetailData }) {
  const machineRecipes = useTopic(Topics.machineRecipes);
  // プレイヤーが選択画面へ戻った状態。選択が届いた時点で自動的にインベントリへ戻る
  // Whether the player returned to the selection screen; a new selection drops back to inventory automatically
  const [changingRecipe, setChangingRecipe] = useState(false);
  const { t } = useI18n();

  const rows = buildMachineRecipeSelectionRows(machineRecipes?.recipes ?? [], machine.blockGuid, machine.selectedRecipeGuid);
  const selectedRow = rows.find((row) => row.selected);
  const stateDisplay = machineStateDisplay(machine.currentState);
  const footer = (
    <Group justify="center" gap="xs">
      <LackHighlightText insufficient={stateDisplay.insufficient} size="sm" testId="machine-state-label">{t(stateDisplay.labelKey)}</LackHighlightText>
      {stateDisplay.showPowerRate && <PowerRateText currentPower={machine.currentPower} requestPower={machine.requestPower} testId="machine-power-rate" />}
    </Group>
  );

  if (rows.length === 0) {
    return <Stack gap="xs" data-testid="machine-section"><MachineInventoryBody data={data} recipe={null} />{footer}</Stack>;
  }

  const showSelection = !hasSelectedRecipe(machine.selectedRecipeGuid) || selectedRow === undefined || changingRecipe;
  return (
    <Stack gap="sm" data-testid="machine-section">
      {showSelection ? (
        <MachineRecipeSelectionList rows={rows} onSelected={() => setChangingRecipe(false)} />
      ) : (
        <>
          <SelectedRecipeHeader recipe={selectedRow.recipe} onChangeRecipe={() => setChangingRecipe(true)} />
          <MachineInventoryBody data={data} recipe={selectedRow.recipe} />
        </>
      )}
      {footer}
    </Stack>
  );
}
```

削除: `MachineRecipeSelectionTab.tsx` / `.test.ts` / `machineRecipeSelection.module.css`。`ModeSwitch` の他利用が無くなっても共有部品は残す。

Run: `npx vitest run && npm run typecheck && npm run lint`
Expected: PASS

- [x] **Step 6: コミット**

```bash
git add -A moorestech_web/webui Localization
git commit -m "feat(webui): 機械UIをタブ廃止の2モード＋レシピ名付き行リストにする (ADR 0042 R1-R4)"
```

---

### Task 7: e2e とモックフィクスチャ、チュートリアルアンカー確認

**Files:**
- Modify: `moorestech_web/webui/e2e/mock-host/blockDetailFixtures.ts:9-31`（`blockMachine`）
- Modify: `moorestech_web/webui/e2e/tests/block/machineRecipe.spec.ts`
- Modify: `moorestech_web/webui/e2e/tests/block/machineGestures.spec.ts`（必要時）

- [x] **Step 1: フィクスチャを新仕様と整合させる**

`blockMachine`: `itemSlots` を `[{ itemId: 2, count: 5 }, empty(), empty(), empty()]`（選択中 `bbbbbbbb` の素材 itemId 2 がスロット0、出力スロットは空）、`fluidSlots` を `[empty fluid (fluidId 0, amount 0, capacity 100, fluidGuid "")]` にして液体ゴーストが出る状態にし、`slotLayout` に `inputTank: 1` を足す（Task 5 のワイヤ変更）。他フィクスチャの `slotLayout` にも `inputTank: 0` を足す。

- [x] **Step 2: machineRecipe.spec.ts を書き直す**

```ts
import { test, expect } from "@playwright/test";
import { setBlock } from "../../support/mockControl";

const firstRecipeTestId = "machine-recipe-aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const selectedRecipeTestId = "machine-recipe-bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";

test.afterEach(async ({ page }) => {
  await setBlock(page, "closed");
});

test("選択済機械は大型パネルでヘッダ＋レシピ分スロット＋ゴーストを出し、タブを持たない", async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toHaveAttribute("data-large", "true");
  await expect(page.getByTestId("machine-tab-switch")).toHaveCount(0);
  await expect(page.getByTestId("machine-selected-recipe")).toBeVisible();
  await expect(page.getByTestId("machine-selected-recipe-time")).toContainText("10");
  // 入力は素材数(1)・出力は生産物数(1)だけ描く（機械は入2/出1）
  // Draw only recipe-count slots: 1 input, 1 output (the machine itself has 2/1)
  await expect(page.getByTestId("machine-input-slots").locator("> div")).toHaveCount(1);
  await expect(page.getByTestId("machine-output-slots").locator("> div")).toHaveCount(1);
  // 空の出力スロットはゴースト、実物のある入力スロットはゴースト無し
  // The empty output slot is a ghost; the occupied input slot is not
  await expect(page.getByTestId("machine-output-slots").locator('[data-ghost="true"]')).toHaveCount(1);
  await expect(page.getByTestId("machine-input-slots").locator('[data-ghost="true"]')).toHaveCount(0);
  await expect(page.getByTestId("machine-fluid-slots").locator('[data-ghost="true"]')).toHaveCount(1);
  await expect(page.getByTestId("machine-power-rate")).toBeVisible();
  await expect(page.getByTestId("machine-state-label")).toBeVisible();
});

test("ヘッダクリックでレシピ選択モードへ戻り、行クリックでインベントリモードへ戻る", async ({ page }) => {
  await setBlock(page, "machine");
  await page.goto("/");
  await page.getByTestId("machine-selected-recipe").click();
  const selection = page.getByTestId("machine-recipe-selection");
  await expect(selection).toBeVisible();
  await expect(page.getByTestId("machine-inventory-body")).toHaveCount(0);
  await expect(selection.locator('[data-testid^="machine-recipe-"][data-testid$="-name"]')).toHaveCount(3);
  await expect(page.getByTestId(selectedRecipeTestId)).toHaveAttribute("data-selected", "true");
  await expect(page.getByTestId(`${selectedRecipeTestId}-row-duration`)).toContainText("10");

  // 右クリックは解除を送らない（選択が残る）
  // Right-click never clears (the selection stays)
  await page.getByTestId(selectedRecipeTestId).click({ button: "right" });
  await expect(page.getByTestId(selectedRecipeTestId)).toHaveAttribute("data-selected", "true");

  await page.getByTestId(firstRecipeTestId).click();
  await expect(page.getByTestId("machine-inventory-body")).toBeVisible();
  await expect(page.getByTestId("machine-selected-recipe-time")).toContainText("5");
});

test("レシピ未選択の機械はレシピ選択モードで開く", async ({ page }) => {
  await setBlock(page, "gearMachine");
  await page.goto("/");
  await expect(page.getByTestId("machine-recipe-selection")).toBeVisible();
  await expect(page.getByTestId("machine-inventory-body")).toHaveCount(0);
});

test("レシピ無しブロックは小型パネルのまま", async ({ page }) => {
  await setBlock(page, "generator");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();
  await expect(page.getByTestId("block-inventory")).not.toHaveAttribute("data-large", "true");
  await expect(page.getByTestId("machine-recipe-selection")).toHaveCount(0);
});
```

`SlotGrid` の直下要素が `ItemSlot` の div でない（Tooltip ラッパ等）場合は `locator("> div")` を `locator('[data-testid^="slot"]')` 等、実DOMに合わせて調整する（`SlotGrid`/`ItemSlot` の実装で確認）。

- [x] **Step 3: machineGestures.spec.ts と blockDetails.spec.ts を確認・修正**

```bash
grep -n "machine-tab\|machine-selected-product\|machine-recipe-detail" e2e/tests/**/*.ts e2e/support/*.ts
```
該当箇所を新testId（`machine-selected-recipe`）へ置換。`machineGestures.spec.ts` は入力スロットのインデックス（表示1枚目=統合スロット0）が変わらないことを確認して通す。

Run: `npm run test:e2e -- e2e/tests/block/machineRecipe.spec.ts e2e/tests/block/machineGestures.spec.ts e2e/tests/block/blockDetails.spec.ts e2e/tests/block/fluidSlot.spec.ts`（e2eポート衝突に注意: 並列worktreeがあれば `webui-e2e-port-collision` メモリ参照）
Expected: PASS

- [x] **Step 4: チュートリアルアンカーの追従確認**

```bash
grep -rn "machine-tab\|machine-selected-product\|machine-recipe" ../moorestech_master/server_v8 moorestech_web/webui/src/shared/anchors* 2>/dev/null
```
ヒットがあればアンカー語彙とマスタJSONを新testIdへ更新し、`../moorestech_master` 側は別PRを作ってピン（`.moorestech-external-revisions.json`）を更新する（AGENTS.md 関連リポジトリ規約）。ヒット0ならこのステップは完了。

- [x] **Step 5: コミット**

```bash
git add -A moorestech_web/webui
git commit -m "test(webui): 機械UI2モード・ゴーストのe2eとモックフィクスチャを更新 (ADR 0042)"
```

---

### Task 8: 通し確認と全ブランチレビュー（省略不可）

- [ ] **Step 1: サーバー・クライアント全テストとWebの全検査**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Machine|Inventory|CleanRoom|Gear|Blueprint|Wire|SaveLoad"
cd moorestech_web/webui && npm run lint && npm run typecheck && npx vitest run && npm run test:e2e
```
Expected: 全PASS。失敗があれば当該タスクへ戻って修正する（結果をそのまま報告し、通っていないものを通ったと書かない）。

- [ ] **Step 2: 実プレイ確認（unityプレイ録画テスト）**

`unity-playmode-recorded-playtest` スキルで、電気機械を設置→開く→レシピ選択モード表示→行クリック→ゴースト付きインベントリ→素材を誤スロットへドロップして拒否される→正スロットへ入る、までを1シナリオで録画する。masterピンは `.moorestech-external-revisions.json` に従う。

- [ ] **Step 3: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

`moores-code-review` を起動し、指摘を実コード照合のうえ適用する。設計判断は AskUserQuestion にまとめる。

- [ ] **Step 4: PR作成と撤収**

`pr-create` スキルでPRを作り、`bd close moorestech-j2kx --reason="PR #<番号>"`、`moores-wt rm feature/machine-ui-mode-split-ghost-slots`。

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 機構 | 前例 / 判定 |
|---|---|---|---|---|
| 1 | `MachineRecipeSlotBindingUtil`（純関数） | Game.Block/Blocks/Machine/RecipeSelection | static util、MasterHolder読取のみ | `MachineRecipeSelectionUtil.cs` / `MachineRecipeRefundUtil.cs` 同ディレクトリ・同形。ItemMaster等へは足さない（層マップ「ItemMasterへのメソッド追加は誤り」） |
| 2 | 束縛レシピの伝達 `SetBoundRecipe` | 入出力サブインベントリ | 具体側からの `SetHoge` プッシュ、`MachineProcessContext.BindSelectedRecipe` が唯一の書き手 | 層マップ「汎用基盤への状態伝達は SetHoge でプッシュ」、`GearEnergyTransformer.SetTorqueRequestRate` |
| 3 | `IVanillaMachineSubInventory.IsAllowedToPlace` | Game.Block（機械内部IF） | 判定は各サブインベントリが自前で持つ | `ISortExcludedSlots` のように機械コンポーネントが規則を宣言する既存形。`IOpenableInventory`（Core.Inventory 汎用）には足さない |
| 4 | プレイヤー操作の拒否 | `VanillaMachineBlockInventoryComponent.ReplaceItem/SetItem` | 拒否＝そのまま返す/無視 | `InventoryItemMoveService` は `ReplaceItem` の戻り値を残余として扱う既存契約に乗る。ロードは `SetItemWithoutEvent`（サブインベントリ直）で影響なし |
| 5 | 液体の受入ゲート | `VanillaMachineFluidInventoryComponent.AddLiquid` | 入力インベントリの `IsFluidAllowedAt` を問い合わせ | 同メソッドが既にタンク指定分岐を持つ。`FluidContainer`（汎用）にレシピ知識を入れない |
| 6 | 整理の除外 | `SortExcludedSlots` 全スロット | 既存IF | `ISortExcludedSlots`（モジュール除外の前例を全域へ） |
| 7 | レシピ液体のワイヤ | `MachineRecipeDto.InputFluids/OutputFluids` | 既存トピック拡張、新プロトコル無し | `ResearchUnlockFluidDto`（`{FluidId, FluidGuid, Amount}`）と同型。ゴースト内容はマスタ由来で既存 `machine_recipes` トピックが既に同情報を運ぶため「導出」に該当し3点セット不要 |
| 8 | `SlotLayoutDto.InputTank` | `BlockDetailDtoBuilder` | 既存DTO拡張 | 同DTOの `Input/Output/Module` |
| 9 | ゴースト表現 | `SlotFrame data-ghost` + `--slot-ghost-opacity` | data属性＋トークン | §4「新しい状態は data 属性を追加」、`data-insufficient` の opacity 表現 |
| 10 | 表示スロット導出 `buildMachineSlotView` | features/blockInventory/details/machine | 純関数（vitest） | `machineRecipeSelectionLogic.ts` / `detailLogic.splitSlotIndices` |
| 11 | 選択行 | `RecipeRow` 流用 | 表示専用骨格へ中身だけ渡す | §8.17「骨格は RecipeRow 1枚に集約」、`MachineRecipeEntry.tsx` |
| 12 | モード状態 | `MachineSection` の `useState(changingRecipe)` | 選択GUIDはサーバー権威（`block_inventory.current`）、戻り操作だけローカル | 旧 `tab` state と同位置。選択の正はサーバー（既存の `machine_recipe.select` → state イベント再配信） |

データフロー: `machine_recipe.select` →（既存プロトコル）→ `MachineProcessContext.BindSelectedRecipe` →［入出力サブインベントリの束縛］→ `InsertItem/ReplaceItem/AddLiquid` が規則を適用 → `MachineBlockStateDetail.SelectedRecipeGuid` → `block_inventory.current` → Web が `machine_recipes` と突き合わせて表示スロット・ゴーストを導出。新規の書き込み経路・交差点は無い（Web は読み手、サーバー束縛は既存選択の下流）。

機能パリティ（死活表）:

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 機械を開いてレシピを選ぶ | 生きる | 行リスト（R3） |
| 選択レシピを変更する | 生きる | ヘッダクリック→リスト→別行（R2） |
| 選択を解除して未選択へ戻す | **消える（裁定済み）** | `.decisions/2026-08-30-機械UIにレシピ解除の導線は設けない.md` |
| 入力スロットへの手投入/シフト移動/右クリック分割/ダブルクリック収集 | 生きる（束縛内のみ） | ジェスチャ配線は index 不変。束縛外はサーバーが拒否（裁定済み） |
| 機械インベントリの整理（sort） | **無効化** | 全スロット束縛で並べ替えの意味が無い。`SortExcludedSlots` 全域（agent前提。プレイヤー体験上の差は「押しても何も起きない」のみ） |
| ベルトからの自動投入 | 生きる（束縛内のみ） | `InsertItem` が素材iをスロットiへ |
| 液体パイプからの流入 | 生きる（束縛内のみ） | `AddLiquid` ゲート。タンク指定接続で束縛外は滞留（裁定「サーバーもスロット固定」の帰結） |
| モジュール装備 | 生きる | モジュールは `IsAllowedToPlace` 常にtrue、既存プロトコルのまま |
| ブループリントのレシピ復元 | 生きる | `MachineRecipeBlueprintSettingsComponent` は `SetSelectedRecipe` 経由で `BindSelectedRecipe` を通る |
| セーブ/ロード | 生きる | ロードは `SetItemWithoutEvent`、`BindSelectedRecipe` を復元ctorで呼ぶ |

## 判断記録（ADR）

- 設計ADR: `docs/adr/0042-machine-ui-satisfactory-mode-split-and-ghost-slots.md`（裁定7件＋agent前提6件）。
- planning中の追加判断（すべて agent前提）:
  1. 出力スロットの束縛は「実現出力k → スロット k % 生産物数」の番号束縛（品質変種でIDが変わるため）。出所: agent前提（`MachineOutputFactoryUtil.ApplyQualityLevel` の実装に基づく）
  2. `SetItem`（`IOpenableInventory` 面）も束縛ガードを掛け、ロード復元はサブインベントリの `SetItemWithoutEvent` に限る。出所: agent前提（`InventoryItemMoveService` の全量swap経路が `SetItem` を使うため、ガード無しでは手投入の抜け道になる）
  3. 機械の整理（sort）は全スロット除外で実質無効化。出所: agent前提（束縛スロットの並べ替えは規則を壊す）
  4. 入力タンク数を `SlotLayoutDto.InputTank` としてワイヤへ追加（既存DTO拡張）。出所: agent前提（Webが入力/出力タンク境界を知る手段が無い）
  5. `crafting.machine_recipes` へ液体を追加（新プロトコル無し）。出所: agent前提（研究トピックの `ResearchUnlockFluidDto` 同型）
  6. 既存セーブで束縛外の位置にあるアイテムは補正しない。出所: AGENTS.md「後方互換性は考慮不要」＋ユーザー確認（2026-08-30 最終確認 5）
