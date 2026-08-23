using System;
using System.Collections.Generic;
using Game.MapGeneration.Cache;

namespace Game.MapGeneration.Pipeline.Visual
{
    /// <summary>
    ///     Unityへ渡すRGBA8平面とその寸法を、不整合な組み合わせを作れない単位で保持する
    ///     Holds RGBA8 planes and their dimensions as one unit that cannot represent an inconsistent combination
    /// </summary>
    public sealed class TileAlphamap
    {
        public readonly IReadOnlyList<byte[]> Planes;
        public readonly int Resolution;
        public readonly int LayerCount;

        private TileAlphamap(IReadOnlyList<byte[]> planes, int resolution, int layerCount)
        {
            Planes = planes;
            Resolution = resolution;
            LayerCount = layerCount;
        }

        public static TileAlphamap Create(IReadOnlyList<byte[]> planes, int resolution, int layerCount)
        {
            // 寸法を先に確定し、平面長の計算もオーバーフローを不正入力として落とす
            // Settle dimensions first and reject overflow in the plane-length calculation as invalid input
            if (resolution <= 0) throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Resolution must be positive.");
            if (layerCount <= 0) throw new ArgumentOutOfRangeException(nameof(layerCount), layerCount, "Layer count must be positive.");
            if (planes == null) throw new ArgumentNullException(nameof(planes));

            var expectedPlaneCount =
                (layerCount + TerrainVisualCacheFormat.LayersPerAlphamapPlane - 1) /
                TerrainVisualCacheFormat.LayersPerAlphamapPlane;
            var expectedPlaneByteLength = checked(resolution * resolution * 4);
            if (planes.Count != expectedPlaneCount)
                throw new ArgumentException(
                    $"{layerCount} layers need {expectedPlaneCount} alphamap planes but {planes.Count} were given.",
                    nameof(planes));

            // 各平面はUnity互換RGBA8の全画素を欠けなく保持する
            // Every plane must contain every pixel of the Unity-compatible RGBA8 payload
            for (var planeIndex = 0; planeIndex < planes.Count; planeIndex++)
                if (planes[planeIndex] == null || planes[planeIndex].Length != expectedPlaneByteLength)
                    throw new ArgumentException(
                        $"Alphamap plane {planeIndex} must hold {expectedPlaneByteLength} bytes.", nameof(planes));

            return new TileAlphamap(new List<byte[]>(planes).AsReadOnly(), resolution, layerCount);
        }
    }
}
