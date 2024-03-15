using System.Collections.Generic;
using System.Reflection;
using Core.Item;
using Game.Block.Blocks.Miner;
using Game.Block.Interface;
using Game.Map.Interface.Vein;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

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
            var mapVeinDatastore = serviceProvider.GetService<IMapVeinDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();

            //採掘機を設置する位置を取得
            var (mapVein, pos) = GetMapVein();

            worldBlockDatastore.AddBlock(blockFactory.Create(MinerId, 1), pos, BlockDirection.North);

            var miner = worldBlockDatastore.GetBlock(pos) as VanillaMinerBase;
            //リフレクションでidを取得する
            var miningItems = (List<IItemStack>)typeof(VanillaMinerBase)
                .GetField("_miningItems", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(miner);

            var defaultMiningTime = (int)typeof(VanillaMinerBase)
                .GetField("_defaultMiningTime", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(miner);


            Assert.AreEqual(mapVein.VeinItemId, miningItems[0].Id);
            Assert.AreEqual(1000, defaultMiningTime);

            #region Internal

            (IMapVein mapVein,Vector2Int pos) GetMapVein()
            {
                var pos = new Vector2Int(0, 0);
                for (var i = 0; i < 500; i++)
                {
                    for (var j = 0; j < 500; j++)
                    {
                        var veins = mapVeinDatastore.GetOverVeins(new Vector2Int(i, j));
                        if (veins.Count == 0) continue;
                    
                         return (veins[0], new Vector2Int(i, j));
                    }
                }
                return (null, pos);
            }

            #endregion
        }
    }
}