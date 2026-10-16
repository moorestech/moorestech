using Client.Game.InGame.Context;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController
{
    public class PreviewBlockCreator
    {
        public static BlockPreviewObject Create(BlockId blockId)
        {
            // 実Prefabからプレビュー表示を初期化
            // Initialize preview visuals from the real prefab.
            var block = ClientContext.BlockGameObjectPrefabContainer.CreateBlockGameObject(blockId, Vector3.zero, Quaternion.identity);
            block.SetActive(true);
            
            var preview = block.AddComponent<BlockPreviewObject>();
            preview.Initialize(blockId);
            DisableColliders(block);
            return preview;
        }

        internal static void DisableColliders(GameObject block)
        {
            // 初期化で有効化した子も含め当たり判定を全て切る（ADR 0081）
            // Disable every collider, including children activated by initialization (ADR 0081)
            foreach (var collider in block.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }
    }
}
