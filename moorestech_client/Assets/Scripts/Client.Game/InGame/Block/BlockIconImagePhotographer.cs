using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class BlockIconImagePhotographer : MonoBehaviour
    {
        [SerializeField] private int iconSize = 512;
        [SerializeField] Camera cameraPrefab;

        public async UniTask<List<Texture2D>> TakeBlockIconImages(List<BlockPrefabInfo> blockObjectInfos)
        {
            var targets = blockObjectInfos.Select(info => (info.BlockObjectPrefab, info.BlockMasterElement.Name)).ToList();
            return await TakeIconImages(targets);
        }

        public async UniTask<List<Texture2D>> TakeIconImages(List<(GameObject prefab, string debugName)> targets)
        {
            var createdObjects = new List<(GameObject instance, string debugName)>();

            foreach (var target in targets)
            {
                var instance = Instantiate(target.prefab, transform);
                createdObjects.Add((instance, target.debugName));
            }

            // 撮影対象を一直線に並べる
            // Line up the subjects in a row
            var maxSize = GetMaxObjectSize(createdObjects);
            var spacing = maxSize * 2f;
            for (int i = 0; i < createdObjects.Count; i++)
            {
                var createdObject = createdObjects[i];
                createdObject.instance.transform.position = new Vector3(i * spacing, 0f, 0f);
                createdObject.instance.transform.rotation = Quaternion.identity;
                createdObject.instance.transform.localScale = Vector3.one;
            }

            // 全ての対象でアイコンを取得
            // Capture the icon for every subject
            var tasks = new List<UniTask<Texture2D>>();
            foreach (var createdObject in createdObjects)
            {
                tasks.Add(GetIcon(createdObject.instance, createdObject.debugName));
            }

            var result = await UniTask.WhenAll(tasks);

            return result.ToList();
        }

        private async UniTask<Texture2D> GetIcon(GameObject target, string debugName)
        {
            // 対象の重心とバウンディングを取得
            var bounds = target.GetComponentsInChildren<Renderer>().Select(b => b.bounds).ToList();
            if (bounds.Count == 0)
            {
                throw new System.Exception("撮影対象にメッシュレンダラーがありませんでした:" + target.name + " " + debugName);
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
            blockImageCamera.backgroundColor = Color.white;

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
            Destroy(target);
            Destroy(renderTexture);

            return texture;
        }

        private float GetMaxObjectSize(List<(GameObject instance, string debugName)> createdObjects)
        {
            float maxSize = 0f;
            foreach (var createdObject in createdObjects)
            {
                var renderers = createdObject.instance.GetComponentsInChildren<MeshRenderer>();
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
