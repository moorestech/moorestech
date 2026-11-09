using System.Collections.Generic;
using Client.Ui.State.Inventory.Grab;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Ui.State.Inventory.Grab
{
    /// <summary>
    /// 掴みの中身が掴みの流れのたびに読み直され、変わったときだけ流れることを確かめる
    /// Verifies the grab is re-read on each grab notification and published only when it changes
    /// </summary>
    public class PlayerGrabModelTest
    {
        private static readonly ItemId Wood = new(1);

        [Test]
        public void 初期化で今のgrabを読みその後の変化を流す()
        {
            var operations = new RecordingPlayerInventoryOperations(9, 2);
            operations.SetGrab(new SlotItem(Wood, 3));
            var model = new PlayerGrabModel(operations);
            model.Initialize();
            var counts = new List<int>();
            model.Current.Subscribe(grab => counts.Add(grab.Count));

            operations.SetGrab(new SlotItem(Wood, 1));
            operations.SetGrab(new SlotItem(Wood, 1));

            // 同じ値の通知では流さない
            // A notification carrying the same value is not republished
            Assert.AreEqual(new[] { 3, 1 }, counts.ToArray());
        }
    }
}
