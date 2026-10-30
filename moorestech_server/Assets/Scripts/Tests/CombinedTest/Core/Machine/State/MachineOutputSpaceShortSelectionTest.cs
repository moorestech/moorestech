using System.Linq;
using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.UnlockState;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.MachineOutputSpaceShortTestHelper;

namespace Tests.CombinedTest.Core.Machine
{
    // レシピ解除・変更の通知と、その次tickの理由通知を確かめる
    // Verifies the recipe clear/change notices and the reason notice on the following tick
    public class MachineOutputSpaceShortSelectionTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void レシピ解除の直後の通知に古い理由を残さない()
        {
            var recipe = ItemRecipe();
            var (_, inventory, processor) = PlaceSelected(recipe, Vector3Int.one);
            FillOutputSlot(recipe, inventory);
            InsertInputs(recipe, inventory);
            GameUpdater.UpdateOneTick();
            Assert.IsTrue(ReadOutputSpaceShort(processor));

            // tickを待たず、解除の通知時点の詳細で理由が消えている
            // Without waiting a tick, the detail at the clear notice already drops the reason
            var notices = RecordReasonNotices(processor);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, processor.ClearSelectedRecipe(CreateOverflow()));
            CollectionAssert.AreEqual(new[] { false }, notices);
            Assert.IsFalse(ReadOutputSpaceShort(processor));

            // 解除後の待機は次tickの通知でも理由を持たない
            // The idle after clearing carries no reason in the next tick's notices either
            GameUpdater.UpdateOneTick();
            CollectionAssert.DoesNotContain(notices, true);
        }

        [Test]
        public void 別レシピへの変更の通知に旧レシピの理由を残さず次tickで新レシピの理由を通知する()
        {
            // 同じ入力で産出物だけ違う同一ブロックのレシピへ切り替える（入力はそのまま束縛に残る）
            // Switch to a same-block recipe with identical inputs but a different product (inputs stay bound)
            var recipe = ItemRecipe();
            var other = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == recipe.BlockGuid && r.MachineRecipeGuid != recipe.MachineRecipeGuid
                && r.InputItems.Select(i => (i.ItemGuid, i.Count)).SequenceEqual(recipe.InputItems.Select(i => (i.ItemGuid, i.Count))) && r.OutputItems[0].ItemGuid != recipe.OutputItems[0].ItemGuid);
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockMachineRecipe(other.MachineRecipeGuid);
            var (_, inventory, processor) = PlaceSelected(recipe, Vector3Int.one);
            FillOutputSlot(recipe, inventory);
            InsertInputs(recipe, inventory);
            GameUpdater.UpdateOneTick();
            Assert.IsTrue(ReadOutputSpaceShort(processor));

            // 変更の通知時点では旧レシピの確定結果を使わない
            // At the change notice the old recipe's realization is not reused
            var notices = RecordReasonNotices(processor);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, processor.SetSelectedRecipe(other, CreateOverflow()));
            Assert.AreEqual(other.MachineRecipeGuid, processor.SelectedRecipeGuid);
            CollectionAssert.AreEqual(new[] { false }, notices, "旧レシピの理由が変更の通知に残っている");

            // 次tickで新レシピを確定し、旧産出物で埋まった出口に入らない理由がそのtickの最後の通知で届く
            // The next tick realizes the new recipe, and its wait on the outlet full of the old product arrives in that tick's last notice
            var noticeCountBeforeTick = notices.Count;
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.IsTrue(ReadOutputSpaceShort(processor), "新レシピの確定結果が入りきらない待機");
            Assert.Less(noticeCountBeforeTick, notices.Count);
            Assert.IsTrue(notices.Last(), "次tickの新レシピの理由が通知されない");

            // 出口を空けると新レシピで加工が始まる
            // Freeing the outlet starts the new recipe's job
            EmptyOutputSlot(inventory);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(other.MachineRecipeGuid, processor.RecipeGuid);
            Assert.IsFalse(ReadOutputSpaceShort(processor));
            Assert.IsFalse(notices.Last());
        }

        private static OpenableInventoryItemDataStoreService CreateOverflow()
        {
            return new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 100);
        }
    }
}
