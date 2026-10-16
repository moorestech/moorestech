using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     地形めり込みの唯一の判定。底面から許容量より深く沈んだら埋まり（ADR 0082）
    ///     The single terrain-sink judgement: buried when sunk deeper than the allowance below the bottom (ADR 0082)
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

        // 設置Y決定と同じ規則で「沈みが許容量に収まる最低セル」を出し、底セルがそれより下なら埋まり
        // Derive the lowest cell within the sink allowance with the Y-resolution rule; a base cell below it is buried
        internal static bool IsSunkAbove(int blockBaseCellY, float groundMaxHeight)
        {
            return blockBaseCellY < PlacementGroundCellResolver.ResolveCellY(groundMaxHeight, 0);
        }
    }
}
