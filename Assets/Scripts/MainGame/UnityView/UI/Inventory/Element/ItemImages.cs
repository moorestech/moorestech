using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.Element
{
    [CreateAssetMenu(fileName = "ItemImages", menuName = "ItemImages", order = 0)]
    public class ItemImages : ScriptableObject
    {
        [SerializeField] private List<ItemViewData> itemImageList;
        [SerializeField] private ItemViewData nothingIndexItemImage;

        public ItemViewData GetItemViewData(int index)
        {
            if (itemImageList.Count <= index)
            {
                return nothingIndexItemImage;
            }

            return itemImageList[index];
        }
    }

    [Serializable]
    public class ItemViewData
    {
        public Sprite itemImage;
        public string itemName;
    }
}