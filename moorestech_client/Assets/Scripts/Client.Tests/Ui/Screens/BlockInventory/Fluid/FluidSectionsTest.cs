using System.Linq;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.Components.Slots;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Machine;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の 2 区画（行と詳細）の出し分け。blockType ごとの設定・機械・件数で決まる
    /// Visibility of the two fluid sections (row and details) per blockType setting, machine and fluid count
    /// </summary>
    public class FluidSectionsTest : FluidScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">%s の既存表示を保つ</web-test>
        [TestCase("Chest", null, true)]
        [TestCase("ElectricMachine", "machine-fluid-slots", false)]
        [TestCase("GearMachine", "machine-fluid-slots", false)]
        [TestCase("ElectricGenerator", "generator-fluid-slots", false)]
        [TestCase("FuelGearGenerator", null, true)]
        [TestCase("SimpleGearGenerator", null, true)]
        [TestCase("ElectricMiner", null, true)]
        [TestCase("GearMiner", null, true)]
        [TestCase("CleanRoomMachine", "machine-fluid-slots", false)]
        [TestCase("CleanRoomAirFilter", null, false)]
        [TestCase("unknown", "generic-block-fluids", true)]
        public void 液体の行は設定のtestidで出し進捗は設定どおり(string blockType, string fluidRowTestId, bool showFluidProgress)
        {
            var body = MountBody(blockType);
            SetFluids(0.4f, BlockFluidSlotState.Empty(100));

            var rowNames = Shown<FluidSlotRow>(body.Root).Select(row => row.name).ToArray();
            Assert.AreEqual(fluidRowTestId == null ? new string[0] : new[] { fluidRowTestId }, rowNames);
            if (fluidRowTestId != null) Assert.AreEqual(showFluidProgress ? 1 : 0, Shown<VisualElement>(body.Root, "progress-arrow-bar").Count);
        }

        [TestCase("ElectricPump", "pump-fluid-slots")]
        [TestCase("GearPump", "pump-fluid-slots")]
        public void ポンプは進捗なしの行で出る(string blockType, string rowTestId)
        {
            var body = MountBody(blockType);
            SetFluids(0.4f, BlockFluidSlotState.Empty(100));

            Assert.AreEqual(new[] { rowTestId }, Shown<FluidSlotRow>(body.Root).Select(row => row.name).ToArray());
            Assert.AreEqual(0, Shown<VisualElement>(body.Root, "progress-arrow-bar").Count);
        }

        [Test]
        public void パイプは詳細の区画で出し行は出さない()
        {
            var body = MountBody("FluidPipe");
            SetFluids(null, BlockFluidSlotState.Empty(100));

            Assert.AreEqual(1, Shown<VisualElement>(body.Root, "fluid-pipe-fluid-slots").Count);
            Assert.AreEqual(new[] { "fluid-pipe-fluid-slots-slot-0" }, Shown<FluidSlotRow>(body.Root).Select(row => row.name).ToArray());
        }

        [Test]
        public void 機械のModelが有るとFluidRowは出ない()
        {
            var body = MountBody("unknown");
            SetFluids(null, BlockFluidSlotState.Empty(100));
            Assert.AreEqual(1, Shown<FluidSlotRow>(body.Root).Count);

            Machine.Set(SectionContent<MachineSectionViewModel>.Present(MachineTestDetails.ViewModelWithModules(0, 0f, 1)));
            simulate.FrameUpdate();
            Assert.AreEqual(0, Shown<FluidSlotRow>(body.Root).Count);

            Machine.Set(SectionContent<MachineSectionViewModel>.Absent);
            simulate.FrameUpdate();
            Assert.AreEqual(1, Shown<FluidSlotRow>(body.Root).Count);
        }

        [TestCase("unknown")]
        [TestCase("FluidPipe")]
        public void 液体が0件なら行も詳細も出ない(string blockType)
        {
            var body = MountBody(blockType);
            SetFluids(null);

            Assert.AreEqual(0, Shown<FluidSlotRow>(body.Root).Count);
        }
    }
}
