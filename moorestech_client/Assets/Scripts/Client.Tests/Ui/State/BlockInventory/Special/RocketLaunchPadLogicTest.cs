using Client.Ui.State.BlockInventory.Logic.Special;
using Game.Block.Interface.State;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// Web の rocketLaunchPadLogic.test.ts の写し（L0）
    /// Port of Web rocketLaunchPadLogic.test.ts (L0)
    /// </summary>
    public class RocketLaunchPadLogicTest
    {
        /// <web-test file="src/features/blockInventory/details/rocket/rocketLaunchPadLogic.test.ts">%s has a status and only command waits have a button</web-test>
        [TestCase(RocketLaunchPadPhase.Idle, "ui.blockInventory.rocketStatusIdle", null)]
        [TestCase(RocketLaunchPadPhase.AwaitingBuildCommand, "ui.blockInventory.rocketStatusAwaitingBuild", "build")]
        [TestCase(RocketLaunchPadPhase.Building, "ui.blockInventory.rocketStatusBuilding", null)]
        [TestCase(RocketLaunchPadPhase.AwaitingLaunchCommand, "ui.blockInventory.rocketStatusAwaitingLaunch", "launch")]
        [TestCase(RocketLaunchPadPhase.ReadyToLaunch, "ui.blockInventory.rocketStatusReadyToLaunch", null)]
        [TestCase(RocketLaunchPadPhase.Launching, "ui.blockInventory.rocketStatusLaunching", null)]
        public void 段階ごとに状態文を持ち合図待ちだけがボタンを持つ(RocketLaunchPadPhase phase, string statusKey, string command)
        {
            Assert.AreEqual(statusKey, RocketLaunchPadLogic.StatusKey(phase, RocketLaunchPadWaitReason.None).Key);
            var hasButton = RocketLaunchPadLogic.TryGetCommandButton(phase, out var button);
            var actualCommand = hasButton ? (button.Command == RocketCommand.Build ? "build" : "launch") : null;
            Assert.AreEqual(command, actualCommand);
        }

        /// <web-test file="src/features/blockInventory/details/rocket/rocketLaunchPadLogic.test.ts">%s takes precedence over the phase</web-test>
        [TestCase(RocketLaunchPadWaitReason.MissingMaterials, "ui.blockInventory.rocketWaitMissingMaterials")]
        [TestCase(RocketLaunchPadWaitReason.MissingPayload, "ui.blockInventory.rocketWaitMissingPayload")]
        [TestCase(RocketLaunchPadWaitReason.OutputFull, "ui.blockInventory.rocketWaitOutputFull")]
        [TestCase(RocketLaunchPadWaitReason.NoPower, "ui.blockInventory.rocketWaitNoPower")]
        public void 待ち理由は段階より優先する(RocketLaunchPadWaitReason waitReason, string statusKey)
        {
            Assert.AreEqual(statusKey, RocketLaunchPadLogic.StatusKey(RocketLaunchPadPhase.ReadyToLaunch, waitReason).Key);
        }
    }
}
