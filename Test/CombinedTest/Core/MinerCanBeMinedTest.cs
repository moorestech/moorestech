using System.Reflection;
using Core.Block.BlockFactory;
using Core.Block.Blocks.Miner;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;
using Server.StartServerSystem;
using Test.Module.TestConfig;
using Test.Module.TestMod;

namespace Test.CombinedTest.Core
{
    public class MinerCanBeMinedTest
    {
        private int MinerId = 6;
        [Test]
        public void MinerTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var veinGenerator = serviceProvider.GetService<VeinGenerator>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            var worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();

            var oreId = 0;
            var x = 0;
            var y = 0;
            for (int i = 0; i < 500; i++)
            {
                for (int j = 0; j < 500; j++)
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
            
            worldBlockDatastore.AddBlock(blockFactory.Create(MinerId,1), x, y,BlockDirection.North);
            
            var miner = worldBlockDatastore.GetBlock(x, y) as VanillaMiner;
            //リフレクションでidを取得する
            var miningItemId = (int)miner.GetType().GetField("_miningItemId",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(miner);
            
            var defaultMiningTime = (int)miner.GetType().GetField("_defaultMiningTime",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(miner);
            
            
            Assert.AreEqual(3,miningItemId);
            Assert.AreEqual(1000,defaultMiningTime);
        }
    }
}