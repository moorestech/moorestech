using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public class UIHighlightTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        public const string TutorialType = "uiHighLight";
        
        [SerializeField] private UIHighlightTutorialView highlightTutorialViewPrefab;
        [SerializeField] private RectTransform highlightParent;
        
        public ITutorialView ApplyTutorial(ITutorialParam param)
        {
            var highlightParam = (UiHighLightTutorialParam)param;
            
            var highlightTargetObjects = FindObjectsOfType<UIHighlightTutorialTargetObject>(true);
            foreach (var targetObject in highlightTargetObjects)
            {
                if (targetObject.HighlightObjectId != highlightParam.HighLightUIObjectId) continue;
                
                var highlightView = Instantiate(highlightTutorialViewPrefab, transform);
                highlightView.SetTargetObject(targetObject, highlightParam.HighLightText);
                return highlightView;
            }
            
            return null;
        }
    }
}