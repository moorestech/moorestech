using System;
using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Core.Master;
using Game.Context;

namespace Client.Game.InGame.Tutorial
{
    public class TutorialManager
    {
        private readonly Dictionary<Guid, List<ITutorialView>> _tutorialViews = new();
        private readonly Dictionary<string, ITutorialViewManager> _tutorialViewManagers = new();
        
        public TutorialManager(MapObjectPin mapObjectPin, UIHighlightTutorialManager uiHighlightTutorialManager, KeyControlTutorialManager keyControlTutorialManager)
        {
            _tutorialViewManagers.Add(MapObjectPin.TutorialType, mapObjectPin);
            _tutorialViewManagers.Add(UIHighlightTutorialManager.TutorialType, uiHighlightTutorialManager);
            _tutorialViewManagers.Add(KeyControlTutorialManager.TutorialType, keyControlTutorialManager);
        }
        
        public void ApplyTutorial(Guid challengeGuid)
        {
            var tutorialViews = new List<ITutorialView>();
            var challenge = MasterHolder.ChallengeMaster.GetChallenge(challengeGuid);
            
            // チュートリアルを実際のManagerに適用する
            // Apply the tutorial to the actual Manager
            foreach (var tutorial in challenge.Tutorials)
            {
                var tutorialView = _tutorialViewManagers[tutorial.TutorialType].ApplyTutorial(tutorial.TutorialParam);
                
                if (tutorialView != null) tutorialViews.Add(tutorialView);
            }
            
            _tutorialViews.Add(challengeGuid, tutorialViews);
        }
        
        public void CompleteChallenge(Guid challengeId)
        {
            if (!_tutorialViews.TryGetValue(challengeId, out var tutorialViews)) return;
            
            foreach (var tutorialView in tutorialViews)
            {
                tutorialView.CompleteTutorial();
            }
        }
    }
}