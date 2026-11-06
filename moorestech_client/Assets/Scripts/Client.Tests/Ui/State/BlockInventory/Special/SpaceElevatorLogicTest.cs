using Client.Ui.State.BlockInventory.Logic.Special;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// Web の SpaceElevatorSection.test.tsx の状態表（spaceElevatorStatusDisplay の検査）の写し（L0）。描画の検査は S4-special
    /// Port of the status table in Web SpaceElevatorSection.test.tsx (the spaceElevatorStatusDisplay check) (L0); the render check belongs to S4-special
    /// </summary>
    public class SpaceElevatorLogicTest
    {
        /// <web-test file="src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx">%s の文言と不足色を表示する</web-test>
        [TestCase(SpaceElevatorStatus.ReadyToConnect, "ui.blockInventory.spaceElevatorStatusReadyToConnect", false)]
        [TestCase(SpaceElevatorStatus.InsufficientCable, "ui.blockInventory.spaceElevatorStatusInsufficientCable", true)]
        [TestCase(SpaceElevatorStatus.WaitingSatellite, "ui.blockInventory.spaceElevatorStatusWaitingSatellite", false)]
        [TestCase(SpaceElevatorStatus.Climbing, "ui.blockInventory.spaceElevatorStatusClimbing", false)]
        [TestCase(SpaceElevatorStatus.LowPower, "ui.blockInventory.spaceElevatorStatusLowPower", true)]
        [TestCase(SpaceElevatorStatus.InletBlocked, "ui.blockInventory.spaceElevatorStatusInletBlocked", true)]
        [TestCase(SpaceElevatorStatus.OutputBlocked, "ui.blockInventory.spaceElevatorStatusOutputBlocked", true)]
        public void 状態ごとに文言と不足色を決める(SpaceElevatorStatus status, string labelKey, bool insufficient)
        {
            var display = SpaceElevatorLogic.StatusDisplay(status);
            Assert.AreEqual(labelKey, display.LabelKey.Key);
            Assert.AreEqual(insufficient, display.Insufficient);
        }
    }
}
