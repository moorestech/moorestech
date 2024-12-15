using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class BlockIconImagePhotographer : MonoBehaviour
    {
        [SerializeField] Camera _camera;
        
        public async UniTask<Sprite> GetIcon(GameObject blockPrefab)
        {
            var block = Instantiate(blockPrefab);
            block.transform.position = Vector3.zero;
            block.transform.rotation = Quaternion.identity;
            block.transform.localScale = Vector3.one;
            
            // ブロックの重心とバウンディングを取得
            var bounds = block.GetComponentsInChildren<MeshRenderer>().Select(b => b.bounds).ToList();
            var center = bounds.Select(b => b.center).Aggregate((b1, b2) => b1 + b2) / bounds.Count;
            
            // カメラ角度設定(例：上から30度、Y軸に対して45度傾ける)
            _camera.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            
            // バウンディングボックスの最大寸法を取得
            var minPos = bounds.Select(b => b.min).Aggregate(Vector3.Min);
            var maxPos = bounds.Select(b => b.max).Aggregate(Vector3.Max);
            var maxSize = Vector3.Distance(minPos, maxPos);
            
            // カメラの視野角(FOV)と最大サイズから距離を計算
            float fovRad = _camera.fieldOfView * Mathf.Deg2Rad;
            float distance = (maxSize * 0.5f) / Mathf.Tan(fovRad * 0.5f);
            
            _camera.transform.position = center - _camera.transform.forward * (distance * 0.7f);
            _camera.transform.LookAt(center);

            // カメラ背景をアルファ付き透明に設定
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            
            await UniTask.DelayFrame(10);
            
            // アルファ付きのRenderTextureを使用
            var renderTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            renderTexture.useMipMap = false;
            renderTexture.autoGenerateMips = false;

            _camera.targetTexture = renderTexture;
            _camera.Render();
            _camera.targetTexture = null;
            
            // アルファ付きのTexture2Dに読み込み
            var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            // 不要なオブジェクトを破棄
            Destroy(block);
            Destroy(renderTexture);

            return Sprite.Create(texture, new Rect(0, 0, 256, 256), Vector2.one * 0.5f);
        }
    }
}
