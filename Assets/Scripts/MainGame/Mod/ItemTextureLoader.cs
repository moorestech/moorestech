using System.Collections.Generic;
using System.Linq;
using Core.Item.Config;
using Mod.Loader;
using SinglePlay;
using UnityEngine;

namespace MainGame.Mod
{
    public static class ItemTextureLoader
    {
        private const string TextureDirectory = "assets/item/";
        public static List<Texture2D> GetItemTexture(string modDirectory,SinglePlayInterface singlePlayInterface)
        {
            var textureList = new List<Texture2D>();
            
            using var mods = new ModsResource(modDirectory);

            foreach (var mod in mods.Mods)
            {
                var itemIds = singlePlayInterface.ItemConfig.GetItemIds(mod.Value.ModMetaJson.ModId);
                var itemConfigs = itemIds.Select(singlePlayInterface.ItemConfig.GetItemConfig).ToList();

                textureList.AddRange(GetTextures(itemConfigs,mod.Value));
            }

            return textureList;
        }


        private static List<Texture2D> GetTextures(List<ItemConfigData> itemConfigs,global::Mod.Loader.Mod mod)
        {
            var textureList = new List<Texture2D>();
            foreach (var config in itemConfigs)
            {
                var texture = GetZipTexture.Get(mod.ZipArchive, TextureDirectory + config.Name + ".png");
                if (texture == null)
                {
                    Debug.LogError("ItemTexture Not Found  ModId:" + mod.ModMetaJson.ModId + " ItemName:" + config.Name);
                    continue;
                }
                textureList.Add(texture);
            }

            return textureList;
        }
    }
}