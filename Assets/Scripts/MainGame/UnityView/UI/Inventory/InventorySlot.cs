using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory
{
    public class InventorySlot: MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private TextMeshPro countText;
        
        public void SetItem(Sprite sprite, int count)
        {
            image.sprite = sprite;
            
            if (count == 0)
            {
                countText.text = "";
            }
            else
            {
                countText.text = count.ToString();
            }
        }
    }
}