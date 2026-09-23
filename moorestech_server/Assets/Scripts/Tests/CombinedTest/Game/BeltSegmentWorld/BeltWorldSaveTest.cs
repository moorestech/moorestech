using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.BeltSegment;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Migration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
namespace Tests.CombinedTest.Game.BeltSegmentWorld
{
    public class BeltWorldSaveTest
    {
        [Test]
        public void NewFormatLoadsThroughProductionDiskPathWithSameLoopAndTransportIdentity()
        {
            var f = new BeltWorldFixture(); var belts = f.Loop(true); f.Seed(belts[1], 1); f.Tick(3);
            var expected = f.Snapshot(); string save = f.Save();
            string root = Path.Combine(Path.GetTempPath(), "belt-v3-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); string file = Path.Combine(root, "save.json"); File.WriteAllText(file, save);
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            { worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, file) };
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(options);
            services.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var world = services.GetRequiredService<BeltWorldDatastore>(); world.Load(); var restored = world.CaptureSnapshot();
            // セル保存は旧segment入口を局所入口へ正規化するため、輸送identityと進行位置を比較する。
            // Cell saves normalize the old segment entry to the local entry; compare transport identity and progress.
            CollectionAssert.AreEqual(expected.Simulation.Segments.SelectMany(s => s.Items).Select(i => (i.Item.Guid, i.Item.ItemId, i.DistanceToExit)),
                restored.Simulation.Segments.SelectMany(s => s.Items).Select(i => (i.Item.Guid, i.Item.ItemId, i.DistanceToExit)));
            Assert.AreEqual(expected.Routes[0].Cells[0].Cell, restored.Routes[0].Cells[0].Cell);
            Assert.AreEqual(expected.Position.Tick, restored.Position.Tick); Assert.AreEqual(0, restored.Position.Sequence);
            Assert.AreEqual(save, File.ReadAllText(file)); Directory.Delete(root, true);
        }
        [TestCase(1)] [TestCase(2)]
        public void LegacyWorldIsExplicitlyRejectedWithoutChangingOriginal(int version)
        {
            var f = new BeltWorldFixture(); var json = JObject.Parse(f.Save()); json["worldVersion"] = version;
            string root = Path.Combine(Path.GetTempPath(), "belt-legacy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); string file = Path.Combine(root, "save.json"); string original = json.ToString(); File.WriteAllText(file, original);
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            { worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, file) };
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(options);
            LogAssert.Expect(LogType.Error, new Regex("変換できませんでした"));
            LogAssert.Expect(LogType.Error, new Regex("^セーブをロードできません"));
            LogAssert.Expect(LogType.Error, new Regex("^セーブファイルパス"));
            var error = Assert.Throws<Exception>(() => services.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize());
            StringAssert.Contains("new-world belt segment prototype", error.Message);
            Assert.AreEqual(original, File.ReadAllText(file)); Directory.Delete(root, true);
        }
        [Test]
        public void MergeBufferAndRetainedPriorityRoundTrip()
        {
            var f = new BeltWorldFixture(); var merge = f.Belt(Vector3Int.zero, BlockDirection.North);
            var input = f.Belt(Vector3Int.back, BlockDirection.North); f.Belt(Vector3Int.left, BlockDirection.East);
            f.Seed(input, 1); f.Tick(36);
            var state = f.Belts.CaptureCell(merge); Assert.IsNotNull(state.BufferedItem);
            string save = f.Save(); var before = f.Snapshot();
            var loaded = new BeltWorldFixture();
            ((WorldLoaderFromJson)loaded.Services.GetRequiredService<IWorldSaveDataLoader>()).Load(save); loaded.Belts.Load();
            var restored = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero).GetComponent<SegmentBeltComponent>();
            var after = loaded.Belts.CaptureCell(restored);
            Assert.AreEqual(state.PriorityIndex, after.PriorityIndex); Assert.AreEqual(state.BufferedItem.TransportGuid, after.BufferedItem.TransportGuid);
            Assert.AreEqual(new BeltReplaySimulation(before.Simulation).ComputeStateHash(), new BeltReplaySimulation(loaded.Snapshot().Simulation).ComputeStateHash());
        }
        [Test]
        public void MissingRunningAndBufferedMastersAreArchivedAndPrunedBeforeV3Load()
        {
            var fixture = new BeltWorldFixture(); var belt = fixture.Belt(new Vector3Int(20, 0, 30), BlockDirection.North);
            fixture.Seed(belt, 7); fixture.Tick(2);
            var valid = fixture.Belts.CaptureCell(belt).RunningItem;
            var save = JObject.Parse(fixture.Save());
            var block = (JObject)save["world"][0];
            var state = (JObject)block["state"][SegmentBeltComponent.SaveKeyStatic];
            var missingRunning = Guid.NewGuid(); var missingBuffered = Guid.NewGuid();
            var missingMaster = Guid.Parse(SaveLoadPreparerTestFixture.MissingGuid);
            state["RunningItem"] = JObject.FromObject(new BeltSavedItem(missingRunning, missingMaster, 64, BeltDirection.Back));
            state["BufferedItem"] = JObject.FromObject(new BeltSavedItem(missingBuffered, missingMaster, 256, BeltDirection.Left));
            var control = (JObject)block.DeepClone(); control["instanceId"] = 9999; control["X"] = 40;
            control["state"][SegmentBeltComponent.SaveKeyStatic]["RunningItem"] = JObject.FromObject(valid);
            control["state"][SegmentBeltComponent.SaveKeyStatic]["BufferedItem"] = null;
            ((JArray)save["world"]).Add(control);
            string root = Path.Combine(Path.GetTempPath(), "belt-prune-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); string file = Path.Combine(root, "save.json"); string original = save.ToString(); File.WriteAllText(file, original);
            var directory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, file);
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory) { worldDataDirectory = directory });
            services.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var world = services.GetRequiredService<BeltWorldDatastore>(); world.Load();
            var empty = ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(20, 0, 30)).GetComponent<SegmentBeltComponent>();
            Assert.IsNull(world.CaptureCell(empty).RunningItem); Assert.IsNull(world.CaptureCell(empty).BufferedItem);
            Assert.AreEqual(valid.TransportGuid, world.CaptureSnapshot().Simulation.Segments.SelectMany(segment => segment.Items).Single().Item.Guid);
            Assert.AreEqual(original, File.ReadAllText(file)); Assert.AreEqual(original, File.ReadAllText(directory.BackupSaveJsonPath(3)));
            string archive = string.Join("\n", Directory.GetFiles(directory.SavePrunedDirectory, "*.json").Select(File.ReadAllText));
            StringAssert.Contains(missingMaster.ToString(), archive); StringAssert.Contains(missingRunning.ToString(), archive); StringAssert.Contains(missingBuffered.ToString(), archive);
            StringAssert.Contains("RunningItem", archive); StringAssert.Contains("BufferedItem", archive); StringAssert.Contains("20", archive);
            Directory.Delete(root, true);
        }
        [Test]
        public void V3VerticalDirectionRejectsWholeLoadAndPreservesOriginal()
        {
            var fixture = new BeltWorldFixture(); fixture.Belt(Vector3Int.zero, BlockDirection.North);
            var json = JObject.Parse(fixture.Save()); json["world"][0]["direction"] = (int)BlockDirection.UpNorth;
            string root = Path.Combine(Path.GetTempPath(), "belt-direction-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); string file = Path.Combine(root, "save.json"); string original = json.ToString(); File.WriteAllText(file, original);
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            { worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, file) });
            LogAssert.Expect(LogType.Error, new Regex("^\\[BlockPlacement\\] Save rejected"));
            Assert.Throws<Exception>(() => services.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize());
            Assert.IsEmpty(ServerContext.WorldBlockDatastore.BlockMasterDictionary);
            Assert.AreEqual(original, File.ReadAllText(file)); Directory.Delete(root, true);
        }
    }
}
