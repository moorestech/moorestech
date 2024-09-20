using System;
using System.Reflection;
using Core.Const;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class PowerGeneratorTest
    {
        private const int FuelItem1Id = 0;
        private const int FuelItem2Id = 1;
        
        [Test]
        public void UseFuelTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var blockFactory = ServerContext.BlockFactory;
            var posInfo = new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one);
            var powerGenerator = blockFactory.Create(ForUnitTestModBlockId.GeneratorId, new BlockInstanceId(10), posInfo);
            var generatorComponent = powerGenerator.GetComponent<VanillaElectricGeneratorComponent>();
            var generatorConfigParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GeneratorId).BlockParam as ElectricGeneratorBlockParam;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var fuelItem1 = itemStackFactory.Create(generatorConfigParam.FuelItems[FuelItem1Id].ItemGuid, 1);
            var fuelItem2 = itemStackFactory.Create(generatorConfigParam.FuelItems[FuelItem2Id].ItemGuid, 1);
            
            
            //燃料の燃焼時間ループする
            var endTime1 = DateTime.Now.AddSeconds(generatorConfigParam.FuelItems[FuelItem1Id].Time);
            
            //燃料を挿入
            generatorComponent.InsertItem(fuelItem1);
            
            //1回目のループ
            GameUpdater.UpdateWithWait();
            
            //供給電力の確認
            Assert.AreEqual(generatorConfigParam.FuelItems[FuelItem1Id].Power, generatorComponent.OutputEnergy().AsPrimitive());
            
            //燃料の枯渇までループ
            while (endTime1.AddSeconds(0.1).CompareTo(DateTime.Now) == 1)
            {
                GameUpdater.UpdateWithWait();
            }
            
            //燃料が枯渇しているか確認
            //リフレクションで現在の燃料を取得
            var fuelItemId = (ItemId)typeof(VanillaElectricGeneratorComponent).GetField("_currentFuelItemId",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(generatorComponent);
            Assert.AreEqual(ItemConst.EmptyItemId, fuelItemId);
            
            //燃料を2個挿入
            generatorComponent.InsertItem(fuelItem1);
            generatorComponent.InsertItem(fuelItem2);
            
            //燃料の1個目の枯渇までループ
            endTime1 = DateTime.Now.AddSeconds(generatorConfigParam.FuelItems[FuelItem1Id].Time);
            while (endTime1.AddSeconds(0.3).CompareTo(DateTime.Now) == 1) GameUpdater.UpdateWithWait();
            
            //2個の燃料が入っていることを確認
            fuelItemId = (ItemId)typeof(VanillaElectricGeneratorComponent).GetField("_currentFuelItemId",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(generatorComponent);
            var fuelItemId2 = MasterHolder.ItemMaster.GetItemId(generatorConfigParam.FuelItems[FuelItem2Id].ItemGuid);
            Assert.AreEqual(fuelItemId2, fuelItemId);
            
            //燃料の2個目の枯渇までループ
            var endTime2 = DateTime.Now.AddSeconds(generatorConfigParam.FuelItems[FuelItem2Id].Time);
            while (endTime2.AddSeconds(0.1).CompareTo(DateTime.Now) == 1) GameUpdater.UpdateWithWait();
            
            //2個目の燃料が枯渇しているか確認
            fuelItemId = (ItemId)typeof(VanillaElectricGeneratorComponent).GetField("_currentFuelItemId",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(generatorComponent);
            Assert.AreEqual(ItemConst.EmptyItemId, fuelItemId);
        }
        
        [Test]
        public void InfinityGeneratorTet()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var blockFactory = ServerContext.BlockFactory;
            var posInfo = new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one);
            var powerGenerator = blockFactory.Create(ForUnitTestModBlockId.InfinityGeneratorId, new BlockInstanceId(10), posInfo);
            var generatorComponent = powerGenerator.GetComponent<VanillaElectricGeneratorComponent>();
            
            var generatorConfigParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.InfinityGeneratorId).BlockParam as ElectricGeneratorBlockParam;
            
            //1回目のループ
            GameUpdater.UpdateWithWait();
            
            //供給電力の確認
            Assert.AreEqual(generatorConfigParam.InfinityPower, generatorComponent.OutputEnergy().AsPrimitive());
        }
    }
}