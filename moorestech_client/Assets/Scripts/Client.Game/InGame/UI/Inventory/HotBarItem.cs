using Client.Game.UI.Inventory.Element;
using MainGame.ModLoader.Texture;
using TMPro;
using UnityEngine;

namespace Client.Game.UI.Inventory
{
    public class HotBarItem : MonoBehaviour
    {
        [SerializeField] private ItemSlotObject itemSlotObject;
        [SerializeField] private TMP_Text keyBoardText;

        private void Awake()
        {
        }

        public void SetItem(ItemViewData itemViewData, int count)
        {
            itemSlotObject.SetItem(itemViewData, count);
        }

        public void SetKeyBoardText(string text)
        {
            keyBoardText.text = text;
        }

        public void SetSelect(bool isSelect)
        {
            itemSlotObject.SetHotBarSelect(isSelect);
        }
    }
}