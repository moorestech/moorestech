using System.Collections.Generic;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     layoutを距離順に並べ近傍境界を算出
    ///     Orders layouts by distance and counts the near-field boundary
    /// </summary>
    public static class MapObjectLayoutDistanceOrder
    {
        // 起動待機を解除する近傍半径
        // Radius of the near field that releases the startup wait
        public const float NearFieldRadius = 150f;

        public static bool IsWithinNearField(Vector3 position, Vector3 origin)
        {
            return (position - origin).sqrMagnitude <= NearFieldRadius * NearFieldRadius;
        }

        public static List<Entry> Sort(IReadOnlyList<MapObjectLayoutMessagePack> layouts, Vector3 origin)
        {
            // 79,000件規模でも一度きりのソートなので距離は前計算して焼き込む
            // Even at the 79,000 scale this sorts once, so distances are precomputed and baked in
            var entries = new List<Entry>(layouts.Count);
            foreach (var layout in layouts)
            {
                var sqrDistance = (new Vector3(layout.X, layout.Y, layout.Z) - origin).sqrMagnitude;
                entries.Add(new Entry(layout, sqrDistance));
            }

            entries.Sort(static (a, b) => a.SqrDistance.CompareTo(b.SqrDistance));
            return entries;
        }

        public static int CountWithinRadius(List<Entry> sortedEntries, float radius)
        {
            // ソート済み前提で先頭から数え、半径ちょうどは近傍に含める
            // Assumes sorted input; counts from the head, a distance exactly at the radius counts as near
            var sqrRadius = radius * radius;
            for (var index = 0; index < sortedEntries.Count; index++)
            {
                if (sqrRadius < sortedEntries[index].SqrDistance) return index;
            }

            return sortedEntries.Count;
        }

        /// <summary>
        ///     距離を焼き込んだソート用エントリ
        ///     A sort entry with its distance baked in
        /// </summary>
        public readonly struct Entry
        {
            public readonly MapObjectLayoutMessagePack Layout;
            public readonly float SqrDistance;

            internal Entry(MapObjectLayoutMessagePack layout, float sqrDistance)
            {
                Layout = layout;
                SqrDistance = sqrDistance;
            }
        }
    }
}
