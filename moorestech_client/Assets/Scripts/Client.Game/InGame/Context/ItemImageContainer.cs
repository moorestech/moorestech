using System;
using System.Collections.Generic;
using Client.Mod.Texture;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.Context
{
    /// <summary>
    ///     アイテム画像を管理するクラス
    /// </summary>
    public class ItemImageContainer
    {
        private readonly Dictionary<ItemId,ItemViewData> _itemImageList = new();
        
        private ItemImageContainer(Dictionary<ItemId,ItemViewData> itemImageList)
        {
            _itemImageList = itemImageList;
        }
        
        public static ItemImageContainer CreateAndLoadItemImageContainer(string modsDirectory)
        {
            var itemImageList = ItemTextureLoader.GetItemTexture(modsDirectory);
            
            return new ItemImageContainer(itemImageList);
        }
        
        public ItemViewData GetItemView(Guid itemGuid)
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
            return GetItemView(itemId);
        }
        
        public ItemViewData GetItemView(ItemId itemId)
        {
            //TODO アイテムがないときの対応
            return _itemImageList[itemId];
        }
    }
}