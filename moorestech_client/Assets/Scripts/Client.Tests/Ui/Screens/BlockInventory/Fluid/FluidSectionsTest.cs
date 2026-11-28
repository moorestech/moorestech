using System.Linq;
using Client.Tests.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.Components.Slots;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Machine;
using NUnit.Framework;
using UnityEngine.UIElements;
using S = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst;

namespace Client.Tests.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の 2 区画の出し分け
    /// Visibility of the two fluid sections
    /// </summary>
    public class FluidSectionsTest : FluidScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">%s の既存表示を保つ</web-test>
        [TestCase("Chest", S.Chest, null, true)]
        [TestCase("ElectricMachine", S.Machine, "machine-fluid-slots", false)]
        [TestCase("GearMachine", S.Machine, "machine-fluid-slots", false)]
        [TestCase("ElectricGenerator", S.ElectricGenerator, "generator-fluid-slots", false)]
        [TestCase("FuelGearGenerator", S.FuelGearGenerator, null, true)]
        [TestCase("SimpleGearGenerator", S.FuelGearGenerator, null, true)]
        [TestCase("ElectricMiner", S.ElectricMiner, null, true)]
        [TestCase("GearMiner", S.GearMiner, null, true)]
        [TestCase("CleanRoomMachine", S.Machine, "machine-fluid-slots", false)]
        [TestCase("CleanRoomAirFilter", S.CleanRoomAirFilter, null, false)]
        [TestCase("unknown", S.Generic, "generic-block-fluids", true)]
        public void 液体の行は設定のtestidで出し進捗は設定どおり(string blockType, string blockScreen, string fluidRowTestId, bool showFluidProgress)
        {
            var body = MountBody(blockType, blockScreen);
            SetFluids(0.4f, BlockFluidSlotState.Empty(100));

            var rowNames = Shown<FluidSlotRow>(body.Root, null, null).Select(row => row.name).ToArray();
            Assert.AreEqual(fluidRowTestId == null ? new string[0] : new[] { fluidRowTestId }, rowNames);
            if (fluidRowTestId != null) Assert.AreEqual(showFluidProgress ? 1 : 0, Shown<VisualElement>(body.Root, "progress-arrow-bar", null).Count);
        }

        [TestCase("ElectricPump", S.Pump, "pump-fluid-slots")]
        [TestCase("GearPump", S.Pump, "pump-fluid-slots")]
        public void ポンプは進捗なしの行で出る(string blockType, string blockScreen, string rowTestId)
        {
            var body = MountBody(blockType, blockScreen);
            SetFluids(0.4f, BlockFluidSlotState.Empty(100));

            Assert.AreEqual(new[] { rowTestId }, Shown<FluidSlotRow>(body.Root, null, null).Select(row => row.name).ToArray());
            Assert.AreEqual(0, Shown<VisualElement>(body.Root, "progress-arrow-bar", null).Count);
        }

        [Test]
        public void パイプは詳細の区画で出し行は出さない()
        {
            var body = MountBody("FluidPipe", S.FluidPipe);
            SetFluids(null, BlockFluidSlotState.Empty(100));

            Assert.AreEqual(1, Shown<VisualElement>(body.Root, "fluid-pipe-fluid-slots", null).Count);
            Assert.AreEqual(new[] { "fluid-pipe-fluid-slots-slot-0" }, Shown<FluidSlotRow>(body.Root, null, null).Select(row => row.name).ToArray());
        }

        [Test]
        public void 機械のModelが有るとFluidRowは出ない()
        {
            var body = MountBody("unknown", S.Generic);
            SetFluids(null, BlockFluidSlotState.Empty(100));
            Assert.AreEqual(1, Shown<FluidSlotRow>(body.Root, null, null).Count);

            Machine.Set(SectionContent<MachineSectionViewModel>.Present(MachineTestDetails.ViewModelWithModules(0, 0f, 1)));
            simulate.FrameUpdate();
            Assert.AreEqual(0, Shown<FluidSlotRow>(body.Root, null, null).Count);

            Machine.Set(SectionContent<MachineSectionViewModel>.Absent);
            simulate.FrameUpdate();
            Assert.AreEqual(1, Shown<FluidSlotRow>(body.Root, null, null).Count);
        }

        [TestCase("unknown", S.Generic)]
        [TestCase("FluidPipe", S.FluidPipe)]
        public void 液体が0件なら行も詳細も出ない(string blockType, string blockScreen)
        {
            var body = MountBody(blockType, blockScreen);
            SetFluids(null);

            Assert.AreEqual(0, Shown<FluidSlotRow>(body.Root, null, null).Count);
        }
    }
}
