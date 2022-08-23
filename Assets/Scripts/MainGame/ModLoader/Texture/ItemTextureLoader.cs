using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Core.Item.Config;
using Cysharp.Threading.Tasks;
using Mod.Loader;
using SinglePlay;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MainGame.ModLoader.Texture
{
    public static class ItemTextureLoader
    {
        private const string ModTextureDirectory = "assets/item/";
        public static List<(Texture2D texture2D,string name)> GetItemTexture(string modDirectory,SinglePlayInterface singlePlayInterface)
        {
            var textureList = new List<(Texture2D,string)>();
            
            var mods = new ModsResource(modDirectory);

            foreach (var mod in mods.Mods)
            {
                var itemIds = singlePlayInterface.ItemConfig.GetItemIds(mod.Value.ModMetaJson.ModId);
                var itemConfigs = itemIds.Select(singlePlayInterface.ItemConfig.GetItemConfig).ToList();

                textureList.AddRange( GetTextures(itemConfigs,mod.Value));
            }

            return textureList;
        }


        private static List<(Texture2D texture2D,string name)> GetTextures(List<ItemConfigData> itemConfigs,Mod.Loader.Mod mod)
        {
            var textureList = new List<(Texture2D,string)>();
            foreach (var config in itemConfigs)
            {
                var texture = GetExtractedZipTexture.Get(mod.ExtractedPath, ModTextureDirectory + config.Name + ".png");
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