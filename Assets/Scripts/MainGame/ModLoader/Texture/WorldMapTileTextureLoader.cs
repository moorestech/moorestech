using System.Collections.Generic;
using System.Linq;
using Core.Ore.Config;
using Cysharp.Threading.Tasks;
using Mod.Loader;
using SinglePlay;
using UnityEngine;

namespace MainGame.ModLoader.Texture
{
    public static class WorldMapTileTextureLoader
    {
        private const string TextureDirectory = "assets/maptile/";
        public static List<Material> GetMapTileMaterial(string modDirectory,SinglePlayInterface singlePlayInterface,Material baseMaterial)
        {
            var materials = new List<Material>();
            
            using var mods = new ModsResource(modDirectory);

            foreach (var mod in mods.Mods)
            {
                var oreIDs = singlePlayInterface.OreConfig.GetOreIds(mod.Value.ModMetaJson.ModId);
                var oreConfigs = oreIDs.Select(singlePlayInterface.OreConfig.Get).ToList();

                materials.AddRange( GetTextures(oreConfigs,mod.Value,baseMaterial));
            }

            return materials;
        }


        private static List<Material> GetTextures(List<OreConfigData> oreConfigs,global::Mod.Loader.Mod mod,Material baseMaterial)
        {
            var textureList = new List<Material>();
            foreach (var config in oreConfigs)
            {
                var texture = GetExtractedZipTexture.Get(mod.ExtractedPath, TextureDirectory + config.Name + ".png");
                if (texture == null)
                {
                    Debug.LogError("ItemTexture Not Found  ModId:" + mod.ModMetaJson.ModId + " OreName:" + config.Name);
                    continue;
                }
                var newMaterial = new Material(baseMaterial.shader)
                {
                    mainTexture = texture,
                    name = config.Name,
                };
                
                //透過テクスチャの時もあるのでアルファクリップを設定
                newMaterial.SetFloat("_AlphaClip",1.0f);
                newMaterial.SetFloat("_Cutoff",0.5f);
                
                textureList.Add(newMaterial);
            }

            return textureList;
        }
    }
}