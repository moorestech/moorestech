using System.Collections.Generic;
using NUnit.Framework;
using Tests.Util;
using static Tests.UnitTest.Game.TrainStation.Naming.TrainStationNamingTestInput;

namespace Tests.UnitTest.Game.TrainStation.Naming
{
    public class TrainStationNameRankingTest
    {
        [SetUp]
        public void Setup()
        {
            TrainTestHelper.CreateEnvironment();
        }

        [TestCase(5, 1, "Test2 Station")]
        [TestCase(1, 5, "Test1 Station")]
        [TestCase(3, 2, "Steam Station")]
        [TestCase(2, 3, "Water Station")]
        [TestCase(2, 1, "Test1 Station")]
        public void EquidistantVeinsKeepInputOrderWithinKindAndItemsBeatFluids(int first, int second, string expected)
        {
            var map = EmptyMap();
            AddVein(map, first);
            AddVein(map, second);
            Assert.AreEqual(expected, Scan(map, new Dictionary<string, string>()));
        }

        [Test]
        public void ObjectTiesUseOrdinalEnglishNamesInsteadOfInsertionOrCulture()
        {
            var map = EmptyMap();
            AddObject(map, TreeTest);
            AddObject(map, TreeGuid);
            // Zebraは序数順でappleより前、文化依存順では後になる
            // Zebra precedes apple ordinally but follows it culturally
            var names = new Dictionary<string, string> { [Item1] = "Zebra", [Item2] = "apple" };
            Assert.AreEqual("Zebra Station", Scan(map, names));
        }

        [Test]
        public void DifferentObjectGuidsAggregateByEarnedItemName()
        {
            var map = EmptyMap();
            AddObject(map, TreeGuid);
            AddObject(map, TreeGuid);
            AddObject(map, TreeTest);
            AddObject(map, Rock);
            AddObject(map, Rubble);
            Assert.AreEqual("Test2 Station", Scan(map, new Dictionary<string, string>()));
        }

        [Test]
        public void FluidAndObjectNamesUseEnglishLookup()
        {
            var names = new Dictionary<string, string> { [Fluid1] = "Spring", [Item2] = "Timber" };
            var fluidMap = EmptyMap();
            AddVein(fluidMap, 2);
            Assert.AreEqual("Spring Station", Scan(fluidMap, names));
            var objectMap = EmptyMap();
            AddObject(objectMap, TreeTest);
            Assert.AreEqual("Timber Station", Scan(objectMap, names));
        }
    }
}
