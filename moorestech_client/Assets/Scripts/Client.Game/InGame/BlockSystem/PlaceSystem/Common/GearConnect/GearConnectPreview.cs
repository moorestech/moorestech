using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
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

        public GearConnectPreview(BlockGameObjectDataStore blockDataStore)
        {
            _blockDataStore = blockDataStore;
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
            _renderer.Hide();
        }
    }
}
