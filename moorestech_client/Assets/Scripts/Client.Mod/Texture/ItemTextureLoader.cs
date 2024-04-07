using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Core.Item.Config;
using Game.Context;
using Mod.Loader;
using UnityEngine;

namespace MainGame.ModLoader.Texture
{
    public static class ItemTextureLoader
    {
        private const string ModTextureDirectory = "assets/item/";

        public static List<ItemViewData> GetItemTexture(string modDirectory)
        {
            var textureList = new List<ItemViewData>();

            var mods = new ModsResource(modDirectory);

            foreach (KeyValuePair<string, Mod.Loader.Mod> mod in mods.Mods)
            {
                List<int> itemIds = ServerContext.ItemConfig.GetItemIds(mod.Value.ModMetaJson.ModId);
                var itemConfigs = itemIds.Select(ServerContext.ItemConfig.GetItemConfig).ToList();

                textureList.AddRange(GetTextures(itemConfigs, mod.Value));
            }

            return textureList;
        }


        private static List<ItemViewData> GetTextures(List<IItemConfigData> itemConfigs, Mod.Loader.Mod mod)
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
        public readonly IItemConfigData ItemConfigData;

        public readonly Sprite ItemImage;
        public readonly UnityEngine.Texture ItemTexture;

        public ItemViewData(Sprite itemImage, UnityEngine.Texture itemTexture, IItemConfigData itemConfigData)
        {
            ItemImage = itemImage;
            ItemConfigData = itemConfigData;
            ItemTexture = itemTexture;
        }
        public int ItemId => ItemConfigData.ItemId;
        public string ItemName => ItemConfigData.Name;
    }
}