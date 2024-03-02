using System.Collections.Generic;
using System.Reflection;
using Core.Item;
using Game.Block.Blocks.Miner;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class MinerCanBeMinedTest
    {
        private const int MinerId = UnitTestModBlockId.MinerId;

        [Test]
        public void MinerTest()
        {
            var (_, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var veinGenerator = serviceProvider.GetService<VeinGenerator>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();

            var oreId = 0;
            var x = 0;
            var y = 0;
            for (var i = 0; i < 500; i++)
            {
                for (var j = 0; j < 500; j++)
                {
                    oreId = veinGenerator.GetOreId(i, j);
                    //oreIdが1の時にその上に採掘機を設置する
                    if (oreId != 1) continue;
                    x = i;
                    y = j;
                    break;
                }

                if (oreId != 1) continue;
                break;
            }

            worldBlockDatastore.AddBlock(blockFactory.Create(MinerId, 1), x, y, BlockDirection.North);

            var miner = worldBlockDatastore.GetBlock(x, y) as VanillaMinerBase;
            //リフレクションでidを取得する
            var miningItems = (List<IItemStack>)typeof(VanillaMinerBase)
                .GetField("_miningItems", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(miner);

            var defaultMiningTime = (int)typeof(VanillaMinerBase)
                .GetField("_defaultMiningTime", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(miner);


            Assert.AreEqual(3, miningItems[0].Id);
            Assert.AreEqual(1000, defaultMiningTime);
        }
    }
}