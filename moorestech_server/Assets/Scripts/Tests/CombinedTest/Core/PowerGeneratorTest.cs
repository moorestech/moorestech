using System;
using System.Reflection;
using Core.Const;
using Core.Item;
using Core.Update;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class PowerGeneratorTest
    {
        private const int PowerGeneratorId = UnitTestModBlockId.GeneratorId;
        private const int FuelItem1Id = 1;
        private const int FuelItem2Id = 2;

        [Test]
        public void UseFuelTest()
        {
            var (_, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            GameUpdater.ResetUpdate();
            
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var powerGenerator = blockFactory.Create(PowerGeneratorId, 10) as VanillaPowerGeneratorBase;
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var generatorConfigParam = blockConfig.GetBlockConfig(PowerGeneratorId).Param as PowerGeneratorConfigParam;
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            var fuelItem1 = itemStackFactory.Create(generatorConfigParam.FuelSettings[FuelItem1Id].ItemId, 1);
            var fuelItem2 = itemStackFactory.Create(generatorConfigParam.FuelSettings[FuelItem2Id].ItemId, 1);


            //燃料の燃焼時間ループする
            var endTime1 = DateTime.Now.AddMilliseconds(generatorConfigParam.FuelSettings[FuelItem1Id].Time);

            //燃料を挿入
            powerGenerator.InsertItem(fuelItem1);

            //1回目のループ
            GameUpdater.UpdateWithWait();

            //供給電力の確認
            Assert.AreEqual(generatorConfigParam.FuelSettings[FuelItem1Id].Power, powerGenerator.OutputEnergy());

            //燃料の枯渇までループ
            while (endTime1.AddSeconds(0.1).CompareTo(DateTime.Now) == 1) GameUpdater.UpdateWithWait();

            //燃料が枯渇しているか確認
            //リフレクションで現在の燃料を取得
            var fuelItemId = (int)typeof(VanillaPowerGeneratorBase).GetField("_fuelItemId",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(powerGenerator);
            Assert.AreEqual(ItemConst.EmptyItemId, fuelItemId);

            //燃料を2個挿入
            powerGenerator.InsertItem(fuelItem1);
            powerGenerator.InsertItem(fuelItem2);

            //燃料の1個目の枯渇までループ
            endTime1 = DateTime.Now.AddMilliseconds(generatorConfigParam.FuelSettings[FuelItem1Id].Time);
            while (endTime1.AddSeconds(0.3).CompareTo(DateTime.Now) == 1) GameUpdater.UpdateWithWait();

            //2個の燃料が入っていることを確認
            fuelItemId = (int)typeof(VanillaPowerGeneratorBase).GetField("_fuelItemId",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(powerGenerator);
            Assert.AreEqual(generatorConfigParam.FuelSettings[FuelItem2Id].ItemId, fuelItemId);

            //燃料の2個目の枯渇までループ
            var endTime2 = DateTime.Now.AddMilliseconds(generatorConfigParam.FuelSettings[FuelItem2Id].Time);
            while (endTime2.AddSeconds(0.1).CompareTo(DateTime.Now) == 1) GameUpdater.UpdateWithWait();

            //2個目の燃料が枯渇しているか確認
            fuelItemId = (int)typeof(VanillaPowerGeneratorBase).GetField("_fuelItemId",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(powerGenerator);
            Assert.AreEqual(ItemConst.EmptyItemId, fuelItemId);
        }

        [Test]
        public void InfinityGeneratorTet()
        {
            var (_, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            GameUpdater.ResetUpdate();
            
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var powerGenerator =
                blockFactory.Create(UnitTestModBlockId.InfinityGeneratorId, 10) as VanillaPowerGeneratorBase;
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var generatorConfigParam =
                blockConfig.GetBlockConfig(UnitTestModBlockId.InfinityGeneratorId).Param as PowerGeneratorConfigParam;

            //1回目のループ
            GameUpdater.UpdateWithWait();

            //供給電力の確認
            Assert.AreEqual(generatorConfigParam.InfinityPower, powerGenerator.OutputEnergy());
        }
    }
}