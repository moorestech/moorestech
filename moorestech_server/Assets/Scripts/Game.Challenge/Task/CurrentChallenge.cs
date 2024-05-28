using System;
using Game.Context;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using UniRx;

namespace Game.Challenge.Task
{
    public class CurrentChallenge
    {
        public IObservable<CurrentChallenge> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<CurrentChallenge> _onChallengeComplete = new();

        public readonly ChallengeInfo Config;
        public readonly int PlayerId;

        private bool _completed;

        public CurrentChallenge(int playerId, ChallengeInfo config)
        {
            Config = config;
            PlayerId = playerId;

            if (Config.TaskCompletionType == ChallengeInfo.CreateItem)
            {
                var craftEvent = ServerContext.GetService<CraftEvent>();
                craftEvent.OnCraftItem.Subscribe(CreateItem);
            }
        }

        public void ManualUpdate()
        {
            if (_completed)
            {
                return;
            }
            switch (Config.TaskCompletionType)
            {
                case ChallengeInfo.InInventoryItem:
                    InInventoryItem();
                    break;
            }
        }

        private void CreateItem(CraftingConfigInfo configInfo)
        {
            var param = (CreateItemTaskParam)Config.TaskParam;

            if (configInfo.ResultItem.Id == param.ItemId)
            {
                _onChallengeComplete.OnNext(this);
            }
        }

        private IPlayerInventoryDataStore _playerInventoryDataStore;
        private PlayerInventoryData _playerInventory;
        private InInventoryItemTaskParam _inInventoryItemTaskParam;

        private void InInventoryItem()
        {
            _inInventoryItemTaskParam ??= (InInventoryItemTaskParam)Config.TaskParam;
            _playerInventoryDataStore ??= ServerContext.GetService<IPlayerInventoryDataStore>();

            if (_playerInventoryDataStore != null)
            {
                _playerInventory = _playerInventoryDataStore.GetInventoryData(PlayerId);
            }
            if (_playerInventory == null) return;

            var itemCount = 0;
            foreach (var item in _playerInventory.MainOpenableInventory.Items)
            {
                if (item.Id != _inInventoryItemTaskParam.ItemId) continue;

                itemCount += item.Count;
                if (itemCount < _inInventoryItemTaskParam.Count) continue;

                _onChallengeComplete.OnNext(this);
                _completed = true;
                break;
            }
        }
    }
}