using UnityEngine;

namespace MainGame.UnityView.WorldMapTile
{
    public class MapTileObject : MonoBehaviour
    {
        [SerializeField] private MeshRenderer plane;
        public int TileId { get; private set; }
        public void SetMaterial(int id,Material material)
        {
            TileId = id;
            plane.material = material;
        }
    }
}