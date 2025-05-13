using System;
using System.Collections.Generic;
using Game.Challenge.Task;
using Mooresmaster.Model.ChallengesModule;
using UniRx;

namespace Game.Challenge
{
    public class ChallengeEvent
    {
        private readonly Subject<CompleteChallengeEventProperty> _onCompleteChallenge = new();
        public IObservable<CompleteChallengeEventProperty> OnCompleteChallenge => _onCompleteChallenge;
        
        public void InvokeCompleteChallenge(IChallengeTask completeTask, List<ChallengeMasterElement> nextChallengeMasterElements, List<string> playedSkitIdsStr)
        {
            _onCompleteChallenge.OnNext(new CompleteChallengeEventProperty(completeTask, nextChallengeMasterElements, playedSkitIdsStr));
        }
        
        public class CompleteChallengeEventProperty
        {
            public IChallengeTask ChallengeTask { get; }
            public List<ChallengeMasterElement> NextChallengeMasterElements { get; }
            public List<string> PlayedSkitIdsStr { get; }

            public CompleteChallengeEventProperty(IChallengeTask challengeTask, List<ChallengeMasterElement> nextChallengeMasterElements, List<string> playedSkitIdsStr)
            {
                ChallengeTask = challengeTask;
                NextChallengeMasterElements = nextChallengeMasterElements;
                PlayedSkitIdsStr = playedSkitIdsStr;
            }
        }
    }
}