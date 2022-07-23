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
        
        [SerializeField] private GameObject questCompleted;
        [SerializeField] private GameObject completedAndNotReadrded;

        private bool _isCompleted;
        private bool _isRewarded;
        
        public void SetQuest(QuestConfigData questConfigData,Action<(QuestConfigData config,bool isCompleted,bool isRewarded)> onClick)
        {
            //もうちょっとちゃんとしたUI設定を行うようにする
            var rectTransform = GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(questConfigData.UiPosition.X, questConfigData.UiPosition.Y);
            itemName.text = questConfigData.QuestName;
            
            gameObject.name = questConfigData.QuestId;
            
            questButton.onClick.AddListener(() =>
            {
                onClick?.Invoke((questConfigData,_isCompleted,_isRewarded));
            });
        }

        public void SetProgress(bool isCompleted, bool isRewarded)
        {
            _isCompleted = isCompleted;
            _isRewarded = isRewarded;
            
            questCompleted.SetActive(isCompleted);
            completedAndNotReadrded.SetActive(isCompleted && !isRewarded);
        }
    }
}