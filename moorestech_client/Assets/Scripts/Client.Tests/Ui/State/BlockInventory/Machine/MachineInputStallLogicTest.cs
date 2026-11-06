using Client.Ui.State.BlockInventory.Logic.Machine;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// Web の machineInputStallLogic.test.ts の写し（L0）
    /// Port of Web machineInputStallLogic.test.ts (L0)
    /// </summary>
    public class MachineInputStallLogicTest
    {
        /// <web-test file="src/features/blockInventory/details/machineInputStall/__tests__/machineInputStallLogic.test.ts">レベル違いはレベル違いの文言にする</web-test>
        [Test]
        public void レベル違いはレベル違いの文言にする()
        {
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.MachineInputStallMixedLevel.Key, MachineInputStallLogic.MessageKey(MachineInputStallReason.MixedLevel).Key);
        }

        /// <web-test file="src/features/blockInventory/details/machineInputStall/__tests__/machineInputStallLogic.test.ts">上限超えは製作上限の文言にする</web-test>
        [Test]
        public void 上限超えは製作上限の文言にする()
        {
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.MachineInputStallAboveCraftCap.Key, MachineInputStallLogic.MessageKey(MachineInputStallReason.AboveCraftCap).Key);
        }
    }
}
