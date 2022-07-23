using System.Collections.Generic;
using Game.Quest.Config;
using Game.Quest.Interface;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Quest
{
    public class QuestUI : MonoBehaviour
    {
        [SerializeField] private QuestCategoryButton QuestCategoryButtonPrefab;
        [SerializeField] private QuestTab QuestTabPrefab;
        
        [SerializeField] private RectTransform QuestCategoryButtonContainer;
        [SerializeField] private RectTransform QuestTabParent;

        private readonly Dictionary<string, QuestTab> questTabs = new();
        private IQuestConfig _questConfig;


        [Inject]
        public void Construct(IQuestConfig questConfig,ItemImages itemImages)
        {
            _questConfig = questConfig;
            
            string firstCategory = null;
            foreach (var quests in questConfig.GetQuestListEachCategory())
            {
                firstCategory ??= quests.Key;
                
                var questButton = Instantiate(QuestCategoryButtonPrefab, QuestCategoryButtonContainer);
                questButton.SetCategory(quests.Key,OnPushQuestButton);
                questButton.name = quests.Key + " Button";
                
                var questTab = Instantiate(QuestTabPrefab, QuestTabParent);
                questTab.SetQuests(quests.Value,itemImages);
                questTab.SetActive(false);
                questTab.name = quests.Key + " Tab";
                
                questTabs.Add(quests.Key, questTab);
            }

            if (firstCategory != null)
            {
                questTabs[firstCategory].SetActive(true);   
            }
        }

        private void OnPushQuestButton(string category)
        {
            foreach (var tab in questTabs.Values)
            {
                tab.SetActive(false);
            }
            questTabs[category].SetActive(true);
        }


        /// <summary>
        /// クエストIDごとの進捗を設定する
        /// </summary>
        /// <param name="questProgress"></param>
        public void SetQuestProgress(Dictionary<string,(bool IsCompleted,bool IsRewarded)> questProgress)
        {
            foreach (var quest in questProgress)
            {
                //カテゴリを取得
                var cat = _questConfig.GetQuestConfig(quest.Key).QuestCategory;
                //クエストタブに進捗を設定
                questTabs[cat].SetQuestProgress(quest.Key,quest.Value.IsCompleted,quest.Value.IsRewarded);
            }
        }
        
    }
}