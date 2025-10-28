using Client.Game.InGame.Context;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.PreviewController
{
    public class PreviewBlockCreator
    {
        public static BlockPreviewObject Create(BlockId blockId)
        {
            //ブロックの作成とセットアップをして返す
            var block = ClientContext.BlockGameObjectContainer.CreateBlockGameObject(blockId, Vector3.zero, Quaternion.identity);
            block.SetActive(true);
            
            var previewGameObject = block.AddComponent<BlockPreviewObject>();
            previewGameObject.SetTriggerCollider(true);
            previewGameObject.Initialize(blockId);
            
            return previewGameObject;
        }
    }
}