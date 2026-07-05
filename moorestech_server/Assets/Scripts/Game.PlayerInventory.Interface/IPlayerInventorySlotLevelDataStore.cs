using System;

namespace Game.PlayerInventory.Interface
{
    /// <summary>
    ///     スロットレベルをワールド共通保持
    ///     Holds slot level globally
    /// </summary>
    public interface IPlayerInventorySlotLevelDataStore
    {
        int CurrentLevel { get; }
        int CurrentSlotCount { get; }

        /// <summary>スロット数が実際に増えたときのみ発行。流れる値は新しいスロット数</summary>
        IObservable<int> OnSlotCountChanged { get; }

        void UnlockLevel(int level);
        void LoadLevel(int level);
        int GetSaveLevel();
    }
}
