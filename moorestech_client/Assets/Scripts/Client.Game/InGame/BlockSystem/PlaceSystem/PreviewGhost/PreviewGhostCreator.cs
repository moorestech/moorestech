using System.Threading;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.PreviewGhost
{
    /// <summary>
    /// チュートリアル用プレビューブロックを生成する
    /// Creates tutorial preview blocks
    /// </summary>
    public static class PreviewGhostCreator
    {
        /// <summary>
        /// チュートリアル用プレビューオブジェクトを非同期で作成する。生成中にキャンセルされた場合はnullを返す
        /// Asynchronously creates the tutorial preview object; returns null when cancelled mid-creation
        /// </summary>
        public static async UniTask<PreviewGhostObject> CreateAsync(BlockId blockId, CancellationToken cancellationToken)
        {
            var block = ClientContext.BlockGameObjectPrefabContainer.CreateBlockGameObject(blockId, Vector3.zero, Quaternion.identity);
            block.SetActive(true);

            // チュートリアル用プレビューコンポーネントを追加して初期化
            // Add and initialize tutorial preview component
            var previewObject = block.AddComponent<PreviewGhostObject>();
            await previewObject.InitializeAsync(blockId);

            // 生成中に対象が変わっていたら作りかけを畳んで何も返さない。呼び手が古い対象へ書き戻すのを防ぐ
            // A target changed mid-creation folds the half-built ghost and returns nothing, so the caller cannot write back a stale one
            if (cancellationToken.IsCancellationRequested)
            {
                previewObject.DestroyPreview();
                return null;
            }

            return previewObject;
        }
    }
}
