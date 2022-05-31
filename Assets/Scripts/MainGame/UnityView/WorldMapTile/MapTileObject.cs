using UnityEngine;

namespace MainGame.UnityView.WorldMapTile
{
    public class MapTileObject : MonoBehaviour
    {
        [SerializeField] private MeshRenderer plane;
        public void SetMaterial(Material material)
        {
            plane.material = material;
        }
    }
}