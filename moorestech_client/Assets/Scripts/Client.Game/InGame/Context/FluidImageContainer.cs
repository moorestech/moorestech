using System;
using System.Collections.Generic;
using Client.Mod.Texture;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.Context
{
    /// <summary>
    ///     液体の画像を管理するクラス
    /// </summary>
    public class FluidImageContainer
    {
        private readonly Dictionary<FluidId, FluidViewData> _fluidImageList = new();
        
        private FluidImageContainer(Dictionary<FluidId, FluidViewData> fluidImageList)
        {
            _fluidImageList = fluidImageList;
        }
        
        public static FluidImageContainer CreateAndLoadFluidImageContainer(string modsDirectory)
        {
            var fluidImageList = FluidTextureLoader.GetItemTexture(modsDirectory);
            
            return new FluidImageContainer(fluidImageList);
        }
        
        public FluidViewData GetItemView(Guid fluidGuid)
        {
            var fluidId = MasterHolder.FluidMaster.GetFluidId(fluidGuid);
            return GetItemView(fluidId);
        }
        
        public FluidViewData GetItemView(FluidId fluidId)
        {
            if (fluidId == FluidMaster.EmptyFluidId)
            {
                return null;
            }
            
            if (_fluidImageList.TryGetValue(fluidId, out var view)) return view;
            
            Debug.LogError($"ItemViewData not found. itemId:{fluidId}");
            return null;
        }
        
        public void AddItemView(FluidId fluidId, FluidViewData fluidViewData)
        {
            _fluidImageList[fluidId] = fluidViewData;
        }
    }
}