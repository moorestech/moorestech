using Client.Game.InGame.Block;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject
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
        
        public void SetBoundingBox(Vector3Int blockSize, BlockDirection blockDirection, BlockGameObject blockGameObject)
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
            
            
            // 面の種類は、ローカルの外向き法線を向きで回したワールドの面で決める（縦向きを含む12向き）
            // Each face type comes from rotating its local outward normal by the direction (all 12 directions, vertical included)
            xy_Origin_Surface.transform.localScale = new Vector3(blockSize.x, blockSize.y, 1);
            xy_Origin_Surface.transform.localPosition = new Vector3(0, 0, 0);
            xy_Origin_SurfaceScript.SetPreviewSurfaceType(PreviewSurfaceTypeResolver.Resolve(blockDirection, new Vector3Int(0, 0, -1)), blockGameObject);

            xy_Z_Surface.transform.localScale = new Vector3(blockSize.x, blockSize.y, 1);
            xy_Z_Surface.transform.localPosition = new Vector3(0, 0, blockSize.z);
            xy_Z_SurfaceScript.SetPreviewSurfaceType(PreviewSurfaceTypeResolver.Resolve(blockDirection, new Vector3Int(0, 0, 1)), blockGameObject);

            yz_Origin_Surface.transform.localScale = new Vector3(1, blockSize.y, blockSize.z);
            yz_Origin_Surface.transform.localPosition = new Vector3(0, 0, 0);
            yz_Origin_SurfaceScript.SetPreviewSurfaceType(PreviewSurfaceTypeResolver.Resolve(blockDirection, new Vector3Int(-1, 0, 0)), blockGameObject);

            yz_X_Surface.transform.localScale = new Vector3(1, blockSize.y, blockSize.z);
            yz_X_Surface.transform.localPosition = new Vector3(blockSize.x, 0, 0);
            yz_X_SurfaceScript.SetPreviewSurfaceType(PreviewSurfaceTypeResolver.Resolve(blockDirection, new Vector3Int(1, 0, 0)), blockGameObject);

            zx_Origin_Surface.transform.localScale = new Vector3(blockSize.x, 1, blockSize.z);
            zx_Origin_Surface.transform.localPosition = new Vector3(0, 0, 0);
            zx_Origin_SurfaceScript.SetPreviewSurfaceType(PreviewSurfaceTypeResolver.Resolve(blockDirection, new Vector3Int(0, -1, 0)), blockGameObject);

            zx_Y_Surface.transform.localScale = new Vector3(blockSize.x, 1, blockSize.z);
            zx_Y_Surface.transform.localPosition = new Vector3(0, blockSize.y, 0);
            zx_Y_SurfaceScript.SetPreviewSurfaceType(PreviewSurfaceTypeResolver.Resolve(blockDirection, new Vector3Int(0, 1, 0)), blockGameObject);
        }
    }
}