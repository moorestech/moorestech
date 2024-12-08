using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class BlockIconImagePhotographer : MonoBehaviour
    {
        [SerializeField] Camera _camera;
        
        public Sprite GetIcon(GameObject blockPrefab)
        {
            var block = Instantiate(blockPrefab);
            block.transform.position = new Vector3(0, 0, 0);
            block.transform.rotation = Quaternion.identity;
            block.transform.localScale = Vector3.one;
            
            // ブロックの重心を求める
            var bounds = block.GetComponentInChildren<MeshRenderer>().bounds;
            var center = bounds.center;
                
            // カメラの位置を、ブロックの中心からY、Xを斜め45度にずらし、全体が収まるように距離を求める
            //TODO
            
            var renderTexture = new RenderTexture(256, 256, 24);
            _camera.targetTexture = renderTexture;
            _camera.Render();
            _camera.targetTexture = null;
            
            var texture = new Texture2D(256, 256);
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            
            Destroy(block);
            Destroy(renderTexture);
            
            return Sprite.Create(texture, new Rect(0, 0, 256, 256), Vector2.one * 0.5f);
        }
    }
}