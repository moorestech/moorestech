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

        [SerializeField] private RectTransform allowParent;
        [SerializeField] private PrerequisiteQuestsAllow prerequisiteQuestsAllow;
        public void SetQuests(List<QuestConfigData> questConfigs)
        {
            foreach (var questConfig in questConfigs)
            {
                //クエストの追加
                var questElement = Instantiate(questElementPrefab, transform);
                questElement.SetQuest(questConfig);
                
                //前提クエストの矢印設定
                foreach (var prerequisite in questConfig.PrerequisiteQuests)
                {
                    var allow = Instantiate(prerequisiteQuestsAllow, allowParent);
                    allow.SetAllow(prerequisite.UiPosition,questConfig.UiPosition);
                }
            }
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}