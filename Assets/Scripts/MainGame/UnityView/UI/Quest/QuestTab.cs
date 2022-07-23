using System.Collections.Generic;
using Core.Item.Config;
using Game.Quest.Interface;
using MainGame.Basic.Server;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Quest.QuestDetail;
using UnityEngine;

namespace MainGame.UnityView.UI.Quest
{
    public class QuestTab : MonoBehaviour
    {
        [SerializeField] private RectTransform questElementParent;
        [SerializeField] private QuestElement questElementPrefab;

        [SerializeField] private RectTransform allowParent;
        [SerializeField] private PrerequisiteQuestsAllow prerequisiteQuestsAllow;

        [SerializeField] private QuestDetailUI questDetailUI;

        private ItemImages _itemImages;
        
        public void SetQuests(List<QuestConfigData> questConfigs,ItemImages itemImages)
        {
            _itemImages = itemImages;
            foreach (var questConfig in questConfigs)
            {
                //クエストの追加
                var questElement = Instantiate(questElementPrefab, questElementParent);
                questElement.SetQuest(questConfig,SetQuestDetail);
                
                //前提クエストの矢印設定
                foreach (var prerequisite in questConfig.PrerequisiteQuests)
                {
                    var allow = Instantiate(prerequisiteQuestsAllow, allowParent);
                    allow.SetAllow(prerequisite.UiPosition,questConfig.UiPosition);
                }
            }
        }


        private void SetQuestDetail(QuestConfigData questConfigData)
        {
            questDetailUI.SetQuest(questConfigData, _itemImages);
        }
        
        
        public void SetActive(bool active) { gameObject.SetActive(active); }
    }
}