using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     地形めり込みの唯一の判定（ADR 0082）
    ///     The single terrain-sink judgement (ADR 0082)
    /// </summary>
    public static class PlacementTerrainSinkQuery
    {
        public static bool IsSunk(Vector3Int blockPos, BlockDirection blockDirection, Vector3Int blockSize)
        {
            // 地表が無い場所には埋まりようがない
            // Nothing can be buried where there is no ground
            // silent-ok: 地表欠落は設置Y解決側がGroundNotFoundとして理由行に出す / プレイヤーがツールチップで気づく
            if (!GroundHeightProbe.TryGetFootprintMaxGroundHeight(blockPos, blockDirection, blockSize, out var groundMax)) return false;
            return IsSunkAbove(blockPos.y, groundMax);
        }

        public static List<bool> DetectSunkCells(IReadOnlyList<PlaceInfo> placeInfos)
        {
            var sinks = new List<bool>(placeInfos.Count);
            foreach (var info in placeInfos)
            {
                var size = MasterHolder.BlockMaster.GetBlockMaster(info.BlockId).BlockSize;
                sinks.Add(IsSunk(info.Position, info.Direction, size));
            }
            return sinks;
        }

        // 設置Y決定と同じ最低セルより下なら埋まり
        // Buried if below the lowest cell the Y-resolution rule gives
        internal static bool IsSunkAbove(int blockBaseCellY, float groundMaxHeight)
        {
            return blockBaseCellY < PlacementGroundCellResolver.ResolveCellY(groundMaxHeight, 0);
        }
    }
}
