using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Routing;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Inventory.Equipment
{
    public class EquipmentWheelGateTest
    {
        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">ホイール占有中はホイールで装備が切り替わらない</web-test>
        [Test]
        public void ツールがホイールを占有中は止める()
        {
            Assert.AreEqual(EquipmentWheelBlock.Tool, EquipmentWheelGate.Judge(new EquipmentWheelSituation(UiScreen.None, false, false, true)));
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">ホイールを読まない接続ツール中は抑止されず装備が切り替わる</web-test>
        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">BPコピー選択中でもドラッグしていなければ装備が切り替わる</web-test>
        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">block選択中は抑止されずホイールで装備が切り替わる</web-test>
        [Test]
        public void 占有していない配置ツール中は止めない()
        {
            // 接続ツール・ドラッグしていない BP コピー・ブロックの選択はどれも占有を偽にする（判定は種別を見ない）
            // The connect tool, an undragged blueprint copy and block selection all leave ownership false (the gate never looks at the kind)
            Assert.AreEqual(EquipmentWheelBlock.None, EquipmentWheelGate.Judge(new EquipmentWheelSituation(UiScreen.None, false, false, false)));
        }

        [TestCase(UiScreen.SubInventory)]
        [TestCase(UiScreen.ResearchTree)]
        [TestCase(UiScreen.BuildMenu)]
        public void 前面の層がゲームでない画面は止める(UiScreen screen)
        {
            Assert.AreEqual(EquipmentWheelBlock.Layer, EquipmentWheelGate.Judge(new EquipmentWheelSituation(screen, false, false, false)));
        }

        [Test]
        public void モーダルの保留中は止める()
        {
            Assert.AreEqual(EquipmentWheelBlock.Layer, EquipmentWheelGate.Judge(new EquipmentWheelSituation(UiScreen.PlayerInventory, true, false, false)));
        }

        [Test]
        public void 素通しでないUIの上は止める()
        {
            Assert.AreEqual(EquipmentWheelBlock.Ui, EquipmentWheelGate.Judge(new EquipmentWheelSituation(UiScreen.PlayerInventory, false, true, false)));
        }
    }
}
