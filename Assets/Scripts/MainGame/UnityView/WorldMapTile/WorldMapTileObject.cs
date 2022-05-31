using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UnityView.WorldMapTile
{
    [CreateAssetMenu(fileName = "WorldMapTileObject", menuName = "WorldMapTileObject", order = 0)]
    public class WorldMapTileObject : ScriptableObject
    {
        [SerializeField] private MapTileObject MapTile;
        public MapTileObject MapTileObject => MapTile;
        
        [SerializeField] private Material baseMaterial;
        public Material BaseMaterial => baseMaterial;
        
        [SerializeField] private Material noneTileMaterial;
        public Material NoneTileMaterial => noneTileMaterial;
        
        
        [SerializeField] private Material emptyTileMaterial;
        public Material EmptyTileMaterial => emptyTileMaterial;
    }
}