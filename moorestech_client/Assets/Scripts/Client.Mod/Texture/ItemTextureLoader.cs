﻿using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Core.Master;
using Mod.Loader;
using Mooresmaster.Model.ItemsModule;
using UnityEngine;

namespace Client.Mod.Texture
{
    public static class ItemTextureLoader
    {
        private const string ModTextureDirectory = "assets/item/";
        
        public static Dictionary<ItemId, ItemViewData> GetItemTexture(string modDirectory)
        {
            var textureList = new Dictionary<ItemId, ItemViewData>();
            
            var mods = new ModsResource(modDirectory);
            
            foreach (var mod in mods.Mods)
            {
                // TODO MooresmasterのmodId対応が入ってから、modごとにアイテムを取得する用になる
                
                // 今は仮で全てのアイテムに対してテクスチャを取得する
                var itemIds = MasterHolder.ItemMaster.GetItemAllIds().ToList();
                foreach (var texture in GetTextures(itemIds, mod.Value))
                {
                    textureList.Add(texture.ItemId, texture);
                }
            }
            
            return textureList;
        }
        
        
        private static List<ItemViewData> GetTextures(List<ItemId> itemIds, global::Mod.Loader.Mod mod)
        {
            var textureList = new List<ItemViewData>();
            foreach (var itemId in itemIds)
            {
                var itemIdMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                Texture2D texture = null;
                Sprite sprite = null;
                
                if (itemIdMaster.ImagePath != null)
                {
                    texture = GetExtractedZipTexture.Get(mod.ExtractedPath, itemIdMaster.ImagePath);
                    if (texture == null) Debug.LogError("ItemTexture Not Found  ModId:" + mod.ModMetaJson.ModId + " ItemName:" + itemIdMaster.Name);
                    sprite = texture.ToSprite();
                }
                
                textureList.Add(new ItemViewData(sprite, texture, itemIdMaster));
            }
            
            return textureList;
        }
    }
    
    
    public class ItemViewData
    {
        public readonly ItemId ItemId;
        public string ItemName => ItemMasterElement.Name;
        public readonly ItemMasterElement ItemMasterElement;
        
        public readonly Sprite ItemImage;
        public readonly UnityEngine.Texture ItemTexture;
        
        public ItemViewData(Sprite itemImage, UnityEngine.Texture itemTexture, ItemMasterElement itemMasterElement)
        {
            ItemImage = itemImage;
            ItemTexture = itemTexture;
            ItemMasterElement = itemMasterElement;
            ItemId = MasterHolder.ItemMaster.GetItemId(itemMasterElement.ItemGuid);
        }
    }
}