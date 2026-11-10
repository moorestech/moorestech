using System;
using Client.Tests.Ui.State.Inventory;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.Inventory.Panel
{
    public class PlayerInventoryPanelModelTest
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private RecordingPlayerInventoryOperations _operations;
        private FakeEquipmentSelectionPort _equipment;
        private PlayerInventoryPanelModel _model;
        private ItemId _wood;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
            _operations = new RecordingPlayerInventoryOperations(36, 3);
            _equipment = new FakeEquipmentSelectionPort(3);
            _model = new PlayerInventoryPanelModel(_operations, _equipment);
        }

        [Test]
        public void Initializeより前から今の持ち物と装備の選択を持つ()
        {
            // Presenter が先に購読しても null を描かない（前例 PlayerGrabModel・ModeHudModel は ctor で初期値を持つ）
            // Even if a presenter subscribes first it never draws null (precedents PlayerGrabModel and ModeHudModel seed in the ctor)
            var current = _model.Current.Value;
            Assert.AreEqual(36, current.MainSlots.Count);
            Assert.AreEqual(3, current.EquipmentSlots.Count);
            Assert.AreEqual(0, current.SelectedEquipment);
        }

        [Test]
        public void 中身の変化の合図で同期に描き直しの値を配る()
        {
            _model.Initialize();
            _equipment.ConfirmFromServer(2);
            _operations.SetMain(4, new SlotItem(_wood, 7));

            var current = _model.Current.Value;
            Assert.AreEqual(7, current.MainSlots[4].Count);
            Assert.AreEqual(2, current.SelectedEquipment);
            Assert.IsTrue(current.MainSlotItemGuids.ContainsKey(4), "所持品の表も同じ値で組む");
        }
    }
}
