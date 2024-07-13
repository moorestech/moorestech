using Game.Challenge;
using Game.Challenge.Config.TutorialParam;
using UnityEngine;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public class UIHighlightManager : MonoBehaviour, ITutorialViewManager
    {
        [SerializeField] private UIHighlightView highlightViewPrefab;
        
        public ITutorialView ApplyTutorial(ITutorialParam param)
        {
            var highlightParam = (UIHighLightTutorialParam)param;
           
            var highlightTargetObjects =  FindObjectsOfType<UIHighlightTargetObject>();
            foreach (var targetObject in highlightTargetObjects)
            {
                if (targetObject.HighlightObjectId != highlightParam.HighLightUIObjectId) continue;
                
                var highlightView = Instantiate(highlightViewPrefab, transform);
                highlightView.SetTargetObject(targetObject, highlightParam.HighLightText);
                return highlightView;
            }
            
            return null;
        }
    }
}