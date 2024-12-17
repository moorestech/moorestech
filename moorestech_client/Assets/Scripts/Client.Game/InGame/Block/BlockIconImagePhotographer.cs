using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class BlockIconImagePhotographer : MonoBehaviour
    {
        [SerializeField] private int iconSize = 512;
        [SerializeField] Camera cameraPrefab;
        
        public async UniTask<List<Texture2D>> TakeBlockIconImages(List<GameObject> blockPrefabs)
        {
            var blocks = new List<GameObject>();
            
            foreach (var blockPrefab in blockPrefabs)
            {
                var block = Instantiate(blockPrefab, transform);
                blocks.Add(block);
            }
            
            // ブロックを一直線に並べる
            var maxSize = GetMaxBlockSize(blocks);
            var spacing = maxSize * 2f;
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                block.transform.position = new Vector3(i * spacing, 0f, 0f);
                block.transform.rotation = Quaternion.identity;
                block.transform.localScale = Vector3.one;
            }
            
            // 全てのブロックでアイコンを取得
            var tasks = new List<UniTask<Texture2D>>();
            foreach (var block in blocks)
            {
                tasks.Add(GetIcon(block));
            }
            
            var result = await UniTask.WhenAll(tasks);
            
            return result.ToList();
        }
        
        private async UniTask<Texture2D> GetIcon(GameObject blockPrefab)
        {
            var block = Instantiate(blockPrefab);
            
            // ブロックの重心とバウンディングを取得
            var bounds = block.GetComponentsInChildren<MeshRenderer>().Select(b => b.bounds).ToList();
            var center = bounds.Select(b => b.center).Aggregate((b1, b2) => b1 + b2) / bounds.Count;
            
            // カメラ角度設定(例：上から30度、Y軸に対して45度傾ける)
            var camera = Instantiate(cameraPrefab);
            camera.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            
            // バウンディングボックスの最大寸法を取得
            var minPos = bounds.Select(b => b.min).Aggregate(Vector3.Min);
            var maxPos = bounds.Select(b => b.max).Aggregate(Vector3.Max);
            var maxSize = Vector3.Distance(minPos, maxPos);
            
            // カメラの視野角(FOV)と最大サイズから距離を計算
            float fovRad = camera.fieldOfView * Mathf.Deg2Rad;
            float distance = (maxSize * 0.5f) / Mathf.Tan(fovRad * 0.5f);
            
            camera.transform.position = center - camera.transform.forward * (distance * 0.8f);
            camera.transform.LookAt(center);
            
            // カメラ背景をアルファ付き透明に設定
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            
            await UniTask.Yield(PlayerLoopTiming.Update);
            
            // アルファ付きのRenderTextureを使用
            var renderTexture = new RenderTexture(iconSize, iconSize, 24, RenderTextureFormat.ARGB32)
            {
                useMipMap = false,
                autoGenerateMips = false
            };
            
            camera.targetTexture = renderTexture;
            camera.Render();
            camera.targetTexture = null;
            
            // アルファ付きのTexture2Dに読み込み
            var texture = new Texture2D(iconSize, iconSize, TextureFormat.RGBA32, false);
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, iconSize, iconSize), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            
            // 不要なオブジェクトを破棄
            Destroy(block);
            Destroy(renderTexture);
            
            return texture;
        }
        
        private float GetMaxBlockSize(List<GameObject> blocks)
        {
            float maxSize = 0f;
            foreach (var block in blocks)
            {
                var renderers = block.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length == 0) continue;
                
                var boundsList = renderers.Select(r => r.bounds).ToList();
                var minPos = boundsList.Select(b => b.min).Aggregate(Vector3.Min);
                var maxPos = boundsList.Select(b => b.max).Aggregate(Vector3.Max);
                var size = Vector3.Distance(minPos, maxPos);
                
                if (size > maxSize)
                    maxSize = size;
            }
            
            return maxSize;
        }
    }
}