using System;
using Game.Context;
using Game.PlayerInventory.Interface;
using UniRx;

namespace Game.Challenge.Task
{
    public class InInventoryItemChallengeTask : IChallengeTask
    {
        public ChallengeInfo Config { get; }
        public int PlayerId { get; }
        
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();
        
        private bool _completed;
        
        private readonly InInventoryItemTaskParam _inInventoryItemTaskParam;
        private readonly PlayerInventoryData _playerInventory;
        
        public static IChallengeTask Create(int playerId, ChallengeInfo config)
        {
            return new InInventoryItemChallengeTask(playerId, config);
        }
        public InInventoryItemChallengeTask(int playerId, ChallengeInfo config)
        {
            Config = config;
            PlayerId = playerId;
            
            _inInventoryItemTaskParam = (InInventoryItemTaskParam)Config.TaskParam;
            _playerInventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
        }
        
        public void ManualUpdate()
        {
            if (_completed) return;
            
            var itemCount = 0;
            foreach (var item in _playerInventory.MainOpenableInventory.InventoryItems)
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