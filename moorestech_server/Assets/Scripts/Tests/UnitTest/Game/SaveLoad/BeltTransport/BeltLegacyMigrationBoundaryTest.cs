using System;
using System.IO;
using System.Text.RegularExpressions;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Core.Transport.Segment.BeltWorldTransportTest;

namespace Tests.UnitTest.Game.SaveLoad.BeltTransport
{
    public class BeltLegacyMigrationBoundaryTest
    {
        [Test]
        public void FourSlotDiskLoadRestoresExitNearestHeadAndLogsCollisionsTest()
        {
            var fixture = CreateFixture(1, 4, false, false);
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(fixture.Options);
            services.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var belt = ServerContext.WorldBlockDatastore.GetBlock(fixture.BlockId).GetComponent<VanillaBeltConveyorComponent>();
            Assert.AreEqual(4, belt.BeltConveyorItems.Count);
            var mapped = belt.CaptureItems();
            var mappedProgress = Array.ConvertAll(mapped, item => item.Progress);
            CollectionAssert.AreEqual(new[] { 256, 192, 128, 64 }, mappedProgress);
            var candidates = new Guid[4];
            for (int slot = 0; slot < 4; slot++)
            {
                long id = ((long)fixture.BlockId.AsPrimitive() << 32) | (uint)(slot + 1);
                candidates[slot] = BeltTransportIdentity.ToGuid(new ItemInstanceId(id));
                Assert.AreEqual(candidates[slot], mapped[slot].Item.Guid);
                if (slot != 0) LogAssert.Expect(LogType.Warning, new Regex($"Belt item removed: cell={fixture.BlockId.AsPrimitive()}, item={candidates[slot]}, reason=The item overlaps a higher-priority restored item."));
            }
            // 初回snapshot生成で復元を確定し、物理tickを進めずに記録する。
            // Commit restoration through the initial snapshot without advancing a physical tick.
            var snapshot = services.GetRequiredService<BeltWorldTransport>().CaptureCommittedSnapshot();
            string backup = Path.Combine(Path.GetDirectoryName(fixture.Path), "backup", "3", "save.json");
            Assert.AreEqual(0ul, GameUpdater.CurrentTick);
            Assert.AreEqual(0ul, snapshot.Tick);
            Assert.AreEqual(fixture.Original, File.ReadAllText(backup));
            Assert.AreEqual(fixture.Original, File.ReadAllText(fixture.Path));
            Debug.Log("V3_FOUR_SLOT_OBSERVATION " + JsonConvert.SerializeObject(new {
                blockId = fixture.BlockId.AsPrimitive(), remainingSeconds = new[] { 0d, 0.5d, 1d, 1.5d },
                candidateGuids = candidates, mappedProgress, pendingCount = mapped.Length, survivorCount = snapshot.Snapshot.Items.Length,
                survivors = snapshot.Snapshot.Items, tick = snapshot.Tick, backup, original = fixture.Path }));
            Assert.AreEqual(1, snapshot.Snapshot.Items.Length);
            Assert.AreEqual(candidates[0], snapshot.Snapshot.Items[0].Item.Guid);
            Assert.AreEqual(256, snapshot.Snapshot.Items[0].Progress);
        }

        [TestCase(false, 2, false)]
        [TestCase(true, 1, true)]
        [TestCase(true, 2, false)]
        public void InvalidLegacyShapeIsBlockedBeforeRuntimeTest(bool wrapped, int count, bool invalidGuid)
        {
            var fixture = CreateFixture(count, 1, wrapped, invalidGuid);
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(fixture.Options);
            LogAssert.Expect(LogType.Error, new Regex("V3からV4へ変換できませんでした"));
            LogAssert.Expect(LogType.Error, new Regex("cause=StepFailed"));
            LogAssert.Expect(LogType.Error, new Regex("セーブファイルパス"));
            var exception = Assert.Throws<Exception>(() => services.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize());
            Debug.Log($"V3_INVALID_SHAPE wrapped={wrapped}, count={count}, invalidGuid={invalidGuid}: {exception.Message}");
            Assert.That(exception.Message, Does.Contain("StepFailed"));
            Assert.That(exception.Message, Does.Contain(invalidGuid ? "sourceConnectorGuid" : "count"));
            Assert.AreEqual(0, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            Assert.AreEqual(fixture.Original, File.ReadAllText(fixture.Path));
        }

        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        public void ValidRawAndWrappedLegacyPayloadsLoadTest(bool wrapped, int count)
        {
            var fixture = CreateFixture(count, 1, wrapped, false);
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(fixture.Options);
            services.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var snapshot = services.GetRequiredService<BeltWorldTransport>().CaptureCommittedSnapshot();
            Assert.AreEqual(count, snapshot.Snapshot.Items.Length);
            Assert.AreEqual(0ul, snapshot.Tick);
            Assert.AreEqual(fixture.Original, File.ReadAllText(Path.Combine(Path.GetDirectoryName(fixture.Path), "backup", "3", "save.json")));
        }

        private static (MoorestechServerDIContainerOptions Options, string Path, string Original, BlockInstanceId BlockId) CreateFixture(int count, int slots, bool wrapped, bool invalidGuid)
        {
            string directory = Path.Combine(Path.GetTempPath(), "belt-legacy-boundary-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory) {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, path) };
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(options);
            services.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var block = Place(ForUnitTestModBlockId.BeltConveyorId, 0, 0, BlockDirection.North);
            var param = (BeltConveyorBlockParam)block.BlockMasterElement.BlockParam;
            Assert.AreEqual(4, param.BeltConveyorItemCount);
            Assert.AreEqual(2d, param.TimeOfItemEnterToExit);
            var save = JObject.Parse(services.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
            var items = new JArray();
            // 旧serializerと同じJSON文字列配列に実masterのGUIDを載せる。
            // Use real master GUIDs in the JSON-string array produced by the old serializer.
            for (int slot = 0; slot < slots; slot++)
                items.Add(new JObject {
                    ["itemStack"] = new JObject { ["itemGuid"] = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).ItemGuid.ToString(), ["count"] = count },
                    ["remainingSeconds"] = param.TimeOfItemEnterToExit * slot / 4,
                    ["sourceConnectorGuid"] = invalidGuid ? "not-a-guid" : param.InventoryConnectors.InputConnects[0].ConnectorGuid.ToString(),
                    ["goalConnectorGuid"] = param.InventoryConnectors.OutputConnects[0].ConnectorGuid.ToString() }.ToString(Formatting.None));
            save["worldVersion"] = 3;
            save["world"][0]["state"][typeof(VanillaBeltConveyorComponent).FullName] = wrapped ? new JObject { ["legacyItems"] = items } : items;
            string original = save.ToString(Formatting.None);
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, original);
            return (options, path, original, block.BlockInstanceId);
        }
    }
}
