using System;
using Game.PlayerInventory.Interface;
using UniRx;

namespace Game.PlayerInventory
{
    public class PlayerInventorySlotLevelDataStore : IPlayerInventorySlotLevelDataStore
    {
        public int CurrentLevel => _currentLevel;
        public int CurrentSlotCount => PlayerInventorySlotLevelMasterUtil.GetSlotCount(_currentLevel);
        public IObservable<int> OnSlotCountChanged => _onSlotCountChanged;

        private readonly Subject<int> _onSlotCountChanged = new();
        private int _currentLevel;

        public void UnlockLevel(int level)
        {
            // レベルは下がらない冪等操作。範囲外は最大レベルへクランプ
            // Idempotent unlock; the level never decreases and clamps to the max defined level
            var clamped = Math.Clamp(level, 0, PlayerInventorySlotLevelMasterUtil.GetMaxLevel());
            if (clamped <= _currentLevel) return;

            SetLevel(clamped);
        }

        public void LoadLevel(int level)
        {
            var clamped = Math.Clamp(level, 0, PlayerInventorySlotLevelMasterUtil.GetMaxLevel());
            if (clamped == _currentLevel) return;

            SetLevel(clamped);
        }

        public int GetSaveLevel()
        {
            return _currentLevel;
        }

        private void SetLevel(int level)
        {
            // スロット数が実際に変わったときだけイベント発行する
            // Publish the event only when the slot count actually changes
            var beforeSlotCount = CurrentSlotCount;
            _currentLevel = level;
            if (CurrentSlotCount != beforeSlotCount) _onSlotCountChanged.OnNext(CurrentSlotCount);
        }
    }
}
