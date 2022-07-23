using System;
using Game.Quest.Interface;
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
        

        private void Start()
        {
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }


        public void SetQuest(QuestConfigData questConfigData,ItemImages itemImages)
        {
            gameObject.SetActive(true);
            
            title.text = questConfigData.QuestName;
            description.text = questConfigData.QuestDescription;

            foreach (var rewardItem in questConfigData.RewardItemStacks)
            {
                var rewardItemElement = Instantiate(rewardItemElementPrefab, rewardIteParent);
                rewardItemElement.SetItem(rewardItem,itemImages);
            }
        }
    }
}