using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UnityView.WorldMapTile
{
    [CreateAssetMenu(fileName = "WorldMapTileObject", menuName = "WorldMapTileObject", order = 0)]
    public class WorldMapTileObject : ScriptableObject
    {
        [SerializeField] private MapTileObject MapTile;
        public MapTileObject MapTileObject => MapTile;
        [SerializeField] private Material BaseMaterial;
        public Material BaseMaterialObject => BaseMaterial;
        [SerializeField] private Material NoneTileMaterial;
        public Material NoneTileMaterialObject => NoneTileMaterial;
    }
}