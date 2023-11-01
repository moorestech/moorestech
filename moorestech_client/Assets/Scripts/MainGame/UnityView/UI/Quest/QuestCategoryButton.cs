using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Quest
{
    public class QuestCategoryButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text CategoryName;
        public void SetCategory(string category,Action<string> buttonPush)
        {
            CategoryName.text = category;
            button.onClick.AddListener(() => buttonPush(category));
        }
    }
}