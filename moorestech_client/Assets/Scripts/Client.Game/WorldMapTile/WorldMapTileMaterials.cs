using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameConst;
using MainGame.ModLoader;
using MainGame.ModLoader.Texture;
using ServerServiceProvider;
using UnityEngine;

namespace MainGame.UnityView.WorldMapTile
{
    public class WorldMapTileMaterials
    {
        private readonly WorldMapTileObject _worldMapTileObject;
        private List<Material> _materials;

        public WorldMapTileMaterials(WorldMapTileObject worldMapTileObject, MoorestechServerServiceProvider moorestechServerServiceProvider)
        {
            _worldMapTileObject = worldMapTileObject;
            LoadMaterial(moorestechServerServiceProvider).Forget();
        }

        private async UniTask LoadMaterial(MoorestechServerServiceProvider moorestechServerServiceProvider)
        {
            //await BlockGlbLoader.GetBlockLoaderは同期処理になっているため、ここで1フレーム待って他のイベントが追加されるのを待つ
            await UniTask.WaitForFixedUpdate();

            _materials = WorldMapTileTextureLoader.GetMapTileMaterial(ServerConst.ServerModsDirectory, moorestechServerServiceProvider, _worldMapTileObject.BaseMaterial);
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