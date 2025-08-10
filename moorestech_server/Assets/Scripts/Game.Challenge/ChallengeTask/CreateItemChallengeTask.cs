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
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();
        
        private bool _completed;
        
        
        public static IChallengeTask Create(ChallengeMasterElement challengeMasterElement)
        {
            return new CreateItemChallengeTask(challengeMasterElement);
        }
        
        public CreateItemChallengeTask(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;
            
            var craftEvent = ServerContext.GetService<CraftEvent>();
            craftEvent.OnCraftItem.Subscribe(CreateItem);
        }
        
        private void CreateItem(CraftRecipeMasterElement craftRecipeMasterElement)
        {
            if (_completed) return;
            
            var param = ChallengeMasterElement.TaskParam as CreateItemTaskParam;
            
            if (craftRecipeMasterElement.CraftResultItemGuid == param.ItemGuid)
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