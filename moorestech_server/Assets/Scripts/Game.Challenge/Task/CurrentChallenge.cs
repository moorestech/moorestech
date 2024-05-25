using System;
using Game.Crafting.Interface;
using UniRx;

namespace Game.Challenge.Task
{
    public class CurrentChallenge
    {
        public IObservable<CurrentChallenge> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<CurrentChallenge> _onChallengeComplete = new();

        public readonly ChallengeInfo Config;
        public readonly int PlayerId;

        public CurrentChallenge(int playerId, CraftEvent craftEvent, ChallengeInfo config)
        {
            Config = config;
            PlayerId = playerId;

            if (Config.TaskCompletionType == ChallengeInfo.CreateItem)
            {
                craftEvent.OnCraftItem.Subscribe(CreateItem);
            }
        }

        public void ManualUpdate()
        {
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

        private void InInventoryItem()
        {
        }
    }
}