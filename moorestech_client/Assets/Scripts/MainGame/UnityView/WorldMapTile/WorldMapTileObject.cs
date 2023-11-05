using UnityEngine;

namespace MainGame.UnityView.WorldMapTile
{
    [CreateAssetMenu(fileName = "WorldMapTileObject", menuName = "WorldMapTileObject", order = 0)]
    public class WorldMapTileObject : ScriptableObject
    {
        [SerializeField] private MapTileObject MapTile;

        [SerializeField] private Material baseMaterial;

        [SerializeField] private Material noneTileMaterial;


        [SerializeField] private Material emptyTileMaterial;
        public MapTileObject MapTileObject => MapTile;
        public Material BaseMaterial => baseMaterial;
        public Material NoneTileMaterial => noneTileMaterial;
        public Material EmptyTileMaterial => emptyTileMaterial;
    }
}