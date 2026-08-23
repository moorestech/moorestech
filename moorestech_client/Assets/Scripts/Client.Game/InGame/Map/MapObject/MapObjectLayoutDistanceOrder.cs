using System.Collections.Generic;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     layoutを距離順に並べ近傍境界まで一度に確定させる
    ///     Orders layouts by distance and settles the near-field boundary in the same call
    /// </summary>
    public static class MapObjectLayoutDistanceOrder
    {
        // 起動待機を解除する近傍半径
        // Radius of the near field that releases the startup wait
        public const float NearFieldRadius = 150f;

        public static NearFieldOrder SortNearFieldFirst(IReadOnlyList<MapObjectLayoutMessagePack> layouts, Vector3 origin)
        {
            // 大規模な一度きりのsortへ距離を焼く
            // Bake distances into the one large sort
            var entries = new List<Entry>(layouts.Count);
            foreach (var layout in layouts)
            {
                var sqrDistance = (new Vector3(layout.X, layout.Y, layout.Z) - origin).sqrMagnitude;
                entries.Add(new Entry(layout, sqrDistance));
            }

            entries.Sort(static (a, b) => a.SqrDistance.CompareTo(b.SqrDistance));

            // 件数算出はソート直後にここで閉じる。未ソート入力を数えて無音で近傍0件になる誤用を型から消す
            // Counting closes right after the sort here, so no caller can silently count an unsorted list into an empty near field
            return new NearFieldOrder(entries, CountWithinNearField());

            #region Internal

            int CountWithinNearField()
            {
                // 半径ちょうども近傍に含める
                // Include entries exactly on the radius
                var sqrRadius = NearFieldRadius * NearFieldRadius;
                for (var index = 0; index < entries.Count; index++)
                {
                    if (sqrRadius < entries[index].SqrDistance) return index;
                }

                return entries.Count;
            }

            #endregion
        }

        /// <summary>
        ///     距離順のlayoutと、その先頭から数えた近傍件数
        ///     Distance-ordered layouts plus the near-field count taken from their head
        /// </summary>
        public readonly struct NearFieldOrder
        {
            public readonly IReadOnlyList<Entry> Entries;
            public readonly int NearFieldCount;

            internal NearFieldOrder(IReadOnlyList<Entry> entries, int nearFieldCount)
            {
                Entries = entries;
                NearFieldCount = nearFieldCount;
            }
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
