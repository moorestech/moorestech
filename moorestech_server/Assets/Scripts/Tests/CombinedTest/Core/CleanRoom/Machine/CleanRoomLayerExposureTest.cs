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
