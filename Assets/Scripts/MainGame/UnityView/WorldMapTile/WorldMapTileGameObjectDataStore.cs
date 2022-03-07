using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.WorldMapTile
{
    public class WorldMapTileGameObjectDataStore : MonoBehaviour
    {
        private WorldMapTileObjects _worldMapTileObjects;
        
        private Dictionary<Vector2Int,MapTileObject> _blockObjectsDictionary = new();

        [Inject]
        public void Construct(WorldMapTileObjects worldMapTileObjects)
        {
            _worldMapTileObjects = worldMapTileObjects;
        }
        
        public void GameObjectBlockPlace(Vector2Int tilePosition, int tileId)
        {
            if (_blockObjectsDictionary.ContainsKey(tilePosition) || tileId == 0)
            {
                return;
            }
            
            MainThreadExecutionQueue.Instance.Insert(() =>
            {
                var tile = Instantiate(
                    _worldMapTileObjects.GetTile(tileId),
                    new Vector3(tilePosition.x, 0, tilePosition.y),
                    Quaternion.Euler(0, 0, 0),
                    transform).GetComponent<MapTileObject>();
                
                _blockObjectsDictionary.Add(tilePosition, tile);
            });
        }
    }
}