using System;
using UniRx;

namespace Client.Ui.State.Inventory.Equipment
{
    /// <summary>
    /// 装備の選択の口。Select は楽観で選択を変えて送り、確定の世代はサーバーのエコーだけが進める
    /// Port for equipment selection; Select changes optimistically and sends, and only server echoes advance the confirmation revision
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
