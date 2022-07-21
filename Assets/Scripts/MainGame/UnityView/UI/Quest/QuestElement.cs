using Core.Item.Config;
using Game.Quest.Interface;
using MainGame.UnityView.UI.Inventory.Element;
using TMPro;
using UnityEngine;

namespace MainGame.UnityView.UI.Quest
{
    public class QuestElement : MonoBehaviour
    {
        [SerializeField] private TMP_Text itemName;

        public void SetQuest(QuestConfigData questConfigData)
        {
            //もうちょっとちゃんとしたUI設定を行うようにする
            var rectTransform = GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(questConfigData.UiPosition.X, questConfigData.UiPosition.Y);
            itemName.name = questConfigData.QuestName;
        }
    }
}