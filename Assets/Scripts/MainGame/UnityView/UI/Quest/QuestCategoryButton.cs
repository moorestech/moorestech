using TMPro;
using UnityEngine;

namespace MainGame.UnityView.UI.Quest
{
    public class QuestCategoryButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text CategoryName;
        public void SetCategory(string category)
        {
            CategoryName.text = category;
        }
    }
}