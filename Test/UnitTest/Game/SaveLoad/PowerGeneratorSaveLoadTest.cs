using System.Collections.Generic;
using System.Reflection;
using Core.Block.BlockFactory;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Block.PowerGenerator;
using Core.Item;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;

namespace Test.UnitTest.Game.SaveLoad
{
    public class PowerGeneratorSaveLoadTest
    {
        private const int PowerGeneratorId = 5;
        
        [Test]
        public void PowerGeneratorTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var fuelSlotCount =
                (serviceProvider.GetService<IBlockConfig>().GetBlockConfig(PowerGeneratorId).Param as
                    PowerGeneratorConfigParam).FuelSlot;
            var powerGenerator = (VanillaPowerGenerator) blockFactory.Create(PowerGeneratorId, 10);

            const int fuelItemId = 5;
            const int remainingFuelTime = 567;
            
            //検証元の発電機を作成
            var type = powerGenerator.GetType();
            type.GetField("_fuelItemId",BindingFlags.NonPublic | BindingFlags.Instance).SetValue(powerGenerator,fuelItemId);
            type.GetField("_remainingFuelTime",BindingFlags.NonPublic | BindingFlags.Instance).SetValue(powerGenerator,remainingFuelTime);
            var fuelItemStacks = (List<IItemStack>)type.GetField("_fuelItemStacks", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(powerGenerator);
            fuelItemStacks[0] = itemStackFactory.Create(1, 5);
            fuelItemStacks[2] = itemStackFactory.Create(3, 5);

            //セーブのテキストを取得
            var saveText = powerGenerator.GetSaveState();
            
            //発電機を再作成
            var loadedPowerGenerator = (VanillaPowerGenerator) blockFactory.Create(PowerGeneratorId, 10,saveText);
            //発電機を再作成した結果を検証
            var loadedFuelItemId = (int)type.GetField("_fuelItemId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(loadedPowerGenerator);
            Assert.AreEqual(fuelItemId,loadedFuelItemId);
            var loadedRemainingFuelTime = (int)type.GetField("_remainingFuelTime", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(loadedPowerGenerator);
            Assert.AreEqual(remainingFuelTime,loadedRemainingFuelTime);
            var loadedFuelItemStacks = (List<IItemStack>)type.GetField("_fuelItemStacks", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadedPowerGenerator);

            //燃料スロットの検証
            Assert.AreEqual(fuelItemStacks.Count,loadedFuelItemStacks.Count);
            for (int i = 0; i < fuelSlotCount; i++)
            {
                Assert.AreEqual(fuelItemStacks[i],loadedFuelItemStacks[i]);
            }
        }
    }
}