using System.Collections.Generic;
using Game.Quest.Interface;
using UnityEngine;

namespace MainGame.UnityView.UI.Quest
{
    public class QuestTab : MonoBehaviour
    {
        public void SetQuests(List<QuestConfigData> questConfigs)
        {
            
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}