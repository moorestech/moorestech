using System;
using System.Collections.Generic;
using Client.Ui.State.Inventory.Equipment;
using UniRx;

namespace Client.Tests.Ui.State.Inventory.Equipment
{
    public sealed class FakeEquipmentSelectionPort : IEquipmentSelectionPort
    {
        private readonly Subject<Unit> _changed = new();

        public int SlotCount { get; private set; }
        public int SelectedIndex { get; private set; }
        public int ConfirmationRevision { get; private set; }
        public IObservable<Unit> OnSelectionChanged => _changed;
        public List<int> Selections { get; } = new();

        public FakeEquipmentSelectionPort(int slotCount)
        {
            SlotCount = slotCount;
        }

        public void Select(int index)
        {
            Selections.Add(index);
            SelectedIndex = index;
            _changed.OnNext(Unit.Default);
        }

        // サーバーのエコー（ApplySelected 相当）
        // Server echo (ApplySelected equivalent)
        public void ConfirmFromServer(int index)
        {
            SelectedIndex = index;
            ConfirmationRevision++;
            _changed.OnNext(Unit.Default);
        }

        public void SetSlotCount(int slotCount)
        {
            SlotCount = slotCount;
            _changed.OnNext(Unit.Default);
        }
    }
}
