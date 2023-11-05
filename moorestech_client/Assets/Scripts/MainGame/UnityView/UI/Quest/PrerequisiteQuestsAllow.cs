using UnityEngine;

namespace MainGame.UnityView.UI.Quest
{
    public class PrerequisiteQuestsAllow : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;

        public void SetAllow(Vector2 prerequisiteQuestUIPosition, Vector2 questUIPosition)
        {
            rectTransform.anchoredPosition = questUIPosition;
            var differenceVector = questUIPosition - prerequisiteQuestUIPosition;
            var angle = Mathf.Atan2(differenceVector.y, differenceVector.x) * Mathf.Rad2Deg;

            rectTransform.rotation = Quaternion.Euler(0, 0, angle);

            var distance = Vector2.Distance(prerequisiteQuestUIPosition, questUIPosition);
            rectTransform.sizeDelta = new Vector2(distance, rectTransform.sizeDelta.y);
        }
    }
}