using System.Collections.Generic;
using Game.Quest.Config;
using Game.Quest.Interface;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Quest
{
    public class QuestUI : MonoBehaviour
    {
        [SerializeField] private QuestTab m_QuestTabPrefab;
        [SerializeField] private QuestCategoryButton m_QuestCategory;


        [Inject]
        public void Construct(IQuestConfig questConfig)
        {
        }
    }
}