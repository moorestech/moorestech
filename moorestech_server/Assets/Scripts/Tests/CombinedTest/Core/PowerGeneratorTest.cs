using System;
using System.Reflection;
using Core.Const;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.BlockConfig;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class PowerGeneratorTest
    {
        private const int PowerGeneratorId = ForUnitTestModBlockId.GeneratorId;
        private const int FuelItem1Id = 1;
        private const int FuelItem2Id = 2;

        [Test]
        public void UseFuelTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            GameUpdater.ResetUpdate();

            var blockFactory = ServerContext.BlockFactory;
            var posInfo = new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one);
            var powerGenerator = blockFactory.Create(PowerGeneratorId, 10, posInfo);
            var generatorComponent = powerGenerator.ComponentManager.GetComponent<VanillaElectricGeneratorComponent>();
            var blockConfig = ServerContext.BlockConfig;
            var generatorConfigParam = blockConfig.GetBlockConfig(PowerGeneratorId).Param as PowerGeneratorConfigParam;
            var itemStackFactory = ServerContext.ItemStackFactory;

            var fuelItem1 = itemStackFactory.Create(generatorConfigParam.FuelSettings[FuelItem1Id].ItemId, 1);
            var fuelItem2 = itemStackFactory.Create(generatorConfigParam.FuelSettings[FuelItem2Id].ItemId, 1);


            //燃料の燃焼時間ループする
            var endTime1 = DateTime.Now.AddMilliseconds(generatorConfigParam.FuelSettings[FuelItem1Id].Time);

            //燃料を挿入
            generatorComponent.InsertItem(fuelItem1);

            //1回目のループ
            GameUpdater.UpdateWithWait();

            //供給電力の確認
            Assert.AreEqual(generatorConfigParam.FuelSettings[FuelItem1Id].Power, generatorComponent.OutputEnergy());

            //燃料の枯渇までループ
            while (endTime1.AddSeconds(0.1).CompareTo(DateTime.Now) == 1) 
                GameUpdater.UpdateWithWait();

            //燃料が枯渇しているか確認
            //リフレクションで現在の燃料を取得
            var fuelItemId = (int)typeof(VanillaElectricGeneratorComponent).GetField("_fuelItemId",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(generatorComponent);
            Assert.AreEqual(ItemConst.EmptyItemId, fuelItemId);

            //燃料を2個挿入
            generatorComponent.InsertItem(fuelItem1);
            generatorComponent.InsertItem(fuelItem2);

            //燃料の1個目の枯渇までループ
            endTime1 = DateTime.Now.AddMilliseconds(generatorConfigParam.FuelSettings[FuelItem1Id].Time);
            while (endTime1.AddSeconds(0.3).CompareTo(DateTime.Now) == 1) GameUpdater.UpdateWithWait();

            //2個の燃料が入っていることを確認
            fuelItemId = (int)typeof(VanillaElectricGeneratorComponent).GetField("_fuelItemId",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(generatorComponent);
            Assert.AreEqual(generatorConfigParam.FuelSettings[FuelItem2Id].ItemId, fuelItemId);

            //燃料の2個目の枯渇までループ
            var endTime2 = DateTime.Now.AddMilliseconds(generatorConfigParam.FuelSettings[FuelItem2Id].Time);
            while (endTime2.AddSeconds(0.1).CompareTo(DateTime.Now) == 1) GameUpdater.UpdateWithWait();

            //2個目の燃料が枯渇しているか確認
            fuelItemId = (int)typeof(VanillaElectricGeneratorComponent).GetField("_fuelItemId",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(generatorComponent);
            Assert.AreEqual(ItemConst.EmptyItemId, fuelItemId);
        }

        [Test]
        public void InfinityGeneratorTet()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            GameUpdater.ResetUpdate();

            var blockFactory = ServerContext.BlockFactory;
            var posInfo = new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one);
            var powerGenerator = blockFactory.Create(ForUnitTestModBlockId.InfinityGeneratorId, 10, posInfo);
            var generatorComponent = powerGenerator.ComponentManager.GetComponent<VanillaElectricGeneratorComponent>();
            
            var blockConfig = ServerContext.BlockConfig;
            var generatorConfigParam = blockConfig.GetBlockConfig(ForUnitTestModBlockId.InfinityGeneratorId).Param as PowerGeneratorConfigParam;

            //1回目のループ
            GameUpdater.UpdateWithWait();

            //供給電力の確認
            Assert.AreEqual(generatorConfigParam.InfinityPower, generatorComponent.OutputEnergy());
        }
    }
}