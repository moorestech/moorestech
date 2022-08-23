using System;
using System.Collections.Generic;
using Game.Quest.Interface;
using MainGame.Basic.Quest;
using MainGame.UnityView.UI.Inventory.Element;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MainGame.UnityView.UI.Quest.QuestDetail
{
    public class QuestDetailUI : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        
        [SerializeField] private RectTransform rewardIteParent;
        [SerializeField] private QuestRewardItemElement rewardItemElementPrefab;

        [SerializeField] private Button getRewardButton;

        /// <summary>
        /// クエストUIまでイベントを伝えるためのイベント
        /// </summary>
        public event Action<string> OnGetReward; 
        private readonly List<QuestRewardItemElement> _questRewardItemElements = new();
        
        private string _questId;
        


        private void Start()
        {
            getRewardButton.onClick.AddListener(() =>
            {
                OnGetReward?.Invoke(_questId);
                getRewardButton.gameObject.SetActive(false);
            });
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }


        public void SetQuest(QuestConfigData config,QuestProgressData questProgressData ,ItemImages itemImages)
        {
            gameObject.SetActive(true);
            
            _questId = config.QuestId;
            title.text = config.QuestName;
            description.text = config.QuestDescription;
            
            getRewardButton.gameObject.SetActive(questProgressData.IsRewardEarnbable);


            //今までのリワードアイテム表示を削除
            foreach (var questReward in _questRewardItemElements)
            {
                Destroy(questReward.gameObject);
            }
            //リワードアイテムを再生成
            _questRewardItemElements.Clear();
            foreach (var rewardItem in config.RewardItemStacks)
            {
                var rewardItemElement = Instantiate(rewardItemElementPrefab, rewardIteParent);
                rewardItemElement.SetItem(rewardItem,itemImages);
                _questRewardItemElements.Add(rewardItemElement);
            }
        }
    }
}