using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.Block.Preview
{
    internal static class BlockPreviewOnlyObjects
    {
        internal static List<IPreviewOnlyObject> Initialize(GameObject block, BlockId blockId, bool active)
        {
            // 表示専用部品の初期化と表示切替
            // Initialize preview-only visuals and set visibility
            var previewOnlyObjects = block.GetComponentsInChildren<IPreviewOnlyObject>(true).ToList();
            foreach (var previewOnlyObject in previewOnlyObjects)
            {
                previewOnlyObject.Initialize(blockId);
                previewOnlyObject.SetActive(active);
            }
            return previewOnlyObjects;
        }
    }
}
