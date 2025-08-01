using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.Fluid;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Block
{
    public class MachineRecipeConfigTest
    {
        /// <summary>
        ///     レシピがある時のテスト
        /// </summary>
        [TestCase(1, new int[2] { 1, 2 }, 3, 1)]
        [TestCase(1, new int[2] { 2, 1 }, 3, 1)]
        [TestCase(3, new int[3] { 1, 2, 3 }, 5, 1)]
        [TestCase(3, new int[3] { 2, 1, 3 }, 5, 1)]
        public void RecipeInputItemBlockIdTest(int BlocksId, int[] items, int output0Id, double output0Percent)
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var inputItems = new List<IItemStack>();
            items.ToList().ForEach(i => inputItems.Add(itemStackFactory.Create(new ItemId(i), 1)));
            var inputFluids = new List<FluidContainer>();
            
            MachineRecipeMasterUtil.TryGetRecipeElement((BlockId)BlocksId, inputItems, inputFluids, out var ans);
            
            Assert.AreEqual(output0Id, MasterHolder.ItemMaster.GetItemId(ans.OutputItems[0].ItemGuid).AsPrimitive());
            Assert.AreEqual(output0Percent, ans.OutputItems[0].Percent);
        }
        
        [TestCase(3, new int[4] { 2, 1, 0, 5 }, 0)] //レシピが存在しない時のテスト
        [TestCase(0, new int[3] { 2, 1, 0 }, 0)] // not exist test
        [TestCase(3, new int[3] { 4, 1, 0 }, 0)]
        [TestCase(10, new int[1] { 2 }, 0)]
        [TestCase(0, new int[0], 0)]
        [TestCase(1, new int[2] { 2, 1 }, 1)] //存在するときのテストケース exist test
        public void NullRecipeTest(int BlocksId, int[] items, int outputLength)
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var inputItems = new List<IItemStack>();
            items.ToList().ForEach(i => inputItems.Add(itemStackFactory.Create(new ItemId(i), 1)));
            var inputFluids = new List<FluidContainer>();
            
            var ans = MachineRecipeMasterUtil.TryGetRecipeElement((BlockId)BlocksId, inputItems, inputFluids, out _);
            Assert.AreEqual(outputLength == 1, ans);
        }
        
        [TestCase(1, new int[2] { 1, 2 }, new int[2] { 3, 1 }, true)]
        [TestCase(1, new int[2] { 2, 1 }, new int[2] { 1, 3 }, true)]
        [TestCase(1, new int[2] { 2, 1 }, new int[2] { 1, 30 }, true)]
        [TestCase(1, new int[2] { 2, 1 }, new int[2] { 1, 1 }, false)]
        [TestCase(3, new int[3] { 1, 2, 3 }, new int[3] { 2, 3, 4 }, true)]
        [TestCase(3, new int[3] { 1, 2, 3 }, new int[3] { 4, 6, 8 }, true)]
        [TestCase(3, new int[3] { 1, 2, 3 }, new int[3] { 4, 6, 1 }, false)]
        [TestCase(3, new int[3] { 2, 1, 3 }, new int[3] { 3, 2, 4 }, true)]
        [TestCase(3, new int[3] { 2, 1, 3 }, new int[3] { 3, 1, 4 }, false)]
        [TestCase(3, new int[4] { 2, 1, 0, 5 }, new int[4] { 3, 1, 4, 5 }, false)]
        public void RecipeConfirmationTest(int blocksId, int[] items, int[] itemcount, bool ans)
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var itemStacks = new List<IItemStack>();
            for (var i = 0; i < items.Length; i++)
            {
                var itemId = new ItemId(items[i]);
                itemStacks.Add(itemStackFactory.Create(itemId, itemcount[i]));
            }
            var inputFluids = new List<FluidContainer>();

            
            MachineRecipeMasterUtil.TryGetRecipeElement((BlockId)blocksId, itemStacks, inputFluids, out var machineRecipeElement);
            
            if (!ans && machineRecipeElement == null)
            {
                Assert.Pass();
                return;
            }
            
            var a = machineRecipeElement.RecipeConfirmation((BlockId)blocksId, itemStacks, inputFluids);
            Assert.AreEqual(ans, a);
        }
    }
}