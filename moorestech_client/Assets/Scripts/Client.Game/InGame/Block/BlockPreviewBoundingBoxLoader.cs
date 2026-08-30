using Client.Common.Asset;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    /// <summary>
    ///     設置プレビュー用のバウンディングボックスを非同期で読み込み、ブロック配下に組み付ける
    ///     Loads the placement preview bounding box asynchronously and attaches it under the block
    /// </summary>
    internal static class BlockPreviewBoundingBoxLoader
    {
        private const string PreviewBoundingBoxAddressablePath = "Vanilla/Block/Util/BlockPreviewBoundingBox";

        public static async UniTask<IPreviewOnlyObject> LoadAsync(BlockGameObject owner, BlockMasterElement master, BlockPositionInfo posInfo)
        {
            var previewBoundingBoxPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(PreviewBoundingBoxAddressablePath);
            var previewBoundingBoxObj = Object.Instantiate(previewBoundingBoxPrefab, owner.transform);
            previewBoundingBoxObj.GetComponent<BlockPreviewBoundingBox>().SetBoundingBox(master.BlockSize, posInfo.BlockDirection, owner);

            // 生成直後は非表示。設置プレビュー時だけ点灯する
            // Starts hidden and lights up only during placement preview
            var previewOnlyObject = previewBoundingBoxObj.GetComponent<PreviewOnlyObject>();
            previewOnlyObject.Initialize(owner.BlockId);
            previewOnlyObject.SetActive(false);
            return previewOnlyObject;
        }
    }
}
