using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.ModLoader;
using MainGame.ModLoader.Texture;
using MainGame.UnityView.Util;
using SinglePlay;
using UnityEngine;

namespace MainGame.UnityView.WorldMapTile
{
    public class WorldMapTileMaterials
    {
        private readonly WorldMapTileObject _worldMapTileObject;
        private List<Material> _materials;

        public WorldMapTileMaterials(WorldMapTileObject worldMapTileObject,ModDirectory modDirectory,
            SinglePlayInterface singlePlayInterface,IInitialViewLoadingDetector initialViewLoadingDetector)
        {
            _worldMapTileObject = worldMapTileObject;
            LoadMaterial(modDirectory,singlePlayInterface,initialViewLoadingDetector).Forget();
        }

        private async UniTask LoadMaterial(ModDirectory modDirectory,
            SinglePlayInterface singlePlayInterface,IInitialViewLoadingDetector initialViewLoadingDetector)
        {
            _materials = await WorldMapTileTextureLoader.GetMapTileMaterial(modDirectory.Directory,singlePlayInterface,_worldMapTileObject.BaseMaterial);
            initialViewLoadingDetector.FinishMapTileTextureLoading();
        }
        

        public Material GetMaterial(int index)
        {
            if (index == 0)
            {
                return _worldMapTileObject.EmptyTileMaterial;
            }
            
            index--;
            if (_materials.Count <= index)
            {
                return _worldMapTileObject.NoneTileMaterial;
            }
            return _materials[index];
        }
        
    }
}