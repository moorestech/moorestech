using System.Collections.Generic;
using System.IO;
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
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                
                var path = string.IsNullOrEmpty(itemMaster.ImagePath) ? Path.Combine("assets", "item", $"{itemMaster.Name}.png") : itemMaster.ImagePath;
                
                var texture = GetExtractedZipTexture.Get(mod.ExtractedPath, path);
                
                textureList.Add(new ItemViewData(texture, itemMaster));
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
        
        public ItemViewData(Texture2D itemTexture, ItemMasterElement itemMasterElement)
        {
            ItemImage = itemTexture.ToSprite();
            ItemTexture = itemTexture;
            ItemMasterElement = itemMasterElement;
            ItemId = MasterHolder.ItemMaster.GetItemId(itemMasterElement.ItemGuid);
        }
    }
}