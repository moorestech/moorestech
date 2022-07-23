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

        private Dictionary<string, QuestTab> QuestTabs = new();


        [Inject]
        public void Construct(IQuestConfig questConfig,ItemImages itemImages)
        {
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
                
                QuestTabs.Add(quests.Key, questTab);
            }

            if (firstCategory != null)
            {
                QuestTabs[firstCategory].SetActive(true);   
            }
        }

        private void OnPushQuestButton(string category)
        {
            foreach (var tab in QuestTabs.Values)
            {
                tab.SetActive(false);
            }
            QuestTabs[category].SetActive(true);
        }
        
    }
}