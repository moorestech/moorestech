using System;
using System.Collections.Generic;
using GameConst;
using MainGame.Mod;
using SinglePlay;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.Element
{
    public class ItemImages
    {
        private List<ItemViewData> _itemImageList;
        private ItemViewData _nothingIndexItemImage;

        public ItemImages(string modDirectory,SinglePlayInterface singlePlayInterface)
        {
            var textures = ItemTextureLoader.GetItemTexture(ServerConst.ServerModsDirectory,new SinglePlayInterface(ServerConst.ServerModsDirectory));
            
        }


        public ItemViewData GetItemView(int index)
        {
            if (_itemImageList.Count <= index)
            {
                return _nothingIndexItemImage;
            }

            return _itemImageList[index];
        }

        public int GetItemNum() { return _itemImageList.Count; }
    }

    [Serializable]
    public class ItemViewData
    {
        public Sprite itemImage;
        public string itemName;
    }
}