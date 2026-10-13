using System.IO;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.UnitTest.Game.TrainStation.Naming.TrainStationNamingTestInput;

namespace Tests.UnitTest.Game.TrainStation.Naming
{
    public class TrainStationNameModIntegrationTest
    {
        private string _directory;
        private string _masterMapPath;

        [SetUp]
        public void Setup()
        {
            _directory = CopyMod();
            _masterMapPath = Path.Combine(_directory, "mods/forUnitTest/master/map.json");
        }

        [TearDown]
        public void Cleanup()
        {
            Directory.Delete(_directory, true);
        }

        [TestCase(0, 0, "Ore Station")]
        [TestCase(4, 0, "Spring Station")]
        public void CsvNamesReachActuallyPlacedStations(int x, int z, string expected)
        {
            var localization = Path.Combine(_directory, "mods/forUnitTest/localization");
            Directory.CreateDirectory(localization);
            File.WriteAllText(Path.Combine(localization, "localization.csv"),
                $"key,Source,english\n{Item1},,Ore\n{Fluid1},,Spring\n");
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(_directory));
            // 辞書の直接注入を避け、DIと設置処理の配線を検査する
            // Inspect DI and placement wiring without injecting a dictionary directly
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation,
                new Vector3Int(x, 0, z), BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var block));
            Assert.AreEqual(expected, block.GetComponent<TrainStationComponent>().StationName);
        }

        [Test]
        public void MultipleRewardsUseFirstEarnedItem()
        {
            var master = JObject.Parse(File.ReadAllText(_masterMapPath));
            var rewards = (JArray)master["mapObjects"][0]["miningParam"]["earnItems"];
            var second = (JObject)rewards[0].DeepClone();
            second["itemGuid"] = "00000000-0000-0000-1234-000000000002";
            rewards.Add(second);
            File.WriteAllText(_masterMapPath, master.ToString());
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(_directory));
            var map = EmptyMap();
            AddObject(map, TreeGuid);
            Assert.AreEqual("Test1 Station", Scan(map, new System.Collections.Generic.Dictionary<string, string>()));
        }

        [Test]
        public void SharedEnglishNameBeatsLargerIndividualItemCount()
        {
            var master = JObject.Parse(File.ReadAllText(_masterMapPath));
            master["mapObjects"][3]["miningParam"]["earnItems"][0]["itemGuid"] = "00000000-0000-0000-1234-000000000003";
            File.WriteAllText(_masterMapPath, master.ToString());
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(_directory));
            // 個別の最多2対2では別名が勝ち、名前合算3対2でSharedが勝つ
            // Individual counts favor the competing name on a tie; name aggregation wins three to two
            var map = EmptyMap();
            AddObject(map, TreeGuid);
            AddObject(map, TreeGuid);
            AddObject(map, TreeTest);
            AddObject(map, Rubble);
            AddObject(map, Rubble);
            var names = new System.Collections.Generic.Dictionary<string, string>
            {
                [Item1] = "Shared", [Item2] = "Shared",
                ["item.00000000-0000-0000-1234-000000000003.name"] = "Aardvark"
            };
            Assert.AreEqual("Shared Station", Scan(map, names));
        }
    }
}
