using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.CleanRoom.Info
{
    /// <summary>
    ///     密閉できていない理由。種別ごとの型のどれか1つだけになり、漏れの件数は Openings の型だけが持つ
    ///     Why the structure is not sealed; always exactly one per-kind type, and only the openings type carries the opening count
    /// </summary>
    public abstract class CleanRoomLeakReport
    {
        // 種別はこのファイルの4つの型に閉じる
        // The kinds are closed to the four types in this file
        private protected CleanRoomLeakReport()
        {
        }

        public static CleanRoomLeakReport Openings(IEnumerable<Vector3Int> cells)
        {
            var count = cells.Distinct().Count();
            if (count == 0) throw new ArgumentException("漏れセルが無いなら Openings ではない / Openings needs at least one cell", nameof(cells));
            return new CleanRoomOpeningsLeak(count);
        }

        public static CleanRoomLeakReport NoEnclosure()
        {
            return CleanRoomNoEnclosureLeak.Instance;
        }

        public static CleanRoomLeakReport TooLarge()
        {
            return CleanRoomTooLargeLeak.Instance;
        }

        public static CleanRoomLeakReport DetectionPending()
        {
            return CleanRoomDetectionPendingLeak.Instance;
        }
    }

    public sealed class CleanRoomOpeningsLeak : CleanRoomLeakReport
    {
        public int TotalOpeningCount { get; }

        internal CleanRoomOpeningsLeak(int totalOpeningCount)
        {
            TotalOpeningCount = totalOpeningCount;
        }
    }

    // 種の候補のどれも境界に囲まれていない
    // None of the seed candidates is enclosed by boundaries
    public sealed class CleanRoomNoEnclosureLeak : CleanRoomLeakReport
    {
        public static readonly CleanRoomNoEnclosureLeak Instance = new CleanRoomNoEnclosureLeak();

        private CleanRoomNoEnclosureLeak()
        {
        }
    }

    // 塗りが部屋の上限体積を超えた
    // The fill exceeded the room volume cap
    public sealed class CleanRoomTooLargeLeak : CleanRoomLeakReport
    {
        public static readonly CleanRoomTooLargeLeak Instance = new CleanRoomTooLargeLeak();

        private CleanRoomTooLargeLeak()
        {
        }
    }

    // 穴は無いが、まだ部屋として検出されていない
    // No hole exists, but the room has not been detected yet
    public sealed class CleanRoomDetectionPendingLeak : CleanRoomLeakReport
    {
        public static readonly CleanRoomDetectionPendingLeak Instance = new CleanRoomDetectionPendingLeak();

        private CleanRoomDetectionPendingLeak()
        {
        }
    }
}
