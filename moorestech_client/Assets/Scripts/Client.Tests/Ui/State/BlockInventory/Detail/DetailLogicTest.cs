using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Detail
{
    /// <summary>
    /// Web の detailLogic.test.ts の写し（L0）
    /// Port of Web detailLogic.test.ts (L0)
    /// </summary>
    public class DetailLogicTest
    {
        /// <web-test file="src/features/blockInventory/details/detailLogic.test.ts">computes output items per minute from recipe seconds</web-test>
        [Test]
        public void レシピ秒から毎分の出力個数を出しレシピ秒0なら出さない()
        {
            Assert.IsTrue(BlockDetailMath.TryGetItemsPerMinute(3, 15, out var perMinute));
            Assert.AreEqual(12, perMinute);
            Assert.IsFalse(BlockDetailMath.TryGetItemsPerMinute(3, 0, out _));
        }

        /// <web-test file="src/features/blockInventory/details/detailLogic.test.ts">computePowerRate follows the uGUI formula</web-test>
        [Test]
        public void 充足率はuGUIと同式で要求0は1とみなす()
        {
            Assert.AreEqual(0.5, BlockDetailMath.ComputePowerRate(50, 100));
            Assert.AreEqual(1, BlockDetailMath.ComputePowerRate(0, 0));
        }

        /// <web-test file="src/features/blockInventory/details/detailLogic.test.ts">splitSlotIndices splits input→output→module in order</web-test>
        [Test]
        public void 統合indexを入力出力モジュールの順に割り範囲外を作らない()
        {
            var bands = new MachineSlotLayout(2, 1, 1).Split(4);
            CollectionAssert.AreEqual(new[] { 0, 1 }, bands.Input);
            CollectionAssert.AreEqual(new[] { 2 }, bands.Output);
            CollectionAssert.AreEqual(new[] { 3 }, bands.Module);

            // 総数が合わなくても範囲外を作らない
            // Never produce out-of-range indices even when counts mismatch
            var mismatched = new MachineSlotLayout(3, 2, 0).Split(4);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, mismatched.Input);
            CollectionAssert.AreEqual(new[] { 3 }, mismatched.Output);
            CollectionAssert.IsEmpty(mismatched.Module);
        }

        /// <web-test file="src/features/blockInventory/details/detailLogic.test.ts">fuelRatio clamps to 0..1 and handles zero denominators</web-test>
        [Test]
        public void 燃料比は0から1に収め分母0は0()
        {
            Assert.AreEqual(0.5, BlockDetailMath.FuelRatio(5, 10));
            Assert.AreEqual(0, BlockDetailMath.FuelRatio(0, 0));
            Assert.AreEqual(1, BlockDetailMath.FuelRatio(20, 10));
        }

        /// <web-test file="src/features/blockInventory/details/detailLogic.test.ts">stopReasonTranslationKey maps finite reasons to typed localization keys</web-test>
        [Test]
        public void 停止理由ごとに文言キーを選び停止していなければ文言なし()
        {
            Assert.IsFalse(BlockDetailStateDisplay.TryGetStopReasonKey(GearNetworkStopReasonKind.None, out _));
            AssertStopReason(GearNetworkStopReasonKind.Rocked, LocalizationKeys.Ui.BlockInventory.StopReasonLocked);
            AssertStopReason(GearNetworkStopReasonKind.OverRequirePower, LocalizationKeys.Ui.BlockInventory.StopReasonInsufficientPower);
            AssertStopReason(GearNetworkStopReasonKind.NoGeneration, LocalizationKeys.Ui.BlockInventory.StopReasonNoGeneration);
            AssertStopReason(GearNetworkStopReasonKind.NoGenerator, LocalizationKeys.Ui.BlockInventory.StopReasonNoGenerator);
        }

        /// <web-test file="src/features/blockInventory/details/detailLogic.test.ts">$state の表示を確定する</web-test>
        [TestCase(MachineProcessDisplayState.Idle, "ui.blockInventory.machineStateIdle", false, true)]
        [TestCase(MachineProcessDisplayState.Processing, "ui.blockInventory.machineStateProcessing", false, true)]
        [TestCase(MachineProcessDisplayState.Halted, "ui.blockInventory.machineStateHalted", true, false)]
        public void 機械の状態ごとに文言と不足トーンと充足率の表示可否を決める(MachineProcessDisplayState state, string key, bool insufficient, bool showPowerRate)
        {
            var display = BlockDetailStateDisplay.Machine(state);
            Assert.AreEqual(key, display.LabelKey.Key);
            Assert.AreEqual(insufficient, display.Insufficient);
            Assert.AreEqual(showPowerRate, display.ShowPowerRate);
        }

        /// <web-test file="src/features/blockInventory/details/detailLogic.test.ts">要求電力0でも稼働状態なら充足率を表示する</web-test>
        [Test]
        public void 要求電力0でも稼働状態なら充足率を表示する()
        {
            Assert.IsTrue(BlockDetailStateDisplay.Machine(MachineProcessDisplayState.Idle).ShowPowerRate);
            Assert.IsTrue(BlockDetailStateDisplay.Machine(MachineProcessDisplayState.Processing).ShowPowerRate);
        }

        /// <web-test file="src/features/blockInventory/details/detailLogic.test.ts">gear rows follow the role: consumers consume with a base RPM, generators generate with current RPM only</web-test>
        [Test]
        public void 歯車行は消費側が基準RPM付きで発電側は現在値だけ()
        {
            var consumer = GearRowDisplay.From(GearDetail.Consumer(5, 2.4, 10));
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.GearConsumedTorque, new[] { "value", "2.4" }, consumer.Torque);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.GearRpmWithBase, new[] { "current", "5.0", "base", "10.0" }, consumer.Rpm);

            var generator = GearRowDisplay.From(GearDetail.Generator(20, 5));
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.GearGeneratedTorque, new[] { "value", "5.0" }, generator.Torque);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.GearRpmCurrent, new[] { "current", "20.0" }, generator.Rpm);
        }

        // Web に対応する it が無い新設の検査（発電状態の文言と不足トーン）
        // New check with no Web counterpart (generation label and insufficient tone)
        [Test]
        public void 発電状態ごとに文言と不足トーンを決める()
        {
            AssertGearToElectric(GearToElectricDisplayState.Generating, LocalizationKeys.Ui.BlockInventory.GearToElectricStateGenerating, false);
            AssertGearToElectric(GearToElectricDisplayState.Idle, LocalizationKeys.Ui.BlockInventory.GearToElectricStateIdle, false);
            AssertGearToElectric(GearToElectricDisplayState.Stopped, LocalizationKeys.Ui.BlockInventory.GearToElectricStateStopped, true);
        }

        private static void AssertGearToElectric(GearToElectricDisplayState state, LocalizationKey expected, bool insufficient)
        {
            var display = BlockDetailStateDisplay.GearToElectric(state);
            Assert.AreEqual(expected.Key, display.LabelKey.Key);
            Assert.AreEqual(insufficient, display.Insufficient);
        }

        private static void AssertStopReason(GearNetworkStopReasonKind reason, LocalizationKey expected)
        {
            Assert.IsTrue(BlockDetailStateDisplay.TryGetStopReasonKey(reason, out var key), reason.ToString());
            Assert.AreEqual(expected.Key, key.Key);
        }
    }
}
