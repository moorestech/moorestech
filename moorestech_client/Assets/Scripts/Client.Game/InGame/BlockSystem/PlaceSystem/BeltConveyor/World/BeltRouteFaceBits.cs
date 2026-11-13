using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     セルの面ビット。bit = 面(0..3) + 4×高さ差（0:セルの高さ、1:セルの1つ上）。下り坂の入口は1つ上の高さで照合する
    ///     A cell's face bits: bit = face (0..3) + 4 x height offset (0: the cell's level, 1: one above); a down slope's entry matches one level up
    ///     辺から面ビットを立てる計算はここ1本。本番の世界とテストの箱が同じ並びになる
    ///     Raising face bits from an edge lives here alone, so the live world and the test box share one layout
    /// </summary>
    public static class BeltRouteFaceBits
    {
        public static int FaceBit(BlockDirection face, int heightOffset)
        {
            return 1 << ((int)face + BeltRouteDirection.DirectionCount * heightOffset);
        }

        // 辺に接する2セルの辺高さと1つ下に面ビットを立てる
        // Raise face bits on both edge cells at edge height and one below
        public static void Mark(BeltRouteEdge edge, Dictionary<Vector3Int, int> faceBits)
        {
            var positive = edge.PositiveCell;
            var forward = edge.NormalIsX ? new Vector3Int(1, 0, 0) : new Vector3Int(0, 0, 1);
            var positiveFace = edge.NormalIsX ? BlockDirection.East : BlockDirection.North;
            var negativeFace = BeltRouteDirection.Opposite(positiveFace);
            for (var heightOffset = 0; heightOffset <= 1; heightOffset++)
            {
                var level = positive.y - heightOffset;
                Raise(faceBits, new Vector3Int(positive.x - forward.x, level, positive.z - forward.z), positiveFace, heightOffset);
                Raise(faceBits, new Vector3Int(positive.x, level, positive.z), negativeFace, heightOffset);
            }
        }

        private static void Raise(Dictionary<Vector3Int, int> faceBits, Vector3Int cell, BlockDirection face, int heightOffset)
        {
            faceBits.TryGetValue(cell, out var bits);
            faceBits[cell] = bits | FaceBit(face, heightOffset);
        }
    }
}
