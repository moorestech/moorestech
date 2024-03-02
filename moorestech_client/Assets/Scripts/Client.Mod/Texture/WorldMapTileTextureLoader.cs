using System.Collections.Generic;
using System.Linq;
using Core.Ore.Config;
using Mod.Loader;
using ServerServiceProvider;
using UnityEngine;

namespace MainGame.ModLoader.Texture
{
    public static class WorldMapTileTextureLoader
    {
        private const string TextureDirectory = "assets/maptile/";

        public static List<Material> GetMapTileMaterial(string modDirectory, MoorestechServerServiceProvider moorestechServerServiceProvider, Material baseMaterial)
        {
            var materials = new List<Material>();
            var mods = new ModsResource(modDirectory);

            foreach (var mod in mods.Mods)
            {
                var oreIDs = moorestechServerServiceProvider.OreConfig.GetOreIds(mod.Value.ModMetaJson.ModId);
                var oreConfigs = oreIDs.Select(moorestechServerServiceProvider.OreConfig.Get).ToList();

                materials.AddRange(GetTextures(oreConfigs, mod.Value, baseMaterial));
            }

            return materials;
        }


        private static List<Material> GetTextures(List<OreConfigData> oreConfigs, Mod.Loader.Mod mod, Material baseMaterial)
        {
            var resultMaterial = new List<Material>();
            foreach (var config in oreConfigs)
            {
                var texture = GetExtractedZipTexture.Get(mod.ExtractedPath, TextureDirectory + config.Name + ".png");
                if (texture == null)
                {
                    Debug.LogError("ItemTexture Not Found  ModId:" + mod.ModMetaJson.ModId + " OreName:" + config.Name);
                    continue;
                }

                var newMaterial = new Material(baseMaterial.shader);
                newMaterial.CopyPropertiesFromMaterial(baseMaterial);
                newMaterial.name = config.Name;
                newMaterial.mainTexture = texture;

                resultMaterial.Add(newMaterial);
            }

            return resultMaterial;
        }
    }
}