using System.Reflection;
using Core.Inventory;
using Core.Item;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class PowerGeneratorSaveLoadTest
    {
        private const int PowerGeneratorId = UnitTestModBlockId.GeneratorId;

        [Test]
        public void PowerGeneratorTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var fuelSlotCount =
                (serviceProvider.GetService<IBlockConfig>().GetBlockConfig(PowerGeneratorId).Param as
                    PowerGeneratorConfigParam).FuelSlot;
            var powerGenerator = (VanillaPowerGeneratorBase)blockFactory.Create(PowerGeneratorId, 10);

            const int fuelItemId = 5;
            const int remainingFuelTime = 567;

            //検証元の発電機を作成
            var type = typeof(VanillaPowerGeneratorBase);
            type.GetField("_fuelItemId", BindingFlags.NonPublic | BindingFlags.Instance)
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


            var blockHash = serviceProvider.GetService<IBlockConfig>().GetBlockConfig(PowerGeneratorId).BlockHash;
            //発電機を再作成
            var loadedPowerGenerator = (VanillaPowerGeneratorBase)blockFactory.Load(blockHash, 10, saveText);
            //発電機を再作成した結果を検証
            var loadedFuelItemId = (int)type.GetField("_fuelItemId", BindingFlags.NonPublic | BindingFlags.Instance)
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
                Assert.AreEqual(fuelItemStacks.Inventory[i], loadedFuelItemStacks.Inventory[i]);
        }
    }
}