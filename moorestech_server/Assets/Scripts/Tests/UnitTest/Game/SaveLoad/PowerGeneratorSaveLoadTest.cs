using System.Reflection;
using Core.Inventory;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class PowerGeneratorSaveLoadTest
    {
        private const int PowerGeneratorId = ForUnitTestModBlockId.GeneratorId;
        
        [Test]
        public void PowerGeneratorTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var blockFactory = ServerContext.BlockFactory;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var fuelSlotCount = (ServerContext.BlockConfig.GetBlockConfig(PowerGeneratorId).Param as PowerGeneratorConfigParam).FuelSlot;
            var generatorPosInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var powerGeneratorBlock = blockFactory.Create(PowerGeneratorId, new BlockInstanceId(10), generatorPosInfo);
            var powerGenerator = powerGeneratorBlock.GetComponent<VanillaElectricGeneratorComponent>();
            
            const int fuelItemId = 5;
            const int remainingFuelTime = 567;
            
            //検証元の発電機を作成
            var type = typeof(VanillaElectricGeneratorComponent);
            type.GetField("_currentFuelItemId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(powerGenerator, fuelItemId);
            type.GetField("_remainingFuelTime", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(powerGenerator, remainingFuelTime);
            var fuelItemStacks = (OpenableInventoryItemDataStoreService)type
                .GetField("_itemDataStoreService", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(powerGenerator);
            fuelItemStacks.SetItem(0, itemStackFactory.Create(1, 5));
            fuelItemStacks.SetItem(2, itemStackFactory.Create(3, 5));
            
            
            //セーブのテキストを取得
            var saveText = powerGenerator.GetSaveState();
            Debug.Log(saveText);
            
            
            var blockHash = ServerContext.BlockConfig.GetBlockConfig(PowerGeneratorId).BlockHash;
            //発電機を再作成
            var loadedPowerGeneratorBlock = blockFactory.Load(blockHash, new BlockInstanceId(10), saveText, generatorPosInfo);
            var loadedPowerGenerator = loadedPowerGeneratorBlock.GetComponent<VanillaElectricGeneratorComponent>();
            //発電機を再作成した結果を検証
            var loadedFuelItemId = (int)type.GetField("_currentFuelItemId", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadedPowerGenerator);
            Assert.AreEqual(fuelItemId, loadedFuelItemId);
            
            var loadedRemainingFuelTime = (double)type
                .GetField("_remainingFuelTime", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadedPowerGenerator);
            Assert.AreEqual(remainingFuelTime, loadedRemainingFuelTime);
            
            var loadedFuelItemStacks = (OpenableInventoryItemDataStoreService)type
                .GetField("_itemDataStoreService", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadedPowerGenerator);
            
            //燃料スロットの検証
            Assert.AreEqual(fuelItemStacks.GetSlotSize(), loadedFuelItemStacks.GetSlotSize());
            for (var i = 0; i < fuelSlotCount; i++)
                Assert.AreEqual(fuelItemStacks.InventoryItems[i], loadedFuelItemStacks.InventoryItems[i]);
        }
    }
}