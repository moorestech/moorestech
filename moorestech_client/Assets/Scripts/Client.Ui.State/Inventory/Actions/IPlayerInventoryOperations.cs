using System;
using Client.Ui.State.ItemMove;
using Client.Ui.State.Items;
using UniRx;

namespace Client.Ui.State.Inventory.Actions
{
    /// <summary>
    /// プレイヤーの持ち物（メイン・装備・grab）の読み書きの口。番地は PlayerSlotRef、移動の結果は PlayerItemMoveOutcome
    /// Read/write port for the player's inventory (main, equipment, grab); addresses are PlayerSlotRef, move results PlayerItemMoveOutcome
    /// </summary>
    public interface IPlayerInventoryOperations
    {
        // grab が変わったかもしれない合図（値は ReadGrab で読み直す）
        // Signal that the grab may have changed (re-read with ReadGrab)
        IObservable<Unit> OnGrabChanged { get; }

        // main・装備の中身と装備の選択が変わったかもしれない合図（値は ReadSnapshot で読み直す）
        // Signal that main/equipment contents or the equipment selection may have changed (re-read with ReadSnapshot)
        IObservable<Unit> OnSnapshotChanged { get; }

        PlayerInventorySnapshot ReadSnapshot();
        SlotItem ReadGrab();
        PlayerItemMoveOutcome MoveToGrab(PlayerSlotRef from, int count);
        PlayerItemMoveOutcome MoveFromGrab(PlayerSlotRef to, int count);
        PlayerItemMoveOutcome MoveBetweenSlots(PlayerSlotRef from, PlayerSlotRef to, int count);
        void CollectToGrab();
        void CollectToSlot(PlayerSlotRef slot);
    }
}
