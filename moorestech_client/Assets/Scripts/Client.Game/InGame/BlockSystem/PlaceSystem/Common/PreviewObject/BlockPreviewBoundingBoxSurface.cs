using Client.Game.InGame.Block;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject
{
    public class BlockPreviewBoundingBoxSurface : MonoBehaviour
    {
        public BlockGameObject BlockGameObject { get; private set; }
        
        public PreviewSurfaceType PreviewSurfaceType => _previewSurfaceType;
        [SerializeField] private PreviewSurfaceType _previewSurfaceType;
        
        public void SetPreviewSurfaceType(PreviewSurfaceType previewSurfaceType, BlockGameObject blockGameObject)
        {
            BlockGameObject = blockGameObject;
            _previewSurfaceType = previewSurfaceType;
        }
    }
    
    public enum PreviewSurfaceType
    {
        YX_Origin, // YX平面の手前側 Front side of YX plane
        YX_Z, // YX平面のZ方向の奥側 Back side of the YX plane in the Z direction
        
        YZ_Origin, // YZ平面の手前側 Front side of YZ plane
        YZ_X, // YZ平面のX方向の奥側 Back side of the YZ plane in the X direction
        
        XZ_Origin, // XZ平面の下側 Bottom side of XZ plane
        XZ_Y, // XZ平面のY方向の上側 Top side of XZ plane
    }
}