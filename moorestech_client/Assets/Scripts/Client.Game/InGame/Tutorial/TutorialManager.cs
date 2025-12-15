using System;
using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.ChallengesModule;

namespace Client.Game.InGame.Tutorial
{
    public class TutorialManager
    {
        private readonly Dictionary<Guid, List<ITutorialView>> _tutorialViews = new();
        private readonly Dictionary<string, ITutorialViewManager> _tutorialViewManagers = new();
        
        public TutorialManager(
            IMapObjectPin mapObjectPin,
            UIHighlightTutorialManager uiHighlightTutorialManager,
            KeyControlTutorialManager keyControlTutorialManager,
            ItemViewHighLightTutorialManager itemViewHighLightTutorialManager,
            BlockPlacePreviewTutorialManager blockPlacePreviewTutorialManager
            )
        {
            _tutorialViewManagers.Add(TutorialsElement.TutorialTypeConst.mapObjectPin, mapObjectPin);
            _tutorialViewManagers.Add(TutorialsElement.TutorialTypeConst.uiHighLight, uiHighlightTutorialManager);
            _tutorialViewManagers.Add(TutorialsElement.TutorialTypeConst.keyControl, keyControlTutorialManager);
            _tutorialViewManagers.Add(TutorialsElement.TutorialTypeConst.itemViewHighLight, itemViewHighLightTutorialManager);
            _tutorialViewManagers.Add(TutorialsElement.TutorialTypeConst.blockPlacePreview, blockPlacePreviewTutorialManager);
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