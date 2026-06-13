using Game.CleanRoom.Pollution;
using NUnit.Framework;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomPollutionTest
    {
        [Test]
        public void ComputeATotal_ReferenceRoom_MachineLess()
        {
            // 基準部屋(機械なし): V=74, S=109, 接続点2(ItemHatch1+PipeHatch1), ハッチ搬送0。
            // Machine-less reference room: V=74, S=109, connectors=2, hatch throughput 0.
            var aTotal = CleanRoomPollutionCalculator.ComputeATotal(
                volume: 74, surfaceArea: 109, connectorCount: 2, runningMachineCount: 0,
                hatchThroughputPerSecond: 0.0);

            // 0.10*74 + 0.05*109 + 0.50*2 = 13.85
            Assert.AreEqual(13.85, aTotal, 1e-9);
        }

        [Test]
        public void ComputeATotal_MachineTermAddsTwoPerRunningMachine()
        {
            // A_machine=2.0 個/(稼働機械·秒) の係数を固定（実機械の配線はフェーズ4）。
            // Pin the A_machine=2.0 coefficient; actual machine wiring lands in phase 4.
            var withMachine = CleanRoomPollutionCalculator.ComputeATotal(74, 109, 2, 1, 0.0);
            Assert.AreEqual(15.85, withMachine, 1e-9);
        }
    }
}
