using System;
using Core.Master;
using Game.Context;
using Game.Crafting.Interface;
using Mooresmaster.Model.ChallengesModule;
using UniRx;

namespace Game.Challenge.Task
{
    public class CreateItemChallengeTask : IChallengeTask
    {
        public ChallengeElement ChallengeElement { get; }
        public int PlayerId { get; }
        
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();
        
        private bool _completed;
        
        
        public static IChallengeTask Create(int playerId, ChallengeElement challengeElement)
        {
            return new CreateItemChallengeTask(playerId, challengeElement);
        }
        public CreateItemChallengeTask(int playerId, ChallengeElement challengeElement)
        {
            ChallengeElement = challengeElement;
            PlayerId = playerId;
            
            var craftEvent = ServerContext.GetService<CraftEvent>();
            craftEvent.OnCraftItem.Subscribe(CreateItem);
        }
        
        private void CreateItem(CraftingConfigInfo configInfo)
        {
            if (_completed) return;
            
            var param = ChallengeElement.TaskParam as CreateItemTaskParam;
            var challengeItemId = ItemMaster.GetItemId(param.ItemGuid);
            
            if (configInfo.ResultItem.Id == challengeItemId)
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