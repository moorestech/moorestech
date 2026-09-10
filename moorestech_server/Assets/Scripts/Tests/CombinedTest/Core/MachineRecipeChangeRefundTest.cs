using System;
using System.Linq;
using Core.Inventory;
using Core.Item;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.RecipeSelection;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.UnlockState;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    // 加工中のレシピ変更・クリア時の材料返却フローを検証する
    // Verifies material refund flow when changing or clearing a recipe mid-processing
    public class MachineRecipeChangeRefundTest
    {
        [Test]
        public void RefundToInputInventoryTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var next = MachineRecipeChangeRefundTestHelper.FindAlternateRecipe(recipe);
            Assert.IsNotNull(next);
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            MachineRecipeChangeRefundTestHelper.StartProcessing(selector, processor, inventory, recipe, overflow);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(next, overflow));
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.AreEqual(next.MachineRecipeGuid, selector.SelectedRecipeGuid);

            var (input, output) = MachineRecipeChangeRefundTestHelper.GetNonEmptySlots(inventory);
            Assert.AreEqual(0, output.Count);
            foreach (var inputItem in recipe.InputItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid);
                Assert.AreEqual(inputItem.Count, MachineRecipeChangeRefundTestHelper.CountItem(input, itemId));
            }
        }

        [Test]
        public void RefundOverflowGoesToPlayerInventoryTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var next = MachineRecipeChangeRefundTestHelper.FindAlternateRecipe(recipe);
            Assert.IsNotNull(next);
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            MachineRecipeChangeRefundTestHelper.StartProcessing(selector, processor, inventory, recipe, overflow);
            MachineRecipeChangeRefundTestHelper.FillInputSlotsToCapacity(inventory, recipe);

            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(next, overflow));
            foreach (var inputItem in recipe.InputItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid);
                Assert.AreEqual(inputItem.Count, MachineRecipeChangeRefundTestHelper.CountOverflowItem(overflow, itemId));
            }
        }

        [Test]
        public void RefundImpossibleCancelsChangeTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var next = MachineRecipeChangeRefundTestHelper.FindAlternateRecipe(recipe);
            Assert.IsNotNull(next);
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            MachineRecipeChangeRefundTestHelper.StartProcessing(selector, processor, inventory, recipe, overflow);
            MachineRecipeChangeRefundTestHelper.FillInputSlotsToCapacity(inventory, recipe);
            var (inputBefore, _) = MachineRecipeChangeRefundTestHelper.GetNonEmptySlots(inventory);
            var beforeSnapshot = inputBefore.Select(i => (i.Id, i.Count)).ToList();

            var noOverflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(0);
            Assert.AreEqual(MachineRecipeSelectionResult.RefundFailed, selector.SetSelectedRecipe(next, noOverflow));
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(recipe.MachineRecipeGuid, selector.SelectedRecipeGuid);

            var (inputAfter, _) = MachineRecipeChangeRefundTestHelper.GetNonEmptySlots(inventory);
            Assert.AreEqual(beforeSnapshot.Count, inputAfter.Count);
            for (var i = 0; i < beforeSnapshot.Count; i++)
            {
                Assert.AreEqual(beforeSnapshot[i].Id, inputAfter[i].Id);
                Assert.AreEqual(beforeSnapshot[i].Count, inputAfter[i].Count);
            }
        }

        [Test]
        public void ClearDuringProcessingRefundsTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            MachineRecipeChangeRefundTestHelper.StartProcessing(selector, processor, inventory, recipe, overflow);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.ClearSelectedRecipe(overflow));
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.AreEqual(Guid.Empty, selector.SelectedRecipeGuid);

            var (input, _) = MachineRecipeChangeRefundTestHelper.GetNonEmptySlots(inventory);
            foreach (var inputItem in recipe.InputItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid);
                Assert.AreEqual(inputItem.Count, MachineRecipeChangeRefundTestHelper.CountItem(input, itemId));
            }
        }

        [Test]
        public void SameRecipeReSelectIsNoOpTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            MachineRecipeChangeRefundTestHelper.StartProcessing(selector, processor, inventory, recipe, overflow);
            var remainingBefore = processor.GetRemainingTicks();
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(recipe, overflow));
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(remainingBefore, processor.GetRemainingTicks());
            Assert.AreEqual(recipe.MachineRecipeGuid, processor.RecipeGuid);
        }

        [Test]
        // isRemain素材はジョブ返却の対象外(消費されていない)だが、新レシピの束縛から外れれば
        // 「束縛外になった入力スロットの未消費アイテム」としてプレイヤーへ自動返却される(2026-08-30裁定)
        // An isRemain material is never job-refunded (it was never consumed), but once the new recipe
        // unbinds its slot it counts as a leftover unconsumed input and is auto-refunded to the player (2026-08-30 ruling)
        public void IsRemainInputIsRefundedWhenUnboundByNewRecipeTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                .First(r => r.InputItems.Any(i => i.IsRemain.HasValue && i.IsRemain.Value));
            var next = MachineRecipeChangeRefundTestHelper.FindAlternateRecipe(recipe);
            Assert.IsNotNull(next);
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            MachineRecipeChangeRefundTestHelper.StartProcessing(selector, processor, inventory, recipe, overflow);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(next, overflow));

            var remain = recipe.InputItems.First(i => i.IsRemain.HasValue && i.IsRemain.Value);
            var remainId = MasterHolder.ItemMaster.GetItemId(remain.ItemGuid);
            var (input, _) = MachineRecipeChangeRefundTestHelper.GetNonEmptySlots(inventory);
            Assert.AreEqual(0, MachineRecipeChangeRefundTestHelper.CountItem(input, remainId));
            Assert.AreEqual(remain.Count, MachineRecipeChangeRefundTestHelper.CountOverflowItem(overflow, remainId));
        }

        [Test]
        // ジョブに一度も入らなかった(未消費の)入力スロットの素材も、新レシピで束縛外になれば
        // プレイヤーへ自動返却されることを検証する(2026-08-30裁定)
        // Verifies a material that was never consumed by any job is also auto-refunded to the player
        // once the new recipe's binding no longer covers its slot (2026-08-30 ruling)
        public void UnboundLeftoverInputIsRefundedToPlayerTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var next = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                .First(r => r.InputItems.Any(i => i.IsRemain.HasValue && i.IsRemain.Value));
            var recipe = MachineRecipeChangeRefundTestHelper.FindAlternateRecipe(next);
            Assert.IsNotNull(recipe);
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, _, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            // recipeを選択し、素材1(=recipe.InputItems[1])を入力スロットへ置くだけで一度も処理しない
            // Select recipe and place material 1 (recipe.InputItems[1]) in its slot without ever processing
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(recipe, overflow));
            var leftover = recipe.InputItems[1];
            var leftoverItemId = MasterHolder.ItemMaster.GetItemId(leftover.ItemGuid);
            inventory.InsertItem(ServerContext.ItemStackFactory.Create(leftoverItemId, leftover.Count));

            // nextでは同じスロットが別素材へ束縛されるため、素材1は束縛外の残留として返却される
            // next binds the same slot to a different material, so material 1 becomes an unbound leftover and gets refunded
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(next, overflow));

            var (input, _) = MachineRecipeChangeRefundTestHelper.GetNonEmptySlots(inventory);
            Assert.AreEqual(0, MachineRecipeChangeRefundTestHelper.CountItem(input, leftoverItemId));
            Assert.AreEqual(leftover.Count, MachineRecipeChangeRefundTestHelper.CountOverflowItem(overflow, leftoverItemId));
        }

        [Test]
        // 返却先(プレイヤーのインベントリ)が満杯なら、束縛外になった素材は機械に残り消失しないことを検証する(2026-08-30裁定)
        // Verifies an unbound leftover material stays on the machine (never lost) when the player's inventory is full (2026-08-30 ruling)
        public void UnboundLeftoverInputStaysOnMachineWhenOverflowFullTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var next = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                .First(r => r.InputItems.Any(i => i.IsRemain.HasValue && i.IsRemain.Value));
            var recipe = MachineRecipeChangeRefundTestHelper.FindAlternateRecipe(next);
            Assert.IsNotNull(recipe);
            var roomyOverflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var fullOverflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(0);
            var (_, selector, _, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(recipe, roomyOverflow));
            var leftover = recipe.InputItems[1];
            var leftoverItemId = MasterHolder.ItemMaster.GetItemId(leftover.ItemGuid);
            inventory.InsertItem(ServerContext.ItemStackFactory.Create(leftoverItemId, leftover.Count));

            // 返却先に空きが無いため、束縛外になっても機械側のスロットへ残る（消失しない）
            // No room to refund into, so the unbound material stays in the machine's slot (never lost)
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(next, fullOverflow));

            var (input, _) = MachineRecipeChangeRefundTestHelper.GetNonEmptySlots(inventory);
            Assert.AreEqual(leftover.Count, MachineRecipeChangeRefundTestHelper.CountItem(input, leftoverItemId));
            Assert.AreEqual(0, MachineRecipeChangeRefundTestHelper.CountOverflowItem(fullOverflow, leftoverItemId));
        }

        [Test]
        public void FluidRefundBestEffortTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            // 対象レシピを明示アンロック
            // Explicitly unlock the LockedMachineRecipe fixture before selecting it
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockMachineRecipe(ForUnitTestMachineRecipeId.LockedMachineRecipe);

            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.LockedMachineRecipe);
            Assert.IsTrue(2 <= recipe.InputFluids.Length, "流体返却テストには2種類以上の入力液体が必要");
            var next = MachineRecipeChangeRefundTestHelper.FindAlternateRecipe(recipe);
            Assert.IsNotNull(next);
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            var (_, selector, processor, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);

            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(recipe, overflow));
            MachineRecipeChangeRefundTestHelper.InsertRecipeInputs(inventory, recipe);
            var tanks = MachineRecipeChangeRefundTestHelper.GetInputInventory(inventory).FluidInputSlot;
            MachineRecipeChangeRefundTestHelper.InsertRecipeFluids(tanks, recipe);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            Assert.IsTrue(3 <= tanks.Count);
            var (before0, before1, before2, expected0, expected1) = MachineRecipeChangeRefundTestHelper.PreparePartialFluidTanksForOverflowRefund(tanks, recipe, MachineFluidIOTest.FluidId3);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(next, overflow));
            Assert.AreEqual(next.MachineRecipeGuid, selector.SelectedRecipeGuid);
            Assert.AreEqual(expected0, tanks[0].Amount, 0.0001);
            Assert.AreEqual(expected1, tanks[1].Amount, 0.0001);
            Assert.AreEqual(before2, tanks[2].Amount, 0.0001);
            Assert.AreEqual(MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[0].FluidGuid), tanks[0].FluidId);
            Assert.AreEqual(MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[1].FluidGuid), tanks[1].FluidId);
            // 溢れた分は消失すること
            // Overflow beyond tank capacity is discarded and not preserved in total
            Assert.Less(expected0 + expected1, before0 + before1 + recipe.InputFluids[0].Amount + recipe.InputFluids[1].Amount);
        }

        [Test]
        // 返却シミュレーション(CanRefundAllItems)が実挿入(InsertItem)と同じ束縛規則を使うことを確認する回帰テスト
        // 修正前は汎用の空きスロット探索でシミュレートしており、束縛外スロットへ仮置きした結果が実挿入と食い違っていた
        // Regression test verifying the refund simulation uses the same binding rule as the real insert (ADR 0042 C7)
        // Before the fix it simulated with a generic free-slot search and could place refunds into an unbound slot, diverging from the real insert
        public void CanRefundAllItemsMatchesBoundInsertOutcomeTest()
        {
            MachineRecipeChangeRefundTestHelper.InitDi();
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var (_, selector, _, inventory) = MachineRecipeChangeRefundTestHelper.PlaceMachine(recipe);
            selector.SetSelectedRecipe(recipe, MachineRecipeChangeRefundTestHelper.CreateOverflow(0));

            // スロット0(素材0の束縛先)を束縛外の異物で塞ぐ。スロット1(素材1の束縛先)は空のまま
            // Block slot 0 (input 0's bound slot) with a foreign item; slot 1 (input 1's bound slot) stays empty
            var input = MachineRecipeChangeRefundTestHelper.GetInputInventory(inventory);
            var foreignItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            input.SetItemWithoutEvent(0, ServerContext.ItemStackFactory.Create(foreignItemId, 1));

            // 溢れ先の唯一のスロットも別種のアイテムで満杯にし、素材0の入る余地を無くす
            // Fill the overflow's sole slot with yet another item type, leaving no room for input 0
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(1);
            var input1ItemId = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[1].ItemGuid);
            // 満杯ではなく1個分だけ空きを残す。満杯にすると「束縛外への仮置き」バグと「本来の拒否」が
            // 同じ結果(false)になり回帰を検知できない(C12・mutation testing実測)
            // Leave exactly one slot of room instead of filling it completely. A full stack makes the
            // "placed into an unbound slot" bug and the correct rejection both return false, hiding the regression (C12, per mutation testing)
            var input1MaxStack = ItemStackLevelDataStore.Instance.GetMaxStack(input1ItemId);
            overflow.SetItemWithoutEvent(0, ServerContext.ItemStackFactory.Create(input1ItemId, input1MaxStack - 1));

            var refunds = MachineRecipeRefundUtil.CreateRefundStacks(recipe);

            // 素材0はスロット0が異物で塞がり、溢れ先も別種で埋まっているため全量収容できない
            // Input 0 cannot fit anywhere: its bound slot holds a foreign item and the overflow is full of yet another type
            Assert.IsFalse(MachineRecipeRefundUtil.CanRefundAllItems(input, overflow, refunds));
        }
    }
}
