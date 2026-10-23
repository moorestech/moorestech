using System;
using System.IO;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Fluid;
using Game.Paths;
using Game.PlayerIdentity;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.PlayerIdentity;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad.PowerGenerator
{
    public class ElectricGeneratorShortenedFuelDiskSaveLoadTest
    {
        private string _root;
        [TearDown]
        public void Cleanup()
        {
            if (_root != null && Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DiskLoadKeepsConsumedFuelLifetimeAfterDurationShortens(bool liquid)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _root = Path.Combine(Path.GetTempPath(), $"generator-duration-{Guid.NewGuid()}");
            var mod = Path.Combine(_root, "mod");
            ElectricGeneratorRecoveryTestMod.Create(mod);
            SetDuration(10);
            var (_, originalProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(mod));
            var player = PlayerIdentityTestHelper.Register(originalProvider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            originalProvider.GetRequiredService<IPlayerInventoryDataStore>().GetInventoryData(player).MainOpenableInventory.SetItem(0, ForUnitTestItemId.ItemId2, 8);
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GeneratorId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(5, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest));
            chest.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.ItemId3, 4);
            var generator = block.GetComponent<VanillaElectricGeneratorComponent>();
            var param = (ElectricGeneratorBlockParam)block.BlockMasterElement.BlockParam;
            if (liquid)
            {
                var fuel = param.FuelFluids[0];
                generator.AddLiquid(new FluidStack(fuel.Amount * 3, MasterHolder.FluidMaster.GetFluidId(fuel.FluidGuid)), default);
            }
            else generator.SetItem(0, MasterHolder.ItemMaster.GetItemId(Guid.Parse(ElectricGeneratorRecoveryTestMod.FuelGuid)), 1);
            generator.SetItem(1, ForUnitTestItemId.TestElevatorReward, 7);
            GameUpdater.UpdateOneTick();
            GameUpdater.RunFrames(GameUpdater.SecondsToTicks(2));
            var state = JObject.FromObject(generator.GetSaveState());
            Assert.AreEqual(liquid ? "Fluid" : "Item", (string)state["activeFuelType"]);
            Assert.AreEqual(8, (double)state["remainingFuelSeconds"], 0.001);
            var text = originalProvider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();
            var original = JObject.Parse(text);
            var directory = WorldDataDirectory.FromWorldRoot(Path.Combine(_root, "world"));
            Directory.CreateDirectory(Path.GetDirectoryName(directory.SaveJsonFilePath));
            File.WriteAllText(directory.SaveJsonFilePath, text);
            var bytes = File.ReadAllBytes(directory.SaveJsonFilePath);

            // 新DIの構築前にmodを変更
            // Modify the private mod before constructing the fresh DI and its caches
            SetDuration(5);
            var options = new MoorestechServerDIContainerOptions(mod) { worldDataDirectory = WorldDataDirectory.FromServerDataMap(mod, directory.SaveJsonFilePath) };
            var (_, loaded) = new MoorestechServerDIContainerGenerator().Create(options);
            loaded.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            generator = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero).GetComponent<VanillaElectricGeneratorComponent>();
            var restored = JObject.FromObject(generator.GetSaveState());
            Assert.IsTrue(JToken.DeepEquals(state, restored));
            var detail = MessagePackSerializer.Deserialize<PowerGeneratorStateDetail>(generator.GetBlockStateDetails().Single(d => d.Key == PowerGeneratorStateDetail.StateDetailKey).Value);
            Assert.AreEqual(8, detail.RemainingFuelTime, 0.001);
            Assert.AreEqual(5, detail.CurrentFuelTime, 0.001);
            Assert.AreEqual(1, detail.OperatingRate);
            var roundTrip = JObject.Parse(loaded.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
            Assert.IsTrue(JToken.DeepEquals(original["players"], roundTrip["players"]));
            Assert.IsTrue(JToken.DeepEquals(original["playerInventory"], roundTrip["playerInventory"]));
            Assert.AreEqual(2, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            Assert.IsTrue(JToken.DeepEquals(original["world"].Single(b => (int)b["instanceId"] == chest.BlockInstanceId.AsPrimitive()),
                roundTrip["world"].Single(b => (int)b["instanceId"] == chest.BlockInstanceId.AsPrimitive())));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(directory.SaveJsonFilePath));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(_root, "world", "backup", WorldSaveAllInfo.CurrentVersion.ToString(), "save.json")));
            Debug.Log($"W7 fuel={detail.RemainingFuelTime}/{detail.CurrentFuelTime} liquid={liquid} sourceAndBackup=preserved");

            // 次の点火だけが新しい5秒設定を使う
            // Only the next ignition uses the new five-second duration
            if (!liquid) generator.SetItem(0, MasterHolder.ItemMaster.GetItemId(Guid.Parse(ElectricGeneratorRecoveryTestMod.FuelGuid)), 1);
            GameUpdater.RunFrames(GameUpdater.SecondsToTicks(8));
            Assert.AreEqual(5, (double)JObject.FromObject(generator.GetSaveState())["remainingFuelSeconds"], 0.001);

            #region Internal
            void SetDuration(int seconds)
            {
                var path = Path.Combine(mod, "mods", "forUnitTest", "master", "blocks.json");
                var json = JObject.Parse(File.ReadAllText(path));
                var entry = json["data"].Single(b => (string)b["blockGuid"] == MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GeneratorId).BlockGuid.ToString());
                foreach (var fuel in entry["blockParam"]["fuelItems"].Concat(entry["blockParam"]["fuelFluids"])) fuel["time"] = seconds;
                File.WriteAllText(path, json.ToString());
            }
            #endregion
        }
    }
}
