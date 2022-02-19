using System.Collections.Generic;
using System.Linq;
using Core.Block.RecipeConfig;
using Core.Item;
using Core.Item.Config;
using Core.Item.Implementation;
using Core.Item.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.UnitTest.Core.Block
{
    [TestClass]
    public class MachineRecipeConfigTest
    {
        private TestMachineRecipeConfig _testMachineRecipeConfig;
        private ItemStackFactory _itemStackFactory;

        [TestInitialize]
        public void Setup()
        {
            _itemStackFactory = new ItemStackFactory(new TestItemConfig());
            _testMachineRecipeConfig = new(_itemStackFactory);
        }

        [TestMethod]
        public void RecipeTimeTest()
        {
            RecipeTimeTest(0, 1500);
            RecipeTimeTest(1, 1500);
        }

        private void RecipeTimeTest(int id, int ans)
        {
            var time = _testMachineRecipeConfig.GetRecipeData(id).Time;
            Assert.AreEqual(ans, time);
        }

        /// <summary>
        /// レシピがある時のテスト
        /// </summary>
        [TestMethod]
        public void RecipeInputItemBlockIdTest()
        {
            
            RecipeInputItemBlockIdTest(1, new int[2] {1, 2}, 3, 1);
            RecipeInputItemBlockIdTest(1, new int[2] {2, 1}, 3, 1);
            RecipeInputItemBlockIdTest(3, new int[3] {1, 2, 3}, 5, 1);
            RecipeInputItemBlockIdTest(3, new int[3] {2, 1, 3}, 5, 1);
        }

        private void RecipeInputItemBlockIdTest(int BlocksId, int[] items, int output0Id, double output0Percent)
        {
            var input = new List<IItemStack>();
            items.ToList().ForEach(
                i => input.Add(_itemStackFactory.Create(i, 1)));

            var ans = _testMachineRecipeConfig.GetRecipeData(BlocksId, input);
            Assert.AreEqual(output0Id, ans.ItemOutputs[0].OutputItem.Id);
            Assert.AreEqual(output0Percent, ans.ItemOutputs[0].Percent);
        }

        [TestMethod]
        public void NullRecipeTest()
        {
            NullRecipeTest(3, new int[4] {2, 1, 0, 5}, 0); //nullの時のテスト
            NullRecipeTest(0, new int[3] {2, 1, 0}, 0);
            NullRecipeTest(3, new int[3] {4, 1, 0}, 0);
            NullRecipeTest(3, new int[2] {2, 1}, 0);
            NullRecipeTest(10, new int[1] {2}, 0);
            NullRecipeTest(3, new int[3] {2, 1, 0}, 0);
            NullRecipeTest(1, new int[2] {2, 1}, 1); //存在するときのテストケース
            NullRecipeTest(0, new int[0], 0);
        }

        private void NullRecipeTest(int BlocksId, int[] items, int outputLength)
        {
            var input = new List<IItemStack>();
            items.ToList().ForEach(
                i => input.Add(_itemStackFactory.Create(i, 1)));

            int ans = _testMachineRecipeConfig.GetRecipeData(BlocksId, input).ItemOutputs.Count;
            Assert.AreEqual(outputLength, ans);
        }

        [TestMethod]
        public void RecipeConfirmationTest()
        {
            RecipeConfirmationTest(1, new int[2] {1, 2}, new int[2] {3, 1}, true);
            RecipeConfirmationTest(1, new int[2] {2, 1}, new int[2] {1, 3}, true);
            RecipeConfirmationTest(1, new int[2] {2, 1}, new int[2] {1, 30}, true);
            RecipeConfirmationTest(1, new int[2] {2, 1}, new int[2] {1, 1}, false);
            RecipeConfirmationTest(3, new int[3] {1, 2, 3}, new int[3] {2, 3, 4}, true);
            RecipeConfirmationTest(3, new int[3] {1, 2, 3}, new int[3] {4, 6, 8}, true);
            RecipeConfirmationTest(3, new int[3] {1, 2, 3}, new int[3] {4, 6, 1}, false);
            RecipeConfirmationTest(3, new int[3] {2, 1, 3}, new int[3] {3, 2, 4}, true);
            RecipeConfirmationTest(3, new int[3] {2, 1, 3}, new int[3] {3, 1, 4}, false);
            RecipeConfirmationTest(3, new int[4] {2, 1, 0, 5}, new int[4] {3, 1, 4, 5}, false);
        }

        private void RecipeConfirmationTest(int BlocksId, int[] items, int[] itemcount, bool ans)
        {
            List<IItemStack> itemStacks = new List<IItemStack>();
            for (int i = 0; i < items.Length; i++)
            {
                itemStacks.Add(_itemStackFactory.Create(items[i], itemcount[i]));
            }

            var a = _testMachineRecipeConfig.GetRecipeData(BlocksId, itemStacks).RecipeConfirmation(itemStacks);
            Assert.AreEqual(ans, a);
        }
    }
}