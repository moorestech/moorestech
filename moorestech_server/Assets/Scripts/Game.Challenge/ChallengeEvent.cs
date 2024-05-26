using System;
using Game.Challenge.Task;
using Game.Crafting.Interface;
using UniRx;

namespace Game.Challenge
{
    public class ChallengeEvent
    {
        public IObservable<CurrentChallenge> OnCraftItem => _onCraftItem;
        private readonly Subject<CurrentChallenge> _onCraftItem = new();

        public void InvokeCraftItem(CurrentChallenge craftConfig)
        {
            _onCraftItem.OnNext(craftConfig);
        }
    }
}