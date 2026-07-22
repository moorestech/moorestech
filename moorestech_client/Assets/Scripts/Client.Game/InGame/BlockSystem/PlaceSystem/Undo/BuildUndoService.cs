using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Input;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     Ctrl+Zで直前の建築操作を取り消す。UIステートからManualUpdateで毎フレーム駆動される
    ///     Undo the latest build operation on Ctrl+Z; driven every frame from UI states via ManualUpdate
    /// </summary>
    public class BuildUndoService
    {
        private readonly BuildOperationHistory _history;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private bool _isUndoing;

        public BuildUndoService(BuildOperationHistory history, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _history = history;
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        public void ManualUpdate()
        {
            if (!IsUndoKeyPressed()) return;
            if (_isUndoing) return;
            if (!_history.TryPop(out var record)) return;

            UndoAsync(record).Forget();

            #region Internal

            static bool IsUndoKeyPressed()
            {
                var modifierHeld = HybridInput.GetKey(KeyCode.LeftControl) || HybridInput.GetKey(KeyCode.LeftCommand);
                return modifierHeld && HybridInput.GetKeyDown(KeyCode.Z);
            }

            #endregion
        }

        private async UniTask UndoAsync(IBuildOperationRecord record)
        {
            _isUndoing = true;
            // ネットワーク送受信（外部境界）の例外でも再入フラグを必ず復帰させる（try-catch原則禁止の境界例外条項）
            // Guarantee the re-entrancy flag resets even on network-boundary exceptions (boundary exemption of the no-try-catch rule)
            try
            {
                switch (record)
                {
                    case PlaceOperationRecord placeRecord:
                        await UndoPlaceOperation(placeRecord);
                        break;
                    case RemoveOperationRecord removeRecord:
                        UndoRemoveOperation(removeRecord);
                        break;
                }
            }
            finally
            {
                _isUndoing = false;
            }

            #region Internal

            async UniTask UndoPlaceOperation(PlaceOperationRecord placeRecord)
            {
                // 同座標同BlockIdの現存セルだけを撤去する（設置失敗・他者変更セルの誤爆防止）
                // Remove only cells still holding the same BlockId (avoids nuking failed or replaced cells)
                var cells = placeRecord.SelectUndoableCells(GetBlockIdAt);
                foreach (var position in cells)
                {
                    await ClientContext.VanillaApi.Response.BlockRemove(position, CancellationToken.None);
                }
            }

            void UndoRemoveOperation(RemoveOperationRecord removeRecord)
            {
                // 占有範囲が空いているセルだけを1バッチで再設置する（CreateParamsは復元不可のため空）
                // Re-place only cells whose footprint is unoccupied, in one batch (CreateParams cannot be restored, so empty)
                var cells = removeRecord.SelectReplaceableCells(IsFootprintOccupied);
                if (cells.Count == 0) return;

                var placeInfos = new List<PlaceInfo>(cells.Count);
                foreach (var cell in cells)
                {
                    placeInfos.Add(new PlaceInfo
                    {
                        Position = cell.Position,
                        Direction = cell.Direction,
                        VerticalDirection = ToVerticalDirection(cell.Direction),
                        BlockId = cell.BlockId,
                        Placeable = true,
                    });
                }
                ClientContext.VanillaApi.SendOnly.PlaceBlock(placeInfos);
            }

            BlockId? GetBlockIdAt(Vector3Int position)
            {
                if (!_blockGameObjectDataStore.TryGetBlockGameObject(position, out var blockGameObject)) return null;
                return blockGameObject.BlockId;
            }

            bool IsFootprintOccupied(RemovedBlockInfo removed)
            {
                // 辞書キーはオリジン座標のみのため、マルチセルブロックとの重なりは占有範囲同士で判定する
                // The dictionary keys origins only, so overlap with multi-cell blocks needs a footprint check
                var blockSize = MasterHolder.BlockMaster.GetBlockMaster(removed.BlockId).BlockSize;
                var positionInfo = new BlockPositionInfo(removed.Position, removed.Direction, blockSize);
                return _blockGameObjectDataStore.IsOverlapPositionInfo(positionInfo);
            }

            static BlockVerticalDirection ToVerticalDirection(BlockDirection direction)
            {
                return direction switch
                {
                    BlockDirection.UpNorth or BlockDirection.UpEast or BlockDirection.UpSouth or BlockDirection.UpWest => BlockVerticalDirection.Up,
                    BlockDirection.DownNorth or BlockDirection.DownEast or BlockDirection.DownSouth or BlockDirection.DownWest => BlockVerticalDirection.Down,
                    _ => BlockVerticalDirection.Horizontal,
                };
            }

            #endregion
        }
    }
}
