using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.BeltConveyor;
using Client.Game.InGame.Context;
using Game.Entity.Interface;
using MessagePack;
using UnityEngine;

namespace Client.Game.InGame.Entity.Object.Util
{
    /// <summary>
    /// ベルトコンベア上のアイテムエンティティの位置を計算するユーティリティ
    /// Utility for calculating item entity position on belt conveyor
    /// </summary>
    public static class BeltConveyorItemPositionCalculator
    {
        /// <summary>
        /// ブロックのBeltConveyorItemPathを使用して位置を計算
        /// Calculate position using block's BeltConveyorItemPath
        /// </summary>
        public static Vector3 CalculatePosition(byte[] data)
        {
            var state = MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(data);
            
            Vector3Int blockPos = new(state.BlockPosX, state.BlockPosY, state.BlockPosZ);
            var remainingPercent = state.RemainingPercent;
            Guid? sourceConnectorGuid = state.SourceConnectorGuid;
            Guid? goalConnectorGuid = state.GoalConnectorGuid;

            
            // ブロックGameObjectの取得を試みる
            // Try to get block GameObject
            if (!ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(blockPos, out var blockGameObject))
            {
                Debug.LogError($"BeltConveyorItemPositionCalculator: Failed to get BlockGameObject at position {blockPos}");
                return blockPos;
            }
            
            // BeltConveyorItemPathコンポーネントを取得
            // Get BeltConveyorItemPath component
            var itemPath = blockGameObject.GetComponent<BeltConveyorItemPath>();
            if (itemPath == null)
            {
                Debug.LogError($"BeltConveyorItemPositionCalculator: BeltConveyorItemPath component not found on BlockGameObject at position {blockPos}");
                return blockPos;
            }
            
            // ConnectorGuidを文字列に変換（nullの場合は空文字列）
            // Convert ConnectorGuid to string (empty string if null)
            var startId = sourceConnectorGuid?.ToString() ?? "";
            var goalId = goalConnectorGuid?.ToString() ?? "";
            
            // パスから位置を計算
            // Calculate position from path
            return itemPath.GetWorldPosition(startId, goalId, remainingPercent);
        }
    }
}
