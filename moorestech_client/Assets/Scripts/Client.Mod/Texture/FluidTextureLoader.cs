using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Common;
using Core.Master;
using Mod.Loader;
using Mooresmaster.Model.FluidsModule;
using UnityEngine;

namespace Client.Mod.Texture
{
    public class FluidTextureLoader
    {
        
        public static Dictionary<FluidId, FluidViewData> GetItemTexture(string modDirectory)
        {
            var textureList = new Dictionary<FluidId, FluidViewData>();
            
            var mods = new ModsResource(modDirectory);
            
            foreach (var mod in mods.Mods)
            {
                // TODO MooresmasterのmodId対応が入ってから、modごとにアイテムを取得する用になる
                
                // 今は仮で全てのアイテムに対してテクスチャを取得する
                var itemIds = MasterHolder.FluidMaster.GetAllFluidIds().ToList();
                foreach (var texture in GetTextures(itemIds, mod.Value))
                {
                    textureList.Add(texture.FluidId, texture);
                }
            }
            
            return textureList;
        }
        
        
        private static List<FluidViewData> GetTextures(List<FluidId> fluidIds, global::Mod.Loader.Mod mod)
        {
            var textureList = new List<FluidViewData>();
            foreach (var fluidId in fluidIds)
            {
                var fluidMaster = MasterHolder.FluidMaster.GetFluidMaster(fluidId);
                
                var path = string.IsNullOrEmpty(fluidMaster.ImagePath) ? Path.Combine("assets", "item", $"{fluidMaster.Name}.png") : fluidMaster.ImagePath;
                
                var texture = GetExtractedZipTexture.Get(mod.ExtractedPath, path);
                textureList.Add(new FluidViewData(texture, fluidMaster));
            }
            
            return textureList;
        }
    }
    
    
    public class FluidViewData
    {
        public readonly FluidId FluidId;
        public string FluidName => FluidMasterElement.Name;
        public readonly FluidMasterElement FluidMasterElement;
        
        public readonly Sprite FluidImage;
        public readonly UnityEngine.Texture FluidTexture;
        
        public FluidViewData(Texture2D fluidTexture, FluidMasterElement fluidMasterElement)
        {
            FluidImage = fluidTexture.ToSprite();
            FluidTexture = fluidTexture;
            FluidMasterElement = fluidMasterElement;
            FluidId = MasterHolder.FluidMaster.GetFluidId(fluidMasterElement.FluidGuid);
        }
    }
}