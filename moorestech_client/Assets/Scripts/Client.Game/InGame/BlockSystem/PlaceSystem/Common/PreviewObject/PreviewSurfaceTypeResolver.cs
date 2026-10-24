using System;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject
{
    /// <summary>
    ///     ローカル面をワールド面へ変換
    ///     Converts local faces into world faces
    /// </summary>
    public static class PreviewSurfaceTypeResolver
    {
        public static PreviewSurfaceType Resolve(BlockDirection blockDirection, Vector3Int localOutwardNormal)
        {
            // 回転は軸に沿った90度単位なので、回した法線は6方向の単位ベクトルのどれかになる
            // Rotations are axis-aligned 90-degree steps, so the rotated normal is one of the six unit vectors
            var worldNormal = blockDirection.ConvertLocalCell(localOutwardNormal);
            if (worldNormal == new Vector3Int(0, 0, -1)) return PreviewSurfaceType.YX_Origin;
            if (worldNormal == new Vector3Int(0, 0, 1)) return PreviewSurfaceType.YX_Z;
            if (worldNormal == new Vector3Int(-1, 0, 0)) return PreviewSurfaceType.YZ_Origin;
            if (worldNormal == new Vector3Int(1, 0, 0)) return PreviewSurfaceType.YZ_X;
            if (worldNormal == new Vector3Int(0, -1, 0)) return PreviewSurfaceType.XZ_Origin;
            if (worldNormal == new Vector3Int(0, 1, 0)) return PreviewSurfaceType.XZ_Y;
            throw new ArgumentOutOfRangeException(nameof(localOutwardNormal), localOutwardNormal, "outward normal must be an axis-aligned unit vector");
        }
    }
}
