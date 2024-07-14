using Game.Challenge;
using Game.Challenge.Config.TutorialParam;
using UnityEngine;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public class UIHighlightTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        [SerializeField] private UIHighlightTutorialView highlightTutorialViewPrefab;
        [SerializeField] private RectTransform highlightParent;
        
        public ITutorialView ApplyTutorial(ITutorialParam param)
        {
            var highlightParam = (UIHighLightTutorialParam)param;
            
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