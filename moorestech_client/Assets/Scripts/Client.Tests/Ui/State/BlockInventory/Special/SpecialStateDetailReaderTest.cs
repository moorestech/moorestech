using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Source;
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
        public void 発射台は資材と積荷の絶対添字と出力の先頭を持ち在庫の数まで出力を並べる()
        {
            var detail = SpecialStateDetailReader.RocketLaunchPad(new RocketLaunchPadStateDetail(RocketLaunchPadPhase.AwaitingBuildCommand, RocketLaunchPadWaitReason.None, 0.375f), new[] { 0, 1 }, new[] { 2 }, 3);
            CollectionAssert.AreEqual(new[] { 0, 1 }, detail.MaterialSlots);
            CollectionAssert.AreEqual(new[] { 2 }, detail.PayloadSlots);
            CollectionAssert.AreEqual(new[] { 3, 4 }, detail.OutputSlots(5));
            CollectionAssert.IsEmpty(detail.OutputSlots(3));
            Assert.AreEqual(0.375f, detail.Progress);
        }

        [Test]
        public void 宇宙エレベーターは入口1枠と出口の連番とプールと状態を持つ()
        {
            var detail = SpecialStateDetailReader.SpaceElevator(new SpaceElevatorBaseStateDetail(false, 250, 0f, SpaceElevatorBaseStatus.InsufficientCable), 0, 1, 2, 1000);
            CollectionAssert.AreEqual(new[] { 0 }, detail.SatelliteInputSlots);
            CollectionAssert.AreEqual(new[] { 1, 2 }, detail.OutputSlots);
            Assert.AreEqual(250, detail.CablePoolAmount);
            Assert.AreEqual(1000, detail.CablesPerElevator);
            Assert.AreEqual(SpaceElevatorBaseStatus.InsufficientCable, detail.Status);
            Assert.IsFalse(detail.Connected);
        }
    }
}
