using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Inventory.Equipment
{
    public class EquipmentWheelSelectorTest
    {
        private FakeEquipmentSelectionPort _port;
        private EquipmentWheelSelector _selector;

        [SetUp]
        public void SetUp()
        {
            _port = new FakeEquipmentSelectionPort(3);
            _selector = new EquipmentWheelSelector(_port);
            _selector.Initialize();
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">装備枠が1つのときはホイールで select_equipment を送らない</web-test>
        [Test]
        public void 装備枠が1つならホイールで選択しない()
        {
            _port.SetSlotCount(1);
            _selector.Wheel(1, WheelDeltaUnit.Page);
            Assert.IsEmpty(_port.Selections);
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">topic反映前の高速2ノッチはpending値から1→2へ進む</web-test>
        [Test]
        public void エコー前の2ノッチは未確認の値から1と2へ進む()
        {
            _selector.Wheel(1, WheelDeltaUnit.Page);
            _selector.Wheel(1, WheelDeltaUnit.Page);
            CollectionAssert.AreEqual(new[] { 1, 2 }, _port.Selections);
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">楽観topicと古いechoが来ても未確認の末尾から次ノッチを計算する</web-test>
        [Test]
        public void 古いエコーで選択が戻っても未確認の末尾から次を計算する()
        {
            _selector.Wheel(1, WheelDeltaUnit.Page);
            _selector.Wheel(1, WheelDeltaUnit.Page);

            // 1 への要求のエコーが届き、手元の選択が 1 へ巻き戻る（2 はまだ未確認）
            // The echo for the request to 1 arrives and rewinds the local selection to 1 (2 is still unconfirmed)
            _port.ConfirmFromServer(1);
            _selector.Wheel(1, WheelDeltaUnit.Page);

            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, _port.Selections);
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">加速で膨らんだ1イベントでも1段しか進まない</web-test>
        [Test]
        public void 大きな1回のホイールでも1段だけ進む()
        {
            _selector.Wheel(5, WheelDeltaUnit.Page);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        [Test]
        public void 未確認が全部確定した後はサーバーの確定値から次を計算する()
        {
            _selector.Wheel(1, WheelDeltaUnit.Page);
            _port.ConfirmFromServer(1);

            // 未確認が空なので、サーバーが別の値（0）を確定させたらそこから進む
            // With nothing unconfirmed, a different server-confirmed value (0) becomes the origin
            _port.ConfirmFromServer(0);
            _selector.Wheel(1, WheelDeltaUnit.Page);

            CollectionAssert.AreEqual(new[] { 1, 1 }, _port.Selections);
        }
    }
}
