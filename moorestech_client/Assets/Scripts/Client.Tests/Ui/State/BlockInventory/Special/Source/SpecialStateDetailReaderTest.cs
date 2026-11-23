using System.Linq;
using Client.Tests.Ui.State.BlockInventory.Special.Cargo;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Source;
using Core.Master;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Blocks.GearToElectric;
using Game.Block.Interface.State;
using Game.Gear.Common;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// state detail とマスタの値から Web の DTO と同じ値の詳細を組む
    /// Builds details with the same values as the Web DTOs from state details and master values
    /// </summary>
    public class SpecialStateDetailReaderTest
    {
        [Test]
        public void 回転生成機は選択と充足率と消費電力とマスタ順のモードを写す()
        {
            var state = new ElectricToGearGeneratorBlockStateDetail { SelectedIndex = 1, ElectricFulfillmentRate = 0.75f, ConsumedElectricPower = 10f };
            var modes = new[] { new ElectricToGearOutputMode(10, 10, 10), new ElectricToGearOutputMode(20, 20, 10) };

            var detail = SpecialStateDetailReader.ElectricToGear(state, modes);

            Assert.AreEqual(1, detail.SelectedIndex);
            Assert.AreEqual(0.75d, detail.FulfillmentRate, 1e-6);
            Assert.AreEqual(10d, detail.ConsumedElectricPower, 1e-6);
            CollectionAssert.AreEqual(modes, detail.OutputModes);
        }

        [TestCase(GearToElectricGenerationState.Generating, GearToElectricDisplayState.Generating)]
        [TestCase(GearToElectricGenerationState.Idle, GearToElectricDisplayState.Idle)]
        [TestCase(GearToElectricGenerationState.Stopped, GearToElectricDisplayState.Stopped)]
        public void 回転発電機の発電状態を表示の状態へ写す(GearToElectricGenerationState server, GearToElectricDisplayState expected)
        {
            var state = new GearToElectricGeneratorBlockStateDetail { GenerationState = server, GeneratedElectricPower = 72.4f };
            var detail = SpecialStateDetailReader.GearToElectric(state, 100);
            Assert.AreEqual(expected, detail.State);
            Assert.AreEqual(72.4d, detail.SuppliedPower, 1e-4);
            Assert.AreEqual(100d, detail.MaxGeneratedPower);
        }

        [Test]
        public void 歯車は役割ごとに消費側は基準RPM付き発電側は現在値だけを持つ()
        {
            var consumer = SpecialStateDetailReader.Consumer(new GearStateDetail(true, 5f, 2.4f, GearRole.Consumer), 10);
            var generator = SpecialStateDetailReader.Generator(new GearStateDetail(true, 20f, 5f, GearRole.Generator));
            var consumerRow = GearRowDisplay.From(consumer);
            var generatorRow = GearRowDisplay.From(generator);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.GearRpmWithBase, new[] { "current", "5.0", "base", "10.0" }, consumerRow.Rpm);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.GearGeneratedTorque, new[] { "value", "5.0" }, generatorRow.Torque);
        }

        [Test]
        public void 発射台は資材と積荷の枠と入れ口の束縛と積荷の選択と出力の先頭を持ち在庫の数まで出力を並べる()
        {
            var bindings = new[] { new InletSlotBinding(0, new[] { new ItemId(3) }) };
            var materials = new[] { new RequiredItemSlot(0, new ItemId(3), 20), new RequiredItemSlot(1, new ItemId(4), 5) };
            var payloads = new[] { new RequiredItemSlot(2, OrbitalCargoSamples.ChipLv1, 20) };
            var cargo = OrbitalCargoSamples.Pad(OrbitalCargoSamples.ChipLv1, null, OrbitalCargoSamples.ChipLv1);
            var detail = SpecialStateDetailReader.RocketLaunchPad(new RocketLaunchPadStateDetail(RocketLaunchPadPhase.AwaitingBuildCommand, RocketLaunchPadWaitReason.None, 0.375f, OrbitalCargoSamples.ChipLv1, null), materials, payloads, bindings, 3, cargo);
            Assert.AreSame(bindings, detail.InletBindings);
            Assert.AreSame(cargo, detail.Cargo);
            CollectionAssert.AreEqual(new[] { 0, 1 }, detail.MaterialSlots.Select(slot => slot.Slot).ToArray());
            CollectionAssert.AreEqual(new[] { 2 }, detail.PayloadSlots.Select(slot => slot.Slot).ToArray());
            CollectionAssert.AreEqual(new[] { 3, 4 }, detail.OutputSlots(5));
            CollectionAssert.IsEmpty(detail.OutputSlots(3));
            Assert.AreEqual(0.375f, detail.Progress);
        }

        [Test]
        public void 宇宙エレベーターは入口1枠と選んだ積荷の束縛と出口の連番とプールと状態を持つ()
        {
            var satellite = OrbitalCargoSamples.ChipLv1;
            var selected = new OrbitalCargoDetail(satellite, null, new[] { OrbitalCargoSamples.ChipLv1Choice }, new[] { satellite }, 240);
            var detail = SpecialStateDetailReader.SpaceElevator(new SpaceElevatorBaseStateDetail(false, 250, 0f, SpaceElevatorBaseStatus.InsufficientCable, satellite, null), 0, 1, 2, 1000, selected);
            CollectionAssert.AreEqual(new[] { 0 }, detail.SatelliteInputSlots);
            Assert.AreEqual(1, detail.InletBindings.Count);
            Assert.AreEqual(0, detail.InletBindings[0].Slot);
            CollectionAssert.AreEqual(new[] { satellite }, detail.InletBindings[0].AcceptedItemIds);
            var unselected = new OrbitalCargoDetail(null, null, new[] { OrbitalCargoSamples.ChipLv1Choice }, new[] { satellite }, 240);
            CollectionAssert.IsEmpty(SpecialStateDetailReader.SpaceElevator(new SpaceElevatorBaseStateDetail(false, 250, 0f, SpaceElevatorBaseStatus.CargoNotSelected, null, null), 0, 1, 2, 1000, unselected).InletBindings, "未選択の入口は Shift で何も受けない");
            CollectionAssert.AreEqual(new[] { 1, 2 }, detail.OutputSlots);
            Assert.AreEqual(250, detail.CablePoolAmount);
            Assert.AreEqual(1000, detail.CablesPerElevator);
            Assert.AreEqual(SpaceElevatorBaseStatus.InsufficientCable, detail.Status);
            Assert.IsFalse(detail.Connected);
        }
    }
}
