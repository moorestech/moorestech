using System;

namespace Game.PlayerInventory.Interface
{
    /// <summary>
    ///     プレイヤーインベントリのスロット数レベルをワールド共通で保持する
    ///     Holds the world-global player inventory slot level
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
