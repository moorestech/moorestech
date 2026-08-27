using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.GearConnect
{
    /// <summary>
    ///     通常の設置プレビュー中、歯車系ブロックのカーソルセルについて接続先を解決し線で示す（常設・チュートリアル非依存）
    ///     During normal placement preview, resolves and draws the gear connections of the cursor cell (always on, not tutorial-bound)
    /// </summary>
    public class GearConnectPreview
    {
        private readonly BlockGameObjectDataStore _blockDataStore;
        private readonly GearConnectPreviewRenderer _renderer = new();

        // 解決結果はカーソルの設置内容とワールドの両方が変わらない限り同じなので、変化時だけ解き直す
        // The resolution only changes when the cursor placement or the world does, so it is recomputed on change alone
        private (BlockId blockId, Vector3Int position, BlockDirection direction)? _resolvedFor;
        private bool _worldChanged = true;

        public GearConnectPreview(BlockGameObjectDataStore blockDataStore)
        {
            _blockDataStore = blockDataStore;

            // 隣接ブロックの増減で接続先が変わるため、設置と撤去で解決結果を捨てる
            // Placing or removing a neighbour changes the partners, so both drop the cached resolution
            _blockDataStore.OnBlockPlaced.Subscribe(_ => _worldChanged = true);
            _blockDataStore.OnBlockRemoved.Subscribe(_ => _worldChanged = true);
        }

        public void Apply(List<PlaceInfo> placeInfos, BlockId blockId, int cursorIndex)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (blockMaster.BlockParam is not IGearConnectors || cursorIndex < 0 || placeInfos.Count <= cursorIndex)
            {
                Hide();
                return;
            }

            var cursor = placeInfos[cursorIndex];
            var resolveKey = (blockId, cursor.Position, cursor.Direction);
            if (!_worldChanged && _resolvedFor == resolveKey) return;

            _worldChanged = false;
            _resolvedFor = resolveKey;

            var selfPositionInfo = new BlockPositionInfo(cursor.Position, cursor.Direction, blockMaster.BlockSize);
            _renderer.Show(GearConnectPairResolver.Resolve(blockId, selfPositionInfo, CollectNeighbours(selfPositionInfo)));

            #region Internal

            // 占有範囲を1セル膨らませた箱と重なるブロックを候補にする。歯車は隣接セルとしか繋がらない
            // Candidates are blocks overlapping the footprint expanded by one cell; gears only mesh with adjacent cells
            // 原点引きの座標検索では風車や粉砕機のような複数セルブロックを取り逃すため、占有範囲同士で交差を見る
            // An origin-keyed cell lookup would miss multi-cell blocks such as the windmill or the crusher, so footprints are intersected instead
            List<(BlockId, BlockPositionInfo)> CollectNeighbours(BlockPositionInfo positionInfo)
            {
                var min = positionInfo.MinPos - Vector3Int.one;
                var max = positionInfo.MaxPos + Vector3Int.one;

                var neighbours = new List<(BlockId, BlockPositionInfo)>();
                foreach (var block in _blockDataStore.BlockGameObjectDictionary.Values)
                {
                    var blockPosInfo = block.BlockPosInfo;
                    if (blockPosInfo.MaxPos.x < min.x || max.x < blockPosInfo.MinPos.x) continue;
                    if (blockPosInfo.MaxPos.y < min.y || max.y < blockPosInfo.MinPos.y) continue;
                    if (blockPosInfo.MaxPos.z < min.z || max.z < blockPosInfo.MinPos.z) continue;
                    neighbours.Add((block.BlockId, blockPosInfo));
                }
                return neighbours;
            }

            #endregion
        }

        public void Hide()
        {
            // 非表示のまま同じセルへ戻ったときに線が消えたままにならないよう、解決済みの記憶も落とす
            // Drop the cached resolution too, so returning to the same cell after a hide redraws the lines
            _resolvedFor = null;
            _renderer.Hide();
        }
    }
}
