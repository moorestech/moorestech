using System;
using System.IO;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Core.Transport.Segment.BeltWorldTransportTest;

namespace Tests.UnitTest.Game.SaveLoad.BeltTransport
{
    public class BeltLegacyWorldLoadTest
    {
        [Test]
        public void VersionThreeWorldLoadsThroughMigrationAndPrunesMissingItemTest()
        {
            string directory = Path.Combine(Path.GetTempPath(), "belt-migration-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory) {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, path) };
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(options);
            services.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var belt = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 0, BlockDirection.North);
            var save = JObject.Parse(services.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
            save["worldVersion"] = 3;
            var guid = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).ItemGuid;
            var key = typeof(VanillaBeltConveyorComponent).FullName;
            save["world"][0]["state"][key] = new JArray(Legacy(guid, 0.25), Legacy(guid, 0.5), Legacy(Guid.Empty, 1));
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, save.ToString(Formatting.None));

            // 実ファイルを通常の起動経路でロードし、連鎖・除去・復元を通す。
            // Load a real file through normal startup, migration, pruning and restoration.
            var (_, loadedServices) = new MoorestechServerDIContainerGenerator().Create(options);
            loadedServices.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var transport = loadedServices.GetRequiredService<BeltWorldTransport>();
            transport.Initialize();
            var items = transport.Network.CaptureItems();
            Assert.AreEqual(1, items.Length);
            Assert.AreEqual(224, items[0].Progress);
            Assert.AreEqual(ForUnitTestItemId.ItemId1.AsPrimitive(), items[0].Item.ItemId);
            Assert.AreEqual(1, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            Assert.IsTrue(File.Exists(Path.Combine(directory, "backup", "3", "save.json")));
            UnityEngine.Debug.Log("LOAD OK | blocks=1 | beltItems=1 | progress=224 | V3 backup retained");

            #region Internal
            string Legacy(Guid itemGuid, double remaining) => new JObject {
                ["itemStack"] = new JObject { ["itemGuid"] = itemGuid.ToString(), ["count"] = 1 },
                ["remainingSeconds"] = remaining, ["sourceConnectorGuid"] = JValue.CreateNull(),
                ["goalConnectorGuid"] = JValue.CreateNull() }.ToString(Formatting.None);
            #endregion
        }
    }
}
