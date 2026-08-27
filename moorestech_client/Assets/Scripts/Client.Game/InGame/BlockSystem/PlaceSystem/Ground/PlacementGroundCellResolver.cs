using System.Collections.Generic;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     地形の高さから設置セルYを決める（ADR 0037）
    ///     Decides the placement cell Y from the terrain height (ADR 0037)
    /// </summary>
    public static class PlacementGroundCellResolver
    {
        // 整数の地表が誤差で1段浮くのを防ぐ
        // Keeps ground exactly on an integer from floating one cell
        private const float IntegerGroundTolerance = 0.001f;

        // 地形最高点を上回る最初のセルを返す
        // Returns the first cell above the terrain max height
        public static int ResolveCellY(float groundMaxHeight, int heightOffset)
        {
            return Mathf.CeilToInt(groundMaxHeight - IntegerGroundTolerance) + heightOffset;
        }

        // 占有範囲の地形最高点からYを決め直す
        // Re-decides Y from the footprint's terrain max height
        public static Vector3Int ResolveCellFromGround(Vector3Int cellPosition, BlockDirection blockDirection, Vector3Int blockSize, int heightOffset)
        {
            if (!SlopeBlockPlaceSystem.TryGetBlockFourCornerMaxHeight(cellPosition, blockDirection, blockSize, out var groundMaxHeight)) return cellPosition;

            return new Vector3Int(cellPosition.x, ResolveCellY(groundMaxHeight, heightOffset), cellPosition.z);
        }

        // ドラッグ列の各セルを真下の地形へ追従させる
        // Makes each cell of a drag run follow the terrain beneath it
        public static void ApplyGroundCellY(List<PlaceInfo> placeInfos, Vector3Int blockSize, int heightOffset)
        {
            foreach (var placeInfo in placeInfos)
            {
                placeInfo.Position = ResolveCellFromGround(placeInfo.Position, placeInfo.Direction, blockSize, heightOffset);
            }
        }
    }
}
