using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using Game.Paths;
using Game.PlayerIdentity;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Event.Notification;
using Tests.CombinedTest.Game;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using Tests.Util.PlayerIdentity;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.PowerGenerator
{
    public class ElectricGeneratorMissingFuelDiskSaveLoadTest
    {
        private string _root;
        [TearDown]
        public void Cleanup()
        {
            if (_root != null && Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DiskLoadRecoversOnlyActuallyMissingFuel(bool removeItem)
        {
            SaveLoadPreparerTestFixture.CreateContainer();
            _root = Path.Combine(Path.GetTempPath(), $"generator-recovery-{Guid.NewGuid()}");
            var mod = Path.Combine(_root, "mod");
            ElectricGeneratorRecoveryTestMod.Create(mod);
            var (_, originalProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(mod));
            var identities = originalProvider.GetRequiredService<PlayerIdentityRegistry>();
            var player = PlayerIdentityTestHelper.Register(identities, "steam:1").PlayerId;
            originalProvider.GetRequiredService<IPlayerInventoryDataStore>().GetInventoryData(player).MainOpenableInventory.SetItem(0, ForUnitTestItemId.ItemId2, 8);
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GeneratorId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(5, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest));
            chest.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.ItemId3, 4);
            var generator = block.GetComponent<VanillaElectricGeneratorComponent>();
            generator.SetItem(0, MasterHolder.ItemMaster.GetItemId(Guid.Parse(ElectricGeneratorRecoveryTestMod.FuelGuid)), 1);
            generator.SetItem(1, ForUnitTestItemId.ItemId2, 7);
            GameUpdater.UpdateOneTick();
            var burning = JObject.FromObject(generator.GetSaveState());
            Assert.AreEqual("Item", (string)burning["activeFuelType"]);
            Assert.Greater((double)burning["remainingFuelSeconds"], 0);
            var text = originalProvider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();
            var original = JObject.Parse(text);
            var directory = WorldDataDirectory.FromWorldRoot(Path.Combine(_root, "world"));
            Directory.CreateDirectory(Path.GetDirectoryName(directory.SaveJsonFilePath));
            File.WriteAllText(directory.SaveJsonFilePath, text);
            var bytes = File.ReadAllBytes(directory.SaveJsonFilePath);

            // 独立modで定義除去し実読込を通す
            // Remove definitions in the private mod and exercise the production preparer and disk loader
            ElectricGeneratorRecoveryTestMod.RemoveFuel(mod, removeItem);
            var options = new MoorestechServerDIContainerOptions(mod) { worldDataDirectory = WorldDataDirectory.FromServerDataMap(mod, directory.SaveJsonFilePath) };
            var (_, loaded) = new MoorestechServerDIContainerGenerator().Create(options);
            var prepared = loaded.GetRequiredService<SaveLoadPreparer>().Prepare(text);
            Assert.IsTrue(prepared.CanLoad);
            var state = prepared.Save["world"].Single(b => (int)b["instanceId"] == block.BlockInstanceId.AsPrimitive())["state"][generator.SaveKey];
            if (!removeItem)
            {
                // 許可変更後も既消費の燃料を復元
                // Restore already consumed fuel after ignition permissions change
                Assert.AreEqual("Item", (string)state["activeFuelType"]);
                LogAssert.Expect(LogType.Warning, new Regex("許可設定から外れた既消費燃料"));
                loaded.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
                generator = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero).GetComponent<VanillaElectricGeneratorComponent>();
                Assert.IsTrue(JToken.DeepEquals(burning, JObject.FromObject(generator.GetSaveState())));
                Assert.AreEqual(0, generator.OutputEnergy().AsPrimitive());
                AssertPreserved();
                return;
            }
            loaded.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            Assert.AreEqual("None", (string)state["activeFuelType"]);
            Assert.AreEqual(JTokenType.Null, state["currentFuelItemGuid"].Type);
            Assert.AreEqual(JTokenType.Null, state["currentFuelFluidGuid"].Type);
            Assert.AreEqual(0, (double)state["remainingFuelSeconds"]);
            generator = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero).GetComponent<VanillaElectricGeneratorComponent>();
            var power = MessagePackSerializer.Deserialize<PowerGeneratorStateDetail>(generator.GetBlockStateDetails().Single(d => d.Key == PowerGeneratorStateDetail.StateDetailKey).Value);
            Assert.AreEqual(0f, power.OperatingRate);
            Assert.AreEqual(0, power.RemainingFuelTime);
            Assert.AreEqual(0, generator.OutputEnergy().AsPrimitive());
            Assert.AreEqual(7, generator.GetItem(1).Count);
            Assert.AreEqual(ForUnitTestItemId.ItemId2, generator.GetItem(1).Id);
            Assert.AreEqual(2, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            Assert.IsNotNull(ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(5, 0, 0)));
            var roundTrip = JObject.Parse(loaded.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
            Assert.IsTrue(JToken.DeepEquals(original["players"], roundTrip["players"]));
            Assert.IsTrue(JToken.DeepEquals(original["playerInventory"], roundTrip["playerInventory"]));
            var chestId = chest.BlockInstanceId.AsPrimitive();
            Assert.AreEqual(original["world"].Single(b => (int)b["instanceId"] == chestId).ToString(),
                roundTrip["world"].Single(b => (int)b["instanceId"] == chestId).ToString());
            AssertPreserved();

            // 除去の住所と原値を退避し、接続プレイヤーへ既存診断を届ける
            // Archive the removed reference and its address, then deliver the existing diagnostic to the connecting player
            var archives = Directory.GetFiles(Path.Combine(_root, "world", "pruned"), "*.json");
            Assert.IsNotEmpty(archives);
            foreach (var archive in archives)
            {
                var entry = JObject.Parse(File.ReadAllText(archive))["items"].Single();
                Assert.AreEqual("bareGuid", (string)entry["kind"]);
                Assert.AreEqual(ElectricGeneratorRecoveryTestMod.FuelGuid, (string)entry["itemGuid"]);
                Assert.AreEqual(block.BlockInstanceId.AsPrimitive(), (int)entry["origin"]["blockInstanceId"]);
                Assert.AreEqual(generator.SaveKey, (string)entry["origin"]["stateKey"]);
                Assert.AreEqual("currentFuelItemGuid", (string)entry["origin"]["field"]);
            }
            var sink = new CapturedEventSink();
            loaded.GetRequiredService<EventProtocolProvider>().RegisterPlayer(player, sink);
            var notice = sink.Events.Where(e => e.Tag == NotificationService.EventTag)
                .Select(e => MessagePackSerializer.Deserialize<NotificationMessagePack>(e.Payload)).Single();
            Assert.AreEqual(NotificationCategory.SaveMigration, notice.Category);
            Assert.AreEqual("saveMigration.missingMasterPruned", notice.MessageId);
            CollectionAssert.AreEqual(new[] { "0", "1", "0" }, notice.MessageParams);

            #region Internal
            void AssertPreserved()
            {
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(directory.SaveJsonFilePath));
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(_root, "world", "backup", WorldSaveAllInfo.CurrentVersion.ToString(), "save.json")));
            }
            #endregion
        }
    }
}
