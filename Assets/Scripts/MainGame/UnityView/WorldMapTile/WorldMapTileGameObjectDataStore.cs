using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.WorldMapTile
{
    public class WorldMapTileGameObjectDataStore : MonoBehaviour
    {
        [SerializeField] private WorldMapTileObject worldMapTileObject;
        private WorldMapTileMaterials _worldMapTileMaterials;
        
        private Dictionary<Vector2Int,MapTileObject> _blockObjectsDictionary = new();

        [Inject]
        public void Construct(WorldMapTileMaterials worldMapTileMaterials)
        {
            _worldMapTileMaterials = worldMapTileMaterials;
        }
        
        public void GameObjectBlockPlace(Vector2Int tilePosition, int tileId)
        {
            if (_blockObjectsDictionary.ContainsKey(tilePosition) || tileId == 0)
            {
                return;
            }
            
            var tile = Instantiate(worldMapTileObject.MapTileObject, new Vector3(tilePosition.x, 0, tilePosition.y), Quaternion.Euler(0, 0, 0),transform).
                GetComponent<MapTileObject>();
            tile.SetMaterial(tileId,_worldMapTileMaterials.GetMaterial(tileId));
                
            _blockObjectsDictionary.Add(tilePosition, tile);
        }
    }
}