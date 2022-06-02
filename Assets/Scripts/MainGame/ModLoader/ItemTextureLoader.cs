using System.Collections.Generic;
using System.Linq;
using Core.Item.Config;
using Mod.Loader;
using SinglePlay;
using UnityEngine;

namespace MainGame.ModLoader
{
    public static class ItemTextureLoader
    {
        private const string TextureDirectory = "assets/item/";
        public static List<(Texture2D texture2D,string name)> GetItemTexture(string modDirectory,SinglePlayInterface singlePlayInterface)
        {
            var textureList = new List<(Texture2D,string)>();
            
            using var mods = new ModsResource(modDirectory);

            foreach (var mod in mods.Mods)
            {
                var itemIds = singlePlayInterface.ItemConfig.GetItemIds(mod.Value.ModMetaJson.ModId);
                var itemConfigs = itemIds.Select(singlePlayInterface.ItemConfig.GetItemConfig).ToList();

                textureList.AddRange(GetTextures(itemConfigs,mod.Value));
            }

            return textureList;
        }


        private static List<(Texture2D texture2D,string name)> GetTextures(List<ItemConfigData> itemConfigs,Mod.Loader.Mod mod)
        {
            var textureList = new List<(Texture2D,string)>();
            foreach (var config in itemConfigs)
            {
                var texture = GetZipTexture.Get(mod.ExtractedPath, TextureDirectory + config.Name + ".png");
                if (texture == null)
                {
                    Debug.LogError("ItemTexture Not Found  ModId:" + mod.ModMetaJson.ModId + " ItemName:" + config.Name);
                    continue;
                }
                textureList.Add((texture,config.Name));
            }

            return textureList;
        }
    }
}