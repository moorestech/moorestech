using System;
using Client.Game.InGame.UI.Inventory.Equipment;
using UniRx;

namespace Client.Ui.State.Inventory.Equipment
{
    public sealed class LocalPlayerEquipmentSelectionPort : IEquipmentSelectionPort
    {
        private readonly LocalPlayerEquipment _equipment;

        // 別オブジェクトの値の読み替え（自分のフィールドの素通しではない）
        // Reads another object's values (not a pass-through of this class's own fields)
        public int SlotCount => _equipment.Slots.Count;
        public int SelectedIndex => _equipment.SelectedIndex;
        public int ConfirmationRevision => _equipment.SelectionConfirmationRevision;
        public IObservable<Unit> OnSelectionChanged => _equipment.OnSlotsOrSelectionChanged;

        public LocalPlayerEquipmentSelectionPort(LocalPlayerEquipment equipment)
        {
            _equipment = equipment;
        }

        public void Select(int index)
        {
            _equipment.SetSelectedIndex(index);
        }
    }
}
