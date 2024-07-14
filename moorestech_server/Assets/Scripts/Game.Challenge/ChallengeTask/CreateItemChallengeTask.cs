using System;
using Game.Context;
using Game.Crafting.Interface;
using UniRx;

namespace Game.Challenge.Task
{
    public class CreateItemChallengeTask : IChallengeTask
    {
        public ChallengeInfo Config { get; }
        public int PlayerId { get; }
        
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();
        
        private bool _completed;
        
        
        public static IChallengeTask Create(int playerId, ChallengeInfo config)
        {
            return new CreateItemChallengeTask(playerId, config);
        }
        public CreateItemChallengeTask(int playerId, ChallengeInfo config)
        {
            Config = config;
            PlayerId = playerId;
            
            var craftEvent = ServerContext.GetService<CraftEvent>();
            craftEvent.OnCraftItem.Subscribe(CreateItem);
        }
        
        private void CreateItem(CraftingConfigInfo configInfo)
        {
            if (_completed) return;
            
            var param = (CreateItemTaskParam)Config.TaskParam;
            
            if (configInfo.ResultItem.Id == param.ItemId)
            {
                _completed = true;
                _onChallengeComplete.OnNext(this);
            }
        }
        
        public void ManualUpdate()
        {
            
        }
    }
}