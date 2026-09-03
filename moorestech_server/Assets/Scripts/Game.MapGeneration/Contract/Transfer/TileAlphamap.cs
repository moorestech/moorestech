using System;
using System.Collections.Generic;

namespace Game.MapGeneration.Transfer
{
    /// <summary>
    ///     Unityへ渡すRGBA8平面とその寸法を、不整合な組み合わせを作れない単位で保持する
    ///     RGBA8のパッキング規則（4層/面・1画素4バイト）もここが正本で、保存形式側はこれを参照する
    ///     Holds RGBA8 planes and their dimensions as one unit that cannot represent an inconsistent combination
    ///     The RGBA8 packing rule (four layers per plane, four bytes per pixel) is owned here; the storage format refers to it
    /// </summary>
    public sealed class TileAlphamap
    {
        // Unity互換RGBA8平面（4層/面）
        // Unity-compatible RGBA8 planes, four layers each.
        public const int LayersPerAlphamapPlane = 4;
        public const int AlphamapPlaneBytesPerPixel = 4;

        public readonly IReadOnlyList<ReadOnlyMemory<byte>> Planes;
        public readonly int Resolution;
        public readonly int LayerCount;

        private TileAlphamap(IReadOnlyList<ReadOnlyMemory<byte>> planes, int resolution, int layerCount)
        {
            Planes = planes;
            Resolution = resolution;
            LayerCount = layerCount;
        }

        // レイヤー数から平面数を出す唯一の場所。UnityのalphamapTextureCountと同じ切り上げ規則
        // The single place deriving the plane count from the layer count, with Unity's own alphamapTextureCount rounding
        public static int AlphamapPlaneCount(int layerCount)
        {
            return (layerCount + LayersPerAlphamapPlane - 1) / LayersPerAlphamapPlane;
        }

        // 外部が持ち続ける配列は写し取る。後から書き換えられても保持内容が動かない
        // Arrays the caller keeps are copied, so a later write on their side cannot move what is held here
        public static TileAlphamap Create(IReadOnlyList<byte[]> planes, int resolution, int layerCount)
        {
            var expectedPlaneByteLength = ValidatePlanes(planes, resolution, layerCount);

            var ownedPlanes = new ReadOnlyMemory<byte>[planes.Count];
            for (var planeIndex = 0; planeIndex < planes.Count; planeIndex++)
            {
                var ownedPlane = new byte[expectedPlaneByteLength];
                Buffer.BlockCopy(planes[planeIndex], 0, ownedPlane, 0, expectedPlaneByteLength);
                ownedPlanes[planeIndex] = ownedPlane;
            }

            return new TileAlphamap(Array.AsReadOnly(ownedPlanes), resolution, layerCount);
        }

        // その場で確保して渡し切る配列は所有権ごと受け取る。タイル毎に数十MBの防御コピーを作らないため
        // Arrays allocated on the spot and handed over are taken by ownership, sparing a defensive copy of tens of MB per tile
        public static TileAlphamap CreateOwning(byte[][] planes, int resolution, int layerCount)
        {
            ValidatePlanes(planes, resolution, layerCount);

            var ownedPlanes = new ReadOnlyMemory<byte>[planes.Length];
            for (var planeIndex = 0; planeIndex < planes.Length; planeIndex++) ownedPlanes[planeIndex] = planes[planeIndex];
            return new TileAlphamap(Array.AsReadOnly(ownedPlanes), resolution, layerCount);
        }

        private static int ValidatePlanes(IReadOnlyList<byte[]> planes, int resolution, int layerCount)
        {
            // 寸法を先に確定し、平面長の計算もオーバーフローを不正入力として落とす
            // Settle dimensions first and reject overflow in the plane-length calculation as invalid input
            if (resolution <= 0) throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Resolution must be positive.");
            if (layerCount <= 0) throw new ArgumentOutOfRangeException(nameof(layerCount), layerCount, "Layer count must be positive.");
            if (planes == null) throw new ArgumentNullException(nameof(planes));

            var expectedPlaneCount = AlphamapPlaneCount(layerCount);
            var expectedPlaneByteLength = checked(resolution * resolution * AlphamapPlaneBytesPerPixel);
            if (planes.Count != expectedPlaneCount)
                throw new ArgumentException(
                    $"{layerCount} layers need {expectedPlaneCount} alphamap planes but {planes.Count} were given.",
                    nameof(planes));

            // 各平面はUnity互換RGBA8の全画素を欠けなく保持する
            // Every plane must contain every pixel of the Unity-compatible RGBA8 payload
            for (var planeIndex = 0; planeIndex < planes.Count; planeIndex++)
            {
                var sourcePlane = planes[planeIndex];
                if (sourcePlane == null || sourcePlane.Length != expectedPlaneByteLength)
                    throw new ArgumentException(
                        $"Alphamap plane {planeIndex} must hold {expectedPlaneByteLength} bytes.", nameof(planes));
            }

            return expectedPlaneByteLength;
        }
    }
}
