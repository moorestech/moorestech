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
            Assert.AreEqual(new BeltReplaySimulation(expected.Simulation).ComputeStateHash(), new BeltReplaySimulation(restored.Simulation).ComputeStateHash());
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
    }
}
