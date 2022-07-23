using Core.Item;
using MainGame.UnityView.UI.Inventory.Element;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Quest.QuestDetail
{
    public class QuestRewardItemElement : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text cntText;
        public void SetItem(IItemStack itemStack,ItemImages itemImages)
        {
            cntText.text = itemStack.Count.ToString();
            iconImage.sprite = itemImages.GetItemView(itemStack.Id).itemImage;
        }
        
    }
}