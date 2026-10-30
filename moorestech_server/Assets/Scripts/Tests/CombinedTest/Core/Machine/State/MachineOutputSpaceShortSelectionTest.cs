using System.Linq;
using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.UnlockState;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
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
            var other = UnlockedRecipeWithSameInputs(recipe);
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

            // 次tickの最後の通知で新レシピの理由が届く
            // The next tick's last notice carries the new recipe's reason
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

        [Test]
        public void 加工中からのレシピ変更の次tickは理由の通知と状態遷移の通知を重ねない()
        {
            var recipe = ItemRecipe();
            var other = UnlockedRecipeWithSameInputs(recipe);
            var (_, inventory, processor) = PlaceSelected(recipe, Vector3Int.one);
            InsertInputs(recipe, inventory);
            processor.SupplyExternalPower(processor.EffectiveRequestPower);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            // 旧産出物で出口を埋め、tick外で加工中からIdleへ書き換える（直前状態は加工中のまま残る）
            // Fill the outlet with the old product and rewrite processing to idle off-tick (the previous state stays processing)
            FillOutputSlot(recipe, inventory);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, processor.SetSelectedRecipe(other, CreateOverflow()));
            Assert.IsFalse(ReadOutputSpaceShort(processor));
            var counter = new MachineStateNotificationCounter();
            processor.OnChangeBlockState.Subscribe(_ => counter.Count++);

            // 理由が変わるtickは、変化の無いtickより通知がちょうど1件多い（どちらも電気網の給電通知を含む）
            // The tick where the reason changes notifies exactly one more than a steady tick (both include grid supply notices)
            GameUpdater.UpdateOneTick();
            Assert.IsTrue(ReadOutputSpaceShort(processor), "新レシピの確定結果が入りきらない待機");
            var changeTickCount = counter.Count;
            GameUpdater.UpdateOneTick();
            var steadyTickCount = counter.Count - changeTickCount;
            Assert.AreEqual(steadyTickCount + 1, changeTickCount, "理由の通知と状態遷移の通知が同じtickで重なった");
        }

        // 同じ入力で産出物だけ違う同一ブロックのレシピを解放して返す
        // Unlock and return a same-block recipe with identical inputs but a different product
        private static MachineRecipeMasterElement UnlockedRecipeWithSameInputs(MachineRecipeMasterElement recipe)
        {
            var other = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == recipe.BlockGuid && r.MachineRecipeGuid != recipe.MachineRecipeGuid
                && r.InputItems.Select(i => (i.ItemGuid, i.Count)).SequenceEqual(recipe.InputItems.Select(i => (i.ItemGuid, i.Count))) && r.OutputItems[0].ItemGuid != recipe.OutputItems[0].ItemGuid);
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockMachineRecipe(other.MachineRecipeGuid);
            return other;
        }

        private static OpenableInventoryItemDataStoreService CreateOverflow()
        {
            return new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 100);
        }
    }
}
