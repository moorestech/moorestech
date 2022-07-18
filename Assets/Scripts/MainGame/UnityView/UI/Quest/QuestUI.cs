using System.Collections.Generic;
using Game.Quest.Config;
using Game.Quest.Interface;
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


        [Inject]
        public void Construct(IQuestConfig questConfig)
        {
            foreach (var quests in questConfig.GetQuestListEachCategory())
            {
                var questButton = Instantiate(QuestCategoryButtonPrefab, QuestCategoryButtonContainer);
                questButton.SetCategory(quests.Key);
                
                var questTab = Instantiate(QuestTabPrefab, QuestTabParent);
                questTab.SetQuests(quests.Value);
            }
        }
    }
}