using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.ModLoader;
using MainGame.ModLoader.Texture;
using SinglePlay;
using UnityEngine;

namespace MainGame.UnityView.WorldMapTile
{
    public class WorldMapTileMaterials
    {
        private readonly WorldMapTileObject _worldMapTileObject;
        private List<Material> _materials;

        public WorldMapTileMaterials(WorldMapTileObject worldMapTileObject, ModDirectory modDirectory,
            SinglePlayInterface singlePlayInterface)
        {
            _worldMapTileObject = worldMapTileObject;
            LoadMaterial(modDirectory, singlePlayInterface).Forget();
        }

        public event Action OnLoadFinished;

        private async UniTask LoadMaterial(ModDirectory modDirectory, SinglePlayInterface singlePlayInterface)
        {
            //await BlockGlbLoader.GetBlockLoaderは同期処理になっているため、ここで1フレーム待って他のイベントが追加されるのを待つ
            await UniTask.WaitForFixedUpdate();

            _materials = WorldMapTileTextureLoader.GetMapTileMaterial(modDirectory.Directory, singlePlayInterface, _worldMapTileObject.BaseMaterial);
            OnLoadFinished?.Invoke();
        }


        public Material GetMaterial(int index)
        {
            if (index == 0) return _worldMapTileObject.EmptyTileMaterial;

            index--;
            if (_materials.Count <= index) return _worldMapTileObject.NoneTileMaterial;
            return _materials[index];
        }
    }
}