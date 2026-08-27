using System.Collections.Generic;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     地形の高さから設置セルYを決める。埋まるくらいなら浮かせる（ADR 0037）
    ///     Decides the placement cell Y from the terrain height, floating rather than sinking (ADR 0037)
    /// </summary>
    public static class PlacementGroundCellResolver
    {
        // 整数ちょうどの地表が探査誤差で1段浮くのを防ぐ許容量
        // Tolerance that keeps ground exactly on an integer from floating one cell due to probe noise
        private const float IntegerGroundTolerance = 0.001f;

        // 占有範囲の地形最高点を上回る最初のセルを返す。手動オフセットはその後に加算する
        // Returns the first cell above the footprint's terrain max height, then adds the manual offset
        public static int ResolveCellY(float groundMaxHeight, int heightOffset)
        {
            return Mathf.CeilToInt(groundMaxHeight - IntegerGroundTolerance) + heightOffset;
        }

        // セルの占有範囲の地形最高点からYを決め直す。地表が取れなければ元のセルを返す
        // Re-decides Y from the footprint's terrain max height; returns the original cell when no ground is found
        public static Vector3Int ResolveCellFromGround(Vector3Int cellPosition, BlockDirection blockDirection, Vector3Int blockSize, int heightOffset)
        {
            if (!SlopeBlockPlaceSystem.TryGetBlockFourCornerMaxHeight(cellPosition, blockDirection, blockSize, out var groundMaxHeight)) return cellPosition;

            return new Vector3Int(cellPosition.x, ResolveCellY(groundMaxHeight, heightOffset), cellPosition.z);
        }

        // ドラッグ列の各セルを自分の真下の地形へ追従させる。開始セルのYコピーをここで打ち消す
        // Makes each cell of a drag run follow the terrain beneath it, cancelling the start cell's Y copy
        public static void ApplyGroundCellY(List<PlaceInfo> placeInfos, Vector3Int blockSize, int heightOffset)
        {
            foreach (var placeInfo in placeInfos)
            {
                placeInfo.Position = ResolveCellFromGround(placeInfo.Position, placeInfo.Direction, blockSize, heightOffset);
            }
        }
    }
}
