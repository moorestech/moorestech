using System.Collections.Generic;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     mapObjectのlayoutを基準点からの距離順に並べ、近傍境界の件数を算出する（ADR 0030）
    ///     Orders map object layouts by distance from an origin and counts the near-field boundary (ADR 0030)
    /// </summary>
    public static class MapObjectLayoutDistanceOrder
    {
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

            public Entry(MapObjectLayoutMessagePack layout, float sqrDistance)
            {
                Layout = layout;
                SqrDistance = sqrDistance;
            }
        }
    }
}
