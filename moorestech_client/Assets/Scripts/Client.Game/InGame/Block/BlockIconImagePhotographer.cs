using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class BlockIconImagePhotographer : MonoBehaviour
    {
        [SerializeField] private int iconSize = 512;
        [SerializeField] Camera cameraPrefab;
        
        public async UniTask<List<Texture2D>> TakeBlockIconImages(List<BlockObjectInfo> blockObjectInfos)
        {
            var createdBlocks = new List<(GameObject block,BlockObjectInfo blockObjectInfo)>();
            
            foreach (var blockObjectInfo in blockObjectInfos)
            {
                var block = Instantiate(blockObjectInfo.BlockObjectPrefab, transform);
                createdBlocks.Add((block, blockObjectInfo));
            }
            
            // ブロックを一直線に並べる
            var maxSize = GetMaxBlockSize(createdBlocks);
            var spacing = maxSize * 2f;
            for (int i = 0; i < createdBlocks.Count; i++)
            {
                var createdBlock = createdBlocks[i];
                createdBlock.block.transform.position = new Vector3(i * spacing, 0f, 0f);
                createdBlock.block.transform.rotation = Quaternion.identity;
                createdBlock.block.transform.localScale = Vector3.one;
            }
            
            // 全てのブロックでアイコンを取得
            var tasks = new List<UniTask<Texture2D>>();
            foreach (var block in createdBlocks)
            {
                tasks.Add(GetIcon(block.block, block.blockObjectInfo));
            }
            
            var result = await UniTask.WhenAll(tasks);
            
            return result.ToList();
        }
        
        private async UniTask<Texture2D> GetIcon(GameObject block, BlockObjectInfo blockObjectInfo)
        {
            // ブロックの重心とバウンディングを取得
            var bounds = block.GetComponentsInChildren<Renderer>().Select(b => b.bounds).ToList();
            if (bounds.Count == 0)
            {
                throw new System.Exception("ブロックにメッシュレンダラーがありませんでした:" + block.name + " " + blockObjectInfo.BlockMasterElement.Name);
            }
            var center = bounds.Select(b => b.center).Aggregate((b1, b2) => b1 + b2) / bounds.Count;
            
            // カメラ角度設定(例：上から30度、Y軸に対して45度傾ける)
            var blockImageCamera = Instantiate(cameraPrefab);
            blockImageCamera.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            
            // バウンディングボックスの最大寸法を取得
            var minPos = bounds.Select(b => b.min).Aggregate(Vector3.Min);
            var maxPos = bounds.Select(b => b.max).Aggregate(Vector3.Max);
            var maxSize = Vector3.Distance(minPos, maxPos);
            
            // カメラの視野角(FOV)と最大サイズから距離を計算
            float fovRad = blockImageCamera.fieldOfView * Mathf.Deg2Rad;
            float distance = (maxSize * 0.5f) / Mathf.Tan(fovRad * 0.5f);
            
            blockImageCamera.transform.position = center - blockImageCamera.transform.forward * (distance * 0.8f);
            blockImageCamera.transform.LookAt(center);
            
            // カメラ背景をアルファ付き透明に設定
            blockImageCamera.clearFlags = CameraClearFlags.SolidColor;
            blockImageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            
            await UniTask.Yield(PlayerLoopTiming.Update);
            
            // アルファ付きのRenderTextureを使用
            var renderTexture = new RenderTexture(iconSize, iconSize, 24, RenderTextureFormat.ARGB32)
            {
                useMipMap = false,
                autoGenerateMips = false
            };
            
            blockImageCamera.targetTexture = renderTexture;
            blockImageCamera.Render();
            blockImageCamera.targetTexture = null;
            
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
        
        private float GetMaxBlockSize(List<(GameObject block,BlockObjectInfo blockObjectInfo)> createdBlocks)
        {
            float maxSize = 0f;
            foreach (var createdBlock in createdBlocks)
            {
                var renderers = createdBlock.block.GetComponentsInChildren<MeshRenderer>();
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