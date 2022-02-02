using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory
{
    [CreateAssetMenu(fileName = "ItemImages", menuName = "ItemImages", order = 0)]
    public class ItemImages : ScriptableObject
    {
        [SerializeField] private List<Sprite> itemImageList;
        [SerializeField] private Sprite nothingIndexItemImage;

        public Sprite GetItemImage(int index)
        {
            if (itemImageList.Count <= index)
            {
                return nothingIndexItemImage;
            }

            return itemImageList[index];
        }
    }
}