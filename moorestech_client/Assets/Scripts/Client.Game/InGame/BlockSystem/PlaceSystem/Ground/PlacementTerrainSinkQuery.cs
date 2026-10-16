using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     地形めり込みの唯一の判定。底から1セルより深く沈んだら埋まり（ADR 0081）
    ///     The single terrain-sink judgement: buried when sunk deeper than one cell from the bottom (ADR 0081)
    /// </summary>
    public static class PlacementTerrainSinkQuery
    {
        public static bool IsSunk(Vector3Int blockPos, BlockDirection blockDirection, Vector3Int blockSize)
        {
            // 地表が無い場所には埋まりようがない
            // Nothing can be buried where there is no ground
            // silent-ok: 地表欠落は設置Y解決側がGroundNotFoundとして理由行に出す / プレイヤーがツールチップで気づく
            if (!GroundHeightProbe.TryGetFootprintMaxGroundHeight(blockPos, blockDirection, blockSize, out var groundMax, out var step)) return false;
            return IsSunkAbove(blockPos.y, groundMax, step);
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

        // 設置Y決定と同じ許容誤差で「地形が自然に置くセル」を出し、底セルより上なら埋まり
        // Derive the terrain's natural cell with the same tolerance as Y resolution; above the base cell means buried
        internal static bool IsSunkAbove(int blockBaseCellY, float groundMaxHeight, float heightQuantizationStep)
        {
            return blockBaseCellY < PlacementGroundCellResolver.ResolveCellY(groundMaxHeight, heightQuantizationStep, 0);
        }
    }
}
