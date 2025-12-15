using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Tutorial.TutorialBlock
{
    /// <summary>
    /// チュートリアル用プレビューブロックを生成する
    /// Creates tutorial preview blocks
    /// </summary>
    public static class TutorialPreviewBlockCreator
    {
        /// <summary>
        /// チュートリアル用プレビューオブジェクトを非同期で作成
        /// Asynchronously create tutorial preview object
        /// </summary>
        public static async UniTask<TutorialBlockPreviewObject> CreateAsync(BlockId blockId)
        {
            // ブロックの作成
            // Create block
            var block = ClientContext.BlockGameObjectPrefabContainer.CreateBlockGameObject(blockId, Vector3.zero, Quaternion.identity);
            block.SetActive(true);

            // チュートリアル用プレビューコンポーネントを追加して初期化
            // Add and initialize tutorial preview component
            var previewObject = block.AddComponent<TutorialBlockPreviewObject>();
            await previewObject.InitializeAsync(blockId);

            return previewObject;
        }
    }
}
