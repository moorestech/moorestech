using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.View
{
    public class InventoryItemSlot: MonoBehaviour
    {
        public delegate void OnItemSlotClicked(int slotIndex);

        [SerializeField] private Image image;
        [SerializeField] private TextMeshProUGUI countText;
        private Button _button;
        private int _slotIndex = -1;
        public void Construct(int slotIndex)
        {
            _button = GetComponent<Button>();
            _slotIndex = slotIndex;
        }
        
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

        public void SubscribeOnItemSlotClick(OnItemSlotClicked onItemSlotClicked)
        {
            _button.onClick.AddListener(() => onItemSlotClicked(_slotIndex));
        }
    }
}