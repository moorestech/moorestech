using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UnityView.WorldMapTile
{
    [CreateAssetMenu(fileName = "WorldMapTileObjects", menuName = "WorldMapTileObjects", order = 0)]
    public class WorldMapTileObjects : ScriptableObject
    {
        [SerializeField] private List<MapTileObject> TileObjectList;
        [SerializeField] private MapTileObject NothingIndexBlockObject;

        public MapTileObject GetTile(int index)
        {
            if (TileObjectList.Count <= index)
            {
                return NothingIndexBlockObject;
            }

            return TileObjectList[index];
        }
    }
}