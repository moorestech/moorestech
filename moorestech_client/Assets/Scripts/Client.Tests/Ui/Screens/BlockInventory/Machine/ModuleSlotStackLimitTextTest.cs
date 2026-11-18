using Client.Tests.Ui.TestHost;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// モジュール 1 枠の上限の文
    /// The per-module-slot limit text, redrawn when the value changes
    /// </summary>
    public class ModuleSlotStackLimitTextTest : MachineScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/details/machine/ModuleSlotStackLimitText.test.ts">配信された上限を差し込んで描く</web-test>
        [Test]
        public void 上限を差し込んで描き値が変わると描き直す()
        {
            var root = MountSection(MachineScreenViewModels.Filled(1, 1, 0, FirstItem, 1, 8), false).Root;
            Assert.AreEqual("Up to 8 per slot", root.Q<Label>("machine-module-stack-limit").PlainText());

            SetMachine(MachineScreenViewModels.Filled(1, 1, 0, FirstItem, 1, 16));
            Assert.AreEqual("Up to 16 per slot", root.Q<Label>("machine-module-stack-limit").PlainText());
        }
    }
}
