using UnityEngine;

namespace Client.Game.InGame.Block
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
        [SerializeField] private GameObject xy_Z_Surface;
        
        [SerializeField] private GameObject yz_Origin_Surface;
        [SerializeField] private GameObject yz_X_Surface;
        
        [SerializeField] private GameObject zx_Origin_Surface;
        [SerializeField] private GameObject ZX_Y_Surface;
        
        public void SetBoundingBox(Vector3Int boundingBox)
        {
            // ========== X軸方向のエッジ ==========
            x_Origin_Edge.transform.localScale = new Vector3(boundingBox.x, 1, 1);
            x_Origin_Edge.transform.localPosition = new Vector3(0, 0, 0);
            
            x_Y_Edge.transform.localScale = new Vector3(boundingBox.x, 1, 1);
            x_Y_Edge.transform.localPosition = new Vector3(0, boundingBox.y, 0);
            
            x_YZ_Edge.transform.localScale = new Vector3(boundingBox.x, 1, 1);
            x_YZ_Edge.transform.localPosition = new Vector3(0, boundingBox.y, boundingBox.z);
            
            x_Z_Edge.transform.localScale = new Vector3(boundingBox.x, 1, 1);
            x_Z_Edge.transform.localPosition = new Vector3(0, 0, boundingBox.z);
            
            
            // ========== Y軸方向のエッジ ==========
            y_Origin_Edge.transform.localScale = new Vector3(boundingBox.y, 1, 1);
            y_Origin_Edge.transform.localPosition = new Vector3(0, 0, 0);
            
            y_X_Edge.transform.localScale = new Vector3(boundingBox.y, 1, 1);
            y_X_Edge.transform.localPosition = new Vector3(boundingBox.x, 0, 0);
            
            y_Z_Edge.transform.localScale = new Vector3(boundingBox.y, 1, 1);
            y_Z_Edge.transform.localPosition = new Vector3(0, 0, boundingBox.z);
            
            y_XZ_Edge.transform.localScale = new Vector3(boundingBox.y, 1, 1);
            y_XZ_Edge.transform.localPosition = new Vector3(boundingBox.x, 0, boundingBox.z);
            
            
            // ========== Z軸方向のエッジ ==========
            z_Origin_Edge.transform.localScale = new Vector3(boundingBox.z, 1, 1);
            z_Origin_Edge.transform.localPosition = new Vector3(0, 0, 0);
            
            z_Y_Edge.transform.localScale = new Vector3(boundingBox.z, 1, 1);
            z_Y_Edge.transform.localPosition = new Vector3(0, boundingBox.y, 0);
            
            z_X_Edge.transform.localScale = new Vector3(boundingBox.z, 1, 1);
            z_X_Edge.transform.localPosition = new Vector3(boundingBox.x, 0, 0);
            
            z_XY_Edge.transform.localScale = new Vector3(boundingBox.z, 1, 1);
            z_XY_Edge.transform.localPosition = new Vector3(boundingBox.x, boundingBox.y, 0);
            
            
            // ========== x-y 平面 ==========
            xy_Origin_Surface.transform.localScale = new Vector3(boundingBox.x, boundingBox.y, 1);
            xy_Origin_Surface.transform.localPosition = new Vector3(0, 0, 0);
            
            xy_Z_Surface.transform.localScale = new Vector3(boundingBox.x, boundingBox.y, 1);
            xy_Z_Surface.transform.localPosition = new Vector3(0, 0, boundingBox.z);
            
            
            // ========== y-z 平面 ==========
            yz_Origin_Surface.transform.localScale = new Vector3(1, boundingBox.y, boundingBox.z);
            yz_Origin_Surface.transform.localPosition = new Vector3(0, 0, 0);
            
            yz_X_Surface.transform.localScale = new Vector3(1, boundingBox.y, boundingBox.z);
            yz_X_Surface.transform.localPosition = new Vector3(boundingBox.x, 0, 0);
            
            
            // ========== z-x 平面 ==========
            zx_Origin_Surface.transform.localScale = new Vector3(boundingBox.x, 1, boundingBox.z);
            zx_Origin_Surface.transform.localPosition = new Vector3(0, 0, 0);
            
            ZX_Y_Surface.transform.localScale = new Vector3(boundingBox.x, 1, boundingBox.z);
            ZX_Y_Surface.transform.localPosition = new Vector3(0, boundingBox.y, 0);
        }
    }
}