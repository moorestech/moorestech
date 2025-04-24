using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public class UIHighlightTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        [SerializeField] private UIHighlightTutorialView highlightTutorialViewPrefab;
        [SerializeField] private RectTransform highlightParent;
        
        public ITutorialView ApplyTutorial(ITutorialParam param)
        {
            var highlightParam = (UiHighLightTutorialParam)param;
            
            var objectId = highlightParam.HighLightUIObjectId;
            var text = highlightParam.HighLightText;
            return SetHighLightTargetObject(highlightTutorialViewPrefab, highlightParent, objectId, text, false);
        }
        
        public static ITutorialView SetHighLightTargetObject(UIHighlightTutorialView prefab, Transform parent, string targetId, string setText, bool forceCreate)
        {
            if (forceCreate)
            {
                var highlightView = Instantiate(prefab, parent);
                highlightView.SetTargetObject(null, targetId, setText);
                return highlightView;
            }
            
            var highlightTargetObjects = FindObjectsOfType<UIHighlightTutorialTargetObject>(true);
            foreach (var targetObject in highlightTargetObjects)
            {
                if (targetObject.HighlightObjectId != targetId) continue;
                
                var highlightView = Instantiate(prefab, parent);
                highlightView.SetTargetObject(targetObject, targetId, setText);
                return highlightView;
            }
            
            Debug.LogError($"ハイライトのターゲットが見つかりませんでした: {targetId}\n本文：{setText}");
            return null;
        }
    }
}