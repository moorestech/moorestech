using System;
using System.Collections.Generic;
using Game.Quest.Interface;
using MainGame.Basic.Quest;
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
        private readonly Dictionary<string, QuestElement> _questElements = new();

        private ItemImages _itemImages;

        public void SetQuests(List<QuestConfigData> questConfigs, ItemImages itemImages, Action<string> getReward)
        {
            //報酬アイテム取得イベントをQuestUIに伝える
            questDetailUI.OnGetReward += getReward;

            _itemImages = itemImages;
            foreach (var questConfig in questConfigs)
            {
                //クエストの追加
                var questElement = Instantiate(questElementPrefab, questElementParent);
                questElement.SetQuest(questConfig, SetQuestDetail);
                _questElements.Add(questConfig.QuestId, questElement);


                //前提クエストの矢印設定
                foreach (var prerequisite in questConfig.PrerequisiteQuests)
                {
                    var allow = Instantiate(prerequisiteQuestsAllow, allowParent);
                    allow.SetAllow(prerequisite.UiPosition, questConfig.UiPosition);
                }
            }
        }


        private void SetQuestDetail(QuestConfigData config, QuestProgressData questProgressData)
        {
            questDetailUI.SetQuest(config, questProgressData, _itemImages);
        }


        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        public void SetQuestProgress(string quest, QuestProgressData questProgressData)
        {
            _questElements[quest].SetProgress(questProgressData);
        }
    }
}