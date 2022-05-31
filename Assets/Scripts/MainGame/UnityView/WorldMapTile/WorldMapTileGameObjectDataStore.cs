using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.WorldMapTile
{
    public class WorldMapTileGameObjectDataStore : MonoBehaviour
    {
        private WorldMapTileObject _worldMapTileObject;
        private WorldMapTileMaterials _worldMapTileMaterials;
        
        private Dictionary<Vector2Int,MapTileObject> _blockObjectsDictionary = new();

        [Inject]
        public void Construct(WorldMapTileObject worldMapTileObject,WorldMapTileMaterials worldMapTileMaterials)
        {
            _worldMapTileMaterials = worldMapTileMaterials;
            _worldMapTileObject = worldMapTileObject;
        }
        
        public void GameObjectBlockPlace(Vector2Int tilePosition, int tileId)
        {
            if (_blockObjectsDictionary.ContainsKey(tilePosition) || tileId == 0)
            {
                return;
            }
            
            var tile = Instantiate(_worldMapTileObject.MapTileObject, new Vector3(tilePosition.x, 0, tilePosition.y), Quaternion.Euler(0, 0, 0),transform).
                GetComponent<MapTileObject>();
            tile.SetMaterial(_worldMapTileMaterials.GetMaterial(tileId));
                
            _blockObjectsDictionary.Add(tilePosition, tile);
        }
    }
}