using System.Collections.Generic;
using MainGame.Mod;
using SinglePlay;
using UnityEngine;

namespace MainGame.UnityView.WorldMapTile
{
    public class WorldMapTileMaterials
    {
        private readonly WorldMapTileObject _worldMapTileObject;
        private readonly List<Material> _materials;

        public WorldMapTileMaterials(WorldMapTileObject worldMapTileObject,ModDirectory modDirectory,SinglePlayInterface singlePlayInterface)
        {
            _worldMapTileObject = worldMapTileObject;
            _materials = WorldMapTileTextureLoader.GetMapTileMaterial(modDirectory.Directory,singlePlayInterface,_worldMapTileObject.BaseMaterialObject);
        }

        public Material GetMaterial(int index)
        {
            if (_materials.Count <= index)
            {
                return _worldMapTileObject.NoneTileMaterialObject;
            }
            return _materials[index];
        }
        
    }
}