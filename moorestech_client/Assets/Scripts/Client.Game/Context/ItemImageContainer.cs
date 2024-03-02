using System;
using System.Collections.Generic;
using Core.Item.Config;
using Cysharp.Threading.Tasks;
using MainGame.ModLoader;
using MainGame.ModLoader.Texture;
using ServerServiceProvider;
using UnityEngine;

namespace MainGame.UnityView.Item
{
    /// <summary>
    /// アイテム画像を管理するクラス
    /// </summary>
    public class ItemImageContainer
    {
        private readonly List<ItemViewData> _itemImageList = new();
        private readonly ItemViewData _nothingIndexItemImage;

        public static ItemImageContainer CreateAndLoadItemImageContainer(string modsDirectory, MoorestechServerServiceProvider moorestechServerServiceProvider)
        {
            var nothingIndexItemImage = new ItemViewData(null, null, new ItemConfigData("Not item", 100, "Not mod", 0));
            var itemImageList = new List<ItemViewData>();

            itemImageList.Add(nothingIndexItemImage); //id 0番は何もないことを表すので、何もない画像を追加
            
            var textures = ItemTextureLoader.GetItemTexture(modsDirectory, moorestechServerServiceProvider);
            itemImageList.AddRange(textures);
            
            return new ItemImageContainer(itemImageList, nothingIndexItemImage);
        }

        private ItemImageContainer(List<ItemViewData> itemImageList, ItemViewData nothingIndexItemImage)
        {
            _itemImageList = itemImageList;
            _nothingIndexItemImage = nothingIndexItemImage;
        } 


        public ItemViewData GetItemView(int itemId)
        {
            if (_itemImageList.Count <= itemId)
            {
                Debug.Log("存在しないアイテムIDです。" + itemId);
                return _nothingIndexItemImage;
            }
            

            return _itemImageList[itemId];
        }

    }
}