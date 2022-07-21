using System.Collections.Generic;
using Core.Item.Config;
using Game.Quest.Interface;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;

namespace MainGame.UnityView.UI.Quest
{
    public class QuestTab : MonoBehaviour
    {
        [SerializeField] private QuestElement questElementPrefab;
        public void SetQuests(List<QuestConfigData> questConfigs)
        {
            foreach (var questConfig in questConfigs)
            {
                var questElement = Instantiate(questElementPrefab, transform);
                questElement.SetQuest(questConfig);
            }
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}