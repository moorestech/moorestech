using System.Linq;
using Core.Master;
using Game.Context;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomMachineTestHelpers;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomProcessMachineTestHelpers;

namespace Tests.CombinedTest.Core.CleanRoom.Machine
{
    public class CleanRoomLayerExposureTest
    {
        // 層失敗率は最悪クラスでも0.25。20枚が全部失敗する確率は0.25^20
        // The layer fail rate is at most 0.25 even in the worst class; all 20 failing has odds 0.25^20
        private const int WaferCount = 20;
        private const int TickCap = 4000;

        [TestCase(0)]
        [TestCase(1)]
        public void ExtremeFailureRateDeterminesEveryOutputTest(int failRate)
        {
            CleanRoomHatchTest.CreateServer();
            // テスト内の全クラスを同じ端点率にする
            // Set every in-memory class to the same endpoint rate
            var container = ServerContext.GetService<MasterJsonFileContainer>();
            var key = new JsonFileName("cleanRoom");
            var config = container.ConfigJsons.Single(entry => entry.JsonContents.ContainsKey(key));
            var json = JObject.Parse(config.JsonContents[key]);
            foreach (var threshold in json["data"][0]["thresholds"]) threshold["layerFailRate"] = failRate;
            config.JsonContents[key] = json.ToString();
            MasterHolder.Load(container);
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomLayerExposureRecipe, ForUnitTestItemId.TestGradedWaferG2, WaferCount);
            var collected = RunAndCollect(filter, machine, WaferCount, TickCap);
            Assert.AreEqual(WaferCount * (1 - failRate), CountOf(collected, ForUnitTestItemId.TestLayeredWaferG2));
            Assert.AreEqual(WaferCount * failRate, CountOf(collected, ForUnitTestItemId.TestFailedPatternWafer));
        }

        [Test]
        public void ExposureKeepsInputGradeOrFailsTest()
        {
            CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();

            // G2のウェハは成功ならG2の次層ウェハ、失敗なら失敗パターンウェハになり、他のグレードには化けない
            // A G2 wafer becomes a G2 layered wafer on success or a failed pattern wafer, never another grade
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomLayerExposureRecipe, ForUnitTestItemId.TestGradedWaferG2, WaferCount);
            var collected = RunAndCollect(filter, machine, WaferCount, TickCap);

            var layeredGradeTwo = CountOf(collected, ForUnitTestItemId.TestLayeredWaferG2);
            Assert.AreEqual(WaferCount, layeredGradeTwo + CountOf(collected, ForUnitTestItemId.TestFailedPatternWafer));
            Assert.AreEqual(0, CountOf(collected, ForUnitTestItemId.TestLayeredWaferG1));
            Assert.AreEqual(0, CountOf(collected, ForUnitTestItemId.TestLayeredWaferG3));
            Assert.Greater(layeredGradeTwo, 0);
        }
    }
}
