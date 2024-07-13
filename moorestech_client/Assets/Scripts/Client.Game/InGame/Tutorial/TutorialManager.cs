using System.Collections.Generic;
using Game.Challenge.Config.TutorialParam;
using Game.Context;

namespace Client.Game.InGame.Tutorial
{
    public class TutorialManager
    {
        private readonly Dictionary<int,List<ITutorialView>> _tutorialViews = new(); 
        
        private readonly MapObjectPin _mapObjectPin;
        
        public TutorialManager(MapObjectPin mapObjectPin)
        {
            _mapObjectPin = mapObjectPin;
        }
        
        public void ApplyTutorial(int challengeId)
        {
            var tutorialViews = new List<ITutorialView>();
            var challenge = ServerContext.ChallengeConfig.GetChallenge(challengeId);
            foreach (var tutorial in challenge.Tutorials)
            {
                ITutorialView tutorialView = null;
                switch (tutorial.TutorialType)
                {
                    case MapObjectPinTutorialParam.TaskCompletionType:
                        tutorialView = _mapObjectPin.ApplyTutorial((MapObjectPinTutorialParam)tutorial.Param);
                        break;
                }
                
                if (tutorialView != null)  tutorialViews.Add(tutorialView);
            }
            
            _tutorialViews.Add(challengeId, tutorialViews);
        }
        
        public void CompleteChallenge(int challengeId)
        {
            if (!_tutorialViews.TryGetValue(challengeId, out var tutorialViews)) return;
            
            foreach (var tutorialView in tutorialViews)
            {
                tutorialView.CompleteTutorial();
            }
        }
    }
}