using System;
using System.IO;
using System.Linq;
using Core.Master;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Master.SpaceElevator
{
    public class OrbitalOutputCapacityRecoveryTest
    {
        [SetUp]
        public void Setup() { Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil.CreateServer(); }
        [TearDown]
        public void Restore() { Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil.CreateServer(); }

        [TestCase(true)]
        [TestCase(false)]
        public void WideCeilingRejectsTwoBillionOutputsInOneSlot(bool rocket)
        {
            var masters = Create(1500000000, new[] { 2000000000 }, 1, rocket);
            var error = Assert.Throws<InvalidOperationException>(() => MasterHolder.Load(masters));
            StringAssert.Contains("2 stacks at level-1 stack", error.Message);
        }

        [TestCase(100, 1, 1, 1)]
        [TestCase(1000000, int.MaxValue, 0, 2148)]
        [TestCase(1000000, int.MaxValue, int.MaxValue, 4295)]
        public void BothValidatorsAggregateDuplicateRowsWithoutAllocatingStacks(int capacity, int first, int second, int slots)
        {
            var counts = second == 0 ? new[] { first } : new[] { first, second };
            MasterHolder.Load(Create(capacity, counts, slots, true));
            var reward = ForUnitTestItemId.TestElevatorReward;
            Assert.AreEqual(slots, MasterHolder.ItemMaster.CountRequiredSlotsAtLevelOne(counts.Select(count => (reward, count))));

        }

        [TestCase(10, 1, false)]
        [TestCase(10, 10, true)]
        [TestCase(10, 100, true)]
        public void StackTableRejectsOnlyDecreasingLevels(int first, int second, bool expected)
        {
            // テーブルを独立入力として検証し、初期装備の容量条件は変えない
            // Validate an independent table input without changing initial-equipment capacity checks
            var configs = ModJsonStringLoader.GetMasterString(new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")));
            var key = new JsonFileName("items");
            var json = JObject.Parse(configs[0].JsonContents[key]);
            ((JArray)json["itemStackLevelTables"]).Add(new JObject { ["tableGuid"] = Guid.NewGuid().ToString(), ["name"] = "MonotonicRecovery", ["stackCounts"] = new JArray(first, second) });
            var itemMaster = new ItemMaster(json);
            Assert.AreEqual(expected, itemMaster.Validate(out var errors), errors);
            if (!expected) StringAssert.Contains("MonotonicRecovery StackCounts must be nondecreasing: level 1 (10) to level 2 (1)", errors);
        }

        private static MasterJsonFileContainer Create(int capacity, int[] outputs, int slots, bool rocket)
        {
            var configs = ModJsonStringLoader.GetMasterString(new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")));
            var itemKey = new JsonFileName("items");
            var blockKey = new JsonFileName("blocks");
            var items = JObject.Parse(configs[0].JsonContents[itemKey]);
            var satellite = MasterHolder.ItemMaster.GetItemGuid(ForUnitTestItemId.TestElevatorSatellite);
            var reward = MasterHolder.ItemMaster.GetItemGuid(ForUnitTestItemId.TestElevatorReward);
            foreach (var payload in items["orbitalPayloads"]) payload["arrivalOutputs"] = new JArray();
            items["orbitalPayloads"].Single(p => (Guid)p["payloadItemGuid"] == satellite)["arrivalOutputs"] =
                new JArray(outputs.Select(count => new JObject { ["itemGuid"] = reward.ToString(), ["count"] = count }));
            var table = Guid.NewGuid().ToString();
            ((JArray)items["itemStackLevelTables"]).Add(new JObject { ["tableGuid"] = table, ["name"] = "CapacityRecovery", ["stackCounts"] = new JArray(capacity) });
            items["data"].Single(i => (Guid)i["itemGuid"] == reward)["stackLevelTableGuid"] = table;
            var blocks = JObject.Parse(configs[0].JsonContents[blockKey]);
            foreach (var block in blocks["data"].Where(b => (string)b["blockType"] == "SpaceElevatorBase" || (string)b["blockType"] == "RocketLaunchPad"))
                block["blockParam"]["outputSlotCount"] = !rocket && (string)block["blockType"] == "RocketLaunchPad" ? int.MaxValue : slots;
            configs[0].JsonContents[itemKey] = items.ToString();
            configs[0].JsonContents[blockKey] = blocks.ToString();
            return new MasterJsonFileContainer(configs);
        }

    }
}
