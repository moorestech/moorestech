using System;
using Core.Master;
using Game.Context;
using Game.Crafting.Interface;
using Mooresmaster.Model.ChallengesModule;
using Mooresmaster.Model.CraftRecipesModule;
using UniRx;

namespace Game.Challenge.Task
{
    public class CreateItemChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public int PlayerId { get; }
        
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();
        
        private bool _completed;
        
        
        public static IChallengeTask Create(int playerId, ChallengeMasterElement challengeMasterElement)
        {
            return new CreateItemChallengeTask(playerId, challengeMasterElement);
        }
        public CreateItemChallengeTask(int playerId, ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;
            PlayerId = playerId;
            
            var craftEvent = ServerContext.GetService<CraftEvent>();
            craftEvent.OnCraftItem.Subscribe(CreateItem);
        }
        
        private void CreateItem(CraftRecipeMasterElement craftRecipeMasterElement)
        {
            if (_completed) return;
            
            var param = ChallengeMasterElement.TaskParam as CreateItemTaskParam;
            
            if (craftRecipeMasterElement.ResultItem.ItemGuid == param.ItemGuid)
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