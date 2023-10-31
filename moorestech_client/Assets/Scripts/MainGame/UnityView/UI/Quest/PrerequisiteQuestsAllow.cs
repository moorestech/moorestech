using Core.Util;
using MainGame.Basic.Server;
using UnityEngine;

namespace MainGame.UnityView.UI.Quest
{
    public class PrerequisiteQuestsAllow : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        public void SetAllow(CoreVector2 prerequisiteQuestUIPosition,CoreVector2 questUIPosition)
        {
            rectTransform.anchoredPosition = questUIPosition.ToVec2();
            var differenceVector = questUIPosition.ToVec2() - prerequisiteQuestUIPosition.ToVec2();
            var angle = Mathf.Atan2(differenceVector.y, differenceVector.x) * Mathf.Rad2Deg;
            
            rectTransform.rotation = Quaternion.Euler(0, 0, angle);
            
            var distance = Vector2.Distance(prerequisiteQuestUIPosition.ToVec2(), questUIPosition.ToVec2());
            rectTransform.sizeDelta = new Vector2(distance, rectTransform.sizeDelta.y);
        }
    }
}