using System;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Mooresmaster.Model.ChallengesModule;
using UniRx;

namespace Game.Challenge.Task
{
    public class InInventoryItemChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();
        
        private bool _completed;
        
        private readonly InInventoryItemTaskParam _inInventoryItemTaskParam;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        public static IChallengeTask Create(ChallengeMasterElement challengeMasterElement)
        {
            return new InInventoryItemChallengeTask(challengeMasterElement);
        }
        private InInventoryItemChallengeTask(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;
            
            _inInventoryItemTaskParam = (InInventoryItemTaskParam)challengeMasterElement.TaskParam;
            _playerInventoryDataStore = ServerContext.GetService<IPlayerInventoryDataStore>();
        }
        
        public void ManualUpdate()
        {
            if (_completed) return;
            
            // ワールド共通チャレンジなので全プレイヤーの所持数を合算する
            // A world-wide challenge, so sum the held count over all players
            var taskItemId = MasterHolder.ItemMaster.GetItemId(_inInventoryItemTaskParam.ItemGuid);
            var itemCount = 0;
            foreach (var playerId in _playerInventoryDataStore.GetAllPlayerId())
            {
                foreach (var item in _playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory.InventoryItems)
                {
                    if (item.Id != taskItemId) continue;

                    itemCount += item.Count;
                    if (itemCount < _inInventoryItemTaskParam.ItemCount) continue;

                    // 達成通知を重ねないよう到達した時点で抜ける
                    // Return on reaching the goal so completion is notified only once
                    _completed = true;
                    _onChallengeComplete.OnNext(this);
                    return;
                }
            }
        }
    }
}