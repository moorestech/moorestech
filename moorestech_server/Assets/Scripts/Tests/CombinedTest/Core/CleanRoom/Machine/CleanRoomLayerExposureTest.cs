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
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomLayerExposureRecipe, ForUnitTestItemId.TestTransistorWafer, WaferCount);
            var collected = RunAndCollect(filter, machine, WaferCount, TickCap);
            Assert.AreEqual(WaferCount * (1 - failRate), CountOf(collected, ForUnitTestItemId.TestLayeredWafer));
            Assert.AreEqual(WaferCount * failRate, CountOf(collected, ForUnitTestItemId.TestFailedPatternWafer));
        }

        [Test]
        public void ExposureYieldsNextLayerOrFailsTest()
        {
            CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();

            // ウェハは成功なら次層ウェハ、失敗なら失敗パターンウェハになり、ほかの品目には化けない
            // A wafer becomes the next-layer wafer on success or a failed pattern wafer, never anything else
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomLayerExposureRecipe, ForUnitTestItemId.TestTransistorWafer, WaferCount);
            var collected = RunAndCollect(filter, machine, WaferCount, TickCap);

            var layered = CountOf(collected, ForUnitTestItemId.TestLayeredWafer);
            Assert.AreEqual(WaferCount, layered + CountOf(collected, ForUnitTestItemId.TestFailedPatternWafer));
            Assert.AreEqual(WaferCount, collected.Values.Sum());
            Assert.Greater(layered, 0);
        }
    }
}
