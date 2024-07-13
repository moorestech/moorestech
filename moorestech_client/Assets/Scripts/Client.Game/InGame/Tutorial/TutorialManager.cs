using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Game.Challenge.Config.TutorialParam;
using Game.Context;

namespace Client.Game.InGame.Tutorial
{
    public class TutorialManager
    {
        private readonly Dictionary<int,List<ITutorialView>> _tutorialViews = new(); 
        
        private readonly Dictionary<string,ITutorialViewManager> _tutorialViewManagers = new();
        
        public TutorialManager(MapObjectPin mapObjectPin, UIHighlightManager uiHighlightManager)
        {
            _tutorialViewManagers.Add(MapObjectPinTutorialParam.TaskCompletionType, mapObjectPin);
            _tutorialViewManagers.Add(UIHighLightTutorialParam.TaskCompletionType, uiHighlightManager);
        }
        
        public void ApplyTutorial(int challengeId)
        {
            var tutorialViews = new List<ITutorialView>();
            var challenge = ServerContext.ChallengeConfig.GetChallenge(challengeId);
            
            // チュートリアルを実際のManagerに適用する
            // Apply the tutorial to the actual Manager
            foreach (var tutorial in challenge.Tutorials)
            {
                var tutorialView = _tutorialViewManagers[tutorial.TutorialType].ApplyTutorial(tutorial.Param);
                
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