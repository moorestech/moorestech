using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.Block.Preview
{
    internal static class BlockPreviewOnlyObjects
    {
        internal static List<IPreviewOnlyObject> Initialize(GameObject block, BlockId blockId)
        {
            // 実ブロックの表示専用部品を初期化して隠す
            // Initialize and hide the real block's preview-only visuals
            var previewOnlyObjects = block.GetComponentsInChildren<IPreviewOnlyObject>(true).ToList();
            foreach (var previewOnlyObject in previewOnlyObjects)
            {
                previewOnlyObject.Initialize(blockId);
                previewOnlyObject.SetActive(false);
            }
            return previewOnlyObjects;
        }
    }
}
