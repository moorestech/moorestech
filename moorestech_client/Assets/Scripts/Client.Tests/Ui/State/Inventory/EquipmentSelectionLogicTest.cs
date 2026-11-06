using Client.Ui.State.Inventory.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Inventory
{
    public class EquipmentSelectionLogicTest
    {
        private const double Tolerance = 1e-9;

        private static void AssertStep(double remainder, int steps, WheelStepResult actual)
        {
            Assert.AreEqual(remainder, actual.Remainder, Tolerance);
            Assert.AreEqual(steps, actual.Steps);
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/equipmentLogic.test.ts">下方向で0→1→2→0と循環し未装備には落ちない</web-test>
        [Test]
        public void 下方向で循環し未装備には落ちない()
        {
            Assert.AreEqual(1, EquipmentSelectionLogic.CycleEquipment(0, 1, 3));
            Assert.AreEqual(0, EquipmentSelectionLogic.CycleEquipment(2, 1, 3));
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/equipmentLogic.test.ts">上方向で逆順に循環する</web-test>
        [Test]
        public void 上方向で逆順に循環する()
        {
            Assert.AreEqual(2, EquipmentSelectionLogic.CycleEquipment(0, -1, 3));
            Assert.AreEqual(1, EquipmentSelectionLogic.CycleEquipment(2, -1, 3));
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/equipmentLogic.test.ts">スロット数が変わっても周期が追随し1枠では動かない</web-test>
        [Test]
        public void スロット数が変わっても周期が追随し1枠では動かない()
        {
            Assert.AreEqual(0, EquipmentSelectionLogic.CycleEquipment(0, 1, 1));
            Assert.AreEqual(0, EquipmentSelectionLogic.CycleEquipment(0, -1, 1));
            Assert.AreEqual(0, EquipmentSelectionLogic.CycleEquipment(4, 1, 5));
            Assert.AreEqual(4, EquipmentSelectionLogic.CycleEquipment(0, -1, 5));
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/equipmentLogic.test.ts">周期を超える delta も1周として畳み込む</web-test>
        [Test]
        public void 周期を超えるdeltaも1周として畳み込む()
        {
            Assert.AreEqual(0, EquipmentSelectionLogic.CycleEquipment(0, 3, 3));
            Assert.AreEqual(0, EquipmentSelectionLogic.CycleEquipment(0, -3, 3));
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/equipmentLogic.test.ts">小さい入力は累積し閾値を越えるまで切り替えない</web-test>
        [Test]
        public void 小さい入力は累積し閾値を越えるまで切り替えない()
        {
            AssertStep(0.4, 0, EquipmentSelectionLogic.AccumulateWheelSteps(0, 40, WheelDeltaUnit.Pixel));
            AssertStep(0, 1, EquipmentSelectionLogic.AccumulateWheelSteps(0.4, 70, WheelDeltaUnit.Pixel));
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/equipmentLogic.test.ts">標準1ノッチ(±100)でちょうど1段切り替わる</web-test>
        [Test]
        public void 標準の1ノッチでちょうど1段切り替わる()
        {
            AssertStep(0, 1, EquipmentSelectionLogic.AccumulateWheelSteps(0, 100, WheelDeltaUnit.Pixel));
            AssertStep(0, -1, EquipmentSelectionLogic.AccumulateWheelSteps(0, -100, WheelDeltaUnit.Pixel));
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/equipmentLogic.test.ts">スクロール加速で膨らんだ1ノッチでも1段しか進まない</web-test>
        [Test]
        public void スクロール加速で膨らんだ1ノッチでも1段しか進まない()
        {
            AssertStep(0, 1, EquipmentSelectionLogic.AccumulateWheelSteps(0, 300, WheelDeltaUnit.Pixel));
            AssertStep(0, -1, EquipmentSelectionLogic.AccumulateWheelSteps(0, -250, WheelDeltaUnit.Pixel));
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/equipmentLogic.test.ts">逆回転では順方向の端数を捨ててから判定する</web-test>
        [Test]
        public void 逆回転では順方向の端数を捨ててから判定する()
        {
            AssertStep(-0.2, 0, EquipmentSelectionLogic.AccumulateWheelSteps(0.9, -20, WheelDeltaUnit.Pixel));
            AssertStep(0, -1, EquipmentSelectionLogic.AccumulateWheelSteps(0.9, -100, WheelDeltaUnit.Pixel));
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/equipmentLogic.test.ts">line/page単位のdeltaModeでも1ノッチが1段になる</web-test>
        [Test]
        public void 行単位とページ単位でも1ノッチが1段になる()
        {
            AssertStep(0, 1, EquipmentSelectionLogic.AccumulateWheelSteps(0, 3, WheelDeltaUnit.Line));
            AssertStep(0, -1, EquipmentSelectionLogic.AccumulateWheelSteps(0, -1, WheelDeltaUnit.Page));
        }
    }
}
