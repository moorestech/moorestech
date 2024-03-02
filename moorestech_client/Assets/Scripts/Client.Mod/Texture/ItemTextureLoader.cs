using System.Collections.Generic;
using System.Linq;
using Core.Item.Config;
using Constant;
using Mod.Loader;
using ServerServiceProvider;
using UnityEngine;

namespace MainGame.ModLoader.Texture
{
    public static class ItemTextureLoader
    {
        private const string ModTextureDirectory = "assets/item/";

        public static List<ItemViewData> GetItemTexture(string modDirectory, MoorestechServerServiceProvider moorestechServerServiceProvider)
        {
            var textureList = new List<ItemViewData>();

            var mods = new ModsResource(modDirectory);

            foreach (var mod in mods.Mods)
            {
                var itemIds = moorestechServerServiceProvider.ItemConfig.GetItemIds(mod.Value.ModMetaJson.ModId);
                var itemConfigs = itemIds.Select(moorestechServerServiceProvider.ItemConfig.GetItemConfig).ToList();

                textureList.AddRange(GetTextures(itemConfigs, mod.Value));
            }

            return textureList;
        }


        private static List<ItemViewData> GetTextures(List<ItemConfigData> itemConfigs, Mod.Loader.Mod mod)
        {
            var textureList = new List<ItemViewData>();
            foreach (var config in itemConfigs)
            {
                var texture = GetExtractedZipTexture.Get(mod.ExtractedPath, config.ImagePath);
                if (texture == null) Debug.LogError("ItemTexture Not Found  ModId:" + mod.ModMetaJson.ModId + " ItemName:" + config.Name);
                textureList.Add(new ItemViewData(texture.ToSprite(), texture, config));
            }

            return textureList;
        }
    }
    
    

    public class ItemViewData
    {
        public readonly ItemConfigData ItemConfigData;
        public int ItemId => ItemConfigData.ItemId;
        public string ItemName => ItemConfigData.Name;
        
        public readonly Sprite ItemImage;
        public readonly UnityEngine.Texture ItemTexture;

        public ItemViewData(Sprite itemImage, UnityEngine.Texture itemTexture, ItemConfigData itemConfigData)
        {
            ItemImage = itemImage;
            ItemConfigData = itemConfigData;
            ItemTexture = itemTexture;
        }
    }
}