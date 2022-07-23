using System;
using Core.Item.Config;
using Game.Quest.Interface;
using MainGame.UnityView.UI.Inventory.Element;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Quest
{
    public class QuestElement : MonoBehaviour
    {
        [SerializeField] private TMP_Text itemName;
        [SerializeField] private Button questButton;

        public void SetQuest(QuestConfigData questConfigData,Action<QuestConfigData> onClick)
        {
            //もうちょっとちゃんとしたUI設定を行うようにする
            var rectTransform = GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(questConfigData.UiPosition.X, questConfigData.UiPosition.Y);
            itemName.text = questConfigData.QuestName;
            
            gameObject.name = questConfigData.QuestId;
            
            questButton.onClick.AddListener(() =>
            {
                onClick?.Invoke(questConfigData);
            });
        }
    }
}