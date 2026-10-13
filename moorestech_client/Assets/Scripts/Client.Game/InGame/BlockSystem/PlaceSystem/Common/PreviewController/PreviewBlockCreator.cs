using Client.Game.InGame.Context;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController
{
    public class PreviewBlockCreator
    {
        public static BlockPreviewObject Create(BlockId blockId)
        {
            // 実Prefabを複製してプレビューの表示を初期化する
            // Clone the real prefab and initialize preview visuals.
            var block = ClientContext.BlockGameObjectPrefabContainer.CreateBlockGameObject(blockId, Vector3.zero, Quaternion.identity);
            block.SetActive(true);
            
            var preview = block.AddComponent<BlockPreviewObject>();
            preview.Initialize(blockId);
            // 初期化が有効化した子も含めて物理設定を確定する
            // Finalize physics settings after preview-only children activate.
            preview.ConfigurePlacementColliders();
            return preview;
        }
    }
}