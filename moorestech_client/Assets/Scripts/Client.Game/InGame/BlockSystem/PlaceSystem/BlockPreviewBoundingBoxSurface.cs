using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class BlockPreviewBoundingBoxSurface : MonoBehaviour
    {
        public PreviewSurfaceType PreviewSurfaceType => _previewSurfaceType;
        [SerializeField] private PreviewSurfaceType _previewSurfaceType;
    }
    
    public enum PreviewSurfaceType
    {
        YX_Origin,
        YX_Z,
        
        YZ_Origin,
        YZ_X,
        
        XZ_Origin,
        XZ_Y,
    }
}