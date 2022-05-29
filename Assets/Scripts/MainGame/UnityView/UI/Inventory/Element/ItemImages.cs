using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.Element
{
    [CreateAssetMenu(fileName = "ItemImages", menuName = "ItemImages", order = 0)]
    public class ItemImages : ScriptableObject
    {
        [SerializeField] private List<ItemViewData> itemImageList;
        [SerializeField] private ItemViewData nothingIndexItemImage;

        [Inject]
        public void Construct()
        {
            Debug.Log("ItemImages Construct");
        }
        

        public ItemViewData GetItemView(int index)
        {
            if (itemImageList.Count <= index)
            {
                return nothingIndexItemImage;
            }

            return itemImageList[index];
        }

        public int GetItemNum() { return itemImageList.Count; }
    }

    [Serializable]
    public class ItemViewData
    {
        public Sprite itemImage;
        public string itemName;
    }
}