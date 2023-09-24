using System.Collections.Generic;
using System.Linq;
using Core.Block.RecipeConfig;
using Core.ConfigJson;
using Core.Item;
using Core.Item.Config;
using Core.Item.Implementation;
using Core.Item.Util;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;


using Test.Module.TestMod;

namespace Test.UnitTest.Core.Block
{
    public class MachineRecipeConfigTest
    {
        private IMachineRecipeConfig _machineRecipeConfig;
        private ItemStackFactory _itemStackFactory;

        [SetUp]
        public void Setup()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            _machineRecipeConfig = serviceProvider.GetService<IMachineRecipeConfig>();
        }

        /// <summary>
        /// レシピがある時のテスト
        /// </summary>
        [TestCase(1, new int[2] {1, 2}, 3, 1)]
        [TestCase(1, new int[2] {2, 1}, 3, 1)]
        [TestCase(3, new int[3] {1, 2, 3}, 5, 1)]
        [TestCase(3, new int[3] {2, 1, 3}, 5, 1)]
        public void RecipeInputItemBlockIdTest(int BlocksId, int[] items, int output0Id, double output0Percent)
        {
            var input = new List<IItemStack>();
            items.ToList().ForEach(
                i => input.Add(_itemStackFactory.Create(i, 1)));

            var ans = _machineRecipeConfig.GetRecipeData(BlocksId, input);
            Assert.AreEqual(output0Id, ans.ItemOutputs[0].OutputItem.Id);
            Assert.AreEqual(output0Percent, ans.ItemOutputs[0].Percent);
        }

        [TestCase(3, new int[4] {2, 1, 0, 5}, 0)] //nullの時のテスト
        [TestCase(0, new int[3] {2, 1, 0}, 0)]
        [TestCase(3, new int[3] {4, 1, 0}, 0)]
        [TestCase(3, new int[2] {2, 1}, 0)]
        [TestCase(10, new int[1] {2}, 0)]
        [TestCase(3, new int[3] {2, 1, 0}, 0)]
        [TestCase(1, new int[2] {2, 1}, 1)] //存在するときのテストケース
        [TestCase(0, new int[0], 0)]
        public void NullRecipeTest(int BlocksId, int[] items, int outputLength)
        {
            var input = new List<IItemStack>();
            items.ToList().ForEach(
                i => input.Add(_itemStackFactory.Create(i, 1)));

            int ans = _machineRecipeConfig.GetRecipeData(BlocksId, input).ItemOutputs.Count;
            Assert.AreEqual(outputLength, ans);
        }

        [TestCase(1, new int[2] {1, 2}, new int[2] {3, 1}, true)]
        [TestCase(1, new int[2] {2, 1}, new int[2] {1, 3}, true)]
        [TestCase(1, new int[2] {2, 1}, new int[2] {1, 30}, true)]
        [TestCase(1, new int[2] {2, 1}, new int[2] {1, 1}, false)]
        [TestCase(3, new int[3] {1, 2, 3}, new int[3] {2, 3, 4}, true)]
        [TestCase(3, new int[3] {1, 2, 3}, new int[3] {4, 6, 8}, true)]
        [TestCase(3, new int[3] {1, 2, 3}, new int[3] {4, 6, 1}, false)]
        [TestCase(3, new int[3] {2, 1, 3}, new int[3] {3, 2, 4}, true)]
        [TestCase(3, new int[3] {2, 1, 3}, new int[3] {3, 1, 4}, false)]
        [TestCase(3, new int[4] {2, 1, 0, 5}, new int[4] {3, 1, 4, 5}, false)]
        public void RecipeConfirmationTest(int blocksId, int[] items, int[] itemcount, bool ans)
        {
            List<IItemStack> itemStacks = new List<IItemStack>();
            for (int i = 0; i < items.Length; i++)
            {
                itemStacks.Add(_itemStackFactory.Create(items[i], itemcount[i]));
            }

            var a = _machineRecipeConfig.GetRecipeData(blocksId, itemStacks).RecipeConfirmation(itemStacks, blocksId);
            Assert.AreEqual(ans, a);
        }
    }
}