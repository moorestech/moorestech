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
            
            // カメラ角度設定(例：上から45度、Y軸に対して45度傾ける)
            _camera.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            
            // バウンディングボックスの最大寸法を取得
            var minPos = bounds.Select(b => b.min).Aggregate(Vector3.Min);
            var maxPos = bounds.Select(b => b.max).Aggregate(Vector3.Max);
            
            var maxSize = Vector3.Distance(minPos, maxPos);
            
            // カメラの視野角(FOV)と最大サイズから距離を計算
            // FOVは垂直方向基準なので、最大サイズがカメラの垂直FOV内に収まる距離を求める
            float fovRad = _camera.fieldOfView * Mathf.Deg2Rad;
            // maxSize/2 がカメラ中央線から上下方向に半分入るようにするためにtanを使用
            float distance = (maxSize * 0.5f) / Mathf.Tan(fovRad * 0.5f);
            
            // カメラをブロック中心を向く方向に distance 分後退させる
            _camera.transform.position = center - _camera.transform.forward * (distance * 0.7f);
            _camera.transform.LookAt(center);
            
            await UniTask.DelayFrame(10);
            
            var renderTexture = new RenderTexture(256, 256, 24);
            
            _camera.targetTexture = renderTexture;
            _camera.Render();
            _camera.targetTexture = null;
            
            var texture = new Texture2D(256, 256, TextureFormat.RGB24, false);
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            
            //Destroy(block);
            Destroy(renderTexture);
            
            return Sprite.Create(texture, new Rect(0, 0, 256, 256), Vector2.one * 0.5f);
        }
    }
}