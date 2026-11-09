using Client.Ui.State.Inventory.Logic;
using NUnit.Framework;
using SlotRef = Client.Ui.State.Inventory.Actions.PlayerSlotRef;

namespace Client.Tests.Ui.State.Inventory
{
    public class SplitDragSessionTest
    {
        /// <web-test file="src/features/inventory/splitDrag.test.ts">collects unique slots and sends one host-calculated distribution on release</web-test>
        [Test]
        public void 通ったスロットを重複なしで集め離したとき1回だけ渡す()
        {
            var session = new SplitDragSession();
            session.Begin(SlotRef.Main(1));
            session.Enter(SlotRef.Main(2));
            session.Enter(SlotRef.Main(2));

            CollectionAssert.AreEqual(new[] { SlotRef.Main(1), SlotRef.Main(2) }, session.End());
            CollectionAssert.IsEmpty(session.End());
            Assert.IsFalse(session.IsActive);
        }
    }
}
