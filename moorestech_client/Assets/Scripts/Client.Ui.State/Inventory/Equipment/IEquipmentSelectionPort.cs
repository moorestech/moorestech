using System;
using UniRx;

namespace Client.Ui.State.Inventory.Equipment
{
    /// <summary>
    /// 装備の選択の口（楽観で選び送る）
    /// Equipment selection port (select optimistically and send)
    /// </summary>
    public interface IEquipmentSelectionPort
    {
        int SlotCount { get; }
        int SelectedIndex { get; }
        int ConfirmationRevision { get; }
        IObservable<Unit> OnSelectionChanged { get; }
        void Select(int index);
    }
}
