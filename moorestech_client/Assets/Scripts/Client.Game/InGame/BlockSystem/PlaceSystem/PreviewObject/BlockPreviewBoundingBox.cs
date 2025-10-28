using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.PreviewObject
{
    public class BlockPreviewBoundingBox : MonoBehaviour
    {
        [SerializeField] private GameObject x_Origin_Edge;
        [SerializeField] private GameObject x_Y_Edge;
        [SerializeField] private GameObject x_YZ_Edge;
        [SerializeField] private GameObject x_Z_Edge;
        
        [SerializeField] private GameObject y_Origin_Edge;
        [SerializeField] private GameObject y_X_Edge;
        [SerializeField] private GameObject y_Z_Edge;
        [SerializeField] private GameObject y_XZ_Edge;
        
        [SerializeField] private GameObject z_Origin_Edge;
        [SerializeField] private GameObject z_Y_Edge;
        [SerializeField] private GameObject z_X_Edge;
        [SerializeField] private GameObject z_XY_Edge;
        
        [SerializeField] private GameObject xy_Origin_Surface;
        [SerializeField] private BlockPreviewBoundingBoxSurface xy_Origin_SurfaceScript;
        [SerializeField] private GameObject xy_Z_Surface;
        [SerializeField] private BlockPreviewBoundingBoxSurface xy_Z_SurfaceScript;
        
        [SerializeField] private GameObject yz_Origin_Surface;
        [SerializeField] private BlockPreviewBoundingBoxSurface yz_Origin_SurfaceScript;
        [SerializeField] private GameObject yz_X_Surface;
        [SerializeField] private BlockPreviewBoundingBoxSurface yz_X_SurfaceScript;
        
        [SerializeField] private GameObject zx_Origin_Surface;
        [SerializeField] private BlockPreviewBoundingBoxSurface zx_Origin_SurfaceScript;
        [SerializeField] private GameObject zx_Y_Surface;
        [SerializeField] private BlockPreviewBoundingBoxSurface zx_Y_SurfaceScript;
        
        public void SetBoundingBox(Vector3Int blockSize, BlockDirection blockDirection)
        {
            // ========== X軸方向のエッジ ==========
            x_Origin_Edge.transform.localScale = new Vector3(blockSize.x, 1, 1);
            x_Origin_Edge.transform.localPosition = new Vector3(0, 0, 0);
            
            x_Y_Edge.transform.localScale = new Vector3(blockSize.x, 1, 1);
            x_Y_Edge.transform.localPosition = new Vector3(0, blockSize.y, 0);
            
            x_YZ_Edge.transform.localScale = new Vector3(blockSize.x, 1, 1);
            x_YZ_Edge.transform.localPosition = new Vector3(0, blockSize.y, blockSize.z);
            
            x_Z_Edge.transform.localScale = new Vector3(blockSize.x, 1, 1);
            x_Z_Edge.transform.localPosition = new Vector3(0, 0, blockSize.z);
            
            
            // ========== Y軸方向のエッジ ==========
            y_Origin_Edge.transform.localScale = new Vector3(blockSize.y, 1, 1);
            y_Origin_Edge.transform.localPosition = new Vector3(0, 0, 0);
            
            y_X_Edge.transform.localScale = new Vector3(blockSize.y, 1, 1);
            y_X_Edge.transform.localPosition = new Vector3(blockSize.x, 0, 0);
            
            y_Z_Edge.transform.localScale = new Vector3(blockSize.y, 1, 1);
            y_Z_Edge.transform.localPosition = new Vector3(0, 0, blockSize.z);
            
            y_XZ_Edge.transform.localScale = new Vector3(blockSize.y, 1, 1);
            y_XZ_Edge.transform.localPosition = new Vector3(blockSize.x, 0, blockSize.z);
            
            
            // ========== Z軸方向のエッジ ==========
            z_Origin_Edge.transform.localScale = new Vector3(blockSize.z, 1, 1);
            z_Origin_Edge.transform.localPosition = new Vector3(0, 0, 0);
            
            z_Y_Edge.transform.localScale = new Vector3(blockSize.z, 1, 1);
            z_Y_Edge.transform.localPosition = new Vector3(0, blockSize.y, 0);
            
            z_X_Edge.transform.localScale = new Vector3(blockSize.z, 1, 1);
            z_X_Edge.transform.localPosition = new Vector3(blockSize.x, 0, 0);
            
            z_XY_Edge.transform.localScale = new Vector3(blockSize.z, 1, 1);
            z_XY_Edge.transform.localPosition = new Vector3(blockSize.x, blockSize.y, 0);
            
            
            // ========== x-y 平面 ==========
            xy_Origin_Surface.transform.localScale = new Vector3(blockSize.x, blockSize.y, 1);
            xy_Origin_Surface.transform.localPosition = new Vector3(0, 0, 0);
            xy_Origin_SurfaceScript.SetPreviewSurfaceType(blockDirection switch
            { 
                BlockDirection.North => PreviewSurfaceType.YX_Origin,
                BlockDirection.East => PreviewSurfaceType.YZ_Origin,
                BlockDirection.South => PreviewSurfaceType.YX_Z,
                BlockDirection.West => PreviewSurfaceType.YZ_X,
            });
            
            xy_Z_Surface.transform.localScale = new Vector3(blockSize.x, blockSize.y, 1);
            xy_Z_Surface.transform.localPosition = new Vector3(0, 0, blockSize.z);
            xy_Z_SurfaceScript.SetPreviewSurfaceType(blockDirection switch
            {
                BlockDirection.North => PreviewSurfaceType.YX_Z,
                BlockDirection.East => PreviewSurfaceType.YZ_X,
                BlockDirection.South => PreviewSurfaceType.YX_Origin,
                BlockDirection.West => PreviewSurfaceType.YZ_Origin,
            });
            
            
            // ========== y-z 平面 ==========
            yz_Origin_Surface.transform.localScale = new Vector3(1, blockSize.y, blockSize.z);
            yz_Origin_Surface.transform.localPosition = new Vector3(0, 0, 0);
            yz_Origin_SurfaceScript.SetPreviewSurfaceType(blockDirection switch
            {
                BlockDirection.North => PreviewSurfaceType.YZ_Origin,
                BlockDirection.East => PreviewSurfaceType.YX_Z,
                BlockDirection.South => PreviewSurfaceType.YZ_X,
                BlockDirection.West => PreviewSurfaceType.YX_Origin,
            });
            
            yz_X_Surface.transform.localScale = new Vector3(1, blockSize.y, blockSize.z);
            yz_X_Surface.transform.localPosition = new Vector3(blockSize.x, 0, 0);
            yz_X_SurfaceScript.SetPreviewSurfaceType(blockDirection switch
            {
                BlockDirection.North => PreviewSurfaceType.YZ_X,
                BlockDirection.East => PreviewSurfaceType.YX_Origin,
                BlockDirection.South => PreviewSurfaceType.YZ_Origin,
                BlockDirection.West => PreviewSurfaceType.YX_Z,
            });
            
            
            // ========== z-x 平面 ==========
            zx_Origin_Surface.transform.localScale = new Vector3(blockSize.x, 1, blockSize.z);
            zx_Origin_Surface.transform.localPosition = new Vector3(0, 0, 0);
            zx_Origin_SurfaceScript.SetPreviewSurfaceType(PreviewSurfaceType.XZ_Origin);
            
            zx_Y_Surface.transform.localScale = new Vector3(blockSize.x, 1, blockSize.z);
            zx_Y_Surface.transform.localPosition = new Vector3(0, blockSize.y, 0);
            zx_Y_SurfaceScript.SetPreviewSurfaceType(PreviewSurfaceType.XZ_Y);
        }
    }
}